using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskVault.UI.Services.Document;

internal enum TemporaryDocumentArtifactLifecycleState
{
    Preparing = 1,
    ExternalConsumerMayNeedArtifact = 2,
    CleanupEligible = 3,
    CreationPending = 4
}

internal sealed record TemporaryDocumentArtifactOwnershipRecord(
    int Version,
    Guid ArtifactId,
    string ArtifactFileName,
    TemporaryDocumentArtifactLifecycleState State);

/// <summary>
/// Owns the durable lifecycle record and exclusive process lease for one
/// temporary document artifact.
/// </summary>
/// <remarks>
/// The record is append-only. A state transition is considered committed
/// only after its complete record has been flushed to disk.
///
/// FileShare.Delete permits the owning process to remove its own metadata
/// file while its lease is held. Other read/write opens remain excluded.
/// </remarks>
internal sealed class TemporaryDocumentArtifactOwnership : IDisposable
{
    private const int CurrentVersion = 1;
    private const int MaximumMetadataSizeBytes = 4096;
    private const int MetadataBufferSize = 4096;

    private const FileSystemRights MetadataFileRights =
        FileSystemRights.Read |
        FileSystemRights.Write |
        FileSystemRights.Delete |
        FileSystemRights.Synchronize;

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

    private readonly Lock _sync = new();

    private readonly FileStream _leaseStream;

    private TemporaryDocumentArtifactLifecycleState _currentState;

    private bool _disposed;
    private bool _faulted;

    private TemporaryDocumentArtifactOwnership(
        Guid artifactId,
        string artifactFileName,
        string metadataPath,
        FileStream leaseStream,
        TemporaryDocumentArtifactLifecycleState initialState)
    {
        ArtifactId = artifactId;
        ArtifactFileName = artifactFileName;
        MetadataPath = metadataPath;
        _leaseStream = leaseStream;
        _currentState = initialState;
    }

    public Guid ArtifactId { get; }

    public string ArtifactFileName { get; }

    public string MetadataPath { get; }

    public TemporaryDocumentArtifactLifecycleState CurrentState
    {
        get
        {
            lock (_sync)
            {
                return _currentState;
            }
        }
    }

    /// <summary>
    /// Creates the metadata file exclusively and establishes its initial
    /// CreationPending state before attempting artifact creation.
    /// </summary>
    internal static TemporaryDocumentArtifactOwnership Create(
        string directoryPath,
        Guid artifactId,
        string artifactFileName,
        FileSecurity fileSecurity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactFileName);
        ArgumentNullException.ThrowIfNull(fileSecurity);

        if (artifactId == Guid.Empty ||
            !IsValidArtifactFileName(artifactId, artifactFileName))
        {
            throw new ArgumentException(
                "The temporary artifact ownership identity is invalid.");
        }

        // Ownership metadata is unique to the complete artifact filename.
        // Different extensions therefore retain independent metadata files.
        string metadataPath =
            Path.Combine(
                directoryPath,
                $"{artifactFileName}.owner.json");

        FileStream leaseStream =
            new FileInfo(metadataPath).Create(
                FileMode.CreateNew,
                MetadataFileRights,
                FileShare.Delete,
                MetadataBufferSize,
                FileOptions.WriteThrough,
                fileSecurity);

        var ownership = new TemporaryDocumentArtifactOwnership(
            artifactId,
            artifactFileName,
            metadataPath,
            leaseStream,
            TemporaryDocumentArtifactLifecycleState.CreationPending);

        try
        {
            ownership.AppendRecord(
                TemporaryDocumentArtifactLifecycleState.CreationPending);

            return ownership;
        }
        catch
        {
            // Preserve the original metadata initialization exception.
            try
            {
                ownership.Dispose();
            }
            catch
            {
                // The original initialization failure takes precedence.
            }

            throw;
        }
    }

    /// <summary>
    /// Opens an existing ownership record for recovery and acquires its exclusive process lease.
    /// </summary>
    /// <param name="directoryPath">The dedicated temporary-document namespace.</param>
    /// <param name="metadataPath">The ownership metadata file to inspect.</param>
    /// <returns>The ownership object holding the recovery lease.</returns>
    internal static TemporaryDocumentArtifactOwnership OpenForRecovery(
        string directoryPath,
        string metadataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);

        string fullDirectoryPath =
            Path.GetFullPath(directoryPath);

        string fullMetadataPath =
            Path.GetFullPath(metadataPath);

        string? metadataDirectory =
            Path.GetDirectoryName(fullMetadataPath);

        if (!string.Equals(
            metadataDirectory,
            fullDirectoryPath,
            StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullMetadataPath).EndsWith(
                ".owner.json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The temporary artifact ownership metadata path is invalid.");
        }

        // The original owner opens this file with FileShare.Delete only.
        // Requesting read/write access with the same share mode therefore
        // succeeds only when no other process currently owns the lease.
        FileStream leaseStream = new(
            fullMetadataPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Delete,
            MetadataBufferSize,
            FileOptions.WriteThrough);

        try
        {
            TemporaryDocumentArtifactOwnershipRecord record =
                ReadLatestRecord(leaseStream);

            string expectedMetadataPath =
                Path.Combine(
                    fullDirectoryPath,
                    $"{record.ArtifactFileName}.owner.json");

            if (!string.Equals(
                expectedMetadataPath,
                fullMetadataPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The ownership metadata filename does not match its artifact identity.");
            }

            var ownership = new TemporaryDocumentArtifactOwnership(
                record.ArtifactId,
                record.ArtifactFileName,
                fullMetadataPath,
                leaseStream,
                record.State);

            return ownership;
        }
        catch
        {
            // Preserve the original validation or initialization exception.
            try
            {
                leaseStream.Dispose();
            }
            catch
            {
                // Disposal failure must not mask the original exception.
            }

            throw;
        }
    }

    /// <summary>
    /// Marks the artifact as potentially required by an external consumer.
    /// </summary>
    public void MarkExternalConsumerMayNeedArtifact()
    {
        TransitionTo(
            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact);
    }

    /// <summary>
    /// Marks the artifact as safe for owner-controlled cleanup.
    /// </summary>
    public void MarkCleanupEligible()
    {
        TransitionTo(
            TemporaryDocumentArtifactLifecycleState.CleanupEligible);
    }

    /// <summary>
    /// Confirms that the artifact file was successfully created and is owned
    /// by this reservation.
    /// </summary>
    internal void MarkPreparing()
    {
        TransitionTo(
            TemporaryDocumentArtifactLifecycleState.Preparing);
    }

    /// <summary>
    /// Reads and validates the complete ownership history from the held lease.
    /// </summary>
    internal TemporaryDocumentArtifactOwnershipRecord ReadCurrentRecord()
    {
        lock (_sync)
        {
            ThrowIfUnavailable();

            return ReadLatestRecord(_leaseStream);
        }
    }

    /// <summary>
    /// Reads the last complete, validated ownership record.
    /// An incomplete trailing record is ignored because its transition
    /// was never confirmed as durable.
    /// </summary>
    internal static TemporaryDocumentArtifactOwnershipRecord ReadLatestRecord(
        Stream metadataStream)
    {
        ArgumentNullException.ThrowIfNull(metadataStream);

        if (!metadataStream.CanRead || !metadataStream.CanSeek)
        {
            throw new ArgumentException(
                "The ownership stream must support reading and seeking.",
                nameof(metadataStream));
        }

        if (metadataStream.Length <= 0 ||
            metadataStream.Length > MaximumMetadataSizeBytes)
        {
            throw new InvalidDataException(
                "The temporary artifact ownership record has an invalid size.");
        }

        byte[] contents =
            new byte[(int)metadataStream.Length];

        long originalPosition = metadataStream.Position;

        try
        {
            metadataStream.Position = 0;
            metadataStream.ReadExactly(contents);
        }
        finally
        {
            metadataStream.Position = originalPosition;
        }

        int lastCompleteRecord =
            Array.LastIndexOf(contents, (byte)'\n');

        if (lastCompleteRecord < 0)
        {
            throw new InvalidDataException(
                "The temporary artifact ownership history has no complete record.");
        }

        string completeText;

        try
        {
            completeText =
                StrictUtf8.GetString(
                    contents,
                    0,
                    lastCompleteRecord + 1);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The temporary artifact ownership history contains invalid encoding.",
                exception);
        }

        string[] lines =
            completeText.Split('\n');

        TemporaryDocumentArtifactOwnershipRecord? previous = null;

        for (int index = 0; index < lines.Length - 1; index++)
        {
            string line = lines[index].TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidDataException(
                    "The temporary artifact ownership history contains an empty record.");
            }

            TemporaryDocumentArtifactOwnershipRecord current;

            try
            {
                current =
                    JsonSerializer.Deserialize<
                        TemporaryDocumentArtifactOwnershipRecord>(
                        line,
                        SerializerOptions) ??
                    throw new InvalidDataException(
                        "The temporary artifact ownership record is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The temporary artifact ownership record is malformed.",
                    exception);
            }

            ValidateRecord(current);

            if (previous is null)
            {
                // CreationPending is the only valid initial record.
                // Preparing must follow it after FileMode.CreateNew succeeds.
                if (current.State !=
                    TemporaryDocumentArtifactLifecycleState.CreationPending)
                {
                    throw new InvalidDataException(
                        "The temporary artifact ownership history has an invalid initial state.");
                }
            }
            else
            {
                if (current.Version != previous.Version ||
                    current.ArtifactId != previous.ArtifactId ||
                    !string.Equals(
                        current.ArtifactFileName,
                        previous.ArtifactFileName,
                        StringComparison.Ordinal) ||
                    !IsAllowedTransition(
                        previous.State,
                        current.State))
                {
                    throw new InvalidDataException(
                        "The temporary artifact ownership history is inconsistent.");
                }
            }

            previous = current;
        }

        return previous ??
            throw new InvalidDataException(
                "The temporary artifact ownership history is incomplete.");
    }

    /// <summary>
    /// Deletes the ownership record and releases the lease.
    /// The caller must first successfully remove the associated artifact.
    /// </summary>
    internal void DeleteRecord()
    {
        lock (_sync)
        {
            // FileShare.Delete allows deletion while this ownership handle
            // remains open. Creation of a new owner with the same identity
            // remains blocked until the handle is released.
            File.Delete(MetadataPath);

            if (!_disposed)
            {
                _leaseStream.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Releases the process lease without deleting the ownership record.
    /// The durable state remains available for subsequent recovery.
    /// </summary>
    internal void ReleaseLease()
    {
        Dispose();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _leaseStream.Dispose();
            _disposed = true;
        }
    }

    private void TransitionTo(
        TemporaryDocumentArtifactLifecycleState nextState)
    {
        lock (_sync)
        {
            ThrowIfUnavailable();

            if (_currentState == nextState)
            {
                return;
            }

            if (!IsAllowedTransition(
                _currentState,
                nextState))
            {
                throw new InvalidOperationException(
                    "The temporary artifact lifecycle transition is invalid.");
            }

            AppendRecord(nextState);
        }
    }

    private void AppendRecord(
        TemporaryDocumentArtifactLifecycleState state)
    {
        ThrowIfUnavailable();

        var record = new TemporaryDocumentArtifactOwnershipRecord(
            CurrentVersion,
            ArtifactId,
            ArtifactFileName,
            state);

        byte[] json =
            JsonSerializer.SerializeToUtf8Bytes(
                record,
                SerializerOptions);

        if (_leaseStream.Length + json.Length + 1 >
            MaximumMetadataSizeBytes)
        {
            _faulted = true;

            throw new IOException(
                "The temporary artifact ownership history exceeded its permitted size.");
        }

        byte[] completeRecord =
            new byte[json.Length + 1];

        Buffer.BlockCopy(
            json,
            0,
            completeRecord,
            0,
            json.Length);

        completeRecord[^1] = (byte)'\n';

        try
        {
            _leaseStream.Position = _leaseStream.Length;

            _leaseStream.Write(completeRecord);

            // Do not publish the new in-memory state until the complete
            // record has been flushed to the filesystem.
            _leaseStream.Flush(flushToDisk: true);

            _currentState = state;
        }
        catch
        {
            // A partial record is not allowed to be extended by a later
            // transition. Recovery will preserve malformed histories.
            _faulted = true;

            throw;
        }
    }

    private static bool IsAllowedTransition(
        TemporaryDocumentArtifactLifecycleState current,
        TemporaryDocumentArtifactLifecycleState next)
    {
        return current switch
        {
            TemporaryDocumentArtifactLifecycleState.CreationPending =>
                next == TemporaryDocumentArtifactLifecycleState.Preparing,

            TemporaryDocumentArtifactLifecycleState.Preparing =>
                next is
                    TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact or
                    TemporaryDocumentArtifactLifecycleState.CleanupEligible,

            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact =>
                next == TemporaryDocumentArtifactLifecycleState.CleanupEligible,

            _ => false
        };
    }

    private static void ValidateRecord(
        TemporaryDocumentArtifactOwnershipRecord record)
    {
        if (record.Version != CurrentVersion ||
            record.ArtifactId == Guid.Empty ||
            !Enum.IsDefined(record.State) ||
            !IsValidArtifactFileName(
                record.ArtifactId,
                record.ArtifactFileName))
        {
            throw new InvalidDataException(
                "The temporary artifact ownership record failed validation.");
        }
    }

    internal static bool IsValidArtifactFileName(
        Guid artifactId,
        string artifactFileName)
    {
        if (artifactId == Guid.Empty ||
            string.IsNullOrWhiteSpace(artifactFileName) ||
            !string.Equals(
                artifactFileName,
                Path.GetFileName(artifactFileName),
                StringComparison.Ordinal))
        {
            return false;
        }

        string fileIdentity =
            Path.GetFileNameWithoutExtension(artifactFileName);

        if (!string.Equals(
            fileIdentity,
            artifactId.ToString("N"),
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string extension =
            Path.GetExtension(artifactFileName);

        return extension.Length == 0 ||
            (extension.Length >= 2 &&
             extension.Length <= 17 &&
             extension[0] == '.' &&
             extension.Skip(1).All(char.IsAsciiLetterOrDigit));
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_faulted)
        {
            throw new InvalidOperationException(
                "The temporary artifact ownership history is no longer writable.");
        }
    }
}

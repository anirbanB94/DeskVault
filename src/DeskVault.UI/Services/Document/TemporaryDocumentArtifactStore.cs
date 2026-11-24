using System.Security.AccessControl;
using System.Security.Principal;

namespace DeskVault.UI.Services.Document;

/// <summary>
/// Creates temporary plaintext document artifacts in a dedicated, user-protected namespace.
/// </summary>
/// <remarks>
/// The namespace grants directory traversal, file-creation, permission-management, and
/// synchronization rights. Each artifact grants read, write, delete, and synchronization
/// rights to the current Windows user, while the creation handle requests write and
/// synchronization access only.
/// </remarks>
internal sealed class TemporaryDocumentArtifactStore
{
    private const string ApplicationDirectoryName = "DeskVault";
    private const string ArtifactDirectoryName = "TemporaryDocuments";

    private const int DefaultBufferSize = 81920;

    private const FileSystemRights NamespaceDirectoryRights =
        FileSystemRights.ReadAndExecute |
        FileSystemRights.CreateFiles |
        FileSystemRights.ChangePermissions | FileSystemRights.Synchronize;

    private const FileSystemRights ArtifactFileRights =
        FileSystemRights.Read |
        FileSystemRights.Write |
        FileSystemRights.Delete | FileSystemRights.Synchronize;

    private const FileSystemRights ArtifactWriteHandleRights =
        FileSystemRights.Write | FileSystemRights.Synchronize;

    private readonly string _applicationDirectoryPath;
    private readonly string _directoryPath;

    /// <summary>
    /// Enumerates ownership metadata files in the existing protected namespace.
    /// The method does not create the namespace or recurse into subdirectories.
    /// </summary>
    /// <returns>The top-level ownership metadata paths.</returns>
    internal IReadOnlyList<string> EnumerateOwnershipMetadataFiles()
    {
        FileAttributes? applicationDirectoryAttributes =
            GetExistingDirectoryAttributes(
                _applicationDirectoryPath,
                "The DeskVault temporary storage parent is not a directory.");

        if (applicationDirectoryAttributes is null)
        {
            return [];
        }

        if ((applicationDirectoryAttributes.Value &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The DeskVault temporary storage parent is a reparse point.");
        }

        FileAttributes? artifactDirectoryAttributes =
            GetExistingDirectoryAttributes(
                _directoryPath,
                "The DeskVault temporary storage namespace is not a directory.");

        if (artifactDirectoryAttributes is null)
        {
            return [];
        }

        if ((artifactDirectoryAttributes.Value &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "The DeskVault temporary storage namespace is a reparse point.");
        }

        DirectoryInfo artifactDirectory =
            new(_directoryPath);

        VerifyAccessRules(
            artifactDirectory.GetAccessControl(),
            NamespaceDirectoryRights,
            InheritanceFlags.ContainerInherit |
            InheritanceFlags.ObjectInherit);

        return [.. Directory
            .EnumerateFiles(
                _directoryPath,
                "*.owner.json",
                SearchOption.TopDirectoryOnly)];
    }

    /// <summary>
    /// Gets attributes for an existing directory without treating access or
    /// structural errors as an absent namespace.
    /// </summary>
    /// <param name="directoryPath">The expected directory path.</param>
    /// <param name="notDirectoryMessage">The validation message for a non-directory object.</param>
    /// <returns>The directory attributes, or <see langword="null"/> only when the path is absent.</returns>
    private static FileAttributes? GetExistingDirectoryAttributes(
        string directoryPath,
        string notDirectoryMessage)
    {
        try
        {
            FileAttributes attributes =
                File.GetAttributes(directoryPath);

            if ((attributes & FileAttributes.Directory) == 0)
            {
                throw new InvalidDataException(
                    notDirectoryMessage);
            }

            return attributes;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Opens ownership metadata for recovery after validating its namespace,
    /// file attributes and protected access policy.
    /// </summary>
    /// <param name="metadataPath">The candidate metadata path.</param>
    /// <returns>An ownership object holding the exclusive recovery lease.</returns>
    internal TemporaryDocumentArtifactOwnership OpenOwnershipForRecovery(
        string metadataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);

        string fullDirectoryPath =
            Path.GetFullPath(_directoryPath);

        string fullMetadataPath =
            Path.GetFullPath(metadataPath);

        if (!string.Equals(
            Path.GetDirectoryName(fullMetadataPath),
            fullDirectoryPath,
            StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullMetadataPath).EndsWith(
                ".owner.json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The candidate ownership metadata is outside the DeskVault namespace.");
        }

        FileAttributes attributes =
            File.GetAttributes(fullMetadataPath);

        if ((attributes &
             (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException(
                "The candidate ownership metadata is not a regular file.");
        }

        VerifyAccessRules(
            new FileInfo(fullMetadataPath).GetAccessControl(),
            ArtifactFileRights,
            InheritanceFlags.None);

        return TemporaryDocumentArtifactOwnership.OpenForRecovery(
            fullDirectoryPath,
            fullMetadataPath);
    }

    /// <summary>
    /// Resolves and validates the physical path for a recorded artifact.
    /// </summary>
    /// <param name="record">The validated ownership record.</param>
    /// <returns>The artifact path inside the dedicated namespace.</returns>
    internal string GetArtifactPathForRecovery(
        TemporaryDocumentArtifactOwnershipRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!TemporaryDocumentArtifactOwnership.IsValidArtifactFileName(
            record.ArtifactId,
            record.ArtifactFileName))
        {
            throw new InvalidDataException(
                "The temporary artifact filename does not match its ownership identity.");
        }

        string fullDirectoryPath =
            Path.GetFullPath(_directoryPath);

        string artifactPath =
            Path.GetFullPath(
                Path.Combine(
                    fullDirectoryPath,
                    record.ArtifactFileName));

        if (!string.Equals(
            Path.GetDirectoryName(artifactPath),
            fullDirectoryPath,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The temporary artifact path is outside the DeskVault namespace.");
        }

        return artifactPath;
    }

    /// <summary>
    /// Determines whether a filesystem object occupies the validated artifact path.
    /// An existing object is preserved when creation ownership is unconfirmed.
    /// </summary>
    /// <param name="record">The validated creation-pending ownership record.</param>
    /// <returns><see langword="true"/> when an object exists at the artifact path.</returns>
    internal bool ArtifactPathExistsForRecovery(
        TemporaryDocumentArtifactOwnershipRecord record)
    {
        string artifactPath =
            GetArtifactPathForRecovery(record);

        try
        {
            _ = File.GetAttributes(artifactPath);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes an artifact after verifying its recorded identity, regular-file
    /// attributes and protected access policy. An already absent artifact is safe.
    /// </summary>
    /// <param name="record">The validated ownership record.</param>
    internal void DeleteArtifactForRecovery(
        TemporaryDocumentArtifactOwnershipRecord record)
    {
        string artifactPath =
            GetArtifactPathForRecovery(record);

        FileAttributes attributes;

        try
        {
            attributes = File.GetAttributes(artifactPath);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        if ((attributes &
             (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException(
                "The recorded temporary artifact is not a regular file.");
        }

        VerifyAccessRules(
            new FileInfo(artifactPath).GetAccessControl(),
            ArtifactFileRights,
            InheritanceFlags.None);

        // File.Delete is intentionally used without an existence check.
        // The ownership lease prevents a new owner from reusing this sidecar
        // while recovery is reconciling the associated artifact.
        File.Delete(artifactPath);
    }

    private readonly SecurityIdentifier _currentUserSid;
    private readonly Func<Guid> _idFactory;

    /// <summary>
    /// Initializes the store using the current user's local application data directory.
    /// </summary>
    public TemporaryDocumentArtifactStore()
        : this(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            Guid.NewGuid)
    {
    }

    /// <summary>
    /// Initializes the store with an explicit root and artifact identity provider.
    /// </summary>
    /// <param name="localApplicationDataDirectory">
    /// The local application data root beneath which the store namespace is created.
    /// </param>
    /// <param name="idFactory">The provider used to assign artifact identities.</param>
    /// <remarks>
    /// The explicit root and identity provider allow isolated filesystem tests without
    /// changing the production storage location or identity-generation policy.
    /// </remarks>
    internal TemporaryDocumentArtifactStore(
        string localApplicationDataDirectory,
        Func<Guid> idFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            localApplicationDataDirectory);

        ArgumentNullException.ThrowIfNull(idFactory);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Secure temporary document storage requires Windows.");
        }

        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        _currentUserSid =
            identity.User ??
            throw new InvalidOperationException(
                "The current Windows user identity is unavailable.");

        string localApplicationDataPath =
            Path.GetFullPath(localApplicationDataDirectory);

        _applicationDirectoryPath =
            Path.Combine(
                localApplicationDataPath,
                ApplicationDirectoryName);

        _directoryPath =
            Path.Combine(
                _applicationDirectoryPath,
                ArtifactDirectoryName);

        _idFactory = idFactory;
    }

    /// <summary>
    /// Gets the dedicated temporary artifact directory.
    /// </summary>
    internal string DirectoryPath => _directoryPath;

    /// <summary>
    /// Creates a durable reservation before attempting exclusive artifact creation.
    /// </summary>
    /// <param name="artifactId">The proposed artifact identity.</param>
    /// <param name="artifactFileName">The exact generated artifact filename.</param>
    /// <returns>An ownership reservation in the <see cref="TemporaryDocumentArtifactLifecycleState.CreationPending"/> state.</returns>
    internal TemporaryDocumentArtifactOwnership CreateOwnershipReservation(
        Guid artifactId,
        string artifactFileName)
    {
        EnsureProtectedNamespace();

        return TemporaryDocumentArtifactOwnership.Create(
            _directoryPath,
            artifactId,
            artifactFileName,
            CreateFileSecurity());
    }

    /// <summary>
    /// Creates a new artifact using the extension of the supplied document filename.
    /// </summary>
    /// <param name="fileName">The original document filename used to determine its extension.</param>
    /// <returns>An artifact with an exclusively created file and an open write stream.</returns>
    /// <remarks>
    /// The original filename is not used as the physical artifact name. A generated identity
    /// prevents document names from disclosing metadata or influencing the storage path.
    /// </remarks>
    public TemporaryDocumentArtifact CreateArtifact(
        string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string extension =
            GetSafeExtension(fileName);

        Guid artifactId = _idFactory();

        if (artifactId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The temporary artifact identity is invalid.");
        }

        string artifactFileName =
            $"{artifactId:N}{extension}";

        string artifactPath =
            Path.Combine(
                _directoryPath,
                artifactFileName);

        // Establish a durable reservation before attempting to create the artifact.
        // CreationPending does not prove that an existing file belongs to us.
        TemporaryDocumentArtifactOwnership ownership =
            CreateOwnershipReservation(
                artifactId,
                artifactFileName);

        FileStream? writeStream = null;

        try
        {
            FileInfo fileInfo =
                new(artifactPath);

            // Preserve create-new semantics and the existing protected ACL.
            // Never overwrite or delete an artifact that already exists.
            writeStream =
                fileInfo.Create(
                    FileMode.CreateNew,
                    ArtifactWriteHandleRights,
                    FileShare.Read,
                    DefaultBufferSize,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan,
                    CreateFileSecurity());

            // The successful CreateNew handle establishes ownership of the file.
            // Persist that fact before exposing the artifact to the caller.
            ownership.MarkPreparing();

            return new TemporaryDocumentArtifact(
                artifactId,
                artifactPath,
                writeStream,
                ownership);
        }
        catch
        {
            if (writeStream is null)
            {
                // CreateNew did not give us an owned file handle.
                // Remove only the ownership record created by this operation.
                try
                {
                    ownership.DeleteRecord();
                }
                catch
                {
                    ownership.Dispose();
                }
            }
            else
            {
                // A successfully returned write handle proves this operation
                // created the artifact. Only now may its path be cleaned up.
                try
                {
                    writeStream.Dispose();
                    File.Delete(artifactPath);
                    ownership.DeleteRecord();
                }
                catch
                {
                    // Preserve the CreationPending record for subsequent recovery.
                    ownership.Dispose();
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Creates or hardens the dedicated artifact namespace and verifies its access rules.
    /// </summary>
    /// <remarks>
    /// The application directory must not be a reparse point. The dedicated artifact directory
    /// receives a protected DACL so permissions are not inherited from broader parent rules.
    /// </remarks>
    private void EnsureProtectedNamespace()
    {
        DirectoryInfo applicationDirectory =
            Directory.CreateDirectory(
                _applicationDirectoryPath);

        if ((applicationDirectory.Attributes &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "The DeskVault temporary storage parent is not a regular directory.");
        }

        DirectorySecurity expectedSecurity =
            CreateDirectorySecurity();

        DirectoryInfo artifactDirectory =
            new(_directoryPath);

        // The ACL-aware DirectoryInfo overload applies the security descriptor
        // atomically when creating a new directory. For an existing directory,
        // creation is a no-op and the descriptor is explicitly reapplied below.
        artifactDirectory.Create(expectedSecurity);
        artifactDirectory.Refresh();

        if ((artifactDirectory.Attributes &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "The DeskVault temporary storage namespace is not a regular directory.");
        }

        // Existing directories are not assumed to have the required security descriptor.
        artifactDirectory.SetAccessControl(
            expectedSecurity);

        VerifyAccessRules(
            artifactDirectory.GetAccessControl(),
            NamespaceDirectoryRights,
            InheritanceFlags.ContainerInherit |
            InheritanceFlags.ObjectInherit);
    }

    /// <summary>
    /// Creates the protected directory access-control policy.
    /// </summary>
    /// <returns>
    /// A DACL granting the current user namespace traversal, file creation, permission-management, and synchronization rights.
    /// </returns>
    private DirectorySecurity CreateDirectorySecurity()
    {
        var security = new DirectorySecurity();

        // Remove inherited ACEs so broader parent permissions cannot grant access.
        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        security.AddAccessRule(
            new FileSystemAccessRule(
                _currentUserSid,
                NamespaceDirectoryRights,
                InheritanceFlags.ContainerInherit |
                InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

        return security;
    }

    /// <summary>
    /// Creates the protected file access-control policy.
    /// </summary>
    /// <returns>A DACL granting the current user read, write, delete, and synchronization rights.</returns>
    private FileSecurity CreateFileSecurity()
    {
        var security = new FileSecurity();

        // File permissions are explicit; they do not depend on directory inheritance.
        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        security.AddAccessRule(
            new FileSystemAccessRule(
                _currentUserSid,
                ArtifactFileRights,
                AccessControlType.Allow));

        return security;
    }

    /// <summary>
    /// Verifies that a security descriptor contains only the required protected access rule.
    /// </summary>
    /// <param name="security">The security descriptor to inspect.</param>
    /// <param name="expectedInheritanceFlags">The required inheritance configuration.</param>
    /// <exception cref="UnauthorizedAccessException">
    /// The descriptor is unprotected or contains permissions outside the required policy.
    /// </exception>
    private void VerifyAccessRules(
        FileSystemSecurity security,
        FileSystemRights expectedRights,
        InheritanceFlags expectedInheritanceFlags)
    {
        if (!security.AreAccessRulesProtected)
        {
            throw new UnauthorizedAccessException(
                "Temporary document storage ACL protection could not be established.");
        }

        FileSystemAccessRule[] rules =
            [.. security
                .GetAccessRules(
                    includeExplicit: true,
                    includeInherited: true,
                    targetType: typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()];

        if (rules.Length != 1 ||
            !rules[0].IdentityReference.Equals(_currentUserSid) ||
            rules[0].AccessControlType != AccessControlType.Allow ||
            rules[0].FileSystemRights != expectedRights ||
            rules[0].InheritanceFlags != expectedInheritanceFlags ||
            rules[0].PropagationFlags != PropagationFlags.None)
        {
            throw new UnauthorizedAccessException(
                "Temporary document storage permissions do not match the required security policy.");
        }
    }

    /// <summary>
    /// Extracts and validates a document extension for use in a temporary artifact filename.
    /// </summary>
    /// <param name="fileName">The original document filename.</param>
    /// <returns>A validated extension, or an empty string when no extension is present.</returns>
    /// <exception cref="ArgumentException">
    /// The extension contains unsupported characters or exceeds the permitted length.
    /// </exception>
    private static string GetSafeExtension(
        string fileName)
    {
        string extension =
            Path.GetExtension(fileName);

        if (extension.Length == 0)
        {
            return string.Empty;
        }

        // Only a short, single-component ASCII extension is permitted.
        // This excludes alternate data stream syntax and path-related characters.
        bool invalidExtension =
            extension.Length < 2 ||
            extension.Length > 17 ||
            extension[0] != '.' ||
            extension.Skip(1).Any(
                character =>
                    !char.IsAsciiLetterOrDigit(character));

        if (invalidExtension)
        {
            throw new ArgumentException(
                "The document extension is invalid for temporary viewing.",
                nameof(fileName));
        }

        return extension;
    }
}

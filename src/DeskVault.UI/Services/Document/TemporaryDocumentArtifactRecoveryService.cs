using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Services.Document;

internal enum TemporaryDocumentArtifactRecoveryDisposition
{
    Cleaned = 1,
    PreservedActiveLease = 2,
    PreservedExternalConsumer = 3,
    PreservedInvalidOwnership = 4,
    PreservedUnsafeFile = 5,
    AlreadyRemoved = 6,
    Failed = 7,
    PreservedUnconfirmedCreation = 8,
    RemovedEmptyCreationReservation = 9
}

internal sealed record TemporaryDocumentArtifactRecoveryFinding(
    string OwnershipFileName,
    Guid? ArtifactId,
    TemporaryDocumentArtifactLifecycleState? State,
    TemporaryDocumentArtifactRecoveryDisposition Disposition,
    string? FailureCategory);

internal sealed record TemporaryDocumentArtifactRecoveryResult(
    int CandidateCount,
    IReadOnlyList<TemporaryDocumentArtifactRecoveryFinding> Findings)
{
    public int CleanedCount =>
        Findings.Count(
            finding =>
                finding.Disposition is
                    TemporaryDocumentArtifactRecoveryDisposition.Cleaned or
                    TemporaryDocumentArtifactRecoveryDisposition.RemovedEmptyCreationReservation);

    public int PreservedCount =>
        Findings.Count(
            finding =>
                finding.Disposition is
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedActiveLease or
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedExternalConsumer or
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedInvalidOwnership or
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedUnsafeFile or
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedUnconfirmedCreation);

    public int AlreadyRemovedCount =>
        Findings.Count(
            finding =>
                finding.Disposition ==
                TemporaryDocumentArtifactRecoveryDisposition.AlreadyRemoved);

    public int FailedCount =>
        Findings.Count(
            finding =>
                finding.Disposition ==
                TemporaryDocumentArtifactRecoveryDisposition.Failed);
}

/// <summary>
/// Reconciles stale temporary document artifacts using only the protected
/// DeskVault ownership metadata convention.
/// </summary>
/// <remarks>
/// Recovery is deliberately conservative:
/// - An unavailable ownership lease is treated as an active or concurrent owner.
/// - CreationPending is removed only when the corresponding artifact path is absent.
/// - ExternalConsumerMayNeedArtifact is never deleted by recovery.
/// - Preparing and CleanupEligible may be deleted only after the ownership
///   lease has been acquired and the artifact identity and file policy validated.
/// - Invalid or ambiguous ownership is preserved.
/// </remarks>
internal sealed class TemporaryDocumentArtifactRecoveryService
{
    private readonly TemporaryDocumentArtifactStore _artifactStore;
    private readonly ILogger<TemporaryDocumentArtifactRecoveryService> _logger;

    public TemporaryDocumentArtifactRecoveryService(
        TemporaryDocumentArtifactStore artifactStore,
        ILogger<TemporaryDocumentArtifactRecoveryService> logger)
    {
        ArgumentNullException.ThrowIfNull(artifactStore);
        ArgumentNullException.ThrowIfNull(logger);

        _artifactStore = artifactStore;
        _logger = logger;
    }

    /// <summary>
    /// Enumerates and reconciles all safely identifiable ownership records.
    /// Individual failures do not prevent other candidates from being considered.
    /// </summary>
    public TemporaryDocumentArtifactRecoveryResult Recover()
    {
        string[] metadataPaths;

        try
        {
            metadataPaths =
                [.. _artifactStore.EnumerateOwnershipMetadataFiles()];
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Temporary document artifact recovery could not enumerate the protected namespace. FailureCategory: {FailureCategory}",
                exception.GetType().Name);

            return new TemporaryDocumentArtifactRecoveryResult(
                CandidateCount: 0,
                Findings:
                [
                    new TemporaryDocumentArtifactRecoveryFinding(
                        OwnershipFileName: "<namespace>",
                        ArtifactId: null,
                        State: null,
                        Disposition: TemporaryDocumentArtifactRecoveryDisposition.Failed,
                        FailureCategory: exception.GetType().Name)
                ]);
        }

        var findings =
            new List<TemporaryDocumentArtifactRecoveryFinding>(
                metadataPaths.Length);

        foreach (string metadataPath in metadataPaths)
        {
            TemporaryDocumentArtifactRecoveryFinding finding =
                RecoverOne(metadataPath);

            findings.Add(finding);

            LogFinding(finding);
        }

        return new TemporaryDocumentArtifactRecoveryResult(
            CandidateCount: metadataPaths.Length,
            Findings: findings);
    }

    /// <summary>
    /// Attempts conservative reconciliation of one ownership record.
    /// </summary>
    /// <param name="metadataPath">The enumerated ownership metadata path.</param>
    private TemporaryDocumentArtifactRecoveryFinding RecoverOne(
        string metadataPath)
    {
        string ownershipFileName =
            Path.GetFileName(metadataPath);

        TemporaryDocumentArtifactOwnership? ownership = null;

        Guid? artifactId = null;

        TemporaryDocumentArtifactLifecycleState? state = null;

        try
        {
            // The store validates the path, DACL and file attributes.
            // Opening the existing record also acquires its exclusive lease.
            ownership =
                _artifactStore.OpenOwnershipForRecovery(metadataPath);

            TemporaryDocumentArtifactOwnershipRecord record =
                ownership.ReadCurrentRecord();

            artifactId = record.ArtifactId;
            state = record.State;

            if (record.State ==
                TemporaryDocumentArtifactLifecycleState.CreationPending)
            {
                if (_artifactStore.ArtifactPathExistsForRecovery(record))
                {
                    // The reservation exists, but the journal does not prove
                    // that this process successfully created the file.
                    return CreateFinding(
                        TemporaryDocumentArtifactRecoveryDisposition.PreservedUnconfirmedCreation);
                }

                // No artifact was created. Only the reservation held by this
                // recovery lease is removed.
                ownership.DeleteRecord();
                ownership = null;

                return CreateFinding(
                    TemporaryDocumentArtifactRecoveryDisposition.RemovedEmptyCreationReservation);
            }

            if (record.State ==
                TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact)
            {
                // The external application's use of the file cannot be inferred
                // from the absence of a DeskVault process lease.
                return CreateFinding(
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedExternalConsumer);
            }

            if (record.State is not
                (TemporaryDocumentArtifactLifecycleState.Preparing or
                 TemporaryDocumentArtifactLifecycleState.CleanupEligible))
            {
                return CreateFinding(
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedInvalidOwnership);
            }

            // The artifact path and its DACL are validated before deletion.
            // An already absent artifact is an idempotent cleanup case.
            _artifactStore.DeleteArtifactForRecovery(record);

            // Delete metadata only after artifact deletion or confirmed absence.
            ownership.DeleteRecord();
            ownership = null;

            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.Cleaned);
        }
        catch (FileNotFoundException)
            when (ownership is null)
        {
            // Another recovery process may have completed this candidate
            // after our namespace enumeration.
            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.AlreadyRemoved);
        }
        catch (DirectoryNotFoundException)
            when (ownership is null)
        {
            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.AlreadyRemoved);
        }
        catch (InvalidDataException exception)
        {
            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.PreservedInvalidOwnership,
                exception.GetType().Name);
        }
        catch (UnauthorizedAccessException exception)
        {
            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.PreservedUnsafeFile,
                exception.GetType().Name);
        }
        catch (IOException exception)
            when (ownership is null)
        {
            // An active owner or another recovery process may hold the lease.
            // Recovery must not infer staleness from an unavailable lease.
            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.PreservedActiveLease,
                exception.GetType().Name);
        }
        catch (Exception exception)
        {
            return CreateFinding(
                TemporaryDocumentArtifactRecoveryDisposition.Failed,
                exception.GetType().Name);
        }
        finally
        {
            if (ownership is not null)
            {
                try
                {
                    ownership.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Temporary document artifact recovery lease disposal failed. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                        artifactId,
                        exception.GetType().Name);
                }
            }
        }

        TemporaryDocumentArtifactRecoveryFinding CreateFinding(
            TemporaryDocumentArtifactRecoveryDisposition disposition,
            string? failureCategory = null)
        {
            return new TemporaryDocumentArtifactRecoveryFinding(
                OwnershipFileName: ownershipFileName,
                ArtifactId: artifactId,
                State: state,
                Disposition: disposition,
                FailureCategory: failureCategory);
        }
    }

    /// <summary>
    /// Emits diagnostics without logging document content or absolute file paths.
    /// </summary>
    private void LogFinding(
        TemporaryDocumentArtifactRecoveryFinding finding)
    {
        switch (finding.Disposition)
        {
            case TemporaryDocumentArtifactRecoveryDisposition.Cleaned:
                _logger.LogInformation(
                    "Stale DeskVault temporary document artifact was reconciled. ArtifactId: {ArtifactId}",
                    finding.ArtifactId);
                break;

            case TemporaryDocumentArtifactRecoveryDisposition.PreservedActiveLease:
                _logger.LogDebug(
                    "Temporary document artifact was preserved because its ownership lease was unavailable. OwnershipFileName: {OwnershipFileName}; FailureCategory: {FailureCategory}",
                    finding.OwnershipFileName,
                    finding.FailureCategory);
                break;

            case TemporaryDocumentArtifactRecoveryDisposition.PreservedExternalConsumer:
                _logger.LogInformation(
                    "Temporary document artifact was preserved because an external consumer may still need it. ArtifactId: {ArtifactId}",
                    finding.ArtifactId);
                break;

            case TemporaryDocumentArtifactRecoveryDisposition.PreservedInvalidOwnership:
            case TemporaryDocumentArtifactRecoveryDisposition.PreservedUnsafeFile:
            case TemporaryDocumentArtifactRecoveryDisposition.Failed:
                _logger.LogWarning(
                    "Temporary document artifact recovery preserved or failed a candidate. OwnershipFileName: {OwnershipFileName}; ArtifactId: {ArtifactId}; Disposition: {Disposition}; FailureCategory: {FailureCategory}",
                    finding.OwnershipFileName,
                    finding.ArtifactId,
                    finding.Disposition,
                    finding.FailureCategory);
                break;

            case TemporaryDocumentArtifactRecoveryDisposition.AlreadyRemoved:
                _logger.LogDebug(
                    "Temporary document artifact was already removed by another recovery operation. OwnershipFileName: {OwnershipFileName}",
                    finding.OwnershipFileName);
                break;

            case TemporaryDocumentArtifactRecoveryDisposition.PreservedUnconfirmedCreation:
                _logger.LogWarning(
                    "Temporary document artifact was preserved because file creation ownership was not confirmed. OwnershipFileName: {OwnershipFileName}; ArtifactId: {ArtifactId}",
                    finding.OwnershipFileName,
                    finding.ArtifactId);
                break;

            case TemporaryDocumentArtifactRecoveryDisposition.RemovedEmptyCreationReservation:
                _logger.LogDebug(
                    "Empty temporary document artifact creation reservation was removed. OwnershipFileName: {OwnershipFileName}; ArtifactId: {ArtifactId}",
                    finding.OwnershipFileName,
                    finding.ArtifactId);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(finding),
                    finding.Disposition,
                    "The temporary artifact recovery disposition is unsupported.");
        }
    }
}

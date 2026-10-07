namespace DeskVault.Domain.Documents;

public sealed class Document
{
    private readonly List<DocumentKnowledgeAvailability> _knowledgeAvailability;

    public Guid Id { get; }

    public string FileName { get; }

    public string DisplayName { get; private set; }

    public string Sha256Hash { get; }

    public DateTime ImportedAt { get; }

    // Legacy compatibility state retained while lifecycle persistence
    // is migrated to the separated state model.
    public DocumentStatus Status { get; private set; }

    public DocumentLifecycleState LifecycleState { get; private set; }

    public DocumentProcessingState ProcessingState { get; private set; }

    public IReadOnlyList<DocumentKnowledgeAvailability>
        KnowledgeAvailability =>
        _knowledgeAvailability;

    public string StoredFilePath { get; }

    public long ProcessingGeneration { get; }

    public long LastSuccessfulProcessingGeneration { get; }

    public string? LastSuccessfulProcessingRuleVersion { get; }

    private Document(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status,
        DocumentLifecycleState lifecycleState,
        DocumentProcessingState processingState,
        IReadOnlyList<DocumentKnowledgeAvailability> knowledgeAvailability,
        long processingGeneration,
        long lastSuccessfulProcessingGeneration,
        string? lastSuccessfulProcessingRuleVersion)
    {
        Id = id;
        FileName = fileName;
        DisplayName = displayName;
        Sha256Hash = sha256Hash;
        StoredFilePath = storedFilePath;
        ImportedAt = importedAt;

        Status = status;
        LifecycleState = lifecycleState;
        ProcessingState = processingState;

        _knowledgeAvailability =
            new List<DocumentKnowledgeAvailability>(
                knowledgeAvailability);

        ProcessingGeneration =
            processingGeneration;

        LastSuccessfulProcessingGeneration =
            lastSuccessfulProcessingGeneration;

        LastSuccessfulProcessingRuleVersion =
            lastSuccessfulProcessingRuleVersion;
    }

    public static Document Create(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath)
    {
        Validate(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath);

        return new Document(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath,
            DateTime.UtcNow,
            DocumentStatus.Imported,
            DocumentLifecycleState.Active,
            DocumentProcessingState.NeverProcessed,
            [
                new DocumentKnowledgeAvailability(
                    DocumentKnowledgeRepresentationKind.KeywordSearch,
                    DocumentKnowledgeAvailabilityState.Unavailable,
                    null)
            ],
            0L,
            0L,
            null);
    }

    public static Document Restore(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status,
        long processingGeneration = 0L,
        long lastSuccessfulProcessingGeneration = 0L,
        string? lastSuccessfulProcessingRuleVersion = null)
    {
        (
            DocumentLifecycleState lifecycleState,
            DocumentProcessingState processingState,
            IReadOnlyList<DocumentKnowledgeAvailability>
                knowledgeAvailability) =
            ResolveLegacyState(
                status,
                lastSuccessfulProcessingGeneration);

        return Restore(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath,
            importedAt,
            status,
            processingGeneration,
            lastSuccessfulProcessingGeneration,
            lastSuccessfulProcessingRuleVersion,
            lifecycleState,
            processingState,
            knowledgeAvailability);
    }

    public static Document Restore(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status,
        long processingGeneration,
        long lastSuccessfulProcessingGeneration,
        string? lastSuccessfulProcessingRuleVersion,
        DocumentLifecycleState lifecycleState,
        DocumentProcessingState processingState,
        IReadOnlyList<DocumentKnowledgeAvailability>?
            knowledgeAvailability = null)
    {
        Validate(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath);

        if (importedAt == default)
        {
            throw new ArgumentException(
                "Imported date is required.",
                nameof(importedAt));
        }

        if (!Enum.IsDefined(lifecycleState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifecycleState),
                "Document lifecycle state is invalid.");
        }

        if (!Enum.IsDefined(processingState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingState),
                "Document processing state is invalid.");
        }

        if (processingGeneration < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingGeneration),
                "Processing generation cannot be negative.");
        }

        if (lastSuccessfulProcessingGeneration < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastSuccessfulProcessingGeneration),
                "Last successful processing generation cannot be negative.");
        }

        if (lastSuccessfulProcessingGeneration >
            processingGeneration)
        {
            throw new ArgumentException(
                "Last successful processing generation cannot be greater than the current processing generation.",
                nameof(lastSuccessfulProcessingGeneration));
        }

        if (lastSuccessfulProcessingRuleVersion is not null &&
            string.IsNullOrWhiteSpace(
                lastSuccessfulProcessingRuleVersion))
        {
            throw new ArgumentException(
                "Last successful processing-rule version cannot be empty when provided.",
                nameof(lastSuccessfulProcessingRuleVersion));
        }

        IReadOnlyList<DocumentKnowledgeAvailability>
            restoredKnowledgeAvailability =
                knowledgeAvailability ??
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Unavailable,
                        null)
                ];

        ValidateKnowledgeAvailability(
            restoredKnowledgeAvailability,
            lastSuccessfulProcessingGeneration);

        return new Document(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath,
            importedAt,
            status,
            lifecycleState,
            processingState,
            restoredKnowledgeAvailability,
            processingGeneration,
            lastSuccessfulProcessingGeneration,
            lastSuccessfulProcessingRuleVersion);
    }

    public void Archive()
    {
        EnsureLifecycleTransition(
            DocumentLifecycleState.Active,
            DocumentLifecycleState.Archived);

        LifecycleState =
            DocumentLifecycleState.Archived;
    }

    public void RestoreFromArchive()
    {
        EnsureLifecycleTransition(
            DocumentLifecycleState.Archived,
            DocumentLifecycleState.Active);

        LifecycleState =
            DocumentLifecycleState.Active;
    }

    public void Delete()
    {
        if (LifecycleState ==
            DocumentLifecycleState.Deleted)
        {
            throw new InvalidOperationException(
                "A deleted document cannot be deleted again.");
        }

        LifecycleState =
            DocumentLifecycleState.Deleted;
    }

    public void BeginProcessing()
    {
        EnsureDocumentIsProcessable();

        ProcessingState =
            DocumentProcessingState.Processing;
    }

    public void MarkProcessingSucceeded()
    {
        EnsureProcessingInProgress();

        ProcessingState =
            DocumentProcessingState.Succeeded;
    }

    public void MarkProcessingFailed()
    {
        EnsureProcessingInProgress();

        ProcessingState =
            DocumentProcessingState.Failed;
    }

    public void MarkProcessingCancelled()
    {
        EnsureProcessingInProgress();

        ProcessingState =
            DocumentProcessingState.Cancelled;
    }

    public void SetKnowledgeAvailability(
        DocumentKnowledgeRepresentationKind representation,
        DocumentKnowledgeAvailabilityState state,
        long? lastAvailableProcessingGeneration)
    {
        DocumentKnowledgeAvailability availability =
            new(
                representation,
                state,
                lastAvailableProcessingGeneration);

        ValidateKnowledgeAvailability(
            [availability],
            LastSuccessfulProcessingGeneration);

        int existingIndex =
            _knowledgeAvailability.FindIndex(
                item =>
                    item.Representation ==
                    representation);

        if (existingIndex >= 0)
        {
            _knowledgeAvailability[existingIndex] =
                availability;

            return;
        }

        _knowledgeAvailability.Add(
            availability);
    }

    public DocumentKnowledgeAvailability
        GetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind representation)
    {
        DocumentKnowledgeAvailability? availability =
            _knowledgeAvailability.FirstOrDefault(
                item =>
                    item.Representation ==
                    representation);

        if (availability is null)
        {
            throw new InvalidOperationException(
                $"Knowledge availability for representation '{representation}' is not defined.");
        }

        return availability;
    }

    // Legacy status mutators retained for compatibility with
    // existing callers until lifecycle migration is completed.
    public void MarkProcessing()
    {
        Status =
            DocumentStatus.Processing;
    }

    public void MarkIndexed()
    {
        Status =
            DocumentStatus.Indexed;
    }

    public void MarkAvailable()
    {
        Status =
            DocumentStatus.Available;
    }

    public void MarkFailed()
    {
        Status =
            DocumentStatus.Failed;
    }

    private void EnsureDocumentIsProcessable()
    {
        if (LifecycleState ==
            DocumentLifecycleState.Deleted)
        {
            throw new InvalidOperationException(
                "A deleted document cannot be processed.");
        }
    }

    private void EnsureProcessingInProgress()
    {
        if (ProcessingState !=
            DocumentProcessingState.Processing)
        {
            throw new InvalidOperationException(
                "Document processing must be in progress before its processing outcome can be changed.");
        }
    }

    private void EnsureLifecycleTransition(
        DocumentLifecycleState expectedCurrentState,
        DocumentLifecycleState targetState)
    {
        if (LifecycleState !=
            expectedCurrentState)
        {
            throw new InvalidOperationException(
                $"Document lifecycle cannot transition from '{LifecycleState}' to '{targetState}'.");
        }
    }

    private static (
        DocumentLifecycleState LifecycleState,
        DocumentProcessingState ProcessingState,
        IReadOnlyList<DocumentKnowledgeAvailability>
            KnowledgeAvailability)
        ResolveLegacyState(
            DocumentStatus status,
            long lastSuccessfulProcessingGeneration)
    {
        DocumentLifecycleState lifecycleState =
            status switch
            {
                DocumentStatus.Archived =>
                    DocumentLifecycleState.Archived,

                DocumentStatus.Deleted =>
                    DocumentLifecycleState.Deleted,

                _ =>
                    DocumentLifecycleState.Active
            };

        DocumentProcessingState processingState =
            status switch
            {
                DocumentStatus.Processing =>
                    DocumentProcessingState.Processing,

                DocumentStatus.Failed =>
                    DocumentProcessingState.Failed,

                DocumentStatus.Indexed or
                DocumentStatus.Available =>
                    DocumentProcessingState.Succeeded,

                DocumentStatus.Archived or
                DocumentStatus.Deleted
                    when lastSuccessfulProcessingGeneration > 0L =>
                    DocumentProcessingState.Succeeded,

                _ =>
                    DocumentProcessingState.NeverProcessed
            };

        bool legacyKeywordSearchIsAvailable =
            status != DocumentStatus.Deleted &&
            (
                lastSuccessfulProcessingGeneration > 0L ||
                status is
                    DocumentStatus.Indexed or
                    DocumentStatus.Available
            );

        IReadOnlyList<DocumentKnowledgeAvailability>
            knowledgeAvailability =
                legacyKeywordSearchIsAvailable
                    ?
                    [
                        new DocumentKnowledgeAvailability(
                            DocumentKnowledgeRepresentationKind.KeywordSearch,
                            DocumentKnowledgeAvailabilityState.Available,
                            lastSuccessfulProcessingGeneration)
                    ]
                    :
                    [
                        new DocumentKnowledgeAvailability(
                            DocumentKnowledgeRepresentationKind.KeywordSearch,
                            DocumentKnowledgeAvailabilityState.Unavailable,
                            null)
                    ];

        return (
            lifecycleState,
            processingState,
            knowledgeAvailability);
    }

    private static void ValidateKnowledgeAvailability(
        IReadOnlyList<DocumentKnowledgeAvailability>
            knowledgeAvailability,
        long lastSuccessfulProcessingGeneration)
    {
        HashSet<DocumentKnowledgeRepresentationKind>
            representations = [];

        foreach (
            DocumentKnowledgeAvailability availability
            in knowledgeAvailability)
        {
            if (!representations.Add(
                    availability.Representation))
            {
                throw new ArgumentException(
                    $"Knowledge availability for representation '{availability.Representation}' is duplicated.",
                    nameof(knowledgeAvailability));
            }

            if (availability.LastAvailableProcessingGeneration
                is long generation &&
                generation > lastSuccessfulProcessingGeneration)
            {
                throw new ArgumentException(
                    $"Knowledge availability generation '{generation}' cannot be greater than last successful processing generation '{lastSuccessfulProcessingGeneration}'.",
                    nameof(knowledgeAvailability));
            }
        }
    }

    private static void Validate(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "File name is required.",
                nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name is required.",
                nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(sha256Hash))
        {
            throw new ArgumentException(
                "SHA-256 hash is required.",
                nameof(sha256Hash));
        }

        if (string.IsNullOrWhiteSpace(storedFilePath))
        {
            throw new ArgumentException(
                "Stored file path is required.",
                nameof(storedFilePath));
        }
    }
}

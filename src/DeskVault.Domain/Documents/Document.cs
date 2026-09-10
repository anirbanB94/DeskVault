namespace DeskVault.Domain.Documents;

public sealed class Document
{
    public Guid Id { get; }

    public string FileName { get; }

    public string DisplayName { get; private set; }

    public string Sha256Hash { get; }

    public DateTime ImportedAt { get; }

    public DocumentStatus Status { get; private set; }

    public string StoredFilePath { get; }

    public long ProcessingGeneration { get; }

    public long LastSuccessfulProcessingGeneration { get; }

    private Document(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status,
        long processingGeneration,
        long lastSuccessfulProcessingGeneration)
    {
        Id = id;
        FileName = fileName;
        DisplayName = displayName;
        Sha256Hash = sha256Hash;
        StoredFilePath = storedFilePath;
        ImportedAt = importedAt;
        Status = status;
        ProcessingGeneration = processingGeneration;
        LastSuccessfulProcessingGeneration =
            lastSuccessfulProcessingGeneration;
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
            0,
            0);
    }

    public static Document Restore(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status,
        long processingGeneration = 0,
        long lastSuccessfulProcessingGeneration = 0)
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

        if (processingGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingGeneration),
                "Processing generation cannot be negative.");
        }

        if (lastSuccessfulProcessingGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastSuccessfulProcessingGeneration),
                "Last successful processing generation cannot be negative.");
        }

        if (lastSuccessfulProcessingGeneration > processingGeneration)
        {
            throw new ArgumentException(
                "Last successful processing generation cannot be greater than the current processing generation.",
                nameof(lastSuccessfulProcessingGeneration));
        }

        return new Document(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath,
            importedAt,
            status,
            processingGeneration,
            lastSuccessfulProcessingGeneration);
    }

    public void MarkProcessing()
    {
        Status = DocumentStatus.Processing;
    }

    public void MarkIndexed()
    {
        Status = DocumentStatus.Indexed;
    }

    public void MarkAvailable()
    {
        Status = DocumentStatus.Available;
    }

    public void MarkFailed()
    {
        Status = DocumentStatus.Failed;
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

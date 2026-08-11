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

    private Document(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status)
    {
        Id = id;
        FileName = fileName;
        DisplayName = displayName;
        Sha256Hash = sha256Hash;
        StoredFilePath = storedFilePath;
        ImportedAt = importedAt;
        Status = status;
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
            DocumentStatus.Imported);
    }

    public static Document Restore(
        Guid id,
        string fileName,
        string displayName,
        string sha256Hash,
        string storedFilePath,
        DateTime importedAt,
        DocumentStatus status)
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

        return new Document(
            id,
            fileName,
            displayName,
            sha256Hash,
            storedFilePath,
            importedAt,
            status);
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

namespace DeskVault.Infrastructure.Persistence.Entities;

public sealed class DocumentEntity
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Sha256Hash { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; }

    public int Status { get; set; }

    public string StoredFilePath { get; set; } = string.Empty;
}

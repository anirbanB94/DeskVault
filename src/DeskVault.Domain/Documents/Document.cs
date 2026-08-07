namespace DeskVault.Domain.Documents;

public sealed class Document
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTime ImportedAt { get; init; }

    public DocumentStatus Status { get; init; } = DocumentStatus.Imported;
}
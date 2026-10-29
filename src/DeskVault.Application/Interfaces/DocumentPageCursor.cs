namespace DeskVault.Application.Interfaces;

/// <summary>
/// Represents a continuation cursor for retrieving document records in deterministic order.
/// </summary>
/// <param name="ImportedAt">The import timestamp of the last document record returned.</param>
/// <param name="DocumentId">The identifier of the last document record returned, used to distinguish
/// records with the same import timestamp.</param>
public sealed record DocumentPageCursor(
    DateTime ImportedAt,
    Guid DocumentId);

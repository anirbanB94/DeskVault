using DeskVault.Domain.Documents;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Defines operations for storing, retrieving, updating, and deleting document records.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Defines the maximum number of document records that can be returned in a page.
    /// </summary>
    const int MaximumDocumentPageSize = 256;

    /// <summary>
    /// Checks whether a document with the specified SHA-256 hash exists.
    /// </summary>
    /// <param name="sha256Hash">The SHA-256 hash to search for.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns><see langword="true"/> if a matching document exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsByHashAsync(
        string sha256Hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a document record to the repository.
    /// </summary>
    /// <param name="document">The document record to add.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a document record by its identifier.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching document record, or <see langword="null"/> if no record exists.</returns>
    Task<Document?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all document records.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A read-only list of document records.</returns>
    Task<IReadOnlyList<Document>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a bounded page of document records using a continuation cursor.
    /// </summary>
    /// <param name="cursor">The cursor identifying the position after which to retrieve records, or <see langword="null"/> to start from the beginning.</param>
    /// <param name="pageSize">The maximum number of document records to return, up to <see cref="MaximumDocumentPageSize"/>.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A read-only list of document records in deterministic order.</returns>
    Task<IReadOnlyList<Document>> GetPageAsync(
        DocumentPageCursor? cursor,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document record by its identifier.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing document record in the repository.
    /// </summary>
    /// <param name="document">The document record containing the updated values.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken = default);
}

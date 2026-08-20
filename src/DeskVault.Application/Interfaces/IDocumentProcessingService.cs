namespace DeskVault.Application.Interfaces;

public interface IDocumentProcessingService
{
    Task ProcessAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}

namespace DeskVault.Application.Interfaces;

public interface IDocumentArtifactEnumerator
{
    Task<IReadOnlyList<string>> EnumerateAsync(
        CancellationToken cancellationToken = default);
}

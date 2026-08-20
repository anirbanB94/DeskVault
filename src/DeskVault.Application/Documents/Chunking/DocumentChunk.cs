namespace DeskVault.Application.Documents.Chunking;

public sealed record DocumentChunk(
    int Order,
    string Text);

namespace DeskVault.Application.Configurations;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = string.Empty;

    public string DefaultModel { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; }
}
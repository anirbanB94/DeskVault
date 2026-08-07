namespace DeskVault.Application.Configurations;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;
}
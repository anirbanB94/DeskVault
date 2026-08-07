namespace DeskVault.Application.Configurations;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; init; } = string.Empty;

    public string ConnectionString { get; init; } = string.Empty;
}
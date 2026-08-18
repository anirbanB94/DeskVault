namespace DeskVault.Infrastructure.Services;

public sealed class DeskVaultDataPaths
{
    public string RootDirectory { get; }

    public string DatabasePath =>
        Path.Combine(
            RootDirectory,
            "DeskVault.db");

    public string DocumentsDirectory =>
        Path.Combine(
            RootDirectory,
            "Documents");

    public string SecurityDirectory =>
        Path.Combine(
            RootDirectory,
            "Security");

    public DeskVaultDataPaths(
        string? rootDirectory = null)
    {
        RootDirectory =
            rootDirectory ??
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "DeskVault");
    }
}

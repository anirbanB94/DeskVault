namespace DeskVault.UI.Services.Interfaces;

public interface IExternalDocumentLauncher
{
    IExternalDocumentLaunch Launch(
        string filePath);
}

public interface IExternalDocumentLaunch :
    IDisposable
{
    Task? Completion { get; }
}

namespace DeskVault.UI.Services.Interfaces;

/// <summary>
/// Starts documents through their registered external applications.
/// </summary>
public interface IExternalDocumentLauncher
{
    /// <summary>
    /// Starts the specified document through the operating system.
    /// </summary>
    /// <param name="filePath">The fully qualified document path.</param>
    /// <returns>A handle representing the external launch operation.</returns>
    IExternalDocumentLaunch Launch(
        string filePath);
}

/// <summary>
/// Represents an external document launch and its consumer-lifecycle observation.
/// </summary>
/// <remarks>
/// Disposing this handle releases launcher-owned resources. It does not
/// terminate the external application or establish that document consumption
/// has finished.
/// </remarks>
public interface IExternalDocumentLaunch :
    IDisposable
{
    /// <summary>
    /// Gets the trustworthy external-consumer completion task, when available.
    /// </summary>
    /// <remarks>
    /// A non-null task must represent the relevant lifecycle of the application
    /// actually consuming the document. A Shell activation process exiting is
    /// not sufficient evidence.
    ///
    /// A null task means consumer completion is unobservable. A faulted or
    /// cancelled task also does not establish that the consumer has finished.
    /// </remarks>
    Task? Completion { get; }
}

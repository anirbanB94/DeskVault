using DeskVault.UI.Services.Interfaces;
using System.Diagnostics;

namespace DeskVault.UI.Services.Document;

/// <summary>
/// Launches document files through the Windows Shell.
/// </summary>
/// <remarks>
/// The process returned by a Shell launch is not assumed to represent
/// the application consuming the document. External completion is
/// therefore reported as unobservable.
/// </remarks>
public sealed class ExternalDocumentLauncher :
    IExternalDocumentLauncher
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public ExternalDocumentLauncher()
        : this(
            static startInfo => Process.Start(startInfo))
    {
    }

    internal ExternalDocumentLauncher(
        Func<ProcessStartInfo, Process?> startProcess)
    {
        ArgumentNullException.ThrowIfNull(startProcess);

        _startProcess = startProcess;
    }

    /// <summary>
    /// Launches a document through its registered Windows application.
    /// </summary>
    /// <param name="filePath">The fully qualified temporary document path.</param>
    /// <returns>
    /// A launch handle with no assumed external-consumer completion signal.
    /// </returns>
    public IExternalDocumentLaunch Launch(
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        Process? process =
            _startProcess(
                new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

        return new ExternalDocumentLaunch(process);
    }

    private sealed class ExternalDocumentLaunch :
        IExternalDocumentLaunch
    {
        private readonly Process? _process;

        public ExternalDocumentLaunch(
            Process? process)
        {
            _process = process;
        }

        public Task? Completion => null;

        public void Dispose()
        {
            _process?.Dispose();
        }
    }
}

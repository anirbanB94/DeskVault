using System.Diagnostics;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Services.Document;

public sealed class ExternalDocumentLauncher :
    IExternalDocumentLauncher
{
    public IExternalDocumentLaunch Launch(
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        Process? process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

        if (process is null ||
            process.HasExited)
        {
            return new ExternalDocumentLaunch(
                process,
                completion: null);
        }

        return new ExternalDocumentLaunch(
            process,
            process.WaitForExitAsync());
    }

    private sealed class ExternalDocumentLaunch :
        IExternalDocumentLaunch
    {
        private readonly Process? _process;

        public ExternalDocumentLaunch(
            Process? process,
            Task? completion)
        {
            _process = process;
            Completion = completion;
        }

        public Task? Completion { get; }

        public void Dispose()
        {
            _process?.Dispose();
        }
    }
}

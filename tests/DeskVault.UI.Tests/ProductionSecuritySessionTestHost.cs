using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.Infrastructure.Services;
using DeskVault.UI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DeskVault.UI.Tests;

/// <summary>
/// Builds and owns a real DeskVault production host with isolated data paths
/// and deterministic substitutes only at permitted external boundaries.
/// </summary>
internal sealed class ProductionSecuritySessionTestHost : IAsyncDisposable
{
    private readonly string _rootDirectory;
    private readonly DeskVaultHostLifetime _hostLifetime;

    private bool _disposed;

    private ProductionSecuritySessionTestHost(
        string rootDirectory,
        IHost host,
        DeskVaultDataPaths dataPaths,
        ControlledWorkstationSecurityEventSource workstationEventSource,
        ControlledWindowsHelloPlatformApi windowsHelloPlatformApi,
        ControlledWindowsHelloWindowHandleProvider windowHandleProvider)
    {
        _rootDirectory = rootDirectory;

        Host = host;
        _hostLifetime = new DeskVaultHostLifetime(host);

        DataPaths = dataPaths;
        WorkstationEventSource = workstationEventSource;
        WindowsHelloPlatformApi = windowsHelloPlatformApi;
        WindowHandleProvider = windowHandleProvider;
    }

    public IHost Host { get; }

    public IServiceProvider Services =>
        Host.Services;

    public DeskVaultDataPaths DataPaths { get; }

    public ControlledWorkstationSecurityEventSource WorkstationEventSource { get; }

    public ControlledWindowsHelloPlatformApi WindowsHelloPlatformApi { get; }

    public ControlledWindowsHelloWindowHandleProvider WindowHandleProvider { get; }

    public static ProductionSecuritySessionTestHost Create(
        TimeProvider? timeProvider = null,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        string rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "DeskVaultTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(rootDirectory, "appsettings.json"),
                "{}");

            var dataPaths = new DeskVaultDataPaths(
                Path.Combine(rootDirectory, "vault"));

            var workstationEventSource =
                new ControlledWorkstationSecurityEventSource();

            var windowsHelloPlatformApi =
                new ControlledWindowsHelloPlatformApi();

            var windowHandleProvider =
                new ControlledWindowsHelloWindowHandleProvider();

            IHost host = HostConfigurator.BuildForTesting(
                rootDirectory,
                services =>
                {
                    // Replace only nondeterministic operating-system
                    // boundaries. Application, Infrastructure and UI
                    // production registrations remain in place.
                    services.RemoveAll<IWorkstationSecurityEventSource>();

                    services.AddSingleton<IWorkstationSecurityEventSource>(
                        workstationEventSource);

                    services.RemoveAll<IWindowsHelloPlatformApi>();

                    services.AddSingleton<IWindowsHelloPlatformApi>(
                        windowsHelloPlatformApi);

                    services.RemoveAll<IWindowsHelloWindowHandleProvider>();

                    services.AddSingleton<IWindowsHelloWindowHandleProvider>(
                        windowHandleProvider);

                    services.RemoveAll<DeskVaultDataPaths>();

                    services.AddSingleton(dataPaths);

                    if (timeProvider is not null)
                    {
                        services.RemoveAll<TimeProvider>();
                        services.AddSingleton(timeProvider);
                    }
                },
                configurationOverrides);

            return new ProductionSecuritySessionTestHost(
                rootDirectory,
                host,
                dataPaths,
                workstationEventSource,
                windowsHelloPlatformApi,
                windowHandleProvider);
        }
        catch
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }

            throw;
        }
    }

    public Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        return _hostLifetime.StartAsync(
            cancellationToken);
    }

    public Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        return _hostLifetime.StopAsync(
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _hostLifetime.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(
                    _rootDirectory,
                    recursive: true);
            }
        }
    }
}

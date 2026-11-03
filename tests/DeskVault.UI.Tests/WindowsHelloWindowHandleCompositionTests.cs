using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.UI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeskVault.UI.Tests;

public sealed class WindowsHelloWindowHandleCompositionTests
{
    [Fact]
    public void Build_RegistersWindowsHelloWindowHandleProviderInProductionComposition()
    {
        // Arrange
        using IHost host =
            HostConfigurator.Build();

        // Act
        IWindowsHelloWindowHandleProvider firstHandleProvider =
            host.Services.GetRequiredService<IWindowsHelloWindowHandleProvider>();

        IWindowsHelloWindowHandleProvider secondHandleProvider =
            host.Services.GetRequiredService<IWindowsHelloWindowHandleProvider>();

        IApplicationAuthenticationProvider authenticationProvider =
            host.Services.GetRequiredService<IApplicationAuthenticationProvider>();

        // Assert
        Assert.IsType<WindowsHelloWindowHandleProvider>(
            firstHandleProvider);

        Assert.Same(
            firstHandleProvider,
            secondHandleProvider);

        Assert.IsType<WindowsHelloAuthenticationProvider>(
            authenticationProvider);
    }
}

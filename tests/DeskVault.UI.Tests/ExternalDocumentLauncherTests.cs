using DeskVault.UI.Services.Document;
using DeskVault.UI.Services.Interfaces;
using System.Diagnostics;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies the external launcher completion contract.
/// </summary>
public sealed class ExternalDocumentLauncherTests
{
    [Fact]
    public void Launch_WhenShellReturnsProcess_ReportsLifecycleAsUnobservable()
    {
        // Arrange
        const string filePath =
            @"C:\DeskVault\document.html";

        ProcessStartInfo? capturedStartInfo = null;

        var launcher = new ExternalDocumentLauncher(
            startInfo =>
            {
                capturedStartInfo = startInfo;

                return Process.GetCurrentProcess();
            });

        // Act
        using IExternalDocumentLaunch launch =
            launcher.Launch(filePath);

        // Assert
        Assert.NotNull(capturedStartInfo);

        Assert.Equal(
            filePath,
            capturedStartInfo.FileName);

        Assert.True(
            capturedStartInfo.UseShellExecute);

        Assert.Null(launch.Completion);
    }

    [Fact]
    public void Launch_WhenShellReturnsNoProcess_ReportsLifecycleAsUnobservable()
    {
        // Arrange
        const string filePath =
            @"C:\DeskVault\document.html";

        var launcher = new ExternalDocumentLauncher(
            static _ => null);

        // Act
        using IExternalDocumentLaunch launch =
            launcher.Launch(filePath);

        // Assert
        Assert.Null(launch.Completion);
    }

    [Fact]
    public void Launch_WhenFilePathIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var launcher = new ExternalDocumentLauncher(
            static _ => null);

        // Act / Assert
        Assert.Throws<ArgumentException>(
            () => launcher.Launch(" "));
    }
}

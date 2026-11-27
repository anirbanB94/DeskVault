using DeskVault.Infrastructure.Services;
using System.Diagnostics;

namespace DeskVault.Infrastructure.Tests;

/// <summary>
/// Verifies canonical vault directory resolution against real Windows
/// filesystem paths and directory reparse-point behavior.
/// </summary>
public sealed class CanonicalVaultDirectoryResolverTests
{
    /// <summary>
    /// Verifies that relative path segments and trailing separators do not
    /// change the resolved physical directory identity.
    /// </summary>
    [Fact]
    public void Resolve_WhenPathContainsRelativeSegmentsAndTrailingSeparator_ReturnsNormalizedPath()
    {
        // Arrange
        using var context = new TestContext();

        string vaultDirectory =
            Path.Combine(
                context.RootDirectory,
                "Vault");

        Directory.CreateDirectory(
            vaultDirectory);

        string nonCanonicalPath =
            Path.Combine(
                context.RootDirectory,
                "Vault",
                ".",
                "Nested",
                "..") +
            Path.DirectorySeparatorChar;

        // Act
        string expectedPath =
            CreateResolver(vaultDirectory).Resolve();

        string actualPath =
            CreateResolver(nonCanonicalPath).Resolve();

        // Assert
        Assert.Equal(
            expectedPath,
            actualPath,
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            actualPath,
            Path.TrimEndingDirectorySeparator(actualPath));
    }

    /// <summary>
    /// Verifies that Windows path casing differences identify the same
    /// physical vault directory.
    /// </summary>
    [Fact]
    public void Resolve_WhenPathUsesDifferentCasing_ReturnsSameDirectoryIdentity()
    {
        // Arrange
        using var context = new TestContext();

        string differentlyCasedPath =
            context.RootDirectory.ToUpperInvariant();

        // Act
        string expectedPath =
            CreateResolver(context.RootDirectory).Resolve();

        string actualPath =
            CreateResolver(differentlyCasedPath).Resolve();

        // Assert
        Assert.Equal(
            expectedPath,
            actualPath,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that resolving a directory junction produces the physical
    /// identity of its target directory rather than the junction path.
    /// </summary>
    [Fact]
    public void Resolve_WhenPathUsesDirectoryJunction_ReturnsTargetDirectoryIdentity()
    {
        // Arrange
        using var context = new TestContext();

        string targetDirectory =
            Path.Combine(
                context.RootDirectory,
                "VaultTarget");

        string junctionDirectory =
            Path.Combine(
                context.RootDirectory,
                "VaultAlias");

        Directory.CreateDirectory(
            targetDirectory);

        try
        {
            // A directory junction exercises reparse-point resolution
            // without requiring symbolic-link privileges.
            CreateDirectoryJunction(
                junctionDirectory,
                targetDirectory);

            // Act
            string targetPath =
                CreateResolver(targetDirectory).Resolve();

            string junctionPath =
                CreateResolver(junctionDirectory).Resolve();

            // Assert
            Assert.Equal(
                targetPath,
                junctionPath,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            // Delete the junction itself before the test context removes
            // its parent directory. The target directory must remain intact.
            if (Directory.Exists(junctionDirectory))
            {
                Directory.Delete(
                    junctionDirectory);
            }
        }
    }

    /// <summary>
    /// Verifies that a nonexistent vault directory is rejected instead of
    /// being silently created or treated as a valid identity.
    /// </summary>
    [Fact]
    public void Resolve_WhenDirectoryDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        using var context = new TestContext();

        string missingDirectory =
            Path.Combine(
                context.RootDirectory,
                "MissingVault");

        // Act
        DirectoryNotFoundException exception =
            Assert.Throws<DirectoryNotFoundException>(
                () => CreateResolver(missingDirectory).Resolve());

        // Assert
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Verifies that a regular file cannot be accepted as a vault root.
    /// </summary>
    [Fact]
    public void Resolve_WhenPathIdentifiesAFile_ThrowsIOException()
    {
        // Arrange
        using var context = new TestContext();

        string filePath =
            Path.Combine(
                context.RootDirectory,
                "not-a-directory.txt");

        File.WriteAllText(
            filePath,
            "test");

        // Act
        IOException exception =
            Assert.Throws<IOException>(
                () => CreateResolver(filePath).Resolve());

        // Assert
        Assert.Contains(
            "does not identify a directory",
            exception.Message);
    }

    /// <summary>
    /// Verifies that a whitespace-only configured root is rejected with
    /// the expected parameter name.
    /// </summary>
    [Fact]
    public void Resolve_WhenConfiguredRootIsWhitespace_ThrowsArgumentException()
    {
        // Arrange
        CanonicalVaultDirectoryResolver resolver =
            CreateResolver(" ");

        // Act
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => resolver.Resolve());

        // Assert
        Assert.Equal(
            "rootDirectory",
            exception.ParamName);
    }

    /// <summary>
    /// Creates a resolver configured with the supplied vault root.
    /// </summary>
    /// <param name="rootDirectory">
    /// The vault root to resolve.
    /// </param>
    /// <returns>
    /// A resolver configured for the supplied root.
    /// </returns>
    private static CanonicalVaultDirectoryResolver CreateResolver(
        string rootDirectory)
    {
        return new CanonicalVaultDirectoryResolver(
            new DeskVaultDataPaths(
                rootDirectory));
    }

    /// <summary>
    /// Creates a Windows directory junction for testing physical path
    /// resolution through a directory reparse point.
    /// </summary>
    /// <param name="junctionPath">
    /// The path at which the directory junction is created.
    /// </param>
    /// <param name="targetDirectoryPath">
    /// The existing directory targeted by the junction.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The junction command cannot be started or fails.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// The junction command does not finish within the allowed time.
    /// </exception>
    private static void CreateDirectoryJunction(
        string junctionPath,
        string targetDirectoryPath)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    Path.Combine(
                        Environment.SystemDirectory,
                        "cmd.exe"),

                Arguments =
                    $"/c mklink /J \"{junctionPath}\" \"{targetDirectoryPath}\"",

                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        using Process process =
            Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "Could not start the Windows command processor to create a directory junction.");

        if (!process.WaitForExit(
                (int)TimeSpan.FromSeconds(10).TotalMilliseconds))
        {
            // Stop the command if junction creation becomes unresponsive.
            process.Kill(
                entireProcessTree: true);

            process.WaitForExit();

            throw new TimeoutException(
                "Creating the directory junction did not finish within the expected time.");
        }

        string standardOutput =
            process.StandardOutput.ReadToEnd();

        string standardError =
            process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Creating the directory junction failed. Output: {standardOutput} Error: {standardError}");
        }
    }

    /// <summary>
    /// Owns an isolated temporary directory for one resolver test and
    /// removes it after the test completes.
    /// </summary>
    private sealed class TestContext : IDisposable
    {
        /// <summary>
        /// Gets the temporary root directory used by this test.
        /// </summary>
        public string RootDirectory { get; }

        /// <summary>
        /// Creates a unique temporary directory for the test.
        /// </summary>
        public TestContext()
        {
            RootDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultCanonicalVaultDirectoryResolverTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                RootDirectory);
        }

        /// <summary>
        /// Removes the temporary directory and its test artifacts.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(
                    RootDirectory,
                    recursive: true);
            }
        }
    }
}

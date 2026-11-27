using DeskVault.Infrastructure.Services;
using System.Diagnostics;

namespace DeskVault.Infrastructure.Tests;

/// <summary>
/// Verifies document-artifact coordination paths are isolated by canonical
/// vault identity and document identity.
/// </summary>
public sealed class DocumentArtifactLeasePathResolverTests
{
    /// <summary>
    /// Verifies that a valid document ID resolves to a lock file below the
    /// dedicated coordination directory, outside the Documents directory.
    /// </summary>
    [Fact]
    public void GetPath_WhenDocumentIdIsValid_ReturnsPathUnderDedicatedCoordinationDirectory()
    {
        // Arrange
        using var context = new TestContext();
        Guid documentId = Guid.NewGuid();

        string expectedDirectory =
            Path.Combine(
                context.RootDirectory,
                "Security",
                "Coordination",
                "DocumentArtifacts");

        string expectedPath =
            Path.Combine(
                expectedDirectory,
                $"{documentId:D}.lock");

        // Act
        string actualPath =
            context.Resolver.GetPath(
                documentId);

        // Assert
        Assert.Equal(
            expectedPath,
            actualPath,
            StringComparer.OrdinalIgnoreCase);

        Assert.True(
            Directory.Exists(expectedDirectory));

        Assert.False(
            IsPathWithinDirectory(
                context.DataPaths.DocumentsDirectory,
                actualPath));
    }

    /// <summary>
    /// Verifies that an empty document ID is rejected without creating
    /// coordination directories.
    /// </summary>
    [Fact]
    public void GetPath_WhenDocumentIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        using var context = new TestContext();

        // Act
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => context.Resolver.GetPath(Guid.Empty));

        // Assert
        Assert.Equal(
            "documentId",
            exception.ParamName);

        Assert.False(
            Directory.Exists(
                context.DataPaths.SecurityDirectory));
    }

    /// <summary>
    /// Verifies that different document IDs map to independent
    /// coordination-file paths within the same vault.
    /// </summary>
    [Fact]
    public void GetPath_WhenDocumentIdsAreDifferent_ReturnsDifferentPaths()
    {
        // Arrange
        using var context = new TestContext();

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        // Act
        string firstPath =
            context.Resolver.GetPath(
                firstDocumentId);

        string secondPath =
            context.Resolver.GetPath(
                secondDocumentId);

        // Assert
        Assert.NotEqual(
            firstPath,
            secondPath);

        Assert.EndsWith(
            $"{firstDocumentId:D}.lock",
            firstPath,
            StringComparison.OrdinalIgnoreCase);

        Assert.EndsWith(
            $"{secondDocumentId:D}.lock",
            secondPath,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that two different configured paths resolving to the same
    /// physical vault produce the same coordination-file path.
    /// </summary>
    [Fact]
    public void GetPath_WhenVaultIsAccessedThroughDirectoryJunction_ReturnsSamePath()
    {
        // Arrange
        using var context = new TestContext();

        string junctionPath =
            Path.Combine(
                context.TestDirectory,
                "VaultAlias");

        CreateDirectoryJunction(
            junctionPath,
            context.RootDirectory);

        try
        {
            Guid documentId = Guid.NewGuid();

            var aliasResolver =
                CreateResolver(junctionPath);

            // Act
            string originalPath =
                context.Resolver.GetPath(
                    documentId);

            string aliasResolvedPath =
                aliasResolver.GetPath(
                    documentId);

            // Assert
            Assert.Equal(
                originalPath,
                aliasResolvedPath,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            // Remove the junction itself before TestContext recursively
            // deletes its parent. Directory.Delete without recursion removes
            // the reparse-point entry rather than deleting its target.
            if (Directory.Exists(junctionPath))
            {
                Directory.Delete(
                    junctionPath,
                    recursive: false);
            }
        }
    }

    /// <summary>
    /// Verifies that a Security directory redirected outside the vault
    /// is rejected before descendant coordination directories are created.
    /// </summary>
    [Fact]
    public void GetPath_WhenSecurityDirectoryIsRedirectedOutsideVault_ThrowsIOException()
    {
        // Arrange
        using var context = new TestContext();

        string outsideDirectory =
            Path.Combine(
                context.TestDirectory,
                "OutsideVault");

        string securityDirectory =
            context.DataPaths.SecurityDirectory;

        Directory.CreateDirectory(
            outsideDirectory);

        CreateDirectoryJunction(
            securityDirectory,
            outsideDirectory);

        try
        {
            // Act
            IOException exception =
                Assert.Throws<IOException>(
                    () => context.Resolver.GetPath(Guid.NewGuid()));

            // Assert
            Assert.Contains(
                "does not resolve to its expected location",
                exception.Message);

            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        outsideDirectory,
                        "Coordination")));
        }
        finally
        {
            // Remove the junction itself, not its external target.
            if (Directory.Exists(securityDirectory))
            {
                Directory.Delete(
                    securityDirectory);
            }
        }
    }

    /// <summary>
    /// Creates a path resolver bound to the supplied vault root.
    /// </summary>
    /// <param name="rootDirectory">
    /// The configured vault root.
    /// </param>
    /// <returns>
    /// A resolver for the supplied vault.
    /// </returns>
    private static DocumentArtifactLeasePathResolver CreateResolver(
        string rootDirectory)
    {
        var dataPaths =
            new DeskVaultDataPaths(
                rootDirectory);

        var directoryResolver =
            new CanonicalVaultDirectoryResolver(
                dataPaths);

        return new DocumentArtifactLeasePathResolver(
            directoryResolver);
    }

    /// <summary>
    /// Determines whether a file path is contained within a directory.
    /// </summary>
    /// <param name="directoryPath">
    /// The directory used as the containment boundary.
    /// </param>
    /// <param name="filePath">
    /// The file path being checked.
    /// </param>
    /// <returns>
    /// True when the file path is a descendant of the directory; otherwise false.
    /// </returns>
    private static bool IsPathWithinDirectory(
        string directoryPath,
        string filePath)
    {
        string relativePath =
            Path.GetRelativePath(
                directoryPath,
                filePath);

        return
            !Path.IsPathRooted(relativePath) &&
            !string.Equals(
                relativePath,
                "..",
                StringComparison.Ordinal) &&
            !relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a Windows directory junction for testing canonical vault
    /// resolution and coordination-directory redirection.
    /// </summary>
    /// <param name="junctionPath">
    /// The path at which the junction is created.
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
            // Terminate the command if junction creation becomes unresponsive.
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
    /// Owns an isolated temporary directory and resolver for one test.
    /// </summary>
    private sealed class TestContext : IDisposable
    {
        /// <summary>
        /// Gets the temporary test directory containing the vault.
        /// </summary>
        public string TestDirectory { get; }

        /// <summary>
        /// Gets the temporary vault root.
        /// </summary>
        public string RootDirectory { get; }

        /// <summary>
        /// Gets the configured vault data paths.
        /// </summary>
        public DeskVaultDataPaths DataPaths { get; }

        /// <summary>
        /// Gets the canonical directory resolver for the test vault.
        /// </summary>
        public CanonicalVaultDirectoryResolver DirectoryResolver { get; }

        /// <summary>
        /// Gets the document-artifact lease path resolver.
        /// </summary>
        public DocumentArtifactLeasePathResolver Resolver { get; }

        /// <summary>
        /// Creates a unique temporary vault and its path resolvers.
        /// </summary>
        public TestContext()
        {
            TestDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultDocumentArtifactLeasePathResolverTests",
                    Guid.NewGuid().ToString("N"));

            RootDirectory =
                Path.Combine(
                    TestDirectory,
                    "Vault");

            Directory.CreateDirectory(
                RootDirectory);

            DataPaths =
                new DeskVaultDataPaths(
                    RootDirectory);

            DirectoryResolver =
                new CanonicalVaultDirectoryResolver(
                    DataPaths);

            Resolver =
                new DocumentArtifactLeasePathResolver(
                    DirectoryResolver);
        }

        /// <summary>
        /// Removes the temporary test directory and its artifacts.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(TestDirectory))
            {
                Directory.Delete(
                    TestDirectory,
                    recursive: true);
            }
        }
    }
}

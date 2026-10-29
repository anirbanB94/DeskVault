using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentArtifactEnumeratorTests
{
    [Fact]
    public async Task EnumerateAsync_WhenDocumentsDirectoryDoesNotExist_ReturnsEmptyResult()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            // Act
            List<string> result = [];

            await foreach (string artifactPath in enumerator.EnumerateIncrementallyAsync(
                               TestContext.Current.CancellationToken))
            {
                result.Add(artifactPath);
            }

            // Assert
            Assert.Empty(result);
        }
        finally
        {
            if (Directory.Exists(
                    rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnumerateAsync_WhenDocumentsDirectoryContainsDvaultFiles_ReturnsOnlyDvaultFiles()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            string firstArtifactPath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{Guid.NewGuid()}.dvault");

            string secondArtifactPath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{Guid.NewGuid()}.dvault");

            string unrelatedFilePath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    "not-an-artifact.txt");

            await File.WriteAllTextAsync(firstArtifactPath, "artifact", TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(secondArtifactPath, "artifact", TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(unrelatedFilePath, "unrelated", TestContext.Current.CancellationToken);

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            // Act
            List<string> result = [];

            await foreach (string artifactPath in enumerator.EnumerateIncrementallyAsync(
                               TestContext.Current.CancellationToken))
            {
                result.Add(artifactPath);
            }

            // Assert
            Assert.Equal(
                2,
                result.Count);

            Assert.Contains(
                firstArtifactPath,
                result);

            Assert.Contains(
                secondArtifactPath,
                result);

            Assert.DoesNotContain(
                unrelatedFilePath,
                result);
        }
        finally
        {
            if (Directory.Exists(
                    rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnumerateAsync_WhenArtifactExistsInNestedDirectory_DoesNotReturnNestedArtifact()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            string nestedDirectory =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    "nested");

            Directory.CreateDirectory(
                nestedDirectory);

            string nestedArtifactPath =
                Path.Combine(
                    nestedDirectory,
                    $"{Guid.NewGuid()}.dvault");

            await File.WriteAllTextAsync(nestedArtifactPath, "artifact", TestContext.Current.CancellationToken);

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            // Act
            List<string> result = [];

            await foreach (string artifactPath in enumerator.EnumerateIncrementallyAsync(
                               TestContext.Current.CancellationToken))
            {
                result.Add(artifactPath);
            }

            // Assert
            Assert.Empty(result);
        }
        finally
        {
            if (Directory.Exists(
                    rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnumerateAsync_WhenCancellationIsRequestedBeforeEnumeration_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () =>
                {
                    await foreach (string _ in enumerator.EnumerateIncrementallyAsync(
                                       cancellationTokenSource.Token))
                    {
                        // Cancellation should stop enumeration before yielding an artifact.
                    }
                });
        }
        finally
        {
            if (Directory.Exists(
                    rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnumerateIncrementallyAsync_WhenDirectoryContainsArtifacts_ReturnsOnlyTopLevelDvaultFiles()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            string firstArtifactPath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{Guid.NewGuid()}.dvault");

            string unrelatedFilePath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    "not-an-artifact.txt");

            string nestedDirectory =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    "nested");

            Directory.CreateDirectory(nestedDirectory);

            string nestedArtifactPath =
                Path.Combine(
                    nestedDirectory,
                    $"{Guid.NewGuid()}.dvault");

            await File.WriteAllTextAsync(
                firstArtifactPath,
                "artifact",
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                unrelatedFilePath,
                "unrelated",
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                nestedArtifactPath,
                "nested artifact",
                TestContext.Current.CancellationToken);

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            var result =
                new List<string>();

            await foreach (
                string artifactPath in enumerator.EnumerateIncrementallyAsync(
                    TestContext.Current.CancellationToken))
            {
                result.Add(artifactPath);
            }

            Assert.Equal(
                [firstArtifactPath],
                result);

            Assert.DoesNotContain(
                unrelatedFilePath,
                result);

            Assert.DoesNotContain(
                nestedArtifactPath,
                result);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnumerateIncrementallyAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var enumerator =
                new DocumentArtifactEnumerator(
                    new DeskVaultDataPaths(rootDirectory));

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () =>
                {
                    await foreach (
                        string _ in enumerator.EnumerateIncrementallyAsync(
                            cancellationTokenSource.Token))
                    {
                        // Enumeration should stop before yielding an artifact.
                    }
                });
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void ExistsForDocument_WhenCanonicalArtifactExists_ReturnsTrue()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(dataPaths.DocumentsDirectory);

            Guid documentId = Guid.NewGuid();

            string artifactPath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            File.WriteAllText(artifactPath, "artifact");

            var enumerator =
                new DocumentArtifactEnumerator(dataPaths);

            Assert.True(enumerator.ExistsForDocument(documentId));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistsForDocument_WhenCanonicalArtifactDoesNotExist_ReturnsFalse()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var enumerator =
                new DocumentArtifactEnumerator(
                    new DeskVaultDataPaths(rootDirectory));

            Assert.False(
                enumerator.ExistsForDocument(Guid.NewGuid()));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistsForDocument_WhenDocumentIdIsEmpty_ThrowsArgumentException()
    {
        var enumerator =
            new DocumentArtifactEnumerator(
                new DeskVaultDataPaths(
                    Path.Combine(
                        Path.GetTempPath(),
                        "DeskVaultInfrastructureTests",
                        Guid.NewGuid().ToString("N"))));

        Assert.Throws<ArgumentException>(
            () => enumerator.ExistsForDocument(Guid.Empty));
    }

    [Fact]
    public void ExistsForDocument_WhenCanonicalArtifactPathIsDirectory_ThrowsIOException()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultInfrastructureTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            Guid documentId = Guid.NewGuid();

            string artifactPath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            Directory.CreateDirectory(artifactPath);

            var enumerator =
                new DocumentArtifactEnumerator(dataPaths);

            Assert.Throws<IOException>(
                () => enumerator.ExistsForDocument(documentId));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }
}

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
            IReadOnlyList<string> result =
                await enumerator.EnumerateAsync();

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

            await File.WriteAllTextAsync(
                firstArtifactPath,
                "artifact");

            await File.WriteAllTextAsync(
                secondArtifactPath,
                "artifact");

            await File.WriteAllTextAsync(
                unrelatedFilePath,
                "unrelated");

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            // Act
            IReadOnlyList<string> result =
                await enumerator.EnumerateAsync();

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

            await File.WriteAllTextAsync(
                nestedArtifactPath,
                "artifact");

            var enumerator =
                new DocumentArtifactEnumerator(
                    dataPaths);

            // Act
            IReadOnlyList<string> result =
                await enumerator.EnumerateAsync();

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
            await Assert.ThrowsAsync<OperationCanceledException>(
                () =>
                    enumerator.EnumerateAsync(
                        cancellationTokenSource.Token));
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
}

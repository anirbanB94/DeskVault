using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsEncryptionKeyServiceIntegrationTests
{
    [Fact]
    public async Task EncryptAndDecryptAsync_WhenFirstUseIsConcurrent_PreservesDataAcrossServiceInstances()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] firstContent =
            Encoding.UTF8.GetBytes(
                "DeskVault concurrent master-key initialization integration test.");

        byte[] secondContent =
            Encoding.UTF8.GetBytes(
                "DeskVault verifies concurrent encryption uses the authoritative master key.");

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            DocumentEncryptionService firstEncryptionService =
                CreateEncryptionService(dataPaths);

            DocumentEncryptionService secondEncryptionService =
                CreateEncryptionService(dataPaths);

            await using var firstSource =
                new MemoryStream(firstContent);

            await using var secondSource =
                new MemoryStream(secondContent);

            await using var firstEncrypted =
                new MemoryStream();

            await using var secondEncrypted =
                new MemoryStream();

            using var startGate =
                new ManualResetEventSlim(false);

            Task firstEncryptionTask =
                EncryptWhenReleasedAsync(
                    firstEncryptionService,
                    firstSource,
                    firstEncrypted,
                    startGate);

            Task secondEncryptionTask =
                EncryptWhenReleasedAsync(
                    secondEncryptionService,
                    secondSource,
                    secondEncrypted,
                    startGate);

            string keyFilePath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "master.key");

            // Act
            startGate.Set();

            await Task.WhenAll(
                firstEncryptionTask,
                secondEncryptionTask);

            firstEncrypted.Position = 0;
            secondEncrypted.Position = 0;

            DocumentEncryptionService firstDecryptionService =
                CreateEncryptionService(dataPaths);

            DocumentEncryptionService secondDecryptionService =
                CreateEncryptionService(dataPaths);

            await using var firstDecrypted =
                new MemoryStream();

            await using var secondDecrypted =
                new MemoryStream();

            await firstDecryptionService.DecryptAsync(
                firstEncrypted,
                firstDecrypted);

            await secondDecryptionService.DecryptAsync(
                secondEncrypted,
                secondDecrypted);

            // Assert
            Assert.True(
                File.Exists(keyFilePath));

            Assert.Single(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));

            Assert.Equal(
                firstContent,
                firstDecrypted.ToArray());

            Assert.Equal(
                secondContent,
                secondDecrypted.ToArray());
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DecryptAsync_WhenMasterKeyIsMissing_ThrowsWithoutCreatingReplacementKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault missing master-key integration regression test.");

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            DocumentEncryptionService encryptionService =
                CreateEncryptionService(dataPaths);

            await using var source =
                new MemoryStream(originalContent);

            await using var encrypted =
                new MemoryStream();

            await encryptionService.EncryptAsync(
                source,
                encrypted);

            string keyFilePath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "master.key");

            Assert.True(
                File.Exists(keyFilePath));

            File.Delete(keyFilePath);

            encrypted.Position = 0;

            DocumentEncryptionService decryptionService =
                CreateEncryptionService(dataPaths);

            await using var decrypted =
                new MemoryStream();

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    decryptionService.DecryptAsync(
                        encrypted,
                        decrypted));

            Assert.False(
                File.Exists(keyFilePath));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));

            Assert.Empty(
                decrypted.ToArray());
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static Task EncryptWhenReleasedAsync(
        DocumentEncryptionService encryptionService,
        Stream source,
        Stream destination,
        ManualResetEventSlim startGate)
    {
        return Task.Run(
            async () =>
            {
                startGate.Wait();

                await encryptionService.EncryptAsync(
                    source,
                    destination);
            });
    }

    private static DocumentEncryptionService CreateEncryptionService(
        DeskVaultDataPaths dataPaths)
    {
        return new DocumentEncryptionService(
            new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance),
            NullLogger<DocumentEncryptionService>.Instance);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            directory);

        return directory;
    }

    private static void DeleteTemporaryDirectory(
        string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class Sha256HashServiceTests
{
    private readonly Sha256HashService _service =
        new(
            NullLogger<Sha256HashService>.Instance);

    [Fact]
    public async Task ComputeSha256Async_WhenFileContainsKnownContent_ReturnsExpectedHash()
    {
        const string content =
            "DeskVault";

        string filePath =
            CreateTemporaryFile(
                content);

        try
        {
            string hash =
                await _service.ComputeSha256Async(
                    filePath);

            byte[] expectedBytes =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        content));

            string expectedHash =
                Convert.ToHexString(
                    expectedBytes)
                .ToLowerInvariant();

            Assert.Equal(
                expectedHash,
                hash);
        }
        finally
        {
            DeleteTemporaryFile(
                filePath);
        }
    }

    [Fact]
    public async Task ComputeSha256Async_WhenFileIsEmpty_ReturnsExpectedHash()
    {
        string filePath =
            CreateTemporaryFile(
                string.Empty);

        try
        {
            string hash =
                await _service.ComputeSha256Async(
                    filePath);

            Assert.Equal(
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                hash);
        }
        finally
        {
            DeleteTemporaryFile(
                filePath);
        }
    }

    [Fact]
    public async Task ComputeSha256Async_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                _service.ComputeSha256Async(
                    filePath));
    }

    private static string CreateTemporaryFile(
        string content)
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.txt");

        File.WriteAllText(
            filePath,
            content);

        return filePath;
    }

    private static void DeleteTemporaryFile(
        string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

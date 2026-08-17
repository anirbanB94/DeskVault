using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class Sha256HashServiceTests
{
    private readonly Sha256HashService _service = new();

    [Fact]
    public async Task ComputeSha256Async_WhenFileContainsKnownContent_ReturnsExpectedHash()
    {
        string content = "DeskVault";
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(filePath, content);

        try
        {
            string hash = await _service.ComputeSha256Async(filePath);

            byte[] expectedBytes =
                SHA256.HashData(Encoding.UTF8.GetBytes(content));

            string expectedHash =
                Convert.ToHexString(expectedBytes).ToLowerInvariant();

            Assert.Equal(expectedHash, hash);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ComputeSha256Async_WhenFileIsEmpty_ReturnsExpectedHash()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(filePath, string.Empty);

        try
        {
            string hash = await _service.ComputeSha256Async(filePath);

            Assert.Equal(
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                hash);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

using System.Security.Cryptography;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDatabasePipelineIntegrationTests
{
    [Fact]
    public async Task EncryptedDatabase_WhenUsingProductionInfrastructurePath_SupportsRepositoryProcessingAndSearch()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        Guid documentId =
            Guid.NewGuid();

        try
        {
            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (serviceProvider)
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();

                var repository =
                    serviceProvider.GetRequiredService<IDocumentRepository>();

                var processingStore =
                    serviceProvider.GetRequiredService<IDocumentProcessingStore>();

                var searchStore =
                    serviceProvider.GetRequiredService<IDocumentSearchStore>();

                var document =
                    Document.Create(
                        documentId,
                        "encrypted-integration-test.txt",
                        "Encrypted Database Integration Test",
                        "test-sha256-hash",
                        Path.Combine(
                            rootDirectory,
                            "Documents",
                            "encrypted-integration-test.dvault"));

                await repository.AddAsync(
                    document);

                Document? storedDocument =
                    await repository.GetByIdAsync(
                        documentId);

                Assert.NotNull(
                    storedDocument);

                Assert.Equal(
                    documentId,
                    storedDocument.Id);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    storedDocument.DisplayName);

                var chunks =
                    new[]
                    {
                        new DocumentChunk(
                            0,
                            "DeskVault encrypted database integration testing."),
                        new DocumentChunk(
                            1,
                            "SQLite3MC protects the local document metadata and search index.")
                    };

                await processingStore.ReplaceChunksAsync(
                    documentId,
                    chunks);

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    await searchStore.SearchAsync(
                        "SQLite3MC");

                SearchDocumentsResult matchingResult =
                    Assert.Single(
                        searchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "encrypted-integration-test.txt",
                    matchingResult.FileName);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    matchingResult.DisplayName);

                Assert.Contains(
                    "SQLite3MC",
                    matchingResult.ChunkText,
                    StringComparison.OrdinalIgnoreCase);
            }

            byte[] databaseHeader =
                await ReadDatabaseHeaderAsync(
                    Path.Combine(
                        rootDirectory,
                        "DeskVault.db"));

            Assert.NotEqual(
                "SQLite format 3",
                System.Text.Encoding.ASCII.GetString(
                    databaseHeader,
                    0,
                    Math.Min(
                        15,
                        databaseHeader.Length)));

            ServiceProvider secondServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (secondServiceProvider)
            {
                var repository =
                    secondServiceProvider.GetRequiredService<IDocumentRepository>();

                var searchStore =
                    secondServiceProvider.GetRequiredService<IDocumentSearchStore>();

                Document? restoredDocument =
                    await repository.GetByIdAsync(
                        documentId);

                Assert.NotNull(
                    restoredDocument);

                Assert.Equal(
                    documentId,
                    restoredDocument.Id);

                IReadOnlyList<SearchDocumentsResult> restoredSearchResults =
                    await searchStore.SearchAsync(
                        "encrypted database");

                SearchDocumentsResult restoredResult =
                    Assert.Single(
                        restoredSearchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    restoredResult.DisplayName);
            }
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static ServiceProvider BuildServiceProvider(
        string rootDirectory,
        byte[] databaseKey)
    {
        var services =
            new ServiceCollection();

        services.AddLogging();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddSingleton(
            new DeskVaultDataPaths(
                rootDirectory));

        services.AddInfrastructure(
            configuration);

        services.AddSingleton<IDatabaseEncryptionKeyService>(
            new TestDatabaseEncryptionKeyService(
                databaseKey));

        return services.BuildServiceProvider();
    }

    private static async Task<byte[]> ReadDatabaseHeaderAsync(
        string databasePath)
    {
        await using FileStream stream =
            new FileStream(
                databasePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        byte[] header =
            new byte[32];

        int bytesRead =
            await stream.ReadAsync(
                header);

        return header[..bytesRead];
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultEncryptedDatabaseTests",
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

    private sealed class TestDatabaseEncryptionKeyService
        : IDatabaseEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestDatabaseEncryptionKeyService(
            byte[] key)
        {
            _key =
                key.ToArray();
        }

        public Task<byte[]> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _key.ToArray());
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using DeskVault.Application;
using DeskVault.Application.Documents.Commands.ImportDocument;
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
    public async Task EncryptedDatabase_WhenUsingProductionInfrastructurePath_SupportsDocumentImportProcessingAndSearch()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "encrypted-integration-test.txt");

            string sourceText =
                """
                DeskVault encrypted database integration testing.

                This document contains searchable enterprise architecture content.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;

            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (serviceProvider)
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();

                var importHandler =
                    serviceProvider.GetRequiredService<ImportDocumentHandler>();

                var processingService =
                    serviceProvider.GetRequiredService<IDocumentProcessingService>();

                var searchHandler =
                    serviceProvider.GetRequiredService<SearchDocumentsHandler>();

                ImportDocumentResult importResult =
                    await importHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Encrypted Database Integration Test"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                var repository =
                    serviceProvider.GetRequiredService<IDocumentRepository>();

                Document? importedDocument =
                    await repository.GetByIdAsync(
                        documentId);

                Assert.NotNull(
                    importedDocument);

                Assert.Equal(
                    documentId,
                    importedDocument.Id);

                Assert.Equal(
                    "encrypted-integration-test.txt",
                    importedDocument.FileName);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    importedDocument.DisplayName);

                Assert.Equal(
                    DocumentStatus.Imported,
                    importedDocument.Status);

                Assert.True(
                    File.Exists(
                        importedDocument.StoredFilePath));

                Assert.EndsWith(
                    ".dvault",
                    importedDocument.StoredFilePath,
                    StringComparison.OrdinalIgnoreCase);

                await processingService.ProcessAsync(
                    documentId);

                Document? processedDocument =
                    await repository.GetByIdAsync(
                        documentId);

                Assert.NotNull(
                    processedDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    processedDocument.Status);

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    await searchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "ENTERPRISE ARCHITECTURE"));

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
                    "enterprise architecture",
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
                Encoding.ASCII.GetString(
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

                var searchHandler =
                    secondServiceProvider.GetRequiredService<SearchDocumentsHandler>();

                Document? restoredDocument =
                    await repository.GetByIdAsync(
                        documentId);

                Assert.NotNull(
                    restoredDocument);

                Assert.Equal(
                    documentId,
                    restoredDocument.Id);

                Assert.Equal(
                    "encrypted-integration-test.txt",
                    restoredDocument.FileName);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    restoredDocument.DisplayName);

                Assert.Equal(
                    DocumentStatus.Available,
                    restoredDocument.Status);

                Assert.True(
                    File.Exists(
                        restoredDocument.StoredFilePath));

                IReadOnlyList<SearchDocumentsResult> restoredSearchResults =
                    await searchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "encrypted database"));

                SearchDocumentsResult restoredResult =
                    Assert.Single(
                        restoredSearchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    restoredResult.DisplayName);

                Assert.Contains(
                    "encrypted database",
                    restoredResult.ChunkText,
                    StringComparison.OrdinalIgnoreCase);
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

        services.AddApplication();

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

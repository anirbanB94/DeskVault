using System.Security.Cryptography;
using System.Text;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Entities;

namespace DeskVault.Integration.Tests;

public sealed class DocumentImportIntegrationTests
{
    [Fact]
    public async Task ImportDocument_WhenValidTextDocument_CompletesProcessingAndMakesDocumentSearchable()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "integration-test.txt");

            string sourceText =
                """
                DeskVault integration testing verifies the complete document pipeline.

                This document contains searchable enterprise architecture content.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;

            await using (
                var firstInstance =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await firstInstance.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Integration Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                Document? document =
                    await firstInstance.GetDocumentAsync(
                        documentId);

                Assert.NotNull(document);

                Assert.Equal(
                    "integration-test.txt",
                    document.FileName);

                Assert.Equal(
                    "Integration Test Document",
                    document.DisplayName);

                Assert.Equal(
                    DocumentStatus.Available,
                    document.Status);

                Assert.True(
                    File.Exists(
                        document.StoredFilePath));

                Assert.EndsWith(
                    ".dvault",
                    document.StoredFilePath,
                    StringComparison.OrdinalIgnoreCase);

                List<DocumentChunkEntity> chunks =
                    await firstInstance.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(chunks);

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    await firstInstance.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "ENTERPRISE ARCHITECTURE"));

                SearchDocumentsResult matchingResult =
                    Assert.Single(
                        searchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "integration-test.txt",
                    matchingResult.FileName);

                Assert.Equal(
                    "Integration Test Document",
                    matchingResult.DisplayName);

                Assert.Contains(
                    "enterprise architecture",
                    matchingResult.ChunkText,
                    StringComparison.OrdinalIgnoreCase);
            }

            await using (
                var secondInstance =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                Document? restoredDocument =
                    await secondInstance.GetDocumentAsync(
                        documentId);

                Assert.NotNull(restoredDocument);

                Assert.Equal(
                    documentId,
                    restoredDocument.Id);

                Assert.Equal(
                    "integration-test.txt",
                    restoredDocument.FileName);

                Assert.Equal(
                    "Integration Test Document",
                    restoredDocument.DisplayName);

                Assert.Equal(
                    DocumentStatus.Available,
                    restoredDocument.Status);

                Assert.True(
                    File.Exists(
                        restoredDocument.StoredFilePath));

                List<DocumentChunkEntity> restoredChunks =
                    await secondInstance.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    restoredChunks);

                string indexedText =
                    string.Join(
                        "\n",
                        restoredChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));

                Assert.Contains(
                    "DeskVault integration testing",
                    indexedText);

                Assert.Contains(
                    "searchable enterprise architecture content",
                    indexedText);

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    await secondInstance.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "ENTERPRISE ARCHITECTURE"));

                SearchDocumentsResult matchingResult =
                    Assert.Single(
                        searchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "integration-test.txt",
                    matchingResult.FileName);

                Assert.Equal(
                    "Integration Test Document",
                    matchingResult.DisplayName);

                Assert.Contains(
                    "enterprise architecture",
                    matchingResult.ChunkText,
                    StringComparison.OrdinalIgnoreCase);
            }
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

using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Entities;
using System.Security.Cryptography;
using System.Text;

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

    [Fact]
    public async Task ImportDocument_WhenSameDocumentIsImportedTwice_ReturnsDuplicateAndDoesNotCreateSecondDocument()
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
                    "duplicate-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault duplicate import integration test.");

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            ImportDocumentResult firstResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "First Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                firstResult.Status);

            Assert.NotNull(
                firstResult.DocumentId);

            ImportDocumentResult secondResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Second Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Duplicate,
                secondResult.Status);

            Assert.Null(
                secondResult.DocumentId);

            Assert.Equal(
                "The document has already been imported.",
                secondResult.Description);

            Document? document =
                await harness.GetDocumentAsync(
                    firstResult.DocumentId.Value);

            Assert.NotNull(document);

            Assert.Equal(
                "First Document",
                document.DisplayName);
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
    public async Task RemoveDocument_WhenDocumentExists_RemovesStoredFileMetadataAndChunks()
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
                    "remove-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                """
                DeskVault removal integration testing verifies
                complete document cleanup across storage and persistence.
                """);

            Guid documentId;
            string storedFilePath;

            await using (
                var instance =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await instance.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Removal Integration Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                Document? document =
                    await instance.GetDocumentAsync(
                        documentId);

                Assert.NotNull(document);

                storedFilePath =
                    document.StoredFilePath;

                Assert.True(
                    File.Exists(
                        storedFilePath));

                List<DocumentChunkEntity> chunks =
                    await instance.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(chunks);

                RemoveDocumentResult removeResult =
                    await instance.RemoveHandler.HandleAsync(
                        new RemoveDocumentCommand(
                            documentId));

                Assert.Equal(
                    RemoveDocumentResultStatus.Success,
                    removeResult.Status);

                Assert.False(
                    File.Exists(
                        storedFilePath));

                Document? removedDocument =
                    await instance.GetDocumentAsync(
                        documentId);

                Assert.Null(
                    removedDocument);

                List<DocumentChunkEntity> removedChunks =
                    await instance.GetChunksAsync(
                        documentId);

                Assert.Empty(
                    removedChunks);
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

    [Fact]
    public async Task ImportDocument_WhenProcessingFails_PersistsFailedStatusAndKeepsStoredFile()
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
                    "processing-failure-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault processing failure integration test.");

            var failingExtractor =
                new FailingDocumentTextExtractor();

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [failingExtractor]);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    harness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Processing Failure Test Document")));

            IReadOnlyList<Document> documents =
                await harness.GetDocumentsAsync();

            Document document =
                Assert.Single(documents);

            Assert.Equal(
                "processing-failure-test.txt",
                document.FileName);

            Assert.Equal(
                "Processing Failure Test Document",
                document.DisplayName);

            Assert.Equal(
                DocumentStatus.Failed,
                document.Status);

            Assert.True(
                File.Exists(
                    document.StoredFilePath));

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    document.Id);

            Assert.Empty(chunks);

            Assert.True(
                failingExtractor.WasCalled);
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
    public async Task ImportDocument_WhenValidCsvDocument_CompletesProcessingAndMakesDocumentSearchable()
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
                    "integration-test.csv");

            string sourceText =
                """
            Id,Name,Department
            1001,Alice Johnson,Engineering
            1002,Bob Smith,Design
            """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Integration CSV Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Document? document =
                await harness.GetDocumentAsync(
                    importResult.DocumentId.Value);

            Assert.NotNull(document);

            Assert.Equal(
                "integration-test.csv",
                document.FileName);

            Assert.Equal(
                "Integration CSV Document",
                document.DisplayName);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    document.Id);

            Assert.NotEmpty(chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "Name: Alice Johnson",
                indexedText);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "Alice Johnson"));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == document.Id);

            Assert.Equal(
                "integration-test.csv",
                matchingResult.FileName);

            Assert.Equal(
                "Integration CSV Document",
                matchingResult.DisplayName);

            Assert.Contains(
                "Alice Johnson",
                matchingResult.ChunkText,
                StringComparison.OrdinalIgnoreCase);
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
    public async Task ImportDocument_WhenValidMarkdownDocument_CompletesProcessingAndMakesDocumentSearchable()
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
                    "integration-test.md");

            string sourceText =
                """
            # DeskVault Integration Test

            This document contains searchable markdown architecture content.

            ## Processing

            Markdown syntax should remain preserved.
            """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Integration Markdown Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Document? document =
                await harness.GetDocumentAsync(
                    importResult.DocumentId.Value);

            Assert.NotNull(document);

            Assert.Equal(
                "integration-test.md",
                document.FileName);

            Assert.Equal(
                "Integration Markdown Document",
                document.DisplayName);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    document.Id);

            Assert.NotEmpty(chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "searchable markdown architecture content",
                indexedText);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "markdown architecture"));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == document.Id);

            Assert.Equal(
                "integration-test.md",
                matchingResult.FileName);

            Assert.Equal(
                "Integration Markdown Document",
                matchingResult.DisplayName);

            Assert.Contains(
                "markdown architecture",
                matchingResult.ChunkText,
                StringComparison.OrdinalIgnoreCase);
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

    private sealed class FailingDocumentTextExtractor
        : IDocumentTextExtractor
    {
        public bool WasCalled { get; private set; }

        public bool CanExtract(
            string fileName)
        {
            return fileName.EndsWith(
                ".txt",
                StringComparison.OrdinalIgnoreCase);
        }

        public Task<DocumentTextExtractionResult> ExtractAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;

            throw new InvalidOperationException(
                "Test processing failure.");
        }
    }
}

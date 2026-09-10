using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
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

                Document? importedDocument =
                    await firstInstance.GetDocumentAsync(
                        documentId);

                Assert.NotNull(importedDocument);

                Assert.Equal(
                    DocumentStatus.Imported,
                    importedDocument.Status);

                Assert.Equal(
                    0L,
                    importedDocument.ProcessingGeneration);

                Assert.Equal(
                    0L,
                    importedDocument.LastSuccessfulProcessingGeneration);

                await firstInstance.ProcessingService.ProcessAsync(
                    documentId);

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

                Assert.Equal(
                    1L,
                    document.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    document.LastSuccessfulProcessingGeneration);

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

                Assert.Equal(
                    1L,
                    restoredDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    restoredDocument.LastSuccessfulProcessingGeneration);

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
    public async Task ImportDocument_WhenProcessingIsCancelledWithoutPreviousSuccessfulProcessing_RecoversToImported()
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
                "DeskVault processing cancellation integration test.",
                Encoding.UTF8);

            var cancellingExtractor =
                new CancellingDocumentTextExtractor();

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [cancellingExtractor]);

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Processing Cancellation Test Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            using var cancellationTokenSource =
                new CancellationTokenSource();

            Task processingTask =
                harness.ProcessingService.ProcessAsync(
                    documentId,
                    cancellationTokenSource.Token);

            await cancellingExtractor.WaitUntilExtractionStartedAsync();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => processingTask);

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(document);

            Assert.Equal(
                DocumentStatus.Imported,
                document.Status);

            Assert.Equal(
                1L,
                document.ProcessingGeneration);

            Assert.Equal(
                0L,
                document.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.Empty(chunks);

            Assert.True(
                File.Exists(
                    document.StoredFilePath));

            Assert.True(
                cancellingExtractor.WasCalled);
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
    public async Task ImportDocument_WhenReprocessingIsCancelledAfterPreviousSuccess_PreservesSuccessfulResultAndRecoversToAvailable()
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
                    "reprocessing-cancellation-test.txt");

            string sourceText =
                """
                DeskVault successful processing result must remain available.

                This content represents the previously successful indexed result.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;
            string storedFilePath;

            await using (
                var initialHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await initialHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Reprocessing Cancellation Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                await initialHarness.ProcessingService.ProcessAsync(
                    documentId);

                Document? successfulDocument =
                    await initialHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(successfulDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    successfulDocument.Status);

                Assert.Equal(
                    1L,
                    successfulDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    successfulDocument.LastSuccessfulProcessingGeneration);

                storedFilePath =
                    successfulDocument.StoredFilePath;

                Assert.True(
                    File.Exists(
                        storedFilePath));

                List<DocumentChunkEntity> successfulChunks =
                    await initialHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(successfulChunks);

                string successfulIndexedText =
                    string.Join(
                        "\n",
                        successfulChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));

                Assert.Contains(
                    "previously successful indexed result",
                    successfulIndexedText,
                    StringComparison.OrdinalIgnoreCase);
            }

            var cancellingExtractor =
                new CancellingDocumentTextExtractor();

            await using (
                var reprocessingHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        [cancellingExtractor]))
            {
                using var cancellationTokenSource =
                    new CancellationTokenSource();

                Task processingTask =
                    reprocessingHarness.ProcessingService.ProcessAsync(
                        documentId,
                        cancellationTokenSource.Token);

                await cancellingExtractor.WaitUntilExtractionStartedAsync();

                cancellationTokenSource.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => processingTask);

                Document? recoveredDocument =
                    await reprocessingHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(recoveredDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    recoveredDocument.Status);

                Assert.Equal(
                    2L,
                    recoveredDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    recoveredDocument.LastSuccessfulProcessingGeneration);

                Assert.Equal(
                    storedFilePath,
                    recoveredDocument.StoredFilePath);

                Assert.True(
                    File.Exists(
                        recoveredDocument.StoredFilePath));

                List<DocumentChunkEntity> recoveredChunks =
                    await reprocessingHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(recoveredChunks);

                string recoveredIndexedText =
                    string.Join(
                        "\n",
                        recoveredChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));

                Assert.Contains(
                    "previously successful indexed result",
                    recoveredIndexedText,
                    StringComparison.OrdinalIgnoreCase);

                Assert.True(
                    cancellingExtractor.WasCalled);
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
    public async Task ImportDocument_WhenReprocessingFailsAfterPreviousSuccess_PreservesLastSuccessfulGenerationAndChunks()
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
                    "reprocessing-failure-test.txt");

            string sourceText =
                """
                DeskVault previous successful processing result.

                This content must remain available after a later processing failure.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;
            string storedFilePath;
            string successfulIndexedText;

            await using (
                var initialHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await initialHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Reprocessing Failure Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                await initialHarness.ProcessingService.ProcessAsync(
                    documentId);

                Document? successfulDocument =
                    await initialHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(successfulDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    successfulDocument.Status);

                Assert.Equal(
                    1L,
                    successfulDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    successfulDocument.LastSuccessfulProcessingGeneration);

                storedFilePath =
                    successfulDocument.StoredFilePath;

                Assert.True(
                    File.Exists(
                        storedFilePath));

                List<DocumentChunkEntity> successfulChunks =
                    await initialHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(successfulChunks);

                successfulIndexedText =
                    string.Join(
                        "\n",
                        successfulChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));
            }

            var failingExtractor =
                new FailingDocumentTextExtractor();

            await using (
                var reprocessingHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        [failingExtractor]))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        reprocessingHarness.ProcessingService.ProcessAsync(
                            documentId));

                Document? failedDocument =
                    await reprocessingHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(failedDocument);

                Assert.Equal(
                    DocumentStatus.Failed,
                    failedDocument.Status);

                Assert.Equal(
                    2L,
                    failedDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    failedDocument.LastSuccessfulProcessingGeneration);

                Assert.Equal(
                    storedFilePath,
                    failedDocument.StoredFilePath);

                Assert.True(
                    File.Exists(
                        failedDocument.StoredFilePath));

                List<DocumentChunkEntity> preservedChunks =
                    await reprocessingHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(preservedChunks);

                string preservedIndexedText =
                    string.Join(
                        "\n",
                        preservedChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));

                Assert.Equal(
                    successfulIndexedText,
                    preservedIndexedText);

                Assert.True(
                    failingExtractor.WasCalled);
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

                Document? importedDocument =
                    await instance.GetDocumentAsync(
                        documentId);

                Assert.NotNull(importedDocument);

                Assert.Equal(
                    DocumentStatus.Imported,
                    importedDocument.Status);

                await instance.ProcessingService.ProcessAsync(
                    documentId);

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

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Processing Failure Test Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            Document? importedDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(importedDocument);

            Assert.Equal(
                DocumentStatus.Imported,
                importedDocument.Status);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    harness.ProcessingService.ProcessAsync(
                        documentId));

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

            Guid documentId =
                importResult.DocumentId.Value;

            Document? importedDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(importedDocument);

            Assert.Equal(
                DocumentStatus.Imported,
                importedDocument.Status);

            await harness.ProcessingService.ProcessAsync(
                documentId);

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

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

            Guid documentId =
                importResult.DocumentId.Value;

            Document? importedDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(importedDocument);

            Assert.Equal(
                DocumentStatus.Imported,
                importedDocument.Status);

            await harness.ProcessingService.ProcessAsync(
                documentId);

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

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

    [Fact]
    public async Task ImportDocument_WhenFormattedJsonIsImported_PreservesOriginalSourceBytes()
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
                    "source-preservation-test.json");

            string sourceText =
                """
                {
                    "name": "DeskVault",
                    "version": "1.0",
                    "enabled": true,
                    "workspace": {
                        "type": "knowledge",
                        "features": [
                            "search",
                            "processing",
                            "rendering"
                        ]
                    }
                }
                """;

            byte[] originalBytes =
                Encoding.UTF8.GetBytes(
                    sourceText);

            await File.WriteAllBytesAsync(
                sourceFilePath,
                originalBytes);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Source Preservation JSON"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Document? document =
                await harness.GetDocumentAsync(
                    importResult.DocumentId.Value);

            Assert.NotNull(document);

            Assert.True(
                File.Exists(
                    document.StoredFilePath));

            var encryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(
                        encryptionKey),
                    NullLogger<DocumentEncryptionService>.Instance);

            var reader =
                new EncryptedDocumentReader(
                    encryptionService,
                    NullLogger<EncryptedDocumentReader>.Instance);

            await using Stream decryptedStream =
                await reader.OpenReadAsync(
                    document.StoredFilePath);

            using var decryptedContent =
                new MemoryStream();

            await decryptedStream.CopyToAsync(
                decryptedContent);

            Assert.Equal(
                originalBytes,
                decryptedContent.ToArray());
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

    private sealed class CancellingDocumentTextExtractor
        : IDocumentTextExtractor
    {
        private readonly TaskCompletionSource<bool> _extractionStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WasCalled { get; private set; }

        public bool CanExtract(
            string fileName)
        {
            return fileName.EndsWith(
                ".txt",
                StringComparison.OrdinalIgnoreCase);
        }

        public async Task<DocumentTextExtractionResult> ExtractAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            _extractionStarted.TrySetResult(true);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            throw new InvalidOperationException(
                "Cancellation was not observed.");
        }

        public Task WaitUntilExtractionStartedAsync()
        {
            return _extractionStarted.Task;
        }
    }

    private sealed class TestEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestEncryptionKeyService(
            byte[] key)
        {
            _key = key;
        }

        public Task<byte[]> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _key);
        }
    }
}

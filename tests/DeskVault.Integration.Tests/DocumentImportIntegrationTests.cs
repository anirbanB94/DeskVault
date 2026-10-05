using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
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

            DocumentProcessingRuleVersion expectedProcessingRuleVersion =
                DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                    new TextDocumentTextExtractor().RuleVersion,
                    new DocumentTextNormalizer().RuleVersion);

            DocumentChunkingRuleVersion expectedChunkingRuleVersion =
                new DocumentTextChunker(
                    maxChunkSize: 4000)
                    .RuleVersion;

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

                Assert.Equal(
                    expectedProcessingRuleVersion.Value,
                    document.LastSuccessfulProcessingRuleVersion);

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

                Assert.All(
                    chunks,
                    chunk =>
                    {
                        Assert.Equal(
                            1L,
                            chunk.ProcessingGeneration);

                        Assert.Equal(
                            expectedChunkingRuleVersion.Value,
                            chunk.ChunkingRuleVersion);
                    });

                SearchDocumentsPage searchPage =
                    await firstInstance.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "ENTERPRISE ARCHITECTURE"));

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    searchPage.Results;

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
                    matchingResult.Matches,
                    match =>
                        match.Source == SearchMatchSource.ProcessedContent &&
                        match.Context.Contains(
                            "enterprise architecture",
                            StringComparison.OrdinalIgnoreCase));

                await firstInstance.ProcessingService.ProcessAsync(
                    documentId);

                Document? afterSecondProcessing =
                    await firstInstance.GetDocumentAsync(
                        documentId);

                Assert.NotNull(afterSecondProcessing);

                Assert.Equal(
                    DocumentStatus.Available,
                    afterSecondProcessing.Status);

                Assert.Equal(
                    2L,
                    afterSecondProcessing.ProcessingGeneration);

                Assert.Equal(
                    2L,
                    afterSecondProcessing.LastSuccessfulProcessingGeneration);

                Assert.Equal(
                    expectedProcessingRuleVersion.Value,
                    afterSecondProcessing.LastSuccessfulProcessingRuleVersion);

                List<DocumentChunkEntity> afterSecondProcessingChunks =
                    await firstInstance.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    afterSecondProcessingChunks);

                Assert.All(
                    afterSecondProcessingChunks,
                    chunk =>
                    {
                        Assert.Equal(
                            2L,
                            chunk.ProcessingGeneration);

                        Assert.Equal(
                            expectedChunkingRuleVersion.Value,
                            chunk.ChunkingRuleVersion);
                    });
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
                    2L,
                    restoredDocument.ProcessingGeneration);

                Assert.Equal(
                    2L,
                    restoredDocument.LastSuccessfulProcessingGeneration);

                Assert.Equal(
                    expectedProcessingRuleVersion.Value,
                    restoredDocument.LastSuccessfulProcessingRuleVersion);

                Assert.True(
                    File.Exists(
                        restoredDocument.StoredFilePath));

                List<DocumentChunkEntity> restoredChunks =
                    await secondInstance.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    restoredChunks);

                Assert.All(
                    restoredChunks,
                    chunk =>
                    {
                        Assert.Equal(
                            2L,
                            chunk.ProcessingGeneration);

                        Assert.Equal(
                            expectedChunkingRuleVersion.Value,
                            chunk.ChunkingRuleVersion);
                    });

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

                SearchDocumentsPage searchPage =
                    await secondInstance.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "ENTERPRISE ARCHITECTURE"));

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    searchPage.Results;

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
                    matchingResult.Matches,
                    match =>
                        match.Source == SearchMatchSource.ProcessedContent &&
                        match.Context.Contains(
                            "enterprise architecture",
                            StringComparison.OrdinalIgnoreCase));
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
    public async Task ImportDocument_WhenProcessingExceedsResourceLimit_MarksFailedAndCanBeRetried()
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
                    "resource-limit-test.txt");

            string sourceText =
                """
            DeskVault resource limit integration test.

            This document must exceed the configured processing boundary.
            """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;

            await using (
                var limitedHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        processingOptions:
                            new DocumentProcessingOptions
                            {
                                MaxDecryptedDocumentBytes = 16
                            }))
            {
                ImportDocumentResult importResult =
                    await limitedHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Resource Limit Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                ResourceLimitExceededException exception =
                    await Assert.ThrowsAsync<
                        ResourceLimitExceededException>(
                        () =>
                            limitedHarness.ProcessingService.ProcessAsync(
                                documentId));

                Assert.Equal(
                    16L,
                    exception.LimitBytes);

                Document? failedDocument =
                    await limitedHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    failedDocument);

                Assert.Equal(
                    DocumentStatus.Failed,
                    failedDocument.Status);

                Assert.Equal(
                    1L,
                    failedDocument.ProcessingGeneration);

                Assert.Equal(
                    0L,
                    failedDocument.LastSuccessfulProcessingGeneration);

                Assert.True(
                    File.Exists(
                        failedDocument.StoredFilePath));

                List<DocumentChunkEntity> chunks =
                    await limitedHarness.GetChunksAsync(
                        documentId);

                Assert.Empty(chunks);
            }

            await using (
                var retryHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                await retryHarness.ProcessingService.ProcessAsync(
                    documentId);

                Document? retriedDocument =
                    await retryHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    retriedDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    retriedDocument.Status);

                Assert.Equal(
                    2L,
                    retriedDocument.ProcessingGeneration);

                Assert.Equal(
                    2L,
                    retriedDocument.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> chunks =
                    await retryHarness.GetChunksAsync(
                        documentId);

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
                    "resource limit integration test",
                    indexedText,
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
    public async Task ImportDocument_WhenProcessingIsWithinResourceLimit_CompletesSuccessfully()
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
                    "resource-limit-success-test.txt");

            string sourceText =
                """
            DeskVault resource boundary success test.

            This document must remain fully processable at the configured boundary.
            """;

            byte[] sourceBytes =
                Encoding.UTF8.GetBytes(
                    sourceText);

            await File.WriteAllBytesAsync(
                sourceFilePath,
                sourceBytes);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    processingOptions:
                        new DocumentProcessingOptions
                        {
                            MaxDecryptedDocumentBytes =
                                sourceBytes.Length
                        });

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Resource Limit Success Test Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            await harness.ProcessingService.ProcessAsync(
                documentId);

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(document);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            Assert.Equal(
                1L,
                document.ProcessingGeneration);

            Assert.Equal(
                1L,
                document.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

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
                "resource boundary success test",
                indexedText,
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
    public async Task ImportDocument_WhenLargeDocumentExceedsResourceLimit_MarksFailedWithoutUnboundedProcessing()
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

        const int resourceLimitBytes = 1024 * 1024;
        const int documentSizeBytes = (1024 * 1024) + 1;

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "large-resource-limit-test.txt");

            byte[] sourceBytes =
                new byte[documentSizeBytes];

            Array.Fill(
                sourceBytes,
                (byte)'A');

            await File.WriteAllBytesAsync(
                sourceFilePath,
                sourceBytes);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    processingOptions:
                        new DocumentProcessingOptions
                        {
                            MaxDecryptedDocumentBytes =
                                resourceLimitBytes
                        });

            ImportDocumentResult importResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Large Resource Limit Test Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            ResourceLimitExceededException exception =
                await Assert.ThrowsAsync<
                    ResourceLimitExceededException>(
                    () =>
                        harness.ProcessingService.ProcessAsync(
                            documentId));

            Assert.Equal(
                resourceLimitBytes,
                exception.LimitBytes);

            Assert.Equal(
                documentSizeBytes,
                exception.AttemptedBytes);

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(document);

            Assert.Equal(
                DocumentStatus.Failed,
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

            await using var reprocessingHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        [cancellingExtractor]);

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

            await using var reprocessingHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        [failingExtractor]);

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
    public async Task ImportDocument_WhenMetadataPersistenceFailsAfterStorageSucceeds_RemovesStoredArtifact()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        string? storedFilePath = null;

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "failed-import-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault failed import cleanup integration test.",
                Encoding.UTF8);

            var persistenceException =
                new InvalidOperationException(
                    "Metadata persistence failed.");

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importRepositoryDecorator:
                        repository =>
                            new ImportDocumentRepositoryDecorator(
                                repository,
                                (document, _) =>
                                {
                                    storedFilePath =
                                        document.StoredFilePath;

                                    throw persistenceException;
                                }));

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        harness.ImportHandler.HandleAsync(
                            new ImportDocumentCommand(
                                sourceFilePath,
                                "Failed Import Test Document")));

            Assert.Same(
                persistenceException,
                exception);

            Assert.NotNull(
                storedFilePath);

            Assert.False(
                File.Exists(
                    storedFilePath));

            IReadOnlyList<Document> documents =
                await harness.GetDocumentsAsync();

            Assert.Empty(
                documents);

            if (Directory.Exists(
                harness.DataPaths.DocumentsDirectory))
            {
                Assert.Empty(
                    Directory.GetFiles(
                        harness.DataPaths.DocumentsDirectory,
                        "*.dvault"));
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
    public async Task ImportDocument_WhenCancelledAfterStorageSucceeds_RemovesStoredArtifact()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        string? storedFilePath = null;

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "cancelled-import-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault cancelled import cleanup integration test.",
                Encoding.UTF8);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importRepositoryDecorator:
                        repository =>
                            new ImportDocumentRepositoryDecorator(
                                repository,
                                (document, _) =>
                                {
                                    storedFilePath =
                                        document.StoredFilePath;

                                    cancellationTokenSource.Cancel();

                                    throw new OperationCanceledException(
                                        cancellationTokenSource.Token);
                                }));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    harness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Cancelled Import Test Document"),
                        cancellationTokenSource.Token));

            Assert.NotNull(
                storedFilePath);

            Assert.False(
                File.Exists(
                    storedFilePath));

            IReadOnlyList<Document> documents =
                await harness.GetDocumentsAsync();

            Assert.Empty(
                documents);

            if (Directory.Exists(
                harness.DataPaths.DocumentsDirectory))
            {
                Assert.Empty(
                    Directory.GetFiles(
                        harness.DataPaths.DocumentsDirectory,
                        "*.dvault"));
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
    public async Task ImportDocument_WhenSameDocumentIsProcessedConcurrently_PreventsStaleAttemptFromOverwritingNewerResult()
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
                    "concurrent-processing-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "Original source content.",
                Encoding.UTF8);

            Guid documentId;

            await using (
                var setupHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await setupHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Concurrent Processing Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;
            }

            var firstExtractor =
                new BlockingDocumentTextExtractor(
                    "Stale generation one content.");

            var secondExtractor =
                new ImmediateDocumentTextExtractor(
                    "Authoritative generation two content.");

            await using var firstHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [firstExtractor]);

            await using var secondHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [secondExtractor]);

            Task firstProcessingTask =
                firstHarness.ProcessingService.ProcessAsync(
                    documentId);

            await firstExtractor.WaitUntilExtractionStartedAsync();

            Task secondProcessingTask =
                secondHarness.ProcessingService.ProcessAsync(
                    documentId);

            await secondProcessingTask;

            Document? afterSecondProcessing =
                await secondHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(afterSecondProcessing);

            Assert.Equal(
                DocumentStatus.Available,
                afterSecondProcessing.Status);

            Assert.Equal(
                2L,
                afterSecondProcessing.ProcessingGeneration);

            Assert.Equal(
                2L,
                afterSecondProcessing.LastSuccessfulProcessingGeneration);

            firstExtractor.ReleaseExtraction();

            StaleProcessingGenerationException staleException =
                await Assert.ThrowsAsync<StaleProcessingGenerationException>(
                    () => firstProcessingTask);

            Assert.Equal(
                documentId,
                staleException.DocumentId);

            Assert.Equal(
                1L,
                staleException.ProcessingGeneration);

            Assert.Equal(
                2L,
                staleException.CurrentGeneration);

            Document? finalDocument =
                await secondHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(finalDocument);

            Assert.Equal(
                DocumentStatus.Available,
                finalDocument.Status);

            Assert.Equal(
                2L,
                finalDocument.ProcessingGeneration);

            Assert.Equal(
                2L,
                finalDocument.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> finalChunks =
                await secondHarness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(finalChunks);

            string finalIndexedText =
                string.Join(
                    "\n",
                    finalChunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "Authoritative generation two content.",
                finalIndexedText,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Stale generation one content.",
                finalIndexedText,
                StringComparison.Ordinal);

            Assert.Equal(
                1,
                finalChunks.Count(
                    chunk =>
                        chunk.Text.Contains(
                            "Authoritative generation two content.",
                            StringComparison.Ordinal)));

            Assert.Equal(
                0,
                finalChunks.Count(
                    chunk =>
                        chunk.Text.Contains(
                            "Stale generation one content.",
                            StringComparison.Ordinal)));

            Assert.True(
                firstExtractor.WasCalled);

            Assert.True(
                secondExtractor.WasCalled);
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
    public async Task ImportDocument_WhenOlderProcessingAttemptIsCancelledAfterNewerAttemptSucceeds_DoesNotRecoverNewerProcessingState()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rootDirectory);

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
                    "concurrent-cancellation-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "Original source content.",
                Encoding.UTF8);

            Guid documentId;

            await using (
                var setupHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await setupHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Concurrent Cancellation Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;
            }

            var firstExtractor =
                new BlockingDocumentTextExtractor(
                    "Stale generation one content.");

            var secondExtractor =
                new ImmediateDocumentTextExtractor(
                    "Authoritative generation two content.");

            await using var firstHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [firstExtractor]);

            await using var secondHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [secondExtractor]);

            using var firstCancellation =
                new CancellationTokenSource();

            Task firstProcessingTask =
                firstHarness.ProcessingService.ProcessAsync(
                    documentId,
                    firstCancellation.Token);

            await firstExtractor.WaitUntilExtractionStartedAsync();

            Task secondProcessingTask =
                secondHarness.ProcessingService.ProcessAsync(
                    documentId);

            await secondProcessingTask;

            Document? afterSecondProcessing =
                await secondHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(afterSecondProcessing);

            Assert.Equal(
                DocumentStatus.Available,
                afterSecondProcessing.Status);

            Assert.Equal(
                2L,
                afterSecondProcessing.ProcessingGeneration);

            Assert.Equal(
                2L,
                afterSecondProcessing.LastSuccessfulProcessingGeneration);

            firstCancellation.Cancel();

            StaleProcessingGenerationException staleException =
                await Assert.ThrowsAsync<StaleProcessingGenerationException>(
                    () => firstProcessingTask);

            Assert.Equal(
                documentId,
                staleException.DocumentId);

            Assert.Equal(
                1L,
                staleException.ProcessingGeneration);

            Assert.Equal(
                2L,
                staleException.CurrentGeneration);

            Document? finalDocument =
                await secondHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(finalDocument);

            Assert.Equal(
                DocumentStatus.Available,
                finalDocument.Status);

            Assert.Equal(
                2L,
                finalDocument.ProcessingGeneration);

            Assert.Equal(
                2L,
                finalDocument.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> finalChunks =
                await secondHarness.GetChunksAsync(
                    documentId);

            string finalIndexedText =
                string.Join(
                    "\n",
                    finalChunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "Authoritative generation two content.",
                finalIndexedText,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Stale generation one content.",
                finalIndexedText,
                StringComparison.Ordinal);
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
    public async Task ImportDocument_WhenOlderProcessingAttemptFailsAfterNewerAttemptSucceeds_DoesNotOverwriteNewerProcessingState()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rootDirectory);

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
                    "concurrent-failure-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "Original source content.",
                Encoding.UTF8);

            Guid documentId;

            await using (
                var setupHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey))
            {
                ImportDocumentResult importResult =
                    await setupHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Concurrent Failure Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;
            }

            var firstExtractor =
                new BlockingFailingDocumentTextExtractor(
                    "Stale generation one processing failure.");

            var secondExtractor =
                new ImmediateDocumentTextExtractor(
                    "Authoritative generation two content.");

            await using var firstHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [firstExtractor]);

            await using var secondHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    [secondExtractor]);

            Task firstProcessingTask =
                firstHarness.ProcessingService.ProcessAsync(
                    documentId);

            await firstExtractor.WaitUntilExtractionStartedAsync();

            Task secondProcessingTask =
                secondHarness.ProcessingService.ProcessAsync(
                    documentId);

            await secondProcessingTask;

            Document? afterSecondProcessing =
                await secondHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(afterSecondProcessing);

            Assert.Equal(
                DocumentStatus.Available,
                afterSecondProcessing.Status);

            Assert.Equal(
                2L,
                afterSecondProcessing.ProcessingGeneration);

            Assert.Equal(
                2L,
                afterSecondProcessing.LastSuccessfulProcessingGeneration);

            firstExtractor.ReleaseFailure();

            StaleProcessingGenerationException staleException =
                await Assert.ThrowsAsync<StaleProcessingGenerationException>(
                    () => firstProcessingTask);

            Assert.Equal(
                documentId,
                staleException.DocumentId);

            Assert.Equal(
                1L,
                staleException.ProcessingGeneration);

            Assert.Equal(
                2L,
                staleException.CurrentGeneration);

            Document? finalDocument =
                await secondHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(finalDocument);

            Assert.Equal(
                DocumentStatus.Available,
                finalDocument.Status);

            Assert.Equal(
                2L,
                finalDocument.ProcessingGeneration);

            Assert.Equal(
                2L,
                finalDocument.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> finalChunks =
                await secondHarness.GetChunksAsync(
                    documentId);

            string finalIndexedText =
                string.Join(
                    "\n",
                    finalChunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "Authoritative generation two content.",
                finalIndexedText,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Stale generation one processing failure.",
                finalIndexedText,
                StringComparison.Ordinal);
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
    public async Task ImportDocument_WhenSameDocumentIsImportedConcurrently_ReturnsOneSuccessAndOneDuplicate()
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

        var storageBarrier =
            new ConcurrentImportStorageBarrier();

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "concurrent-duplicate-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault concurrent duplicate import test.",
                Encoding.UTF8);

            await using var firstHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importStorageDecorator:
                        storage =>
                            new BarrierStorageService(
                                storage,
                                storageBarrier));

            await using var secondHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importStorageDecorator:
                        storage =>
                            new BarrierStorageService(
                                storage,
                                storageBarrier));

            Task<ImportDocumentResult> firstImportTask =
                firstHarness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "First Concurrent Document"));

            Task<ImportDocumentResult> secondImportTask =
                secondHarness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Second Concurrent Document"));

            await storageBarrier.WaitUntilBothStoresCompleteAsync();

            storageBarrier.Release();

            ImportDocumentResult[] results =
                await Task.WhenAll(
                    firstImportTask,
                    secondImportTask);

            Assert.Equal(
                1,
                results.Count(
                    result =>
                        result.Status ==
                        ImportDocumentResultStatus.Success));

            Assert.Equal(
                1,
                results.Count(
                    result =>
                        result.Status ==
                        ImportDocumentResultStatus.Duplicate));

            ImportDocumentResult successResult =
                Assert.Single(
                    results,
                    result =>
                        result.Status ==
                        ImportDocumentResultStatus.Success);

            ImportDocumentResult duplicateResult =
                Assert.Single(
                    results,
                    result =>
                        result.Status ==
                        ImportDocumentResultStatus.Duplicate);

            Assert.NotNull(
                successResult.DocumentId);

            Assert.Null(
                duplicateResult.DocumentId);

            Assert.Equal(
                "The document has already been imported.",
                duplicateResult.Description);

            IReadOnlyList<Document> documents =
                await firstHarness.GetDocumentsAsync();

            Assert.Single(
                documents);

            Assert.Equal(
                successResult.DocumentId,
                documents[0].Id);

            string expectedDisplayName =
                results[0].Status ==
                ImportDocumentResultStatus.Success
                    ? "First Concurrent Document"
                    : "Second Concurrent Document";

            Assert.Equal(
                expectedDisplayName,
                documents[0].DisplayName);

            Assert.Equal(
                Convert.ToHexString(
                    SHA256.HashData(
                        await File.ReadAllBytesAsync(
                            sourceFilePath)))
                    .ToLowerInvariant(),
                documents[0].Sha256Hash);

            Assert.True(
                File.Exists(
                    documents[0].StoredFilePath));

            string[] storedArtifacts =
                Directory.GetFiles(
                    firstHarness.DataPaths.DocumentsDirectory,
                    "*.dvault");

            Assert.Single(
                storedArtifacts);

            Assert.Equal(
                documents[0].StoredFilePath,
                storedArtifacts[0]);
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
    public async Task ImportDocument_WhenDifferentDocumentsAreImportedConcurrently_CompletesBothImports()
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
            string firstSourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "concurrent-first.txt");

            string secondSourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "concurrent-second.txt");

            await File.WriteAllTextAsync(
                firstSourceFilePath,
                "First concurrent document content.",
                Encoding.UTF8);

            await File.WriteAllTextAsync(
                secondSourceFilePath,
                "Second concurrent document content.",
                Encoding.UTF8);

            await using var firstHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            await using var secondHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            Task<ImportDocumentResult> firstImportTask =
                firstHarness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        firstSourceFilePath,
                        "First Concurrent Document"));

            Task<ImportDocumentResult> secondImportTask =
                secondHarness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        secondSourceFilePath,
                        "Second Concurrent Document"));

            ImportDocumentResult[] results =
                await Task.WhenAll(
                    firstImportTask,
                    secondImportTask);

            Assert.All(
                results,
                result =>
                    Assert.Equal(
                        ImportDocumentResultStatus.Success,
                        result.Status));

            Assert.All(
                results,
                result =>
                    Assert.NotNull(
                        result.DocumentId));

            Assert.NotEqual(
                results[0].DocumentId,
                results[1].DocumentId);

            IReadOnlyList<Document> documents =
                await firstHarness.GetDocumentsAsync();

            Assert.Equal(
                2,
                documents.Count);

            Assert.Equal(
                2,
                documents.Select(
                        document =>
                            document.Sha256Hash)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());

            Assert.Contains(
                documents,
                document =>
                    document.DisplayName ==
                    "First Concurrent Document");

            Assert.Contains(
                documents,
                document =>
                    document.DisplayName ==
                    "Second Concurrent Document");
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

            await using var instance =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey);

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
    public async Task ImportDocument_WhenProcessingFails_CanBeProcessedAgainSuccessfully()
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
                    "processing-retry-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault processing retry integration test.",
                Encoding.UTF8);

            Guid documentId;

            var failingExtractor =
                new FailingDocumentTextExtractor();

            await using (
                var failingHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        [failingExtractor]))
            {
                ImportDocumentResult importResult =
                    await failingHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Processing Retry Test Document"));

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        failingHarness.ProcessingService.ProcessAsync(
                            documentId));

                Document? failedDocument =
                    await failingHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(failedDocument);

                Assert.Equal(
                    DocumentStatus.Failed,
                    failedDocument.Status);

                Assert.Equal(
                    1L,
                    failedDocument.ProcessingGeneration);

                Assert.Equal(
                    0L,
                    failedDocument.LastSuccessfulProcessingGeneration);

                Assert.Empty(
                    await failingHarness.GetChunksAsync(
                        documentId));
            }

            await using var retryHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            await retryHarness.ProcessingService.ProcessAsync(
                documentId);

            Document? retriedDocument =
                await retryHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(retriedDocument);

            Assert.Equal(
                DocumentStatus.Available,
                retriedDocument.Status);

            Assert.Equal(
                2L,
                retriedDocument.ProcessingGeneration);

            Assert.Equal(
                2L,
                retriedDocument.LastSuccessfulProcessingGeneration);

            Assert.NotEmpty(
                await retryHarness.GetChunksAsync(
                    documentId));
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
    public async Task ImportDocument_WhenCandidatePublicationFails_PreservesLastSuccessfulResult()
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
                    "candidate-failure-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                """
            Previous successful processing result.

            This result must remain searchable after candidate publication fails.
            """,
                Encoding.UTF8);

            Guid documentId;
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
                            "Candidate Failure Test Document"));

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

            var failingChunker =
                new FixedDocumentTextChunker(
                [
                    new DocumentChunk(
                    0,
                    "Candidate chunk one."),

                new DocumentChunk(
                    0,
                    "Candidate chunk with duplicate order.")
                ]);

            await using var failingHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    chunker: failingChunker);

            await Assert.ThrowsAnyAsync<Exception>(
                () =>
                    failingHarness.ProcessingService.ProcessAsync(
                        documentId));

            Assert.True(
                failingChunker.WasCalled);

            Assert.True(
                failingChunker.CandidateOutputProduced);

            Document? failedDocument =
                await failingHarness.GetDocumentAsync(
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

            List<DocumentChunkEntity> preservedChunks =
                await failingHarness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                preservedChunks);

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
    public async Task ImportDocument_WhenCandidatePublicationIsCancelled_PreservesLastSuccessfulResult()
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
                    "candidate-cancellation-test.txt");

            string sourceText =
                """
            Previous successful processing result.

            This result must remain available when candidate publication is cancelled.
            """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;
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
                            "Candidate Cancellation Test Document"));

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

            using var cancellationTokenSource =
                new CancellationTokenSource();

            var cancellingChunker =
                new CancellingCandidateDocumentTextChunker(
                    [
                        new DocumentChunk(
                            0,
                            "Cancelled candidate chunk one."),

                        new DocumentChunk(
                            1,
                            "Cancelled candidate chunk two.")
                    ],
                    cancellationTokenSource);

            await using var reprocessingHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    chunker: cancellingChunker);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    reprocessingHarness.ProcessingService.ProcessAsync(
                        documentId,
                        cancellationTokenSource.Token));

            Assert.True(
                cancellingChunker.WasCalled);

            Assert.True(
                cancellingChunker.CandidateOutputProduced);

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

            List<DocumentChunkEntity> preservedChunks =
                await reprocessingHarness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                preservedChunks);

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

            Assert.DoesNotContain(
                "Cancelled candidate chunk one.",
                preservedIndexedText,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Cancelled candidate chunk two.",
                preservedIndexedText,
                StringComparison.Ordinal);
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
    public async Task ImportDocument_WhenCandidatePublicationFails_CanRetryAndReplacePreviousSuccessfulResult()
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
                    "candidate-retry-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                """
            Previous successful processing result.

            This result must be replaced by the successful retry.
            """,
                Encoding.UTF8);

            Guid documentId;

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
                            "Candidate Retry Test Document"));

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
            }

            var failingChunker =
                new FixedDocumentTextChunker(
                    [
                        new DocumentChunk(
                            0,
                            "Failed candidate chunk."),

                        new DocumentChunk(
                            0,
                            "Failed candidate duplicate order.")
                    ]);

            await using (
                var failingHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        chunker: failingChunker))
            {
                await Assert.ThrowsAnyAsync<Exception>(
                    () =>
                        failingHarness.ProcessingService.ProcessAsync(
                            documentId));

                Document? failedDocument =
                    await failingHarness.GetDocumentAsync(
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

                Assert.True(
                    failingChunker.WasCalled);

                Assert.True(
                    failingChunker.CandidateOutputProduced);
            }

            var successfulRetryChunker =
                new FixedDocumentTextChunker(
                    [
                        new DocumentChunk(
                            0,
                            "NEW SUCCESSFUL RESULT."),

                        new DocumentChunk(
                            1,
                            "SECOND NEW SUCCESSFUL CHUNK.")
                    ]);

            await using var retryHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    chunker: successfulRetryChunker);

            await retryHarness.ProcessingService.ProcessAsync(
                documentId);

            Document? retriedDocument =
                await retryHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(retriedDocument);

            Assert.Equal(
                DocumentStatus.Available,
                retriedDocument.Status);

            Assert.Equal(
                3L,
                retriedDocument.ProcessingGeneration);

            Assert.Equal(
                3L,
                retriedDocument.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> retryChunks =
                await retryHarness.GetChunksAsync(
                    documentId);

            Assert.Equal(
                2,
                retryChunks.Count);

            Assert.All(
                retryChunks,
                chunk =>
                    Assert.Equal(
                        3L,
                        chunk.ProcessingGeneration));

            string retriedIndexedText =
                string.Join(
                    "\n",
                    retryChunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "NEW SUCCESSFUL RESULT.",
                retriedIndexedText,
                StringComparison.Ordinal);

            Assert.Contains(
                "SECOND NEW SUCCESSFUL CHUNK.",
                retriedIndexedText,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Previous successful processing result.",
                retriedIndexedText,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                "Failed candidate chunk.",
                retriedIndexedText,
                StringComparison.Ordinal);
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

            SearchDocumentsPage searchPage =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "Alice Johnson"));

            IReadOnlyList<SearchDocumentsResult> searchResults =
                searchPage.Results;

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
                matchingResult.Matches,
                match =>
                    match.Source == SearchMatchSource.ProcessedContent &&
                    match.Context.Contains(
                        "Alice Johnson",
                        StringComparison.OrdinalIgnoreCase));
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

            SearchDocumentsPage searchPage =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "markdown architecture"));

            IReadOnlyList<SearchDocumentsResult> searchResults =
                searchPage.Results;

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
                matchingResult.Matches,
                match =>
                    match.Source == SearchMatchSource.ProcessedContent &&
                    match.Context.Contains(
                        "markdown architecture",
                        StringComparison.OrdinalIgnoreCase));
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

            var artifactPathResolver =
                new DocumentArtifactPathResolver(
                    new DeskVaultDataPaths(
                        rootDirectory));

            var reader =
                new EncryptedDocumentReader(
                    encryptionService,
                    artifactPathResolver,
                    NullLogger<EncryptedDocumentReader>.Instance);

            await using Stream decryptedStream =
                await reader.OpenReadAsync(
                    document.Id);

            using var decryptedContent =
                new MemoryStream();

            await decryptedStream.CopyToAsync(
                decryptedContent);

            byte[] decryptedBytes =
                decryptedContent.ToArray();

            string storedContentHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        decryptedBytes))
                .ToLowerInvariant();

            Assert.Equal(
                originalBytes,
                decryptedBytes);

            Assert.Equal(
                storedContentHash,
                document.Sha256Hash);
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
    public async Task ImportDocument_WhenSourceChangesAfterHashing_RejectsImportAndRemovesArtifact()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rootDirectory);

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
                    "mutation-test.txt");

            byte[] originalBytes =
                "Original document content."u8.ToArray();

            byte[] mutatedBytes =
                "Mutated document content."u8.ToArray();

            await File.WriteAllBytesAsync(
                sourceFilePath,
                originalBytes);

            var hashService =
                new MutatingHashService(
                    mutatedBytes);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    hashService: hashService);

            await Assert.ThrowsAsync<DocumentImportContentMismatchException>(
                () =>
                    harness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Mutation Test Document")));

            IReadOnlyList<Document> documents =
                await harness.GetDocumentsAsync();

            Assert.Empty(
                documents);

            if (Directory.Exists(
                harness.DataPaths.DocumentsDirectory))
            {
                Assert.Empty(
                    Directory.GetFiles(
                        harness.DataPaths.DocumentsDirectory,
                        "*.dvault"));
            }
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

    private sealed class ImportDocumentRepositoryDecorator
        : IDocumentRepository
    {
        private readonly IDocumentRepository _inner;

        private readonly Action<Document, CancellationToken>?
            _addAsyncOverride;

        public ImportDocumentRepositoryDecorator(
            IDocumentRepository inner,
            Action<Document, CancellationToken>? addAsyncOverride = null)
        {
            _inner = inner;
            _addAsyncOverride = addAsyncOverride;
        }

        public Task<bool> ExistsByHashAsync(
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            return _inner.ExistsByHashAsync(
                sha256Hash,
                cancellationToken);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            if (_addAsyncOverride is not null)
            {
                _addAsyncOverride(
                    document,
                    cancellationToken);

                return Task.CompletedTask;
            }

            return _inner.AddAsync(
                document,
                cancellationToken);
        }

        public Task<Document?> GetByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetByIdAsync(
                documentId,
                cancellationToken);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return _inner.GetAllAsync(
                cancellationToken);
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return _inner.UpdateAsync(
                document,
                cancellationToken);
        }
    }

    private sealed class ConcurrentImportStorageBarrier
    {
        private readonly TaskCompletionSource<bool>
            _bothStoresComplete =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _release =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private int _completedStoreCount;

        public Task WaitUntilBothStoresCompleteAsync()
        {
            return _bothStoresComplete.Task;
        }

        public void StoreCompleted()
        {
            if (Interlocked.Increment(
                    ref _completedStoreCount) == 2)
            {
                _bothStoresComplete.TrySetResult(true);
            }
        }

        public Task WaitUntilReleasedAsync(
            CancellationToken cancellationToken)
        {
            return _release.Task.WaitAsync(
                cancellationToken);
        }

        public void Release()
        {
            _release.TrySetResult(true);
        }
    }

    private sealed class BarrierStorageService
        : IStorageService
    {
        private readonly IStorageService _inner;

        private readonly ConcurrentImportStorageBarrier _barrier;

        public BarrierStorageService(
            IStorageService inner,
            ConcurrentImportStorageBarrier barrier)
        {
            _inner = inner;
            _barrier = barrier;
        }

        public async Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default,
            string? expectedSha256Hash = null)
        {
            string storedFilePath =
                await _inner.StoreAsync(
                    sourceFilePath,
                    documentId,
                    cancellationToken,
                    expectedSha256Hash);

            _barrier.StoreCompleted();

            await _barrier.WaitUntilReleasedAsync(
                cancellationToken);

            return storedFilePath;
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public bool IsOwnedArtifactPath(
            Guid documentId,
            string storedFilePath)
        {
            return _inner.IsOwnedArtifactPath(
                documentId,
                storedFilePath);
        }
    }

    private sealed class FixedDocumentTextChunker
        : IDocumentTextChunker
    {
        private readonly IReadOnlyList<DocumentChunk> _chunks;

        public FixedDocumentTextChunker(
            IReadOnlyList<DocumentChunk> chunks)
        {
            _chunks = chunks;
        }

        public bool WasCalled { get; private set; }

        public bool CandidateOutputProduced { get; private set; }

        public DocumentChunkingRuleVersion RuleVersion => new("test-chunker-v1");

        public Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
            DocumentTextNormalizationResult normalizationResult,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;
            CandidateOutputProduced = true;

            return Task.FromResult(
                _chunks);
        }
    }

    private sealed class CancellingCandidateDocumentTextChunker
        : IDocumentTextChunker
    {
        private readonly IReadOnlyList<DocumentChunk> _chunks;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellingCandidateDocumentTextChunker(
            IReadOnlyList<DocumentChunk> chunks,
            CancellationTokenSource cancellationTokenSource)
        {
            _chunks = chunks;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public bool WasCalled { get; private set; }

        public bool CandidateOutputProduced { get; private set; }

        public DocumentChunkingRuleVersion RuleVersion => new("test-chunker-v1");

        public Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
            DocumentTextNormalizationResult normalizationResult,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;
            CandidateOutputProduced = true;

            return Task.FromResult<IReadOnlyList<DocumentChunk>>(
                new CancellingDocumentChunkList(
                    _chunks,
                    _cancellationTokenSource));
        }
    }

    private sealed class CancellingDocumentChunkList
        : IReadOnlyList<DocumentChunk>
    {
        private readonly IReadOnlyList<DocumentChunk> _chunks;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellingDocumentChunkList(
            IReadOnlyList<DocumentChunk> chunks,
            CancellationTokenSource cancellationTokenSource)
        {
            _chunks = chunks;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public int Count =>
            _chunks.Count;

        public DocumentChunk this[int index] =>
            _chunks[index];

        public IEnumerator<DocumentChunk> GetEnumerator()
        {
            for (int index = 0;
                 index < _chunks.Count;
                 index++)
            {
                yield return _chunks[index];

                if (index == 0)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
        }

        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class FailingDocumentTextExtractor
        : IDocumentTextExtractor
    {
        public bool WasCalled { get; private set; }

        public string RuleVersion => "test-extractor-v1";

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

        public string RuleVersion => "test-extractor-v1";

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

    private sealed class BlockingFailingDocumentTextExtractor
        : IDocumentTextExtractor
    {
        private readonly string _message;

        private readonly TaskCompletionSource<bool>
            _extractionStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _releaseFailure =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingFailingDocumentTextExtractor(
            string message)
        {
            _message = message;
        }

        public bool WasCalled { get; private set; }

        public string RuleVersion => "test-extractor-v1";

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
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;

            _extractionStarted.TrySetResult(true);

            await _releaseFailure.Task.WaitAsync(
                cancellationToken);

            throw new InvalidOperationException(
                _message);
        }

        public Task WaitUntilExtractionStartedAsync()
        {
            return _extractionStarted.Task;
        }

        public void ReleaseFailure()
        {
            _releaseFailure.TrySetResult(true);
        }
    }

    private sealed class BlockingDocumentTextExtractor
        : IDocumentTextExtractor
    {
        private readonly string _text;

        private readonly TaskCompletionSource<bool>
            _extractionStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _releaseExtraction =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDocumentTextExtractor(
            string text)
        {
            _text = text;
        }

        public bool WasCalled { get; private set; }

        public string RuleVersion => "test-extractor-v1";

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
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;

            _extractionStarted.TrySetResult(true);

            await _releaseExtraction.Task.WaitAsync(
                cancellationToken);

            return new DocumentTextExtractionResult(
                _text);
        }

        public Task WaitUntilExtractionStartedAsync()
        {
            return _extractionStarted.Task;
        }

        public void ReleaseExtraction()
        {
            _releaseExtraction.TrySetResult(true);
        }
    }

    private sealed class ImmediateDocumentTextExtractor
        : IDocumentTextExtractor
    {
        private readonly string _text;

        public ImmediateDocumentTextExtractor(
            string text)
        {
            _text = text;
        }

        public bool WasCalled { get; private set; }

        public string RuleVersion => "test-extractor-v1";

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

            return Task.FromResult(
                new DocumentTextExtractionResult(
                    _text));
        }
    }

    private sealed class MutatingHashService
    : IHashService
    {
        private readonly byte[] _mutatedBytes;

        public MutatingHashService(
            byte[] mutatedBytes)
        {
            _mutatedBytes =
                [.. mutatedBytes];
        }

        public async Task<string> ComputeSha256Async(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] originalBytes =
                await File.ReadAllBytesAsync(
                    filePath,
                    cancellationToken);

            string originalHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        originalBytes))
                .ToLowerInvariant();

            await File.WriteAllBytesAsync(
                filePath,
                _mutatedBytes,
                cancellationToken);

            return originalHash;
        }

        public async Task<string> ComputeSha256Async(
            Stream content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            cancellationToken.ThrowIfCancellationRequested();

            var hash =
                await SHA256.HashDataAsync(
                    content,
                    cancellationToken);

            return Convert.ToHexString(
                    hash)
                .ToLowerInvariant();
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

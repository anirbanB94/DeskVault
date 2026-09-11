using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Application.Documents.Extraction;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Integration.Tests;

public sealed class ExpandedDocumentFormatIntegrationTests
{
    [Theory]
    [InlineData(
        "integration-test.json",
        """
        {
          "name": "DeskVault",
          "purpose": "enterprise knowledge search"
        }
        """,
        "enterprise knowledge search")]
    [InlineData(
        "integration-test.xml",
        """
        <document>
          <name>DeskVault</name>
          <purpose>enterprise knowledge search</purpose>
        </document>
        """,
        "enterprise knowledge search")]
    [InlineData(
        "integration-test.yaml",
        """
        name: DeskVault
        purpose: enterprise knowledge search
        """,
        "enterprise knowledge search")]
    [InlineData(
        "integration-test.yml",
        """
        name: DeskVault
        purpose: enterprise knowledge search
        """,
        "enterprise knowledge search")]
    [InlineData(
        "integration-test.ini",
        """
        [document]
        name=DeskVault
        purpose=enterprise knowledge search
        """,
        "enterprise knowledge search")]
    [InlineData(
        "integration-test.config",
        """
        [document]
        name=DeskVault
        purpose=enterprise knowledge search
        """,
        "enterprise knowledge search")]
    [InlineData(
        "integration-test.log",
        """
        2026-09-11 10:00:00 INFO DeskVault started.
        2026-09-11 10:00:01 INFO Enterprise knowledge search initialized.
        """,
        "enterprise knowledge search")]
    public async Task ImportDocument_WhenExpandedTextFormatIsValid_CompletesProcessingAndMakesDocumentSearchable(
        string fileName,
        string sourceText,
        string searchableText)
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
                    fileName);

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
                        "Expanded Format Integration Document"));

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

            Assert.NotNull(
                importedDocument);

            Assert.Equal(
                DocumentStatus.Imported,
                importedDocument.Status);

            await harness.ProcessingService.ProcessAsync(
                documentId);

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(
                document);

            Assert.Equal(
                fileName,
                document.FileName);

            Assert.Equal(
                "Expanded Format Integration Document",
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

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            foreach (DocumentChunkEntity chunk in chunks)
            {
                Assert.Equal(
                    documentId,
                    chunk.DocumentId);

                Assert.Equal(
                    DocumentChunkIdentity.CreateLogicalId(
                        documentId,
                        chunk.Order),
                    chunk.Id);

                Assert.Equal(
                    DocumentChunkIdentity.ComputeContentHash(
                        chunk.Text),
                    chunk.ContentHash);

                Assert.Equal(
                    1L,
                    chunk.ProcessingGeneration);
            }

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                searchableText,
                indexedText,
                StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        searchableText));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == documentId);

            Assert.Equal(
                fileName,
                matchingResult.FileName);

            Assert.Equal(
                "Expanded Format Integration Document",
                matchingResult.DisplayName);

            Assert.Contains(
                searchableText,
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

    [Theory]
    [InlineData(
        "integration-test.C",
        """
        int deskvault_document(void)
        {
            return 1;
        }

        // enterprise knowledge search
        """)]
    [InlineData(
        "integration-test.CPP",
        """
        class DeskVaultDocument
        {
        public:
            int Search() { return 1; }
        };

        // enterprise knowledge search
        """)]
    [InlineData(
        "integration-test.H",
        """
        #ifndef DESKVAULT_DOCUMENT_H
        #define DESKVAULT_DOCUMENT_H

        int deskvault_document(void);

        // enterprise knowledge search

        #endif
        """)]
    [InlineData(
        "integration-test.HPP",
        """
        class DeskVaultDocument
        {
        public:
            int Search() { return 1; }
        };

        // enterprise knowledge search
        """)]
    [InlineData(
        "integration-test.CS",
        """
        public sealed class DeskVaultDocument
        {
            public string Search()
            {
                return "enterprise knowledge search";
            }
        }
        """)]
    [InlineData(
        "integration-test.JAVA",
        """
        public final class DeskVaultDocument {
            public String search() {
                return "enterprise knowledge search";
            }
        }
        """)]
    [InlineData(
        "integration-test.PY",
        """
        def deskvault_document():
            return "enterprise knowledge search"
        """)]
    [InlineData(
        "integration-test.JS",
        """
        function deskvaultDocument() {
            return "enterprise knowledge search";
        }
        """)]
    [InlineData(
        "integration-test.TS",
        """
        function deskvaultDocument(): string {
            return "enterprise knowledge search";
        }
        """)]
    [InlineData(
        "integration-test.CSS",
        """
        .deskvault-document {
            content: "enterprise knowledge search";
        }
        """)]
    [InlineData(
        "integration-test.SQL",
        """
        SELECT 'enterprise knowledge search' AS purpose;
        """)]
    [InlineData(
        "integration-test.PS1",
        """
        function Get-DeskVaultDocument {
            return "enterprise knowledge search"
        }
        """)]
    public async Task ImportDocument_WhenSourceCodeFormatIsValid_TreatsContentAsSearchableText(
        string fileName,
        string sourceText)
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
                    fileName);

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
                        "Source Code Integration Document"));

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

            Assert.NotNull(
                document);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            string normalizedSourceText =
                sourceText
                    .Replace(
                        "\r\n",
                        "\n",
                        StringComparison.Ordinal)
                    .Replace(
                        "\r",
                        "\n",
                        StringComparison.Ordinal);

            string normalizedIndexedText =
                indexedText
                    .Replace(
                        "\r\n",
                        "\n",
                        StringComparison.Ordinal)
                    .Replace(
                        "\r",
                        "\n",
                        StringComparison.Ordinal);

            Assert.Contains(
                normalizedSourceText,
                normalizedIndexedText,
                StringComparison.Ordinal);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "enterprise knowledge search"));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == documentId);

            Assert.Equal(
                fileName,
                matchingResult.FileName);

            Assert.Contains(
                "enterprise knowledge search",
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
    public async Task ImportDocument_WhenJsonDocumentIsMalformed_PersistsFailedStatusAndKeepsStoredFile()
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
                    "malformed.json");

            await File.WriteAllTextAsync(
                sourceFilePath,
                """
                {
                  "name": "DeskVault",
                  "purpose":
                """,
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
                        "Malformed JSON Integration Document"));

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

            Assert.NotNull(
                importedDocument);

            Assert.Equal(
                DocumentStatus.Imported,
                importedDocument.Status);

            await Assert.ThrowsAnyAsync<Exception>(
                () =>
                    harness.ProcessingService.ProcessAsync(
                        documentId));

            Document? failedDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(
                failedDocument);

            Assert.Equal(
                DocumentStatus.Failed,
                failedDocument.Status);

            Assert.True(
                File.Exists(
                    failedDocument.StoredFilePath));

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.Empty(
                chunks);
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
    public async Task ImportDocument_WhenFileTypeIsUnsupported_RejectsImportBeforePersistence()
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
                    "unsupported-test.xyz");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "This content must not be imported.",
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
                        "Unsupported Format Integration Document"));

            Assert.Equal(
                ImportDocumentResultStatus.UnsupportedFileType,
                importResult.Status);

            Assert.Null(
                importResult.DocumentId);

            Assert.Equal(
                "The file type '.xyz' is not supported.",
                importResult.Description);

            IReadOnlyList<Document> documents =
                await harness.GetDocumentsAsync();

            Assert.Empty(
                documents);
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
    public async Task ImportDocument_WhenTxtDocumentIsValid_CompletesProcessingAndMakesDocumentSearchable()
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
                    "regression-test.txt");

            const string sourceText =
                "DeskVault TXT regression content for enterprise knowledge search.";

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
                        "TXT Regression Integration Document"));

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

            Assert.NotNull(
                document);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                sourceText,
                indexedText,
                StringComparison.Ordinal);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "enterprise knowledge search"));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == documentId);

            Assert.Equal(
                "regression-test.txt",
                matchingResult.FileName);

            Assert.Equal(
                "TXT Regression Integration Document",
                matchingResult.DisplayName);

            Assert.Contains(
                "enterprise knowledge search",
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
    public async Task ProcessDocument_WhenCancelledBeforeProcessing_DoesNotPersistChunks()
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
                    "cancellation-test.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault cancellation integration content.",
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
                        "Cancellation Integration Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    harness.ProcessingService.ProcessAsync(
                        documentId,
                        cancellationTokenSource.Token));

            Document? document =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(
                document);

            Assert.Equal(
                DocumentStatus.Imported,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.Empty(
                chunks);
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
    public async Task ProcessDocument_WhenCancellationOccursAfterPreviousSuccess_PreservesPreviousChunksAndGeneration()
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
                    "cancellation-after-success-test.txt");

            const string initialText =
                "DeskVault initial successful processing content.";

            const string updatedText =
                "DeskVault updated content that must not replace the previous successful chunks.";

            await File.WriteAllTextAsync(
                sourceFilePath,
                initialText,
                Encoding.UTF8);

            Guid documentId;
            string previousChunkText;

            await using (var initialHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey))
            {
                ImportDocumentResult importResult =
                    await initialHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Cancellation After Success Integration Document"));

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

                Assert.NotNull(
                    successfulDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    successfulDocument.Status);

                Assert.Equal(
                    1L,
                    successfulDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    successfulDocument.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> previousChunks =
                    await initialHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    previousChunks);

                previousChunkText =
                    string.Join(
                        "\n",
                        previousChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));
            }

            await File.WriteAllTextAsync(
                sourceFilePath,
                updatedText,
                Encoding.UTF8);

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

                Document? cancelledDocument =
                    await reprocessingHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    cancelledDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    cancelledDocument.Status);

                Assert.Equal(
                    2L,
                    cancelledDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    cancelledDocument.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> preservedChunks =
                    await reprocessingHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    preservedChunks);

                string preservedChunkText =
                    string.Join(
                        "\n",
                        preservedChunks
                            .OrderBy(
                                chunk => chunk.Order)
                            .Select(
                                chunk => chunk.Text));

                Assert.Equal(
                    previousChunkText,
                    preservedChunkText);

                Assert.DoesNotContain(
                    updatedText,
                    preservedChunkText,
                    StringComparison.Ordinal);

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
    public async Task ProcessDocument_WhenProcessingFailsAfterPreviousSuccess_PreservesPreviousChunksAndGeneration()
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
                    "failure-after-success-test.json");

            const string initialText =
                """
                {
                  "purpose": "initial successful processing"
                }
                """;

            const string invalidText =
                """
                {
                  "purpose":
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                initialText,
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
                        "Failure After Success Integration Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            await harness.ProcessingService.ProcessAsync(
                documentId);

            Document? successfulDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(
                successfulDocument);

            Assert.Equal(
                DocumentStatus.Available,
                successfulDocument.Status);

            Assert.Equal(
                1L,
                successfulDocument.ProcessingGeneration);

            Assert.Equal(
                1L,
                successfulDocument.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> previousChunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                previousChunks);

            string previousChunkText =
                string.Join(
                    "\n",
                    previousChunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            await File.WriteAllTextAsync(
                sourceFilePath,
                invalidText,
                Encoding.UTF8);

            Document? currentDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(
                currentDocument);

            File.Delete(
                currentDocument.StoredFilePath);

            await harness.StoreArtifactAsync(
                sourceFilePath,
                documentId);

            await Assert.ThrowsAnyAsync<Exception>(
                () =>
                    harness.ProcessingService.ProcessAsync(
                        documentId));

            Document? failedDocument =
                await harness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(
                failedDocument);

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
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                preservedChunks);

            string preservedChunkText =
                string.Join(
                    "\n",
                    preservedChunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Equal(
                previousChunkText,
                preservedChunkText);

            Assert.DoesNotContain(
                invalidText,
                preservedChunkText,
                StringComparison.Ordinal);
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
    public async Task ImportDocument_WhenDuplicateSourceIsImportedTwice_ReturnsDuplicateAndDoesNotCreateSecondDocument()
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

            const string sourceText =
                "DeskVault duplicate import integration content.";

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            ImportDocumentResult firstImportResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "First Duplicate Integration Document"));

            ImportDocumentResult secondImportResult =
                await harness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Second Duplicate Integration Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                firstImportResult.Status);

            Assert.Equal(
                ImportDocumentResultStatus.Duplicate,
                secondImportResult.Status);

            Assert.NotNull(
                firstImportResult.DocumentId);

            Assert.Null(
                secondImportResult.DocumentId);

            Assert.Equal(
                "The document has already been imported.",
                secondImportResult.Description);

            IReadOnlyList<Document> documents =
                await harness.GetDocumentsAsync();

            Assert.Single(
                documents);

            Document document =
                Assert.Single(
                    documents);

            Assert.Equal(
                firstImportResult.DocumentId.Value,
                document.Id);

            Assert.Equal(
                "First Duplicate Integration Document",
                document.DisplayName);

            await harness.ProcessingService.ProcessAsync(
                firstImportResult.DocumentId.Value);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    firstImportResult.DocumentId.Value);

            Assert.NotEmpty(
                chunks);

            Assert.All(
                chunks,
                chunk =>
                    Assert.Equal(
                        firstImportResult.DocumentId.Value,
                        chunk.DocumentId));
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
    public async Task ImportDocument_WhenCsvDocumentIsValid_CompletesProcessingAndMakesDocumentSearchable()
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
                    "regression-test.csv");

            const string sourceText =
                """
                    name,purpose
                    DeskVault,enterprise knowledge search
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
                        "CSV Regression Integration Document"));

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

            Assert.NotNull(
                document);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "enterprise knowledge search",
                indexedText,
                StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "enterprise knowledge search"));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == documentId);

            Assert.Equal(
                "regression-test.csv",
                matchingResult.FileName);

            Assert.Equal(
                "CSV Regression Integration Document",
                matchingResult.DisplayName);

            Assert.Contains(
                "enterprise knowledge search",
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
    public async Task ImportDocument_WhenMarkdownDocumentIsValid_CompletesProcessingAndMakesDocumentSearchable()
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
                    "regression-test.md");

            const string sourceText =
                """
                    # DeskVault

                    Enterprise knowledge search regression content.
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
                        "Markdown Regression Integration Document"));

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

            Assert.NotNull(
                document);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "Enterprise knowledge search regression content.",
                indexedText,
                StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "enterprise knowledge search"));

            SearchDocumentsResult matchingResult =
                Assert.Single(
                    searchResults,
                    result =>
                        result.DocumentId == documentId);

            Assert.Equal(
                "regression-test.md",
                matchingResult.FileName);

            Assert.Equal(
                "Markdown Regression Integration Document",
                matchingResult.DisplayName);

            Assert.Contains(
                "Enterprise knowledge search regression content.",
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
    public async Task ProcessDocument_WhenJsonDocumentIsValid_PreservesSourceArtifactContent()
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
                    "artifact-preservation.json");

            const string sourceText =
                """
            {
              "name": "DeskVault",
              "purpose": "artifact preservation integration"
            }
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
                        "Artifact Preservation Integration Document"));

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

            Assert.NotNull(
                document);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            Assert.True(
                File.Exists(
                    document.StoredFilePath));

            await using Stream decryptedStream =
                await harness.DocumentReader.OpenReadAsync(
                    document.StoredFilePath);

            using var reader =
                new StreamReader(
                    decryptedStream,
                    Encoding.UTF8);

            string storedText =
                await reader.ReadToEndAsync();

            Assert.Equal(
                sourceText,
                storedText);
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

    private sealed class CancellingDocumentTextExtractor
        : IDocumentTextExtractor
    {
        private readonly TaskCompletionSource<bool>
            _extractionStarted =
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

}

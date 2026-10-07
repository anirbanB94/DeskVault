using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Entities;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Integration.Tests;

public sealed class DocumentEvidenceIntegrationTests
{
    [Fact]
    public async Task ProcessDocument_WhenSameContentAndConfigurationIsProcessedRepeatedly_PreservesDeterministicChunkEvidenceAndSearch()
    {
        string rootDirectory =
            CreateRootDirectory();

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
                    "deterministic-evidence.txt");

            const string sourceText =
                """
                Alpha paragraph proves deterministic chunk evidence.

                Beta paragraph carries the stable processing search content.

                Gamma paragraph verifies repeated processing remains deterministic.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            var processingOptions =
                new DocumentProcessingOptions
                {
                    MaxChunkSize = 70,
                    ChunkOverlap = 8
                };

            Guid documentId;

            await using (
                var harness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        processingOptions: processingOptions))
            {
                documentId =
                    await ImportDocumentAsync(
                        harness,
                        sourceFilePath,
                        "Deterministic Evidence Integration Document");

                await harness.ProcessingService.ProcessAsync(
                    documentId);

                Document? firstDocument =
                    await harness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    firstDocument);

                DocumentKnowledgeAvailability firstKeywordSearchAvailability =
                    Assert.Single(
                        firstDocument.KnowledgeAvailability,
                        availability =>
                            availability.Representation ==
                            DocumentKnowledgeRepresentationKind.KeywordSearch);

                Assert.Equal(
                    DocumentKnowledgeAvailabilityState.Available,
                    firstKeywordSearchAvailability.State);

                Assert.Equal(
                    1L,
                    firstKeywordSearchAvailability.LastAvailableProcessingGeneration);

                Assert.Equal(
                    DocumentStatus.Available,
                    firstDocument.Status);

                Assert.Equal(
                    1L,
                    firstDocument.ProcessingGeneration);

                Assert.Equal(
                    1L,
                    firstDocument.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> firstChunks =
                    await harness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    firstChunks);

                AssertChunkEvidence(
                    firstChunks,
                    documentId,
                    expectedProcessingGeneration: 1L,
                    processingOptions);

                IReadOnlyList<ChunkEvidenceSnapshot> firstSnapshot =
                    CreateSnapshot(
                        firstChunks);

                SearchDocumentsPage firstSearchPage =
                    await harness.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "stable processing search content"));

                AssertSearchResult(
                    firstSearchPage,
                    documentId,
                    "stable processing search content");

                await harness.ProcessingService.ProcessAsync(
                    documentId);

                Document? secondDocument =
                    await harness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    secondDocument);

                DocumentKnowledgeAvailability secondKeywordSearchAvailability =
                    Assert.Single(
                        secondDocument.KnowledgeAvailability,
                        availability =>
                            availability.Representation ==
                            DocumentKnowledgeRepresentationKind.KeywordSearch);

                Assert.Equal(
                    DocumentKnowledgeAvailabilityState.Available,
                    secondKeywordSearchAvailability.State);

                Assert.Equal(
                    2L,
                    secondKeywordSearchAvailability.LastAvailableProcessingGeneration);

                Assert.Equal(
                    DocumentStatus.Available,
                    secondDocument.Status);

                Assert.Equal(
                    2L,
                    secondDocument.ProcessingGeneration);

                Assert.Equal(
                    2L,
                    secondDocument.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> secondChunks =
                    await harness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    secondChunks);

                AssertChunkEvidence(
                    secondChunks,
                    documentId,
                    expectedProcessingGeneration: 2L,
                    processingOptions);

                IReadOnlyList<ChunkEvidenceSnapshot> secondSnapshot =
                    CreateSnapshot(
                        secondChunks);

                Assert.Equal(
                    firstSnapshot,
                    secondSnapshot);

                Assert.Equal(
                    secondChunks.Count,
                    secondChunks
                        .Select(
                            chunk => chunk.Id)
                        .Distinct()
                        .Count());

                SearchDocumentsPage secondSearchPage =
                    await harness.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "stable processing search content"));

                AssertSearchResult(
                    secondSearchPage,
                    documentId,
                    "stable processing search content");
            }
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task SearchDocuments_WhenPaginated_ResumesDeterministicallyAcrossDifferentPageSizes()
    {
        // Arrange
        string rootDirectory =
            CreateRootDirectory();

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        try
        {
            (string FileName, string DisplayName)[] documents =
            [
                ("alpha.txt", "Alpha Document"),
            ("beta.txt", "Beta Document"),
            ("gamma.txt", "Gamma Document"),
            ("delta.txt", "Delta Document")
            ];

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            foreach ((string fileName, string displayName) in documents)
            {
                string sourceFilePath =
                    Path.Combine(
                        rootDirectory,
                        fileName);

                await File.WriteAllTextAsync(
                    sourceFilePath,
                    $"This document contains the security term for deterministic pagination: {displayName}.");

                Guid documentId =
                    await ImportDocumentAsync(
                        harness,
                        sourceFilePath,
                        displayName);

                await harness.ProcessingService.ProcessAsync(
                    documentId);
            }

            SearchDocumentsQuery fullQuery =
                new(
                    "security",
                    Limit: 10);

            SearchDocumentsQuery firstPageQuery =
                new(
                    "security",
                    Limit: 2);

            // Act
            SearchDocumentsPage fullPage =
                await harness.SearchHandler.HandleAsync(
                    fullQuery);

            SearchDocumentsPage firstPage =
                await harness.SearchHandler.HandleAsync(
                    firstPageQuery);

            SearchDocumentsPage secondPage =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "security",
                        Continuation: firstPage.Continuation,
                        Limit: 1));

            SearchDocumentsPage thirdPage =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "security",
                        Continuation: secondPage.Continuation,
                        Limit: 5));

            // Assert
            Assert.Equal(
                [
                    "Alpha Document",
                "Beta Document",
                "Delta Document",
                "Gamma Document"
                ],
                fullPage.Results
                    .Select(
                        result => result.DisplayName));

            Assert.Equal(
                [
                    "Alpha Document",
                "Beta Document"
                ],
                firstPage.Results
                    .Select(
                        result => result.DisplayName));

            Assert.True(
                firstPage.HasMore);

            Assert.NotNull(
                firstPage.Continuation);

            Assert.DoesNotMatch(
                "^\\d+$",
                firstPage.Continuation.Value);

            Assert.Equal(
                "Delta Document",
                Assert.Single(
                    secondPage.Results)
                    .DisplayName);

            Assert.True(
                secondPage.HasMore);

            Assert.NotNull(
                secondPage.Continuation);

            Assert.DoesNotMatch(
                "^\\d+$",
                secondPage.Continuation.Value);

            Assert.Equal(
                "Gamma Document",
                Assert.Single(
                    thirdPage.Results)
                    .DisplayName);

            Assert.False(
                thirdPage.HasMore);

            Assert.Null(
                thirdPage.Continuation);

            List<SearchDocumentsResult> pagedResults =
            [
                .. firstPage.Results,
            .. secondPage.Results,
            .. thirdPage.Results
            ];

            Assert.Equal(
                fullPage.Results.Select(
                    result =>
                        (
                            result.DocumentId,
                            result.FileName,
                            result.DisplayName,
                            result.MatchCount)),
                pagedResults.Select(
                    result =>
                        (
                            result.DocumentId,
                            result.FileName,
                            result.DisplayName,
                            result.MatchCount)));

            Assert.Equal(
                pagedResults.Count,
                pagedResults
                    .Select(
                        result => result.DocumentId)
                    .Distinct()
                    .Count());
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ProcessDocument_WhenChunkConfigurationChanges_ProducesDeterministicNewBoundariesAndRemainsSearchable()
    {
        string rootDirectory =
            CreateRootDirectory();

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
                    "configuration-evidence.txt");

            const string sourceText =
                """
                Alpha paragraph proves deterministic chunk evidence.

                Beta paragraph carries the configurable boundary marker.

                Gamma paragraph remains searchable after reprocessing.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            Guid documentId;

            var configurationA =
                new DocumentProcessingOptions
                {
                    MaxChunkSize = 120,
                    ChunkOverlap = 0
                };

            await using (
                var firstHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        processingOptions: configurationA))
            {
                documentId =
                    await ImportDocumentAsync(
                        firstHarness,
                        sourceFilePath,
                        "Configuration Evidence Integration Document");

                await firstHarness.ProcessingService.ProcessAsync(
                    documentId);

                Document? document =
                    await firstHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    document);

                Assert.Equal(
                    1L,
                    document.ProcessingGeneration);

                List<DocumentChunkEntity> chunks =
                    await firstHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    chunks);

                AssertChunkEvidence(
                    chunks,
                    documentId,
                    expectedProcessingGeneration: 1L,
                    configurationA);

                Assert.Equal(
                    2,
                    chunks.Count);

                Assert.Contains(
                    chunks,
                    chunk =>
                        chunk.Text.Contains(
                            "Alpha paragraph",
                            StringComparison.Ordinal) &&
                        chunk.Text.Contains(
                            "Beta paragraph",
                            StringComparison.Ordinal));

                Assert.DoesNotContain(
                    chunks,
                    chunk =>
                        chunk.Text.Contains(
                            "Gamma paragraph",
                            StringComparison.Ordinal) &&
                        chunk.Text.Contains(
                            "Beta paragraph",
                            StringComparison.Ordinal));
            }

            var configurationB =
                new DocumentProcessingOptions
                {
                    MaxChunkSize = 70,
                    ChunkOverlap = 0
                };

            IReadOnlyList<ChunkEvidenceSnapshot> firstConfigurationBSnapshot;

            await using (
                var secondHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        processingOptions: configurationB))
            {
                await secondHarness.ProcessingService.ProcessAsync(
                    documentId);

                Document? document =
                    await secondHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    document);

                Assert.Equal(
                    2L,
                    document.ProcessingGeneration);

                Assert.Equal(
                    2L,
                    document.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> chunks =
                    await secondHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    chunks);

                AssertChunkEvidence(
                    chunks,
                    documentId,
                    expectedProcessingGeneration: 2L,
                    configurationB);

                Assert.NotEqual(
                    configurationA.MaxChunkSize,
                    configurationB.MaxChunkSize);

                Assert.DoesNotContain(
                    chunks,
                    chunk =>
                        chunk.Text.Contains(
                            "Alpha paragraph",
                            StringComparison.Ordinal) &&
                        chunk.Text.Contains(
                            "Beta paragraph",
                            StringComparison.Ordinal));

                Assert.Contains(
                    chunks,
                    chunk =>
                        chunk.Text.Contains(
                            "Beta paragraph",
                            StringComparison.Ordinal));

                Assert.Contains(
                    chunks,
                    chunk =>
                        chunk.Text.Contains(
                            "Gamma paragraph",
                            StringComparison.Ordinal));

                Assert.NotNull(
                    document.LastSuccessfulProcessingRuleVersion);

                firstConfigurationBSnapshot =
                    CreateSnapshot(
                        chunks);

                SearchDocumentsPage searchPage =
                    await secondHarness.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "configurable boundary marker"));

                AssertSearchResult(
                    searchPage,
                    documentId,
                    "configurable boundary marker");
            }

            await using (
                var thirdHarness =
                    new DocumentPipelineTestHarness(
                        rootDirectory,
                        databasePath,
                        encryptionKey,
                        processingOptions: configurationB))
            {
                await thirdHarness.ProcessingService.ProcessAsync(
                    documentId);

                Document? document =
                    await thirdHarness.GetDocumentAsync(
                        documentId);

                Assert.NotNull(
                    document);

                Assert.Equal(
                    3L,
                    document.ProcessingGeneration);

                Assert.Equal(
                    3L,
                    document.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> chunks =
                    await thirdHarness.GetChunksAsync(
                        documentId);

                Assert.NotEmpty(
                    chunks);

                AssertChunkEvidence(
                    chunks,
                    documentId,
                    expectedProcessingGeneration: 3L,
                    configurationB);

                IReadOnlyList<ChunkEvidenceSnapshot> secondConfigurationBSnapshot =
                    CreateSnapshot(
                        chunks);

                Assert.Equal(
                    firstConfigurationBSnapshot,
                    secondConfigurationBSnapshot);

                Assert.Equal(
                    chunks.Count,
                    chunks
                        .Select(
                            chunk => chunk.Id)
                        .Distinct()
                        .Count());

                SearchDocumentsPage searchPage =
                    await thirdHarness.SearchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "configurable boundary marker"));

                AssertSearchResult(
                    searchPage,
                    documentId,
                    "configurable boundary marker");
            }
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ProcessDocument_WhenStructuredContentIsProcessed_RemainsSearchableThroughCurrentKeywordSearch()
    {
        string rootDirectory =
            CreateRootDirectory();

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
                    "structured-evidence.csv");

            const string sourceText =
                """
                name,purpose
                DeskVault,enterprise knowledge search
                Evidence Pipeline,deterministic structured processing
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

            Guid documentId =
                await ImportDocumentAsync(
                    harness,
                    sourceFilePath,
                    "Structured Evidence Integration Document");

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

            string persistedSearchableText =
                string.Join(
                    "\n\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "name: DeskVault",
                persistedSearchableText,
                StringComparison.Ordinal);

            Assert.Contains(
                "purpose: enterprise knowledge search",
                persistedSearchableText,
                StringComparison.Ordinal);

            Assert.Contains(
                "name: Evidence Pipeline",
                persistedSearchableText,
                StringComparison.Ordinal);

            Assert.Contains(
                "purpose: deterministic structured processing",
                persistedSearchableText,
                StringComparison.Ordinal);

            SearchDocumentsPage searchPage =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "enterprise knowledge search"));

            AssertSearchResult(
                searchPage,
                documentId,
                "enterprise knowledge search");
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ProcessDocument_WhenDirectTextProvenanceIsAvailable_PersistsApplicableSourceLocation()
    {
        string rootDirectory =
            CreateRootDirectory();

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
                    "provenance-evidence.txt");

            const string sourceText =
                """
                First source paragraph contains direct text provenance evidence.

                Second source paragraph demonstrates persisted source location tracking.

                Third source paragraph verifies all emitted chunks retain applicable lines.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            var processingOptions =
                new DocumentProcessingOptions
                {
                    MaxChunkSize = 70,
                    ChunkOverlap = 0
                };

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    processingOptions: processingOptions);

            Guid documentId =
                await ImportDocumentAsync(
                    harness,
                    sourceFilePath,
                    "Provenance Evidence Integration Document");

            await harness.ProcessingService.ProcessAsync(
                documentId);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            Assert.All(
                chunks,
                chunk =>
                {
                    Assert.True(
                        chunk.SourceLocationStartLine.HasValue);

                    Assert.True(
                        chunk.SourceLocationEndLine.HasValue);

                    Assert.True(
                        chunk.SourceLocationStartLine.Value > 0);

                    Assert.True(
                        chunk.SourceLocationEndLine.Value >=
                        chunk.SourceLocationStartLine.Value);
                });

            int sourceLineCount =
                sourceText
                    .Split(
                        '\n')
                    .Length;

            Assert.All(
                chunks,
                chunk =>
                    Assert.InRange(
                        chunk.SourceLocationEndLine!.Value,
                        chunk.SourceLocationStartLine!.Value,
                        sourceLineCount));
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ProcessDocument_WhenProvenanceIsUnavailable_DoesNotInventSourceLocation()
    {
        string rootDirectory =
            CreateRootDirectory();

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
                    "unmapped-evidence.csv");

            const string sourceText =
                """
                name,purpose
                DeskVault,enterprise knowledge search
                Evidence Pipeline,structured processing
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

            Guid documentId =
                await ImportDocumentAsync(
                    harness,
                    sourceFilePath,
                    "Unmapped Provenance Integration Document");

            await harness.ProcessingService.ProcessAsync(
                documentId);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            Assert.All(
                chunks,
                chunk =>
                {
                    Assert.Null(
                        chunk.SourceLocationStartLine);

                    Assert.Null(
                        chunk.SourceLocationEndLine);
                });
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ProcessDocument_WhenOversizedContentIsProcessed_PersistsBoundedChunksAndRemainsSearchable()
    {
        string rootDirectory =
            CreateRootDirectory();

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
                    "oversized-evidence.txt");

            string sourceText =
                "OVERSIZED_SEARCH_TARGET " +
                string.Join(
                    " ",
                    Enumerable.Repeat(
                        "oversized evidence content remains bounded and searchable",
                        8));

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            const int maxChunkSize = 40;

            var processingOptions =
                new DocumentProcessingOptions
                {
                    MaxChunkSize = maxChunkSize,
                    ChunkOverlap = 0
                };

            await using var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    processingOptions: processingOptions);

            Guid documentId =
                await ImportDocumentAsync(
                    harness,
                    sourceFilePath,
                    "Oversized Evidence Integration Document");

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

            Assert.Equal(
                1L,
                document.ProcessingGeneration);

            Assert.Equal(
                1L,
                document.LastSuccessfulProcessingGeneration);

            List<DocumentChunkEntity> chunks =
                await harness.GetChunksAsync(
                    documentId);

            Assert.NotEmpty(
                chunks);

            Assert.All(
                chunks,
                chunk =>
                {
                    Assert.NotEmpty(
                        chunk.Text);

                    Assert.InRange(
                        chunk.Text.Length,
                        1,
                        maxChunkSize);
                });

            AssertChunkEvidence(
                chunks,
                documentId,
                expectedProcessingGeneration: 1L,
                processingOptions);

            SearchDocumentsPage searchPage =
                await harness.SearchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "OVERSIZED_SEARCH_TARGET"));

            AssertSearchResult(
                searchPage,
                documentId,
                "OVERSIZED_SEARCH_TARGET");
        }
        finally
        {
            DeleteRootDirectory(
                rootDirectory);
        }
    }

    private static async Task<Guid> ImportDocumentAsync(
        DocumentPipelineTestHarness harness,
        string sourceFilePath,
        string displayName)
    {
        ImportDocumentResult importResult =
            await harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    displayName));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        return importResult.DocumentId.Value;
    }

    private static void AssertSearchResult(
        SearchDocumentsPage searchPage,
        Guid documentId,
        string expectedContext)
    {
        SearchDocumentsResult matchingResult =
            Assert.Single(
                searchPage.Results,
                result =>
                    result.DocumentId == documentId);

        Assert.Contains(
            matchingResult.Matches,
            match =>
                match.Source == SearchMatchSource.ProcessedContent &&
                match.Context.Contains(
                    expectedContext,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertChunkEvidence(
        IReadOnlyList<DocumentChunkEntity> chunks,
        Guid documentId,
        long expectedProcessingGeneration,
        DocumentProcessingOptions processingOptions)
    {
        Assert.Equal(
            chunks.Count,
            chunks
                .Select(
                    chunk => chunk.Order)
                .Distinct()
                .Count());

        Assert.Equal(
            chunks.Count,
            chunks
                .Select(
                    chunk => chunk.Id)
                .Distinct()
                .Count());

        string expectedRuleVersion =
            new DocumentTextChunker(
                processingOptions.MaxChunkSize,
                processingOptions.ChunkOverlap)
            .RuleVersion
            .Value;

        Assert.All(
            chunks,
            chunk =>
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
                    expectedProcessingGeneration,
                    chunk.ProcessingGeneration);

                Assert.Equal(
                    expectedRuleVersion,
                    chunk.ChunkingRuleVersion);
            });
    }

    private static IReadOnlyList<ChunkEvidenceSnapshot> CreateSnapshot(
        IReadOnlyList<DocumentChunkEntity> chunks)
    {
        return chunks
            .OrderBy(
                chunk => chunk.Order)
            .Select(
                chunk =>
                    new ChunkEvidenceSnapshot(
                        chunk.Id,
                        chunk.Order,
                        chunk.Text,
                        chunk.ContentHash,
                        chunk.ChunkingRuleVersion,
                        chunk.SourceLocationStartLine,
                        chunk.SourceLocationEndLine))
            .ToArray();
    }

    private static string CreateRootDirectory()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            rootDirectory);

        return rootDirectory;
    }

    private static void DeleteRootDirectory(
        string rootDirectory)
    {
        if (Directory.Exists(
                rootDirectory))
        {
            Directory.Delete(
                rootDirectory,
                recursive: true);
        }
    }

    private sealed record ChunkEvidenceSnapshot(
        Guid Id,
        int Order,
        string Text,
        string ContentHash,
        string? ChunkingRuleVersion,
        int? SourceLocationStartLine,
        int? SourceLocationEndLine);
}

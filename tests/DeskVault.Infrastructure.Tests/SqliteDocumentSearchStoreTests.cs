using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDocumentSearchStoreTests
{
    [Fact]
    public async Task SearchAsync_WhenMatchingChunkExists_ReturnsDocumentAndMatch()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "searchable.txt",
                "Searchable Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "This is the introduction."),

                new DocumentChunk(
                    1,
                    "This chunk contains the searchable content."),

                new DocumentChunk(
                    2,
                    "This is the conclusion.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "searchable"));

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.Equal(
            document.FileName,
            result.FileName);

        Assert.Equal(
            document.DisplayName,
            result.DisplayName);

        Assert.Equal(
            3,
            result.MatchCount);

        Assert.Collection(
            result.Matches,
            match =>
            {
                Assert.Equal(
                    SearchMatchSource.DocumentMetadata,
                    match.Source);

                Assert.Equal(
                    SearchMatchKind.Exact,
                    match.Kind);

                Assert.Equal(
                    "searchable.txt",
                    match.Context);
            },
            match =>
            {
                Assert.Equal(
                    SearchMatchSource.DocumentMetadata,
                    match.Source);

                Assert.Equal(
                    SearchMatchKind.Exact,
                    match.Kind);

                Assert.Equal(
                    "Searchable Document",
                    match.Context);
            },
            match =>
            {
                Assert.Equal(
                    SearchMatchSource.ProcessedContent,
                    match.Source);

                Assert.Equal(
                    SearchMatchKind.Exact,
                    match.Kind);

                Assert.Equal(
                    "This chunk contains the searchable content.",
                    match.Context);
            });
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextDiffersOnlyByCase_ReturnsMatchingChunk()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "case-test.txt",
                "Case Test Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Security policy content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "SECURITY"));

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.Equal(
            1,
            result.MatchCount);

        SearchMatch match =
            Assert.Single(result.Matches);

        Assert.Equal(
            SearchMatchSource.ProcessedContent,
            match.Source);

        Assert.Equal(
            "Security policy content.",
            match.Context);
    }

    [Fact]
    public async Task SearchAsync_WhenNoChunkMatches_ReturnsEmpty()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "document.txt",
                "Test Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "This document contains ordinary content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "does-not-exist"));

        Assert.Empty(
            results);
    }

    [Fact]
    public async Task SearchAsync_WhenStaleProcessingGenerationExists_ReturnsOnlyAuthoritativeContent()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "generation-test.txt",
                "Generation Test Document");

        var processingStore =
            CreateProcessingStore(connection);

        long firstGeneration =
            await processingStore.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            1L,
            firstGeneration);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            firstGeneration,
            [
                new DocumentChunk(
                    0,
                    "The authoritative-search-term is current content.")
            ]);

        await processingStore.PublishSuccessfulProcessingAsync(
            document.Id,
            firstGeneration);

        long secondGeneration =
            await processingStore.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            2L,
            secondGeneration);

        await processingStore.PublishSuccessfulProcessingAsync(
            document.Id,
            secondGeneration);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> staleResults =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "authoritative-search-term"));

        Assert.Empty(
            staleResults);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsInDocumentAndChunkOrder()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document firstDocument =
            CreateAndPersistDocument(
                connection,
                "first.txt",
                "Alpha Document");

        Document secondDocument =
            CreateAndPersistDocument(
                connection,
                "second.txt",
                "Beta Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            secondDocument.Id,
            0L,
            [
                new DocumentChunk(
                    1,
                    "Beta matching content second."),

                new DocumentChunk(
                    0,
                    "Beta matching content first.")
            ]);

        await processingStore.ReplaceChunksAsync(
            firstDocument.Id,
            0L,
            [
                new DocumentChunk(
                    1,
                    "Alpha matching content second."),

                new DocumentChunk(
                    0,
                    "Alpha matching content first.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "matching"));

        Assert.Equal(
            2,
            results.Count);

        AssertResult(
            results[0],
            firstDocument,
            [
                "Alpha matching content first.",
                "Alpha matching content second."
            ]);

        AssertResult(
            results[1],
            secondDocument,
            [
                "Beta matching content first.",
                "Beta matching content second."
            ]);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextContainsPercentCharacter_MatchesLiteralPercentCharacter()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document matchingDocument =
            CreateAndPersistDocument(
                connection,
                "percent-match.txt",
                "Percent Match");

        Document nonMatchingDocument =
            CreateAndPersistDocument(
                connection,
                "percent-no-match.txt",
                "Percent No Match");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            matchingDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Completion reached 100%.")
            ]);

        await processingStore.ReplaceChunksAsync(
            nonMatchingDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Completion reached 100 percent.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "100%"));

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            matchingDocument.Id,
            result.DocumentId);

        SearchMatch match =
            Assert.Single(result.Matches);

        Assert.Equal(
            SearchMatchSource.ProcessedContent,
            match.Source);

        Assert.Equal(
            "Completion reached 100%.",
            match.Context);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextContainsUnderscoreCharacter_MatchesLiteralUnderscoreCharacter()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document matchingDocument =
            CreateAndPersistDocument(
                connection,
                "underscore-match.txt",
                "Underscore Match");

        Document nonMatchingDocument =
            CreateAndPersistDocument(
                connection,
                "underscore-no-match.txt",
                "Underscore No Match");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            matchingDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "The value is file_name.")
            ]);

        await processingStore.ReplaceChunksAsync(
            nonMatchingDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "The value is filename.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "file_name"));

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            matchingDocument.Id,
            result.DocumentId);

        SearchMatch match =
            Assert.Single(result.Matches);

        Assert.Equal(
            SearchMatchSource.ProcessedContent,
            match.Source);

        Assert.Equal(
            "The value is file_name.",
            match.Context);
    }

    [Fact]
    public async Task SearchAsync_WhenSingleFileTypeFilterIsProvided_ReturnsOnlyMatchingFileType()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document textDocument =
            CreateAndPersistDocument(
                connection,
                "matching.txt",
                "Matching Text");

        Document markdownDocument =
            CreateAndPersistDocument(
                connection,
                "excluded.md",
                "Excluded Markdown");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            textDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        await processingStore.ReplaceChunksAsync(
            markdownDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "searchable",
                    [".txt"]));

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            textDocument.Id,
            result.DocumentId);

        Assert.Equal(
            "matching.txt",
            result.FileName);
    }

    [Fact]
    public async Task SearchAsync_WhenMultipleFileTypeFiltersAreProvided_ReturnsAllMatchingFileTypes()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document textDocument =
            CreateAndPersistDocument(
                connection,
                "matching.txt",
                "Matching Text");

        Document markdownDocument =
            CreateAndPersistDocument(
                connection,
                "matching.md",
                "Matching Markdown");

        Document csvDocument =
            CreateAndPersistDocument(
                connection,
                "excluded.csv",
                "Excluded CSV");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            textDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        await processingStore.ReplaceChunksAsync(
            markdownDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        await processingStore.ReplaceChunksAsync(
            csvDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "searchable",
                    ["TXT", ".MD"]));

        Assert.Equal(
            2,
            results.Count);

        Assert.Contains(
            results,
            result => result.DocumentId == textDocument.Id);

        Assert.Contains(
            results,
            result => result.DocumentId == markdownDocument.Id);
    }

    [Fact]
    public async Task SearchAsync_WhenNoFileTypeFilterIsProvided_ReturnsMatchingDocumentsOfAllFileTypes()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document textDocument =
            CreateAndPersistDocument(
                connection,
                "matching.txt",
                "Matching Text");

        Document markdownDocument =
            CreateAndPersistDocument(
                connection,
                "matching.md",
                "Matching Markdown");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            textDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        await processingStore.ReplaceChunksAsync(
            markdownDocument.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Shared searchable content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "searchable"));

        Assert.Equal(
            2,
            results.Count);

        Assert.Contains(
            results,
            result => result.DocumentId == textDocument.Id);

        Assert.Contains(
            results,
            result => result.DocumentId == markdownDocument.Id);
    }

    [Fact]
    public async Task SearchAsync_WhenUnsupportedFileTypeFilterIsProvided_ReturnsEmpty()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "document.txt",
                "Test Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            [
                new DocumentChunk(
                    0,
                    "Searchable content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                new SearchDocumentsQuery(
                    "searchable",
                    [".unsupported"]));

        Assert.Empty(
            results);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextIsWhitespace_ThrowsArgumentException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var searchStore =
            CreateSearchStore(connection);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                searchStore.SearchAsync(
                    new SearchDocumentsQuery(
                        "   ")));
    }

    [Fact]
    public async Task SearchAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var searchStore =
            CreateSearchStore(connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                searchStore.SearchAsync(
                    new SearchDocumentsQuery(
                        "matching"),
                    cancellationTokenSource.Token));
    }

    private static void AssertResult(
        SearchDocumentsResult result,
        Document expectedDocument,
        IReadOnlyList<string> expectedMatchTexts)
    {
        Assert.Equal(
            expectedDocument.Id,
            result.DocumentId);

        Assert.Equal(
            expectedDocument.FileName,
            result.FileName);

        Assert.Equal(
            expectedDocument.DisplayName,
            result.DisplayName);

        Assert.Equal(
            expectedMatchTexts.Count,
            result.MatchCount);

        Assert.Equal(
            expectedMatchTexts.Count,
            result.Matches.Count);

        for (int index = 0;
            index < expectedMatchTexts.Count;
            index++)
        {
            SearchMatch match =
                result.Matches[index];

            Assert.Equal(
                SearchMatchSource.ProcessedContent,
                match.Source);

            Assert.Equal(
                SearchMatchKind.Exact,
                match.Kind);

            Assert.Equal(
                expectedMatchTexts[index],
                match.Context);
        }
    }

    private static SqliteDocumentProcessingStore CreateProcessingStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private static SqliteDocumentSearchStore CreateSearchStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentSearchStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentSearchStore>.Instance);
    }

    private static Document CreateAndPersistDocument(
        SqliteConnection connection,
        string fileName,
        string displayName)
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                fileName,
                displayName,
                $"hash-{Guid.NewGuid():N}",
                $"{Guid.NewGuid():N}.dvault");

        using var context =
            CreateContext(connection);

        context.Documents.Add(
            new DocumentEntity
            {
                Id = document.Id,
                FileName = document.FileName,
                DisplayName = document.DisplayName,
                Sha256Hash = document.Sha256Hash,
                ImportedAt = document.ImportedAt,
                Status = (int)document.Status,
                StoredFilePath = document.StoredFilePath
            });

        context.SaveChanges();

        return document;
    }

    private static SqliteConnection CreateConnection()
    {
        var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        connection.Open();

        using var context =
            CreateContext(connection);

        context.Database.EnsureCreated();

        return connection;
    }

    private static DeskVaultDbContext CreateContext(
        SqliteConnection connection)
    {
        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(connection)
                .Options;

        return new DeskVaultDbContext(
            options);
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(
            connection);
    }

    private sealed class TestDbContextFactory
        : IDbContextFactory<DeskVaultDbContext>
    {
        private readonly SqliteConnection _connection;

        public TestDbContextFactory(
            SqliteConnection connection)
        {
            _connection = connection;
        }

        public DeskVaultDbContext CreateDbContext()
        {
            return CreateContext();
        }

        public Task<DeskVaultDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateContext());
        }

        private DeskVaultDbContext CreateContext()
        {
            return SqliteDocumentSearchStoreTests.CreateContext(
                _connection);
        }
    }
}

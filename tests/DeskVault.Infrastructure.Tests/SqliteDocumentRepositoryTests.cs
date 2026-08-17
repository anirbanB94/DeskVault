using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDocumentRepositoryTests
{
    [Fact]
    public async Task AddAsync_WhenDocumentIsValid_PersistsDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var repository =
            new SqliteDocumentRepository(factory);

        Document document = CreateDocument();

        await repository.AddAsync(document);

        Document? result =
            await repository.GetByIdAsync(document.Id);

        Assert.NotNull(result);
        Assert.Equal(document.Id, result.Id);
        Assert.Equal(document.FileName, result.FileName);
        Assert.Equal(document.DisplayName, result.DisplayName);
        Assert.Equal(document.Sha256Hash, result.Sha256Hash);
        Assert.Equal(document.StoredFilePath, result.StoredFilePath);
        Assert.Equal(document.ImportedAt, result.ImportedAt);
        Assert.Equal(document.Status, result.Status);
    }

    [Fact]
    public async Task ExistsByHashAsync_WhenHashExists_ReturnsTrue()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var repository =
            new SqliteDocumentRepository(factory);

        Document document = CreateDocument();

        await repository.AddAsync(document);

        bool exists =
            await repository.ExistsByHashAsync(
                document.Sha256Hash);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_WhenHashDoesNotExist_ReturnsFalse()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var repository =
            new SqliteDocumentRepository(factory);

        bool exists =
            await repository.ExistsByHashAsync(
                "missing-hash");

        Assert.False(exists);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDocumentsInDescendingImportOrder()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var repository =
            new SqliteDocumentRepository(factory);

        Document older = Document.Restore(
            Guid.NewGuid(),
            "older.txt",
            "Older Document",
            "hash-older",
            "older.dvault",
            DateTime.UtcNow.AddMinutes(-5),
            DocumentStatus.Imported);

        Document newer = Document.Restore(
            Guid.NewGuid(),
            "newer.txt",
            "Newer Document",
            "hash-newer",
            "newer.dvault",
            DateTime.UtcNow,
            DocumentStatus.Imported);

        await repository.AddAsync(older);
        await repository.AddAsync(newer);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync();

        Assert.Equal(2, documents.Count);
        Assert.Equal(newer.Id, documents[0].Id);
        Assert.Equal(older.Id, documents[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentExists_RemovesDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var repository =
            new SqliteDocumentRepository(factory);

        Document document = CreateDocument();

        await repository.AddAsync(document);

        await repository.DeleteAsync(document.Id);

        Document? result =
            await repository.GetByIdAsync(document.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentDoesNotExist_DoesNotThrow()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var repository =
            new SqliteDocumentRepository(factory);

        await repository.DeleteAsync(Guid.NewGuid());
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            "document.dvault");
    }

    private static SqliteConnection CreateConnection()
    {
        var connection =
            new SqliteConnection("Data Source=:memory:");

        connection.Open();

        using var context =
            new DeskVaultDbContext(
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(connection)
                    .Options);

        context.Database.EnsureCreated();

        return connection;
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(connection);
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
            return Task.FromResult(CreateContext());
        }

        private DeskVaultDbContext CreateContext()
        {
            DbContextOptions<DeskVaultDbContext> options =
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(_connection)
                    .Options;

            return new DeskVaultDbContext(options);
        }
    }
}

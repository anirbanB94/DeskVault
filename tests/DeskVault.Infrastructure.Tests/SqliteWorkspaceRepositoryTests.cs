using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteWorkspaceRepositoryTests
{
    [Fact]
    public async Task AddAsync_WhenWorkspaceIsEmpty_PersistsAndRestoresWorkspace()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        await repository.AddAsync(
            workspace);

        Workspace? result =
            await repository.GetByIdAsync(
                workspace.Id);

        Assert.NotNull(result);
        Assert.Equal(workspace.Id, result.Id);
        Assert.Equal(workspace.Name, result.Name);
        Assert.Equal(
            workspace.TypeOfWorkspace,
            result.TypeOfWorkspace);
        Assert.Empty(result.Memberships);
        Assert.Null(result.LastActiveDocumentId);
    }

    [Fact]
    public async Task AddAsync_WhenWorkspaceHasDocuments_PersistsMembershipsAndOrder()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document firstDocument =
            CreateDocument();

        Document secondDocument =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            firstDocument);

        await AddDocumentAsync(
            connection,
            secondDocument);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        workspace.AddDocument(firstDocument.Id);
        workspace.AddDocument(secondDocument.Id);

        await repository.AddAsync(workspace);

        Workspace? result =
            await repository.GetByIdAsync(workspace.Id);

        Assert.NotNull(result);

        Assert.Collection(
            result.Memberships,
            firstMembership =>
            {
                Assert.Equal(
                    firstDocument.Id,
                    firstMembership.DocumentId);

                Assert.Equal(
                    0,
                    firstMembership.Order);
            },
            secondMembership =>
            {
                Assert.Equal(
                    secondDocument.Id,
                    secondMembership.DocumentId);

                Assert.Equal(
                    1,
                    secondMembership.Order);
            });
    }

    [Fact]
    public async Task AddAsync_WhenSameDocumentBelongsToMultipleWorkspaces_PersistsBothMemberships()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Workspace firstWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Workspace secondWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Writing");

        firstWorkspace.AddDocument(document.Id);
        secondWorkspace.AddDocument(document.Id);

        await repository.AddAsync(firstWorkspace);
        await repository.AddAsync(secondWorkspace);

        Workspace? firstResult =
            await repository.GetByIdAsync(firstWorkspace.Id);

        Workspace? secondResult =
            await repository.GetByIdAsync(secondWorkspace.Id);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);

        Assert.Contains(
            firstResult.Memberships,
            membership =>
                membership.DocumentId == document.Id);

        Assert.Contains(
            secondResult.Memberships,
            membership =>
                membership.DocumentId == document.Id);
    }

    [Fact]
    public async Task AddAsync_WhenWorkspaceHasLastActiveDocument_PersistsLastActiveDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        workspace.AddDocument(document.Id);
        workspace.SetLastActiveDocument(document.Id);

        await repository.AddAsync(workspace);

        Workspace? result =
            await repository.GetByIdAsync(workspace.Id);

        Assert.NotNull(result);
        Assert.Equal(
            document.Id,
            result.LastActiveDocumentId);
    }

    [Fact]
    public async Task AddAsync_WhenWorkspaceIsTemporary_RejectsPersistence()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Workspace workspace =
            Workspace.CreateTemporary(
                Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                repository.AddAsync(workspace));

        await using var context =
            CreateContext(connection);

        Assert.False(
            await context.Workspaces.AnyAsync(
                existing =>
                    existing.Id == workspace.Id));

        Assert.False(
            await context.WorkspaceDocumentMemberships.AnyAsync(
                membership =>
                    membership.WorkspaceId == workspace.Id));
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkspaceChanges_PersistsNameMembershipsAndLastActiveDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document firstDocument =
            CreateDocument();

        Document secondDocument =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            firstDocument);

        await AddDocumentAsync(
            connection,
            secondDocument);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Original");

        workspace.AddDocument(firstDocument.Id);

        await repository.AddAsync(workspace);

        workspace.Rename("Renamed");
        workspace.AddDocument(secondDocument.Id);
        workspace.SetLastActiveDocument(secondDocument.Id);

        await repository.UpdateAsync(workspace);

        Workspace? result =
            await repository.GetByIdAsync(workspace.Id);

        Assert.NotNull(result);
        Assert.Equal("Renamed", result.Name);

        Assert.Collection(
            result.Memberships,
            firstMembership =>
            {
                Assert.Equal(
                    firstDocument.Id,
                    firstMembership.DocumentId);

                Assert.Equal(
                    0,
                    firstMembership.Order);
            },
            secondMembership =>
            {
                Assert.Equal(
                    secondDocument.Id,
                    secondMembership.DocumentId);

                Assert.Equal(
                    1,
                    secondMembership.Order);
            });

        Assert.Equal(
            secondDocument.Id,
            result.LastActiveDocumentId);
    }

    [Fact]
    public async Task UpdateAsync_WhenWorkspaceIsTemporary_RejectsPersistenceAndPreservesExistingWorkspace()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Guid workspaceId =
            Guid.NewGuid();

        Workspace persistentWorkspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Original");

        persistentWorkspace.AddDocument(document.Id);

        await repository.AddAsync(
            persistentWorkspace);

        Workspace temporaryWorkspace =
            Workspace.CreateTemporary(
                workspaceId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                repository.UpdateAsync(
                    temporaryWorkspace));

        Workspace? result =
            await repository.GetByIdAsync(
                workspaceId);

        Assert.NotNull(result);
        Assert.Equal(
            "Original",
            result.Name);

        Assert.Equal(
            WorkspaceType.Persistent,
            result.TypeOfWorkspace);

        Assert.Contains(
            result.Memberships,
            membership =>
                membership.DocumentId == document.Id);
    }

    [Fact]
    public async Task UpdateAsync_WhenDocumentIsRemovedFromWorkspace_RemovesOnlyMembership()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        workspace.AddDocument(document.Id);

        await repository.AddAsync(workspace);

        workspace.RemoveDocument(document.Id);

        await repository.UpdateAsync(workspace);

        Workspace? workspaceResult =
            await repository.GetByIdAsync(workspace.Id);

        Document? documentResult =
            await GetDocumentAsync(
                connection,
                document.Id);

        Assert.NotNull(workspaceResult);
        Assert.Empty(workspaceResult.Memberships);
        Assert.NotNull(documentResult);
    }

    [Fact]
    public async Task DeleteAsync_WhenWorkspaceExists_RemovesWorkspaceAndMembershipsButPreservesDocuments()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        workspace.AddDocument(document.Id);

        await repository.AddAsync(workspace);

        await repository.DeleteAsync(workspace.Id);

        Workspace? workspaceResult =
            await repository.GetByIdAsync(workspace.Id);

        Document? documentResult =
            await GetDocumentAsync(
                connection,
                document.Id);

        Assert.Null(workspaceResult);
        Assert.NotNull(documentResult);
    }

    [Fact]
    public async Task DeleteDocument_WhenDocumentHasWorkspaceMembership_RemovesMembershipsButPreservesWorkspace()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        workspace.AddDocument(document.Id);

        await repository.AddAsync(workspace);

        await DeleteDocumentAsync(
            connection,
            document.Id);

        Workspace? result =
            await repository.GetByIdAsync(workspace.Id);

        Assert.NotNull(result);
        Assert.Empty(result.Memberships);
    }

    [Fact]
    public async Task RemoveDocumentFromAllWorkspacesAsync_WhenDocumentBelongsToMultipleWorkspaces_RemovesOnlyItsMemberships()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document firstDocument =
            CreateDocument();

        Document secondDocument =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            firstDocument);

        await AddDocumentAsync(
            connection,
            secondDocument);

        Workspace firstWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Workspace secondWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Writing");

        firstWorkspace.AddDocument(firstDocument.Id);
        firstWorkspace.AddDocument(secondDocument.Id);

        secondWorkspace.AddDocument(firstDocument.Id);

        await repository.AddAsync(firstWorkspace);
        await repository.AddAsync(secondWorkspace);

        await repository.RemoveDocumentFromAllWorkspacesAsync(
            firstDocument.Id);

        Workspace? firstResult =
            await repository.GetByIdAsync(
                firstWorkspace.Id);

        Workspace? secondResult =
            await repository.GetByIdAsync(
                secondWorkspace.Id);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);

        Assert.DoesNotContain(
            firstResult.Memberships,
            membership =>
                membership.DocumentId == firstDocument.Id);

        Assert.Contains(
            firstResult.Memberships,
            membership =>
                membership.DocumentId == secondDocument.Id);

        Assert.DoesNotContain(
            secondResult.Memberships,
            membership =>
                membership.DocumentId == firstDocument.Id);

        Document? firstDocumentResult =
            await GetDocumentAsync(
                connection,
                firstDocument.Id);

        Document? secondDocumentResult =
            await GetDocumentAsync(
                connection,
                secondDocument.Id);

        Assert.NotNull(firstDocumentResult);
        Assert.NotNull(secondDocumentResult);
    }

    [Fact]
    public async Task GetAllAsync_WhenMultipleWorkspacesExist_ReturnsAllWorkspacesWithMemberships()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document firstDocument =
            CreateDocument();

        Document secondDocument =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            firstDocument);

        await AddDocumentAsync(
            connection,
            secondDocument);

        Workspace firstWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Workspace secondWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Writing");

        firstWorkspace.AddDocument(firstDocument.Id);
        secondWorkspace.AddDocument(secondDocument.Id);

        await repository.AddAsync(firstWorkspace);
        await repository.AddAsync(secondWorkspace);

        IReadOnlyList<Workspace> results =
            await repository.GetAllAsync();

        Assert.Equal(2, results.Count);

        Workspace restoredFirst =
            Assert.Single(
                results,
                workspace =>
                    workspace.Id == firstWorkspace.Id);

        Workspace restoredSecond =
            Assert.Single(
                results,
                workspace =>
                    workspace.Id == secondWorkspace.Id);

        Assert.Contains(
            restoredFirst.Memberships,
            membership =>
                membership.DocumentId == firstDocument.Id);

        Assert.Contains(
            restoredSecond.Memberships,
            membership =>
                membership.DocumentId == secondDocument.Id);
    }

    [Fact]
    public async Task AddAsync_WhenDuplicateMembershipExistsInDatabase_RejectsDuplicate()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        await AddDocumentAsync(
            connection,
            document);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        workspace.AddDocument(document.Id);

        await repository.AddAsync(workspace);

        await using var context =
            CreateContext(connection);

        await Assert.ThrowsAnyAsync<DbUpdateException>(
            async () =>
            {
                await context.WorkspaceDocumentMemberships.AddAsync(
                    new WorkspaceDocumentMembershipEntity
                    {
                        WorkspaceId = workspace.Id,
                        DocumentId = document.Id,
                        Order = 0
                    });

                await context.SaveChangesAsync();
            });
    }

    private static SqliteWorkspaceRepository CreateRepository(
        SqliteConnection connection)
    {
        return new SqliteWorkspaceRepository(
            new TestDbContextFactory(connection));
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            $"sha256-test-hash-{Guid.NewGuid():N}",
            "document.dvault");
    }

    private static async Task AddDocumentAsync(
        SqliteConnection connection,
        Document document)
    {
        await using var context =
            CreateContext(connection);

        await context.Documents.AddAsync(
            new DocumentEntity
            {
                Id = document.Id,
                FileName = document.FileName,
                DisplayName = document.DisplayName,
                Sha256Hash = document.Sha256Hash,
                ImportedAt = document.ImportedAt,
                Status = (int)document.Status,
                StoredFilePath = document.StoredFilePath,
                ProcessingGeneration =
                    document.ProcessingGeneration,
                LastSuccessfulProcessingGeneration =
                    document.LastSuccessfulProcessingGeneration
            });

        await context.SaveChangesAsync();
    }

    private static async Task<Document?> GetDocumentAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using var context =
            CreateContext(connection);

        DocumentEntity? entity =
            await context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    document =>
                        document.Id == documentId);

        return entity is null
            ? null
            : Document.Restore(
                entity.Id,
                entity.FileName,
                entity.DisplayName,
                entity.Sha256Hash,
                entity.StoredFilePath,
                entity.ImportedAt,
                (DocumentStatus)entity.Status,
                entity.ProcessingGeneration,
                entity.LastSuccessfulProcessingGeneration);
    }

    private static async Task DeleteDocumentAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using var context =
            CreateContext(connection);

        DocumentEntity entity =
            await context.Documents
                .FirstAsync(
                    document =>
                        document.Id == documentId);

        context.Documents.Remove(entity);

        await context.SaveChangesAsync();
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

        return new DeskVaultDbContext(options);
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
            return SqliteWorkspaceRepositoryTests.CreateContext(
                _connection);
        }
    }
}

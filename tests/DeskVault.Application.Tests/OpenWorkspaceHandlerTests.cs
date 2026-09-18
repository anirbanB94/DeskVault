using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class OpenWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsAlreadyActive_WhenWorkspaceIsAlreadyActive()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Active Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new OpenWorkspaceCommand(workspace.Id));

        Assert.Equal(
            OpenWorkspaceResultStatus.AlreadyActive,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        Assert.Empty(result.MissingDocumentIds);

        context.WorkspaceRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceDoesNotExist()
    {
        var context = CreateContext();

        var workspaceId = Guid.NewGuid();

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspaceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new OpenWorkspaceCommand(workspaceId));

        Assert.Equal(
            OpenWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);

        Assert.Null(result.Workspace);
        Assert.Empty(result.MissingDocumentIds);

        Assert.Null(
            context.ActiveWorkspaceRegistry.Get(workspaceId));
    }

    [Fact]
    public async Task HandleAsync_ActivatesWorkspace_WhenAllDocumentsExist()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Persistent Workspace");

        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.SetLastActiveDocument(secondDocumentId);

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    firstDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(firstDocumentId));

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    secondDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(secondDocumentId));

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new OpenWorkspaceCommand(workspace.Id));

        Assert.Equal(
            OpenWorkspaceResultStatus.Activated,
            result.Status);

        Assert.NotNull(result.Workspace);

        Assert.Equal(
            workspace.Id,
            result.Workspace!.Id);

        Assert.Equal(
            "Persistent Workspace",
            result.Workspace.Name);

        Assert.Equal(
            secondDocumentId,
            result.Workspace.LastActiveDocumentId);

        Assert.Empty(result.MissingDocumentIds);

        Assert.Same(
            result.Workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_OpensRemainingDocuments_WhenSomeDocumentsAreMissing()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Workspace");

        var firstDocumentId = Guid.NewGuid();
        var missingDocumentId = Guid.NewGuid();
        var thirdDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(missingDocumentId);
        workspace.AddDocument(thirdDocumentId);
        workspace.SetLastActiveDocument(thirdDocumentId);

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    firstDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(firstDocumentId));

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    missingDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    thirdDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(thirdDocumentId));

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new OpenWorkspaceCommand(workspace.Id));

        Assert.Equal(
            OpenWorkspaceResultStatus.Activated,
            result.Status);

        Assert.NotNull(result.Workspace);

        Assert.Equal(
            [firstDocumentId, thirdDocumentId],
            result.Workspace!.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.DocumentId));

        Assert.Equal(
            [0, 2],
            result.Workspace.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.Order));

        Assert.Single(result.MissingDocumentIds);

        Assert.Equal(
            missingDocumentId,
            result.MissingDocumentIds.Single());

        Assert.Equal(
            thirdDocumentId,
            result.Workspace.LastActiveDocumentId);

        Assert.Same(
            result.Workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_ClearsLastActiveDocument_WhenLastActiveDocumentIsMissing()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Workspace");

        var firstDocumentId = Guid.NewGuid();
        var missingDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(missingDocumentId);
        workspace.SetLastActiveDocument(missingDocumentId);

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    firstDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(firstDocumentId));

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    missingDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new OpenWorkspaceCommand(workspace.Id));

        Assert.Equal(
            OpenWorkspaceResultStatus.Activated,
            result.Status);

        Assert.NotNull(result.Workspace);

        Assert.Null(
            result.Workspace!.LastActiveDocumentId);

        Assert.Equal(
            missingDocumentId,
            result.MissingDocumentIds.Single());
    }

    [Fact]
    public async Task HandleAsync_PreservesWorkspaceMembershipOrderIncludingGaps()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Ordered Workspace");

        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var thirdDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.AddDocument(thirdDocumentId);

        workspace.RemoveDocument(secondDocumentId);

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    firstDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(firstDocumentId));

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    thirdDocumentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(thirdDocumentId));

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new OpenWorkspaceCommand(workspace.Id));

        Assert.Equal(
            OpenWorkspaceResultStatus.Activated,
            result.Status);

        Assert.Equal(
            [0, 2],
            result.Workspace!.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.Order));

        Assert.Equal(
            [firstDocumentId, thirdDocumentId],
            result.Workspace.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.DocumentId));

        Assert.Empty(result.MissingDocumentIds);
    }

    private static OpenWorkspaceHandler CreateHandler(
        TestContext context)
    {
        return new OpenWorkspaceHandler(
            context.DocumentRepository.Object,
            context.WorkspaceRepository.Object,
            context.ActiveWorkspaceRegistry);
    }

    private static TestContext CreateContext()
    {
        return new TestContext(
            new Mock<IDocumentRepository>(),
            new Mock<IWorkspaceRepository>(),
            new ActiveWorkspaceRegistry());
    }

    private static Document CreateDocument(Guid documentId)
    {
        return Document.Restore(
            documentId,
            "test.txt",
            "Test Document",
            "test-hash",
            "stored-path",
            DateTime.UtcNow,
            DocumentStatus.Imported);
    }

    private sealed record TestContext(
        Mock<IDocumentRepository> DocumentRepository,
        Mock<IWorkspaceRepository> WorkspaceRepository,
        ActiveWorkspaceRegistry ActiveWorkspaceRegistry);
}

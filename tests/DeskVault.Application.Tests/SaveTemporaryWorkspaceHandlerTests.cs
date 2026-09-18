using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class SaveTemporaryWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceIsNotActive()
    {
        var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new SaveTemporaryWorkspaceCommand(
                Guid.NewGuid(),
                "Saved Workspace"));

        Assert.Equal(
            SaveTemporaryWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);
        Assert.Null(result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotTemporary_WhenWorkspaceIsPersistent()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Existing Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new SaveTemporaryWorkspaceCommand(
                workspace.Id,
                "New Name"));

        Assert.Equal(
            SaveTemporaryWorkspaceResultStatus.WorkspaceNotTemporary,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNameRequired_WhenNameIsMissing()
    {
        var context = CreateContext();
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new SaveTemporaryWorkspaceCommand(
                workspace.Id,
                "   "));

        Assert.Equal(
            SaveTemporaryWorkspaceResultStatus.NameRequired,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SavesTemporaryWorkspaceAsPersistent()
    {
        var context = CreateContext();
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        var documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new SaveTemporaryWorkspaceCommand(
                workspace.Id,
                "My Workspace"));

        Assert.Equal(
            SaveTemporaryWorkspaceResultStatus.Success,
            result.Status);

        Assert.NotNull(result.Workspace);
        Assert.Equal(
            WorkspaceType.Persistent,
            result.Workspace!.TypeOfWorkspace);
        Assert.Equal(
            "My Workspace",
            result.Workspace.Name);
        Assert.Equal(
            workspace.Id,
            result.Workspace.Id);

        Assert.Single(result.Workspace.Memberships);
        Assert.Equal(
            documentId,
            result.Workspace.Memberships.Single().DocumentId);
        Assert.Equal(
            0,
            result.Workspace.Memberships.Single().Order);

        context.WorkspaceRepository.Verify(
            repository => repository.AddAsync(
                result.Workspace,
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Same(
            result.Workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_PreservesMembershipOrderAndLastActiveDocument()
    {
        var context = CreateContext();
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());

        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var thirdDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.AddDocument(thirdDocumentId);

        workspace.RemoveDocument(secondDocumentId);
        workspace.SetLastActiveDocument(thirdDocumentId);

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new SaveTemporaryWorkspaceCommand(
                workspace.Id,
                "Ordered Workspace"));

        Assert.Equal(
            SaveTemporaryWorkspaceResultStatus.Success,
            result.Status);

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

        Assert.Equal(
            thirdDocumentId,
            result.Workspace.LastActiveDocumentId);

        Assert.Same(
            result.Workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_DoesNotReplaceActiveWorkspace_WhenPersistenceFails()
    {
        var context = CreateContext();
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());

        workspace.AddDocument(Guid.NewGuid());

        context.ActiveWorkspaceRegistry.Add(workspace);

        context.WorkspaceRepository
            .Setup(
                repository => repository.AddAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Persistence failed."));

        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new SaveTemporaryWorkspaceCommand(
                    workspace.Id,
                    "Workspace")));

        Assert.Same(
            workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));

        Assert.Equal(
            WorkspaceType.Temporary,
            context.ActiveWorkspaceRegistry
                .Get(workspace.Id)!
                .TypeOfWorkspace);
    }

    private static SaveTemporaryWorkspaceHandler CreateHandler(
        TestContext context)
    {
        return new SaveTemporaryWorkspaceHandler(
            context.WorkspaceRepository.Object,
            context.ActiveWorkspaceRegistry);
    }

    private static TestContext CreateContext()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new ActiveWorkspaceRegistry();

        return new TestContext(
            workspaceRepository,
            activeWorkspaceRegistry);
    }

    private sealed record TestContext(
        Mock<IWorkspaceRepository> WorkspaceRepository,
        ActiveWorkspaceRegistry ActiveWorkspaceRegistry);
}

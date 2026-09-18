using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class DeleteWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceIsNotActive()
    {
        var context = CreateContext();
        var handler = CreateHandler(context);

        var workspaceId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspaceId));

        Assert.Equal(
            DeleteWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);

        Assert.Null(result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DeletesTemporaryWorkspace_FromActiveRegistryOnly()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspace.Id));

        Assert.Equal(
            DeleteWorkspaceResultStatus.Success,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        Assert.Null(
            context.ActiveWorkspaceRegistry.Get(workspace.Id));

        context.WorkspaceRepository.Verify(
            repository => repository.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DeletesPersistentWorkspace_FromRepositoryAndActiveRegistry()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Persistent Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspace.Id));

        Assert.Equal(
            DeleteWorkspaceResultStatus.Success,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.DeleteAsync(
                workspace.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Null(
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_DoesNotDeleteOtherActiveWorkspace()
    {
        var context = CreateContext();

        var workspaceToDelete =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Workspace To Delete");

        var remainingWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Remaining Workspace");

        context.ActiveWorkspaceRegistry.Add(workspaceToDelete);
        context.ActiveWorkspaceRegistry.Add(remainingWorkspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspaceToDelete.Id));

        Assert.Equal(
            DeleteWorkspaceResultStatus.Success,
            result.Status);

        Assert.Null(
            context.ActiveWorkspaceRegistry.Get(
                workspaceToDelete.Id));

        Assert.Same(
            remainingWorkspace,
            context.ActiveWorkspaceRegistry.Get(
                remainingWorkspace.Id));

        context.WorkspaceRepository.Verify(
            repository => repository.DeleteAsync(
                workspaceToDelete.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        context.WorkspaceRepository.Verify(
            repository => repository.DeleteAsync(
                remainingWorkspace.Id,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNotRemoveActiveWorkspace_WhenPersistentDeletionFails()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Persistent Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        context.WorkspaceRepository
            .Setup(
                repository => repository.DeleteAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Persistence failed."));

        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new DeleteWorkspaceCommand(workspace.Id)));

        Assert.Same(
            workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_RemovesOnlyWorkspaceState_WhenPersistentWorkspaceHasMemberships()
    {
        var context = CreateContext();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Workspace");

        workspace.AddDocument(Guid.NewGuid());
        workspace.AddDocument(Guid.NewGuid());

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new DeleteWorkspaceCommand(workspace.Id));

        Assert.Equal(
            DeleteWorkspaceResultStatus.Success,
            result.Status);

        Assert.Equal(
            2,
            result.Workspace!.Memberships.Count);

        Assert.Null(
            context.ActiveWorkspaceRegistry.Get(workspace.Id));

        context.WorkspaceRepository.Verify(
            repository => repository.DeleteAsync(
                workspace.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static DeleteWorkspaceHandler CreateHandler(
        TestContext context)
    {
        return new DeleteWorkspaceHandler(
            context.WorkspaceRepository.Object,
            context.ActiveWorkspaceRegistry);
    }

    private static TestContext CreateContext()
    {
        return new TestContext(
            new Mock<IWorkspaceRepository>(),
            new ActiveWorkspaceRegistry());
    }

    private sealed record TestContext(
        Mock<IWorkspaceRepository> WorkspaceRepository,
        ActiveWorkspaceRegistry ActiveWorkspaceRegistry);
}

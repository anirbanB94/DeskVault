using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Tests;

public sealed class CloseWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceIsNotActive()
    {
        var registry = new ActiveWorkspaceRegistry();
        var handler = new CloseWorkspaceHandler(registry);

        var workspaceId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new CloseWorkspaceCommand(workspaceId));

        Assert.Equal(
            CloseWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);
        Assert.Null(result.Workspace);
        Assert.Null(registry.Get(workspaceId));
    }

    [Fact]
    public async Task HandleAsync_ClosesTemporaryWorkspace_AndDiscardsIt()
    {
        var registry = new ActiveWorkspaceRegistry();
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());

        registry.Add(workspace);

        var handler = new CloseWorkspaceHandler(registry);

        var result = await handler.HandleAsync(
            new CloseWorkspaceCommand(workspace.Id));

        Assert.Equal(
            CloseWorkspaceResultStatus.Success,
            result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Null(registry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_ClosesPersistentWorkspace_WithoutDeletingPersistedState()
    {
        var registry = new ActiveWorkspaceRegistry();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Persistent Workspace");

        registry.Add(workspace);

        var handler = new CloseWorkspaceHandler(registry);

        var result = await handler.HandleAsync(
            new CloseWorkspaceCommand(workspace.Id));

        Assert.Equal(
            CloseWorkspaceResultStatus.Success,
            result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Null(registry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_ClosesOnlyRequestedWorkspace()
    {
        var registry = new ActiveWorkspaceRegistry();

        var firstWorkspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        var secondWorkspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        registry.Add(firstWorkspace);
        registry.Add(secondWorkspace);

        var handler = new CloseWorkspaceHandler(registry);

        var result = await handler.HandleAsync(
            new CloseWorkspaceCommand(firstWorkspace.Id));

        Assert.Equal(
            CloseWorkspaceResultStatus.Success,
            result.Status);

        Assert.Null(registry.Get(firstWorkspace.Id));
        Assert.Same(
            secondWorkspace,
            registry.Get(secondWorkspace.Id));
    }

    [Fact]
    public async Task HandleAsync_CanCloseMultipleActiveWorkspacesIndependently()
    {
        var registry = new ActiveWorkspaceRegistry();

        var firstWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "First Workspace");

        var secondWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Second Workspace");

        registry.Add(firstWorkspace);
        registry.Add(secondWorkspace);

        var handler = new CloseWorkspaceHandler(registry);

        var firstResult = await handler.HandleAsync(
            new CloseWorkspaceCommand(firstWorkspace.Id));

        var secondResult = await handler.HandleAsync(
            new CloseWorkspaceCommand(secondWorkspace.Id));

        Assert.Equal(
            CloseWorkspaceResultStatus.Success,
            firstResult.Status);

        Assert.Equal(
            CloseWorkspaceResultStatus.Success,
            secondResult.Status);

        Assert.Empty(registry.GetAll());
    }
}

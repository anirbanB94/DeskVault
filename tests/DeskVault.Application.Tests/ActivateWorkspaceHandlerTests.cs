using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.ActivateWorkspace;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ActivateWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenWorkspaceIsAlreadyActive_ReturnsAlreadyActive()
    {
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Test Workspace");

        var context = CreateContext(workspace);

        var command = new ActivateWorkspaceCommand(workspace.Id);

        ActivateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            ActivateWorkspaceResultStatus.AlreadyActive,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Add(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkspaceExistsAndIsNotActive_ActivatesWorkspace()
    {
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Test Workspace");

        var context = CreateContext();

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var command = new ActivateWorkspaceCommand(workspace.Id);

        ActivateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            ActivateWorkspaceResultStatus.Activated,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Add(workspace),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkspaceDoesNotExist_ReturnsWorkspaceNotFound()
    {
        var context = CreateContext();

        Guid workspaceId = Guid.NewGuid();

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspaceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var command = new ActivateWorkspaceCommand(workspaceId);

        ActivateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            ActivateWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);
        Assert.Null(result.Workspace);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Add(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenActivatingDifferentWorkspaces_AllowsBothToRemainActive()
    {
        var firstWorkspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "First Workspace");

        var secondWorkspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Second Workspace");

        var context = CreateContext();

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    firstWorkspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstWorkspace);

        context.WorkspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    secondWorkspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondWorkspace);

        var firstResult =
            await context.Handler.HandleAsync(
                new ActivateWorkspaceCommand(firstWorkspace.Id));

        var secondResult =
            await context.Handler.HandleAsync(
                new ActivateWorkspaceCommand(secondWorkspace.Id));

        Assert.Equal(
            ActivateWorkspaceResultStatus.Activated,
            firstResult.Status);

        Assert.Equal(
            ActivateWorkspaceResultStatus.Activated,
            secondResult.Status);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Add(firstWorkspace),
            Times.Once);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Add(secondWorkspace),
            Times.Once);
    }

    private static ActivateWorkspaceTestContext CreateContext(
        Workspace? activeWorkspace = null)
    {
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        if (activeWorkspace is not null)
        {
            activeWorkspaceRegistry
                .Setup(
                    registry => registry.Get(activeWorkspace.Id))
                .Returns(activeWorkspace);
        }

        var handler = new ActivateWorkspaceHandler(
            workspaceRepository.Object,
            activeWorkspaceRegistry.Object);

        return new ActivateWorkspaceTestContext(
            handler,
            workspaceRepository,
            activeWorkspaceRegistry);
    }

    private sealed record ActivateWorkspaceTestContext(
        ActivateWorkspaceHandler Handler,
        Mock<IWorkspaceRepository> WorkspaceRepository,
        Mock<IActiveWorkspaceRegistry> ActiveWorkspaceRegistry);
}

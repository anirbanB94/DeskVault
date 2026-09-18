using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class RenameWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceIsNotActive()
    {
        var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new RenameWorkspaceCommand(
                Guid.NewGuid(),
                "Renamed Workspace"));

        Assert.Equal(
            RenameWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);
        Assert.Null(result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotPersistent_WhenWorkspaceIsTemporary()
    {
        var context = CreateContext();
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new RenameWorkspaceCommand(
                workspace.Id,
                "Renamed Workspace"));

        Assert.Equal(
            RenameWorkspaceResultStatus.WorkspaceNotPersistent,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNameRequired_WhenNameIsMissing()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Original Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new RenameWorkspaceCommand(
                workspace.Id,
                "   "));

        Assert.Equal(
            RenameWorkspaceResultStatus.NameRequired,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RenamesPersistentWorkspace()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Original Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new RenameWorkspaceCommand(
                workspace.Id,
                "Renamed Workspace"));

        Assert.Equal(
            RenameWorkspaceResultStatus.Success,
            result.Status);

        Assert.NotNull(result.Workspace);
        Assert.Equal(
            workspace.Id,
            result.Workspace!.Id);
        Assert.Equal(
            "Renamed Workspace",
            result.Workspace.Name);
        Assert.Equal(
            WorkspaceType.Persistent,
            result.Workspace.TypeOfWorkspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                result.Workspace,
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Same(
            result.Workspace,
            context.ActiveWorkspaceRegistry.Get(workspace.Id));
    }

    [Fact]
    public async Task HandleAsync_PreservesMembershipsAndLastActiveDocument()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Original Workspace");

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
            new RenameWorkspaceCommand(
                workspace.Id,
                "Renamed Workspace"));

        Assert.Equal(
            RenameWorkspaceResultStatus.Success,
            result.Status);

        Assert.Equal(
            "Renamed Workspace",
            result.Workspace!.Name);

        Assert.Equal(
            [firstDocumentId, thirdDocumentId],
            result.Workspace.Memberships
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
    }

    [Fact]
    public async Task HandleAsync_DoesNotReplaceActiveWorkspace_WhenPersistenceFails()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Original Workspace");

        context.ActiveWorkspaceRegistry.Add(workspace);

        context.WorkspaceRepository
            .Setup(
                repository => repository.UpdateAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Persistence failed."));

        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new RenameWorkspaceCommand(
                    workspace.Id,
                    "Renamed Workspace")));

        Workspace? activeWorkspace =
            context.ActiveWorkspaceRegistry.Get(workspace.Id);

        Assert.Same(
            workspace,
            activeWorkspace);

        Assert.Equal(
            "Original Workspace",
            activeWorkspace!.Name);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(
            workspace.Id,
            activeWorkspace.Id);
    }

    private static RenameWorkspaceHandler CreateHandler(
        TestContext context)
    {
        return new RenameWorkspaceHandler(
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

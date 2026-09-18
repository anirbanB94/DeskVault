using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class RemoveDocumentFromWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenWorkspaceIsNotActive_ReturnsWorkspaceNotFound()
    {
        var context = CreateContext();

        var command = new RemoveDocumentFromWorkspaceCommand(
            Guid.NewGuid(),
            Guid.NewGuid());

        RemoveDocumentFromWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            RemoveDocumentFromWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);
        Assert.Null(result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIsNotMember_ReturnsDocumentNotMember()
    {
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        var context = CreateContext(workspace);

        var command = new RemoveDocumentFromWorkspaceCommand(
            workspace.Id,
            Guid.NewGuid());

        RemoveDocumentFromWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            RemoveDocumentFromWorkspaceResultStatus.DocumentNotMember,
            result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Empty(workspace.Memberships);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRemovingFromTemporaryWorkspace_RemovesMembershipWithoutPersistence()
    {
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);

        var context = CreateContext(workspace);

        var command = new RemoveDocumentFromWorkspaceCommand(
            workspace.Id,
            documentId);

        RemoveDocumentFromWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            RemoveDocumentFromWorkspaceResultStatus.Success,
            result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Empty(workspace.Memberships);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRemovingLastActiveDocument_ClearsLastActiveDocument()
    {
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);
        workspace.SetLastActiveDocument(documentId);

        var context = CreateContext(workspace);

        var command = new RemoveDocumentFromWorkspaceCommand(
            workspace.Id,
            documentId);

        RemoveDocumentFromWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            RemoveDocumentFromWorkspaceResultStatus.Success,
            result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Empty(workspace.Memberships);
        Assert.Null(workspace.LastActiveDocumentId);
    }

    [Fact]
    public async Task HandleAsync_WhenRemovingFromPersistentWorkspace_PersistsAndReplacesActiveWorkspace()
    {
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Test Workspace");

        Guid documentToKeep = Guid.NewGuid();
        Guid documentToRemove = Guid.NewGuid();

        workspace.AddDocument(documentToKeep);
        workspace.AddDocument(documentToRemove);
        workspace.SetLastActiveDocument(documentToRemove);

        var context = CreateContext(workspace);

        Workspace? persistedWorkspace = null;

        context.WorkspaceRepository
            .Setup(
                repository => repository.UpdateAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>(
                (updatedWorkspace, _) =>
                    persistedWorkspace = updatedWorkspace);

        var command = new RemoveDocumentFromWorkspaceCommand(
            workspace.Id,
            documentToRemove);

        RemoveDocumentFromWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            RemoveDocumentFromWorkspaceResultStatus.Success,
            result.Status);
        Assert.NotNull(result.Workspace);
        Assert.NotSame(workspace, result.Workspace);
        Assert.NotNull(persistedWorkspace);

        Assert.Single(result.Workspace.Memberships);
        Assert.Equal(
            documentToKeep,
            result.Workspace.Memberships.Single().DocumentId);
        Assert.Equal(
            0,
            result.Workspace.Memberships.Single().Order);
        Assert.Null(result.Workspace.LastActiveDocumentId);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(result.Workspace!),
            Times.Once);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_DoesNotReplaceActiveWorkspace()
    {
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Test Workspace");

        Guid documentId = Guid.NewGuid();
        workspace.AddDocument(documentId);

        var context = CreateContext(workspace);

        context.WorkspaceRepository
            .Setup(
                repository => repository.UpdateAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Persistence failed."));

        var command = new RemoveDocumentFromWorkspaceCommand(
            workspace.Id,
            documentId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Handler.HandleAsync(command));

        Assert.Single(workspace.Memberships);
        Assert.Equal(
            documentId,
            workspace.Memberships.Single().DocumentId);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    private static RemoveDocumentTestContext CreateContext(
        Workspace? workspace = null)
    {
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        if (workspace is not null)
        {
            activeWorkspaceRegistry
                .Setup(registry => registry.Get(workspace.Id))
                .Returns(workspace);
        }

        var handler = new RemoveDocumentFromWorkspaceHandler(
            workspaceRepository.Object,
            activeWorkspaceRegistry.Object);

        return new RemoveDocumentTestContext(
            handler,
            workspaceRepository,
            activeWorkspaceRegistry);
    }

    private sealed record RemoveDocumentTestContext(
        RemoveDocumentFromWorkspaceHandler Handler,
        Mock<IWorkspaceRepository> WorkspaceRepository,
        Mock<IActiveWorkspaceRegistry> ActiveWorkspaceRegistry);
}

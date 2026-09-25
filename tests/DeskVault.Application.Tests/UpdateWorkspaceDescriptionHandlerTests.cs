using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class UpdateWorkspaceDescriptionHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceIsNotActive()
    {
        var context = CreateContext();
        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new UpdateWorkspaceDescriptionCommand(
                Guid.NewGuid(),
                "Updated description"));

        Assert.Equal(
            UpdateWorkspaceDescriptionResultStatus.WorkspaceNotFound,
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
            new UpdateWorkspaceDescriptionCommand(
                workspace.Id,
                "Updated description"));

        Assert.Equal(
            UpdateWorkspaceDescriptionResultStatus.WorkspaceNotPersistent,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UpdatesPersistentWorkspaceDescription()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "My Workspace",
            "Original description");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new UpdateWorkspaceDescriptionCommand(
                workspace.Id,
                "Updated description"));

        Assert.Equal(
            UpdateWorkspaceDescriptionResultStatus.Success,
            result.Status);

        Assert.NotNull(result.Workspace);
        Assert.Equal(
            workspace.Id,
            result.Workspace!.Id);
        Assert.Equal(
            "My Workspace",
            result.Workspace.Name);
        Assert.Equal(
            "Updated description",
            result.Workspace.Description);
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
    public async Task HandleAsync_AllowsClearingDescription()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "My Workspace",
            "Original description");

        context.ActiveWorkspaceRegistry.Add(workspace);

        var handler = CreateHandler(context);

        var result = await handler.HandleAsync(
            new UpdateWorkspaceDescriptionCommand(
                workspace.Id,
                "   "));

        Assert.Equal(
            UpdateWorkspaceDescriptionResultStatus.Success,
            result.Status);

        Assert.Null(result.Workspace!.Description);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                result.Workspace,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PreservesNameMembershipsAndLastActiveDocument()
    {
        var context = CreateContext();
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "My Workspace",
            "Original description");

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
            new UpdateWorkspaceDescriptionCommand(
                workspace.Id,
                "Updated description"));

        Assert.Equal(
            "My Workspace",
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
            "My Workspace",
            "Original description");

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
                new UpdateWorkspaceDescriptionCommand(
                    workspace.Id,
                    "Updated description")));

        Workspace? activeWorkspace =
            context.ActiveWorkspaceRegistry.Get(workspace.Id);

        Assert.Same(
            workspace,
            activeWorkspace);

        Assert.Equal(
            "Original description",
            activeWorkspace!.Description);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static UpdateWorkspaceDescriptionHandler CreateHandler(
        TestContext context)
    {
        return new UpdateWorkspaceDescriptionHandler(
            context.WorkspaceRepository.Object,
            context.ActiveWorkspaceRegistry,
            NullLogger<UpdateWorkspaceDescriptionHandler>.Instance);
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

using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class GetWorkspacesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsSuccessWithEmptyCollection_WhenNoWorkspacesExist()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(
                repository => repository.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler =
            new GetWorkspacesHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspacesQuery());

        Assert.Equal(
            GetWorkspacesResultStatus.Success,
            result.Status);

        Assert.Empty(result.Workspaces);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAllPersistedWorkspaces()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var firstWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "First Workspace");

        var secondWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Second Workspace");

        workspaceRepository
            .Setup(
                repository => repository.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [firstWorkspace, secondWorkspace]);

        var handler =
            new GetWorkspacesHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspacesQuery());

        Assert.Equal(
            GetWorkspacesResultStatus.Success,
            result.Status);

        Assert.Equal(
            [firstWorkspace, secondWorkspace],
            result.Workspaces);
    }

    [Fact]
    public async Task HandleAsync_PreservesWorkspaceState()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Workspace");

        var firstDocumentId = Guid.NewGuid();
        var secondDocumentId = Guid.NewGuid();
        var thirdDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.AddDocument(thirdDocumentId);

        workspace.RemoveDocument(secondDocumentId);
        workspace.SetLastActiveDocument(thirdDocumentId);

        workspaceRepository
            .Setup(
                repository => repository.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync([workspace]);

        var handler =
            new GetWorkspacesHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspacesQuery());

        var restoredWorkspace =
            Assert.Single(result.Workspaces);

        Assert.Equal(
            workspace.Id,
            restoredWorkspace.Id);

        Assert.Equal(
            "Workspace",
            restoredWorkspace.Name);

        Assert.Equal(
            WorkspaceType.Persistent,
            restoredWorkspace.TypeOfWorkspace);

        Assert.Equal(
            [firstDocumentId, thirdDocumentId],
            restoredWorkspace.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.DocumentId));

        Assert.Equal(
            [0, 2],
            restoredWorkspace.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.Order));

        Assert.Equal(
            thirdDocumentId,
            restoredWorkspace.LastActiveDocumentId);
    }

    [Fact]
    public async Task HandleAsync_DoesNotModifyActiveWorkspaceRegistry()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        workspaceRepository
            .Setup(
                repository => repository.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler =
            new GetWorkspacesHandler(
                workspaceRepository.Object);

        await handler.HandleAsync(
            new GetWorkspacesQuery());

        activeWorkspaceRegistry.Verify(
            registry => registry.Add(
                It.IsAny<Workspace>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            registry => registry.Remove(
                It.IsAny<Guid>()),
            Times.Never);
    }
}

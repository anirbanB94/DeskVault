using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Queries.GetWorkspace;
using DeskVault.Domain.Workspaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class GetWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsWorkspaceNotFound_WhenWorkspaceDoesNotExist()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspaceId = Guid.NewGuid();

        workspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspaceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var handler =
            new GetWorkspaceHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspaceQuery(workspaceId));

        Assert.Equal(
            GetWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);

        Assert.Null(result.Workspace);
    }

    [Fact]
    public async Task HandleAsync_ReturnsPersistentWorkspace_WhenWorkspaceExists()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Persistent Workspace");

        workspace.AddDocument(Guid.NewGuid());

        workspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var handler =
            new GetWorkspaceHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspaceQuery(workspace.Id));

        Assert.Equal(
            GetWorkspaceResultStatus.Success,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        Assert.Equal(
            "Persistent Workspace",
            result.Workspace!.Name);

        Assert.Single(
            result.Workspace.Memberships);
    }

    [Fact]
    public async Task HandleAsync_PreservesMembershipOrderAndLastActiveDocument()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Ordered Workspace");

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
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var handler =
            new GetWorkspaceHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspaceQuery(workspace.Id));

        Assert.Equal(
            GetWorkspaceResultStatus.Success,
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
    }

    [Fact]
    public async Task HandleAsync_DoesNotModifyActiveWorkspaceRegistry()
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Persistent Workspace");

        workspaceRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    workspace.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var handler =
            new GetWorkspaceHandler(
                workspaceRepository.Object);

        var result = await handler.HandleAsync(
            new GetWorkspaceQuery(workspace.Id));

        Assert.Equal(
            GetWorkspaceResultStatus.Success,
            result.Status);

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

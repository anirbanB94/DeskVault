using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class AddDocumentToWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenWorkspaceIsNotActive_ReturnsWorkspaceNotFound()
    {
        var context = CreateContext();

        var command = new AddDocumentToWorkspaceCommand(
            Guid.NewGuid(),
            Guid.NewGuid());

        AddDocumentToWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            AddDocumentToWorkspaceResultStatus.WorkspaceNotFound,
            result.Status);
        Assert.Null(result.Workspace);

        context.DocumentRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ReturnsDocumentNotFound()
    {
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        var context = CreateContext(workspace);

        Guid documentId = Guid.NewGuid();

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    documentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var command = new AddDocumentToWorkspaceCommand(
            workspace.Id,
            documentId);

        AddDocumentToWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            AddDocumentToWorkspaceResultStatus.DocumentNotFound,
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
    public async Task HandleAsync_WhenDocumentIsAlreadyMember_ReturnsAlreadyMember()
    {
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);

        var context = CreateContext(workspace);

        var command = new AddDocumentToWorkspaceCommand(
            workspace.Id,
            documentId);

        AddDocumentToWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            AddDocumentToWorkspaceResultStatus.AlreadyMember,
            result.Status);
        Assert.Same(workspace, result.Workspace);
        Assert.Single(workspace.Memberships);

        context.DocumentRepository.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAddingToTemporaryWorkspace_AddsMembershipWithoutPersistence()
    {
        var workspace = Workspace.CreateTemporary(Guid.NewGuid());
        var context = CreateContext(workspace);

        Guid documentId = Guid.NewGuid();

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    documentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(documentId));

        var command = new AddDocumentToWorkspaceCommand(
            workspace.Id,
            documentId);

        AddDocumentToWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            AddDocumentToWorkspaceResultStatus.Success,
            result.Status);
        Assert.Same(workspace, result.Workspace);

        var membership = Assert.Single(workspace.Memberships);
        Assert.Equal(documentId, membership.DocumentId);
        Assert.Equal(0, membership.Order);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAddingToPersistentWorkspace_PersistsAndReplacesActiveWorkspace()
    {
        var workspace = Workspace.CreatePersistent(
            Guid.NewGuid(),
            "Test Workspace");

        workspace.AddDocument(Guid.NewGuid());

        var context = CreateContext(workspace);

        Guid documentId = Guid.NewGuid();

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    documentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(documentId));

        Workspace? persistedWorkspace = null;

        context.WorkspaceRepository
            .Setup(
                repository => repository.UpdateAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>(
                (updatedWorkspace, _) =>
                    persistedWorkspace = updatedWorkspace);

        var command = new AddDocumentToWorkspaceCommand(
            workspace.Id,
            documentId);

        AddDocumentToWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        Assert.Equal(
            AddDocumentToWorkspaceResultStatus.Success,
            result.Status);
        Assert.NotNull(result.Workspace);
        Assert.NotSame(workspace, result.Workspace);
        Assert.NotNull(persistedWorkspace);

        Assert.Equal(
            workspace.Id,
            result.Workspace.Id);

        Assert.Equal(
            2,
            result.Workspace.Memberships.Count);

        var memberships =
            result.Workspace.Memberships
                .OrderBy(membership => membership.Order)
                .ToList();

        Assert.Equal(0, memberships[0].Order);
        Assert.Equal(1, memberships[1].Order);
        Assert.Equal(documentId, memberships[1].DocumentId);

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

        var context = CreateContext(workspace);

        Guid documentId = Guid.NewGuid();

        context.DocumentRepository
            .Setup(
                repository => repository.GetByIdAsync(
                    documentId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument(documentId));

        context.WorkspaceRepository
            .Setup(
                repository => repository.UpdateAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Persistence failed."));

        var command = new AddDocumentToWorkspaceCommand(
            workspace.Id,
            documentId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Handler.HandleAsync(command));

        Assert.Same(
            workspace,
            context.ActiveWorkspaceRegistry.Object.Get(workspace.Id));

        Assert.Empty(workspace.Memberships);
    }

    private static AddDocumentTestContext CreateContext(
        Workspace? workspace = null)
    {
        var documentRepository = new Mock<IDocumentRepository>();
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        if (workspace is not null)
        {
            activeWorkspaceRegistry
                .Setup(registry => registry.Get(workspace.Id))
                .Returns(workspace);
        }

        var handler = new AddDocumentToWorkspaceHandler(
            documentRepository.Object,
            workspaceRepository.Object,
            activeWorkspaceRegistry.Object,
            NullLogger<AddDocumentToWorkspaceHandler>.Instance);

        return new AddDocumentTestContext(
            handler,
            documentRepository,
            workspaceRepository,
            activeWorkspaceRegistry);
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

    private sealed record AddDocumentTestContext(
        AddDocumentToWorkspaceHandler Handler,
        Mock<IDocumentRepository> DocumentRepository,
        Mock<IWorkspaceRepository> WorkspaceRepository,
        Mock<IActiveWorkspaceRegistry> ActiveWorkspaceRegistry);
}

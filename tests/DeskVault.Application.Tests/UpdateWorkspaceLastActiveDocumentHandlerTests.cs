using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceLastActiveDocument;
using DeskVault.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class UpdateWorkspaceLastActiveDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenWorkspaceIsNotActive_ReturnsWorkspaceNotFound()
    {
        // Arrange
        TestContext context = CreateContext();

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            Guid.NewGuid(),
            Guid.NewGuid());

        // Act
        UpdateWorkspaceLastActiveDocumentResult result =
            await context.Handler.HandleAsync(
                command,
                Xunit.TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            UpdateWorkspaceLastActiveDocumentResultStatus.WorkspaceNotFound,
            result.Status);

        Assert.Null(result.Workspace);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkspaceIsTemporary_ReturnsWorkspaceNotPersistent()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        TestContext context =
            CreateContext(workspace);

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            Guid.NewGuid());

        // Act
        UpdateWorkspaceLastActiveDocumentResult result =
            await context.Handler.HandleAsync(
                command,
                Xunit.TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            UpdateWorkspaceLastActiveDocumentResultStatus.WorkspaceNotPersistent,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        Assert.Null(
            workspace.LastActiveDocumentId);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIsNotMember_ReturnsDocumentNotMember()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Guid existingDocumentId = Guid.NewGuid();
        Guid nonMemberDocumentId = Guid.NewGuid();

        workspace.AddDocument(existingDocumentId);
        workspace.SetLastActiveDocument(existingDocumentId);

        TestContext context =
            CreateContext(workspace);

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            nonMemberDocumentId);

        // Act
        UpdateWorkspaceLastActiveDocumentResult result =
            await context.Handler.HandleAsync(
                command,
                Xunit.TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            UpdateWorkspaceLastActiveDocumentResultStatus.DocumentNotMember,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        Assert.Equal(
            existingDocumentId,
            workspace.LastActiveDocumentId);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIsAlreadyActive_ReturnsUnchangedWithoutPersistence()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);
        workspace.SetLastActiveDocument(documentId);

        TestContext context =
            CreateContext(workspace);

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            documentId);

        // Act
        UpdateWorkspaceLastActiveDocumentResult result =
            await context.Handler.HandleAsync(
                command,
                Xunit.TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            UpdateWorkspaceLastActiveDocumentResultStatus.Unchanged,
            result.Status);

        Assert.Same(
            workspace,
            result.Workspace);

        Assert.Equal(
            documentId,
            result.Workspace!.LastActiveDocumentId);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistentWorkspaceChangesActiveDocument_PersistsThenReplacesWorkspace()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research",
                "Research description");

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();
        Guid thirdDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.AddDocument(thirdDocumentId);
        workspace.SetLastActiveDocument(firstDocumentId);

        TestContext context =
            CreateContext(workspace);

        List<string> operationOrder = [];

        Workspace? persistedWorkspace = null;

        context.WorkspaceRepository
            .Setup(repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>(
                (updatedWorkspace, _) =>
                {
                    persistedWorkspace = updatedWorkspace;
                    operationOrder.Add("persist");
                })
            .Returns(Task.CompletedTask);

        context.ActiveWorkspaceRegistry
            .Setup(registry => registry.Replace(
                It.IsAny<Workspace>()))
            .Callback<Workspace>(
                _ => operationOrder.Add("replace"));

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            secondDocumentId);

        // Act
        UpdateWorkspaceLastActiveDocumentResult result =
            await context.Handler.HandleAsync(
                command,
                Xunit.TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            UpdateWorkspaceLastActiveDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(result.Workspace);
        Assert.NotSame(workspace, result.Workspace);
        Assert.Same(result.Workspace, persistedWorkspace);

        Assert.Equal(
            workspace.Id,
            result.Workspace!.Id);

        Assert.Equal(
            "Research",
            result.Workspace.Name);

        Assert.Equal(
            "Research description",
            result.Workspace.Description);

        Assert.Equal(
            WorkspaceType.Persistent,
            result.Workspace.TypeOfWorkspace);

        Assert.Equal(
            secondDocumentId,
            result.Workspace.LastActiveDocumentId);

        Assert.Equal(
            firstDocumentId,
            workspace.LastActiveDocumentId);

        Assert.Equal(
            [firstDocumentId, secondDocumentId, thirdDocumentId],
            result.Workspace.Memberships
                .OrderBy(membership => membership.Order)
                .Select(membership => membership.DocumentId));

        Assert.Equal(
            ["persist", "replace"],
            operationOrder);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                result.Workspace,
                Xunit.TestContext.Current.CancellationToken),
            Times.Once);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                result.Workspace),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_DoesNotReplaceActiveWorkspace()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.SetLastActiveDocument(firstDocumentId);

        TestContext context =
            CreateContext(workspace);

        context.WorkspaceRepository
            .Setup(repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Persistence failed."));

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            secondDocumentId);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Handler.HandleAsync(
                command,
                Xunit.TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            firstDocumentId,
            workspace.LastActiveDocumentId);

        Assert.Same(
            workspace,
            context.ActiveWorkspaceRegistry.Object.Get(workspace.Id));

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCancelledDuringRepositoryUpdate_DoesNotPublishWorkspace()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Guid documentId = Guid.NewGuid();
        workspace.AddDocument(documentId);

        TestContext context =
            CreateContext(workspace);

        var updateStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseUpdate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        context.WorkspaceRepository
            .Setup(repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Workspace _, CancellationToken _) =>
            {
                updateStarted.TrySetResult(true);

                // Deliberately ignores cancellation to exercise the handler's
                // post-await cancellation fence.
                await releaseUpdate.Task;
            });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            documentId);

        // Act
        Task<UpdateWorkspaceLastActiveDocumentResult> updateTask =
            context.Handler.HandleAsync(
                command,
                cancellationTokenSource.Token);

        await updateStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            Xunit.TestContext.Current.CancellationToken);

        cancellationTokenSource.Cancel();
        releaseUpdate.TrySetResult(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => updateTask);

        // Assert
        Assert.Null(workspace.LastActiveDocumentId);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);

        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ForwardsCancellationTokenToRepository()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);
        workspace.SetLastActiveDocument(firstDocumentId);

        TestContext context =
            CreateContext(workspace);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        context.WorkspaceRepository
            .Setup(repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(cancellationToken));

        var command = new UpdateWorkspaceLastActiveDocumentCommand(
            workspace.Id,
            secondDocumentId);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Handler.HandleAsync(
                command,
                cancellationToken));

        // Assert
        context.WorkspaceRepository.Verify(
            repository => repository.UpdateAsync(
                It.IsAny<Workspace>(),
                cancellationToken),
            Times.Once);

        context.ActiveWorkspaceRegistry.Verify(
            registry => registry.Replace(
                It.IsAny<Workspace>()),
            Times.Never);

        Assert.Equal(
            firstDocumentId,
            workspace.LastActiveDocumentId);
    }

    private static TestContext CreateContext(
        Workspace? workspace = null)
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        if (workspace is not null)
        {
            activeWorkspaceRegistry
                .Setup(registry => registry.Get(workspace.Id))
                .Returns(workspace);
        }

        var handler =
            new UpdateWorkspaceLastActiveDocumentHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<UpdateWorkspaceLastActiveDocumentHandler>.Instance);

        return new TestContext(
            handler,
            workspaceRepository,
            activeWorkspaceRegistry);
    }

    private sealed record TestContext(
        UpdateWorkspaceLastActiveDocumentHandler Handler,
        Mock<IWorkspaceRepository> WorkspaceRepository,
        Mock<IActiveWorkspaceRegistry> ActiveWorkspaceRegistry);
}

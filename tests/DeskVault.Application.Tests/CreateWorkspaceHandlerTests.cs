using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class CreateWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenCreatingTemporaryWorkspace_ReturnsAndRegistersWorkspaceWithoutPersistence()
    {
        // Arrange
        CreateWorkspaceTestContext context = CreateContext();

        var command = new CreateWorkspaceCommand(
            null,
            null,
            false,
            null);

        // Act
        CreateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        // Assert
        Assert.Equal(
            CreateWorkspaceResultStatus.Success,
            result.Status);

        Workspace workspace =
            Assert.IsType<Workspace>(result.Workspace);

        Assert.Equal(
            WorkspaceType.Temporary,
            workspace.TypeOfWorkspace);

        context.WorkspaceRepository.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        context.Registry.Verify(
            registry =>
                registry.Add(workspace),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCreatingPersistentWorkspace_PersistsAndRegistersWorkspace()
    {
        // Arrange
        CreateWorkspaceTestContext context = CreateContext();

        var command = new CreateWorkspaceCommand(
            "My Workspace",
            "My workspace description.",
            true,
            null);

        // Act
        CreateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        // Assert
        Assert.Equal(
            CreateWorkspaceResultStatus.Success,
            result.Status);

        Workspace workspace =
            Assert.IsType<Workspace>(result.Workspace);

        Assert.Equal(
            WorkspaceType.Persistent,
            workspace.TypeOfWorkspace);

        Assert.Equal(
            "My Workspace",
            workspace.Name);

        Assert.Equal(
            "My workspace description.",
            workspace.Description);

        context.WorkspaceRepository.Verify(
            repository =>
                repository.AddAsync(
                    workspace,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        context.Registry.Verify(
            registry =>
                registry.Add(workspace),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentsExist_AddsMembershipsInInputOrder()
    {
        // Arrange
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        CreateWorkspaceTestContext context =
            CreateContext(
                firstDocumentId,
                secondDocumentId);

        var command = new CreateWorkspaceCommand(
            null,
            null,
            false,
            [
                firstDocumentId,
                secondDocumentId
            ]);

        // Act
        CreateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        // Assert
        Assert.Equal(
            CreateWorkspaceResultStatus.Success,
            result.Status);

        Workspace workspace =
            Assert.IsType<Workspace>(result.Workspace);

        Assert.Collection(
            workspace.Memberships,
            first =>
            {
                Assert.Equal(
                    firstDocumentId,
                    first.DocumentId);

                Assert.Equal(0, first.Order);
            },
            second =>
            {
                Assert.Equal(
                    secondDocumentId,
                    second.DocumentId);

                Assert.Equal(1, second.Order);
            });
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIsMissing_ReturnsDocumentNotFoundAndDoesNotCreateWorkspace()
    {
        // Arrange
        Guid existingDocumentId = Guid.NewGuid();
        Guid missingDocumentId = Guid.NewGuid();

        CreateWorkspaceTestContext context =
            CreateContext(existingDocumentId);

        var command = new CreateWorkspaceCommand(
            null,
            null,
            false,
            [
                existingDocumentId,
                missingDocumentId
            ]);

        // Act
        CreateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        // Assert
        Assert.Equal(
            CreateWorkspaceResultStatus.DocumentNotFound,
            result.Status);

        Assert.Null(result.Workspace);

        Assert.Contains(
            missingDocumentId,
            result.MissingDocumentIds);

        context.WorkspaceRepository.Verify(
            repository =>
                repository.AddAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        context.Registry.Verify(
            registry =>
                registry.Add(It.IsAny<Workspace>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIdsContainDuplicates_CreatesSingleMembershipAndChecksDocumentOnce()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        CreateWorkspaceTestContext context =
            CreateContext(documentId);

        var command = new CreateWorkspaceCommand(
            null,
            null,
            false,
            [
                documentId,
                documentId
            ]);

        // Act
        CreateWorkspaceResult result =
            await context.Handler.HandleAsync(command);

        // Assert
        Assert.Equal(
            CreateWorkspaceResultStatus.Success,
            result.Status);

        Workspace workspace =
            Assert.IsType<Workspace>(result.Workspace);

        Assert.Single(workspace.Memberships);

        context.DocumentRepository.Verify(
            repository =>
                repository.GetByIdAsync(
                    documentId,
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistentWorkspacePersistenceFails_DoesNotRegisterWorkspace()
    {
        // Arrange
        CreateWorkspaceTestContext context = CreateContext();

        context.WorkspaceRepository
            .Setup(repository =>
                repository.AddAsync(
                    It.IsAny<Workspace>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Persistence failed."));

        var command = new CreateWorkspaceCommand(
            "My Workspace",
            "My workspace description.",
            true,
            null);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Handler.HandleAsync(command));

        // Assert
        context.Registry.Verify(
            registry =>
                registry.Add(It.IsAny<Workspace>()),
            Times.Never);
    }

    private static CreateWorkspaceTestContext CreateContext(
        params Guid[] existingDocumentIds)
    {
        var documentRepository =
            new Mock<IDocumentRepository>();

        foreach (Guid documentId in existingDocumentIds)
        {
            documentRepository
                .Setup(repository =>
                    repository.GetByIdAsync(
                        documentId,
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    CreateDocument(documentId));
        }

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var registry =
            new Mock<IActiveWorkspaceRegistry>();

        var handler =
            new CreateWorkspaceHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                registry.Object,
                NullLogger<CreateWorkspaceHandler>.Instance);

        return new CreateWorkspaceTestContext(
            handler,
            documentRepository,
            workspaceRepository,
            registry);
    }

    private static Document CreateDocument(
        Guid documentId)
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

    private sealed record CreateWorkspaceTestContext(
        CreateWorkspaceHandler Handler,
        Mock<IDocumentRepository> DocumentRepository,
        Mock<IWorkspaceRepository> WorkspaceRepository,
        Mock<IActiveWorkspaceRegistry> Registry);
}

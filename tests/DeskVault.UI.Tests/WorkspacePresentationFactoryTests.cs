using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationFactoryTests
{
    [Fact]
    public void Create_PersistentWorkspace_ReturnsPresentationWithSuppliedIdentity()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentationFactory factory =
            CreateFactory();

        WorkspacePresentation presentation =
            factory.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                "Description");

        Assert.Equal(
            workspaceId,
            presentation.WorkspaceId);

        Assert.Equal(
            WorkspaceType.Persistent,
            presentation.WorkspaceType);

        Assert.Equal(
            "Workspace",
            presentation.WorkspaceName);

        Assert.Equal(
            "Description",
            presentation.WorkspaceDescription);

        Assert.False(
            presentation.IsTemporary);
    }

    [Fact]
    public void Create_TemporaryWorkspace_ReturnsTemporaryPresentation()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentationFactory factory =
            CreateFactory();

        WorkspacePresentation presentation =
            factory.Create(
                workspaceId,
                WorkspaceType.Temporary,
                null,
                null);

        Assert.Equal(
            workspaceId,
            presentation.WorkspaceId);

        Assert.Equal(
            WorkspaceType.Temporary,
            presentation.WorkspaceType);

        Assert.True(
            presentation.IsTemporary);

        Assert.Null(
            presentation.WorkspaceName);

        Assert.Null(
            presentation.WorkspaceDescription);
    }

    [Fact]
    public void Create_AllowsNullPersistentDescription()
    {
        WorkspacePresentationFactory factory =
            CreateFactory();

        WorkspacePresentation presentation =
            factory.Create(
                Guid.NewGuid(),
                WorkspaceType.Persistent,
                "Workspace",
                null);

        Assert.Equal(
            "Workspace",
            presentation.WorkspaceName);

        Assert.Null(
            presentation.WorkspaceDescription);
    }

    [Fact]
    public void Constructor_ThrowsWhenDocumentPresentationFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                null!,
                CreateListDocumentsHandler(),
                CreateAddDocumentToWorkspaceHandler(),
                CreateRemoveDocumentFromWorkspaceHandler(),
                CreateRenameWorkspaceHandler(),
                CreateSaveTemporaryWorkspaceHandler(),
                CreateUpdateWorkspaceDescriptionHandler(),
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenListDocumentsHandlerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                null!,
                CreateAddDocumentToWorkspaceHandler(),
                CreateRemoveDocumentFromWorkspaceHandler(),
                CreateRenameWorkspaceHandler(),
                CreateSaveTemporaryWorkspaceHandler(),
                CreateUpdateWorkspaceDescriptionHandler(),
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenAddDocumentToWorkspaceHandlerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                CreateListDocumentsHandler(),
                null!,
                CreateRemoveDocumentFromWorkspaceHandler(),
                CreateRenameWorkspaceHandler(),
                CreateSaveTemporaryWorkspaceHandler(),
                CreateUpdateWorkspaceDescriptionHandler(),
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenRemoveDocumentFromWorkspaceHandlerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                CreateListDocumentsHandler(),
                CreateAddDocumentToWorkspaceHandler(),
                null!,
                CreateRenameWorkspaceHandler(),
                CreateSaveTemporaryWorkspaceHandler(),
                CreateUpdateWorkspaceDescriptionHandler(),
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenRenameWorkspaceHandlerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                CreateListDocumentsHandler(),
                CreateAddDocumentToWorkspaceHandler(),
                CreateRemoveDocumentFromWorkspaceHandler(),
                null!,
                CreateSaveTemporaryWorkspaceHandler(),
                CreateUpdateWorkspaceDescriptionHandler(),
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenSaveTemporaryWorkspaceHandlerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                CreateListDocumentsHandler(),
                CreateAddDocumentToWorkspaceHandler(),
                CreateRemoveDocumentFromWorkspaceHandler(),
                CreateRenameWorkspaceHandler(),
                null!,
                CreateUpdateWorkspaceDescriptionHandler(),
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenUpdateWorkspaceDescriptionHandlerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                CreateListDocumentsHandler(),
                CreateAddDocumentToWorkspaceHandler(),
                CreateRemoveDocumentFromWorkspaceHandler(),
                CreateRenameWorkspaceHandler(),
                CreateSaveTemporaryWorkspaceHandler(),
                null!,
                new Mock<IWorkspaceDialogService>().Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenDialogServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePresentationFactory(
                new Mock<IWorkspaceDocumentPresentationFactory>().Object,
                CreateListDocumentsHandler(),
                CreateAddDocumentToWorkspaceHandler(),
                CreateRemoveDocumentFromWorkspaceHandler(),
                CreateRenameWorkspaceHandler(),
                CreateSaveTemporaryWorkspaceHandler(),
                CreateUpdateWorkspaceDescriptionHandler(),
                null!));
    }

    private static WorkspacePresentationFactory CreateFactory()
    {
        return new WorkspacePresentationFactory(
            new Mock<IWorkspaceDocumentPresentationFactory>().Object,
            CreateListDocumentsHandler(),
            CreateAddDocumentToWorkspaceHandler(),
            CreateRemoveDocumentFromWorkspaceHandler(),
            CreateRenameWorkspaceHandler(),
            CreateSaveTemporaryWorkspaceHandler(),
            CreateUpdateWorkspaceDescriptionHandler(),
            new Mock<IWorkspaceDialogService>().Object);
    }

    private static ListDocumentsHandler CreateListDocumentsHandler()
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                []);

        return new ListDocumentsHandler(
            repository.Object,
            NullLogger<ListDocumentsHandler>.Instance);
    }

    private static AddDocumentToWorkspaceHandler
        CreateAddDocumentToWorkspaceHandler()
    {
        return new AddDocumentToWorkspaceHandler(
            new Mock<IDocumentRepository>().Object,
            new Mock<IWorkspaceRepository>().Object,
            new Mock<IActiveWorkspaceRegistry>().Object,
            NullLogger<AddDocumentToWorkspaceHandler>.Instance);
    }

    private static RemoveDocumentFromWorkspaceHandler
        CreateRemoveDocumentFromWorkspaceHandler()
    {
        return new RemoveDocumentFromWorkspaceHandler(
            new Mock<IWorkspaceRepository>().Object,
            new Mock<IActiveWorkspaceRegistry>().Object,
            NullLogger<RemoveDocumentFromWorkspaceHandler>.Instance);
    }

    private static RenameWorkspaceHandler
        CreateRenameWorkspaceHandler()
    {
        return new RenameWorkspaceHandler(
            new Mock<IWorkspaceRepository>().Object,
            new Mock<IActiveWorkspaceRegistry>().Object,
            NullLogger<RenameWorkspaceHandler>.Instance);
    }

    private static SaveTemporaryWorkspaceHandler
        CreateSaveTemporaryWorkspaceHandler()
    {
        return new SaveTemporaryWorkspaceHandler(
            new Mock<IWorkspaceRepository>().Object,
            new Mock<IActiveWorkspaceRegistry>().Object,
            NullLogger<SaveTemporaryWorkspaceHandler>.Instance);
    }

    private static UpdateWorkspaceDescriptionHandler
        CreateUpdateWorkspaceDescriptionHandler()
    {
        return new UpdateWorkspaceDescriptionHandler(
            new Mock<IWorkspaceRepository>().Object,
            new Mock<IActiveWorkspaceRegistry>().Object,
            NullLogger<UpdateWorkspaceDescriptionHandler>.Instance);
    }
}

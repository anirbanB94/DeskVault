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
using DeskVault.UI.Views;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationManagerTests
{
    [Fact]
    public void Get_ReturnsExistingPresentation()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        WorkspacePresentation created =
            manager.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        WorkspacePresentation? result =
            manager.Get(workspaceId);

        Assert.Same(created, result);
        Assert.Same(presentation, result);
    }

    [Fact]
    public void Get_ReturnsNullWhenPresentationDoesNotExist()
    {
        WorkspacePresentationManager manager =
            CreateManager();

        WorkspacePresentation? result =
            manager.Get(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void GetOrCreate_CreatesPresentationWhenMissing()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                "Description");

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                "Description"))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        WorkspacePresentation result =
            manager.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                "Description");

        Assert.Same(
            presentation,
            result);

        Assert.True(
            manager.Contains(workspaceId));

        factory.Verify(
            x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                "Description"),
            Times.Once);
    }

    [Fact]
    public void GetOrCreate_ReusesExistingPresentationAndActivatesIt()
    {
        Guid workspaceId = Guid.NewGuid();

        var view =
            new Mock<IWorkspacePresentationView>();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        WorkspacePresentation first =
            manager.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        WorkspacePresentation second =
            manager.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        Assert.Same(
            first,
            second);

        factory.Verify(
            x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null),
            Times.Once);

        view.Verify(
            x => x.ActivateWorkspace(),
            Times.Exactly(2));
    }

    [Fact]
    public void Contains_ReturnsTrueOnlyForManagedPresentation()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        Assert.False(
            manager.Contains(workspaceId));

        manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null);

        Assert.True(
            manager.Contains(workspaceId));
    }

    [Fact]
    public void Activate_ReturnsFalseWhenPresentationDoesNotExist()
    {
        WorkspacePresentationManager manager =
            CreateManager();

        bool activated =
            manager.Activate(
                Guid.NewGuid());

        Assert.False(activated);
    }

    [Fact]
    public void Activate_ReturnsTrueForManagedPresentation()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null);

        bool activated =
            manager.Activate(workspaceId);

        Assert.True(activated);
    }

    [Fact]
    public async Task FindTemporaryByDocument_ReturnsMatchingTemporaryPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Temporary,
                null,
                null);

        await presentation.SetWorkspaceDocumentsAsync(
            []);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Temporary,
                null,
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Temporary,
            null,
            null);

        Assert.Null(
            manager.FindTemporaryByDocument(documentId));
    }

    [Fact]
    public void FindTemporaryByDocument_DoesNotReturnPersistentPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null);

        Assert.Null(
            manager.FindTemporaryByDocument(documentId));
    }

    [Fact]
    public void Close_ReturnsFalseWhenPresentationDoesNotExist()
    {
        WorkspacePresentationManager manager =
            CreateManager();

        bool closed =
            manager.Close(
                Guid.NewGuid());

        Assert.False(closed);
    }

    [Fact]
    public void Close_RemovesManagedPresentation()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentation presentation =
            CreatePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null))
            .Returns(presentation);

        WorkspacePresentationManager manager =
            new(factory.Object);

        manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null);

        bool closed =
            manager.Close(workspaceId);

        Assert.True(closed);
        Assert.False(manager.Contains(workspaceId));
        Assert.Null(manager.Get(workspaceId));

        Assert.Throws<ObjectDisposedException>(
            () => presentation.Activate());
    }

    [Fact]
    public void Dispose_DisposesAllManagedPresentations()
    {
        Guid firstWorkspaceId = Guid.NewGuid();
        Guid secondWorkspaceId = Guid.NewGuid();

        WorkspacePresentation first =
            CreatePresentation(
                firstWorkspaceId,
                WorkspaceType.Persistent,
                "First",
                null);

        WorkspacePresentation second =
            CreatePresentation(
                secondWorkspaceId,
                WorkspaceType.Persistent,
                "Second",
                null);

        var factory =
            new Mock<IWorkspacePresentationFactory>();

        factory
            .Setup(x => x.Create(
                firstWorkspaceId,
                WorkspaceType.Persistent,
                "First",
                null))
            .Returns(first);

        factory
            .Setup(x => x.Create(
                secondWorkspaceId,
                WorkspaceType.Persistent,
                "Second",
                null))
            .Returns(second);

        WorkspacePresentationManager manager =
            new(factory.Object);

        manager.GetOrCreate(
            firstWorkspaceId,
            WorkspaceType.Persistent,
            "First",
            null);

        manager.GetOrCreate(
            secondWorkspaceId,
            WorkspaceType.Persistent,
            "Second",
            null);

        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => first.Activate());

        Assert.Throws<ObjectDisposedException>(
            () => second.Activate());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        WorkspacePresentationManager manager =
            CreateManager();

        manager.Dispose();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => manager.Contains(Guid.NewGuid()));
    }

    [Fact]
    public void OperationsAfterDispose_ThrowObjectDisposedException()
    {
        WorkspacePresentationManager manager =
            CreateManager();

        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => manager.Get(Guid.NewGuid()));

        Assert.Throws<ObjectDisposedException>(
            () => manager.FindTemporaryByDocument(Guid.NewGuid()));

        Assert.Throws<ObjectDisposedException>(
            () => manager.GetOrCreate(
                Guid.NewGuid(),
                WorkspaceType.Persistent,
                "Workspace",
                null));

        Assert.Throws<ObjectDisposedException>(
            () => manager.Contains(Guid.NewGuid()));

        Assert.Throws<ObjectDisposedException>(
            () => manager.Activate(Guid.NewGuid()));

        Assert.Throws<ObjectDisposedException>(
            () => manager.Close(Guid.NewGuid()));
    }

    private static WorkspacePresentationManager CreateManager()
    {
        return new WorkspacePresentationManager(
            new Mock<IWorkspacePresentationFactory>().Object);
    }

    private static WorkspacePresentation CreatePresentation(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description,
        Mock<IWorkspacePresentationView>? view = null)
    {
        view ??=
            new Mock<IWorkspacePresentationView>();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<DeskVault.Domain.Documents.Document>());

        var listDocumentsHandler =
            new ListDocumentsHandler(
                documentRepository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        var addDocumentToWorkspaceHandler =
            new AddDocumentToWorkspaceHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<AddDocumentToWorkspaceHandler>.Instance);

        var removeDocumentFromWorkspaceHandler =
            new RemoveDocumentFromWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<RemoveDocumentFromWorkspaceHandler>.Instance);

        var renameWorkspaceHandler =
            new RenameWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<RenameWorkspaceHandler>.Instance);

        var saveTemporaryWorkspaceHandler =
            new SaveTemporaryWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<SaveTemporaryWorkspaceHandler>.Instance);

        var updateWorkspaceDescriptionHandler =
            new UpdateWorkspaceDescriptionHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<UpdateWorkspaceDescriptionHandler>.Instance);

        return new WorkspacePresentation(
            workspaceId,
            workspaceType,
            workspaceName,
            description,
            documentPresentationFactory.Object,
            listDocumentsHandler,
            addDocumentToWorkspaceHandler,
            removeDocumentFromWorkspaceHandler,
            renameWorkspaceHandler,
            saveTemporaryWorkspaceHandler,
            updateWorkspaceDescriptionHandler,
            dialogService.Object,
            view.Object,
            NullLogger<WorkspacePresentation>.Instance);
    }
}

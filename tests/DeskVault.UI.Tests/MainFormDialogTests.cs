using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class MainFormDialogTests
{
    [Fact]
    public void ProtectedMessagesAndConfirmations_UseSessionBoundCoordinator()
    {
        const long expectedSessionRevision = 29;

        List<string> dialogTitles = [];

        var coordinator =
            new Mock<ISessionBoundDialogCoordinator>();

        coordinator
            .Setup(x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedSessionRevision,
                It.IsAny<IWin32Window?>()))
            .Callback<Form, long, IWin32Window?>(
                (dialog, _, _) =>
                    dialogTitles.Add(dialog.Text))
            .Returns(DialogResult.Yes);

        using MainForm form =
            CreateMainForm(coordinator.Object);

        form.ShowInformation(
            "Information message",
            "Information title",
            expectedSessionRevision);

        form.ShowWarning(
            "Warning message",
            "Warning title",
            expectedSessionRevision);

        form.ShowError(
            "Error message",
            "Error title",
            expectedSessionRevision);

        bool documentRemovalConfirmed =
            form.ConfirmRemoval(
                "document.csv",
                expectedSessionRevision);

        bool workspaceRemovalConfirmed =
            form.ConfirmWorkspaceRemoval(
                "Archive",
                expectedSessionRevision);

        Assert.True(documentRemovalConfirmed);
        Assert.True(workspaceRemovalConfirmed);

        Assert.Equal(
            [
                "Information title",
                "Warning title",
                "Error title",
                UiMessages.RemoveDocumentTitle,
                UiMessages.DeleteWorkspace
            ],
            dialogTitles);

        coordinator.Verify(
            x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedSessionRevision,
                form),
            Times.Exactly(5));

        form.Close();
    }

    private static MainForm CreateMainForm(
        ISessionBoundDialogCoordinator dialogCoordinator)
    {
        var applicationInfo =
            new Mock<IApplicationInfoService>();

        applicationInfo
            .SetupGet(x => x.ApplicationName)
            .Returns("DeskVault");

        applicationInfo
            .SetupGet(x => x.Version)
            .Returns("1.0");

        var documentRepository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        var storageService =
            new Mock<IStorageService>();

        var hashService =
            new Mock<IHashService>();

        var importValidator =
            new Mock<IImportDocumentValidator>();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var processingService =
            new Mock<IDocumentProcessingService>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        var importDocumentHandler =
            new ImportDocumentHandler(
                importValidator.Object,
                hashService.Object,
                storageService.Object,
                documentRepository.Object,
                NullLogger<ImportDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var listDocumentsHandler =
            new ListDocumentsHandler(
                documentRepository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        var searchDocumentsRetriever =
            new SearchDocumentsRetriever(
                searchStore.Object,
                new SearchDocumentsRanker(),
                new SearchDocumentsContinuationCodec(),
                NullLogger<SearchDocumentsRetriever>.Instance);

        var searchDocumentsHandler =
            new SearchDocumentsHandler(
                searchDocumentsRetriever);

        var createWorkspaceHandler =
            new CreateWorkspaceHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<CreateWorkspaceHandler>.Instance);

        var deleteWorkspaceHandler =
            new DeleteWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<DeleteWorkspaceHandler>.Instance);

        var getWorkspacesHandler =
            new GetWorkspacesHandler(
                workspaceRepository.Object);

        var openWorkspaceHandler =
            new OpenWorkspaceHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<OpenWorkspaceHandler>.Instance);

        var closeWorkspaceHandler =
            new CloseWorkspaceHandler(
                activeWorkspaceRegistry.Object,
                NullLogger<CloseWorkspaceHandler>.Instance);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        DocumentTextExtractorResolver extractorResolver =
            new([]);

        var presenterFactory =
            new MainFormPresenterFactory(
                importDocumentHandler,
                removeDocumentHandler,
                listDocumentsHandler,
                searchDocumentsHandler,
                createWorkspaceHandler,
                deleteWorkspaceHandler,
                getWorkspacesHandler,
                openWorkspaceHandler,
                closeWorkspaceHandler,
                workspacePresentationManager.Object,
                processingService.Object,
                extractorResolver,
                NullLogger<MainFormPresenter>.Instance,
                Options.Create(
                    new SearchOptions
                    {
                        PageSize = SearchOptions.DefaultPageSize
                    }),
                securitySession.Object);

        return new MainForm(
            applicationInfo.Object,
            presenterFactory,
            NullLogger<MainForm>.Instance,
            dialogCoordinator);
    }
}

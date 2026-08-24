using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class MainFormPresenterTests
{
    [Fact]
    public async Task SearchRequested_MatchingChunks_DisplaysUniqueDocuments()
    {
        Guid firstDocumentId =
            Guid.NewGuid();

        Guid secondDocumentId =
            Guid.NewGuid();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                "security",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateSearchResults(
                    firstDocumentId,
                    secondDocumentId));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("security");

        var documentWorkspace =
            new Mock<IDocumentWorkspace>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                documentWorkspace);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                "security",
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    documents =>
                        documents.Count == 2 &&
                        documents[0].Id == firstDocumentId &&
                        documents[0].FileName == "security-policy.md" &&
                        documents[1].Id == secondDocumentId &&
                        documents[1].FileName == "incident-response.md")),
            Times.Once);
    }

    private static IReadOnlyList<SearchDocumentsResult> CreateSearchResults(
        Guid firstDocumentId,
        Guid secondDocumentId)
    {
        return
        [
            new SearchDocumentsResult(
                firstDocumentId,
                "security-policy.md",
                "Security Policy",
                0,
                "Security policy introduction."),

            new SearchDocumentsResult(
                firstDocumentId,
                "security-policy.md",
                "Security Policy",
                2,
                "Security retention requirements."),

            new SearchDocumentsResult(
                secondDocumentId,
                "incident-response.md",
                "Incident Response",
                1,
                "Security incident response procedure.")
        ];
    }

    private static MainFormPresenter CreatePresenter(
        Mock<IMainFormView> view,
        Mock<IDocumentSearchStore> searchStore,
        Mock<IDocumentWorkspace> documentWorkspace)
    {
        var repository =
            new Mock<IDocumentRepository>();

        var storageService =
            new Mock<IStorageService>();

        var hashService =
            new Mock<IHashService>();

        var importValidator =
            new Mock<IImportDocumentValidator>();

        var documentReader =
            new Mock<IDocumentReader>();

        var processingService =
            new Mock<IDocumentProcessingService>();

        var importDocumentHandler =
            new ImportDocumentHandler(
                importValidator.Object,
                hashService.Object,
                storageService.Object,
                repository.Object,
                processingService.Object,
                NullLogger<ImportDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var openDocumentHandler =
            new OpenDocumentHandler(
                repository.Object,
                documentReader.Object,
                NullLogger<OpenDocumentHandler>.Instance);

        var listDocumentsHandler =
            new ListDocumentsHandler(
                repository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        var searchDocumentsHandler =
            new SearchDocumentsHandler(
                searchStore.Object,
                NullLogger<SearchDocumentsHandler>.Instance);

        return new MainFormPresenter(
            view.Object,
            importDocumentHandler,
            removeDocumentHandler,
            openDocumentHandler,
            listDocumentsHandler,
            searchDocumentsHandler,
            documentWorkspace.Object,
            NullLogger<MainFormPresenter>.Instance);
    }

    private static async Task WaitForBackgroundOperationAsync()
    {
        await Task.Delay(100);
    }
}

using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
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

    [Fact]
    public async Task SearchRequested_NoMatches_ShowsEmptyStateAndDisablesDocumentActions()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                "unknown",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                []);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("unknown");

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
                "unknown",
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowEmptyState(),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(false),
            Times.Once);

        view.Verify(
            x => x.SetRemoveEnabled(false),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchRequested_SearchStoreFails_ShowsErrorAndFailureStatus()
    {
        const string searchText = "security";
        const string errorMessage = "Search store failure.";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                searchText,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    errorMessage));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

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
                searchText,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                $"Search failed: {errorMessage}"),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message =>
                        message.Contains(
                            errorMessage,
                            StringComparison.Ordinal)),
                "DeskVault"),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowEmptyState(),
            Times.Never);
    }

    [Fact]
    public async Task OpenRequested_SelectedDocument_OpensDocumentInWorkspace()
    {
        Guid documentId =
            Guid.NewGuid();

        const string fileName =
            "security-policy.md";

        const string content =
            "# Security Policy";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        var documentWorkspace =
            new Mock<IDocumentWorkspace>();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Document.Create(
                    documentId,
                    fileName,
                    "Security Policy",
                    "test-hash",
                    "document.dvault"));

        var documentReader =
            new Mock<IDocumentReader>();

        documentReader
            .Setup(x => x.OpenReadAsync(
                "document.dvault",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateContentStream(
                    content));

        _ =
            CreatePresenter(
                view,
                searchStore,
                documentWorkspace,
                repository,
                documentReader);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        documentReader.Verify(
            x => x.OpenReadAsync(
                "document.dvault",
                It.IsAny<CancellationToken>()),
            Times.Once);

        documentWorkspace.Verify(
            x => x.OpenAsync(
                documentId,
                It.Is<Stream>(
                    stream =>
                        ReadStream(stream) == content),
                fileName),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.DocumentOpenedStatus),
            Times.Once);
    }

    [Fact]
    public async Task OpenRequested_WithoutSelection_DoesNothing()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns((Guid?)null);

        var documentWorkspace =
            new Mock<IDocumentWorkspace>();

        var repository =
            new Mock<IDocumentRepository>();

        var documentReader =
            new Mock<IDocumentReader>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                documentWorkspace,
                repository,
                documentReader);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        documentReader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        documentWorkspace.Verify(
            x => x.OpenAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenRequested_WhenOpeningFails_ShowsErrorAndFailureStatus()
    {
        Guid documentId =
            Guid.NewGuid();

        const string errorMessage =
            "Unable to read document.";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        var documentWorkspace =
            new Mock<IDocumentWorkspace>();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Document.Create(
                    documentId,
                    "security-policy.md",
                    "Security Policy",
                    "test-hash",
                    "document.dvault"));

        var documentReader =
            new Mock<IDocumentReader>();

        documentReader
            .Setup(x => x.OpenReadAsync(
                "document.dvault",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    errorMessage));

        _ =
            CreatePresenter(
                view,
                searchStore,
                documentWorkspace,
                repository,
                documentReader);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToOpenDocumentStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle),
            Times.Once);

        documentWorkspace.Verify(
            x => x.OpenAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.SetOpenEnabled(
                It.IsAny<bool>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ImportRequested_WhenDocumentIsDuplicate_ShowsWarningAndDoesNotRefreshDocuments()
    {
        const string filePath =
            @"C:\Documents\security-policy.md";

        const string duplicateHash =
            "duplicate-hash";

        const string duplicateDescription =
            "The document has already been imported.";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedFilePath)
            .Returns(filePath);

        var documentWorkspace =
            new Mock<IDocumentWorkspace>();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                duplicateHash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicateHash);

        var importValidator =
            CreateSuccessfulImportValidator();

        _ =
            CreatePresenter(
                view,
                searchStore,
                documentWorkspace,
                repository,
                hashService: hashService,
                importValidator: importValidator);

        view.Raise(
            x => x.ImportRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        hashService.Verify(
            x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.ExistsByHashAsync(
                duplicateHash,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                duplicateDescription),
            Times.Once);

        view.Verify(
            x => x.ShowWarning(
                duplicateDescription,
                UiMessages.ImportFailedTitle),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.SetImportEnabled(true),
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

    private static Mock<IImportDocumentValidator>
        CreateSuccessfulImportValidator()
    {
        var validator =
            new Mock<IImportDocumentValidator>();

        validator
            .Setup(x => x.Validate(
                It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        return validator;
    }

    private static MainFormPresenter CreatePresenter(
        Mock<IMainFormView> view,
        Mock<IDocumentSearchStore> searchStore,
        Mock<IDocumentWorkspace> documentWorkspace,
        Mock<IDocumentRepository>? repository = null,
        Mock<IDocumentReader>? documentReader = null,
        Mock<IHashService>? hashService = null,
        Mock<IImportDocumentValidator>? importValidator = null)
    {
        repository ??=
            new Mock<IDocumentRepository>();

        var storageService =
            new Mock<IStorageService>();

        hashService ??=
            new Mock<IHashService>();

        importValidator ??=
            CreateSuccessfulImportValidator();

        documentReader ??=
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

    private static MemoryStream CreateContentStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content));
    }

    private static string ReadStream(
        Stream stream)
    {
        stream.Position = 0;

        using var reader =
            new StreamReader(
                stream,
                leaveOpen: true);

        return reader.ReadToEnd();
    }

    private static async Task WaitForBackgroundOperationAsync()
    {
        await Task.Delay(100);
    }
}

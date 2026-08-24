using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class DocumentWorkspacePresenterTests
{
    [Fact]
    public async Task OpenAsync_SupportedDocument_ShowsDocument()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        view.Verify(
            x => x.ShowDocumentAsync(
                stream,
                "document.csv",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenAsync_UnsupportedDocument_ShowsUnsupportedPreview()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.pdf");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                "document.pdf",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new NotSupportedException());

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "unsupported document"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.pdf");

        view.Verify(
            x => x.ShowUnsupportedPreview(
                UiMessages.UnsupportedDocumentPreviewMessage),
            Times.Once);
    }

    [Fact]
    public async Task OpenAsync_Cancellation_PropagatesCancellation()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                "document.csv",
                cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationToken));

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                presenter.OpenAsync(
                    documentId,
                    stream,
                    "document.csv",
                    cancellationToken));
    }

    [Fact]
    public async Task OpenAsync_UnexpectedRendererFailure_ShowsError()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                "document.csv",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "The document could not be rendered."));

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.DeskVaultTitle),
            Times.Once);
    }

    [Fact]
    public async Task OpenExternally_WithCurrentDocument_ResetsStreamAndOpensDocument()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        stream.Position = stream.Length;

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await Task.Delay(100);

        Assert.Equal(
            0,
            stream.Position);

        documentViewer.Verify(
            x => x.OpenAsync(
                stream,
                "document.csv"),
            Times.Once);
    }

    [Fact]
    public async Task OpenExternally_WithoutCurrentDocument_DoesNothing()
    {
        var repository =
            new Mock<IDocumentRepository>();

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        _ =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await Task.Delay(100);

        documentViewer.Verify(
            x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenExternally_WhenViewerFails_ShowsError()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                "document.csv"))
            .ThrowsAsync(
                new IOException(
                    "Unable to open document."));

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await Task.Delay(100);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle),
            Times.Once);
    }

    [Fact]
    public async Task RemoveDocument_WhenUserCancels_DoesNothing()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(
                "document.csv"))
            .Returns(false);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await Task.Delay(100);

        view.Verify(
            x => x.ConfirmRemoval(
                "document.csv"),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.CloseWorkspace(),
            Times.Never);
    }

    [Fact]
    public async Task RemoveDocument_Success_DisposesStreamClosesWorkspaceAndRaisesEvent()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        repository
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.StoredFilePath,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(
                "document.csv"))
            .Returns(true);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        bool documentRemovedRaised =
            false;

        presenter.DocumentRemoved +=
            (_, _) =>
            {
                documentRemovedRaised = true;
            };

        var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await Task.Delay(100);

        Assert.False(
            stream.CanRead);

        storageService.Verify(
            x => x.DeleteAsync(
                document.StoredFilePath,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.CloseWorkspace(),
            Times.Once);

        Assert.True(
            documentRemovedRaised);
    }

    [Fact]
    public async Task RemoveDocument_StorageDeletionFails_ShowsError()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.StoredFilePath,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Storage deletion failed."));

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(
                "document.csv"))
            .Returns(true);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "document.csv");

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await Task.Delay(100);

        view.Verify(
            x => x.ShowError(
                "Storage deletion failed.",
                UiMessages.RemoveFailedTitle),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.CloseWorkspace(),
            Times.Never);
    }

    [Fact]
    public async Task DocumentInformation_WithCurrentDocument_ShowsInformation()
    {
        Guid documentId =
            Guid.NewGuid();

        var document =
            CreateDocument(
                documentId,
                "Report.CSV");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await presenter.OpenAsync(
            documentId,
            stream,
            "Report.CSV");

        view.Raise(
            x => x.DocumentInformationRequested += null,
            EventArgs.Empty);

        view.Verify(
            x => x.ShowDocumentInformation(
                document.DisplayName,
                document.FileName,
                "CSV",
                document.ImportedAt,
                document.Status.ToString(),
                document.Sha256Hash),
            Times.Once);
    }

    [Fact]
    public async Task DocumentInformation_WithoutCurrentDocument_DoesNothing()
    {
        var repository =
            new Mock<IDocumentRepository>();

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        _ =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        view.Raise(
            x => x.DocumentInformationRequested += null,
            EventArgs.Empty);

        view.Verify(
            x => x.ShowDocumentInformation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    private static DocumentWorkspacePresenter CreatePresenter(
        Mock<IDocumentWorkspaceView> view,
        Mock<IDocumentViewer> documentViewer,
        Mock<IDocumentRepository> repository,
        Mock<IStorageService> storageService)
    {
        return new DocumentWorkspacePresenter(
            view.Object,
            documentViewer.Object,
            new GetDocumentHandler(
                repository.Object,
                NullLogger<GetDocumentHandler>.Instance),
            new RemoveDocumentHandler(
                repository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance),
            NullLogger<DocumentWorkspacePresenter>.Instance);
    }

    private static Document CreateDocument(
        Guid id,
        string fileName)
    {
        return Document.Create(
            id,
            fileName,
            Path.GetFileNameWithoutExtension(fileName),
            "test-sha256",
            $"Data\\{id}\\{fileName}");
    }
}

using DeskVault.Application.Configurations;
using DeskVault.Application.Documents;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Documents;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Tests.TestDoubles;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.UI.Tests;

public sealed class DocumentWorkspacePresenterTests
{
    [Fact]
    public async Task OpenAsync_SupportedDocument_ShowsDocument()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Verify(
            x => x.ShowDocumentAsync(
                stream,
                document.FileName,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenAsync_UnsupportedDocument_ShowsUnsupportedPreview()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.pdf");

        var repository =
            CreateRepository(
                document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

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

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

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
                document.FileName,
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
            CreateDocumentStream();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                presenter.OpenAsync(
                    documentId,
                    stream,
                    document.FileName,
                    cancellationToken));
    }

    [Fact]
    public async Task OpenAsync_UnexpectedRendererFailure_ShowsError()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "PBI232_DOCUMENT_WORKSPACE_OPEN_SENTINEL"));

        var documentViewer =
            new Mock<IDocumentViewer>();

        var logger = new RecordingLogger<DocumentWorkspacePresenter>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                logger);

        using var stream =
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.DeskVaultTitle),
            Times.Once);

        AssertSafeFailureLog(
            logger,
            DiagnosticOperationCodes.DocumentWorkspaceOpenFailure,
            LogMessages.DocumentWorkspaceOpenFailed,
            "PBI232_DOCUMENT_WORKSPACE_OPEN_SENTINEL");
    }

    [Fact]
    public async Task OpenExternally_WithCurrentDocument_ResetsStreamAndOpensDocument()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        stream.Position =
            stream.Length;

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        Assert.Equal(
            0,
            stream.Position);

        documentViewer.Verify(
            x => x.OpenAsync(
                stream,
                document.FileName),
            Times.Once);
    }

    [Theory]
    [InlineData("document.html")]
    [InlineData("document.htm")]
    public async Task OpenExternally_HtmlDocument_UsesExternalViewer(
        string fileName)
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                fileName);

        var repository =
            CreateRepository(
                document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
        .Setup(x => x.ShowDocumentAsync(
            It.IsAny<Stream>(),
            document.FileName,
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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Verify(
            x => x.ShowUnsupportedPreview(
                UiMessages.UnsupportedDocumentPreviewMessage),
            Times.Once);

        DocumentFormatCapability capability =
            SupportedFileTypes.GetForExtension(
                Path.GetExtension(fileName))
            ?? throw new InvalidOperationException(
                $"No capability declaration exists for '{fileName}'.");

        Assert.Equal(
            DocumentTextExtractionKind.None,
            capability.TextExtraction);

        Assert.Equal(
            DocumentInAppPreviewKind.None,
            capability.InAppPreview);

        Assert.True(
            capability.SupportsExternalViewFallback);

        stream.Position =
            stream.Length;

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        Assert.Equal(
            0,
            stream.Position);

        documentViewer.Verify(
            x => x.OpenAsync(
                stream,
                document.FileName),
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

        await WaitForBackgroundOperationAsync();

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

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                document.FileName))
            .ThrowsAsync(
                new IOException(
                    "PBI232_DOCUMENT_WORKSPACE_EXTERNAL_OPEN_SENTINEL"));

        var logger = new RecordingLogger<DocumentWorkspacePresenter>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                logger);

        using var stream =
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle),
            Times.Once);

        AssertSafeFailureLog(
            logger,
            DiagnosticOperationCodes.DocumentWorkspaceOpenExternallyFailure,
            LogMessages.DocumentWorkspaceOpenExternallyFailed,
            "PBI232_DOCUMENT_WORKSPACE_EXTERNAL_OPEN_SENTINEL");
    }

    [Fact]
    public async Task RemoveDocument_WhenUserCancels_DoesNothing()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(
                document.FileName))
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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ConfirmRemoval(
                document.FileName),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
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
    public async Task RemoveDocument_Success_DisposesStreamKeepsWorkspaceOpenAndRaisesEvent()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

        repository
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(
                document.FileName))
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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        Assert.False(
            stream.CanRead);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.CloseWorkspace(),
            Times.Never);

        Assert.True(
            documentRemovedRaised);
    }

    [Fact]
    public async Task RemoveDocument_WhenDisposedDuringRemoval_DoesNotRaiseDocumentRemoved()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var deleteStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var deleteRelease =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => deleteStarted.TrySetResult(true))
            .Returns(deleteRelease.Task);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(document.FileName))
            .Returns(true);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        int documentRemovedCount = 0;

        presenter.DocumentRemoved +=
            (_, _) => documentRemovedCount++;

        MemoryStream stream = CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await deleteStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presenter.Dispose();

        Assert.False(stream.CanRead);

        deleteRelease.TrySetResult();

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, documentRemovedCount);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveDocument_WhenDisposedDuringRemovalFailure_DoesNotShowError()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var removalLookupStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var removalLookup =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(document.FileName))
            .Returns(true);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var logger =
            new RecordingLogger<DocumentWorkspacePresenter>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                logger);

        using var stream = CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        // Replace the repository response for the subsequent removal operation.
        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => removalLookupStarted.TrySetResult(true))
            .Returns(removalLookup.Task);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await removalLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presenter.Dispose();

        Assert.False(stream.CanRead);

        removalLookup.TrySetException(
            new InvalidOperationException(
                "PBI320_DOCUMENT_REMOVE_AFTER_DISPOSE_SENTINEL"));

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RemoveDocument_StorageDeletionFails_ShowsError()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            CreateRepository(
                document);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Storage deletion failed."));

        var view =
            new Mock<IDocumentWorkspaceView>();

        view
            .Setup(x => x.ConfirmRemoval(
                document.FileName))
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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        // Act
        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        // Assert
        view.Verify(
            x => x.ShowError(
                "The document could not be removed because its stored artifact could not be deleted.",
                UiMessages.RemoveFailedTitle),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
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
    public async Task RemoveDocument_WhenConfirmationThrows_LogsSafeFailureAndShowsError()
    {
        Guid documentId = Guid.NewGuid();
        Document document = CreateDocument(documentId, "document.csv");

        var repository = CreateRepository(document);
        var storageService = new Mock<IStorageService>();
        var view = new Mock<IDocumentWorkspaceView>();

        const string sentinel = "PBI232_DOCUMENT_WORKSPACE_REMOVE_SENTINEL";

        view
            .Setup(x => x.ConfirmRemoval(document.FileName))
            .Throws(new IOException(sentinel));

        var documentViewer = new Mock<IDocumentViewer>();
        var logger = new RecordingLogger<DocumentWorkspacePresenter>();

        var presenter = CreatePresenter(
            view,
            documentViewer,
            repository,
            storageService,
            logger);

        using var stream = CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle),
            Times.Once);

        AssertSafeFailureLog(
            logger,
            DiagnosticOperationCodes.DocumentWorkspaceRemovalConfirmationFailure,
            LogMessages.DocumentWorkspaceRemovalConfirmationFailed,
            sentinel);

        storageService.Verify(
            x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DocumentInformation_WithCurrentDocument_ShowsInformation()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "Report.CSV");

        var repository =
            CreateRepository(
                document);

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
            CreateDocumentStream();

        await presenter.OpenAsync(documentId, stream, document.FileName, TestContext.Current.CancellationToken);

        view.Raise(
            x => x.DocumentInformationRequested += null,
            EventArgs.Empty);

        view.Verify(
            x => x.ShowDocumentInformation(
                document.DisplayName,
                document.FileName,
                "CSV",
                document.ImportedAt,
                document.ProcessingState.ToString(),
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

    [Fact]
    public async Task DocumentInformation_AfterSessionRevisionChangesWithoutNotification_DoesNotExposeRetainedMetadata()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        var stream =
            CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        Assert.Equal(documentId, presenter.CurrentDocumentId);
        Assert.Equal(document.DisplayName, presenter.CurrentDocumentDisplayName);
        Assert.Equal(document.FileName, presenter.CurrentDocumentFileName);

        // Lock and reauthenticate without delivering state notifications.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                2);

        Assert.Null(presenter.CurrentDocumentId);
        Assert.Null(presenter.CurrentDocumentDisplayName);
        Assert.Null(presenter.CurrentDocumentFileName);

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

        presenter.Dispose();

        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task Dispose_ClearsCurrentDocumentPresentationAndDisposesStream()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

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

        var stream =
            CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        Assert.Equal(documentId, presenter.CurrentDocumentId);
        Assert.Equal(document.DisplayName, presenter.CurrentDocumentDisplayName);
        Assert.True(stream.CanRead);

        presenter.Dispose();
        presenter.Dispose();

        Assert.Null(presenter.CurrentDocumentId);
        Assert.Null(presenter.CurrentDocumentDisplayName);
        Assert.Null(presenter.CurrentDocumentFileName);
        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);
    }

    private static Mock<IDocumentRepository> CreateRepository(
        Document document)
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        return repository;
    }

    private static void AssertSafeFailureLog(
        RecordingLogger<DocumentWorkspacePresenter> logger,
        string operationCode,
        string safeMessage,
        string sensitiveMarker)
    {
        RecordedLogEntry entry = Assert.Single(
            logger.Entries,
            item => item.Level == LogLevel.Error);

        Assert.Contains(safeMessage, entry.Message);
        Assert.Contains($"OperationCode={operationCode}", entry.Message);
        Assert.Contains($"FailureType={nameof(IOException)}", entry.Message);

        Assert.Equal(
            operationCode,
            Assert.IsType<string>(entry.Properties["OperationCode"]));

        Assert.Equal(
            nameof(IOException),
            Assert.IsType<string>(entry.Properties["FailureType"]));

        Assert.Null(entry.Exception);
        Assert.DoesNotContain(sensitiveMarker, entry.Message);

        foreach (object? propertyValue in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                sensitiveMarker,
                propertyValue?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task Dispose_DuringDocumentLookup_CancelsOpenAndSuppressesLatePresentation()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var lookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var lookupCompletion =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationToken lookupToken = default;

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, cancellationToken) =>
                {
                    lookupToken = cancellationToken;
                    lookupStarted.TrySetResult();
                })
            .Returns(
                (Guid _, CancellationToken _) =>
                    lookupCompletion.Task);

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

        var stream =
            CreateDocumentStream();

        Task openTask =
            presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

        await lookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presenter.Dispose();

        Assert.True(lookupToken.IsCancellationRequested);

        lookupCompletion.SetResult(document);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);
    }

    [Fact]
    public async Task Dispose_DuringMetadataLookupAfterStreamAcquisition_DisposesStreamImmediately()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var metadataLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var metadataLookupCompletion =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int lookupCount = 0;

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) =>
                {
                    if (Interlocked.Increment(ref lookupCount) == 2)
                    {
                        metadataLookupStarted.TrySetResult();
                    }
                })
            .Returns(
                (Guid _, CancellationToken _) =>
                    Volatile.Read(ref lookupCount) == 1
                        ? Task.FromResult<Document?>(document)
                        : metadataLookupCompletion.Task);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var documentReader =
            new Mock<IDocumentReader>();

        var stream =
            CreateDocumentStream();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                documentReader: documentReader);

        Task openTask =
            presenter.OpenAsync(
                documentId,
                TestContext.Current.CancellationToken);

        await metadataLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // The stream has been acquired, but metadata lookup is still pending.
        Assert.True(stream.CanRead);

        presenter.Dispose();

        // Capture whether disposal released the stream before metadata lookup completes.
        bool streamWasDisposedImmediately =
            !stream.CanRead;

        // Complete the deliberately delayed lookup so the operation can settle.
        metadataLookupCompletion.SetResult(document);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.True(
            streamWasDisposedImmediately,
            "The acquired document stream must be disposed immediately when the presenter is disposed.");

        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);
    }

    [Fact]
    public async Task Dispose_DuringRendering_CancelsOperationAndSuppressesLateCompletion()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var renderingStarted =
            new TaskCompletionSource<CancellationToken>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var renderingCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream _,
                string _,
                CancellationToken token) =>
            {
                renderingStarted.TrySetResult(token);

                return renderingCompletion.Task;
            });

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        var stream =
            CreateDocumentStream();

        Task openTask =
            presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

        CancellationToken renderingToken =
            await renderingStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);

        presenter.Dispose();

        Assert.True(renderingToken.IsCancellationRequested);
        Assert.False(stream.CanRead);

        renderingCompletion.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.Null(presenter.CurrentDocumentId);
        Assert.Null(presenter.CurrentDocumentDisplayName);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.Verify(
            x => x.ShowUnsupportedPreview(It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenAsync_LockAndReauthenticationDuringRendering_RejectsStaleResult()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var renderingStarted =
            new TaskCompletionSource<CancellationToken>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var renderingCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream _,
                string _,
                CancellationToken token) =>
            {
                renderingStarted.TrySetResult(token);

                // Deliberately ignore cancellation and complete normally later.
                return renderingCompletion.Task;
            });

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        var stream =
            CreateDocumentStream();

        Task openTask =
            presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

        CancellationToken renderingToken =
            await renderingStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Lock the session while rendering is in progress.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Assert.True(renderingToken.IsCancellationRequested);

        // Re-authenticate before the previous renderer completes.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                2);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        renderingCompletion.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.Null(presenter.CurrentDocumentId);
        Assert.Null(presenter.CurrentDocumentDisplayName);
        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.Verify(
            x => x.ShowUnsupportedPreview(It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenAsync_WhenRevisionAdvancesBeforeStateChangeNotification_RejectsStaleResult()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var renderingStarted =
            new TaskCompletionSource<CancellationToken>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var renderingCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream _,
                string _,
                CancellationToken token) =>
            {
                renderingStarted.TrySetResult(token);

                // Deliberately ignore cancellation and complete normally later.
                return renderingCompletion.Task;
            });

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        var stream =
            CreateDocumentStream();

        Task openTask =
            presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

        CancellationToken renderingToken =
            await renderingStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Advance the authoritative session through lock and reauthentication.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                2);

        // Deliberately do not raise StateChanged.
        // The presenter must rely on the authoritative CurrentSnapshot as well.
        Assert.False(renderingToken.IsCancellationRequested);

        renderingCompletion.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.Null(presenter.CurrentDocumentId);
        Assert.Null(presenter.CurrentDocumentDisplayName);
        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.Verify(
            x => x.ShowUnsupportedPreview(It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        presenter.Dispose();
    }

    [Fact]
    public async Task OpenAsync_WhenSessionIsLockedBeforeOperationBegins_DisposesIncomingStream()
    {
        Guid documentId = Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Locked,
                    1));

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        var stream =
            CreateDocumentStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => presenter.OpenAsync(
                documentId,
                stream,
                "document.csv",
                TestContext.Current.CancellationToken));

        Assert.False(stream.CanRead);

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        presenter.Dispose();
    }

    [Fact]
    public async Task OpenExternally_AfterSessionRevisionChangesWithoutNotification_DoesNotOpenDocument()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        var stream = CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                2);

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        documentViewer.Verify(
            x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()),
            Times.Never);

        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        presenter.Dispose();
    }

    [Fact]
    public async Task RemoveDocument_AfterSessionRevisionChangesWithoutNotification_DoesNotRequestConfirmation()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        var stream = CreateDocumentStream();

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                2);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ConfirmRemoval(It.IsAny<string>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        presenter.Dispose();
    }

    [Fact]
    public async Task OpenAsync_WhenSessionIsLockedDuringMetadataLookupAfterStreamAcquisition_DisposesPendingStream()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var metadataLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var metadataLookupCompletion =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int lookupCount = 0;
        CancellationToken metadataLookupToken = default;

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken token) =>
            {
                int invocation =
                    Interlocked.Increment(ref lookupCount);

                if (invocation == 1)
                {
                    return Task.FromResult<Document?>(document);
                }

                metadataLookupToken = token;
                metadataLookupStarted.TrySetResult();

                return metadataLookupCompletion.Task;
            });

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var documentReader =
            new Mock<IDocumentReader>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var stream =
            CreateDocumentStream();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var logger =
            new RecordingLogger<DocumentWorkspacePresenter>();

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                logger: logger,
                applicationSecuritySession: securitySession,
                documentReader: documentReader);

        Task openTask =
            presenter.OpenAsync(
                documentId,
                TestContext.Current.CancellationToken);

        await metadataLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);

        using CancellationTokenRegistration failingCancellationRegistration =
            metadataLookupToken.Register(
                static () => throw new InvalidOperationException(
                    "PBI320_LOCK_CANCELLATION_CALLBACK_SENTINEL"));

        // Lock the session while the acquired stream is pending.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Assert.True(metadataLookupToken.IsCancellationRequested);
        Assert.False(stream.CanRead);

        // The repository deliberately ignores cancellation and completes late.
        metadataLookupCompletion.SetResult(document);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.Null(presenter.CurrentDocumentId);
        Assert.False(stream.CanRead);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.True(metadataLookupToken.IsCancellationRequested);
        Assert.False(stream.CanRead);

        RecordedLogEntry cancellationFailure =
            Assert.Single(
                logger.Entries,
                entry => entry.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.DocumentWorkspaceCancellationCallbackFailed,
            cancellationFailure.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.DocumentWorkspaceCancellationCallbackFailure}",
            cancellationFailure.Message);

        Assert.Null(cancellationFailure.Exception);

        presenter.Dispose();
    }

    [Fact]
    public async Task OpenAsync_WhenSupersededDuringMetadataLookupAfterStreamAcquisition_DisposesPendingStream()
    {
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        Document firstDocument =
            CreateDocument(firstDocumentId, "first-document.csv");

        Document secondDocument =
            CreateDocument(secondDocumentId, "second-document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var metadataLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var metadataLookupCompletion =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int firstDocumentLookupCount = 0;

        CancellationToken firstMetadataLookupToken = default;

        repository
            .Setup(x => x.GetByIdAsync(
                firstDocumentId,
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken token) =>
            {
                int invocation =
                    Interlocked.Increment(ref firstDocumentLookupCount);

                if (invocation == 1)
                {
                    return Task.FromResult<Document?>(firstDocument);
                }

                firstMetadataLookupToken = token;
                metadataLookupStarted.TrySetResult();

                return metadataLookupCompletion.Task;
            });

        repository
            .Setup(x => x.GetByIdAsync(
                secondDocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondDocument);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var documentReader =
            new Mock<IDocumentReader>();

        var firstStream =
            CreateDocumentStream();

        var secondStream =
            CreateDocumentStream();

        documentReader
            .Setup(x => x.OpenReadAsync(
                firstDocumentId,
                firstDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstStream);

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                documentReader: documentReader);

        Task firstOpenTask =
            presenter.OpenAsync(
                firstDocumentId,
                TestContext.Current.CancellationToken);

        await metadataLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        using CancellationTokenRegistration failingCancellationRegistration =
            firstMetadataLookupToken.Register(
                static () => throw new InvalidOperationException(
                    "PBI320_REPLACEMENT_CANCELLATION_CALLBACK_SENTINEL"));

        Assert.True(firstStream.CanRead);

        await presenter.OpenAsync(
            secondDocumentId,
            secondStream,
            secondDocument.FileName,
            TestContext.Current.CancellationToken);

        Assert.True(firstMetadataLookupToken.IsCancellationRequested);

        Assert.False(firstStream.CanRead);
        Assert.True(secondStream.CanRead);
        Assert.Equal(secondDocumentId, presenter.CurrentDocumentId);

        metadataLookupCompletion.SetResult(firstDocument);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => firstOpenTask);

        Assert.Equal(secondDocumentId, presenter.CurrentDocumentId);
        Assert.True(secondStream.CanRead);

        view.Verify(
            x => x.ShowDocumentAsync(
                secondStream,
                secondDocument.FileName,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowDocumentAsync(
                firstStream,
                firstDocument.FileName,
                It.IsAny<CancellationToken>()),
            Times.Never);

        presenter.Dispose();

        Assert.False(secondStream.CanRead);
    }

    [Fact]
    public async Task OpenAsync_WhenSessionRevisionChangesBeforeReplacement_DisposesDetachedPendingAndIncomingStreams()
    {
        // Arrange
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        Document firstDocument =
            CreateDocument(firstDocumentId, "first-document.csv");

        Document secondDocument =
            CreateDocument(secondDocumentId, "second-document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var metadataLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var metadataLookupCompletion =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int firstDocumentLookupCount = 0;
        CancellationToken firstMetadataLookupToken = default;

        repository
            .Setup(x => x.GetByIdAsync(
                firstDocumentId,
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken token) =>
            {
                int invocation =
                    Interlocked.Increment(ref firstDocumentLookupCount);

                if (invocation == 1)
                {
                    return Task.FromResult<Document?>(firstDocument);
                }

                firstMetadataLookupToken = token;
                metadataLookupStarted.TrySetResult();

                return metadataLookupCompletion.Task;
            });

        repository
            .Setup(x => x.GetByIdAsync(
                secondDocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondDocument);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var documentReader =
            new Mock<IDocumentReader>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var firstStream =
            CreateDocumentStream();

        var secondStream =
            CreateDocumentStream();

        documentReader
            .Setup(x => x.OpenReadAsync(
                firstDocumentId,
                firstDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstStream);

        var presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession,
                documentReader: documentReader);

        Task firstOpenTask =
            presenter.OpenAsync(
                firstDocumentId,
                TestContext.Current.CancellationToken);

        await metadataLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(firstStream.CanRead);

        // Act
        // Change the authoritative session snapshot without raising a
        // notification. The next operation must detect this revision.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => presenter.OpenAsync(
                secondDocumentId,
                secondStream,
                secondDocument.FileName,
                TestContext.Current.CancellationToken));

        // Assert
        Assert.True(firstMetadataLookupToken.IsCancellationRequested);
        Assert.False(firstStream.CanRead);
        Assert.False(secondStream.CanRead);

        // Complete the repository operation that deliberately ignored
        // cancellation and verify that it cannot publish stale content.
        metadataLookupCompletion.SetResult(firstDocument);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => firstOpenTask);

        Assert.Null(presenter.CurrentDocumentId);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        presenter.Dispose();
    }

    [Fact]
    public async Task Dispose_WhenOpenCancellationCallbackThrows_StillClearsPresentationAndDisposesCurrentStream()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(documentId, "document.csv");

        var repository =
            CreateRepository(document);

        var storageService =
            new Mock<IStorageService>();

        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var documentReader =
            new Mock<IDocumentReader>();

        MemoryStream stream =
            CreateDocumentStream();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var showStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var showCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationToken operationToken = default;
        CancellationTokenRegistration cancellationRegistration = default;

        view
            .Setup(x => x.ShowDocumentAsync(
                stream,
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream _,
                string _,
                CancellationToken cancellationToken) =>
            {
                operationToken = cancellationToken;

                cancellationRegistration =
                    cancellationToken.Register(
                        static () => throw new InvalidOperationException(
                            "Document-open cancellation callback failed."));

                showStarted.TrySetResult();

                return showCompletion.Task;
            });

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                documentReader: documentReader);

        Task openTask =
            presenter.OpenAsync(
                documentId,
                TestContext.Current.CancellationToken);

        await showStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);

        try
        {
            // Cancellation callback failures are logged and must not escape disposal.
            presenter.Dispose();

            Assert.False(stream.CanRead);

            view.Verify(
                x => x.ClearPresentation(),
                Times.Once);
        }
        finally
        {
            cancellationRegistration.Dispose();

            showCompletion.TrySetCanceled(operationToken);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => openTask);

            presenter.Dispose();
        }
    }

    private static DocumentWorkspacePresenter CreatePresenter(
        Mock<IDocumentWorkspaceView> view,
        Mock<IDocumentViewer> documentViewer,
        Mock<IDocumentRepository> repository,
        Mock<IStorageService> storageService,
        RecordingLogger<DocumentWorkspacePresenter>? logger = null,
        Mock<IApplicationSecuritySession>? applicationSecuritySession = null,
        Mock<IDocumentReader>? documentReader = null)
    {
        if (applicationSecuritySession is null)
        {
            applicationSecuritySession =
                new Mock<IApplicationSecuritySession>();

            applicationSecuritySession
                .SetupGet(x => x.CurrentSnapshot)
                .Returns(
                    new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Authenticated,
                        0));
        }

        return new DocumentWorkspacePresenter(
            Guid.NewGuid(),
            view.Object,
            documentViewer.Object,
            new GetDocumentHandler(
                repository.Object,
                NullLogger<GetDocumentHandler>.Instance),
            new OpenDocumentHandler(
                repository.Object,
                documentReader?.Object ?? new Mock<IDocumentReader>().Object,
                new DocumentProcessingOptions(),
                NullLogger<OpenDocumentHandler>.Instance),
            new RemoveDocumentHandler(
                repository.Object,
                new Mock<IWorkspaceRepository>().Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance),
            (ILogger<DocumentWorkspacePresenter>?)logger ?? NullLogger<DocumentWorkspacePresenter>.Instance,
            applicationSecuritySession.Object);
    }

    private static Document CreateDocument(
        Guid id,
        string fileName)
    {
        return Document.Create(
            id,
            fileName,
            Path.GetFileNameWithoutExtension(
                fileName),
            "test-sha256",
            $"Data\\{id}\\{fileName}");
    }

    private static MemoryStream CreateDocumentStream()
    {
        return new MemoryStream(
            "Id,Name\r\n1,Alice\r\n"u8.ToArray());
    }

    private static async Task WaitForBackgroundOperationAsync()
    {
        await Task.Delay(
            100);
    }
}

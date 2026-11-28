using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Tests.TestDoubles;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DeskVault.Application.Tests.TestDoubles.SafeDiagnosticLogAssertions;

namespace DeskVault.Application.Tests;

public sealed class ImportDocumentHandlerTests
{
    private static readonly string[] SuccessfulImportLeaseLifecycle =
    [
        "LeaseAcquired",
        "ArtifactStored",
        "MetadataPublished",
        "LeaseReleased"
    ];

    private static readonly string[] FailedImportCompensationLeaseLifecycle =
    [
        "LeaseAcquired",
        "ArtifactStored",
        "MetadataPublicationFailed",
        "CompensationCompleted",
        "LeaseReleased"
    ];

    [Fact]
    public async Task HandleAsync_WhenValidationFails_ReturnsValidationResult()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        var validationResult =
            new ImportDocumentResult(
                ImportDocumentResultStatus.ValidationFailed,
                null,
                "File path is required.");

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(validationResult);

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                string.Empty,
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.ValidationFailed,
            result.Status);

        hashService.Verify(
            x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.ExistsByHashAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateDocumentExists_ReturnsDuplicate()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        var artifactCoordinator =
            new Mock<IDocumentArtifactCoordinator>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("duplicate-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "duplicate-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                artifactCoordinator: artifactCoordinator);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.Duplicate,
            result.Status);

        Assert.Null(
            result.DocumentId);

        artifactCoordinator.Verify(
            x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenImportSucceeds_PersistsDocumentWithCalculatedHash()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .ReturnsAsync("stored/test.txt");

        Document? addedDocument = null;

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                {
                    addedDocument = document;
                })
            .Returns(Task.CompletedTask);

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                "Test Document");

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(
            result.DocumentId);

        Assert.NotNull(
            addedDocument);

        Assert.Equal(
            result.DocumentId,
            addedDocument!.Id);

        Assert.Equal(
            "test.txt",
            addedDocument.FileName);

        Assert.Equal(
            "Test Document",
            addedDocument.DisplayName);

        Assert.Equal(
            "test-hash",
            addedDocument.Sha256Hash);

        Assert.Equal(
            "stored/test.txt",
            addedDocument.StoredFilePath);

        Assert.Equal(
            DocumentStatus.Imported,
            addedDocument.Status);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentHashPersistenceConflicts_ReturnsDuplicate()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("duplicate-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "duplicate-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Guid storedDocumentId =
            Guid.Empty;

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "duplicate-hash"))
            .Callback<string, Guid, CancellationToken, string?>(
                (_, documentId, _, _) =>
                {
                    storedDocumentId =
                        documentId;
                })
            .ReturnsAsync("stored/test.dvault");

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new DocumentHashConflictException(
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<ImportDocumentHandler>();

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                logger);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                "Test Document");

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.Duplicate,
            result.Status);

        Assert.Null(
            result.DocumentId);

        Assert.Equal(
            "The document has already been imported.",
            result.Description);

        Assert.NotEqual(
            Guid.Empty,
            storedDocumentId);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                storedDocumentId,
                It.IsAny<CancellationToken>(),
                "duplicate-hash"),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                storedDocumentId,
                CancellationToken.None),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Warning,
            DiagnosticOperationCodes.DocumentImportDuplicate,
            LogMessages.DocumentImportDuplicate,
            nameof(DocumentHashConflictException));

        var duplicateEntry = Assert.Single(
            logger.Entries,
            entry => entry.Properties.ContainsKey("FailureCategory"));
        Assert.Equal(
            DiagnosticFailureCategory.DuplicateConflict,
            Assert.IsType<DiagnosticFailureCategory>(
                duplicateEntry.Properties["FailureCategory"]));
    }

    [Fact]
    public async Task HandleAsync_WhenDisplayNameIsNotProvided_DerivesDisplayNameFromFileName()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .ReturnsAsync("stored/report.txt");

        Document? addedDocument = null;

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                {
                    addedDocument = document;
                })
            .Returns(Task.CompletedTask);

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\report.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(
            addedDocument);

        Assert.Equal(
            "report.txt",
            addedDocument!.FileName);

        Assert.Equal(
            "report",
            addedDocument.DisplayName);

        Assert.Equal(
            DocumentStatus.Imported,
            addedDocument.Status);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataPersistenceFailsAfterStorageSucceeds_PropagatesFailure()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Guid storedDocumentId =
            Guid.Empty;

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .Callback<string, Guid, CancellationToken, string?>(
                (_, documentId, _, _) =>
                {
                    storedDocumentId =
                        documentId;
                })
            .ReturnsAsync("stored/test.txt");

        var persistenceException =
            new InvalidOperationException(
                "Metadata persistence failed.");

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                persistenceException);

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                "Test Document");

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    handler.HandleAsync(command, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(
            persistenceException,
            exception);

        Assert.NotEqual(
            Guid.Empty,
            storedDocumentId);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                storedDocumentId,
                It.IsAny<CancellationToken>(),
                "test-hash"),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                storedDocumentId,
                CancellationToken.None),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenImportIsCancelledAfterStorageSucceeds_CleansUpStoredArtifact()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Guid storedDocumentId =
            Guid.Empty;

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .Callback<string, Guid, CancellationToken, string?>(
                (_, documentId, _, _) =>
                {
                    storedDocumentId =
                        documentId;
                })
            .ReturnsAsync("stored/test.txt");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (_, _) =>
                {
                    cancellationTokenSource.Cancel();
                })
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationTokenSource.Token));

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                "Test Document");

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    command,
                    cancellationTokenSource.Token));

        // Assert
        Assert.NotEqual(
            Guid.Empty,
            storedDocumentId);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                storedDocumentId,
                It.IsAny<CancellationToken>(),
                "test-hash"),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                storedDocumentId,
                CancellationToken.None),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCleanupFailsAfterMetadataPersistenceFailure_PropagatesOriginalFailure()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Guid storedDocumentId =
            Guid.Empty;

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .Callback<string, Guid, CancellationToken, string?>(
                (_, documentId, _, _) =>
                {
                    storedDocumentId =
                        documentId;
                })
            .ReturnsAsync("stored/test.txt");

        var persistenceException =
            new InvalidOperationException(
                "Metadata persistence failed.");

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                persistenceException);

        storageService
            .Setup(x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    CreateSensitiveDetails(),
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<ImportDocumentHandler>();

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                logger);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                "Test Document");

        // Act
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    handler.HandleAsync(command, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(
            persistenceException,
            exception);

        Assert.NotEqual(
            Guid.Empty,
            storedDocumentId);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                storedDocumentId,
                It.IsAny<CancellationToken>(),
                "test-hash"),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                storedDocumentId,
                CancellationToken.None),
            Times.Once);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentImportArtifactCleanupFailure,
            LogMessages.DocumentImportCleanupFailed,
            nameof(IOException));
    }

    [Fact]
    public async Task HandleAsync_WhenStorageFails_ReturnsStorageFailed()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ThrowsAsync(
                new IOException(
                    CreateSensitiveDetails(),
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<ImportDocumentHandler>();

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                logger);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Null(
            result.DocumentId);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.Equal(
            "The document could not be imported because storage failed.",
            result.Description);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentImportStorageFailure,
            LogMessages.DocumentImportStorageFailed,
            nameof(IOException));
    }

    [Fact]
    public async Task HandleAsync_WhenStorageAccessIsDenied_ReturnsStorageFailed()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ThrowsAsync(
                new UnauthorizedAccessException(
                    CreateSensitiveDetails(),
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<ImportDocumentHandler>();

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                logger);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Null(
            result.DocumentId);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.Equal(
            "The document could not be imported because storage failed.",
            result.Description);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentImportStorageFailure,
            LogMessages.DocumentImportStorageFailed,
            nameof(UnauthorizedAccessException));
    }

    [Fact]
    public async Task HandleAsync_WhenImportSucceeds_HoldsLeaseUntilMetadataPublicationCompletes()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        var lease =
            new Mock<IAsyncDisposable>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        List<string> lifecycle =
            [];

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        coordinator
            .Setup(x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("LeaseAcquired"))
            .ReturnsAsync(lease.Object);

        lease
            .Setup(x => x.DisposeAsync())
            .Callback(() => lifecycle.Add("LeaseReleased"))
            .Returns(ValueTask.CompletedTask);

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .Callback<string, Guid, CancellationToken, string?>(
                (_, _, _, _) => lifecycle.Add("ArtifactStored"))
            .ReturnsAsync("stored/test.txt");

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (_, _) => lifecycle.Add("MetadataPublished"))
            .Returns(Task.CompletedTask);

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                artifactCoordinator: coordinator);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "C:\\Documents\\test.txt",
                    "Test Document"),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            SuccessfulImportLeaseLifecycle,
            lifecycle);

        coordinator.Verify(
            x => x.AcquireAsync(
                It.Is<Guid>(id => id != Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);

        lease.Verify(
            x => x.DisposeAsync(),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataPersistenceFails_CompletesCompensationBeforeLeaseRelease()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        var lease =
            new Mock<IAsyncDisposable>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        List<string> lifecycle =
            [];

        var compensationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var allowCompensationToComplete =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        coordinator
            .Setup(x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("LeaseAcquired"))
            .ReturnsAsync(lease.Object);

        lease
            .Setup(x => x.DisposeAsync())
            .Callback(() => lifecycle.Add("LeaseReleased"))
            .Returns(ValueTask.CompletedTask);

        storageService
            .Setup(x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                "test-hash"))
            .Callback<string, Guid, CancellationToken, string?>(
                (_, _, _, _) => lifecycle.Add("ArtifactStored"))
            .ReturnsAsync("stored/test.txt");

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (_, _) => lifecycle.Add("MetadataPublicationFailed"))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Metadata persistence failed."));

        storageService
            .Setup(x => x.DeleteAsync(
                It.IsAny<Guid>(),
                CancellationToken.None))
            .Returns(
                (Guid _, CancellationToken _) =>
                    CompleteCompensationAsync());

        async Task CompleteCompensationAsync()
        {
            compensationStarted.TrySetResult(true);

            await allowCompensationToComplete.Task;

            lifecycle.Add("CompensationCompleted");
        }

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                artifactCoordinator: coordinator);

        // Act
        Task<ImportDocumentResult> importTask =
            handler.HandleAsync(
                new ImportDocumentCommand(
                    "C:\\Documents\\test.txt",
                    "Test Document"),
                TestContext.Current.CancellationToken);

        await compensationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Compensation is still pending, so the lease must remain held.
        Assert.DoesNotContain(
            "LeaseReleased",
            lifecycle);

        Assert.False(importTask.IsCompleted);

        allowCompensationToComplete.SetResult(true);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => importTask);

        // Assert
        Assert.Equal(
            "Metadata persistence failed.",
            exception.Message);

        Assert.Equal(
            FailedImportCompensationLeaseLifecycle,
            lifecycle);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                CancellationToken.None),
            Times.Once);

        lease.Verify(
            x => x.DisposeAsync(),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCoordinationAcquisitionFails_DoesNotCreateArtifact()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        var logger =
            new RecordingLogger<ImportDocumentHandler>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        coordinator
            .Setup(x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Sensitive coordination failure details."));

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                logger,
                coordinator);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "C:\\Documents\\test.txt",
                    "Test Document"),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.UnexpectedError,
            result.Status);

        Assert.Null(result.DocumentId);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentArtifactCoordinationFailure,
            LogMessages.DocumentArtifactCoordinationFailed,
            nameof(IOException));
    }

    [Fact]
    public async Task HandleAsync_WhenCoordinationAcquisitionIsCancelled_DoesNotCreateArtifact()
    {
        // Arrange
        var validator =
            new Mock<IImportDocumentValidator>();

        var hashService =
            new Mock<IHashService>();

        var storageService =
            new Mock<IStorageService>();

        var repository =
            new Mock<IDocumentRepository>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        validator
            .Setup(x => x.Validate(It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-hash");

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        coordinator
            .Setup(x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new OperationCanceledException());

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository,
                artifactCoordinator: coordinator);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new ImportDocumentCommand(
                    "C:\\Documents\\test.txt",
                    "Test Document"),
                TestContext.Current.CancellationToken));

        // Assert
        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()),
            Times.Never);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        coordinator.Verify(
            x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    private static ImportDocumentHandler CreateHandler(
        IImportDocumentValidator validator,
        IHashService hashService,
        IStorageService storageService,
        Mock<IDocumentRepository> repository,
        ILogger<ImportDocumentHandler>? logger = null,
        Mock<IDocumentArtifactCoordinator>? artifactCoordinator = null)
    {
        Mock<IDocumentArtifactCoordinator> coordinator =
            artifactCoordinator ?? CreateCoordinator();

        return new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository.Object,
            coordinator.Object,
            logger ?? NullLogger<ImportDocumentHandler>.Instance);
    }

    private static Mock<IDocumentArtifactCoordinator> CreateCoordinator()
    {
        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        coordinator
            .Setup(x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new TestDocumentArtifactLease());

        return coordinator;
    }

    private sealed class TestDocumentArtifactLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

}

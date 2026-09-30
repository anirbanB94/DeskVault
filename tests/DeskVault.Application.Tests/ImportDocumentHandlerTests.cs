using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ImportDocumentHandlerTests
{
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
            await handler.HandleAsync(command);

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
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command);

        // Assert
        Assert.Equal(
            ImportDocumentResultStatus.Duplicate,
            result.Status);

        Assert.Null(
            result.DocumentId);

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
            await handler.HandleAsync(command);

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
                    new InvalidOperationException(
                        "Duplicate SHA-256 constraint violation.")));

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
            await handler.HandleAsync(command);

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
            await handler.HandleAsync(command);

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
                    handler.HandleAsync(command));

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
                    "Cleanup failed."));

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
                    handler.HandleAsync(command));

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
                    "Storage failed."));

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command);

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
                    "Access denied."));

        var handler =
            CreateHandler(
                validator.Object,
                hashService.Object,
                storageService.Object,
                repository);

        var command =
            new ImportDocumentCommand(
                "C:\\Documents\\test.txt",
                null);

        // Act
        ImportDocumentResult result =
            await handler.HandleAsync(command);

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
    }

    private static ImportDocumentHandler CreateHandler(
        IImportDocumentValidator validator,
        IHashService hashService,
        IStorageService storageService,
        Mock<IDocumentRepository> repository)
    {
        return new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository.Object,
            NullLogger<ImportDocumentHandler>.Instance);
    }
}

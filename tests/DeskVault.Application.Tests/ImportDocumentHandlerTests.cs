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

        var result =
            await handler.HandleAsync(command);

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
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateDocumentExists_ReturnsDuplicate()
    {
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

        var result =
            await handler.HandleAsync(command);

        Assert.Equal(
            ImportDocumentResultStatus.Duplicate,
            result.Status);

        Assert.Null(result.DocumentId);

        storageService.Verify(
            x => x.StoreAsync(
                It.IsAny<string>(),
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
    public async Task HandleAsync_WhenImportSucceeds_PersistsDocument()
    {
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
                It.IsAny<CancellationToken>()))
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

        var result =
            await handler.HandleAsync(command);

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(result.DocumentId);

        Assert.NotNull(addedDocument);

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

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDisplayNameIsNotProvided_DerivesDisplayNameFromFileName()
    {
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
                It.IsAny<CancellationToken>()))
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

        var result =
            await handler.HandleAsync(command);

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(addedDocument);

        Assert.Equal(
            "report.txt",
            addedDocument!.FileName);

        Assert.Equal(
            "report",
            addedDocument.DisplayName);

        Assert.Equal(
            DocumentStatus.Imported,
            addedDocument.Status);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageFails_ReturnsStorageFailed()
    {
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
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException("Storage failed."));

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

        var result =
            await handler.HandleAsync(command);

        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Null(result.DocumentId);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageAccessIsDenied_ReturnsStorageFailed()
    {
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
                It.IsAny<CancellationToken>()))
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

        var result =
            await handler.HandleAsync(command);

        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Null(result.DocumentId);

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

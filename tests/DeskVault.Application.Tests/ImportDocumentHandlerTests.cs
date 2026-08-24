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
            new TestImportDocumentValidator(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.ValidationFailed,
                    null,
                    "File path is required."));

        var hashService = new TestHashService();
        var storageService = new TestStorageService();
        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    string.Empty,
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.ValidationFailed,
            result.Status);

        Assert.Equal(
            "File path is required.",
            result.Description);

        Assert.False(hashService.WasCalled);
        Assert.False(storageService.WasCalled);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIsDuplicate_ReturnsDuplicateResult()
    {
        var validator = CreateSuccessfulValidator();
        var hashService = new TestHashService("duplicate-hash");
        var storageService = new TestStorageService();
        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                "duplicate-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "document.txt",
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.Duplicate,
            result.Status);

        Assert.Null(result.DocumentId);

        Assert.Equal(
            "The document has already been imported.",
            result.Description);

        Assert.True(hashService.WasCalled);
        Assert.False(storageService.WasCalled);

        repository.Verify(
            x => x.ExistsByHashAsync(
                "duplicate-hash",
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenImportSucceeds_PersistsDocument()
    {
        var validator = CreateSuccessfulValidator();
        var hashService = new TestHashService("test-hash");
        var storageService =
            new TestStorageService("stored/document.dvault");

        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Document? addedDocument = null;

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                    addedDocument = document)
            .Returns(Task.CompletedTask);

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "document.txt",
                    "My Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(result.DocumentId);
        Assert.True(storageService.WasCalled);
        Assert.NotNull(addedDocument);

        Assert.Equal(
            result.DocumentId,
            addedDocument.Id);

        Assert.Equal(
            "document.txt",
            addedDocument.FileName);

        Assert.Equal(
            "My Document",
            addedDocument.DisplayName);

        Assert.Equal(
            "test-hash",
            addedDocument.Sha256Hash);

        Assert.Equal(
            "stored/document.dvault",
            addedDocument.StoredFilePath);

        repository.Verify(
            x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        processingService.Verify(
            x => x.ProcessAsync(
                result.DocumentId!.Value,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDisplayNameIsNotProvided_DerivesDisplayNameFromFileName()
    {
        var validator = CreateSuccessfulValidator();
        var hashService = new TestHashService("test-hash");
        var storageService =
            new TestStorageService("stored/document.dvault");

        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                "test-hash",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Document? addedDocument = null;

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                    addedDocument = document)
            .Returns(Task.CompletedTask);

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    @"C:\Documents\KnowledgeBase.txt",
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(addedDocument);

        Assert.Equal(
            "KnowledgeBase",
            addedDocument.DisplayName);

        processingService.Verify(
            x => x.ProcessAsync(
                result.DocumentId!.Value,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageThrowsIOException_ReturnsStorageFailed()
    {
        var validator = CreateSuccessfulValidator();
        var hashService = new TestHashService("test-hash");

        var storageService =
            new TestStorageService
            {
                ExceptionToThrow =
                    new IOException(
                        "Storage operation failed.")
            };

        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        SetupRepositoryForImport(
            repository,
            "test-hash");

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "document.txt",
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Equal(
            "Storage operation failed.",
            result.Description);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageThrowsUnauthorizedAccessException_ReturnsStorageFailed()
    {
        var validator = CreateSuccessfulValidator();
        var hashService = new TestHashService("test-hash");

        var storageService =
            new TestStorageService
            {
                ExceptionToThrow =
                    new UnauthorizedAccessException(
                        "Access denied.")
            };

        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        SetupRepositoryForImport(
            repository,
            "test-hash");

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "document.txt",
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Equal(
            "Access denied.",
            result.Description);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryAddThrowsIOException_ReturnsStorageFailed()
    {
        var validator = CreateSuccessfulValidator();
        var hashService = new TestHashService("test-hash");

        var storageService =
            new TestStorageService(
                "stored/document.dvault");

        var repository = new Mock<IDocumentRepository>();
        var processingService = new Mock<IDocumentProcessingService>();

        SetupRepositoryForImport(
            repository,
            "test-hash");

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Metadata storage operation failed."));

        var handler =
            CreateHandler(
                validator,
                hashService,
                storageService,
                repository,
                processingService);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    "document.txt",
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.StorageFailed,
            result.Status);

        Assert.Equal(
            "Metadata storage operation failed.",
            result.Description);

        Assert.True(
            storageService.WasCalled);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestImportDocumentValidator CreateSuccessfulValidator()
    {
        return new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));
    }

    private static void SetupRepositoryForImport(
        Mock<IDocumentRepository> repository,
        string hash)
    {
        repository
            .Setup(x => x.ExistsByHashAsync(
                hash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static ImportDocumentHandler CreateHandler(
        IImportDocumentValidator validator,
        IHashService hashService,
        IStorageService storageService,
        Mock<IDocumentRepository> repository,
        Mock<IDocumentProcessingService> processingService)
    {
        return new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository.Object,
            processingService.Object,
            NullLogger<ImportDocumentHandler>.Instance);
    }

    private sealed class TestImportDocumentValidator
        : IImportDocumentValidator
    {
        private readonly ImportDocumentResult _result;

        public TestImportDocumentValidator(
            ImportDocumentResult result)
        {
            _result = result;
        }

        public ImportDocumentResult Validate(
            ImportDocumentCommand command)
        {
            return _result;
        }
    }

    private sealed class TestHashService
        : IHashService
    {
        private readonly string _hash;

        public TestHashService(
            string hash = "test-hash")
        {
            _hash = hash;
        }

        public bool WasCalled { get; private set; }

        public Task<string> ComputeSha256Async(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            return Task.FromResult(_hash);
        }
    }

    private sealed class TestStorageService
        : IStorageService
    {
        private readonly string _storedFilePath;

        public TestStorageService(
            string storedFilePath = "stored.dvault")
        {
            _storedFilePath = storedFilePath;
        }

        public bool WasCalled { get; private set; }

        public Exception? ExceptionToThrow { get; set; }

        public Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(
                _storedFilePath);
        }

        public Task DeleteAsync(
            string storedFilePath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

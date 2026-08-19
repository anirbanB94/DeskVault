using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class ImportDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenValidationFails_ReturnsValidationResult()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.ValidationFailed,
                null,
                "File path is required."));

        var hashService = new TestHashService();
        var storageService = new TestStorageService();
        var repository = new TestDocumentRepository();

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

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
        Assert.False(repository.AddWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentIsDuplicate_ReturnsDuplicateResult()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));

        var hashService = new TestHashService(
            "duplicate-hash");

        var storageService = new TestStorageService();

        var repository = new TestDocumentRepository
        {
            DocumentExists = true
        };

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

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
        Assert.False(repository.AddWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenImportSucceeds_PersistsDocument()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));

        var hashService = new TestHashService(
            "test-hash");

        var storageService = new TestStorageService(
            "stored/document.dvault");

        var repository = new TestDocumentRepository();

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

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
        Assert.True(repository.AddWasCalled);

        Assert.NotNull(repository.AddedDocument);

        Assert.Equal(
            result.DocumentId,
            repository.AddedDocument.Id);

        Assert.Equal(
            "document.txt",
            repository.AddedDocument.FileName);

        Assert.Equal(
            "My Document",
            repository.AddedDocument.DisplayName);

        Assert.Equal(
            "test-hash",
            repository.AddedDocument.Sha256Hash);

        Assert.Equal(
            "stored/document.dvault",
            repository.AddedDocument.StoredFilePath);
    }

    [Fact]
    public async Task HandleAsync_WhenDisplayNameIsNotProvided_DerivesDisplayNameFromFileName()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));

        var hashService = new TestHashService(
            "test-hash");

        var storageService = new TestStorageService(
            "stored/document.dvault");

        var repository = new TestDocumentRepository();

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

        ImportDocumentResult result =
            await handler.HandleAsync(
                new ImportDocumentCommand(
                    @"C:\Documents\KnowledgeBase.txt",
                    null));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            result.Status);

        Assert.NotNull(repository.AddedDocument);

        Assert.Equal(
            "KnowledgeBase",
            repository.AddedDocument.DisplayName);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageThrowsIOException_ReturnsStorageFailed()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));

        var hashService = new TestHashService(
            "test-hash");

        var storageService = new TestStorageService
        {
            ExceptionToThrow = new IOException(
                "Storage operation failed.")
        };

        var repository = new TestDocumentRepository();

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

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

        Assert.False(repository.AddWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageThrowsUnauthorizedAccessException_ReturnsStorageFailed()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));

        var hashService = new TestHashService(
            "test-hash");

        var storageService = new TestStorageService
        {
            ExceptionToThrow = new UnauthorizedAccessException(
                "Access denied.")
        };

        var repository = new TestDocumentRepository();

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

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

        Assert.False(repository.AddWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryAddThrowsIOException_ReturnsStorageFailed()
    {
        var validator = new TestImportDocumentValidator(
            new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                null,
                "Validation successful."));

        var hashService = new TestHashService(
            "test-hash");

        var storageService = new TestStorageService(
            "stored/document.dvault");

        var repository = new TestDocumentRepository
        {
            AddException =
                new IOException(
                    "Metadata storage operation failed.")
        };

        var handler = new ImportDocumentHandler(
            validator,
            hashService,
            storageService,
            repository);

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

        Assert.True(storageService.WasCalled);
        Assert.True(repository.AddWasCalled);
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

    private sealed class TestHashService : IHashService
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

            return Task.FromResult(_storedFilePath);
        }

        public Task DeleteAsync(
            string storedFilePath,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestDocumentRepository
        : IDocumentRepository
    {
        public bool DocumentExists { get; set; }

        public bool AddWasCalled { get; private set; }

        public Document? AddedDocument { get; private set; }

        public Exception? AddException { get; set; }

        public Task<bool> ExistsByHashAsync(
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DocumentExists);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            AddWasCalled = true;
            AddedDocument = document;

            if (AddException is not null)
            {
                throw AddException;
            }

            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Document?>(null);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

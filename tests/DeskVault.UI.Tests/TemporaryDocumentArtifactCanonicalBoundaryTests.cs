using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using DeskVault.UI.Services.Document;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies that temporary viewing-artifact recovery cannot mutate
/// canonical encrypted document artifacts or persisted document metadata.
/// </summary>
public sealed class TemporaryDocumentArtifactCanonicalBoundaryTests
{
    [Fact]
    public async Task Recover_CleansTemporaryArtifactWithoutChangingCanonicalArtifactOrDocumentMetadata()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        string canonicalRootDirectory =
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "CanonicalVault");

        string temporaryArtifactDirectory =
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "TemporaryDocuments");

        Directory.CreateDirectory(canonicalRootDirectory);

        var dataPaths = new DeskVaultDataPaths(
            canonicalRootDirectory);

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(
                EncryptionKeyMaterial.KeySizeBytes);

        using var keyService = new TestEncryptionKeyService(
            encryptionKey);

        Guid documentId = Guid.NewGuid();

        const string documentContent =
            """
            Canonical DeskVault document content.
            This content is stored as an encrypted .dvault artifact.
            """;

        byte[] documentContentBytes =
            Encoding.UTF8.GetBytes(documentContent);

        string sourceFilePath =
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "canonical-source.txt");

        await File.WriteAllBytesAsync(
            sourceFilePath,
            documentContentBytes,
            TestContext.Current.CancellationToken);

        string sha256Hash =
            Convert.ToHexString(
                SHA256.HashData(documentContentBytes))
            .ToLowerInvariant();

        // Create the actual SQLite document metadata store.
        await using var databaseConnection =
            new SqliteConnection(
                $"Data Source={dataPaths.DatabasePath};Pooling=False");

        await databaseConnection.OpenAsync(
            TestContext.Current.CancellationToken);

        DbContextOptions<DeskVaultDbContext> databaseOptions =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(databaseConnection)
                .Options;

        await using (var initializationContext =
                     new DeskVaultDbContext(databaseOptions))
        {
            await initializationContext.Database.EnsureCreatedAsync(
                TestContext.Current.CancellationToken);
        }

        var documentRepository = new SqliteDocumentRepository(
            new TestDbContextFactory(databaseOptions),
            NullLogger<SqliteDocumentRepository>.Instance);

        // Create a genuine encrypted canonical artifact through the production
        // storage and encryption services.
        var artifactPathResolver = new DocumentArtifactPathResolver(
            dataPaths);

        var encryptionService = new DocumentEncryptionService(
            keyService,
            NullLogger<DocumentEncryptionService>.Instance);

        var storageService = new FileSystemStorageService(
            encryptionService,
            dataPaths,
            artifactPathResolver,
            NullLogger<FileSystemStorageService>.Instance);

        string canonicalArtifactPath =
            await storageService.StoreAsync(
                sourceFilePath,
                documentId,
                TestContext.Current.CancellationToken);

        Assert.EndsWith(
            ".dvault",
            canonicalArtifactPath,
            StringComparison.OrdinalIgnoreCase);

        Assert.True(
            File.Exists(canonicalArtifactPath));

        byte[] canonicalArtifactBeforeRecovery =
            await File.ReadAllBytesAsync(
                canonicalArtifactPath,
                TestContext.Current.CancellationToken);

        Assert.NotEmpty(canonicalArtifactBeforeRecovery);

        // Persist the matching canonical document metadata.
        Document document =
            Document.Create(
                documentId,
                "canonical-source.txt",
                "Canonical Boundary Test Document",
                sha256Hash,
                canonicalArtifactPath);

        await documentRepository.AddAsync(
            document,
            TestContext.Current.CancellationToken);

        Document persistedDocumentBeforeRecovery =
            Assert.IsType<Document>(
                await documentRepository.GetByIdAsync(
                    documentId,
                    TestContext.Current.CancellationToken));

        // Create a separate temporary viewing artifact that is eligible for cleanup.
        var temporaryStore = new TemporaryDocumentArtifactStore(
            temporaryArtifactDirectory,
            Guid.NewGuid);

        TemporaryDocumentArtifact temporaryArtifact =
            await CreateCleanupEligibleArtifactAsync(
                temporaryStore);

        string temporaryArtifactPath =
            temporaryArtifact.FilePath;

        string temporaryOwnershipMetadataPath =
            temporaryArtifact.OwnershipMetadataPath;

        Assert.True(File.Exists(temporaryArtifactPath));
        Assert.True(File.Exists(temporaryOwnershipMetadataPath));

        var recoveryService = new TemporaryDocumentArtifactRecoveryService(
            temporaryStore,
            NullLogger<TemporaryDocumentArtifactRecoveryService>.Instance);

        // Act
        TemporaryDocumentArtifactRecoveryResult recoveryResult =
            recoveryService.Recover();

        // Assert: the temporary viewing artifact was cleaned.
        Assert.Equal(1, recoveryResult.CandidateCount);
        Assert.Equal(1, recoveryResult.CleanedCount);
        Assert.Equal(0, recoveryResult.PreservedCount);
        Assert.Equal(0, recoveryResult.FailedCount);

        Assert.False(File.Exists(temporaryArtifactPath));
        Assert.False(File.Exists(temporaryOwnershipMetadataPath));

        // Assert: the canonical encrypted artifact remains byte-for-byte identical.
        Assert.True(File.Exists(canonicalArtifactPath));

        byte[] canonicalArtifactAfterRecovery =
            await File.ReadAllBytesAsync(
                canonicalArtifactPath,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            canonicalArtifactBeforeRecovery,
            canonicalArtifactAfterRecovery);

        // Assert: the canonical artifact remains decryptable and returns
        // the original document content after temporary recovery.
        await using (var encryptedSource =
                     new FileStream(
                         canonicalArtifactPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         bufferSize: 81920,
                         useAsync: true))
        await using (var decryptedDestination = new MemoryStream())
        {
            await encryptionService.DecryptAsync(
                encryptedSource,
                decryptedDestination,
                documentId,
                TestContext.Current.CancellationToken);

            Assert.Equal(
                documentContent,
                Encoding.UTF8.GetString(
                    decryptedDestination.ToArray()));
        }

        // Assert: persisted document metadata remains unchanged.
        Document persistedDocumentAfterRecovery =
            Assert.IsType<Document>(
                await documentRepository.GetByIdAsync(
                    documentId,
                    TestContext.Current.CancellationToken));

        AssertDocumentMetadataUnchanged(
            persistedDocumentBeforeRecovery,
            persistedDocumentAfterRecovery);
    }

    private static async Task<TemporaryDocumentArtifact>
        CreateCleanupEligibleArtifactAsync(
            TemporaryDocumentArtifactStore store)
    {
        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("temporary-view.txt");

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "Temporary document viewing content."));

        await artifact.CopyFromAsync(
            sourceStream,
            TestContext.Current.CancellationToken);

        artifact.MarkCleanupEligible();
        artifact.ReleaseOwnershipLease();

        return artifact;
    }

    private static void AssertDocumentMetadataUnchanged(
        Document expected,
        Document actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.FileName, actual.FileName);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.Sha256Hash, actual.Sha256Hash);
        Assert.Equal(expected.StoredFilePath, actual.StoredFilePath);
        Assert.Equal(expected.ImportedAt, actual.ImportedAt);

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.LifecycleState, actual.LifecycleState);
        Assert.Equal(expected.ProcessingState, actual.ProcessingState);

        Assert.Equal(
            expected.ProcessingGeneration,
            actual.ProcessingGeneration);

        Assert.Equal(
            expected.LastSuccessfulProcessingGeneration,
            actual.LastSuccessfulProcessingGeneration);

        Assert.Equal(
            expected.LastSuccessfulProcessingRuleVersion,
            actual.LastSuccessfulProcessingRuleVersion);

        Assert.Equal(
            expected.KnowledgeAvailability
                .Select(
                    availability =>
                        (
                            availability.Representation,
                            availability.State,
                            availability.LastAvailableProcessingGeneration))
                .ToArray(),
            actual.KnowledgeAvailability
                .Select(
                    availability =>
                        (
                            availability.Representation,
                            availability.State,
                            availability.LastAvailableProcessingGeneration))
                .ToArray());
    }

    private sealed class TestEncryptionKeyService :
        IEncryptionKeyService,
        IDisposable
    {
        private readonly byte[] _key;
        private readonly Guid _keyId = Guid.NewGuid();

        public TestEncryptionKeyService(byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);

            _key = [.. key];
        }

        public Task<EncryptionKeyMaterial> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new EncryptionKeyMaterial(
                    [.. _key],
                    _keyId,
                    keyVersion: 1));
        }

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(_key);
        }
    }

    private sealed class TestDbContextFactory :
        IDbContextFactory<DeskVaultDbContext>
    {
        private readonly DbContextOptions<DeskVaultDbContext> _options;

        public TestDbContextFactory(
            DbContextOptions<DeskVaultDbContext> options)
        {
            _options = options;
        }

        public DeskVaultDbContext CreateDbContext()
        {
            return new DeskVaultDbContext(_options);
        }

        public Task<DeskVaultDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateDbContext());
        }
    }

    private sealed class TemporaryRoot : IDisposable
    {
        public TemporaryRoot()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"DeskVault.CanonicalBoundaryTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
        }
    }
}

using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Tests.TestDoubles;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using static DeskVault.Application.Tests.TestDoubles.SafeDiagnosticLogAssertions;

namespace DeskVault.Application.Tests;

public sealed class RecoverDocumentArtifactsHandlerTests
{

    private static readonly string[] SuccessfulRecoveryLeaseLifecycle =
    [
        "ReconciliationMetadataRead",
        "LeaseAcquired",
        "MetadataRevalidated",
        "OwnershipRevalidated",
        "ArtifactExistenceRevalidated",
        "ArtifactDeleted",
        "LeaseReleased"
    ];

    [Fact]
    public async Task HandleAsync_WhenCanonicalOrphanExists_DeletesArtifactWhileHoldingLease()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        List<string> lifecycle =
            [];

        int metadataReadCount = 0;

        var repository =
            CreateRepository([]);

        repository
            .Setup(x => x.GetByIdAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .Returns(
                (Guid _, CancellationToken _) =>
                {
                    metadataReadCount++;

                    lifecycle.Add(
                        metadataReadCount == 1
                            ? "ReconciliationMetadataRead"
                            : "MetadataRevalidated");

                    return Task.FromResult<Document?>(null);
                });

        var artifactEnumerator =
            CreateArtifactEnumerator([orphanPath]);

        artifactEnumerator
            .Setup(x => x.ExistsForDocument(orphanId))
            .Callback(() => lifecycle.Add("ArtifactExistenceRevalidated"))
            .Returns(true);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                orphanId,
                Path.GetFullPath(orphanPath)))
            .Callback<Guid, string>(
                (_, _) => lifecycle.Add("OwnershipRevalidated"))
            .Returns(true);

        storageService
            .Setup(x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("ArtifactDeleted"))
            .Returns(Task.CompletedTask);

        var lease =
            new Mock<IAsyncDisposable>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        coordinator
            .Setup(x => x.AcquireAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("LeaseAcquired"))
            .ReturnsAsync(lease.Object);

        lease
            .Setup(x => x.DisposeAsync())
            .Callback(() => lifecycle.Add("LeaseReleased"))
            .Returns(ValueTask.CompletedTask);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                artifactCoordinator: coordinator);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.Recovered,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            orphanId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(orphanPath),
            finding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            finding.RecoveryAction);

        Assert.Equal(
            SuccessfulRecoveryLeaseLifecycle,
            lifecycle);

        repository.Verify(
            x => x.GetByIdAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        coordinator.Verify(
            x => x.AcquireAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                orphanId,
                Path.GetFullPath(orphanPath)),
            Times.Once);

        artifactEnumerator.Verify(
            x => x.ExistsForDocument(orphanId),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        lease.Verify(
            x => x.DisposeAsync(),
            Times.Once);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOrphanArtifactDisappearsBeforeRevalidation_DoesNotDeleteMissingArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.ExistsForDocument(orphanId))
            .Returns(false);

        artifactEnumerator
            .SetupSequence(x => x.EnumerateIncrementallyAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                EnumerateArtifactPathsAsync(
                    [orphanPath],
                    TestContext.Current.CancellationToken))
            .Returns(
                EnumerateArtifactPathsAsync(
                    [],
                    TestContext.Current.CancellationToken));

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult firstResult =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        RecoverDocumentArtifactsResult retryResult =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.NoActionRequired,
            firstResult.Status);

        var finding =
            Assert.Single(firstResult.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            finding.RecoveryAction);

        Assert.Equal(
            "The orphaned artifact was no longer present when recovery revalidated it; no cleanup was required.",
            finding.Description);

        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.NoActionRequired,
            retryResult.Status);

        artifactEnumerator.Verify(
            x => x.ExistsForDocument(orphanId),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCleanupIsInterrupted_CanBeRetried()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.ExistsForDocument(
                It.IsAny<Guid>()))
            .Returns(true);

        artifactEnumerator
            .SetupSequence(x => x.EnumerateIncrementallyAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                EnumerateArtifactPathsAsync(
                    [orphanPath],
                    TestContext.Current.CancellationToken))
            .Returns(
                EnumerateArtifactPathsAsync(
                    [orphanPath],
                    TestContext.Current.CancellationToken));

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .SetupSequence(x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Simulated recovery interruption."))
            .Returns(
                Task.CompletedTask);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        await Assert.ThrowsAsync<IOException>(
            () =>
                handler.HandleAsync(
                    new RecoverDocumentArtifactsCommand(),
                    TestContext.Current.CancellationToken));

        RecoverDocumentArtifactsResult retryResult =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.Recovered,
            retryResult.Status);

        var finding =
            Assert.Single(
                retryResult.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            orphanId,
            finding.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactBelongsToPersistedDocument_DoesNotDeleteIt()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [expectedArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                document.Id,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.NoActionRequired,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                document.Id,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenOrphanPathIsOutsideOwnedBoundary_PreservesArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Outside",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                orphanId,
                orphanPath))
            .Returns(false);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            result.Findings.Single().Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactHasNoParseableDocumentIdentity_PreservesArtifact()
    {
        // Arrange
        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    "not-a-document-id.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Null(
            finding.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentHasMissingArtifact_PreservesDocument()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator([]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.MissingArtifact,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactIsUnreadable_PreservesDocument()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [expectedArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                document.Id,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new CryptographicException(
                    "Artifact authentication failed."));

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenReadableArtifactContentDoesNotMatchDocumentHash_PreservesDocument()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [expectedArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                document.Id,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new DocumentContentIntegrityException());

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.ContentMismatch,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationIsAlreadyRequested_ThrowsAndDoesNotAccessDependencies()
    {
        // Arrange
        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        ReconcileDocumentArtifactsHandler reconciliationHandler =
            CreateReconciliationHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        var coordinator =
            CreateCoordinator();

        var handler =
            new RecoverDocumentArtifactsHandler(
                repository.Object,
                reconciliationHandler,
                storageService.Object,
                artifactEnumerator.Object,
                coordinator.Object,
                NullLogger<RecoverDocumentArtifactsHandler>.Instance);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new RecoverDocumentArtifactsCommand(),
                    cancellationTokenSource.Token));

        // Assert
        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.EnumerateIncrementallyAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.ExistsForDocument(
                It.IsAny<Guid>()),
            Times.Never);

        coordinator.Verify(
            x => x.AcquireAsync(
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
    public async Task HandleAsync_WhenDocumentAppearsAfterReconciliation_PreservesArtifact()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{documentId}.dvault"));

        Document persistedDocument =
            Document.Create(
                documentId,
                "document.txt",
                "Persisted Document",
                "sha256-test-hash",
                artifactPath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                []);

        repository
            .SetupSequence(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null)
            .ReturnsAsync(persistedDocument);

        var artifactEnumerator =
            CreateArtifactEnumerator([artifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        var lease =
            new Mock<IAsyncDisposable>();

        coordinator
            .Setup(x => x.AcquireAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease.Object);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                artifactCoordinator: coordinator);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        coordinator.Verify(
            x => x.AcquireAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.ExistsForDocument(
                It.IsAny<Guid>()),
            Times.Never);

        lease.Verify(
            x => x.DisposeAsync(),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCoordinationAcquisitionFails_DoesNotRevalidateOrDeleteArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator([orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        var logger =
            new RecordingLogger<RecoverDocumentArtifactsHandler>();

        coordinator
            .Setup(x => x.AcquireAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(CreateSensitiveDetails()));

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                artifactCoordinator: coordinator,
                logger: logger);

        // Act
        await Assert.ThrowsAsync<IOException>(
            () => handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken));

        // Assert
        coordinator.Verify(
            x => x.AcquireAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The only metadata read is the initial reconciliation read.
        repository.Verify(
            x => x.GetByIdAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.ExistsForDocument(
                It.IsAny<Guid>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
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
    public async Task HandleAsync_WhenCoordinationAcquisitionIsCancelled_DoesNotRevalidateOrDeleteArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator([orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        coordinator
            .Setup(x => x.AcquireAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new OperationCanceledException());

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                artifactCoordinator: coordinator);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new RecoverDocumentArtifactsCommand(),
                TestContext.Current.CancellationToken));

        // Assert
        coordinator.Verify(
            x => x.AcquireAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetByIdAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.ExistsForDocument(
                It.IsAny<Guid>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RecoverDocumentArtifactsHandler CreateHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader,
        Mock<IStorageService> storageService,
        Mock<IDocumentArtifactCoordinator>? artifactCoordinator = null,
        ILogger<RecoverDocumentArtifactsHandler>? logger = null)
    {
        ReconcileDocumentArtifactsHandler reconciliationHandler =
            CreateReconciliationHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        Mock<IDocumentArtifactCoordinator> coordinator =
            artifactCoordinator ?? CreateCoordinator();

        return new RecoverDocumentArtifactsHandler(
            repository.Object,
            reconciliationHandler,
            storageService.Object,
            artifactEnumerator.Object,
            coordinator.Object,
            logger ?? NullLogger<RecoverDocumentArtifactsHandler>.Instance);
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

    private static ReconcileDocumentArtifactsHandler CreateReconciliationHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader,
        Mock<IStorageService> storageService)
    {
        return new ReconcileDocumentArtifactsHandler(
            repository.Object,
            artifactEnumerator.Object,
            reader.Object,
            storageService.Object,
            new DocumentProcessingOptions(),
            NullLogger<ReconcileDocumentArtifactsHandler>.Instance);
    }

    private static Mock<IDocumentRepository> CreateRepository(
        IReadOnlyList<Document> documents)
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (DocumentPageCursor? cursor,
                 int pageSize,
                 CancellationToken _) =>
                {
                    IEnumerable<Document> query = documents;

                    if (cursor is not null)
                    {
                        string cursorDocumentId =
                            cursor.DocumentId.ToString();

                        query = query.Where(document =>
                            document.ImportedAt < cursor.ImportedAt ||
                            (document.ImportedAt == cursor.ImportedAt &&
                             string.CompareOrdinal(
                                 document.Id.ToString(),
                                 cursorDocumentId) > 0));
                    }

                    IReadOnlyList<Document> page = [.. query
                        .OrderByDescending(
                            document => document.ImportedAt)
                        .ThenBy(
                            document => document.Id.ToString(),
                            StringComparer.Ordinal)
                        .Take(pageSize)];

                    return Task.FromResult(page);
                });

        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (Guid documentId, CancellationToken _) =>
                    Task.FromResult<Document?>(
                        documents.FirstOrDefault(
                            document => document.Id == documentId)));

        return repository;
    }

    private static Mock<IDocumentArtifactEnumerator> CreateArtifactEnumerator(
        IReadOnlyList<string> artifactPaths)
    {
        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.ExistsForDocument(
                It.IsAny<Guid>()))
            .Returns(
                (Guid documentId) =>
                    artifactPaths.Any(
                        path =>
                            Guid.TryParse(
                                Path.GetFileNameWithoutExtension(path),
                                out Guid artifactId)
                            && artifactId == documentId));

        artifactEnumerator
            .Setup(x => x.EnumerateIncrementallyAsync(
                It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken cancellationToken) =>
                    EnumerateArtifactPathsAsync(
                        artifactPaths,
                        cancellationToken));

        return artifactEnumerator;
    }

    private static async IAsyncEnumerable<string> EnumerateArtifactPathsAsync(
        IReadOnlyList<string> artifactPaths,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        foreach (string artifactPath in artifactPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return artifactPath;
        }
    }

    private static Mock<IStorageService> CreateStorageServiceMock()
    {
        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()))
            .Returns(true);

        return storageService;
    }

    private static Document CreateDocument()
    {
        Guid documentId =
            Guid.NewGuid();

        return Document.Create(
            documentId,
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            Path.Combine(
                "Documents",
                $"{documentId}.dvault"));
    }
}

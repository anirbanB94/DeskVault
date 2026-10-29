using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Tests.TestDoubles;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security;
using System.Security.Cryptography;

namespace DeskVault.Application.Tests;

public sealed class ReconcileDocumentArtifactsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenArtifactMatchesDocument_ReturnsMatched()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [document.StoredFilePath]);

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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                expectedArtifactPath),
            Times.Exactly(2));

        reader.Verify(
            x => x.ValidateContentHashAsync(
                document.Id,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactIsMissing_ReturnsMissingArtifact()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, Array.Empty<string>());

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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.MissingArtifact,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                expectedArtifactPath),
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
    public async Task HandleAsync_WhenArtifactHasNoDocument_ReturnsOrphanedArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.Combine(
                "Documents",
                $"{orphanId}.dvault");

        string expectedArtifactPath =
            Path.GetFullPath(
                orphanPath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [orphanPath]);

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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            orphanId,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactCannotBeRead_ReturnsUnreadableArtifact()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [document.StoredFilePath]);

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
                    SafeDiagnosticLogAssertions.CreateSensitiveDetails()));

        var storageService =
            CreateStorageServiceMock();

        var logger =
            new RecordingLogger<ReconcileDocumentArtifactsHandler>();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                logger);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        SafeDiagnosticLogAssertions.AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Warning,
            DiagnosticOperationCodes.DocumentArtifactReadValidationFailure,
            LogMessages.DocumentArtifactValidationFailed,
            nameof(CryptographicException));

        var readFailureEntry = Assert.Single(
            logger.Entries,
            entry => entry.Properties.ContainsKey("FailureCategory"));

        Assert.Equal(
            DeskVault.Shared.Extensions.DiagnosticFailureCategory.CryptographicFailure,
            Assert.IsType<DeskVault.Shared.Extensions.DiagnosticFailureCategory>(
                readFailureEntry.Properties["FailureCategory"]));
        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                expectedArtifactPath),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenOneArtifactExceedsValidationLimit_ReportsUnreadableAndContinues()
    {
        // Arrange
        Document oversizedDocument =
            CreateDocument();

        Document validDocument =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                oversizedDocument,
                validDocument
            ]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [oversizedDocument.StoredFilePath, validDocument.StoredFilePath]);

        long maximumPlaintextBytes =
            DocumentProcessingOptions.DefaultMaxDecryptedDocumentBytes;

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                oversizedDocument.Id,
                oversizedDocument.Sha256Hash,
                maximumPlaintextBytes,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ResourceLimitExceededException(
                    maximumPlaintextBytes,
                    maximumPlaintextBytes + 1));

        reader
            .Setup(x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                maximumPlaintextBytes,
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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery(),
                TestContext.Current.CancellationToken);

        // Assert
        var oversizedFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    oversizedDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            oversizedFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            oversizedFinding.RecoveryAction);

        var validFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    validDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            validFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            validFinding.RecoveryAction);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                oversizedDocument.Id,
                oversizedDocument.Sha256Hash,
                maximumPlaintextBytes,
                It.IsAny<CancellationToken>()),
            Times.Once);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                maximumPlaintextBytes,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenValidationThrowsSecurityException_ReportsUnreadableAndContinues()
    {
        // Arrange
        Document inaccessibleDocument =
            CreateDocument();

        Document validDocument =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                inaccessibleDocument,
            validDocument
            ]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(
            artifactEnumerator,
            [
                inaccessibleDocument.StoredFilePath,
            validDocument.StoredFilePath
            ]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                inaccessibleDocument.Id,
                inaccessibleDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new System.Security.SecurityException(
                    SafeDiagnosticLogAssertions.CreateSensitiveDetails()));

        reader
            .Setup(x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            CreateStorageServiceMock();

        var logger =
            new RecordingLogger<ReconcileDocumentArtifactsHandler>();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                logger);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery(),
                TestContext.Current.CancellationToken);

        SafeDiagnosticLogAssertions.AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Warning,
            DiagnosticOperationCodes.DocumentArtifactReadValidationFailure,
            LogMessages.DocumentArtifactValidationFailed,
            nameof(SecurityException));

        // Assert
        var inaccessibleFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    inaccessibleDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            inaccessibleFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            inaccessibleFinding.RecoveryAction);

        var validFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    validDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            validFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            validFinding.RecoveryAction);

        var failureEntry =
            Assert.Single(
                logger.Entries,
                entry => entry.Properties.ContainsKey("FailureCategory"));

        Assert.Equal(
            DiagnosticFailureCategory.AccessDenied,
            Assert.IsType<DiagnosticFailureCategory>(
                failureEntry.Properties["FailureCategory"]));

        reader.Verify(
            x => x.ValidateContentHashAsync(
                inaccessibleDocument.Id,
                inaccessibleDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCanonicalArtifactCannotBeAccessed_ReportsUnreadableAndContinues()
    {
        // Arrange
        Document inaccessibleDocument =
            CreateDocument();

        Document validDocument =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                inaccessibleDocument,
            validDocument
            ]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(
            artifactEnumerator,
            [validDocument.StoredFilePath]);

        artifactEnumerator
            .Setup(x => x.ExistsForDocument(
                inaccessibleDocument.Id))
            .Throws(
                new UnauthorizedAccessException(
                    "Sensitive filesystem details."));

        artifactEnumerator
            .Setup(x => x.ExistsForDocument(
                validDocument.Id))
            .Returns(true);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                DocumentProcessingOptions.DefaultMaxDecryptedDocumentBytes,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            CreateStorageServiceMock();

        var logger =
            new RecordingLogger<ReconcileDocumentArtifactsHandler>();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                logger);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery(),
                TestContext.Current.CancellationToken);

        // Assert
        var inaccessibleFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    inaccessibleDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            inaccessibleFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            inaccessibleFinding.RecoveryAction);

        var validFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    validDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            validFinding.Status);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                inaccessibleDocument.Id,
                inaccessibleDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStoredPathDoesNotMatchDocumentIdentity_ReturnsPathMismatch()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        Guid differentArtifactId =
            Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                Path.Combine(
                    "Documents",
                    $"{differentArtifactId}.dvault"));

        string storedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, Array.Empty<string>());

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath))
            .Returns(false);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.PathMismatch,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            storedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath),
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
    public async Task HandleAsync_WhenStoredPathUsesDifferentDirectoryWithSameDocumentIdentity_ReturnsPathMismatch()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        string storedArtifactPath =
            Path.GetFullPath(
                Path.Combine(
                    "Outside",
                    $"{documentId}.dvault"));

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                storedArtifactPath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, Array.Empty<string>());

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath))
            .Returns(false);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.PathMismatch,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            storedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath),
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
    public async Task HandleAsync_WhenCancellationIsAlreadyRequested_ThrowsAndDoesNotAccessDependencies()
    {
        // Arrange
        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

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

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new ReconcileDocumentArtifactsQuery(),
                    cancellationTokenSource.Token));

        // Assert
        repository.Verify(
            x => x.GetAllAsync(
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

        repository.Verify(
            x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMismatchedStoredPathReferencesOrphanedArtifact_ReportsBothFindings()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        Guid orphanArtifactId =
            Guid.NewGuid();

        string orphanArtifactPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanArtifactId}.dvault"));

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Mismatched Path Document",
                "sha256-test-hash",
                orphanArtifactPath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [orphanArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                documentId,
                orphanArtifactPath))
            .Returns(false);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            2,
            result.Findings.Count);

        DocumentArtifactReconciliationResult pathMismatch =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.Status ==
                    DocumentArtifactReconciliationStatus.PathMismatch);

        Assert.Equal(
            documentId,
            pathMismatch.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            pathMismatch.RecoveryAction);

        DocumentArtifactReconciliationResult orphanFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.Status ==
                    DocumentArtifactReconciliationStatus.OrphanedArtifact);

        Assert.Equal(
            orphanArtifactId,
            orphanFinding.DocumentId);

        Assert.Equal(
            orphanArtifactPath,
            orphanFinding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            orphanFinding.RecoveryAction);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                documentId,
                orphanArtifactPath),
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
    public async Task HandleAsync_WhenStoredPathCannotBeNormalized_ReportsPathMismatchAndContinuesWithOtherDocuments()
    {
        // Arrange
        Guid malformedDocumentId =
            Guid.NewGuid();

        string malformedStoredPath =
            SafeDiagnosticLogAssertions.CreateSensitiveDetails() +
            "\0invalid.dvault";

        Document malformedDocument =
            Document.Create(
                malformedDocumentId,
                "malformed.txt",
                "Malformed Path Document",
                "sha256-test-hash",
                malformedStoredPath);

        Document validDocument =
            CreateDocument();

        string expectedValidArtifactPath =
            Path.GetFullPath(
                validDocument.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                malformedDocument,
                validDocument
            ]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [validDocument.StoredFilePath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            CreateStorageServiceMock();

        var logger =
            new RecordingLogger<ReconcileDocumentArtifactsHandler>();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                logger);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            2,
            result.Findings.Count);

        var malformedPathFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    malformedDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.PathMismatch,
            malformedPathFinding.Status);

        Assert.Equal(
            malformedStoredPath,
            malformedPathFinding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            malformedPathFinding.RecoveryAction);

        var validDocumentFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    validDocument.Id);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            validDocumentFinding.Status);

        Assert.Equal(
            expectedValidArtifactPath,
            validDocumentFinding.ArtifactPath);

        SafeDiagnosticLogAssertions.AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Warning,
            DiagnosticOperationCodes.DocumentArtifactPathNormalizationFailure,
            LogMessages.DocumentArtifactValidationFailed,
            nameof(ArgumentException));

        var pathFailureEntry = Assert.Single(
            logger.Entries,
            entry => entry.Properties.ContainsKey("FailureCategory"));

        Assert.Equal(
            DeskVault.Shared.Extensions.DiagnosticFailureCategory.InvalidData,
            Assert.IsType<DeskVault.Shared.Extensions.DiagnosticFailureCategory>(
                pathFailureEntry.Properties["FailureCategory"]));
        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                malformedDocument.Id,
                It.IsAny<string>()),
            Times.Never);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                malformedDocument.Id,
                malformedDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        reader.Verify(
            x => x.ValidateContentHashAsync(
                validDocument.Id,
                validDocument.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenReconcilingDocuments_DoesNotMutateRepository()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, Array.Empty<string>());

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
        await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenReadableArtifactContentMatchesDocumentHash_ReturnsMatched()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [document.StoredFilePath]);

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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);
    }

    [Fact]
    public async Task HandleAsync_WhenReadableArtifactContentDoesNotMatchDocumentHash_ReturnsContentMismatch()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        ConfigureDocumentLookup(repository);

        repository
            .Setup(x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(artifactEnumerator, [document.StoredFilePath]);

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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(new ReconcileDocumentArtifactsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.ContentMismatch,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenVaultExceedsSingleRepositoryPage_ReconcilesEveryDocumentWithoutFullInventoryCalls()
    {
        // Arrange
        const int additionalDocuments = 4;
        int documentCount =
            IDocumentRepository.MaximumDocumentPageSize + additionalDocuments;

        DateTime importedAt =
            new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Document[] documents =
            Enumerable.Range(0, documentCount)
                .Select(index =>
                {
                    Guid documentId =
                        Guid.Parse(
                            $"00000000-0000-0000-0000-{index + 1:D12}");

                    string artifactPath =
                        Path.GetFullPath(
                            Path.Combine(
                                "Documents",
                                $"{documentId}.dvault"));

                    return Document.Restore(
                        documentId,
                        $"document-{index}.txt",
                        $"Document {index}",
                        $"sha256-test-hash-{index}",
                        artifactPath,
                        importedAt.AddTicks(index),
                        DocumentStatus.Imported);
                })
                .ToArray();

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

                    IReadOnlyList<Document> page = query
                        .OrderByDescending(
                            document => document.ImportedAt)
                        .ThenBy(
                            document => document.Id.ToString(),
                            StringComparer.Ordinal)
                        .Take(pageSize)
                        .ToArray();

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

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        ConfigureArtifactEnumeratorMock(
            artifactEnumerator,
            documents
                .Select(document => document.StoredFilePath)
                .ToArray());

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.ValidateContentHashAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(documentCount, result.Findings.Count);

        Assert.All(
            result.Findings,
            finding =>
                Assert.Equal(
                    DocumentArtifactReconciliationStatus.Matched,
                    finding.Status));

        repository.Verify(
            x => x.GetPageAsync(
                It.Is<DocumentPageCursor?>(
                    cursor => cursor == null),
                IDocumentRepository.MaximumDocumentPageSize,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetPageAsync(
                It.Is<DocumentPageCursor?>(
                    cursor => cursor != null),
                IDocumentRepository.MaximumDocumentPageSize,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetPageAsync(
                It.IsAny<DocumentPageCursor?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

    }

    private static ReconcileDocumentArtifactsHandler CreateHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader,
        Mock<IStorageService> storageService,
        RecordingLogger<ReconcileDocumentArtifactsHandler>? logger = null)
    {
        return new ReconcileDocumentArtifactsHandler(
            repository.Object,
            artifactEnumerator.Object,
            reader.Object,
            storageService.Object,
            new DocumentProcessingOptions(),
            (ILogger<ReconcileDocumentArtifactsHandler>?)logger ?? NullLogger<ReconcileDocumentArtifactsHandler>.Instance);
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

    private static void ConfigureArtifactEnumeratorMock(
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        IReadOnlyList<string> artifactPaths)
    {

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
    }

    private static async IAsyncEnumerable<string> EnumerateArtifactPathsAsync(
        IReadOnlyList<string> artifactPaths,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (string artifactPath in artifactPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return artifactPath;
        }
    }

    private static void ConfigureDocumentLookup(
        Mock<IDocumentRepository> repository)
    {
        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (Guid documentId, CancellationToken cancellationToken) =>
                    FindDocumentAsync(
                        repository.Object,
                        documentId,
                        cancellationToken));
    }

    private static async Task<Document?> FindDocumentAsync(
        IDocumentRepository repository,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        const int pageSize =
            IDocumentRepository.MaximumDocumentPageSize;

        DocumentPageCursor? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Document> documents =
                await repository.GetPageAsync(
                    cursor,
                    pageSize,
                    cancellationToken);

            Document? document =
                documents.FirstOrDefault(
                    item => item.Id == documentId);

            if (document is not null)
            {
                return document;
            }

            if (documents.Count < pageSize)
            {
                return null;
            }

            Document lastDocument = documents[^1];

            cursor = new DocumentPageCursor(
                lastDocument.ImportedAt,
                lastDocument.Id);
        }
    }
}

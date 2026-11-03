using System.Security.Cryptography;
using DeskVault.Application.Security.Sessions;
using DeskVault.Infrastructure.Services;
using Moq;

namespace DeskVault.Infrastructure.Tests;

public sealed class SessionAuthorizedKeyServicesTests
{
    [Fact]
    public async Task DocumentKeyService_WhenSessionIsAuthenticated_DelegatesRetrievalAndForwardsCancellationToken()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        var innerServiceMock =
            new Mock<IEncryptionKeyService>(
                MockBehavior.Strict);

        innerServiceMock
            .Setup(service => service.GetKeyAsync(cancellationToken))
            .ReturnsAsync(keyMaterial);

        var sut =
            new SessionAuthorizedEncryptionKeyService(
                innerServiceMock.Object,
                session);

        try
        {
            // Act
            EncryptionKeyMaterial returnedMaterial =
                await sut.GetKeyAsync(cancellationToken);

            // Assert
            Assert.Same(
                keyMaterial,
                returnedMaterial);

            innerServiceMock.Verify(
                service => service.GetKeyAsync(cancellationToken),
                Times.Once);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                keyMaterial.KeyBytes);
        }
    }

    [Fact]
    public async Task DocumentKeyService_WhenSessionIsNotAuthenticated_DoesNotDelegateRetrieval()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var innerServiceMock =
            new Mock<IEncryptionKeyService>(
                MockBehavior.Strict);

        var sut =
            new SessionAuthorizedEncryptionKeyService(
                innerServiceMock.Object,
                session);

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.GetKeyAsync(
                TestContext.Current.CancellationToken));

        // Assert
        innerServiceMock.Verify(
            service => service.GetKeyAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DatabaseKeyService_WhenSessionIsAuthenticated_DelegatesRetrievalAndForwardsCancellationToken()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        var innerServiceMock =
            new Mock<IDatabaseEncryptionKeyService>(
                MockBehavior.Strict);

        innerServiceMock
            .Setup(service => service.GetKeyAsync(cancellationToken))
            .ReturnsAsync(keyMaterial);

        var sut =
            new SessionAuthorizedDatabaseEncryptionKeyService(
                innerServiceMock.Object,
                session);

        try
        {
            // Act
            EncryptionKeyMaterial returnedMaterial =
                await sut.GetKeyAsync(cancellationToken);

            // Assert
            Assert.Same(
                keyMaterial,
                returnedMaterial);

            innerServiceMock.Verify(
                service => service.GetKeyAsync(cancellationToken),
                Times.Once);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                keyMaterial.KeyBytes);
        }
    }

    [Fact]
    public async Task DatabaseKeyService_WhenSessionIsNotAuthenticated_DoesNotDelegateRetrieval()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var innerServiceMock =
            new Mock<IDatabaseEncryptionKeyService>(
                MockBehavior.Strict);

        var sut =
            new SessionAuthorizedDatabaseEncryptionKeyService(
                innerServiceMock.Object,
                session);

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.GetKeyAsync(
                TestContext.Current.CancellationToken));

        // Assert
        innerServiceMock.Verify(
            service => service.GetKeyAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DocumentKeyService_WhenSessionLocksDuringRetrieval_DoesNotReturnAndClearsMaterial()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        var retrievalCompletionSource =
            new TaskCompletionSource<EncryptionKeyMaterial>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var innerServiceMock =
            new Mock<IEncryptionKeyService>(
                MockBehavior.Strict);

        innerServiceMock
            .Setup(service => service.GetKeyAsync(
                It.IsAny<CancellationToken>()))
            .Returns(retrievalCompletionSource.Task);

        var sut =
            new SessionAuthorizedEncryptionKeyService(
                innerServiceMock.Object,
                session);

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        try
        {
            // Act
            Task<EncryptionKeyMaterial> retrievalTask =
                sut.GetKeyAsync(TestContext.Current.CancellationToken);

            innerServiceMock.Verify(
                service => service.GetKeyAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);

            session.Lock();

            retrievalCompletionSource.SetResult(keyMaterial);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => retrievalTask);

            // Assert
            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                session.CurrentSnapshot.State);

            Assert.All(
                keyMaterial.KeyBytes,
                value => Assert.Equal((byte)0, value));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                keyMaterial.KeyBytes);
        }
    }

    [Fact]
    public async Task DatabaseKeyService_WhenSessionLocksDuringRetrieval_DoesNotReturnAndClearsMaterial()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        var retrievalCompletionSource =
            new TaskCompletionSource<EncryptionKeyMaterial>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var innerServiceMock =
            new Mock<IDatabaseEncryptionKeyService>(
                MockBehavior.Strict);

        innerServiceMock
            .Setup(service => service.GetKeyAsync(
                It.IsAny<CancellationToken>()))
            .Returns(retrievalCompletionSource.Task);

        var sut =
            new SessionAuthorizedDatabaseEncryptionKeyService(
                innerServiceMock.Object,
                session);

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        try
        {
            // Act
            Task<EncryptionKeyMaterial> retrievalTask =
                sut.GetKeyAsync(TestContext.Current.CancellationToken);

            innerServiceMock.Verify(
                service => service.GetKeyAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);

            session.Lock();

            retrievalCompletionSource.SetResult(keyMaterial);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => retrievalTask);

            // Assert
            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                session.CurrentSnapshot.State);

            Assert.All(
                keyMaterial.KeyBytes,
                value => Assert.Equal((byte)0, value));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                keyMaterial.KeyBytes);
        }
    }

    private static ApplicationSecuritySession CreateAuthenticatedSession()
    {
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));

        return session;
    }

    private static EncryptionKeyMaterial CreateKeyMaterial()
    {
        return new EncryptionKeyMaterial(
            RandomNumberGenerator.GetBytes(
                EncryptionKeyMaterial.KeySizeBytes),
            Guid.NewGuid(),
            keyVersion: 1);
    }
}

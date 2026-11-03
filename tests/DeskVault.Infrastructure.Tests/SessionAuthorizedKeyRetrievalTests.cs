using System.Security.Cryptography;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Infrastructure.Services;
using Moq;

namespace DeskVault.Infrastructure.Tests;

public sealed class SessionAuthorizedKeyRetrievalTests
{
    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Authenticating)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    [InlineData(ApplicationSecuritySessionState.Unavailable)]
    public async Task GetKeyAsync_WhenSessionIsNotAuthenticated_DoesNotInvokeRetrieval(
        ApplicationSecuritySessionState state)
    {
        // Arrange
        var sessionMock =
            new Mock<IApplicationSecuritySession>(
                MockBehavior.Strict);

        sessionMock
            .SetupGet(session => session.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    state,
                    revision: 1));

        int retrievalCallCount = 0;

        Func<CancellationToken, Task<EncryptionKeyMaterial>> retrieveKeyAsync =
            _ =>
            {
                retrievalCallCount++;

                return Task.FromException<EncryptionKeyMaterial>(
                    new InvalidOperationException(
                        "Retrieval must not be invoked for an unauthorized session."));
            };

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => SessionAuthorizedKeyRetrieval.GetKeyAsync(
                sessionMock.Object,
                retrieveKeyAsync,
                CancellationToken.None));

        // Assert
        Assert.Equal(0, retrievalCallCount);

        sessionMock.VerifyGet(
            session => session.CurrentSnapshot,
            Times.Once);
    }

    [Fact]
    public async Task GetKeyAsync_WhenSessionRemainsAuthenticated_ReturnsRetrievedMaterial()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        try
        {
            // Act
            EncryptionKeyMaterial returnedMaterial =
                await SessionAuthorizedKeyRetrieval.GetKeyAsync(
                    session,
                    _ => Task.FromResult(keyMaterial),
                    CancellationToken.None);

            // Assert
            Assert.Same(
                keyMaterial,
                returnedMaterial);

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                session.CurrentSnapshot.State);

            Assert.Equal(
                2,
                session.CurrentSnapshot.Revision);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                keyMaterial.KeyBytes);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenSessionLocksDuringRetrieval_DoesNotReturnAndClearsMaterial()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        var retrievalCompletion =
            new TaskCompletionSource<EncryptionKeyMaterial>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<EncryptionKeyMaterial> retrievalTask =
            SessionAuthorizedKeyRetrieval.GetKeyAsync(
                session,
                _ => retrievalCompletion.Task,
                CancellationToken.None);

        try
        {
            // Act
            session.Lock();

            retrievalCompletion.SetResult(
                keyMaterial);

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
    public async Task GetKeyAsync_WhenSessionLocksAndReauthenticatesDuringRetrieval_DoesNotReturnAndClearsMaterial()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        ApplicationSecuritySessionSnapshot originalSnapshot =
            session.CurrentSnapshot;

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        var retrievalCompletion =
            new TaskCompletionSource<EncryptionKeyMaterial>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<EncryptionKeyMaterial> retrievalTask =
            SessionAuthorizedKeyRetrieval.GetKeyAsync(
                session,
                _ => retrievalCompletion.Task,
                CancellationToken.None);

        try
        {
            // Act
            session.Lock();

            Authenticate(session);

            retrievalCompletion.SetResult(
                keyMaterial);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => retrievalTask);

            // Assert
            ApplicationSecuritySessionSnapshot currentSnapshot =
                session.CurrentSnapshot;

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                currentSnapshot.State);

            Assert.True(
                currentSnapshot.Revision > originalSnapshot.Revision);

            Assert.NotEqual(
                originalSnapshot.Revision,
                currentSnapshot.Revision);

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
    public async Task GetKeyAsync_WhenCancelledAfterMaterialIsAcquired_ClearsMaterial()
    {
        // Arrange
        ApplicationSecuritySession session =
            CreateAuthenticatedSession();

        EncryptionKeyMaterial keyMaterial =
            CreateKeyMaterial();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => SessionAuthorizedKeyRetrieval.GetKeyAsync(
                    session,
                    _ =>
                    {
                        cancellationTokenSource.Cancel();

                        return Task.FromResult(keyMaterial);
                    },
                    cancellationTokenSource.Token));

            // Assert
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

        Authenticate(session);

        return session;
    }

    private static void Authenticate(
        ApplicationSecuritySession session)
    {
        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));
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

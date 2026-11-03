using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Hosting;

namespace DeskVault.UI.Tests;

public sealed class DeskVaultStartupCoordinatorTests
{
    [Fact]
    public async Task AuthenticateAsync_WhenSessionBecomesAuthenticated_ReturnsTrue()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        Func<CancellationToken, Task> authenticate =
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                ApplicationSecuritySessionAttempt attempt =
                    session.BeginAuthentication();

                bool accepted =
                    session.TryCompleteAuthentication(
                        attempt,
                        ApplicationAuthenticationOutcome.Succeeded);

                Assert.True(accepted);

                return Task.CompletedTask;
            };

        var coordinator = new DeskVaultStartupCoordinator(
            session,
            authenticate,
            _ => Task.CompletedTask);

        // Act
        bool result =
            await coordinator.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);

        Assert.True(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenSessionIsNotAuthenticated_ReturnsFalse()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        var coordinator = new DeskVaultStartupCoordinator(
            session,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);

        // Act
        bool result =
            await coordinator.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenCancellationRacesWithSuccessfulAuthentication_WithdrawsAccess()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        using var cancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        Func<CancellationToken, Task> authenticate =
            _ =>
            {
                ApplicationSecuritySessionAttempt attempt =
                    session.BeginAuthentication();

                bool accepted =
                    session.TryCompleteAuthentication(
                        attempt,
                        ApplicationAuthenticationOutcome.Succeeded);

                Assert.True(accepted);

                cancellationSource.Cancel();

                return Task.CompletedTask;
            };

        var coordinator = new DeskVaultStartupCoordinator(
            session,
            authenticate,
            _ => Task.CompletedTask);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                coordinator.AuthenticateAsync(
                    cancellationSource.Token));

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Authenticating)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    [InlineData(ApplicationSecuritySessionState.Unavailable)]
    public async Task InitializeDatabaseAsync_WhenProtectedAccessIsUnavailable_DoesNotInitializeDatabase(
        ApplicationSecuritySessionState initialState)
    {
        // Arrange
        var session = CreateSession(initialState);
        int initializerInvocationCount = 0;

        var coordinator = new DeskVaultStartupCoordinator(
            session,
            _ => Task.CompletedTask,
            _ =>
            {
                initializerInvocationCount++;
                return Task.CompletedTask;
            });

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                coordinator.InitializeDatabaseAsync(
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, initializerInvocationCount);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_WhenSessionIsAuthenticated_InvokesInitializer()
    {
        // Arrange
        var session =
            CreateSession(ApplicationSecuritySessionState.Authenticated);

        int initializerInvocationCount = 0;
        CancellationToken receivedToken = default;

        var coordinator = new DeskVaultStartupCoordinator(
            session,
            _ => Task.CompletedTask,
            cancellationToken =>
            {
                initializerInvocationCount++;
                receivedToken = cancellationToken;
                return Task.CompletedTask;
            });

        using var cancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        // Act
        await coordinator.InitializeDatabaseAsync(
            cancellationSource.Token);

        // Assert
        Assert.Equal(1, initializerInvocationCount);
        Assert.Equal(cancellationSource.Token, receivedToken);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_WhenInitializerFails_PropagatesFailure()
    {
        // Arrange
        var session =
            CreateSession(ApplicationSecuritySessionState.Authenticated);

        var expectedException =
            new IOException("Database initialization test failure.");

        var coordinator = new DeskVaultStartupCoordinator(
            session,
            _ => Task.CompletedTask,
            _ => Task.FromException(expectedException));

        // Act
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                () =>
                    coordinator.InitializeDatabaseAsync(
                        TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, actualException);
    }

    private static ApplicationSecuritySession CreateSession(
        ApplicationSecuritySessionState state)
    {
        var session = new ApplicationSecuritySession();

        switch (state)
        {
            case ApplicationSecuritySessionState.Unauthenticated:
                break;

            case ApplicationSecuritySessionState.Authenticating:
                session.BeginAuthentication();
                break;

            case ApplicationSecuritySessionState.Authenticated:
                ApplicationSecuritySessionAttempt attempt =
                    session.BeginAuthentication();

                bool accepted =
                    session.TryCompleteAuthentication(
                        attempt,
                        ApplicationAuthenticationOutcome.Succeeded);

                Assert.True(accepted);
                break;

            case ApplicationSecuritySessionState.Locked:
                session.Lock();
                break;

            case ApplicationSecuritySessionState.Unavailable:
                session.MarkUnavailable();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "Unsupported test security-session state.");
        }

        Assert.Equal(state, session.CurrentSnapshot.State);

        return session;
    }
}

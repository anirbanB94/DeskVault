using System.Collections.Concurrent;
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Tests;

public sealed class ApplicationSecuritySessionTests
{
    [Fact]
    public void Constructor_InitializesUnauthenticatedSession()
    {
        // Arrange

        // Act
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            snapshot.State);

        Assert.Equal(
            0,
            snapshot.Revision);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void BeginAuthentication_TransitionsToAuthenticating()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged +=
            notifications.Add;

        // Act
        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            snapshot.State);

        Assert.Equal(
            snapshot.Revision,
            attempt.Revision);

        Assert.Single(notifications);

        Assert.Same(
            snapshot,
            notifications[0]);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    public void TryCompleteAuthentication_WhenSucceeded_GrantsProtectedAccess(
        ApplicationSecuritySessionState initialState)
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        if (initialState == ApplicationSecuritySessionState.Locked)
        {
            session.Lock();
        }

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            snapshot.State);

        Assert.True(
            snapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            attempt.Revision + 1,
            snapshot.Revision);
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    public void TryCompleteAuthentication_WhenFailed_RestoresPreviousNonAuthorizedState(
        ApplicationSecuritySessionState initialState)
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        if (initialState == ApplicationSecuritySessionState.Locked)
        {
            session.Lock();
        }

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Failed);

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            initialState,
            snapshot.State);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            attempt.Revision + 1,
            snapshot.Revision);
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    public void TryCompleteAuthentication_WhenCancelled_RestoresPreviousNonAuthorizedState(
        ApplicationSecuritySessionState initialState)
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        if (initialState == ApplicationSecuritySessionState.Locked)
        {
            session.Lock();
        }

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Cancelled);

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            initialState,
            snapshot.State);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenUnavailable_MarksSessionUnavailable()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Unavailable);

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            snapshot.State);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Authenticating)]
    [InlineData(ApplicationSecuritySessionState.Authenticated)]
    [InlineData(ApplicationSecuritySessionState.Unavailable)]
    public void BeginAuthentication_WhenStateDoesNotAllowAuthentication_ThrowsInvalidOperationException(
        ApplicationSecuritySessionState state)
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        switch (state)
        {
            case ApplicationSecuritySessionState.Authenticating:
                session.BeginAuthentication();
                break;

            case ApplicationSecuritySessionState.Authenticated:
                ApplicationSecuritySessionAttempt successfulAttempt =
                    session.BeginAuthentication();

                session.TryCompleteAuthentication(
                    successfulAttempt,
                    ApplicationAuthenticationOutcome.Succeeded);
                break;

            case ApplicationSecuritySessionState.Unavailable:
                session.MarkUnavailable();
                break;
        }

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => session.BeginAuthentication());

        // Assert
        Assert.False(
            string.IsNullOrWhiteSpace(exception.Message));

        Assert.Equal(
            state,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void Lock_WhenAuthenticationIsInProgress_InvalidatesAttempt()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        ApplicationSecuritySessionSnapshot lockedSnapshot =
            session.Lock();

        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Assert
        Assert.False(accepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Assert.Equal(
            lockedSnapshot,
            session.CurrentSnapshot);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void TryLock_WhenExpectedAuthenticatedRevisionMatches_LocksSession()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));

        ApplicationSecuritySessionSnapshot authenticated =
            session.CurrentSnapshot;

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged += notifications.Add;

        // Act
        bool locked = session.TryLock(
            authenticated.Revision,
            out ApplicationSecuritySessionSnapshot snapshot);

        // Assert
        Assert.True(locked);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            snapshot.State);

        Assert.False(snapshot.IsProtectedAccessAvailable);

        Assert.Same(snapshot, session.CurrentSnapshot);

        Assert.Single(notifications);
        Assert.Same(snapshot, notifications[0]);
    }

    [Fact]
    public void TryLock_WhenExpectedRevisionIsStale_DoesNotLockNewAuthenticatedSession()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt firstAttempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                firstAttempt,
                ApplicationAuthenticationOutcome.Succeeded));

        long expiredRevision =
            session.CurrentSnapshot.Revision;

        session.Lock();

        ApplicationSecuritySessionAttempt secondAttempt =
            session.BeginAuthentication();

        Assert.True(
            session.TryCompleteAuthentication(
                secondAttempt,
                ApplicationAuthenticationOutcome.Succeeded));

        ApplicationSecuritySessionSnapshot newerAuthenticated =
            session.CurrentSnapshot;

        // Act
        bool locked = session.TryLock(
            expiredRevision,
            out ApplicationSecuritySessionSnapshot snapshot);

        // Assert
        Assert.False(locked);

        Assert.Same(newerAuthenticated, snapshot);
        Assert.Same(newerAuthenticated, session.CurrentSnapshot);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.True(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void Lock_WhenSessionIsAuthenticated_WithdrawsProtectedAccess()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        session.TryCompleteAuthentication(
            attempt,
            ApplicationAuthenticationOutcome.Succeeded);

        // Act
        ApplicationSecuritySessionSnapshot snapshot =
            session.Lock();

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            snapshot.State);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void Lock_WhenAlreadyLocked_IsIdempotent()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged +=
            notifications.Add;

        ApplicationSecuritySessionSnapshot firstSnapshot =
            session.Lock();

        // Act
        ApplicationSecuritySessionSnapshot secondSnapshot =
            session.Lock();

        // Assert
        Assert.Same(
            firstSnapshot,
            secondSnapshot);

        Assert.Equal(
            1,
            secondSnapshot.Revision);

        Assert.Single(notifications);
    }

    [Fact]
    public void MarkUnavailable_WhenAuthenticationIsInProgress_InvalidatesAttempt()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        ApplicationSecuritySessionSnapshot unavailableSnapshot =
            session.MarkUnavailable();

        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Assert
        Assert.False(accepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            unavailableSnapshot.State);

        Assert.Equal(
            unavailableSnapshot,
            session.CurrentSnapshot);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void MarkUnavailable_WhenAlreadyUnavailable_IsIdempotent()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged +=
            notifications.Add;

        ApplicationSecuritySessionSnapshot firstSnapshot =
            session.MarkUnavailable();

        // Act
        ApplicationSecuritySessionSnapshot secondSnapshot =
            session.MarkUnavailable();

        // Assert
        Assert.Same(
            firstSnapshot,
            secondSnapshot);

        Assert.Equal(
            1,
            secondSnapshot.Revision);

        Assert.Single(notifications);
    }

    [Fact]
    public void RestoreAvailability_WhenUnavailable_TransitionsToLocked()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        session.MarkUnavailable();

        // Act
        ApplicationSecuritySessionSnapshot snapshot =
            session.RestoreAvailability();

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            snapshot.State);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void RestoreAvailability_WhenNotUnavailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => session.RestoreAvailability());

        // Assert
        Assert.False(
            string.IsNullOrWhiteSpace(exception.Message));

        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void RestoreAvailability_RequiresNewSuccessfulAuthentication()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        session.MarkUnavailable();
        session.RestoreAvailability();

        // Act
        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);

        Assert.True(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenAttemptBelongsToAnotherSession_ReturnsFalse()
    {
        // Arrange
        var firstSession =
            new ApplicationSecuritySession();

        var secondSession =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            firstSession.BeginAuthentication();

        secondSession.BeginAuthentication();

        // Act
        bool accepted =
            secondSession.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Assert
        Assert.False(accepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            secondSession.CurrentSnapshot.State);

        Assert.False(
            secondSession.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenAttemptWasAlreadyCompleted_ReturnsFalse()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        bool firstCompletionAccepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Act
        bool secondCompletionAccepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Failed);

        // Assert
        Assert.True(firstCompletionAccepted);
        Assert.False(secondCompletionAccepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void Lock_WhenCalledConcurrently_ProducesOneLockedTransition()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged +=
            notifications.Add;

        // Act
        Parallel.For(
            0,
            64,
            _ => session.Lock());

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);

        Assert.Equal(
            1,
            session.CurrentSnapshot.Revision);

        Assert.Single(notifications);

        Assert.Equal(
            1,
            notifications[0].Revision);
    }

    [Fact]
    public void StateChanged_WhenHandlerRequestsAnotherTransition_PublishesNotificationsInRevisionOrder()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged += snapshot =>
        {
            notifications.Add(snapshot);

            if (snapshot.State ==
                ApplicationSecuritySessionState.Authenticated)
            {
                session.Lock();
            }
        };

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        // Act
        bool accepted =
            session.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            [
                ApplicationSecuritySessionState.Authenticating,
                ApplicationSecuritySessionState.Authenticated,
                ApplicationSecuritySessionState.Locked
            ],
            notifications.Select(snapshot => snapshot.State));

        Assert.Equal(
            [1L, 2L, 3L],
            notifications.Select(snapshot => snapshot.Revision));

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void StateChanged_WhenObserverThrows_ContinuesNotifyingOtherObservers()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new List<ApplicationSecuritySessionSnapshot>();

        session.StateChanged +=
            _ => throw new InvalidOperationException(
                "Observer failure.");

        session.StateChanged +=
            notifications.Add;

        // Act
        session.BeginAuthentication();

        // Assert
        Assert.Single(notifications);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            notifications[0].State);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenOutcomeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        var invalidOutcome =
            (ApplicationAuthenticationOutcome)999;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    session.TryCompleteAuthentication(
                        attempt,
                        invalidOutcome));

        // Assert
        Assert.Equal(
            "outcome",
            exception.ParamName);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenAttemptIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        // Act
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                () =>
                    session.TryCompleteAuthentication(
                        null!,
                        ApplicationAuthenticationOutcome.Succeeded));

        // Assert
        Assert.Equal(
            "attempt",
            exception.ParamName);

        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            session.CurrentSnapshot.State);
    }

    [Fact]
    public void BeginAuthentication_WhenCalledConcurrently_AllowsOnlyOneActiveAttempt()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt? acceptedAttempt =
            null;

        int acceptedCount = 0;
        int rejectedCount = 0;

        // Act
        Parallel.For(
            0,
            64,
            _ =>
            {
                try
                {
                    ApplicationSecuritySessionAttempt attempt =
                        session.BeginAuthentication();

                    Interlocked.CompareExchange(
                        ref acceptedAttempt,
                        attempt,
                        null);

                    Interlocked.Increment(
                        ref acceptedCount);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(
                        ref rejectedCount);
                }
            });

        ApplicationSecuritySessionSnapshot snapshot =
            session.CurrentSnapshot;

        // Assert
        Assert.Equal(1, acceptedCount);
        Assert.Equal(63, rejectedCount);
        Assert.NotNull(acceptedAttempt);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            snapshot.State);

        Assert.Equal(
            acceptedAttempt.Revision,
            snapshot.Revision);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenCompetingWithLock_CannotLeaveSessionAuthenticated()
    {
        // Arrange
        var session =
            new ApplicationSecuritySession();

        var notifications =
            new ConcurrentQueue<ApplicationSecuritySessionSnapshot>();

        session.StateChanged +=
            notifications.Enqueue;

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        using var barrier =
            new Barrier(participantCount: 2);

        bool completionAccepted = false;

        ApplicationSecuritySessionSnapshot? lockSnapshot = null;

        // Act
        Parallel.Invoke(
            () =>
            {
                barrier.SignalAndWait();

                completionAccepted =
                    session.TryCompleteAuthentication(
                        attempt,
                        ApplicationAuthenticationOutcome.Succeeded);
            },
            () =>
            {
                barrier.SignalAndWait();

                lockSnapshot =
                    session.Lock();
            });

        ApplicationSecuritySessionSnapshot currentSnapshot =
            session.CurrentSnapshot;

        ApplicationSecuritySessionSnapshot[] observedNotifications =
            notifications.ToArray();

        // Assert
        Assert.NotNull(lockSnapshot);

        Assert.Same(
            lockSnapshot,
            currentSnapshot);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            currentSnapshot.State);

        Assert.False(
            currentSnapshot.IsProtectedAccessAvailable);

        Assert.NotEmpty(observedNotifications);

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            observedNotifications[^1].State);

        Assert.Equal(
            currentSnapshot.Revision,
            observedNotifications[^1].Revision);

        Assert.True(
            observedNotifications
                .Zip(
                    observedNotifications.Skip(1),
                    (previous, next) =>
                        next.Revision > previous.Revision)
                .All(isIncreasing => isIncreasing));

        if (!completionAccepted)
        {
            Assert.DoesNotContain(
                observedNotifications,
                notification =>
                    notification.State ==
                    ApplicationSecuritySessionState.Authenticated);
        }
    }

    [Fact]
    public void Lock_WhenSessionIsUnavailable_PreservesUnavailableState()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        ApplicationSecuritySessionSnapshot unavailableSnapshot =
            session.MarkUnavailable();

        // Act
        ApplicationSecuritySessionSnapshot lockSnapshot =
            session.Lock();

        // Assert
        Assert.Same(unavailableSnapshot, lockSnapshot);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void MarkUnavailable_WhenSessionIsAuthenticated_WithdrawsProtectedAccess()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt attempt =
            session.BeginAuthentication();

        session.TryCompleteAuthentication(
            attempt,
            ApplicationAuthenticationOutcome.Succeeded);

        // Act
        ApplicationSecuritySessionSnapshot snapshot =
            session.MarkUnavailable();

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            snapshot.State);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void TryCompleteAuthentication_WhenEarlierAttemptCompletesDuringNewAttempt_ReturnsFalse()
    {
        // Arrange
        var session = new ApplicationSecuritySession();

        ApplicationSecuritySessionAttempt earlierAttempt =
            session.BeginAuthentication();

        session.Lock();

        ApplicationSecuritySessionAttempt currentAttempt =
            session.BeginAuthentication();

        // Act
        bool earlierAttemptAccepted =
            session.TryCompleteAuthentication(
                earlierAttempt,
                ApplicationAuthenticationOutcome.Succeeded);

        // Assert
        Assert.False(earlierAttemptAccepted);

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticating,
            session.CurrentSnapshot.State);

        Assert.True(
            session.TryCompleteAuthentication(
                currentAttempt,
                ApplicationAuthenticationOutcome.Succeeded));

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            session.CurrentSnapshot.State);
    }
}

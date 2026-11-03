using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Security.Authentication;

/// <summary>
/// Coordinates authentication-provider availability and verification
/// with the authoritative Application security session.
/// </summary>
public sealed class ApplicationAuthenticationService
    : IApplicationAuthenticationService
{
    private const string SafeProviderFailureMessage =
        "The application authentication provider failed.";

    private readonly IApplicationAuthenticationProvider _provider;
    private readonly IApplicationSecuritySession _session;
    private readonly ILogger<ApplicationAuthenticationService> _logger;

    private int _isAuthenticationInProgress;

    public ApplicationAuthenticationService(
        IApplicationAuthenticationProvider provider,
        IApplicationSecuritySession session,
        ILogger<ApplicationAuthenticationService> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(logger);

        _provider = provider;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApplicationAuthenticationServiceResult> AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(
                ref _isAuthenticationInProgress,
                1,
                0) != 0)
        {
            return ApplicationAuthenticationServiceResult.AlreadyInProgress;
        }

        try
        {
            ApplicationSecuritySessionState initialState =
                _session.CurrentSnapshot.State;

            if (initialState is not (
                ApplicationSecuritySessionState.Unauthenticated or
                ApplicationSecuritySessionState.Locked or
                ApplicationSecuritySessionState.Unavailable))
            {
                return ApplicationAuthenticationServiceResult.NotPermitted;
            }

            ApplicationAuthenticationAvailability availability =
                await CheckAvailabilityAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (availability !=
                ApplicationAuthenticationAvailability.Available)
            {
                return TryMarkSessionUnavailable()
                    ? ApplicationAuthenticationServiceResult.Unavailable
                    : ApplicationAuthenticationServiceResult.NotPermitted;
            }

            if (!TryPrepareSessionForAuthentication())
            {
                return ApplicationAuthenticationServiceResult.NotPermitted;
            }

            cancellationToken.ThrowIfCancellationRequested();

            ApplicationSecuritySessionAttempt attempt;

            try
            {
                attempt =
                    _session.BeginAuthentication();
            }
            catch (InvalidOperationException)
            {
                // The session may have changed while provider availability
                // was being checked. The authoritative session decides
                // whether another authentication attempt is permitted.
                return ApplicationAuthenticationServiceResult.NotPermitted;
            }

            using CancellationTokenRegistration cancellationRegistration =
                cancellationToken.Register(
                    static state =>
                    {
                        var registration =
                            (AuthenticationCancellationRegistration)state!;

                        registration.Session.TryCompleteAuthentication(
                            registration.Attempt,
                            ApplicationAuthenticationOutcome.Cancelled);
                    },
                    new AuthenticationCancellationRegistration(
                        _session,
                        attempt));

            ApplicationAuthenticationProviderResult providerResult;

            try
            {
                providerResult =
                    await RequestProviderAuthenticationAsync(
                        cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Explicitly complete cancellation as well as registering the
                // cancellation callback. This ensures the attempt is invalidated
                // before cancellation escapes to the caller, even when a provider
                // does not cooperate with cancellation.
                _session.TryCompleteAuthentication(
                    attempt,
                    ApplicationAuthenticationOutcome.Cancelled);

                throw;
            }

            ApplicationAuthenticationOutcome sessionOutcome =
                MapToSessionOutcome(providerResult);

            bool accepted =
                _session.TryCompleteAuthentication(
                    attempt,
                    sessionOutcome);

            if (!accepted)
            {
                return ApplicationAuthenticationServiceResult.Superseded;
            }

            // A state-change observer may have caused another transition
            // while the session dispatched the accepted completion.
            // Do not report authentication success when access has already
            // been withdrawn.
            if (sessionOutcome ==
                    ApplicationAuthenticationOutcome.Succeeded &&
                !_session.CurrentSnapshot.IsProtectedAccessAvailable)
            {
                return ApplicationAuthenticationServiceResult.Superseded;
            }

            return MapToServiceResult(providerResult);
        }
        finally
        {
            Volatile.Write(
                ref _isAuthenticationInProgress,
                0);
        }
    }

    private async Task<ApplicationAuthenticationAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        Task<ApplicationAuthenticationAvailability>? availabilityTask = null;

        try
        {
            availabilityTask =
                _provider.CheckAvailabilityAsync(cancellationToken);

            ApplicationAuthenticationAvailability availability =
                await availabilityTask.WaitAsync(cancellationToken);

            return availability ==
                    ApplicationAuthenticationAvailability.Available
                ? ApplicationAuthenticationAvailability.Available
                : ApplicationAuthenticationAvailability.Unavailable;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            if (availabilityTask is not null)
            {
                _ = ObserveLateProviderOperationAsync(availabilityTask);
            }

            throw;
        }
        catch (Exception exception)
        {
            LogProviderFailure(exception);

            return ApplicationAuthenticationAvailability.Unavailable;
        }
    }

    private async Task<ApplicationAuthenticationProviderResult> RequestProviderAuthenticationAsync(
        CancellationToken cancellationToken)
    {
        Task<ApplicationAuthenticationProviderResult>? authenticationTask = null;

        try
        {
            authenticationTask =
                _provider.RequestAuthenticationAsync(cancellationToken);

            return await authenticationTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            if (authenticationTask is not null)
            {
                _ = ObserveLateProviderOperationAsync(authenticationTask);
            }

            throw;
        }
        catch (Exception exception)
        {
            LogProviderFailure(exception);

            return ApplicationAuthenticationProviderResult.Unavailable;
        }
    }

    private bool TryMarkSessionUnavailable()
    {
        ApplicationSecuritySessionState currentState =
            _session.CurrentSnapshot.State;

        if (currentState is not (
            ApplicationSecuritySessionState.Unauthenticated or
            ApplicationSecuritySessionState.Locked or
            ApplicationSecuritySessionState.Unavailable))
        {
            return false;
        }

        _session.MarkUnavailable();

        return _session.CurrentSnapshot.State ==
            ApplicationSecuritySessionState.Unavailable;
    }

    private bool TryPrepareSessionForAuthentication()
    {
        ApplicationSecuritySessionState currentState =
            _session.CurrentSnapshot.State;

        if (currentState ==
            ApplicationSecuritySessionState.Unavailable)
        {
            try
            {
                _session.RestoreAvailability();
            }
            catch (InvalidOperationException)
            {
                // Another session transition may have occurred after
                // the snapshot was read. Re-read the authoritative state.
            }

            currentState =
                _session.CurrentSnapshot.State;
        }

        return currentState is
            ApplicationSecuritySessionState.Unauthenticated or
            ApplicationSecuritySessionState.Locked;
    }

    private static ApplicationAuthenticationOutcome MapToSessionOutcome(
        ApplicationAuthenticationProviderResult providerResult)
    {
        return providerResult switch
        {
            ApplicationAuthenticationProviderResult.Succeeded =>
                ApplicationAuthenticationOutcome.Succeeded,

            ApplicationAuthenticationProviderResult.Failed =>
                ApplicationAuthenticationOutcome.Failed,

            ApplicationAuthenticationProviderResult.Cancelled =>
                ApplicationAuthenticationOutcome.Cancelled,

            ApplicationAuthenticationProviderResult.Unavailable =>
                ApplicationAuthenticationOutcome.Unavailable,

            _ =>
                ApplicationAuthenticationOutcome.Unavailable
        };
    }

    private static ApplicationAuthenticationServiceResult MapToServiceResult(
        ApplicationAuthenticationProviderResult providerResult)
    {
        return providerResult switch
        {
            ApplicationAuthenticationProviderResult.Succeeded =>
                ApplicationAuthenticationServiceResult.Succeeded,

            ApplicationAuthenticationProviderResult.Failed =>
                ApplicationAuthenticationServiceResult.Failed,

            ApplicationAuthenticationProviderResult.Cancelled =>
                ApplicationAuthenticationServiceResult.Cancelled,

            ApplicationAuthenticationProviderResult.Unavailable =>
                ApplicationAuthenticationServiceResult.Unavailable,

            _ =>
                ApplicationAuthenticationServiceResult.Unavailable
        };
    }

    private async Task ObserveLateProviderOperationAsync<T>(
        Task<T> providerOperation)
    {
        try
        {
            await providerOperation.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A provider operation that completes after caller cancellation
            // no longer owns the active session attempt.
        }
        catch (Exception exception)
        {
            // Observe late failures without exposing exception details.
            LogProviderFailure(exception);
        }
    }

    private void LogProviderFailure(Exception exception)
    {
        _logger.LogSafeFailure(
            LogLevel.Warning,
            DiagnosticOperationCodes.ApplicationAuthenticationProviderFailure,
            SafeProviderFailureMessage,
            exception);
    }

    private sealed record AuthenticationCancellationRegistration(
        IApplicationSecuritySession Session,
        ApplicationSecuritySessionAttempt Attempt);
}

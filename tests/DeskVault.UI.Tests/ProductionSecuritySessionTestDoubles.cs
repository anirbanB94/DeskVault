using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Workstation;
using DeskVault.Infrastructure.Security.Authentication;
using Windows.Security.Credentials.UI;

namespace DeskVault.UI.Tests;

/// <summary>
/// Deterministic workstation event source used at the external operating
/// system notification boundary of production-composition integration tests.
/// </summary>
internal sealed class ControlledWorkstationSecurityEventSource
    : IWorkstationSecurityEventSource
{
    private EventHandler<WorkstationSecurityEventArgs>? _securityEventReceived;
    private EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? _monitoringFailed;

    private int _startCallCount;
    private int _stopCallCount;
    private readonly TaskCompletionSource<bool> _startInvoked =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event EventHandler<WorkstationSecurityEventArgs>? SecurityEventReceived
    {
        add => _securityEventReceived += value;
        remove => _securityEventReceived -= value;
    }

    public event EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? MonitoringFailed
    {
        add => _monitoringFailed += value;
        remove => _monitoringFailed -= value;
    }

    public int StartCallCount =>
        Volatile.Read(ref _startCallCount);

    public int StopCallCount =>
        Volatile.Read(ref _stopCallCount);

    public Exception? StartException { get; set; }

    public Task StartInvoked =>
        _startInvoked.Task;

    /// <summary>
    /// Optional completion task used to hold source initialization
    /// while testing the production host's readiness gate.
    /// </summary>
    public Task? StartTask { get; set; }

    public Exception? StopException { get; set; }

    public Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _startCallCount);
        _startInvoked.TrySetResult(true);

        Exception? exception = StartException;

        if (exception is not null)
        {
            return Task.FromException(exception);
        }

        return StartTask ?? Task.CompletedTask;
    }

    public Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _stopCallCount);

        Exception? exception = StopException;

        return exception is null
            ? Task.CompletedTask
            : Task.FromException(exception);
    }

    public void RaiseSecurityEvent(
        WorkstationSecurityEventKind kind)
    {
        _securityEventReceived?.Invoke(
            this,
            new WorkstationSecurityEventArgs(kind));
    }

    public void RaiseMonitoringFailure(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _monitoringFailed?.Invoke(
            this,
            new WorkstationSecurityEventSourceFailureEventArgs(exception));
    }

    /// <summary>
    /// Captures the callback currently attached by the monitor so a later
    /// test can deliver it after a newer source generation has replaced it.
    /// </summary>
    public EventHandler<WorkstationSecurityEventArgs>?
        CaptureSecurityEventHandler()
    {
        return _securityEventReceived;
    }

    public void RaiseCapturedSecurityEvent(
        EventHandler<WorkstationSecurityEventArgs>? capturedHandler,
        WorkstationSecurityEventKind kind)
    {
        capturedHandler?.Invoke(
            this,
            new WorkstationSecurityEventArgs(kind));
    }
}

/// <summary>
/// Deterministic Windows Hello API substitute. It preserves the production
/// provider and replaces only the WinRT/native interaction boundary.
/// </summary>
internal sealed class ControlledWindowsHelloPlatformApi
    : IWindowsHelloPlatformApi
{
    private TaskCompletionSource<UserConsentVerificationResult>?
        _pendingVerification;

    private TaskCompletionSource<bool>? _verificationRequestStarted;

    private int _availabilityCallCount;
    private int _verificationCallCount;

    public UserConsentVerifierAvailability Availability { get; set; } =
        UserConsentVerifierAvailability.Available;

    public UserConsentVerificationResult VerificationResult { get; set; } =
        UserConsentVerificationResult.Verified;

    public Exception? AvailabilityException { get; set; }

    public Exception? VerificationException { get; set; }

    public int AvailabilityCallCount =>
        Volatile.Read(ref _availabilityCallCount);

    public int VerificationCallCount =>
        Volatile.Read(ref _verificationCallCount);

    public Task<UserConsentVerifierAvailability> CheckAvailabilityAsync()
    {
        Interlocked.Increment(ref _availabilityCallCount);

        Exception? exception = AvailabilityException;

        return exception is null
            ? Task.FromResult(Availability)
            : Task.FromException<UserConsentVerifierAvailability>(
                exception);
    }

    public Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
        IntPtr windowHandle,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Interlocked.Increment(ref _verificationCallCount);

        Exception? exception = VerificationException;

        if (exception is not null)
        {
            return Task.FromException<UserConsentVerificationResult>(
                exception);
        }

        TaskCompletionSource<UserConsentVerificationResult>? pending =
            Volatile.Read(ref _pendingVerification);

        if (pending is null)
        {
            return Task.FromResult(VerificationResult);
        }

        Volatile.Read(ref _verificationRequestStarted)?
            .TrySetResult(true);

        return pending.Task;
    }

    public void HoldNextVerificationRequest()
    {
        var pending =
            new TaskCompletionSource<UserConsentVerificationResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        if (Interlocked.CompareExchange(
                ref _pendingVerification,
                pending,
                comparand: null) is not null)
        {
            throw new InvalidOperationException(
                "A Windows Hello verification request is already being held.");
        }

        Interlocked.Exchange(
            ref _verificationRequestStarted,
            requestStarted);
    }

    public Task WaitForVerificationRequestAsync(
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<bool>? requestStarted =
            Volatile.Read(ref _verificationRequestStarted);

        return requestStarted is null
            ? throw new InvalidOperationException(
                "No Windows Hello verification request has been configured for synchronization.")
            : (Task)requestStarted.Task.WaitAsync(
            cancellationToken);
    }

    public void CompletePendingVerification(
        UserConsentVerificationResult result)
    {
        TaskCompletionSource<UserConsentVerificationResult>? pending =
            Interlocked.Exchange(
                ref _pendingVerification,
                null) ?? throw new InvalidOperationException(
                "There is no pending Windows Hello verification request.");
        Interlocked.Exchange(
            ref _verificationRequestStarted,
            null);

        if (!pending.TrySetResult(result))
        {
            throw new InvalidOperationException(
                "The pending Windows Hello verification request was already completed.");
        }
    }
}

internal sealed class ControlledWindowsHelloWindowHandleProvider
    : IWindowsHelloWindowHandleProvider
{
    public IntPtr WindowHandle { get; set; } = new(1);

    public int GetWindowHandleCallCount { get; private set; }

    public IntPtr GetWindowHandle()
    {
        GetWindowHandleCallCount++;

        return WindowHandle;
    }
}

/// <summary>
/// Provides a deterministic monotonic timestamp source for automatic-lock
/// integration tests without changing production timing behavior.
/// </summary>
internal sealed class ControlledTimeProvider : TimeProvider
{
    private long _timestampTicks;

    public override long TimestampFrequency =>
        TimeSpan.TicksPerSecond;

    public override long GetTimestamp() =>
        Interlocked.Read(ref _timestampTicks);

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed),
                "Controlled time cannot move backwards.");
        }

        Interlocked.Add(
            ref _timestampTicks,
            elapsed.Ticks);
    }
}

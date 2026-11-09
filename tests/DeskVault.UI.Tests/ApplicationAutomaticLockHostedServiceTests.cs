using System.Collections.Concurrent;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using DeskVault.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class ApplicationAutomaticLockHostedServiceTests
{
    [Fact]
    public async Task StartAsync_StartsAutomaticLockServiceBeforeReturning()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var automaticLockService =
            new Mock<IApplicationAutomaticLockService>(
                MockBehavior.Strict);

        automaticLockService
            .Setup(service => service.Start());

        automaticLockService
            .Setup(service => service.Stop());

        using var hostedService =
            new ApplicationAutomaticLockHostedService(
                automaticLockService.Object,
                TimeProvider.System,
                NullLogger<ApplicationAutomaticLockHostedService>.Instance);

        // Act
        await hostedService.StartAsync(cancellationToken);

        // Assert
        // Start must already have been invoked when StartAsync returns.
        automaticLockService.Verify(
            service => service.Start(),
            Times.Once);

        // Stop the worker so the test leaves no background activity.
        await hostedService.StopAsync(cancellationToken);

        automaticLockService.Verify(
            service => service.Stop(),
            Times.Once);

        automaticLockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_StopsAutomaticLockServiceOnlyOnce()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var automaticLockService =
            new Mock<IApplicationAutomaticLockService>(
                MockBehavior.Strict);

        automaticLockService
            .Setup(service => service.Start());

        automaticLockService
            .Setup(service => service.Stop());

        using var hostedService =
            new ApplicationAutomaticLockHostedService(
                automaticLockService.Object,
                TimeProvider.System,
                NullLogger<ApplicationAutomaticLockHostedService>.Instance);

        await hostedService.StartAsync(cancellationToken);

        // Act
        await hostedService.StopAsync(cancellationToken);
        await hostedService.StopAsync(cancellationToken);

        // Assert
        automaticLockService.Verify(
            service => service.Start(),
            Times.Once);

        automaticLockService.Verify(
            service => service.Stop(),
            Times.Once);

        automaticLockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_WhenAutomaticLockServiceStartFails_PropagatesFailure()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var expectedException =
            new InvalidOperationException(
                "Automatic-lock startup test failure.");

        var automaticLockService =
            new Mock<IApplicationAutomaticLockService>(
                MockBehavior.Strict);

        automaticLockService
            .Setup(service => service.Start())
            .Throws(expectedException);

        automaticLockService
            .Setup(service => service.Stop());

        using var hostedService =
            new ApplicationAutomaticLockHostedService(
                automaticLockService.Object,
                TimeProvider.System,
                NullLogger<ApplicationAutomaticLockHostedService>.Instance);

        // Act
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => hostedService.StartAsync(cancellationToken));

        // Assert
        Assert.Same(expectedException, actualException);

        automaticLockService.Verify(
            service => service.Start(),
            Times.Once);

        automaticLockService.Verify(
            service => service.Stop(),
            Times.Once);

        automaticLockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEvaluationFails_LogsSafeDiagnosticAndContinues()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string sensitiveFailureSentinel =
            "PBI190_AUTOMATIC_LOCK_EXCEPTION_PAYLOAD_SENTINEL";

        int evaluationCount = 0;

        var secondEvaluationReached =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var automaticLockService =
            new Mock<IApplicationAutomaticLockService>(
                MockBehavior.Strict);

        automaticLockService
            .Setup(service => service.Start());

        automaticLockService
            .Setup(service => service.Stop());

        automaticLockService
            .Setup(service => service.EvaluateInactivity())
            .Callback(() =>
            {
                int currentCount =
                    Interlocked.Increment(ref evaluationCount);

                if (currentCount == 1)
                {
                    throw new InvalidOperationException(
                        sensitiveFailureSentinel);
                }

                secondEvaluationReached.TrySetResult(true);
            });

        var logger =
            new RecordingLogger<ApplicationAutomaticLockHostedService>();

        using var hostedService =
            new ApplicationAutomaticLockHostedService(
                automaticLockService.Object,
                TimeProvider.System,
                logger);

        // Act
        await hostedService.StartAsync(cancellationToken);

        try
        {
            // The second tick proves that a failed evaluation is logged
            // safely and does not terminate the worker.
            await secondEvaluationReached.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        // Assert
        Assert.True(evaluationCount >= 2);

        CapturedLogEntry entry =
            Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);

        Assert.Contains(
            LogMessages.AutomaticLockEvaluationFailed,
            entry.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            DiagnosticOperationCodes.AutomaticLockEvaluationFailure,
            entry.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            nameof(InvalidOperationException),
            entry.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            sensitiveFailureSentinel,
            entry.Message,
            StringComparison.Ordinal);

        automaticLockService.Verify(
            service => service.Start(),
            Times.Once);

        automaticLockService.Verify(
            service => service.Stop(),
            Times.Once);

        automaticLockService.Verify(
            service => service.EvaluateInactivity(),
            Times.AtLeast(2));

        automaticLockService.VerifyNoOtherCalls();
    }

    private sealed record CapturedLogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception);

    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

        public CapturedLogEntry[] Entries =>
            _entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) =>
            true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Enqueue(
                new CapturedLogEntry(
                    logLevel,
                    formatter(state, exception),
                    exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}

using DeskVault.Application.Security.Workstation;
using DeskVault.Infrastructure.Security.Workstation;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationSecuritySessionReconciliationWorkerTests
{
    private const uint ProcessSessionId = 42;

    [Fact]
    public async Task StartAsync_WhenProcessSessionIsUnlocked_CompletesWithoutPublishingUnlock()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();

        var events =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var failures =
            new ConcurrentQueue<Exception>();

        var worker = CreateWorker(
            nativeApi,
            events.Enqueue,
            failures.Enqueue);

        // Act
        await worker.StartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, nativeApi.QueryCount);
        Assert.Empty(events);
        Assert.Empty(failures);
        Assert.NotNull(worker.LastQueryDuration);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reconciliation_WhenProcessSessionBecomesLocked_PublishesLockWithoutPublishingUnlock()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (queryNumber, sessionId) =>
                CreateSnapshot(
                    sessionId,
                    sessionFlags: queryNumber == 1 ? 1 : 0)
        };

        var events =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var lockReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = CreateWorker(
            nativeApi,
            kind =>
            {
                events.Enqueue(kind);

                if (kind == WorkstationSecurityEventKind.SessionLocked)
                {
                    lockReceived.TrySetResult(true);
                }
            },
            _ => { },
            pollInterval: TimeSpan.FromMilliseconds(40));

        // Act
        await worker.StartAsync(
            TestContext.Current.CancellationToken);

        await lockReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(
            WorkstationSecurityEventKind.SessionLocked,
            events);

        Assert.DoesNotContain(
            WorkstationSecurityEventKind.SessionUnlocked,
            events);

        Assert.True(nativeApi.QueryCount >= 2);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reconciliation_WhenProcessSessionBecomesDisconnected_PublishesDisconnect()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (queryNumber, sessionId) =>
                CreateSnapshot(
                    sessionId,
                    connectionState: queryNumber == 1 ? 0 : 4)
        };

        var disconnected =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var events =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var worker = CreateWorker(
            nativeApi,
            kind =>
            {
                events.Enqueue(kind);

                if (kind == WorkstationSecurityEventKind.SessionDisconnected)
                {
                    disconnected.TrySetResult(true);
                }
            },
            _ => { },
            pollInterval: TimeSpan.FromMilliseconds(40));

        // Act
        await worker.StartAsync(
            TestContext.Current.CancellationToken);

        await disconnected.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(
            WorkstationSecurityEventKind.SessionDisconnected,
            events);

        Assert.DoesNotContain(
            WorkstationSecurityEventKind.SessionUnlocked,
            events);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reconciliation_WhenProcessSessionIdentifierChanges_ReportsMonitoringFailure()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            ProcessSessionIdHandler = lookupNumber =>
                lookupNumber == 1
                    ? ProcessSessionId
                    : ProcessSessionId + 1
        };

        var failure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            exception => failure.TrySetResult(exception),
            pollInterval: TimeSpan.FromMilliseconds(40));

        // Act
        await worker.StartAsync(
            TestContext.Current.CancellationToken);

        Exception actualFailure =
            await failure.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<InvalidDataException>(actualFailure);
        Assert.Equal(1, nativeApi.QueryCount);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenProcessSessionLookupFails_ReportsFailureWithoutQueryingWts()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            ProcessSessionLookupSucceeds = false,
            ProcessSessionLookupErrorCode = 5
        };

        var failure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            exception => failure.TrySetResult(exception));

        // Act
        Win32Exception exception =
            await Assert.ThrowsAsync<Win32Exception>(
                () => worker.StartAsync(
                    TestContext.Current.CancellationToken));

        Exception reportedFailure =
            await failure.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, exception.NativeErrorCode);
        Assert.Same(exception, reportedFailure);
        Assert.Equal(1, nativeApi.ProcessSessionLookupCount);
        Assert.Equal(0, nativeApi.QueryCount);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task StartAsync_WhenInitialWtsQueryFails_ReportsFailureAndDoesNotBecomeReady()
    {
        // Arrange
        var nativeApi = new FakeNativeApi
        {
            QuerySucceeds = false,
            QueryErrorCode = 5
        };

        var failure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            exception => failure.TrySetResult(exception));

        // Act
        Win32Exception exception =
            await Assert.ThrowsAsync<Win32Exception>(
                () => worker.StartAsync(
                    TestContext.Current.CancellationToken));

        Exception reportedFailure =
            await failure.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, exception.NativeErrorCode);
        Assert.Same(exception, reportedFailure);
        Assert.Equal(1, nativeApi.QueryCount);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reconciliation_WhenWtsQueryExceedsDeadline_ReportsFailureWithoutStartingOverlappingQuery()
    {
        // Arrange
        using var queryEntered = new ManualResetEventSlim(false);
        using var releaseQuery = new ManualResetEventSlim(false);

        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (_, sessionId) =>
            {
                queryEntered.Set();
                releaseQuery.Wait();

                return CreateSnapshot(sessionId);
            }
        };

        var failure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            exception => failure.TrySetResult(exception),
            pollInterval: TimeSpan.FromMilliseconds(25),
            queryTimeout: TimeSpan.FromMilliseconds(160),
            shutdownTimeout: TimeSpan.FromMilliseconds(500));

        try
        {
            // Act
            await Assert.ThrowsAsync<TimeoutException>(
                () => worker.StartAsync(
                    TestContext.Current.CancellationToken));

            Assert.True(
                queryEntered.IsSet,
                "The native query did not start.");

            Exception reportedFailure =
                await failure.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<TimeoutException>(reportedFailure);
            Assert.Equal(1, nativeApi.QueryCount);
            Assert.Equal(1, nativeApi.MaximumConcurrentQueries);
        }
        finally
        {
            // A synchronous native call cannot be forcibly cancelled.
            // Release the fake call so the retained worker can exit cleanly.
            releaseQuery.Set();

            await worker.StopAsync(
                CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_WhenCancelledDuringNativeWtsQuery_ReleasesReturnedBuffer()
    {
        // Arrange
        int expectedSize =
            WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        using var releaseQuery =
            new ManualResetEventSlim(false);

        var queryEntered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var bufferFreed =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int queryCount = 0;
        int freeCount = 0;

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr server,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                Assert.Equal(IntPtr.Zero, server);
                Assert.Equal(25, informationClass);

                IntPtr allocatedBuffer =
                    Marshal.AllocHGlobal(expectedSize);

                Marshal.Copy(
                    new byte[expectedSize],
                    0,
                    allocatedBuffer,
                    expectedSize);

                // Return the current process session as active/unlocked.
                Marshal.WriteInt32(allocatedBuffer, 0, 1);
                Marshal.WriteInt32(
                    allocatedBuffer,
                    8,
                    checked((int)sessionId));
                Marshal.WriteInt32(allocatedBuffer, 12, 0);
                Marshal.WriteInt32(allocatedBuffer, 16, 1);

                buffer = allocatedBuffer;
                bytesReturned = (uint)expectedSize;

                Interlocked.Increment(ref queryCount);
                queryEntered.TrySetResult(true);

                // Simulate a synchronous WTS query that is still in flight
                // after the startup caller cancels its wait.
                if (!releaseQuery.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "The controlled native WTS query was not released.");
                }

                return true;
            };

        WindowsWorkstationWtsMemoryFree free =
            memory =>
            {
                Marshal.FreeHGlobal(memory);

                if (Interlocked.Increment(ref freeCount) == 1)
                {
                    bufferFreed.TrySetResult(true);
                }
            };

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(query, free);

        var worker =
            new WindowsWorkstationSecuritySessionReconciliationWorker(
                nativeApi,
                _ => { },
                _ => { },
                pollInterval: TimeSpan.FromSeconds(1),
                queryTimeout: TimeSpan.FromSeconds(5),
                shutdownTimeout: TimeSpan.FromMilliseconds(500));

        Task? startTask = null;

        try
        {
            // Act: begin startup and wait until the native query is in flight.
            startTask =
                worker.StartAsync(cancellationTokenSource.Token);

            await queryEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, queryCount);
            Assert.Equal(0, freeCount);

            // Cancel startup while the synchronous native query is blocked.
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => startTask);

            // Cancellation ends startup waiting but cannot interrupt the
            // synchronous native operation. The buffer is still owned by
            // TryQuerySessionInformation until that operation returns.
            Assert.Equal(0, freeCount);

            // Allow the native query to return. Its finally block must release
            // the buffer even though the reconciliation worker is stopping.
            releaseQuery.Set();

            await bufferFreed.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            await worker.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, queryCount);
            Assert.Equal(1, freeCount);
        }
        finally
        {
            // Release the controlled native call even when an assertion fails.
            cancellationTokenSource.Cancel();
            releaseQuery.Set();

            if (startTask is not null)
            {
                try
                {
                    await startTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when startup was cancelled.
                }
                catch (TimeoutException)
                {
                    // Observe a timed-out startup during failure cleanup.
                }
            }

            await worker.StopAsync(CancellationToken.None);
        }
    }
    [Fact]
    public async Task StopAsync_WhenNativeQueryDoesNotReturn_IsBoundedAndCanBeRetriedAfterQueryCompletes()
    {
        // Arrange
        using var secondQueryEntered = new ManualResetEventSlim(false);
        using var releaseSecondQuery = new ManualResetEventSlim(false);

        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (queryNumber, sessionId) =>
            {
                if (queryNumber > 1)
                {
                    secondQueryEntered.Set();
                    releaseSecondQuery.Wait();
                }

                return CreateSnapshot(sessionId);
            }
        };

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            _ => { },
            pollInterval: TimeSpan.FromMilliseconds(30),
            queryTimeout: TimeSpan.FromSeconds(5),
            shutdownTimeout: TimeSpan.FromMilliseconds(120));

        try
        {
            await worker.StartAsync(
                TestContext.Current.CancellationToken);

            Assert.True(
                secondQueryEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken),
                "The second query did not start.");

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => worker.StopAsync(
                        TestContext.Current.CancellationToken));

            stopwatch.Stop();

            // Assert
            Assert.Contains("resources remain retained", exception.Message);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                "Worker shutdown exceeded its bounded deadline.");

            Assert.Equal(1, nativeApi.MaximumConcurrentQueries);
        }
        finally
        {
            releaseSecondQuery.Set();

            await worker.StopAsync(
                CancellationToken.None);
        }
    }

    [Fact]
    public async Task Reconciliation_WhenQueriesComplete_RecordsQueryLatencyAndStartCadence()
    {
        // Arrange
        var nativeApi = new FakeNativeApi();

        var thirdQueryCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        nativeApi.QueryHandler = (queryNumber, sessionId) =>
        {
            if (queryNumber >= 3)
            {
                thirdQueryCompleted.TrySetResult(true);
            }

            return CreateSnapshot(sessionId);
        };

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            _ => { },
            pollInterval: TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(
            TestContext.Current.CancellationToken);

        await thirdQueryCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(worker.LastQueryQueueDelay);
        Assert.NotNull(worker.LastQueryDuration);
        Assert.NotNull(worker.LastQueryStartInterval);

        Assert.True(worker.LastQueryQueueDelay >= TimeSpan.Zero);
        Assert.True(worker.LastQueryDuration >= TimeSpan.Zero);
        Assert.True(worker.LastQueryStartInterval > TimeSpan.Zero);

        await worker.StopAsync(
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reconciliation_WhenQueryThreadSchedulingIsDelayed_SeparatesQueueDelayFromExecutionDuration()
    {
        // Arrange
        using var timeProvider = new ManualTimeProvider();
        using var firstWaitEntered = new ManualResetEventSlim(false);
        using var allowQueryStart = new ManualResetEventSlim(false);
        using var secondWaitEntered = new ManualResetEventSlim(false);
        using var allowWorkerStop = new ManualResetEventSlim(false);

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        int waitInvocation = 0;

        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (queryNumber, sessionId) =>
            {
                // Simulate 75 ms spent in the actual native query boundary.
                timeProvider.Advance(
                    TimeSpan.FromMilliseconds(75));

                return CreateSnapshot(sessionId);
            }
        };

        Func<int> waitForWork = () =>
        {
            int invocation =
                Interlocked.Increment(ref waitInvocation);

            if (invocation == 1)
            {
                firstWaitEntered.Set();

                return allowQueryStart.Wait(
                    TimeSpan.FromSeconds(5),
                    cancellationToken)
                        ? 1
                        : 0;
            }

            if (invocation == 2)
            {
                secondWaitEntered.Set();

                _ = allowWorkerStop.Wait(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);

                return 0;
            }

            return 0;
        };

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            _ => { },
            pollInterval: TimeSpan.FromSeconds(10),
            queryTimeout: TimeSpan.FromSeconds(5),
            timeProvider: timeProvider,
            waitForWork: waitForWork);

        try
        {
            // StartAsync schedules the initial query while the controlled
            // query thread is deliberately held before execution.
            Task startTask =
                worker.StartAsync(cancellationToken);

            Assert.True(
                firstWaitEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    cancellationToken),
                "The query thread did not reach the controlled scheduling wait.");

            // Advance the monotonic clock while the query is queued.
            timeProvider.Advance(
                TimeSpan.FromMilliseconds(250));

            allowQueryStart.Set();

            await startTask.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);

            Assert.True(
                secondWaitEntered.Wait(
                    TimeSpan.FromSeconds(5),
                    cancellationToken),
                "The query thread did not return to its idle wait.");

            // Assert: queue delay and actual query duration are independent.
            Assert.Equal(
                TimeSpan.FromMilliseconds(250),
                worker.LastQueryQueueDelay);

            Assert.Equal(
                TimeSpan.FromMilliseconds(75),
                worker.LastQueryDuration);

            // The initial query has no previous actual execution start.
            Assert.Null(worker.LastQueryStartInterval);

            Assert.Equal(1, nativeApi.QueryCount);
            Assert.Equal(1, nativeApi.MaximumConcurrentQueries);
        }
        finally
        {
            // Release both controlled waits before joining the worker.
            allowQueryStart.Set();

            Task stopTask =
                worker.StopAsync(cancellationToken);

            allowWorkerStop.Set();

            await stopTask;
        }
    }
    [Fact]
    public async Task Watchdog_WhenReconciliationWorkerIsNotAliveWhileIdle_ReportsMonitoringFailure()
    {
        // Arrange
        using var timeProvider = new ManualTimeProvider();

        var failure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var nativeApi = new FakeNativeApi();

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            exception => failure.TrySetResult(exception),
            pollInterval: TimeSpan.FromSeconds(1),
            queryTimeout: TimeSpan.FromMilliseconds(400),
            timeProvider: timeProvider,
            queryThreadIsAlive: () => false);

        try
        {
            // Start and complete the initial query. The worker is idle
            // waiting for its next scheduled reconciliation.
            await worker.StartAsync(
                TestContext.Current.CancellationToken);

            Assert.True(
                SpinWait.SpinUntil(
                    () => !worker.IsQueryInProgress,
                    TimeSpan.FromSeconds(5)),
                "The initial reconciliation query did not complete.");

            Assert.Equal(1, nativeApi.QueryCount);

            // The configured query timeout produces a 100 ms watchdog
            // interval. Advance only that interval; the one-second poll
            // timer must not be responsible for this failure.
            timeProvider.Advance(
                TimeSpan.FromMilliseconds(100));

            Exception actualFailure =
                await failure.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<InvalidOperationException>(actualFailure);
            Assert.Contains(
                "terminated unexpectedly",
                actualFailure.Message,
                StringComparison.OrdinalIgnoreCase);

            Assert.Equal(1, nativeApi.QueryCount);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RunQueryLoop_WhenWorkerReturnsWithoutStop_ReportsMonitoringFailure()
    {
        // Arrange
        var failure =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var nativeApi = new FakeNativeApi();

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            exception => failure.TrySetResult(exception),
            waitForWork: () => 0);

        try
        {
            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => worker.StartAsync(
                    TestContext.Current.CancellationToken));

            Exception actualFailure =
                await failure.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.IsType<InvalidOperationException>(actualFailure);
            Assert.Contains(
                "terminated unexpectedly",
                actualFailure.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Reconciliation_WhenManualTimeAdvances_PollsAtConfiguredInterval()
    {
        // Arrange
        using var timeProvider = new ManualTimeProvider();

        var secondQueryStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (queryNumber, sessionId) =>
            {
                if (queryNumber == 2)
                {
                    secondQueryStarted.TrySetResult(true);
                }

                return CreateSnapshot(sessionId);
            }
        };

        var pollInterval = TimeSpan.FromMilliseconds(50);

        var worker = CreateWorker(
            nativeApi,
            _ => { },
            _ => { },
            pollInterval: pollInterval,
            timeProvider: timeProvider);

        try
        {
            // Act
            await worker.StartAsync(
                TestContext.Current.CancellationToken);

            Assert.True(
                SpinWait.SpinUntil(
                    () => !worker.IsQueryInProgress,
                    TimeSpan.FromSeconds(5)),
                "The initial reconciliation query did not leave its in-progress state.");

            Assert.Equal(1, nativeApi.QueryCount);

            timeProvider.Advance(
                TimeSpan.FromMilliseconds(49));

            // Assert that the poll has not fired early.
            Assert.Equal(1, nativeApi.QueryCount);

            timeProvider.Advance(
                TimeSpan.FromMilliseconds(1));

            await secondQueryStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(2, nativeApi.QueryCount);
            Assert.Equal(
                pollInterval,
                worker.LastQueryStartInterval.GetValueOrDefault());
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Reconciliation_WhenSessionLocksAndUnlocksBetweenPolls_DoesNotObserveTransientLockWithoutNotification()
    {
        // Arrange
        using var timeProvider = new ManualTimeProvider();
        using var secondQueryEntered = new ManualResetEventSlim(false);
        using var releaseSecondQuery = new ManualResetEventSlim(false);

        const int unlockedSessionFlag = 1;
        const int lockedSessionFlag = 0;

        int observedSessionFlags = unlockedSessionFlag;

        var nativeApi = new FakeNativeApi
        {
            QueryHandler = (queryNumber, sessionId) =>
            {
                if (queryNumber == 2)
                {
                    secondQueryEntered.Set();

                    if (!releaseSecondQuery.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
                    {
                        throw new TimeoutException(
                            "The controlled second WTS query was not released.");
                    }
                }

                return CreateSnapshot(
                    sessionId,
                    sessionFlags: Volatile.Read(ref observedSessionFlags));
            }
        };

        var events =
            new ConcurrentQueue<WorkstationSecurityEventKind>();

        var failures =
            new ConcurrentQueue<Exception>();

        var pollInterval =
            TimeSpan.FromMilliseconds(50);

        var worker = CreateWorker(
            nativeApi,
            events.Enqueue,
            failures.Enqueue,
            pollInterval: pollInterval,
            queryTimeout: TimeSpan.FromSeconds(2),
            shutdownTimeout: TimeSpan.FromMilliseconds(500),
            timeProvider: timeProvider);

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        async Task WaitForQueryCompletionAsync(string timeoutMessage)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (worker.IsQueryInProgress)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(5))
                {
                    throw new TimeoutException(timeoutMessage);
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(10),
                    cancellationToken);
            }
        }



        try
        {
            // Act 1: establish the initial unlocked observation.
            await worker.StartAsync(
                TestContext.Current.CancellationToken);

            await WaitForQueryCompletionAsync(
                "The initial WTS query did not complete.");

            Assert.Equal(1, nativeApi.QueryCount);
            Assert.Empty(events);
            Assert.Empty(failures);

            // Simulate a lock and unlock that both occur between samples.
            // No primary WTS notification is delivered to this worker.
            Volatile.Write(
                ref observedSessionFlags,
                lockedSessionFlag);

            Volatile.Write(
                ref observedSessionFlags,
                unlockedSessionFlag);

            // Act 2: advance exactly to the next scheduled poll.
            timeProvider.Advance(pollInterval);

            Assert.True(
                secondQueryEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
                "The second scheduled WTS query did not start.");

            Assert.Equal(2, nativeApi.QueryCount);

            // The fake query is held until the assertion boundary is ready.
            releaseSecondQuery.Set();

            await WaitForQueryCompletionAsync(
                "The second WTS query did not complete.");

            // Assert: reconciliation observes only the current unlocked state.
            // It cannot reconstruct the transient lock that ended before the
            // sampling point, so no protective event is published.
            Assert.Empty(events);
            Assert.Empty(failures);
            Assert.Equal(2, nativeApi.QueryCount);
            Assert.Equal(1, nativeApi.MaximumConcurrentQueries);
        }
        finally
        {
            // Ensure the controlled native query cannot strand the worker.
            releaseSecondQuery.Set();

            await worker.StopAsync(TestContext.Current.CancellationToken);
        }
    }
    private static WindowsWorkstationSecuritySessionReconciliationWorker CreateWorker(
        FakeNativeApi nativeApi,
        Action<WorkstationSecurityEventKind> securityEventReceived,
        Action<Exception> monitoringFailed,
        TimeSpan? pollInterval = null,
        TimeSpan? queryTimeout = null,
        TimeSpan? shutdownTimeout = null,
        TimeProvider? timeProvider = null,
        Func<int>? waitForWork = null,
        Func<bool>? queryThreadIsAlive = null)
    {
        return new WindowsWorkstationSecuritySessionReconciliationWorker(
            nativeApi,
            securityEventReceived,
            monitoringFailed,
            timeProvider: timeProvider,
            pollInterval: pollInterval,
            queryTimeout: queryTimeout,
            shutdownTimeout: shutdownTimeout,
            waitForWork: waitForWork,
            queryThreadIsAlive: queryThreadIsAlive);
    }

    private static WindowsWorkstationSecuritySessionStateSnapshot CreateSnapshot(
        uint sessionId,
        int connectionState = 0,
        int sessionFlags = 1)
    {
        return new WindowsWorkstationSecuritySessionStateSnapshot(
            Level: 1,
            StructureSize:
                (uint)WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize,
            SessionId: sessionId,
            ConnectionState: connectionState,
            SessionFlags: sessionFlags);
    }

    private sealed class FakeNativeApi : IWindowsWorkstationSecurityNativeApi
    {
        private int _processSessionLookupCount;
        private int _queryCount;
        private int _activeQueries;
        private int _maximumConcurrentQueries;

        public uint ProcessSessionId { get; set; } =
            WindowsWorkstationSecuritySessionReconciliationWorkerTests.ProcessSessionId;

        public Func<int, uint> ProcessSessionIdHandler { get; set; } =
            _ => WindowsWorkstationSecuritySessionReconciliationWorkerTests.ProcessSessionId;

        public bool ProcessSessionLookupSucceeds { get; set; } = true;

        public int ProcessSessionLookupErrorCode { get; set; } = 5;

        public Func<int, uint, WindowsWorkstationSecuritySessionStateSnapshot>
            QueryHandler
        { get; set; } =
                (_, sessionId) => CreateSnapshot(sessionId);

        public bool QuerySucceeds { get; set; } = true;

        public int QueryErrorCode { get; set; } = 5;

        public int ProcessSessionLookupCount =>
            Volatile.Read(ref _processSessionLookupCount);

        public int QueryCount =>
            Volatile.Read(ref _queryCount);

        public int MaximumConcurrentQueries =>
            Volatile.Read(ref _maximumConcurrentQueries);

        public IWindowsWorkstationSecurityNativeWindow CreateHiddenTopLevelWindow(
            Action<uint, nuint, nint> messageReceived)
        {
            throw new NotSupportedException(
                "The reconciliation worker does not create native windows.");
        }

        public bool RegisterSessionNotification(
            IntPtr windowHandle,
            uint flags,
            out int errorCode)
        {
            errorCode = 50;
            return false;
        }

        public bool UnregisterSessionNotification(
            IntPtr windowHandle,
            out int errorCode)
        {
            errorCode = 50;
            return false;
        }

        public bool TryGetProcessSessionId(
            out uint processSessionId,
            out int errorCode)
        {
            Interlocked.Increment(ref _processSessionLookupCount);

            if (!ProcessSessionLookupSucceeds)
            {
                processSessionId = 0;
                errorCode = ProcessSessionLookupErrorCode;
                return false;
            }

            processSessionId =
                ProcessSessionIdHandler(
                    Volatile.Read(ref _processSessionLookupCount));

            errorCode = 0;
            return true;
        }

        public bool TryQuerySessionInformation(
            uint sessionId,
            out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
            out int errorCode)
        {
            int queryNumber =
                Interlocked.Increment(ref _queryCount);

            int activeQueries =
                Interlocked.Increment(ref _activeQueries);

            UpdateMaximumConcurrentQueries(activeQueries);

            try
            {
                if (!QuerySucceeds)
                {
                    snapshot = default;
                    errorCode = QueryErrorCode;
                    return false;
                }

                snapshot =
                    QueryHandler(queryNumber, sessionId);

                errorCode = 0;
                return true;
            }
            finally
            {
                Interlocked.Decrement(ref _activeQueries);
            }
        }

        public bool TryRegisterSuspendResumeNotification(
            WindowsWorkstationPowerNotificationCallback callback,
            out IntPtr registrationHandle,
            out int errorCode)
        {
            registrationHandle = IntPtr.Zero;
            errorCode = 50;
            return false;
        }

        public bool TryUnregisterSuspendResumeNotification(
            IntPtr registrationHandle,
            out int errorCode)
        {
            errorCode = 50;
            return false;
        }

        private void UpdateMaximumConcurrentQueries(
            int activeQueries)
        {
            while (true)
            {
                int currentMaximum =
                    Volatile.Read(ref _maximumConcurrentQueries);

                if (activeQueries <= currentMaximum)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref _maximumConcurrentQueries,
                        activeQueries,
                        currentMaximum) == currentMaximum)
                {
                    return;
                }
            }
        }
    }
    private sealed class ManualTimeProvider : TimeProvider, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly List<ManualTimer> _timers = [];

        // The worker uses zero as the sentinel for "no previous query start".
        private long _timestamp = TimeSpan.TicksPerSecond;

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            lock (_syncRoot)
            {
                return _timestamp;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);

            var timer = new ManualTimer(this, callback, state);

            if (!timer.Change(dueTime, period))
            {
                throw new InvalidOperationException(
                    "Unable to initialize the manual timer.");
            }

            lock (_syncRoot)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            ManualTimer[] timers;

            lock (_syncRoot)
            {
                _timestamp = checked(_timestamp + amount.Ticks);
                timers = _timers.ToArray();
            }

            foreach (ManualTimer timer in timers)
            {
                timer.FireIfDue();
            }
        }

        public void Dispose()
        {
            ManualTimer[] timers;

            lock (_syncRoot)
            {
                timers = _timers.ToArray();
            }

            foreach (ManualTimer timer in timers)
            {
                timer.Dispose();
            }
        }

        private bool ChangeTimer(
            ManualTimer timer,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ValidateTimeout(dueTime, nameof(dueTime));
            ValidateTimeout(period, nameof(period));

            lock (_syncRoot)
            {
                if (timer.IsDisposed)
                {
                    return false;
                }

                timer.DueTimestamp =
                    dueTime == Timeout.InfiniteTimeSpan
                        ? long.MaxValue
                        : checked(_timestamp + dueTime.Ticks);

                timer.PeriodTicks =
                    period == Timeout.InfiniteTimeSpan
                        ? long.MaxValue
                        : period.Ticks;

                return true;
            }
        }

        private void DisposeTimer(ManualTimer timer)
        {
            lock (_syncRoot)
            {
                timer.IsDisposed = true;
                timer.DueTimestamp = long.MaxValue;
                _timers.Remove(timer);
            }
        }

        private bool TryTakeCallback(
            ManualTimer timer,
            out TimerCallback? callback,
            out object? state)
        {
            lock (_syncRoot)
            {
                if (timer.IsDisposed ||
                    timer.DueTimestamp == long.MaxValue ||
                    timer.DueTimestamp > _timestamp)
                {
                    callback = null;
                    state = null;
                    return false;
                }

                callback = timer.Callback;
                state = timer.State;

                if (timer.PeriodTicks <= 0 ||
                    timer.PeriodTicks == long.MaxValue)
                {
                    timer.DueTimestamp = long.MaxValue;
                }
                else
                {
                    timer.DueTimestamp =
                        checked(timer.DueTimestamp + timer.PeriodTicks);
                }

                return true;
            }
        }

        private static void ValidateTimeout(
            TimeSpan value,
            string parameterName)
        {
            if (value < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;

            internal ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state)
            {
                _owner = owner;
                Callback = callback;
                State = state;
            }

            internal TimerCallback Callback { get; }

            internal object? State { get; }

            internal long DueTimestamp { get; set; } = long.MaxValue;

            internal long PeriodTicks { get; set; } = long.MaxValue;

            internal bool IsDisposed { get; set; }

            public bool Change(
                TimeSpan dueTime,
                TimeSpan period)
            {
                return _owner.ChangeTimer(
                    this,
                    dueTime,
                    period);
            }

            public void Dispose()
            {
                _owner.DisposeTimer(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            internal void FireIfDue()
            {
                if (_owner.TryTakeCallback(
                    this,
                    out TimerCallback? callback,
                    out object? state))
                {
                    callback!(state);
                }
            }
        }
    }
}

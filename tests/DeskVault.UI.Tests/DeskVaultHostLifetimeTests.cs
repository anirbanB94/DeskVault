using DeskVault.UI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeskVault.UI.Tests;

public sealed class DeskVaultHostLifetimeTests
{
    [Fact]
    public async Task StartAsync_WhenCalledRepeatedly_StartsHostOnlyOnce()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var host = new TestHost();

        await using var lifetime =
            new DeskVaultHostLifetime(host);

        // Act
        await lifetime.StartAsync(cancellationToken);
        await lifetime.StartAsync(cancellationToken);

        // Assert
        Assert.Equal(1, host.StartCallCount);
        Assert.Equal(0, host.StopCallCount);
    }

    [Fact]
    public async Task DisposeAsync_AfterStart_StopsAndDisposesHostOnce()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var host = new TestHost();

        var lifetime =
            new DeskVaultHostLifetime(host);

        // Act
        await lifetime.StartAsync(cancellationToken);
        await lifetime.DisposeAsync();
        await lifetime.DisposeAsync();

        // Assert
        Assert.Equal(1, host.StartCallCount);
        Assert.Equal(1, host.StopCallCount);
        Assert.Equal(1, host.AsyncDisposeCallCount);
    }

    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_StopsHostOnlyOnce()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var host = new TestHost();

        await using var lifetime =
            new DeskVaultHostLifetime(host);

        await lifetime.StartAsync(cancellationToken);

        // Act
        await lifetime.StopAsync(cancellationToken);
        await lifetime.StopAsync(cancellationToken);

        // Assert
        Assert.Equal(1, host.StopCallCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenHostWasNeverStarted_DoesNotStopHost()
    {
        // Arrange
        var host = new TestHost();

        var lifetime =
            new DeskVaultHostLifetime(host);

        // Act
        await lifetime.DisposeAsync();

        // Assert
        Assert.Equal(0, host.StartCallCount);
        Assert.Equal(0, host.StopCallCount);
        Assert.Equal(1, host.AsyncDisposeCallCount);
    }

    [Fact]
    public async Task StartAsync_WhenHostStartupFails_AttemptsStopAndPreservesFailure()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var expectedException =
            new InvalidOperationException(
                "Host startup test failure.");

        var host = new TestHost
        {
            StartException = expectedException
        };

        var lifetime =
            new DeskVaultHostLifetime(host);

        // Act
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => lifetime.StartAsync(cancellationToken));

        await lifetime.DisposeAsync();

        // Assert
        Assert.Same(expectedException, actualException);
        Assert.Equal(1, host.StartCallCount);
        Assert.Equal(1, host.StopCallCount);
        Assert.Equal(1, host.AsyncDisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenHostStopFails_StillDisposesHost()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var expectedException =
            new InvalidOperationException(
                "Host stop test failure.");

        var host = new TestHost
        {
            StopException = expectedException
        };

        var lifetime =
            new DeskVaultHostLifetime(host);

        await lifetime.StartAsync(cancellationToken);

        // Act
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await lifetime.DisposeAsync());

        // Assert
        Assert.Same(expectedException, actualException);
        Assert.Equal(1, host.StopCallCount);
        Assert.Equal(1, host.AsyncDisposeCallCount);
    }

    private sealed class TestHost : IHost, IAsyncDisposable
    {
        private readonly ServiceProvider _services =
            new ServiceCollection().BuildServiceProvider();

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public int AsyncDisposeCallCount { get; private set; }

        public Exception? StartException { get; init; }

        public Exception? StopException { get; init; }

        public IServiceProvider Services => _services;

        public Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            StartCallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            return StartException is null
                ? Task.CompletedTask
                : Task.FromException(StartException);
        }

        public Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            StopCallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            return StopException is null
                ? Task.CompletedTask
                : Task.FromException(StopException);
        }

        public void Dispose()
        {
            _services.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            AsyncDisposeCallCount++;

            await _services.DisposeAsync();
        }
    }
}

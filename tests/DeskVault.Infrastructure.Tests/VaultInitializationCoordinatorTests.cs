using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class VaultInitializationCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSameVaultIsInitializedConcurrently_SerializesOperations()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var firstCoordinator =
                CreateCoordinator(rootDirectory);

            var secondCoordinator =
                CreateCoordinator(rootDirectory);

            TaskCompletionSource<bool> firstOperationEntered =
                CreateCompletionSource();

            TaskCompletionSource<bool> releaseFirstOperation =
                CreateCompletionSource();

            TaskCompletionSource<bool> secondOperationEntered =
                CreateCompletionSource();

            Task firstOperation =
                firstCoordinator.ExecuteAsync(
                    async cancellationToken =>
                    {
                        // Arrange
                        firstOperationEntered.SetResult(true);

                        // Act
                        await releaseFirstOperation.Task;

                        // Assert
                        cancellationToken.ThrowIfCancellationRequested();
                    });

            await firstOperationEntered.Task;

            Task secondOperation =
                secondCoordinator.ExecuteAsync(
                    _ =>
                    {
                        // Arrange
                        secondOperationEntered.SetResult(true);

                        // Act / Assert
                        return Task.CompletedTask;
                    });

            await Assert.ThrowsAsync<TimeoutException>(
                () =>
                    secondOperationEntered.Task.WaitAsync(
                        TimeSpan.FromMilliseconds(500)));

            Assert.False(
                secondOperation.IsCompleted);

            releaseFirstOperation.SetResult(true);

            await Task.WhenAll(
                firstOperation,
                secondOperation);

            Assert.True(
                secondOperationEntered.Task.IsCompletedSuccessfully);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_ReleasesLockForSubsequentInitialization()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var firstCoordinator =
                CreateCoordinator(rootDirectory);

            var secondCoordinator =
                CreateCoordinator(rootDirectory);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    firstCoordinator.ExecuteAsync(
                        _ =>
                            Task.FromException(
                                new InvalidOperationException(
                                    "Initialization failed."))));

            bool secondOperationExecuted = false;

            await secondCoordinator.ExecuteAsync(
                _ =>
                {
                    // Arrange
                    secondOperationExecuted = true;

                    // Act / Assert
                    return Task.CompletedTask;
                });

            // Assert
            Assert.True(
                secondOperationExecuted);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenVaultRootDoesNotExist_CreatesRootAndExecutesOperation()
    {
        // Arrange
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultVaultInitializationCoordinatorTests",
                Guid.NewGuid().ToString("N"));

        try
        {
            var coordinator =
                CreateCoordinator(rootDirectory);

            bool operationExecuted = false;

            // Act
            await coordinator.ExecuteAsync(
                _ =>
                {
                    // Arrange
                    operationExecuted = true;

                    // Act / Assert
                    return Task.CompletedTask;
                });

            // Assert
            Assert.True(
                Directory.Exists(
                    rootDirectory));

            Assert.True(
                operationExecuted);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenWaitingIsCancelled_DoesNotEnterCriticalSection()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var firstCoordinator =
                CreateCoordinator(rootDirectory);

            var secondCoordinator =
                CreateCoordinator(rootDirectory);

            TaskCompletionSource<bool> firstOperationEntered =
                CreateCompletionSource();

            TaskCompletionSource<bool> releaseFirstOperation =
                CreateCompletionSource();

            Task firstOperation =
                firstCoordinator.ExecuteAsync(
                    async _ =>
                    {
                        // Arrange
                        firstOperationEntered.SetResult(true);

                        // Act
                        await releaseFirstOperation.Task;

                        // Assert
                    });

            await firstOperationEntered.Task;

            using var cancellationTokenSource =
                new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(250));

            bool secondOperationExecuted = false;

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    secondCoordinator.ExecuteAsync(
                        _ =>
                        {
                            // Arrange
                            secondOperationExecuted = true;

                            // Act / Assert
                            return Task.CompletedTask;
                        },
                        cancellationTokenSource.Token));

            // Assert
            Assert.False(
                secondOperationExecuted);

            releaseFirstOperation.SetResult(true);

            await firstOperation;
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenDifferentVaultsInitializeConcurrently_DoesNotSerializeUnrelatedVaults()
    {
        // Arrange
        string firstRootDirectory =
            CreateTemporaryDirectory();

        string secondRootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var firstCoordinator =
                CreateCoordinator(
                    firstRootDirectory);

            var secondCoordinator =
                CreateCoordinator(
                    secondRootDirectory);

            TaskCompletionSource<bool> firstOperationEntered =
                CreateCompletionSource();

            TaskCompletionSource<bool> releaseFirstOperation =
                CreateCompletionSource();

            Task firstOperation =
                firstCoordinator.ExecuteAsync(
                    async _ =>
                    {
                        // Arrange
                        firstOperationEntered.SetResult(true);

                        // Act
                        await releaseFirstOperation.Task;

                        // Assert
                    });

            await firstOperationEntered.Task;

            bool secondOperationExecuted = false;

            // Act
            await secondCoordinator.ExecuteAsync(
                _ =>
                {
                    // Arrange
                    secondOperationExecuted = true;

                    // Act / Assert
                    return Task.CompletedTask;
                });

            // Assert
            Assert.True(
                secondOperationExecuted);

            releaseFirstOperation.SetResult(true);

            await firstOperation;
        }
        finally
        {
            DeleteTemporaryDirectory(
                firstRootDirectory);

            DeleteTemporaryDirectory(
                secondRootDirectory);
        }
    }

    private static VaultInitializationCoordinator CreateCoordinator(
        string rootDirectory)
    {
        return new VaultInitializationCoordinator(
            new DeskVaultDataPaths(
                rootDirectory));
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultVaultInitializationCoordinatorTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            directory);

        return directory;
    }

    private static void DeleteTemporaryDirectory(
        string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}

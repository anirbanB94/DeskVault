using DeskVault.Infrastructure.Services;
using System.Diagnostics;
using System.Text;

namespace DeskVault.Infrastructure.Tests;

/// <summary>
/// Verifies cross-process document-artifact lease acquisition, ownership,
/// cancellation, and release behavior.
/// </summary>
public sealed class DocumentArtifactCoordinatorTests
{
    private static readonly TimeSpan AcquisitionTimeout =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ProcessReadinessTimeout =
        TimeSpan.FromSeconds(30);

    /// <summary>
    /// Verifies that independent coordinator instances cannot simultaneously
    /// acquire a lease for the same document in the same vault.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenSameDocumentIsAlreadyLeased_WaitsUntilFirstLeaseIsDisposed()
    {
        // Arrange
        using var vault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        DocumentArtifactCoordinator firstCoordinator =
            CreateCoordinator(vault.RootDirectory);

        DocumentArtifactCoordinator secondCoordinator =
            CreateCoordinator(vault.RootDirectory);

        IAsyncDisposable firstLease =
            await firstCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken);

        IAsyncDisposable? secondLease = null;

        try
        {
            Task<IAsyncDisposable> secondAcquisition =
                secondCoordinator.AcquireAsync(
                    documentId,
                    TestContext.Current.CancellationToken);

            await Task.Delay(
                TimeSpan.FromMilliseconds(250),
                TestContext.Current.CancellationToken);

            bool secondAcquisitionWasBlocked =
                !secondAcquisition.IsCompleted;

            // Releasing the first handle must permit the independent
            // coordinator instance to acquire the same document lease.
            await firstLease.DisposeAsync();

            secondLease =
                await secondAcquisition.WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);

            // Assert after cleanup so an unexpected early acquisition
            // cannot leave an undisposed lease behind.
            Assert.True(
                secondAcquisitionWasBlocked,
                "A second coordinator acquired the same document lease while the first lease was held.");
        }
        finally
        {
            await firstLease.DisposeAsync();

            if (secondLease is not null)
            {
                await secondLease.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Verifies that different document IDs within one vault can hold
    /// independent leases concurrently.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenDocumentIdsAreDifferent_AllowsConcurrentLeases()
    {
        // Arrange
        using var vault = new TemporaryVault();

        DocumentArtifactCoordinator firstCoordinator =
            CreateCoordinator(vault.RootDirectory);

        DocumentArtifactCoordinator secondCoordinator =
            CreateCoordinator(vault.RootDirectory);

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        // Act
        await using IAsyncDisposable firstLease =
            await firstCoordinator.AcquireAsync(
                firstDocumentId,
                TestContext.Current.CancellationToken);

        await using IAsyncDisposable secondLease =
            await secondCoordinator.AcquireAsync(
                secondDocumentId,
                TestContext.Current.CancellationToken).WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(
            vault.LeasePathResolver.GetPath(firstDocumentId),
            vault.LeasePathResolver.GetPath(secondDocumentId));
    }

    /// <summary>
    /// Verifies that the same document ID in two separate vaults can hold
    /// independent leases concurrently.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenVaultsAreDifferent_AllowsConcurrentLeases()
    {
        // Arrange
        using var firstVault = new TemporaryVault();
        using var secondVault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        DocumentArtifactCoordinator firstCoordinator =
            CreateCoordinator(firstVault.RootDirectory);

        DocumentArtifactCoordinator secondCoordinator =
            CreateCoordinator(secondVault.RootDirectory);

        // Act
        await using IAsyncDisposable firstLease =
            await firstCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken);

        await using IAsyncDisposable secondLease =
            await secondCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken).WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(
            firstVault.LeasePathResolver.GetPath(documentId),
            secondVault.LeasePathResolver.GetPath(documentId));
    }

    /// <summary>
    /// Verifies that cancellation while waiting stops acquisition and does
    /// not grant a lease to the waiting coordinator.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenWaitingIsCancelled_DoesNotGrantLease()
    {
        // Arrange
        using var vault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        DocumentArtifactCoordinator firstCoordinator =
            CreateCoordinator(vault.RootDirectory);

        DocumentArtifactCoordinator secondCoordinator =
            CreateCoordinator(vault.RootDirectory);

        await using IAsyncDisposable firstLease =
            await firstCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken);

        using var cancellationTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(250));

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await secondCoordinator.AcquireAsync(
                    documentId,
                    cancellationTokenSource.Token));

        // Assert
        // A subsequent acquisition must remain possible after the current
        // owner releases its handle; the cancelled waiter owns nothing.
        await firstLease.DisposeAsync();

        await using IAsyncDisposable subsequentLease =
            await secondCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken).WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that a cancellation token already cancelled before acquisition
    /// prevents directory creation and lease acquisition.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenCancellationIsRequestedBeforeAcquisition_DoesNotCreateCoordinationDirectory()
    {
        // Arrange
        using var vault = new TemporaryVault();

        DocumentArtifactCoordinator coordinator =
            CreateCoordinator(vault.RootDirectory);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await coordinator.AcquireAsync(
                    Guid.NewGuid(),
                    cancellationTokenSource.Token));

        // Assert
        Assert.False(
            Directory.Exists(
                vault.DataPaths.SecurityDirectory));
    }

    /// <summary>
    /// Verifies that an exception inside a protected operation does not
    /// leave the document lease held when it is disposed asynchronously.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenProtectedOperationThrows_ReleasesLeaseForSubsequentAcquisition()
    {
        // Arrange
        using var vault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        DocumentArtifactCoordinator firstCoordinator =
            CreateCoordinator(vault.RootDirectory);

        DocumentArtifactCoordinator secondCoordinator =
            CreateCoordinator(vault.RootDirectory);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await using IAsyncDisposable lease =
                    await firstCoordinator.AcquireAsync(
                        documentId,
                        TestContext.Current.CancellationToken);

                throw new InvalidOperationException(
                    "Simulated protected operation failure.");
            });

        // Assert
        await using IAsyncDisposable subsequentLease =
            await secondCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken).WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that lease disposal is idempotent and the persistent empty
    /// coordination file is not treated as proof of active ownership.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledRepeatedly_ReleasesHandleAndPreservesEmptyCoordinationFile()
    {
        // Arrange
        using var vault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        DocumentArtifactCoordinator firstCoordinator =
            CreateCoordinator(vault.RootDirectory);

        DocumentArtifactCoordinator secondCoordinator =
            CreateCoordinator(vault.RootDirectory);

        string leaseFilePath =
            vault.LeasePathResolver.GetPath(
                documentId);

        IAsyncDisposable lease =
            await firstCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken);

        // Act
        await lease.DisposeAsync();
        await lease.DisposeAsync();

        // Assert
        Assert.True(
            File.Exists(leaseFilePath));

        Assert.Equal(
            0,
            new FileInfo(leaseFilePath).Length);

        // The persistent file remains, but a new owner can acquire its
        // exclusive operating-system handle after the previous release.
        await using IAsyncDisposable subsequentLease =
            await secondCoordinator.AcquireAsync(
                documentId,
                TestContext.Current.CancellationToken).WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that an unexpected filesystem error is propagated instead
    /// of being retried as ordinary lease contention.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenCoordinationPathIsADirectory_FailsClosed()
    {
        // Arrange
        using var vault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        DocumentArtifactCoordinator coordinator =
            CreateCoordinator(vault.RootDirectory);

        string leaseFilePath =
            vault.LeasePathResolver.GetPath(
                documentId);

        // A directory at the file path is an unexpected filesystem state,
        // not another process holding an exclusive file handle.
        Directory.CreateDirectory(
            leaseFilePath);

        // Act
        UnauthorizedAccessException exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () =>
                    await coordinator.AcquireAsync(
                        documentId,
                        TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Verifies that an empty document identity is rejected before the
    /// coordination path is resolved.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenDocumentIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        using var vault = new TemporaryVault();

        DocumentArtifactCoordinator coordinator =
            CreateCoordinator(vault.RootDirectory);

        // Act
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                async () =>
                    await coordinator.AcquireAsync(
                        Guid.Empty,
                        TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            "documentId",
            exception.ParamName);

        Assert.False(
            Directory.Exists(
                vault.DataPaths.SecurityDirectory));
    }

    /// <summary>
    /// Verifies that terminating a separate process releases its exclusive
    /// operating-system handle and permits a new process to acquire the lease.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenOwningProcessTerminates_ReleasesOperatingSystemLease()
    {
        // Arrange
        using var vault = new TemporaryVault();

        Guid documentId = Guid.NewGuid();

        string leaseFilePath =
            vault.LeasePathResolver.GetPath(
                documentId);

        string startedFilePath =
            Path.Combine(
                vault.TestDirectory,
                "lease-owner-started.txt");

        string readyFilePath =
            Path.Combine(
                vault.TestDirectory,
                "lease-owner-ready.txt");

        string errorFilePath =
            Path.Combine(
                vault.TestDirectory,
                "lease-owner-error.txt");

        DocumentArtifactCoordinator coordinator =
            CreateCoordinator(vault.RootDirectory);

        using Process ownerProcess =
            StartExclusiveLockHolder(
                leaseFilePath,
                startedFilePath,
                readyFilePath,
                errorFilePath);

        Task<IAsyncDisposable>? acquisitionTask = null;
        IAsyncDisposable? acquiredLease = null;

        bool acquisitionWasBlocked = false;

        try
        {
            await WaitForLockHolderReadyAsync(
                ownerProcess,
                startedFilePath,
                readyFilePath,
                errorFilePath,
                TestContext.Current.CancellationToken);

            // The readiness marker is written only after the child process
            // successfully opens the coordination file exclusively.
            acquisitionTask =
                coordinator.AcquireAsync(
                    documentId,
                    TestContext.Current.CancellationToken);

            await Task.Delay(
                TimeSpan.FromMilliseconds(250),
                TestContext.Current.CancellationToken);

            acquisitionWasBlocked =
                !acquisitionTask.IsCompleted;

            // Process termination must release the operating-system handle.
            TerminateProcess(
                ownerProcess);

            acquiredLease =
                await acquisitionTask.WaitAsync(
                    AcquisitionTimeout,
                    TestContext.Current.CancellationToken);
        }
        finally
        {
            // Always terminate the child before waiting for any pending
            // acquisition, including when an assertion or timeout fails.
            TerminateProcess(
                ownerProcess);

            if (acquiredLease is null &&
                acquisitionTask is not null)
            {
                try
                {
                    acquiredLease =
                        await acquisitionTask.WaitAsync(
                            AcquisitionTimeout,
                            CancellationToken.None);
                }
                catch (Exception)
                {
                    // Preserve the original test failure while still
                    // ensuring the child process and any acquired lease
                    // are cleaned up.
                }
            }

            if (acquiredLease is not null)
            {
                await acquiredLease.DisposeAsync();
            }

            foreach (string markerFilePath in new[]
                     {
                     startedFilePath,
                     readyFilePath,
                     errorFilePath
                 })
            {
                if (File.Exists(markerFilePath))
                {
                    File.Delete(markerFilePath);
                }
            }
        }

        // Assert
        Assert.True(
            acquisitionWasBlocked,
            "The coordinator acquired the lease while the separate process still held the exclusive handle.");

        Assert.True(
            File.Exists(leaseFilePath));
    }

    /// <summary>
    /// Creates a coordinator bound to the supplied vault root.
    /// </summary>
    /// <param name="rootDirectory">
    /// The configured vault root.
    /// </param>
    /// <returns>
    /// A new coordinator instance with independent resolver instances.
    /// </returns>
    private static DocumentArtifactCoordinator CreateCoordinator(
        string rootDirectory)
    {
        var dataPaths =
            new DeskVaultDataPaths(
                rootDirectory);

        var directoryResolver =
            new CanonicalVaultDirectoryResolver(
                dataPaths);

        var leasePathResolver =
            new DocumentArtifactLeasePathResolver(
                directoryResolver);

        return new DocumentArtifactCoordinator(
            leasePathResolver);
    }

    /// <summary>
    /// Starts a separate Windows PowerShell process that opens the supplied
    /// coordination file exclusively and remains alive.
    /// </summary>
    /// <param name="leaseFilePath">
    /// The coordination file to hold exclusively.
    /// </param>
    /// <param name="startedFilePath">
    /// The startup marker written when the child script begins execution.
    /// </param>
    /// <param name="readyFilePath">
    /// The readiness marker written after exclusive ownership is obtained.
    /// </param>
    /// <param name="errorFilePath">
    /// The error marker written if the child script fails.
    /// </param>
    /// <returns>
    /// The started process.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    /// Windows PowerShell is unavailable at its expected Windows location.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The child process could not be started.
    /// </exception>
    private static Process StartExclusiveLockHolder(
        string leaseFilePath,
        string startedFilePath,
        string readyFilePath,
        string errorFilePath)
    {
        string powerShellPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");

        if (!File.Exists(powerShellPath))
        {
            throw new FileNotFoundException(
                "Windows PowerShell was not found for the process-termination test.",
                powerShellPath);
        }

        string script =
            string.Join(
                Environment.NewLine,
                "$ErrorActionPreference = 'Stop'",
                $"$leasePath = '{EscapePowerShellLiteral(leaseFilePath)}'",
                $"$startedPath = '{EscapePowerShellLiteral(startedFilePath)}'",
                $"$readyPath = '{EscapePowerShellLiteral(readyFilePath)}'",
                $"$errorPath = '{EscapePowerShellLiteral(errorFilePath)}'",
                "try {",
                "    [System.IO.File]::WriteAllText($startedPath, 'started')",
                "    $leaseStream = [System.IO.File]::Open($leasePath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)",
                "    [System.IO.File]::WriteAllText($readyPath, 'ready')",
                "    while ($true) { Start-Sleep -Milliseconds 100 }",
                "}",
                "catch {",
                "    [System.IO.File]::WriteAllText($errorPath, $_.ToString())",
                "    exit 1",
                "}");

        // Windows PowerShell -EncodedCommand expects UTF-16LE text.
        string encodedCommand =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    script));

        var startInfo =
            new ProcessStartInfo
            {
                FileName = powerShellPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);

        return Process.Start(startInfo) ??
               throw new InvalidOperationException(
                   "Could not start the process used for the process-termination test.");
    }

    /// <summary>
    /// Escapes a string for use as a single-quoted PowerShell literal.
    /// </summary>
    /// <param name="value">
    /// The path value to escape.
    /// </param>
    /// <returns>
    /// The escaped PowerShell literal content.
    /// </returns>
    private static string EscapePowerShellLiteral(
        string value)
    {
        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Waits until the child process confirms that it has acquired the
    /// exclusive coordination-file handle.
    /// </summary>
    /// <param name="process">
    /// The child process holding the handle.
    /// </param>
    /// <param name="startedFilePath">
    /// The startup marker written when the child script begins execution.
    /// </param>
    /// <param name="readyFilePath">
    /// The readiness marker written after exclusive ownership is obtained.
    /// </param>
    /// <param name="errorFilePath">
    /// The error marker written if the child script fails.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the readiness wait.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The child process exits or reports an error before announcing ownership.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// The child process does not announce ownership within the bounded period.
    /// </exception>
    private static async Task WaitForLockHolderReadyAsync(
        Process process,
        string startedFilePath,
        string readyFilePath,
        string errorFilePath,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch =
            Stopwatch.StartNew();

        while (!File.Exists(readyFilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(errorFilePath))
            {
                string childError =
                    await File.ReadAllTextAsync(
                        errorFilePath,
                        cancellationToken);

                throw new InvalidOperationException(
                    $"The separate process failed while acquiring the coordination handle. Error: {childError}");
            }

            if (process.HasExited)
            {
                string standardError =
                    await process.StandardError.ReadToEndAsync(
                        cancellationToken);

                string childError =
                    File.Exists(errorFilePath)
                        ? await File.ReadAllTextAsync(
                            errorFilePath,
                            cancellationToken)
                        : "No child-process error marker was produced.";

                string executionStage =
                    File.Exists(startedFilePath)
                        ? "after starting the helper script"
                        : "before starting the helper script";

                throw new InvalidOperationException(
                    $"The process exited {executionStage} before acquiring the coordination handle. " +
                    $"Exit code: {process.ExitCode}. " +
                    $"Child error: {childError}. " +
                    $"Standard error: {standardError}");
            }

            if (stopwatch.Elapsed >= ProcessReadinessTimeout)
            {
                string executionStage =
                    File.Exists(startedFilePath)
                        ? "started the helper script but did not confirm exclusive-handle acquisition"
                        : "did not reach the helper-script startup marker";

                throw new TimeoutException(
                    $"The separate process (PID {process.Id}) {executionStage} " +
                    $"within {ProcessReadinessTimeout.TotalSeconds:0} seconds.");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(50),
                cancellationToken);
        }
    }

    /// <summary>
    /// Terminates the supplied process and waits for its handles to be closed.
    /// </summary>
    /// <param name="process">
    /// The process to terminate.
    /// </param>
    private static void TerminateProcess(
        Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(
                entireProcessTree: true);

            process.WaitForExit();
        }
    }

    /// <summary>
    /// Owns a unique temporary vault and its coordination path resolver.
    /// </summary>
    private sealed class TemporaryVault : IDisposable
    {
        /// <summary>
        /// Gets the unique temporary test directory.
        /// </summary>
        public string TestDirectory { get; }

        /// <summary>
        /// Gets the temporary vault root.
        /// </summary>
        public string RootDirectory { get; }

        /// <summary>
        /// Gets the configured vault data paths.
        /// </summary>
        public DeskVaultDataPaths DataPaths { get; }

        /// <summary>
        /// Gets the resolver for document-artifact coordination paths.
        /// </summary>
        public DocumentArtifactLeasePathResolver LeasePathResolver { get; }

        /// <summary>
        /// Creates a unique temporary vault and its path resolver.
        /// </summary>
        public TemporaryVault()
        {
            TestDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultDocumentArtifactCoordinatorTests",
                    Guid.NewGuid().ToString("N"));

            RootDirectory =
                Path.Combine(
                    TestDirectory,
                    "Vault");

            Directory.CreateDirectory(
                RootDirectory);

            DataPaths =
                new DeskVaultDataPaths(
                    RootDirectory);

            var directoryResolver =
                new CanonicalVaultDirectoryResolver(
                    DataPaths);

            LeasePathResolver =
                new DocumentArtifactLeasePathResolver(
                    directoryResolver);
        }

        /// <summary>
        /// Removes the temporary vault and all test artifacts.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(TestDirectory))
            {
                Directory.Delete(
                    TestDirectory,
                    recursive: true);
            }
        }
    }
}

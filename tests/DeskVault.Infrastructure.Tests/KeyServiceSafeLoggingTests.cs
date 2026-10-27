using System.Security.Cryptography;
using DeskVault.Infrastructure.Services;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Tests;

public sealed class KeyServiceSafeLoggingTests
{
    private const string SensitiveExceptionMarker =
        "SENSITIVE_EXCEPTION_SENTINEL_232";

    private const string SensitivePathMarker =
        "SENSITIVE_KEY_PATH_SENTINEL_232";

    [Fact]
    public async Task DocumentKeyProvisioningFailure_LogsSafeDiagnosticAndRethrows()
    {
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var logger =
                new RecordingLogger<WindowsEncryptionKeyService>();

            var operations =
                new ThrowingCandidateFileOperations(
                    $"Synthetic failure: {SensitiveExceptionMarker}");

            var service =
                new WindowsEncryptionKeyService(
                    new DeskVaultDataPaths(rootDirectory),
                    logger,
                    beforeKeyPublicationAsync: null,
                    candidateFileOperations: operations);

            IOException exception =
                await Assert.ThrowsAsync<IOException>(
                    () => service.GetOrCreateKeyAsync(
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                SensitiveExceptionMarker,
                exception.Message);

            AssertSafeFailureLog(
                logger,
                DiagnosticOperationCodes.EncryptionKeyOperationFailure,
                LogMessages.EncryptionKeyOperationFailed,
                nameof(IOException));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task DocumentKeyRetrievalFailure_LogsSafeDiagnosticAndRethrows()
    {
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "master.key");

            await File.WriteAllBytesAsync(
                keyFilePath,
                new byte[] { 0x7F },
                TestContext.Current.CancellationToken);

            var logger =
                new RecordingLogger<WindowsEncryptionKeyService>();

            var service =
                new WindowsEncryptionKeyService(
                    dataPaths,
                    logger);

            // Keep the file exclusively locked so the real key-reading
            // path produces an IOException containing the synthetic path.
            using var lockedFile =
                new FileStream(
                    keyFilePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

            IOException exception =
                await Assert.ThrowsAsync<IOException>(
                    () => service.GetKeyAsync(
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                SensitivePathMarker,
                exception.Message);

            AssertSafeFailureLog(
                logger,
                DiagnosticOperationCodes.EncryptionKeyOperationFailure,
                LogMessages.EncryptionKeyOperationFailed,
                nameof(IOException));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task DatabaseKeyProvisioningFailure_LogsSafeDiagnosticAndRethrows()
    {
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var logger =
                new RecordingLogger<WindowsDatabaseEncryptionKeyService>();

            var operations =
                new ThrowingCandidateFileOperations(
                    $"Synthetic failure: {SensitiveExceptionMarker}");

            var service =
                new WindowsDatabaseEncryptionKeyService(
                    new DeskVaultDataPaths(rootDirectory),
                    logger,
                    beforeKeyPublicationAsync: null,
                    candidateFileOperations: operations);

            IOException exception =
                await Assert.ThrowsAsync<IOException>(
                    () => service.GetOrCreateKeyAsync(
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                SensitiveExceptionMarker,
                exception.Message);

            AssertSafeFailureLog(
                logger,
                DiagnosticOperationCodes.DatabaseEncryptionKeyOperationFailure,
                LogMessages.DatabaseEncryptionKeyOperationFailed,
                nameof(IOException));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task DatabaseKeyRetrievalFailure_LogsSafeDiagnosticAndRethrows()
    {
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            await File.WriteAllBytesAsync(
                keyFilePath,
                new byte[] { 0x7F },
                TestContext.Current.CancellationToken);

            var logger =
                new RecordingLogger<WindowsDatabaseEncryptionKeyService>();

            var service =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    logger);

            // Keep the file exclusively locked so the real key-reading
            // path produces an IOException containing the synthetic path.
            using var lockedFile =
                new FileStream(
                    keyFilePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

            IOException exception =
                await Assert.ThrowsAsync<IOException>(
                    () => service.GetKeyAsync(
                        TestContext.Current.CancellationToken));

            Assert.Contains(
                SensitivePathMarker,
                exception.Message);

            AssertSafeFailureLog(
                logger,
                DiagnosticOperationCodes.DatabaseEncryptionKeyOperationFailure,
                LogMessages.DatabaseEncryptionKeyOperationFailed,
                nameof(IOException));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    private static void AssertSafeFailureLog<TCategory>(
        RecordingLogger<TCategory> logger,
        string expectedOperationCode,
        string expectedSafeMessage,
        string expectedFailureType)
    {
        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            expectedSafeMessage,
            entry.Message);

        Assert.Contains(
            $"OperationCode={expectedOperationCode}",
            entry.Message);

        Assert.Contains(
            $"FailureType={expectedFailureType}",
            entry.Message);

        Assert.Equal(
            expectedOperationCode,
            Assert.IsType<string>(
                entry.Properties["OperationCode"]));

        Assert.Equal(
            expectedFailureType,
            Assert.IsType<string>(
                entry.Properties["FailureType"]));

        // The exception must remain available to the caller, but must
        // not be attached to the emitted diagnostic event.
        Assert.Null(entry.Exception);

        string[] sensitiveMarkers =
        [
            SensitiveExceptionMarker,
            SensitivePathMarker
        ];

        foreach (RecordedLogEntry logEntry in logger.Entries)
        {
            foreach (string marker in sensitiveMarkers)
            {
                Assert.DoesNotContain(
                    marker,
                    logEntry.Message);

                foreach (object? value in logEntry.Properties.Values)
                {
                    Assert.DoesNotContain(
                        marker,
                        value?.ToString() ?? string.Empty);
                }
            }
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultTests",
                $"{SensitivePathMarker}_{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

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

    private sealed class ThrowingCandidateFileOperations :
        IKeyCandidateFileOperations
    {
        private readonly string _exceptionMessage;

        public ThrowingCandidateFileOperations(
            string exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
        }

        public string GetCandidateFilePath(
            string securityDirectory,
            string canonicalKeyFileName)
        {
            return Path.Combine(
                securityDirectory,
                $"{canonicalKeyFileName}.{Guid.NewGuid():N}.tmp");
        }

        public Stream CreateCandidateFile(
            string candidateFilePath)
        {
            throw new IOException(_exceptionMessage);
        }

        public void FlushCandidateFileToDisk(
            Stream candidateFile)
        {
            throw new InvalidOperationException(
                "Candidate creation failure should prevent flushing.");
        }

        public void PublishCandidateFile(
            string candidateFilePath,
            string canonicalKeyFilePath)
        {
            throw new InvalidOperationException(
                "Candidate creation failure should prevent publication.");
        }

        public void DeleteCandidateFile(
            string candidateFilePath)
        {
            throw new InvalidOperationException(
                "No candidate ownership is established when creation fails.");
        }
    }

    private sealed record RecordedLogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingLogger<TCategory> :
        ILogger<TCategory>
    {
        private readonly List<RecordedLogEntry> _entries = [];

        public IReadOnlyList<RecordedLogEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
                NoOpScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties =
                state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal)
                    : new Dictionary<string, object?>(
                        StringComparer.Ordinal);

            _entries.Add(
                new RecordedLogEntry(
                    logLevel,
                    formatter(state, exception),
                    exception,
                    properties));
        }

        private sealed class NoOpScope : IDisposable
        {
            public static NoOpScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
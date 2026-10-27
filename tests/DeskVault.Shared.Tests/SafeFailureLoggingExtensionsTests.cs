using System.Data.Common;
using System.Security.Cryptography;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Shared.Tests;

public sealed class SafeFailureLoggingExtensionsTests
{
    private const string OperationCode =
        DiagnosticOperationCodes.DocumentImportStorageFailure;

    private const string SafeMessage =
        LogMessages.DocumentImportStorageFailed;

    private const string DocumentMarker = "SENSITIVE_DOCUMENT_7D3F";
    private const string SecretMarker = "SENSITIVE_SECRET_51A9";
    private const string PathMarker = @"C:\Sensitive\PRIVATE_DOCUMENT_82BD.txt";
    private const string QueryMarker = "SENSITIVE_QUERY_2C91";

    [Theory]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void LogSafeFailure_EmitsSafeDiagnosticsWithoutExceptionPayload(
        LogLevel level)
    {
        // Arrange
        var logger = CreateLogger();
        var exception = CreateSensitiveException();

        // Act
        logger.LogSafeFailure(
            level,
            OperationCode,
            SafeMessage,
            exception);

        // Assert
        var entry = Assert.Single(logger.Entries);

        Assert.Equal(level, entry.Level);
        Assert.Equal(
            $"{SafeMessage} OperationCode={OperationCode} FailureType=IOException FailureCategory=IoFailure.",
            entry.Message);

        Assert.Null(entry.Exception);

        Assert.Equal(
            SafeMessage,
            Assert.IsType<string>(entry.Properties["SafeMessage"]));

        Assert.Equal(
            OperationCode,
            Assert.IsType<string>(entry.Properties["OperationCode"]));

        Assert.Equal(
            nameof(IOException),
            Assert.IsType<string>(entry.Properties["FailureType"]));

        Assert.Equal(
            DiagnosticFailureCategory.IoFailure,
            Assert.IsType<DiagnosticFailureCategory>(
                entry.Properties["FailureCategory"]));

        var diagnosticPayload = BuildDiagnosticPayload(entry);

        Assert.DoesNotContain(DocumentMarker, diagnosticPayload);
        Assert.DoesNotContain(SecretMarker, diagnosticPayload);
        Assert.DoesNotContain(PathMarker, diagnosticPayload);
        Assert.DoesNotContain(QueryMarker, diagnosticPayload);
    }

    [Fact]
    public void LogSafeFailure_UsesExplicitCategory_WhenCallerKnowsDomainOutcome()
    {
        // Arrange
        var logger = CreateLogger();
        string sensitiveMessage = CreateSensitiveFailureMessage();
        var exception = new IOException(sensitiveMessage);

        // Act
        logger.LogSafeFailure(
            LogLevel.Warning,
            OperationCode,
            SafeMessage,
            exception,
            failureCategory: DiagnosticFailureCategory.DuplicateConflict);

        // Assert
        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);

        Assert.Equal(
            DiagnosticFailureCategory.DuplicateConflict,
            Assert.IsType<DiagnosticFailureCategory>(
                entry.Properties["FailureCategory"]));

        Assert.Equal(
            nameof(IOException),
            Assert.IsType<string>(entry.Properties["FailureType"]));

        var diagnosticPayload = BuildDiagnosticPayload(entry);
        Assert.DoesNotContain(sensitiveMessage, diagnosticPayload);
        Assert.DoesNotContain(DocumentMarker, diagnosticPayload);
        Assert.DoesNotContain(SecretMarker, diagnosticPayload);
        Assert.DoesNotContain(PathMarker, diagnosticPayload);
        Assert.DoesNotContain(QueryMarker, diagnosticPayload);
    }
    [Theory]
    [InlineData("access-denied", DiagnosticFailureCategory.AccessDenied)]
    [InlineData("cryptographic", DiagnosticFailureCategory.CryptographicFailure)]
    [InlineData("invalid-data", DiagnosticFailureCategory.InvalidData)]
    [InlineData("resource-not-found", DiagnosticFailureCategory.ResourceNotFound)]
    [InlineData("timeout", DiagnosticFailureCategory.Timeout)]
    [InlineData("persistence", DiagnosticFailureCategory.PersistenceFailure)]
    [InlineData("io", DiagnosticFailureCategory.IoFailure)]
    [InlineData("invalid-operation", DiagnosticFailureCategory.InvalidOperation)]
    [InlineData("unexpected", DiagnosticFailureCategory.UnexpectedFailure)]
    public void LogSafeFailure_ReportsSafeFailureCategoryWithoutExceptionPayload(
        string failureKind,
        DiagnosticFailureCategory expectedCategory)
    {
        // Arrange
        var logger = CreateLogger();
        string sensitiveMessage = CreateSensitiveFailureMessage();

        Exception exception = failureKind switch
        {
            "access-denied" =>
                new UnauthorizedAccessException(sensitiveMessage),

            "cryptographic" =>
                new CryptographicException(sensitiveMessage),

            "invalid-data" =>
                new InvalidDataException(sensitiveMessage),

            "resource-not-found" =>
                new FileNotFoundException(sensitiveMessage),

            "timeout" =>
                new TimeoutException(sensitiveMessage),

            "persistence" =>
                new SyntheticDatabaseException(sensitiveMessage),

            "io" =>
                new IOException(sensitiveMessage),

            "invalid-operation" =>
                new InvalidOperationException(sensitiveMessage),

            "unexpected" =>
                new Exception(sensitiveMessage),

            _ => throw new ArgumentOutOfRangeException(nameof(failureKind))
        };

        // Act
        logger.LogSafeFailure(
            LogLevel.Error,
            OperationCode,
            SafeMessage,
            exception);

        // Assert
        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);

        Assert.Equal(
            expectedCategory,
            Assert.IsType<DiagnosticFailureCategory>(
                entry.Properties["FailureCategory"]));

        Assert.Equal(
            exception.GetType().Name,
            Assert.IsType<string>(entry.Properties["FailureType"]));

        var diagnosticPayload = BuildDiagnosticPayload(entry);

        Assert.DoesNotContain(sensitiveMessage, diagnosticPayload);
        Assert.DoesNotContain(DocumentMarker, diagnosticPayload);
        Assert.DoesNotContain(SecretMarker, diagnosticPayload);
        Assert.DoesNotContain(PathMarker, diagnosticPayload);
        Assert.DoesNotContain(QueryMarker, diagnosticPayload);
    }
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.None)]
    public void LogSafeFailure_RejectsNonFailureLevels(LogLevel level)
    {
        // Arrange
        var logger = CreateLogger();
        var exception = CreateSyntheticException();

        // Act
        var act = () => logger.LogSafeFailure(
            level,
            OperationCode,
            SafeMessage,
            exception);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void LogSafeFailure_RejectsBlankOperationCode(string operationCode)
    {
        // Arrange
        var logger = CreateLogger();
        var exception = CreateSyntheticException();

        // Act
        var act = () => logger.LogSafeFailure(
            LogLevel.Error,
            operationCode,
            SafeMessage,
            exception);

        // Assert
        Assert.Throws<ArgumentException>(act);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void LogSafeFailure_RejectsBlankSafeMessage(string safeMessage)
    {
        // Arrange
        var logger = CreateLogger();
        var exception = CreateSyntheticException();

        // Act
        var act = () => logger.LogSafeFailure(
            LogLevel.Error,
            OperationCode,
            safeMessage,
            exception);

        // Assert
        Assert.Throws<ArgumentException>(act);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void LogSafeFailure_RejectsNullLogger()
    {
        // Arrange
        ILogger<SafeFailureLoggingExtensionsTests> logger = null!;
        var exception = CreateSyntheticException();

        // Act
        var act = () => logger.LogSafeFailure(
            LogLevel.Error,
            OperationCode,
            SafeMessage,
            exception);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void LogSafeFailure_RejectsNullException()
    {
        // Arrange
        var logger = CreateLogger();
        Exception exception = null!;

        // Act
        var act = () => logger.LogSafeFailure(
            LogLevel.Error,
            OperationCode,
            SafeMessage,
            exception);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
        Assert.Empty(logger.Entries);
    }

    private static RecordingLogger<SafeFailureLoggingExtensionsTests>
        CreateLogger()
    {
        return new RecordingLogger<SafeFailureLoggingExtensionsTests>();
    }

    private static IOException CreateSyntheticException()
    {
        return new IOException("Synthetic test failure.");
    }

    private static IOException CreateSensitiveException()
    {
        var innerException = new InvalidOperationException(
            $"Nested failure: {SecretMarker}; path={PathMarker}; query={QueryMarker}");

        return new IOException(
            $"Document import failed for {DocumentMarker}.",
            innerException);
    }

    private static string CreateSensitiveFailureMessage()
    {
        return
            $"document={DocumentMarker}; " +
            $"secret={SecretMarker}; " +
            $"path={PathMarker}; " +
            $"query={QueryMarker}";
    }

    private static string BuildDiagnosticPayload(CapturedLogEntry entry)
    {
        return
            $"{entry.Message}{Environment.NewLine}" +
            string.Join(
                Environment.NewLine,
                entry.Properties.Select(
                    property => $"{property.Key}={property.Value}"));
    }

    private sealed class SyntheticDatabaseException : DbException
    {
        public SyntheticDatabaseException(string message)
            : base(message)
        {
        }
    }
    private sealed record CapturedLogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

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

            Entries.Add(
                new CapturedLogEntry(
                    logLevel,
                    formatter(state, exception),
                    exception,
                    properties));
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

using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace DeskVault.UI.Tests;

public sealed class SafeFailureLoggingSerilogSinkTests
{
    [Fact]
    public void LogSafeFailure_WritesSafeContext_WithoutSensitiveExceptionData()
    {
        const string documentSentinel =
            "PBI232_DOCUMENT_CONTENT_SENTINEL";
        const string pathSentinel =
            "PBI232_PRIVATE_PATH_SENTINEL";
        const string keySentinel =
            "PBI232_SECRET_KEY_SENTINEL";
        const string querySentinel =
            "PBI232_QUERY_TERM_SENTINEL";
        const string innerExceptionSentinel =
            "PBI232_INNER_EXCEPTION_SENTINEL";

        const string safeMessage =
            "Document storage operation failed.";

        var operationCode =
            DiagnosticOperationCodes.DocumentStorageFailure;

        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "DeskVault-PBI232-SerilogSinkTests",
            Guid.NewGuid().ToString("N"));

        var logsDirectory = Path.Combine(
            temporaryRoot,
            "Logs");

        Directory.CreateDirectory(logsDirectory);

        try
        {
            var appSettingsPath = FindAppSettingsPath();

            var sinkPath = Path.Combine(
                logsDirectory,
                "log-.txt");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(appSettingsPath)!)
                .AddJsonFile(
                    Path.GetFileName(appSettingsPath),
                    optional: false,
                    reloadOnChange: false)
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Serilog:WriteTo:0:Args:path"] = sinkPath
                    })
                .Build();
            {
                var fileLogger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .CreateLogger();

                try
                {
                    using (var loggerFactory = new SerilogLoggerFactory(
                        fileLogger,
                        dispose: false))
                    {
                        var logger = loggerFactory.CreateLogger<
                            SafeFailureLoggingSerilogSinkTests>();

                        var exceptionMessage =
                            $"Document={documentSentinel}; " +
                            $"Path={pathSentinel}; " +
                            $"Secret={keySentinel}; " +
                            $"Query={querySentinel}";

                        var exception = new IOException(
                            exceptionMessage,
                            new InvalidOperationException(
                                innerExceptionSentinel));

                        logger.LogSafeFailure(
                            LogLevel.Error,
                            operationCode,
                            safeMessage,
                            exception);
                    }
                }
                finally
                {
                    // Flush and close the actual file sink before reading it.
                    fileLogger.Dispose();
                }
            }

            var logFiles = Directory.GetFiles(
                logsDirectory,
                "log-*.txt");

            Assert.Single(logFiles);

            var renderedOutput = File.ReadAllText(logFiles[0]);

            // The configured file sink must retain useful diagnostics.
            Assert.Contains("[ERR]", renderedOutput);
            Assert.Contains(safeMessage, renderedOutput);
            Assert.Contains(operationCode, renderedOutput);
            Assert.Contains("FailureType=IOException", renderedOutput);

            // Exception data must not reach the rendered file output.
            Assert.DoesNotContain(documentSentinel, renderedOutput);
            Assert.DoesNotContain(pathSentinel, renderedOutput);
            Assert.DoesNotContain(keySentinel, renderedOutput);
            Assert.DoesNotContain(querySentinel, renderedOutput);
            Assert.DoesNotContain(innerExceptionSentinel, renderedOutput);
            Assert.DoesNotContain(
                nameof(InvalidOperationException),
                renderedOutput);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static string FindAppSettingsPath()
    {
        DirectoryInfo? current =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "DeskVault.UI",
                "appsettings.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/DeskVault.UI/appsettings.json " +
            "from the test execution directory.");
    }
}
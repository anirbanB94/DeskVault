using Microsoft.Extensions.Logging;

namespace DeskVault.Shared.Extensions;

/// <summary>
/// Provides logging methods for failures without forwarding exception
/// details to the logging provider.
/// </summary>
public static class SafeFailureLoggingExtensions
{
    /// <summary>
    /// Logs a failure with a stable operation identifier and a
    /// source-controlled message without forwarding exception details.
    /// </summary>
    /// <typeparam name="TCategory">The logger category type.</typeparam>
    /// <param name="logger">The logger receiving the diagnostic event.</param>
    /// <param name="level">The severity of the failure.</param>
    /// <param name="operationCode">
    /// A stable identifier defined in DiagnosticOperationCodes.
    /// </param>
    /// <param name="safeMessage">
    /// A source-controlled safe message, such as a LogMessages constant.
    /// Do not pass runtime-sensitive values.
    /// </param>
    /// <param name="exception">
    /// The exception associated with the failure. Its details are not
    /// forwarded to the logging provider.
    /// </param>
    /// <param name="failureCategory">
    /// An optional controlled category supplied when the caller knows a more
    /// precise domain outcome than exception-type classification can provide.
    /// </param>
    public static void LogSafeFailure<TCategory>(
        this ILogger<TCategory> logger,
        LogLevel level,
        string operationCode,
        string safeMessage,
        Exception exception,
        DiagnosticFailureCategory? failureCategory = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(exception);

        if (level is not (
            LogLevel.Warning or
            LogLevel.Error or
            LogLevel.Critical))
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "Safe failure diagnostics require Warning, Error, or Critical severity.");
        }

        DiagnosticFailureCategory effectiveFailureCategory =
            failureCategory ??
            DiagnosticFailureClassifier.Classify(exception);

        logger.Log(
            level,
            "{SafeMessage} OperationCode={OperationCode} FailureType={FailureType} FailureCategory={FailureCategory}.",
            safeMessage,
            operationCode,
            exception.GetType().Name,
            effectiveFailureCategory);
    }
}

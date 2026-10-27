using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Tests.TestDoubles;

internal static class SafeDiagnosticLogAssertions
{
    private const string SensitiveDocumentMarker =
        "SENSITIVE_DOCUMENT_7D3F";

    private const string SensitiveSecretMarker =
        "SENSITIVE_SECRET_51A9";

    private const string SensitivePathMarker =
        @"C:\Sensitive\PRIVATE_DOCUMENT_82BD.txt";

    private const string SensitiveQueryMarker =
        "SENSITIVE_QUERY_2C91";

    internal static string CreateSensitiveDetails()
    {
        return
            $"document={SensitiveDocumentMarker}; " +
            $"secret={SensitiveSecretMarker}; " +
            $"path={SensitivePathMarker}; " +
            $"query={SensitiveQueryMarker}";
    }

    internal static void AssertSafeFailureLog(
        IEnumerable<CapturedLogEntry> entries,
        LogLevel expectedLevel,
        string expectedOperationCode,
        string expectedSafeMessage,
        string expectedFailureType)
    {
        var entry = Assert.Single(
            entries,
            item =>
                item.Properties.TryGetValue(
                    "OperationCode",
                    out var operationCode) &&
                Equals(operationCode, expectedOperationCode));

        Assert.Equal(expectedLevel, entry.Level);
        Assert.Null(entry.Exception);

        Assert.Equal(
            expectedSafeMessage,
            Assert.IsType<string>(entry.Properties["SafeMessage"]));

        Assert.Equal(
            expectedOperationCode,
            Assert.IsType<string>(entry.Properties["OperationCode"]));

        Assert.Equal(
            expectedFailureType,
            Assert.IsType<string>(entry.Properties["FailureType"]));

        var diagnosticPayload =
            $"{entry.Message}{Environment.NewLine}" +
            string.Join(
                Environment.NewLine,
                entry.Properties.Select(
                    property => $"{property.Key}={property.Value}"));

        Assert.DoesNotContain(SensitiveDocumentMarker, diagnosticPayload);
        Assert.DoesNotContain(SensitiveSecretMarker, diagnosticPayload);
        Assert.DoesNotContain(SensitivePathMarker, diagnosticPayload);
        Assert.DoesNotContain(SensitiveQueryMarker, diagnosticPayload);
    }
}

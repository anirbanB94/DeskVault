using System.Data.Common;
using System.IO;
using System.Security.Cryptography;

namespace DeskVault.Shared.Extensions;

/// <summary>
/// Controlled categories for failure diagnostics. These categories
/// intentionally exclude exception messages and other runtime payloads.
/// </summary>
public enum DiagnosticFailureCategory
{
    AccessDenied,
    DuplicateConflict,
    CryptographicFailure,
    InvalidData,
    ResourceNotFound,
    Timeout,
    PersistenceFailure,
    IoFailure,
    InvalidOperation,
    UnexpectedFailure
}

/// <summary>
/// Maps known exception types to controlled diagnostic categories.
/// The mapping does not inspect or expose exception messages or inner exceptions.
/// </summary>
public static class DiagnosticFailureClassifier
{
    public static DiagnosticFailureCategory Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            UnauthorizedAccessException =>
                DiagnosticFailureCategory.AccessDenied,

            CryptographicException =>
                DiagnosticFailureCategory.CryptographicFailure,

            InvalidDataException =>
                DiagnosticFailureCategory.InvalidData,

            FileNotFoundException or DirectoryNotFoundException =>
                DiagnosticFailureCategory.ResourceNotFound,

            TimeoutException =>
                DiagnosticFailureCategory.Timeout,

            DbException =>
                DiagnosticFailureCategory.PersistenceFailure,

            IOException =>
                DiagnosticFailureCategory.IoFailure,

            InvalidOperationException =>
                DiagnosticFailureCategory.InvalidOperation,

            _ =>
                DiagnosticFailureCategory.UnexpectedFailure
        };
    }
}

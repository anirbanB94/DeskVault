using System.Security.Cryptography;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Applies the application security-session authorization boundary to
/// asynchronous protected-key retrieval.
/// </summary>
internal static class SessionAuthorizedKeyRetrieval
{
    private const string AccessDeniedMessage =
        "Protected vault key retrieval requires an unchanged authenticated security session.";

    /// <summary>
    /// Retrieves key material only when the security session remains
    /// authenticated at the same revision for the duration of retrieval.
    /// </summary>
    /// <remarks>
    /// The caller owns the returned key buffer and must clear it after use.
    /// If cancellation or session revalidation prevents the material from
    /// being returned, this method clears the locally acquired buffer.
    /// The final snapshot check is the retrieval authorization point. It
    /// cannot revoke key material already returned to another operation.
    /// </remarks>
    internal static async Task<EncryptionKeyMaterial> GetKeyAsync(
        IApplicationSecuritySession securitySession,
        Func<CancellationToken, Task<EncryptionKeyMaterial>> retrieveKeyAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(securitySession);
        ArgumentNullException.ThrowIfNull(retrieveKeyAsync);

        cancellationToken.ThrowIfCancellationRequested();

        ApplicationSecuritySessionSnapshot authorizedSnapshot =
            securitySession.CurrentSnapshot;

        if (!authorizedSnapshot.IsProtectedAccessAvailable)
        {
            throw new UnauthorizedAccessException(
                AccessDeniedMessage);
        }

        EncryptionKeyMaterial keyMaterial =
            await retrieveKeyAsync(cancellationToken)
                .ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ApplicationSecuritySessionSnapshot currentSnapshot =
                securitySession.CurrentSnapshot;

            if (!currentSnapshot.IsProtectedAccessAvailable ||
                currentSnapshot.Revision != authorizedSnapshot.Revision)
            {
                throw new UnauthorizedAccessException(
                    AccessDeniedMessage);
            }

            return keyMaterial;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(
                keyMaterial.KeyBytes);

            throw;
        }
    }
}

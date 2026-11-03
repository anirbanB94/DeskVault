using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Enforces the application security-session boundary before returning
/// database-encryption key material.
/// </summary>
internal sealed class SessionAuthorizedDatabaseEncryptionKeyService
    : IDatabaseEncryptionKeyService
{
    private readonly IDatabaseEncryptionKeyService _innerService;
    private readonly IApplicationSecuritySession _securitySession;

    public SessionAuthorizedDatabaseEncryptionKeyService(
        IDatabaseEncryptionKeyService innerService,
        IApplicationSecuritySession securitySession)
    {
        ArgumentNullException.ThrowIfNull(innerService);
        ArgumentNullException.ThrowIfNull(securitySession);

        _innerService = innerService;
        _securitySession = securitySession;
    }

    /// <inheritdoc />
    public Task<EncryptionKeyMaterial> GetKeyAsync(
        CancellationToken cancellationToken = default)
    {
        return SessionAuthorizedKeyRetrieval.GetKeyAsync(
            _securitySession,
            token => _innerService.GetKeyAsync(token),
            cancellationToken);
    }
}

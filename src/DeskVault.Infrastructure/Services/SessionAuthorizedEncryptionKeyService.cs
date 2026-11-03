using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Enforces the application security-session boundary before returning
/// document-encryption key material.
/// </summary>
internal sealed class SessionAuthorizedEncryptionKeyService
    : IEncryptionKeyService
{
    private readonly IEncryptionKeyService _innerService;
    private readonly IApplicationSecuritySession _securitySession;

    public SessionAuthorizedEncryptionKeyService(
        IEncryptionKeyService innerService,
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

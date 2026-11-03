using DeskVault.Application.Interfaces;

namespace DeskVault.UI.Hosting;

public sealed class DeskVaultStartupCoordinator
{
    private readonly IApplicationSecuritySession _securitySession;
    private readonly Func<CancellationToken, Task> _authenticate;
    private readonly Func<CancellationToken, Task> _initializeDatabase;

    public DeskVaultStartupCoordinator(
        IApplicationSecuritySession securitySession,
        Func<CancellationToken, Task> authenticate,
        Func<CancellationToken, Task> initializeDatabase)
    {
        ArgumentNullException.ThrowIfNull(securitySession);
        ArgumentNullException.ThrowIfNull(authenticate);
        ArgumentNullException.ThrowIfNull(initializeDatabase);

        _securitySession = securitySession;
        _authenticate = authenticate;
        _initializeDatabase = initializeDatabase;
    }

    public async Task<bool> AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _authenticate(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // A successful completion racing with cancellation must not
            // leave protected access granted as the startup form closes.
            if (_securitySession.CurrentSnapshot.IsProtectedAccessAvailable)
            {
                _securitySession.Lock();
            }

            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (_securitySession.CurrentSnapshot.IsProtectedAccessAvailable)
            {
                _securitySession.Lock();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return _securitySession.CurrentSnapshot.IsProtectedAccessAvailable;
    }

    public async Task InitializeDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_securitySession.CurrentSnapshot.IsProtectedAccessAvailable)
        {
            throw new InvalidOperationException(
                "Protected database initialization requires an authenticated application security session.");
        }

        await _initializeDatabase(cancellationToken);
    }
}

using DeskVault.Shared.Extensions;
using DeskVault.UI.Hosting;
using DeskVault.UI.Resources;
using Microsoft.Extensions.Logging;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.UI.Forms;

public sealed partial class StartupAuthenticationForm : Form
{
    private readonly DeskVaultStartupCoordinator _startupCoordinator;
    private readonly ILogger<StartupAuthenticationForm> _logger;
    private readonly CancellationTokenSource _authenticationCancellation = new();

    private bool _authenticationInProgress;
    private bool _closeRequested;

    public StartupAuthenticationForm(
        DeskVaultStartupCoordinator startupCoordinator,
        ILogger<StartupAuthenticationForm> logger)
    {
        ArgumentNullException.ThrowIfNull(startupCoordinator);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();

        _startupCoordinator = startupCoordinator;
        _logger = logger;

        Text = UiMessages.WindowsHelloAuthenticationTitle;
        titleLabel.Text = UiMessages.WindowsHelloAuthenticationTitle;
        descriptionLabel.Text = UiMessages.WindowsHelloAuthenticationDescription;
        statusLabel.Text = UiMessages.WindowsHelloAuthenticationReady;
        authenticateButton.Text = UiMessages.WindowsHelloAuthenticationButton;
        cancelButton.Text = UiMessages.CancelButton;

        AcceptButton = authenticateButton;
        CancelButton = cancelButton;

        authenticateButton.Click += OnAuthenticateButtonClick;
        cancelButton.Click += OnCancelButtonClick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        authenticateButton.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_authenticationInProgress)
        {
            e.Cancel = true;
            RequestClose();
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        _authenticationCancellation.Dispose();
    }

    private async void OnAuthenticateButtonClick(
        object? sender,
        EventArgs e)
    {
        if (_authenticationInProgress || _closeRequested)
        {
            return;
        }

        _authenticationInProgress = true;
        authenticateButton.Enabled = false;
        cancelButton.Enabled = true;

        statusLabel.Text =
            UiMessages.WindowsHelloAuthenticationInProgress;

        try
        {
            bool authenticated =
                await _startupCoordinator.AuthenticateAsync(
                    _authenticationCancellation.Token);

            if (authenticated)
            {
                CloseWithResult(DialogResult.OK);
                return;
            }

            statusLabel.Text =
                UiMessages.WindowsHelloAuthenticationFailed;
        }
        catch (OperationCanceledException)
            when (_authenticationCancellation.IsCancellationRequested)
        {
            // The form is closing. The finally block completes the close
            // after the authentication service has observed cancellation.
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.ApplicationAuthenticationProviderFailure,
                LogMessages.StartupAuthenticationFailed,
                ex);

            statusLabel.Text =
                UiMessages.WindowsHelloAuthenticationFailed;
        }
        finally
        {
            _authenticationInProgress = false;

            if (_closeRequested)
            {
                CloseWithResult(DialogResult.Cancel);
            }
            else if (!IsDisposed && !Disposing)
            {
                authenticateButton.Enabled = true;
                cancelButton.Enabled = true;
            }
        }
    }

    private void OnCancelButtonClick(
        object? sender,
        EventArgs e)
    {
        RequestClose();
    }

    private void RequestClose()
    {
        if (_authenticationInProgress)
        {
            _closeRequested = true;
            authenticateButton.Enabled = false;
            cancelButton.Enabled = false;

            statusLabel.Text =
                UiMessages.WindowsHelloAuthenticationCancelling;

            if (!_authenticationCancellation.IsCancellationRequested)
            {
                _authenticationCancellation.Cancel();
            }

            return;
        }

        CloseWithResult(DialogResult.Cancel);
    }

    private void CloseWithResult(DialogResult result)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        _authenticationInProgress = false;
        DialogResult = result;
        Close();
    }
}

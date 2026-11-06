using DeskVault.UI.Rendering;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;

namespace DeskVault.UI.Forms;

public partial class DocumentViewForm :
    Form,
    IDocumentWorkspaceView
{
    private readonly IDocumentContentRendererResolver _rendererResolver;
    private long _presentationOperationVersion;
    private readonly int _uiThreadId;
    private readonly SynchronizationContext _uiSynchronizationContext;
    private readonly ISessionBoundDialogCoordinator _dialogCoordinator;

    public DocumentViewForm(
        IDocumentContentRendererResolver rendererResolver,
        ISessionBoundDialogCoordinator dialogCoordinator)
    {
        ArgumentNullException.ThrowIfNull(rendererResolver);
        ArgumentNullException.ThrowIfNull(dialogCoordinator);

        _uiThreadId = Environment.CurrentManagedThreadId;
        _uiSynchronizationContext =
            SynchronizationContext.Current ??
            new WindowsFormsSynchronizationContext();

        InitializeComponent();

        _rendererResolver = rendererResolver;
        _dialogCoordinator = dialogCoordinator;

        Text = UiMessages.DocumentWorkspaceTitle;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 600);
    }

    public event EventHandler OpenExternallyRequested = null!;

    public event EventHandler DocumentInformationRequested = null!;

    public event EventHandler RemoveDocumentRequested = null!;

    public event EventHandler CloseWorkspaceRequested = null!;

    public async Task ShowDocumentAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        long presentationVersion =
            Interlocked.Increment(
                ref _presentationOperationVersion);

        Panel? renderHost = null;
        bool presentationCommitted = false;

        try
        {
            DocumentContentHost.Clear(
                documentContentPanel);

            cancellationToken.ThrowIfCancellationRequested();

            documentTitleLabel.Text = fileName;

            documentMetadataLabel.Text =
                UiMessages.DocumentWorkspaceMetadata;

            unsupportedPreviewPanel.Visible = false;
            documentContentPanel.Visible = true;
            documentContentPanel.BringToFront();

            IDocumentContentRenderer renderer =
                _rendererResolver.Resolve(fileName);

            renderHost = new Panel
            {
                Dock = DockStyle.Fill
            };

            documentContentPanel.Controls.Add(renderHost);

            await renderer.RenderAsync(
                renderHost,
                documentStream,
                fileName,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested ||
                presentationVersion !=
                    Volatile.Read(ref _presentationOperationVersion))
            {
                throw new OperationCanceledException(
                    "The document presentation operation was invalidated.",
                    cancellationToken);
            }

            presentationCommitted = true;

            Show();
            BringToFront();
            Activate();
        }
        catch (OperationCanceledException)
        {
            if (presentationVersion ==
                Volatile.Read(ref _presentationOperationVersion))
            {
                ClearPresentationCore();
            }

            throw;
        }
        finally
        {
            if (!presentationCommitted)
            {
                renderHost?.Dispose();
            }
        }
    }

    public void ShowDocumentInformation(
        string displayName,
        string fileName,
        string fileType,
        DateTime importedAt,
        string status,
        string sha256Hash,
        long expectedSessionRevision = 0)
    {
        string message =
            $"{UiMessages.DisplayNameLabel} {displayName}{Environment.NewLine}" +
            $"{UiMessages.DocumentFileNameLabel} {fileName}{Environment.NewLine}" +
            $"{UiMessages.DocumentFileTypeLabel} {fileType}{Environment.NewLine}" +
            $"{UiMessages.ImportedAtLabel} {importedAt.ToLocalTime():g}{Environment.NewLine}" +
            $"{UiMessages.StatusLabel} {status}{Environment.NewLine}" +
            $"{UiMessages.Sha256Label} {sha256Hash}";

        ShowSessionBoundMessage(
            message,
            UiMessages.DocumentInformation,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            expectedSessionRevision);
    }

    public bool ConfirmRemoval(
        string fileName,
        long expectedSessionRevision = 0)
    {
        DialogResult result =
            ShowSessionBoundMessage(
                UiMessages.ConfirmRemoveDocument(fileName),
                UiMessages.RemoveDocumentTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                expectedSessionRevision);

        return result == DialogResult.Yes;
    }

    private DialogResult ShowSessionBoundMessage(
        string message,
        string title,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        long expectedSessionRevision)
    {
        using SessionBoundMessageDialog dialog =
            new(
                message,
                title,
                buttons,
                icon);

        return _dialogCoordinator.ShowDialog(
            dialog,
            expectedSessionRevision,
            this);
    }

    public void ShowUnsupportedPreview(
        string message)
    {
        unsupportedDocumentPreviewMessageLabel.Text =
            message;

        documentContentPanel.Visible = false;
        unsupportedPreviewPanel.Visible = true;
        unsupportedPreviewPanel.BringToFront();

        Show();
        BringToFront();
        Activate();
    }

    public void PostToUi(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsDisposed || Disposing)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            action();
            return;
        }

        if (IsHandleCreated)
        {
            try
            {
                BeginInvoke(action);
                return;
            }
            catch (InvalidOperationException) when (IsDisposed || Disposing)
            {
                return;
            }
            catch (InvalidOperationException) when (!IsHandleCreated)
            {
                // Fall back to the synchronization context captured by this form.
            }
        }

        _uiSynchronizationContext.Post(
            _ =>
            {
                if (!IsDisposed && !Disposing)
                {
                    action();
                }
            },
            null);
    }

    public void ClearPresentation()
    {
        Interlocked.Increment(
            ref _presentationOperationVersion);

        ClearPresentationCore();
    }

    private void ClearPresentationCore()
    {
        workspaceContextMenu.Close(
            ToolStripDropDownCloseReason.CloseCalled);

        DocumentContentHost.Clear(
            documentContentPanel);

        documentTitleLabel.Text =
            string.Empty;

        documentMetadataLabel.Text =
            string.Empty;

        unsupportedDocumentPreviewMessageLabel.Text =
            string.Empty;

        documentContentPanel.Visible = true;
        unsupportedPreviewPanel.Visible = false;

        Hide();
    }

    public void ActivateWorkspace()
    {
        Show();
        BringToFront();
        Activate();
    }

    public void CloseWorkspace()
    {
        Hide();
    }

    public void ShowError(
        string message,
        string title,
        long expectedSessionRevision = 0)
    {
        ShowSessionBoundMessage(
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            expectedSessionRevision);
    }

    protected override void OnParentChanged(
        EventArgs e)
    {
        base.OnParentChanged(e);

        if (Parent is not null)
        {
            workspaceHeaderPanel.Visible = false;
        }
    }

    private void openExternallyButton_Click(
        object? sender,
        EventArgs e)
    {
        OpenExternallyRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void backButton_Click(
        object? sender,
        EventArgs e)
    {
        Hide();
    }

    private void workspaceMenuButton_Click(
        object? sender,
        EventArgs e)
    {
        workspaceContextMenu.Show(
            workspaceMenuButton,
            new Point(
                0,
                workspaceMenuButton.Height));
    }

    private void documentInformationMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        DocumentInformationRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void removeDocumentMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        RemoveDocumentRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void closeWorkspaceMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        CloseWorkspaceRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void closeButton_Click(
        object sender,
        EventArgs e)
    {
        Hide();
    }

    protected override void OnFormClosing(
        FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}

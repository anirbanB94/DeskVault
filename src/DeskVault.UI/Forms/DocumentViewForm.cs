using DeskVault.UI.Rendering;
using DeskVault.UI.Resources;
using DeskVault.UI.Views;

namespace DeskVault.UI.Forms;

public partial class DocumentViewForm :
    Form,
    IDocumentWorkspaceView
{
    private readonly IDocumentContentRendererResolver _rendererResolver;

    public DocumentViewForm(
        IDocumentContentRendererResolver rendererResolver)
    {
        InitializeComponent();

        _rendererResolver = rendererResolver;

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
        documentTitleLabel.Text = fileName;
        documentMetadataLabel.Text =
            UiMessages.DocumentWorkspaceMetadata;

        unsupportedPreviewPanel.Visible = false;
        documentContentPanel.Visible = true;
        documentContentPanel.BringToFront();

        IDocumentContentRenderer renderer =
            _rendererResolver.Resolve(fileName);

        await renderer.RenderAsync(
            documentContentPanel,
            documentStream,
            fileName,
            cancellationToken);

        Show();
        BringToFront();
        Activate();
    }

    public void ShowDocumentInformation(
        string displayName,
        string fileName,
        string fileType,
        DateTime importedAt,
        string status,
        string sha256Hash)
    {
        MessageBox.Show(
            this,
            $"{UiMessages.DisplayNameLabel} {displayName}{Environment.NewLine}" +
            $"{UiMessages.DocumentFileNameLabel} {fileName}{Environment.NewLine}" +
            $"{UiMessages.DocumentFileTypeLabel} {fileType}{Environment.NewLine}" +
            $"{UiMessages.ImportedAtLabel} {importedAt.ToLocalTime():g}{Environment.NewLine}" +
            $"{UiMessages.StatusLabel} {status}{Environment.NewLine}" +
            $"{UiMessages.Sha256Label} {sha256Hash}",
            UiMessages.DocumentInformation,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    public bool ConfirmRemoval(
        string fileName)
    {
        var result = MessageBox.Show(
            this,
            UiMessages.ConfirmRemoveDocument(fileName),
            UiMessages.RemoveDocumentTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.Yes;
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
        string title)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
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

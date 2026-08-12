using DeskVault.UI.Rendering;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;

namespace DeskVault.UI.Forms;

public partial class DocumentViewForm :
    Form,
    IDocumentWorkspace,
    IDocumentWorkspaceView
{
    private readonly IDocumentContentRendererResolver _rendererResolver;
    private readonly IDocumentViewer _documentViewer;

    private Stream? _currentDocumentStream;
    private string? _currentFileName;

    public DocumentViewForm(
        IDocumentContentRendererResolver rendererResolver,
        IDocumentViewer documentViewer)
    {
        InitializeComponent();

        _rendererResolver = rendererResolver;
        _documentViewer = documentViewer;

        Text = UiMessages.DocumentWorkspaceTitle;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 600);
    }

    public async Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        documentTitleLabel.Text = fileName;
        documentMetadataLabel.Text =
            UiMessages.DocumentWorkspaceMetadata;

        _currentDocumentStream = documentStream;
        _currentFileName = fileName;

        unsupportedPreviewPanel.Visible = false;
        documentContentPanel.Visible = true;
        documentContentPanel.BringToFront();

        try
        {
            IDocumentContentRenderer renderer =
                _rendererResolver.Resolve(fileName);

            await renderer.RenderAsync(
                documentContentPanel,
                documentStream,
                fileName,
                cancellationToken);
        }
        catch (NotSupportedException)
        {
            ShowUnsupportedPreview(
                UiMessages.UnsupportedDocumentPreviewMessage);
        }

        Show();
        BringToFront();
        Activate();
    }

    public void ShowUnsupportedPreview(
        string message)
    {
        unsupportedDocumentPreviewMessageLabel.Text =
            message;

        documentContentPanel.Visible = false;
        unsupportedPreviewPanel.Visible = true;
        unsupportedPreviewPanel.BringToFront();
    }

    public async Task OpenExternallyAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await _documentViewer.OpenAsync(
            documentStream,
            fileName,
            cancellationToken);
    }

    private async void openExternallyButton_Click(
        object? sender,
        EventArgs e)
    {
        if (_currentDocumentStream is null ||
            string.IsNullOrWhiteSpace(_currentFileName))
        {
            return;
        }

        try
        {
            _currentDocumentStream.Position = 0;

            await OpenExternallyAsync(
                _currentDocumentStream,
                _currentFileName);
        }
        catch (Exception)
        {
            MessageBox.Show(
                this,
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
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

using DeskVault.UI.Resources;
using DeskVault.UI.Services;

namespace DeskVault.UI.Forms;

public partial class DocumentViewForm : Form, IDocumentWorkspace
{
    public DocumentViewForm()
    {
        InitializeComponent();

        Text = UiMessages.DocumentWorkspaceTitle;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 600);
    }

    public Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        documentTitleLabel.Text = fileName;
        documentMetadataLabel.Text =
            UiMessages.DocumentWorkspaceTitle;

        Show();

        return Task.CompletedTask;
    }
}

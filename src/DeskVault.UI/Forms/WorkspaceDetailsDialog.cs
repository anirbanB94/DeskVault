using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

public partial class WorkspaceDetailsDialog : Form
{
    public WorkspaceDetailsDialog(
        string workspaceName,
        string? workspaceDescription)
    {
        InitializeComponent();

        workspaceNameTextBox.Text = workspaceName;
        descriptionTextBox.Text = workspaceDescription ?? string.Empty;

        saveButton.Click += OnSaveButtonClick;
    }

    public string WorkspaceName =>
        workspaceNameTextBox.Text.Trim();

    public string? WorkspaceDescription
    {
        get
        {
            string description =
                descriptionTextBox.Text.Trim();

            return string.IsNullOrWhiteSpace(description)
                ? null
                : description;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        workspaceNameTextBox.Focus();
        workspaceNameTextBox.SelectAll();
    }

    private void OnSaveButtonClick(
        object? sender,
        EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WorkspaceName))
        {
            validationLabel.Text =
                UiMessages.WorkspaceNameRequired;

            workspaceNameTextBox.Focus();

            return;
        }

        validationLabel.Text = string.Empty;
        DialogResult = DialogResult.OK;
    }
}

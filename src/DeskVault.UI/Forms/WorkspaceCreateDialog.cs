using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

public partial class WorkspaceCreateDialog : Form
{
    public WorkspaceCreateDialog()
    {
        InitializeComponent();

        createButton.Click += OnCreateButtonClick;
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
    }

    private void OnCreateButtonClick(
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

namespace DeskVault.UI.Forms;

using DeskVault.UI.Resources;

partial class WorkspaceCreateDialog
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Label workspaceNameLabel;
    private System.Windows.Forms.TextBox workspaceNameTextBox;
    private System.Windows.Forms.Label descriptionLabel;
    private System.Windows.Forms.TextBox descriptionTextBox;
    private System.Windows.Forms.Label validationLabel;
    private System.Windows.Forms.Button cancelButton;
    private System.Windows.Forms.Button createButton;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        workspaceNameLabel = new Label();
        workspaceNameTextBox = new TextBox();
        descriptionLabel = new Label();
        descriptionTextBox = new TextBox();
        validationLabel = new Label();
        cancelButton = new Button();
        createButton = new Button();
        SuspendLayout();
        // 
        // workspaceNameLabel
        // 
        workspaceNameLabel.AutoSize = true;
        workspaceNameLabel.Location = new Point(59, 60);
        workspaceNameLabel.Margin = new Padding(6, 0, 6, 0);
        workspaceNameLabel.Name = "workspaceNameLabel";
        workspaceNameLabel.Size = new Size(215, 32);
        workspaceNameLabel.TabIndex = 0;
        workspaceNameLabel.Text = UiMessages.RequiredWorkspaceNameLabel;
        // 
        // workspaceNameTextBox
        // 
        workspaceNameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        workspaceNameTextBox.Location = new Point(59, 107);
        workspaceNameTextBox.Margin = new Padding(6, 6, 6, 6);
        workspaceNameTextBox.MaxLength = 260;
        workspaceNameTextBox.Name = "workspaceNameTextBox";
        workspaceNameTextBox.Size = new Size(992, 39);
        workspaceNameTextBox.TabIndex = 1;
        // 
        // descriptionLabel
        // 
        descriptionLabel.AutoSize = true;
        descriptionLabel.Location = new Point(59, 196);
        descriptionLabel.Margin = new Padding(6, 0, 6, 0);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new Size(140, 32);
        descriptionLabel.TabIndex = 2;
        descriptionLabel.Text = UiMessages.DescriptionLabel;
        // 
        // descriptionTextBox
        // 
        descriptionTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        descriptionTextBox.Location = new Point(59, 243);
        descriptionTextBox.Margin = new Padding(6, 6, 6, 6);
        descriptionTextBox.MaxLength = 400;
        descriptionTextBox.Multiline = true;
        descriptionTextBox.Name = "descriptionTextBox";
        descriptionTextBox.ScrollBars = ScrollBars.Vertical;
        descriptionTextBox.Size = new Size(992, 247);
        descriptionTextBox.TabIndex = 3;
        // 
        // validationLabel
        // 
        validationLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        validationLabel.Location = new Point(59, 533);
        validationLabel.Margin = new Padding(6, 0, 6, 0);
        validationLabel.Name = "validationLabel";
        validationLabel.Size = new Size(995, 51);
        validationLabel.TabIndex = 4;
        // 
        // cancelButton
        // 
        cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(702, 623);
        cancelButton.Margin = new Padding(6, 6, 6, 6);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(167, 68);
        cancelButton.TabIndex = 5;
        cancelButton.Text = UiMessages.CancelButton;
        cancelButton.UseVisualStyleBackColor = true;
        // 
        // createButton
        // 
        createButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        createButton.Location = new Point(888, 623);
        createButton.Margin = new Padding(6, 6, 6, 6);
        createButton.Name = "createButton";
        createButton.Size = new Size(167, 68);
        createButton.TabIndex = 6;
        createButton.Text = UiMessages.CreateButton;
        createButton.UseVisualStyleBackColor = true;
        // 
        // WorkspaceCreateDialog
        // 
        AcceptButton = createButton;
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new Size(1114, 751);
        Controls.Add(createButton);
        Controls.Add(cancelButton);
        Controls.Add(validationLabel);
        Controls.Add(descriptionTextBox);
        Controls.Add(descriptionLabel);
        Controls.Add(workspaceNameTextBox);
        Controls.Add(workspaceNameLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(6, 6, 6, 6);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WorkspaceCreateDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = UiMessages.CreateWorkspaceTitle;
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}

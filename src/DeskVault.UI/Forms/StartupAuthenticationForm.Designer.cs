#nullable enable

namespace DeskVault.UI.Forms;

partial class StartupAuthenticationForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer? components = null;

    private System.Windows.Forms.Label titleLabel = null!;
    private System.Windows.Forms.Label descriptionLabel = null!;
    private System.Windows.Forms.Label statusLabel = null!;
    private System.Windows.Forms.Button authenticateButton = null!;
    private System.Windows.Forms.Button cancelButton = null!;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
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
    /// Required method for Designer support.
    /// Do not modify the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        titleLabel = new System.Windows.Forms.Label();
        descriptionLabel = new System.Windows.Forms.Label();
        statusLabel = new System.Windows.Forms.Label();
        authenticateButton = new System.Windows.Forms.Button();
        cancelButton = new System.Windows.Forms.Button();
        SuspendLayout();

        // titleLabel
        titleLabel.AutoSize = false;
        titleLabel.Location = new System.Drawing.Point(24, 22);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new System.Drawing.Size(472, 32);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "Unlock DeskVault";

        // descriptionLabel
        descriptionLabel.AutoSize = false;
        descriptionLabel.Location = new System.Drawing.Point(24, 66);
        descriptionLabel.Name = "descriptionLabel";
        descriptionLabel.Size = new System.Drawing.Size(472, 48);
        descriptionLabel.TabIndex = 1;
        descriptionLabel.Text = "Verify your identity with Windows Hello to unlock your protected vault.";

        // statusLabel
        statusLabel.AutoSize = false;
        statusLabel.Location = new System.Drawing.Point(24, 126);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new System.Drawing.Size(472, 38);
        statusLabel.TabIndex = 2;
        statusLabel.Text = "DeskVault is waiting for Windows Hello verification.";

        // authenticateButton
        authenticateButton.Location = new System.Drawing.Point(24, 184);
        authenticateButton.Name = "authenticateButton";
        authenticateButton.Size = new System.Drawing.Size(220, 32);
        authenticateButton.TabIndex = 3;
        authenticateButton.Text = "Verify with Windows Hello";
        authenticateButton.UseVisualStyleBackColor = true;

        // cancelButton
        cancelButton.Location = new System.Drawing.Point(406, 184);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new System.Drawing.Size(90, 32);
        cancelButton.TabIndex = 4;
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;

        // StartupAuthenticationForm
        AcceptButton = authenticateButton;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new System.Drawing.Size(520, 240);
        Controls.Add(cancelButton);
        Controls.Add(authenticateButton);
        Controls.Add(statusLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(titleLabel);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "StartupAuthenticationForm";
        ShowInTaskbar = true;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "Unlock DeskVault";

        ResumeLayout(false);
    }

    #endregion
}

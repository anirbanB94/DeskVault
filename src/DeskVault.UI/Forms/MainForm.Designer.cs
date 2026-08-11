namespace DeskVault.UI.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Panel headerPanel;
    private System.Windows.Forms.Label titleLabel;
    private System.Windows.Forms.Button importButton;
    private System.Windows.Forms.Button openButton;
    private System.Windows.Forms.Button removeButton;
    private System.Windows.Forms.Panel contentPanel;
    private System.Windows.Forms.Label emptyStateLabel;
    private System.Windows.Forms.DataGridView documentGridView;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        headerPanel = new Panel();
        removeButton = new Button();
        openButton = new Button();
        importButton = new Button();
        titleLabel = new Label();

        contentPanel = new Panel();
        documentGridView = new DataGridView();
        emptyStateLabel = new Label();

        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();

        headerPanel.SuspendLayout();
        contentPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)documentGridView).BeginInit();
        statusStrip.SuspendLayout();
        SuspendLayout();

        // 
        // headerPanel
        // 
        headerPanel.Controls.Add(removeButton);
        headerPanel.Controls.Add(openButton);
        headerPanel.Controls.Add(importButton);
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(0, 0);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(30, 17, 30, 17);
        headerPanel.Size = new Size(1857, 137);
        headerPanel.TabIndex = 1;

        // 
        // removeButton
        // 
        removeButton.Dock = DockStyle.Right;
        removeButton.Enabled = false;
        removeButton.Location = new Point(1047, 17);
        removeButton.Margin = new Padding(6);
        removeButton.Name = "removeButton";
        removeButton.Size = new Size(260, 103);
        removeButton.TabIndex = 0;
        removeButton.Text = "Remove";
        removeButton.UseVisualStyleBackColor = true;

        // 
        // openButton
        // 
        openButton.Dock = DockStyle.Right;
        openButton.Enabled = false;
        openButton.Location = new Point(1307, 17);
        openButton.Margin = new Padding(6);
        openButton.Name = "openButton";
        openButton.Size = new Size(260, 103);
        openButton.TabIndex = 1;
        openButton.Text = "Open Document";
        openButton.UseVisualStyleBackColor = true;

        // 
        // importButton
        // 
        importButton.Dock = DockStyle.Right;
        importButton.Location = new Point(1567, 17);
        importButton.Margin = new Padding(6);
        importButton.Name = "importButton";
        importButton.Size = new Size(260, 103);
        importButton.TabIndex = 2;
        importButton.Text = "Import Document";
        importButton.UseVisualStyleBackColor = true;

        // 
        // titleLabel
        // 
        titleLabel.AutoSize = true;
        titleLabel.Dock = DockStyle.Left;
        titleLabel.Font = new Font("Segoe UI", 16F);
        titleLabel.Location = new Point(30, 17);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(211, 59);
        titleLabel.TabIndex = 3;
        titleLabel.Text = "DeskVault";
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;

        // 
        // contentPanel
        // 
        contentPanel.Controls.Add(documentGridView);
        contentPanel.Controls.Add(emptyStateLabel);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 137);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(30, 34, 30, 34);
        contentPanel.Size = new Size(1857, 1208);
        contentPanel.TabIndex = 0;

        // 
        // documentGridView
        // 
        documentGridView.AllowUserToAddRows = false;
        documentGridView.AllowUserToDeleteRows = false;
        documentGridView.AllowUserToResizeRows = false;
        documentGridView.AutoGenerateColumns = false;
        documentGridView.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        documentGridView.Dock = DockStyle.Fill;
        documentGridView.Location = new Point(30, 34);
        documentGridView.MultiSelect = false;
        documentGridView.Name = "documentGridView";
        documentGridView.ReadOnly = true;
        documentGridView.RowHeadersVisible = false;
        documentGridView.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;
        documentGridView.Size = new Size(1797, 1140);
        documentGridView.TabIndex = 1;
        documentGridView.Visible = false;

        // 
        // emptyStateLabel
        // 
        emptyStateLabel.Dock = DockStyle.Fill;
        emptyStateLabel.Font = new Font("Segoe UI", 12F);
        emptyStateLabel.Location = new Point(30, 34);
        emptyStateLabel.Name = "emptyStateLabel";
        emptyStateLabel.Size = new Size(1797, 1140);
        emptyStateLabel.TabIndex = 0;
        emptyStateLabel.Text = "No documents imported yet.";
        emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;

        // 
        // statusStrip
        // 
        statusStrip.ImageScalingSize = new Size(32, 32);
        statusStrip.Items.AddRange(
            new ToolStripItem[]
            {
                statusLabel
            });
        statusStrip.Location = new Point(0, 1345);
        statusStrip.Name = "statusStrip";
        statusStrip.Padding = new Padding(2, 0, 26, 0);
        statusStrip.Size = new Size(1857, 42);
        statusStrip.TabIndex = 2;

        // 
        // statusLabel
        // 
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(78, 32);
        statusLabel.Text = "Ready";

        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1857, 1387);
        Controls.Add(contentPanel);
        Controls.Add(headerPanel);
        Controls.Add(statusStrip);
        MinimumSize = new Size(1463, 986);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;

        headerPanel.ResumeLayout(false);
        headerPanel.PerformLayout();

        contentPanel.ResumeLayout(false);

        ((System.ComponentModel.ISupportInitialize)documentGridView).EndInit();

        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();

        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}

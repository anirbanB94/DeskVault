namespace DeskVault.UI.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Panel headerPanel;
    private System.Windows.Forms.Label titleLabel;
    private System.Windows.Forms.Panel searchPanel;
    private System.Windows.Forms.TextBox searchTextBox;
    private System.Windows.Forms.Button searchButton;
    private System.Windows.Forms.FlowLayoutPanel actionPanel;
    private System.Windows.Forms.Button importButton;
    private System.Windows.Forms.Button openButton;
    private System.Windows.Forms.Button removeButton;
    private System.Windows.Forms.Button reprocessButton;
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
        actionPanel = new FlowLayoutPanel();
        importButton = new Button();
        openButton = new Button();
        reprocessButton = new Button();
        removeButton = new Button();
        searchPanel = new Panel();
        searchButton = new Button();
        searchTextBox = new TextBox();
        titleLabel = new Label();
        contentPanel = new Panel();
        documentGridView = new DataGridView();
        emptyStateLabel = new Label();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        headerPanel.SuspendLayout();
        actionPanel.SuspendLayout();
        searchPanel.SuspendLayout();
        contentPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)documentGridView).BeginInit();
        statusStrip.SuspendLayout();
        SuspendLayout();
        //
        // headerPanel
        //
        headerPanel.BackColor = SystemColors.Window;
        headerPanel.Controls.Add(searchPanel);
        headerPanel.Controls.Add(actionPanel);
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(0, 0);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(28, 18, 28, 18);
        headerPanel.Size = new Size(1500, 104);
        headerPanel.TabIndex = 0;
        //
        // actionPanel
        //
        actionPanel.AutoSize = true;
        actionPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        actionPanel.Controls.Add(importButton);
        actionPanel.Controls.Add(openButton);
        actionPanel.Controls.Add(reprocessButton);
        actionPanel.Controls.Add(removeButton);
        actionPanel.Dock = DockStyle.Right;
        actionPanel.FlowDirection = FlowDirection.LeftToRight;
        actionPanel.Location = new Point(930, 18);
        actionPanel.Margin = new Padding(0);
        actionPanel.Name = "actionPanel";
        actionPanel.Padding = new Padding(0);
        actionPanel.Size = new Size(542, 68);
        actionPanel.TabIndex = 4;
        actionPanel.WrapContents = false;
        //
        // importButton
        //
        importButton.BackColor = SystemColors.Control;
        importButton.Cursor = Cursors.Hand;
        importButton.FlatAppearance.BorderSize = 0;
        importButton.FlatStyle = FlatStyle.Flat;
        importButton.Font = new Font("Segoe UI Semibold", 10F);
        importButton.Location = new Point(8, 12);
        importButton.Margin = new Padding(8, 12, 4, 12);
        importButton.Name = "importButton";
        importButton.Size = new Size(125, 44);
        importButton.TabIndex = 0;
        importButton.Text = "Import";
        importButton.UseVisualStyleBackColor = false;
        //
        // openButton
        //
        openButton.BackColor = SystemColors.Control;
        openButton.Cursor = Cursors.Hand;
        openButton.Enabled = false;
        openButton.FlatAppearance.BorderSize = 0;
        openButton.FlatStyle = FlatStyle.Flat;
        openButton.Font = new Font("Segoe UI Semibold", 10F);
        openButton.Location = new Point(141, 12);
        openButton.Margin = new Padding(4, 12, 4, 12);
        openButton.Name = "openButton";
        openButton.Size = new Size(125, 44);
        openButton.TabIndex = 1;
        openButton.Text = "Open";
        openButton.UseVisualStyleBackColor = false;
        //
        // reprocessButton
        //
        reprocessButton.BackColor = SystemColors.Control;
        reprocessButton.Cursor = Cursors.Hand;
        reprocessButton.Enabled = false;
        reprocessButton.FlatAppearance.BorderSize = 0;
        reprocessButton.FlatStyle = FlatStyle.Flat;
        reprocessButton.Font = new Font("Segoe UI Semibold", 10F);
        reprocessButton.Location = new Point(274, 12);
        reprocessButton.Margin = new Padding(4, 12, 4, 12);
        reprocessButton.Name = "reprocessButton";
        reprocessButton.Size = new Size(125, 44);
        reprocessButton.TabIndex = 2;
        reprocessButton.Text = "Reprocess";
        reprocessButton.UseVisualStyleBackColor = false;
        //
        // removeButton
        //
        removeButton.BackColor = SystemColors.Control;
        removeButton.Cursor = Cursors.Hand;
        removeButton.Enabled = false;
        removeButton.FlatAppearance.BorderSize = 0;
        removeButton.FlatStyle = FlatStyle.Flat;
        removeButton.Font = new Font("Segoe UI Semibold", 10F);
        removeButton.Location = new Point(407, 12);
        removeButton.Margin = new Padding(4, 12, 8, 12);
        removeButton.Name = "removeButton";
        removeButton.Size = new Size(125, 44);
        removeButton.TabIndex = 3;
        removeButton.Text = "Remove";
        removeButton.UseVisualStyleBackColor = false;
        //
        // searchPanel
        //
        searchPanel.Anchor = AnchorStyles.Top
            | AnchorStyles.Left
            | AnchorStyles.Right;
        searchPanel.Controls.Add(searchButton);
        searchPanel.Controls.Add(searchTextBox);
        searchPanel.Location = new Point(240, 18);
        searchPanel.Margin = new Padding(0);
        searchPanel.Name = "searchPanel";
        searchPanel.Padding = new Padding(0);
        searchPanel.Size = new Size(670, 68);
        searchPanel.TabIndex = 3;
        //
        // searchButton
        //
        searchButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        searchButton.BackColor = SystemColors.Control;
        searchButton.Cursor = Cursors.Hand;
        searchButton.FlatAppearance.BorderSize = 0;
        searchButton.FlatStyle = FlatStyle.Flat;
        searchButton.Font = new Font("Segoe UI Semibold", 10F);
        searchButton.Location = new Point(555, 12);
        searchButton.Name = "searchButton";
        searchButton.Size = new Size(115, 44);
        searchButton.TabIndex = 1;
        searchButton.Text = "Search";
        searchButton.UseVisualStyleBackColor = false;
        //
        // searchTextBox
        //
        searchTextBox.Anchor = AnchorStyles.Top
            | AnchorStyles.Left
            | AnchorStyles.Right;
        searchTextBox.BorderStyle = BorderStyle.FixedSingle;
        searchTextBox.Font = new Font("Segoe UI", 11F);
        searchTextBox.Location = new Point(0, 12);
        searchTextBox.Name = "searchTextBox";
        searchTextBox.PlaceholderText = "Search documents...";
        searchTextBox.Size = new Size(545, 45);
        searchTextBox.TabIndex = 0;
        //
        // titleLabel
        //
        titleLabel.AutoSize = false;
        titleLabel.Dock = DockStyle.Left;
        titleLabel.Font = new Font("Segoe UI Semibold", 18F);
        titleLabel.Location = new Point(28, 18);
        titleLabel.Margin = new Padding(0);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(212, 68);
        titleLabel.TabIndex = 2;
        titleLabel.Text = "DeskVault";
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // contentPanel
        //
        contentPanel.BackColor = SystemColors.Control;
        contentPanel.Controls.Add(documentGridView);
        contentPanel.Controls.Add(emptyStateLabel);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 104);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(28, 24, 28, 24);
        contentPanel.Size = new Size(1500, 850);
        contentPanel.TabIndex = 1;
        //
        // documentGridView
        //
        documentGridView.AllowUserToAddRows = false;
        documentGridView.AllowUserToDeleteRows = false;
        documentGridView.AllowUserToResizeRows = false;
        documentGridView.AutoGenerateColumns = false;
        documentGridView.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        documentGridView.BackgroundColor = SystemColors.Window;
        documentGridView.BorderStyle = BorderStyle.None;
        documentGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        documentGridView.ColumnHeadersBorderStyle =
            DataGridViewHeaderBorderStyle.None;
        documentGridView.ColumnHeadersDefaultCellStyle =
            new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = SystemColors.Control,
                Font = new Font("Segoe UI Semibold", 9.5F),
                ForeColor = SystemColors.ControlText,
                SelectionBackColor = SystemColors.Control,
                SelectionForeColor = SystemColors.ControlText,
                Padding = new Padding(12, 0, 12, 0)
            };
        documentGridView.ColumnHeadersHeight = 42;
        documentGridView.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        documentGridView.DefaultCellStyle =
            new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = SystemColors.Window,
                Font = new Font("Segoe UI", 10F),
                ForeColor = SystemColors.ControlText,
                SelectionBackColor = SystemColors.Highlight,
                SelectionForeColor = SystemColors.HighlightText,
                Padding = new Padding(12, 0, 12, 0)
            };
        documentGridView.Dock = DockStyle.Fill;
        documentGridView.EnableHeadersVisualStyles = false;
        documentGridView.GridColor = SystemColors.ControlLight;
        documentGridView.Location = new Point(28, 24);
        documentGridView.MultiSelect = false;
        documentGridView.Name = "documentGridView";
        documentGridView.ReadOnly = true;
        documentGridView.RowHeadersVisible = false;
        documentGridView.RowTemplate.Height = 42;
        documentGridView.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;
        documentGridView.Size = new Size(1444, 802);
        documentGridView.TabIndex = 1;
        documentGridView.Visible = false;
        //
        // emptyStateLabel
        //
        emptyStateLabel.Dock = DockStyle.Fill;
        emptyStateLabel.Font = new Font("Segoe UI", 11F);
        emptyStateLabel.ForeColor = SystemColors.GrayText;
        emptyStateLabel.Location = new Point(28, 24);
        emptyStateLabel.Name = "emptyStateLabel";
        emptyStateLabel.Size = new Size(1444, 802);
        emptyStateLabel.TabIndex = 0;
        emptyStateLabel.Text =
            "No documents imported yet.\r\n\r\n" +
            "Use Import to add your first document.";
        emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // statusStrip
        //
        statusStrip.ImageScalingSize = new Size(20, 20);
        statusStrip.Items.AddRange(
            new ToolStripItem[]
            {
                statusLabel
            });
        statusStrip.Location = new Point(0, 954);
        statusStrip.Name = "statusStrip";
        statusStrip.Padding = new Padding(8, 0, 8, 0);
        statusStrip.Size = new Size(1500, 28);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 2;
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(42, 23);
        statusLabel.Text = "Ready";
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Control;
        ClientSize = new Size(1500, 982);
        Controls.Add(contentPanel);
        Controls.Add(headerPanel);
        Controls.Add(statusStrip);
        MinimumSize = new Size(1100, 700);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        headerPanel.ResumeLayout(false);
        actionPanel.ResumeLayout(false);
        searchPanel.ResumeLayout(false);
        searchPanel.PerformLayout();
        contentPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)documentGridView).EndInit();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}

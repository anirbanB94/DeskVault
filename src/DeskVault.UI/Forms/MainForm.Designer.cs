using DeskVault.UI.Resources;
using System.Windows.Forms;

namespace DeskVault.UI.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private Panel headerPanel;
    private Label titleLabel;
    private Panel searchPanel;
    private TextBox searchTextBox;
    private ComboBox searchFileTypeComboBox;
    private Button clearSearchButton;
    private Button searchButton;
    private Panel contentPanel;
    private TableLayoutPanel contentLayoutPanel;
    private Panel documentsPanel;
    private Panel documentsHeaderPanel;
    private Label documentsTitleLabel;
    private FlowLayoutPanel actionPanel;
    private Button importButton;
    private Button openButton;
    private Button removeButton;
    private Button reprocessButton;
    private Label emptyStateLabel;
    private DataGridView documentGridView;
    private Panel loadMorePanel;
    private Button loadMoreButton;
    private Panel workspacesPanel;
    private Panel workspacesHeaderPanel;
    private Label workspacesTitleLabel;
    private FlowLayoutPanel workspaceActionPanel;
    private Button createWorkspaceButton;
    private Button openWorkspaceButton;
    private Button removeWorkspaceButton;
    private Label workspaceEmptyStateLabel;
    private DataGridView workspaceGridView;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
    private ToolStripStatusLabel documentsCountLabel;
    private ToolStripStatusLabel workspaceCountSeparatorLabel;
    private ToolStripStatusLabel workspacesCountLabel;

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
        var dataGridViewCellStyle1 = new DataGridViewCellStyle();
        var dataGridViewCellStyle2 = new DataGridViewCellStyle();
        dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle1.BackColor = SystemColors.Control;
        dataGridViewCellStyle1.Font = new Font("Segoe UI Semibold", 9F);
        dataGridViewCellStyle1.ForeColor = SystemColors.ControlText;
        dataGridViewCellStyle1.SelectionBackColor = SystemColors.Control;
        dataGridViewCellStyle1.SelectionForeColor = SystemColors.ControlText;
        dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
        dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle2.BackColor = SystemColors.Control;
        dataGridViewCellStyle2.Font = new Font("Segoe UI Semibold", 9F);
        dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
        dataGridViewCellStyle2.SelectionBackColor = SystemColors.Control;
        dataGridViewCellStyle2.SelectionForeColor = SystemColors.ControlText;
        dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
        headerPanel = new Panel();
        searchPanel = new Panel();
        searchButton = new Button();
        clearSearchButton = new Button();
        searchFileTypeComboBox = new ComboBox();
        searchTextBox = new TextBox();
        titleLabel = new Label();
        contentPanel = new Panel();
        contentLayoutPanel = new TableLayoutPanel();
        documentsPanel = new Panel();
        documentGridView = new DataGridView();
        loadMorePanel = new Panel();
        loadMoreButton = new Button();
        emptyStateLabel = new Label();
        documentsHeaderPanel = new Panel();
        actionPanel = new FlowLayoutPanel();
        importButton = new Button();
        openButton = new Button();
        reprocessButton = new Button();
        removeButton = new Button();
        documentsTitleLabel = new Label();
        workspacesPanel = new Panel();
        workspaceGridView = new DataGridView();
        workspaceEmptyStateLabel = new Label();
        workspacesHeaderPanel = new Panel();
        workspaceActionPanel = new FlowLayoutPanel();
        createWorkspaceButton = new Button();
        openWorkspaceButton = new Button();
        removeWorkspaceButton = new Button();
        workspacesTitleLabel = new Label();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        documentsCountLabel = new ToolStripStatusLabel();
        workspaceCountSeparatorLabel = new ToolStripStatusLabel();
        workspacesCountLabel = new ToolStripStatusLabel();
        headerPanel.SuspendLayout();
        searchPanel.SuspendLayout();
        contentPanel.SuspendLayout();
        contentLayoutPanel.SuspendLayout();
        documentsPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)documentGridView).BeginInit();
        loadMorePanel.SuspendLayout();
        documentsHeaderPanel.SuspendLayout();
        actionPanel.SuspendLayout();
        workspacesPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)workspaceGridView).BeginInit();
        workspacesHeaderPanel.SuspendLayout();
        workspaceActionPanel.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        //
        // headerPanel
        //
        headerPanel.BackColor = SystemColors.Window;
        headerPanel.Controls.Add(searchPanel);
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(0, 0);
        headerPanel.Margin = new Padding(4);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(36, 23, 36, 23);
        headerPanel.Size = new Size(1950, 133);
        headerPanel.TabIndex = 0;
        //
        // searchPanel
        //
        searchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchPanel.Controls.Add(searchButton);
        searchPanel.Controls.Add(clearSearchButton);
        searchPanel.Controls.Add(searchFileTypeComboBox);
        searchPanel.Controls.Add(searchTextBox);
        searchPanel.Location = new Point(312, 23);
        searchPanel.Margin = new Padding(0);
        searchPanel.Name = "searchPanel";
        searchPanel.Size = new Size(1602, 87);
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
        searchButton.Location = new Point(1452, 15);
        searchButton.Margin = new Padding(4);
        searchButton.Name = "searchButton";
        searchButton.Size = new Size(150, 56);
        searchButton.TabIndex = 3;
        searchButton.Text = UiMessages.SearchButton;
        searchButton.UseVisualStyleBackColor = false;
        //
        // clearSearchButton
        //
        clearSearchButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        clearSearchButton.BackColor = SystemColors.Control;
        clearSearchButton.Cursor = Cursors.Hand;
        clearSearchButton.FlatAppearance.BorderSize = 0;
        clearSearchButton.FlatStyle = FlatStyle.Flat;
        clearSearchButton.Font = new Font("Segoe UI Semibold", 10F);
        clearSearchButton.Location = new Point(1296, 15);
        clearSearchButton.Margin = new Padding(4);
        clearSearchButton.Name = "clearSearchButton";
        clearSearchButton.Size = new Size(143, 56);
        clearSearchButton.TabIndex = 2;
        clearSearchButton.Text = UiMessages.ClearSearchButton;
        clearSearchButton.UseVisualStyleBackColor = false;
        //
        // searchFileTypeComboBox
        //
        searchFileTypeComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        searchFileTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        searchFileTypeComboBox.Font = new Font("Segoe UI", 10F);
        searchFileTypeComboBox.FormattingEnabled = true;
        searchFileTypeComboBox.Location = new Point(1029, 17);
        searchFileTypeComboBox.Margin = new Padding(4);
        searchFileTypeComboBox.Name = "searchFileTypeComboBox";
        searchFileTypeComboBox.Size = new Size(244, 45);
        searchFileTypeComboBox.TabIndex = 1;
        //
        // searchTextBox
        //
        searchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchTextBox.BorderStyle = BorderStyle.FixedSingle;
        searchTextBox.Font = new Font("Segoe UI", 11F);
        searchTextBox.Location = new Point(0, 15);
        searchTextBox.Margin = new Padding(4);
        searchTextBox.Name = "searchTextBox";
        searchTextBox.PlaceholderText = UiMessages.SearchDocumentsPlaceholder;
        searchTextBox.Size = new Size(1021, 47);
        searchTextBox.TabIndex = 0;
        //
        // titleLabel
        //
        titleLabel.Dock = DockStyle.Left;
        titleLabel.Font = new Font("Segoe UI Semibold", 18F);
        titleLabel.Location = new Point(36, 23);
        titleLabel.Margin = new Padding(0);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(276, 87);
        titleLabel.TabIndex = 2;
        titleLabel.Text = UiMessages.DeskVaultTitle;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // contentPanel
        //
        contentPanel.BackColor = SystemColors.Control;
        contentPanel.Controls.Add(contentLayoutPanel);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 133);
        contentPanel.Margin = new Padding(4);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(36, 31, 36, 31);
        contentPanel.Size = new Size(1950, 1082);
        contentPanel.TabIndex = 1;
        //
        // contentLayoutPanel
        //
        contentLayoutPanel.ColumnCount = 1;
        contentLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contentLayoutPanel.Controls.Add(documentsPanel, 0, 0);
        contentLayoutPanel.Controls.Add(workspacesPanel, 0, 1);
        contentLayoutPanel.Dock = DockStyle.Fill;
        contentLayoutPanel.Location = new Point(36, 31);
        contentLayoutPanel.Margin = new Padding(4);
        contentLayoutPanel.Name = "contentLayoutPanel";
        contentLayoutPanel.RowCount = 2;
        contentLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        contentLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        contentLayoutPanel.Size = new Size(1878, 1020);
        contentLayoutPanel.TabIndex = 0;
        //
        // documentsPanel
        //
        documentsPanel.BackColor = SystemColors.Window;
        documentsPanel.Controls.Add(documentGridView);
        documentsPanel.Controls.Add(loadMorePanel);
        documentsPanel.Controls.Add(emptyStateLabel);
        documentsPanel.Controls.Add(documentsHeaderPanel);
        documentsPanel.Dock = DockStyle.Fill;
        documentsPanel.Location = new Point(4, 4);
        documentsPanel.Margin = new Padding(4);
        documentsPanel.Name = "documentsPanel";
        documentsPanel.Padding = new Padding(21, 20, 21, 20);
        documentsPanel.Size = new Size(1870, 604);
        documentsPanel.TabIndex = 0;
        //
        // documentGridView
        //
        documentGridView.AllowUserToAddRows = false;
        documentGridView.AllowUserToDeleteRows = false;
        documentGridView.AllowUserToResizeRows = false;
        documentGridView.BackgroundColor = SystemColors.Window;
        documentGridView.BorderStyle = BorderStyle.None;
        documentGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        documentGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        documentGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
        documentGridView.ColumnHeadersHeight = 42;
        documentGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        documentGridView.Dock = DockStyle.Fill;
        documentGridView.EnableHeadersVisualStyles = false;
        documentGridView.GridColor = SystemColors.ControlLight;
        documentGridView.Location = new Point(21, 87);
        documentGridView.Margin = new Padding(4);
        documentGridView.MultiSelect = false;
        documentGridView.Name = "documentGridView";
        documentGridView.ReadOnly = true;
        documentGridView.RowHeadersVisible = false;
        documentGridView.RowHeadersWidth = 82;
        documentGridView.RowTemplate.Height = 42;
        documentGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        documentGridView.Size = new Size(1828, 430);
        documentGridView.TabIndex = 1;
        documentGridView.Visible = false;
        //
        // loadMorePanel
        //
        loadMorePanel.Controls.Add(loadMoreButton);
        loadMorePanel.Dock = DockStyle.Bottom;
        loadMorePanel.Location = new Point(21, 517);
        loadMorePanel.Margin = new Padding(4);
        loadMorePanel.Name = "loadMorePanel";
        loadMorePanel.Size = new Size(1828, 67);
        loadMorePanel.TabIndex = 3;
        //
        // loadMoreButton
        //
        loadMoreButton.Anchor = AnchorStyles.Top;
        loadMoreButton.BackColor = SystemColors.Control;
        loadMoreButton.Cursor = Cursors.Hand;
        loadMoreButton.FlatAppearance.BorderSize = 0;
        loadMoreButton.FlatStyle = FlatStyle.Flat;
        loadMoreButton.Font = new Font("Segoe UI Semibold", 10F);
        loadMoreButton.Location = new Point(826, 5);
        loadMoreButton.Margin = new Padding(4);
        loadMoreButton.Name = "loadMoreButton";
        loadMoreButton.Size = new Size(177, 56);
        loadMoreButton.TabIndex = 0;
        loadMoreButton.Text = UiMessages.LoadMoreButton;
        loadMoreButton.UseVisualStyleBackColor = false;
        loadMoreButton.Visible = false;
        //
        // emptyStateLabel
        //
        emptyStateLabel.Dock = DockStyle.Fill;
        emptyStateLabel.Font = new Font("Segoe UI", 11F);
        emptyStateLabel.ForeColor = SystemColors.GrayText;
        emptyStateLabel.Location = new Point(21, 87);
        emptyStateLabel.Margin = new Padding(4, 0, 4, 0);
        emptyStateLabel.Name = "emptyStateLabel";
        emptyStateLabel.Size = new Size(1828, 497);
        emptyStateLabel.TabIndex = 0;
        emptyStateLabel.Text =
            UiMessages.NoDocumentsImportedMessage +
            "\r\n\r\n" +
            UiMessages.ImportFirstDocumentMessage;
        emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // documentsHeaderPanel
        //
        documentsHeaderPanel.Controls.Add(actionPanel);
        documentsHeaderPanel.Controls.Add(documentsTitleLabel);
        documentsHeaderPanel.Dock = DockStyle.Top;
        documentsHeaderPanel.Location = new Point(21, 20);
        documentsHeaderPanel.Margin = new Padding(4);
        documentsHeaderPanel.Name = "documentsHeaderPanel";
        documentsHeaderPanel.Size = new Size(1828, 67);
        documentsHeaderPanel.TabIndex = 4;
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
        actionPanel.Location = new Point(1130, 0);
        actionPanel.Margin = new Padding(0);
        actionPanel.Name = "actionPanel";
        actionPanel.Size = new Size(698, 67);
        actionPanel.TabIndex = 1;
        actionPanel.WrapContents = false;
        //
        // importButton
        //
        importButton.BackColor = SystemColors.Control;
        importButton.Cursor = Cursors.Hand;
        importButton.FlatAppearance.BorderSize = 0;
        importButton.FlatStyle = FlatStyle.Flat;
        importButton.Font = new Font("Segoe UI Semibold", 9.5F);
        importButton.Location = new Point(10, 5);
        importButton.Margin = new Padding(10, 5, 5, 5);
        importButton.Name = "importButton";
        importButton.Size = new Size(162, 56);
        importButton.TabIndex = 0;
        importButton.Text = UiMessages.ImportButton;
        importButton.UseVisualStyleBackColor = false;
        //
        // openButton
        //
        openButton.BackColor = SystemColors.Control;
        openButton.Cursor = Cursors.Hand;
        openButton.Enabled = false;
        openButton.FlatAppearance.BorderSize = 0;
        openButton.FlatStyle = FlatStyle.Flat;
        openButton.Font = new Font("Segoe UI Semibold", 9.5F);
        openButton.Location = new Point(182, 5);
        openButton.Margin = new Padding(5);
        openButton.Name = "openButton";
        openButton.Size = new Size(162, 56);
        openButton.TabIndex = 1;
        openButton.Text = UiMessages.OpenButton;
        openButton.UseVisualStyleBackColor = false;
        //
        // reprocessButton
        //
        reprocessButton.BackColor = SystemColors.Control;
        reprocessButton.Cursor = Cursors.Hand;
        reprocessButton.Enabled = false;
        reprocessButton.FlatAppearance.BorderSize = 0;
        reprocessButton.FlatStyle = FlatStyle.Flat;
        reprocessButton.Font = new Font("Segoe UI Semibold", 9.5F);
        reprocessButton.Location = new Point(354, 5);
        reprocessButton.Margin = new Padding(5);
        reprocessButton.Name = "reprocessButton";
        reprocessButton.Size = new Size(162, 56);
        reprocessButton.TabIndex = 2;
        reprocessButton.Text = UiMessages.ReprocessButton;
        reprocessButton.UseVisualStyleBackColor = false;
        //
        // removeButton
        //
        removeButton.BackColor = SystemColors.Control;
        removeButton.Cursor = Cursors.Hand;
        removeButton.Enabled = false;
        removeButton.FlatAppearance.BorderSize = 0;
        removeButton.FlatStyle = FlatStyle.Flat;
        removeButton.Font = new Font("Segoe UI Semibold", 9.5F);
        removeButton.Location = new Point(526, 5);
        removeButton.Margin = new Padding(5, 5, 10, 5);
        removeButton.Name = "removeButton";
        removeButton.Size = new Size(162, 56);
        removeButton.TabIndex = 3;
        removeButton.Text = UiMessages.RemoveButton;
        removeButton.UseVisualStyleBackColor = false;
        //
        // documentsTitleLabel
        //
        documentsTitleLabel.Dock = DockStyle.Left;
        documentsTitleLabel.Font = new Font("Segoe UI Semibold", 12F);
        documentsTitleLabel.Location = new Point(0, 0);
        documentsTitleLabel.Margin = new Padding(4, 0, 4, 0);
        documentsTitleLabel.Name = "documentsTitleLabel";
        documentsTitleLabel.Size = new Size(260, 67);
        documentsTitleLabel.TabIndex = 0;
        documentsTitleLabel.Text = UiMessages.DocumentColumnHeader;
        documentsTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // workspacesPanel
        //
        workspacesPanel.BackColor = SystemColors.Window;
        workspacesPanel.Controls.Add(workspaceGridView);
        workspacesPanel.Controls.Add(workspaceEmptyStateLabel);
        workspacesPanel.Controls.Add(workspacesHeaderPanel);
        workspacesPanel.Dock = DockStyle.Fill;
        workspacesPanel.Location = new Point(4, 616);
        workspacesPanel.Margin = new Padding(4);
        workspacesPanel.Name = "workspacesPanel";
        workspacesPanel.Padding = new Padding(21, 20, 21, 20);
        workspacesPanel.Size = new Size(1870, 400);
        workspacesPanel.TabIndex = 1;
        //
        // workspaceGridView
        //
        workspaceGridView.AllowUserToAddRows = false;
        workspaceGridView.AllowUserToDeleteRows = false;
        workspaceGridView.AllowUserToResizeRows = false;
        workspaceGridView.BackgroundColor = SystemColors.Window;
        workspaceGridView.BorderStyle = BorderStyle.None;
        workspaceGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        workspaceGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        workspaceGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
        workspaceGridView.ColumnHeadersHeight = 42;
        workspaceGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        workspaceGridView.Dock = DockStyle.Fill;
        workspaceGridView.EnableHeadersVisualStyles = false;
        workspaceGridView.GridColor = SystemColors.ControlLight;
        workspaceGridView.Location = new Point(21, 87);
        workspaceGridView.Margin = new Padding(4);
        workspaceGridView.MultiSelect = false;
        workspaceGridView.Name = "workspaceGridView";
        workspaceGridView.ReadOnly = true;
        workspaceGridView.RowHeadersVisible = false;
        workspaceGridView.RowHeadersWidth = 82;
        workspaceGridView.RowTemplate.Height = 42;
        workspaceGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        workspaceGridView.Size = new Size(1828, 293);
        workspaceGridView.TabIndex = 1;
        workspaceGridView.Visible = false;
        //
        // workspaceEmptyStateLabel
        //
        workspaceEmptyStateLabel.Dock = DockStyle.Fill;
        workspaceEmptyStateLabel.Font = new Font("Segoe UI", 11F);
        workspaceEmptyStateLabel.ForeColor = SystemColors.GrayText;
        workspaceEmptyStateLabel.Location = new Point(21, 87);
        workspaceEmptyStateLabel.Margin = new Padding(4, 0, 4, 0);
        workspaceEmptyStateLabel.Name = "workspaceEmptyStateLabel";
        workspaceEmptyStateLabel.Size = new Size(1828, 293);
        workspaceEmptyStateLabel.TabIndex = 2;
        workspaceEmptyStateLabel.Text = UiMessages.NoWorkspacesAvailable;
        workspaceEmptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // workspacesHeaderPanel
        //
        workspacesHeaderPanel.Controls.Add(workspaceActionPanel);
        workspacesHeaderPanel.Controls.Add(workspacesTitleLabel);
        workspacesHeaderPanel.Dock = DockStyle.Top;
        workspacesHeaderPanel.Location = new Point(21, 20);
        workspacesHeaderPanel.Margin = new Padding(4);
        workspacesHeaderPanel.Name = "workspacesHeaderPanel";
        workspacesHeaderPanel.Size = new Size(1828, 67);
        workspacesHeaderPanel.TabIndex = 3;
        //
        // workspaceActionPanel
        //
        workspaceActionPanel.AutoSize = true;
        workspaceActionPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        workspaceActionPanel.Controls.Add(createWorkspaceButton);
        workspaceActionPanel.Controls.Add(openWorkspaceButton);
        workspaceActionPanel.Controls.Add(removeWorkspaceButton);
        workspaceActionPanel.Dock = DockStyle.Right;
        workspaceActionPanel.Location = new Point(1302, 0);
        workspaceActionPanel.Margin = new Padding(0);
        workspaceActionPanel.Name = "workspaceActionPanel";
        workspaceActionPanel.Size = new Size(526, 67);
        workspaceActionPanel.TabIndex = 1;
        workspaceActionPanel.WrapContents = false;
        //
        // createWorkspaceButton
        //
        createWorkspaceButton.BackColor = SystemColors.Control;
        createWorkspaceButton.Cursor = Cursors.Hand;
        createWorkspaceButton.FlatAppearance.BorderSize = 0;
        createWorkspaceButton.FlatStyle = FlatStyle.Flat;
        createWorkspaceButton.Font = new Font("Segoe UI Semibold", 9.5F);
        createWorkspaceButton.Location = new Point(10, 5);
        createWorkspaceButton.Margin = new Padding(10, 5, 5, 5);
        createWorkspaceButton.Name = "createWorkspaceButton";
        createWorkspaceButton.Size = new Size(162, 56);
        createWorkspaceButton.TabIndex = 0;
        createWorkspaceButton.Text = UiMessages.CreateButton;
        createWorkspaceButton.UseVisualStyleBackColor = false;
        //
        // openWorkspaceButton
        //
        openWorkspaceButton.BackColor = SystemColors.Control;
        openWorkspaceButton.Cursor = Cursors.Hand;
        openWorkspaceButton.Enabled = false;
        openWorkspaceButton.FlatAppearance.BorderSize = 0;
        openWorkspaceButton.FlatStyle = FlatStyle.Flat;
        openWorkspaceButton.Font = new Font("Segoe UI Semibold", 9.5F);
        openWorkspaceButton.Location = new Point(182, 5);
        openWorkspaceButton.Margin = new Padding(5);
        openWorkspaceButton.Name = "openWorkspaceButton";
        openWorkspaceButton.Size = new Size(162, 56);
        openWorkspaceButton.TabIndex = 1;
        openWorkspaceButton.Text = UiMessages.OpenButton;
        openWorkspaceButton.UseVisualStyleBackColor = false;
        //
        // removeWorkspaceButton
        //
        removeWorkspaceButton.BackColor = SystemColors.Control;
        removeWorkspaceButton.Cursor = Cursors.Hand;
        removeWorkspaceButton.Enabled = false;
        removeWorkspaceButton.FlatAppearance.BorderSize = 0;
        removeWorkspaceButton.FlatStyle = FlatStyle.Flat;
        removeWorkspaceButton.Font = new Font("Segoe UI Semibold", 9.5F);
        removeWorkspaceButton.Location = new Point(354, 5);
        removeWorkspaceButton.Margin = new Padding(5, 5, 10, 5);
        removeWorkspaceButton.Name = "removeWorkspaceButton";
        removeWorkspaceButton.Size = new Size(162, 56);
        removeWorkspaceButton.TabIndex = 2;
        removeWorkspaceButton.Text = UiMessages.RemoveButton;
        removeWorkspaceButton.UseVisualStyleBackColor = false;
        //
        // workspacesTitleLabel
        //
        workspacesTitleLabel.Dock = DockStyle.Left;
        workspacesTitleLabel.Font = new Font("Segoe UI Semibold", 12F);
        workspacesTitleLabel.Location = new Point(0, 0);
        workspacesTitleLabel.Margin = new Padding(4, 0, 4, 0);
        workspacesTitleLabel.Name = "workspacesTitleLabel";
        workspacesTitleLabel.Size = new Size(260, 67);
        workspacesTitleLabel.TabIndex = 0;
        workspacesTitleLabel.Text = UiMessages.Workspaces;
        workspacesTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // statusStrip
        //
        statusStrip.ImageScalingSize = new Size(20, 20);
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, documentsCountLabel, workspaceCountSeparatorLabel, workspacesCountLabel });
        statusStrip.Location = new Point(0, 1215);
        statusStrip.Name = "statusStrip";
        statusStrip.Padding = new Padding(10, 0, 10, 0);
        statusStrip.Size = new Size(1950, 42);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 2;
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(78, 32);
        statusLabel.Spring = true;
        statusLabel.Text = UiMessages.ReadyStatus;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // documentsCountLabel
        //
        documentsCountLabel.Name = "documentsCountLabel";
        documentsCountLabel.Size = new Size(0, 32);
        documentsCountLabel.Text = string.Format(UiMessages.DocumentsCount, 0);
        documentsCountLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // workspaceCountSeparatorLabel
        //
        workspaceCountSeparatorLabel.Name = "workspaceCountSeparatorLabel";
        workspaceCountSeparatorLabel.Size = new Size(16, 32);
        workspaceCountSeparatorLabel.Text = UiMessages.CountSeparator;
        workspaceCountSeparatorLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // workspacesCountLabel
        //
        workspacesCountLabel.Name = "workspacesCountLabel";
        workspacesCountLabel.Size = new Size(0, 32);
        workspacesCountLabel.Text = string.Format(UiMessages.WorkspacesCount, 0);
        workspacesCountLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Control;
        ClientSize = new Size(1950, 1257);
        Controls.Add(contentPanel);
        Controls.Add(headerPanel);
        Controls.Add(statusStrip);
        Margin = new Padding(4);
        MinimumSize = new Size(1422, 876);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        headerPanel.ResumeLayout(false);
        searchPanel.ResumeLayout(false);
        searchPanel.PerformLayout();
        contentPanel.ResumeLayout(false);
        contentLayoutPanel.ResumeLayout(false);
        documentsPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)documentGridView).EndInit();
        loadMorePanel.ResumeLayout(false);
        documentsHeaderPanel.ResumeLayout(false);
        documentsHeaderPanel.PerformLayout();
        actionPanel.ResumeLayout(false);
        workspacesPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)workspaceGridView).EndInit();
        workspacesHeaderPanel.ResumeLayout(false);
        workspacesHeaderPanel.PerformLayout();
        workspaceActionPanel.ResumeLayout(false);
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}

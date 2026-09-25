namespace DeskVault.UI.Forms;

using DeskVault.UI.Resources;

partial class WorkspaceViewForm
{
    private System.ComponentModel.IContainer components = null!;
    private Panel workspaceHeaderPanel = null!;
    private TableLayoutPanel workspaceHeaderLayout = null!;
    private Button backButton = null!;
    private TableLayoutPanel workspaceHeaderInfoLayout = null!;
    private Label workspaceTitleLabel = null!;
    private Label workspaceMetadataLabel = null!;
    private FlowLayoutPanel workspaceActionPanel = null!;
    private Button workspaceMenuButton = null!;
    private Button aiButton = null!;

    private Button tabsNavigationButton = null!;
    private Button sidebarNavigationButton = null!;

    private Panel workspaceContentPanel = null!;
    private Panel documentTabsPanel = null!;
    private TabControl documentTabControl = null!;
    private Label tabsEmptyStateLabel = null!;
    private Panel documentSidebarPanel = null!;
    private Panel documentSidebarContentPanel = null!;
    private ListBox documentListBox = null!;
    private Label sidebarEmptyStateLabel = null!;
    private Panel aiPanel = null!;
    private Label aiTitleLabel = null!;
    private RichTextBox aiConversationTextBox = null!;
    private Panel aiInputPanel = null!;
    private TextBox aiInputTextBox = null!;
    private Button aiSendButton = null!;

    private ContextMenuStrip workspaceContextMenu = null!;
    private ToolStripMenuItem addDocumentsMenuItem = null!;
    private ToolStripMenuItem removeDocumentsMenuItem = null!;
    private ToolStripMenuItem updateWorkspaceDetailsMenuItem = null!;
    private ToolStripMenuItem deleteWorkspaceMenuItem = null!;
    private ToolStripSeparator workspaceMenuSeparator = null!;
    private ToolStripMenuItem viewMenuItem = null!;
    private ToolStripMenuItem tabsMenuItem = null!;
    private ToolStripMenuItem sidebarMenuItem = null!;
    private ToolStripSeparator viewMenuSeparator = null!;
    private ToolStripMenuItem closeWorkspaceMenuItem = null!;

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
        components = new System.ComponentModel.Container();
        workspaceHeaderPanel = new Panel();
        workspaceHeaderLayout = new TableLayoutPanel();
        backButton = new Button();
        workspaceHeaderInfoLayout = new TableLayoutPanel();
        workspaceTitleLabel = new Label();
        workspaceMetadataLabel = new Label();
        workspaceActionPanel = new FlowLayoutPanel();
        workspaceMenuButton = new Button();
        aiButton = new Button();
        tabsNavigationButton = new Button();
        sidebarNavigationButton = new Button();
        workspaceContentPanel = new Panel();
        documentTabsPanel = new Panel();
        documentTabControl = new TabControl();
        tabsEmptyStateLabel = new Label();
        documentSidebarPanel = new Panel();
        documentSidebarContentPanel = new Panel();
        sidebarEmptyStateLabel = new Label();
        documentListBox = new ListBox();
        aiPanel = new Panel();
        aiConversationTextBox = new RichTextBox();
        aiInputPanel = new Panel();
        aiInputTextBox = new TextBox();
        aiSendButton = new Button();
        aiTitleLabel = new Label();
        workspaceContextMenu = new ContextMenuStrip(components);
        addDocumentsMenuItem = new ToolStripMenuItem();
        removeDocumentsMenuItem = new ToolStripMenuItem();
        updateWorkspaceDetailsMenuItem = new ToolStripMenuItem();
        deleteWorkspaceMenuItem = new ToolStripMenuItem();
        workspaceMenuSeparator = new ToolStripSeparator();
        viewMenuItem = new ToolStripMenuItem();
        tabsMenuItem = new ToolStripMenuItem();
        sidebarMenuItem = new ToolStripMenuItem();
        viewMenuSeparator = new ToolStripSeparator();
        closeWorkspaceMenuItem = new ToolStripMenuItem();
        workspaceHeaderPanel.SuspendLayout();
        workspaceHeaderLayout.SuspendLayout();
        workspaceHeaderInfoLayout.SuspendLayout();
        workspaceActionPanel.SuspendLayout();
        workspaceContentPanel.SuspendLayout();
        documentTabsPanel.SuspendLayout();
        documentSidebarPanel.SuspendLayout();
        documentSidebarContentPanel.SuspendLayout();
        aiPanel.SuspendLayout();
        aiInputPanel.SuspendLayout();
        workspaceContextMenu.SuspendLayout();
        SuspendLayout();
        // 
        // workspaceHeaderPanel
        // 
        workspaceHeaderPanel.BackColor = SystemColors.Window;
        workspaceHeaderPanel.Controls.Add(workspaceHeaderLayout);
        workspaceHeaderPanel.Dock = DockStyle.Top;
        workspaceHeaderPanel.Location = new Point(0, 0);
        workspaceHeaderPanel.Margin = new Padding(4, 4, 4, 4);
        workspaceHeaderPanel.Name = "workspaceHeaderPanel";
        workspaceHeaderPanel.Padding = new Padding(36, 23, 36, 23);
        workspaceHeaderPanel.Size = new Size(1950, 133);
        workspaceHeaderPanel.TabIndex = 0;
        // 
        // workspaceHeaderLayout
        // 
        workspaceHeaderLayout.ColumnCount = 3;
        workspaceHeaderLayout.ColumnStyles.Add(new ColumnStyle());
        workspaceHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        workspaceHeaderLayout.ColumnStyles.Add(new ColumnStyle());
        workspaceHeaderLayout.Controls.Add(backButton, 0, 0);
        workspaceHeaderLayout.Controls.Add(workspaceHeaderInfoLayout, 1, 0);
        workspaceHeaderLayout.Controls.Add(workspaceActionPanel, 2, 0);
        workspaceHeaderLayout.Dock = DockStyle.Fill;
        workspaceHeaderLayout.Location = new Point(36, 23);
        workspaceHeaderLayout.Margin = new Padding(0);
        workspaceHeaderLayout.Name = "workspaceHeaderLayout";
        workspaceHeaderLayout.RowCount = 1;
        workspaceHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        workspaceHeaderLayout.Size = new Size(1878, 87);
        workspaceHeaderLayout.TabIndex = 0;
        // 
        // backButton
        // 
        backButton.BackColor = SystemColors.Control;
        backButton.Cursor = Cursors.Hand;
        backButton.FlatAppearance.BorderSize = 0;
        backButton.FlatStyle = FlatStyle.Flat;
        backButton.Font = new Font("Segoe UI Semibold", 10F);
        backButton.Location = new Point(0, 15);
        backButton.Margin = new Padding(0, 15, 26, 15);
        backButton.Name = "backButton";
        backButton.Size = new Size(208, 56);
        backButton.TabIndex = 0;
        backButton.Text = UiMessages.BackToWorkspaces;
        backButton.UseVisualStyleBackColor = false;
        backButton.Click += backButton_Click;
        // 
        // workspaceHeaderInfoLayout
        // 
        workspaceHeaderInfoLayout.ColumnCount = 1;
        workspaceHeaderInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        workspaceHeaderInfoLayout.Controls.Add(workspaceTitleLabel, 0, 0);
        workspaceHeaderInfoLayout.Controls.Add(workspaceMetadataLabel, 0, 1);
        workspaceHeaderInfoLayout.Dock = DockStyle.Fill;
        workspaceHeaderInfoLayout.Location = new Point(234, 0);
        workspaceHeaderInfoLayout.Margin = new Padding(0);
        workspaceHeaderInfoLayout.Name = "workspaceHeaderInfoLayout";
        workspaceHeaderInfoLayout.RowCount = 2;
        workspaceHeaderInfoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        workspaceHeaderInfoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        workspaceHeaderInfoLayout.Size = new Size(1426, 87);
        workspaceHeaderInfoLayout.TabIndex = 1;
        // 
        // workspaceTitleLabel
        // 
        workspaceTitleLabel.AutoEllipsis = true;
        workspaceTitleLabel.Dock = DockStyle.Fill;
        workspaceTitleLabel.Font = new Font("Segoe UI Semibold", 14F);
        workspaceTitleLabel.Location = new Point(0, 0);
        workspaceTitleLabel.Margin = new Padding(0);
        workspaceTitleLabel.Name = "workspaceTitleLabel";
        workspaceTitleLabel.Size = new Size(1426, 52);
        workspaceTitleLabel.TabIndex = 0;
        workspaceTitleLabel.Text = UiMessages.Workspace;
        workspaceTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // workspaceMetadataLabel
        // 
        workspaceMetadataLabel.AutoEllipsis = true;
        workspaceMetadataLabel.Dock = DockStyle.Fill;
        workspaceMetadataLabel.Font = new Font("Segoe UI", 9F);
        workspaceMetadataLabel.ForeColor = SystemColors.GrayText;
        workspaceMetadataLabel.Location = new Point(0, 52);
        workspaceMetadataLabel.Margin = new Padding(0);
        workspaceMetadataLabel.Name = "workspaceMetadataLabel";
        workspaceMetadataLabel.Size = new Size(1426, 35);
        workspaceMetadataLabel.TabIndex = 1;
        workspaceMetadataLabel.Text = UiMessages.WorkspaceDescription;
        workspaceMetadataLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // workspaceActionPanel
        // 
        workspaceActionPanel.AutoSize = true;
        workspaceActionPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        workspaceActionPanel.Controls.Add(workspaceMenuButton);
        workspaceActionPanel.Controls.Add(aiButton);
        workspaceActionPanel.Dock = DockStyle.Fill;
        workspaceActionPanel.Location = new Point(1660, 0);
        workspaceActionPanel.Margin = new Padding(0);
        workspaceActionPanel.Name = "workspaceActionPanel";
        workspaceActionPanel.Size = new Size(218, 87);
        workspaceActionPanel.TabIndex = 2;
        workspaceActionPanel.WrapContents = false;
        // 
        // workspaceMenuButton
        // 
        workspaceMenuButton.BackColor = SystemColors.Control;
        workspaceMenuButton.Cursor = Cursors.Hand;
        workspaceMenuButton.FlatAppearance.BorderSize = 0;
        workspaceMenuButton.FlatStyle = FlatStyle.Flat;
        workspaceMenuButton.Font = new Font("Segoe UI Semibold", 10F);
        workspaceMenuButton.Location = new Point(5, 15);
        workspaceMenuButton.Margin = new Padding(5, 15, 5, 15);
        workspaceMenuButton.Name = "workspaceMenuButton";
        workspaceMenuButton.Size = new Size(109, 56);
        workspaceMenuButton.TabIndex = 0;
        workspaceMenuButton.Text = UiMessages.WorkspaceMenuButton;
        workspaceMenuButton.UseVisualStyleBackColor = false;
        workspaceMenuButton.Click += workspaceMenuButton_Click;
        // 
        // aiButton
        // 
        aiButton.BackColor = SystemColors.Control;
        aiButton.Cursor = Cursors.Hand;
        aiButton.FlatAppearance.BorderSize = 0;
        aiButton.FlatStyle = FlatStyle.Flat;
        aiButton.Font = new Font("Segoe UI Semibold", 10F);
        aiButton.Location = new Point(124, 15);
        aiButton.Margin = new Padding(5, 15, 0, 15);
        aiButton.Name = "aiButton";
        aiButton.Size = new Size(94, 56);
        aiButton.TabIndex = 1;
        aiButton.Text = UiMessages.AiButton;
        aiButton.UseVisualStyleBackColor = false;
        aiButton.Visible = false;   // For MVP 2 this is valid.
        // 
        // tabsNavigationButton
        // 
        tabsNavigationButton.Location = new Point(0, 0);
        tabsNavigationButton.Name = "tabsNavigationButton";
        tabsNavigationButton.Size = new Size(75, 23);
        tabsNavigationButton.TabIndex = 0;
        tabsNavigationButton.Text = UiMessages.TabsNavigation;
        tabsNavigationButton.Click += tabsNavigationButton_Click;
        // 
        // sidebarNavigationButton
        // 
        sidebarNavigationButton.Location = new Point(0, 0);
        sidebarNavigationButton.Name = "sidebarNavigationButton";
        sidebarNavigationButton.Size = new Size(75, 23);
        sidebarNavigationButton.TabIndex = 0;
        sidebarNavigationButton.Text = UiMessages.SidebarNavigation;
        sidebarNavigationButton.Click += sidebarNavigationButton_Click;
        // 
        // workspaceContentPanel
        // 
        workspaceContentPanel.BackColor = SystemColors.Control;
        workspaceContentPanel.Controls.Add(documentTabsPanel);
        workspaceContentPanel.Controls.Add(documentSidebarPanel);
        workspaceContentPanel.Controls.Add(aiPanel);
        workspaceContentPanel.Dock = DockStyle.Fill;
        workspaceContentPanel.Location = new Point(0, 133);
        workspaceContentPanel.Margin = new Padding(4, 4, 4, 4);
        workspaceContentPanel.Name = "workspaceContentPanel";
        workspaceContentPanel.Padding = new Padding(36, 31, 36, 31);
        workspaceContentPanel.Size = new Size(1950, 1124);
        workspaceContentPanel.TabIndex = 2;
        // 
        // documentTabsPanel
        // 
        documentTabsPanel.BackColor = SystemColors.Window;
        documentTabsPanel.Controls.Add(documentTabControl);
        documentTabsPanel.Controls.Add(tabsEmptyStateLabel);
        documentTabsPanel.Dock = DockStyle.Fill;
        documentTabsPanel.Location = new Point(36, 31);
        documentTabsPanel.Margin = new Padding(4, 4, 4, 4);
        documentTabsPanel.Name = "documentTabsPanel";
        documentTabsPanel.Size = new Size(1437, 1062);
        documentTabsPanel.TabIndex = 0;
        // 
        // documentTabControl
        // 
        documentTabControl.Dock = DockStyle.Fill;
        documentTabControl.ItemSize = new Size(140, 36);
        documentTabControl.Location = new Point(0, 0);
        documentTabControl.Margin = new Padding(4, 4, 4, 4);
        documentTabControl.Name = "documentTabControl";
        documentTabControl.SelectedIndex = 0;
        documentTabControl.Size = new Size(1437, 1062);
        documentTabControl.TabIndex = 0;
        documentTabControl.SelectedIndexChanged += documentTabControl_SelectedIndexChanged;
        // 
        // tabsEmptyStateLabel
        // 
        tabsEmptyStateLabel.BackColor = SystemColors.Window;
        tabsEmptyStateLabel.Dock = DockStyle.Fill;
        tabsEmptyStateLabel.Font = new Font("Segoe UI", 11F);
        tabsEmptyStateLabel.Location = new Point(0, 0);
        tabsEmptyStateLabel.Margin = new Padding(4, 0, 4, 0);
        tabsEmptyStateLabel.Name = "tabsEmptyStateLabel";
        tabsEmptyStateLabel.Size = new Size(1437, 1062);
        tabsEmptyStateLabel.TabIndex = 1;
        tabsEmptyStateLabel.Text = UiMessages.NoDocumentsOpen;
        tabsEmptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        tabsEmptyStateLabel.Visible = false;
        // 
        // documentSidebarPanel
        // 
        documentSidebarPanel.BackColor = SystemColors.Window;
        documentSidebarPanel.Controls.Add(documentSidebarContentPanel);
        documentSidebarPanel.Controls.Add(documentListBox);
        documentSidebarPanel.Dock = DockStyle.Fill;
        documentSidebarPanel.Location = new Point(36, 31);
        documentSidebarPanel.Margin = new Padding(4, 4, 4, 4);
        documentSidebarPanel.Name = "documentSidebarPanel";
        documentSidebarPanel.Size = new Size(1437, 1062);
        documentSidebarPanel.TabIndex = 1;
        documentSidebarPanel.Visible = false;
        // 
        // documentSidebarContentPanel
        // 
        documentSidebarContentPanel.BackColor = SystemColors.Window;
        documentSidebarContentPanel.Controls.Add(sidebarEmptyStateLabel);
        documentSidebarContentPanel.Dock = DockStyle.Fill;
        documentSidebarContentPanel.Location = new Point(390, 0);
        documentSidebarContentPanel.Margin = new Padding(4, 4, 4, 4);
        documentSidebarContentPanel.Name = "documentSidebarContentPanel";
        documentSidebarContentPanel.Padding = new Padding(31, 31, 31, 31);
        documentSidebarContentPanel.Size = new Size(1047, 1062);
        documentSidebarContentPanel.TabIndex = 1;
        // 
        // sidebarEmptyStateLabel
        // 
        sidebarEmptyStateLabel.BackColor = SystemColors.Window;
        sidebarEmptyStateLabel.Dock = DockStyle.Fill;
        sidebarEmptyStateLabel.Font = new Font("Segoe UI", 11F);
        sidebarEmptyStateLabel.Location = new Point(31, 31);
        sidebarEmptyStateLabel.Margin = new Padding(4, 0, 4, 0);
        sidebarEmptyStateLabel.Name = "sidebarEmptyStateLabel";
        sidebarEmptyStateLabel.Size = new Size(985, 1000);
        sidebarEmptyStateLabel.TabIndex = 0;
        sidebarEmptyStateLabel.Text = UiMessages.NoDocumentsInWorkspace;
        sidebarEmptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        sidebarEmptyStateLabel.Visible = false;
        // 
        // documentListBox
        // 
        documentListBox.BorderStyle = BorderStyle.None;
        documentListBox.Dock = DockStyle.Left;
        documentListBox.Font = new Font("Segoe UI", 10F);
        documentListBox.FormattingEnabled = true;
        documentListBox.IntegralHeight = false;
        documentListBox.Location = new Point(0, 0);
        documentListBox.Margin = new Padding(4, 4, 4, 4);
        documentListBox.Name = "documentListBox";
        documentListBox.Size = new Size(390, 1062);
        documentListBox.TabIndex = 0;
        documentListBox.SelectedIndexChanged += documentListBox_SelectedIndexChanged;
        // 
        // aiPanel
        // 
        aiPanel.BackColor = SystemColors.Window;
        aiPanel.BorderStyle = BorderStyle.FixedSingle;
        aiPanel.Controls.Add(aiConversationTextBox);
        aiPanel.Controls.Add(aiInputPanel);
        aiPanel.Controls.Add(aiTitleLabel);
        aiPanel.Dock = DockStyle.Right;
        aiPanel.Location = new Point(1473, 31);
        aiPanel.Margin = new Padding(4, 4, 4, 4);
        aiPanel.Name = "aiPanel";
        aiPanel.Padding = new Padding(21, 20, 21, 20);
        aiPanel.Size = new Size(441, 1062);
        aiPanel.TabIndex = 2;
        aiPanel.Visible = false; // For MVP 2 this is valid.
        // 
        // aiConversationTextBox
        // 
        aiConversationTextBox.BackColor = SystemColors.Window;
        aiConversationTextBox.BorderStyle = BorderStyle.None;
        aiConversationTextBox.Dock = DockStyle.Fill;
        aiConversationTextBox.Font = new Font("Segoe UI", 9.5F);
        aiConversationTextBox.Location = new Point(21, 66);
        aiConversationTextBox.Margin = new Padding(4, 4, 4, 4);
        aiConversationTextBox.Name = "aiConversationTextBox";
        aiConversationTextBox.ReadOnly = true;
        aiConversationTextBox.Size = new Size(397, 887);
        aiConversationTextBox.TabIndex = 1;
        aiConversationTextBox.Text = UiMessages.AiAssistanceFutureVersion;
        // 
        // aiInputPanel
        // 
        aiInputPanel.Controls.Add(aiInputTextBox);
        aiInputPanel.Controls.Add(aiSendButton);
        aiInputPanel.Dock = DockStyle.Bottom;
        aiInputPanel.Location = new Point(21, 953);
        aiInputPanel.Margin = new Padding(4, 4, 4, 4);
        aiInputPanel.Name = "aiInputPanel";
        aiInputPanel.Size = new Size(397, 87);
        aiInputPanel.TabIndex = 2;
        // 
        // aiInputTextBox
        // 
        aiInputTextBox.Dock = DockStyle.Fill;
        aiInputTextBox.Font = new Font("Segoe UI", 9F);
        aiInputTextBox.Location = new Point(0, 0);
        aiInputTextBox.Margin = new Padding(4, 4, 4, 4);
        aiInputTextBox.Multiline = true;
        aiInputTextBox.Name = "aiInputTextBox";
        aiInputTextBox.PlaceholderText = UiMessages.AskAboutDocuments;
        aiInputTextBox.Size = new Size(303, 87);
        aiInputTextBox.TabIndex = 0;
        // 
        // aiSendButton
        // 
        aiSendButton.Dock = DockStyle.Right;
        aiSendButton.Enabled = false;
        aiSendButton.Font = new Font("Segoe UI Semibold", 9F);
        aiSendButton.Location = new Point(303, 0);
        aiSendButton.Margin = new Padding(4, 4, 4, 4);
        aiSendButton.Name = "aiSendButton";
        aiSendButton.Size = new Size(94, 87);
        aiSendButton.TabIndex = 1;
        aiSendButton.Text = UiMessages.SendButton;
        aiSendButton.UseVisualStyleBackColor = true;
        // 
        // aiTitleLabel
        // 
        aiTitleLabel.Dock = DockStyle.Top;
        aiTitleLabel.Font = new Font("Segoe UI Semibold", 11F);
        aiTitleLabel.Location = new Point(21, 20);
        aiTitleLabel.Margin = new Padding(0);
        aiTitleLabel.Name = "aiTitleLabel";
        aiTitleLabel.Size = new Size(397, 46);
        aiTitleLabel.TabIndex = 0;
        aiTitleLabel.Text = UiMessages.AiAssistant;
        aiTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // workspaceContextMenu
        // 
        workspaceContextMenu.ImageScalingSize = new Size(32, 32);
        workspaceContextMenu.Items.AddRange(new ToolStripItem[] { addDocumentsMenuItem, removeDocumentsMenuItem, updateWorkspaceDetailsMenuItem, deleteWorkspaceMenuItem, workspaceMenuSeparator, viewMenuItem, viewMenuSeparator, closeWorkspaceMenuItem });
        workspaceContextMenu.Name = "workspaceContextMenu";
        workspaceContextMenu.Size = new Size(484, 244);
        // 
        // addDocumentsMenuItem
        // 
        addDocumentsMenuItem.Name = "addDocumentsMenuItem";
        addDocumentsMenuItem.Size = new Size(483, 38);
        addDocumentsMenuItem.Text = UiMessages.AddDocumentsToWorkspace;
        // 
        // removeDocumentsMenuItem
        // 
        removeDocumentsMenuItem.Name = "removeDocumentsMenuItem";
        removeDocumentsMenuItem.Size = new Size(483, 38);
        removeDocumentsMenuItem.Text = UiMessages.RemoveDocumentsFromWorkspace;
        // 
        // updateWorkspaceDetailsMenuItem
        // 
        updateWorkspaceDetailsMenuItem.Name = "updateWorkspaceDetailsMenuItem";
        updateWorkspaceDetailsMenuItem.Size = new Size(483, 38);
        updateWorkspaceDetailsMenuItem.Text = UiMessages.UpdateWorkspaceDetailsTitle;
        // 
        // deleteWorkspaceMenuItem
        // 
        deleteWorkspaceMenuItem.Name = "deleteWorkspaceMenuItem";
        deleteWorkspaceMenuItem.Size = new Size(483, 38);
        deleteWorkspaceMenuItem.Text = UiMessages.DeleteWorkspace;
        // 
        // workspaceMenuSeparator
        // 
        workspaceMenuSeparator.Name = "workspaceMenuSeparator";
        workspaceMenuSeparator.Size = new Size(480, 6);
        // 
        // viewMenuItem
        // 
        viewMenuItem.DropDownItems.AddRange(new ToolStripItem[] { tabsMenuItem, sidebarMenuItem });
        viewMenuItem.Name = "viewMenuItem";
        viewMenuItem.Size = new Size(483, 38);
        viewMenuItem.Text = UiMessages.View;
        // 
        // tabsMenuItem
        // 
        tabsMenuItem.Name = "tabsMenuItem";
        tabsMenuItem.Size = new Size(227, 44);
        tabsMenuItem.Text = UiMessages.TabsNavigation;
        // 
        // sidebarMenuItem
        // 
        sidebarMenuItem.Name = "sidebarMenuItem";
        sidebarMenuItem.Size = new Size(227, 44);
        sidebarMenuItem.Text = UiMessages.SidebarNavigation;
        // 
        // viewMenuSeparator
        // 
        viewMenuSeparator.Name = "viewMenuSeparator";
        viewMenuSeparator.Size = new Size(480, 6);
        // 
        // closeWorkspaceMenuItem
        // 
        closeWorkspaceMenuItem.Name = "closeWorkspaceMenuItem";
        closeWorkspaceMenuItem.Size = new Size(483, 38);
        closeWorkspaceMenuItem.Text = UiMessages.CloseWorkspace;
        // 
        // WorkspaceViewForm
        // 
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Control;
        ClientSize = new Size(1950, 1257);
        Controls.Add(workspaceContentPanel);
        Controls.Add(workspaceHeaderPanel);
        Margin = new Padding(4, 4, 4, 4);
        MinimumSize = new Size(1422, 876);
        Name = "WorkspaceViewForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = UiMessages.DeskVaultWorkspaceTitle;
        workspaceHeaderPanel.ResumeLayout(false);
        workspaceHeaderLayout.ResumeLayout(false);
        workspaceHeaderLayout.PerformLayout();
        workspaceHeaderInfoLayout.ResumeLayout(false);
        workspaceActionPanel.ResumeLayout(false);
        workspaceContentPanel.ResumeLayout(false);
        documentTabsPanel.ResumeLayout(false);
        documentSidebarPanel.ResumeLayout(false);
        documentSidebarContentPanel.ResumeLayout(false);
        aiPanel.ResumeLayout(false);
        aiInputPanel.ResumeLayout(false);
        aiInputPanel.PerformLayout();
        workspaceContextMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion
}

using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

partial class DocumentViewForm
{
    private System.ComponentModel.IContainer components = null!;

    private Panel workspaceHeaderPanel = null!;
    private TableLayoutPanel workspaceHeaderLayout = null!;
    private Button backButton = null!;
    private TableLayoutPanel documentHeaderInfoLayout = null!;
    private Label documentTitleLabel = null!;
    private Label documentMetadataLabel = null!;
    private FlowLayoutPanel workspaceActionPanel = null!;
    private Button aiButton = null!;
    private Button workspaceMenuButton = null!;
    private Button closeButton = null!;
    private Panel documentContentPanel = null!;
    private Panel unsupportedPreviewPanel = null!;
    private TableLayoutPanel unsupportedPreviewLayout = null!;
    private Label previewUnavailableLabel = null!;
    private Label unsupportedDocumentPreviewMessageLabel = null!;
    private Button openExternallyButton = null!;
    private ContextMenuStrip workspaceContextMenu = null!;
    private ToolStripMenuItem addRelatedDocumentsMenuItem = null!;
    private ToolStripMenuItem documentInformationMenuItem = null!;
    private ToolStripMenuItem saveWorkspaceMenuItem = null!;
    private ToolStripMenuItem removeDocumentMenuItem = null!;
    private ToolStripMenuItem closeWorkspaceMenuItem = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
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
        documentHeaderInfoLayout = new TableLayoutPanel();
        documentTitleLabel = new Label();
        documentMetadataLabel = new Label();
        workspaceActionPanel = new FlowLayoutPanel();
        aiButton = new Button();
        workspaceMenuButton = new Button();
        closeButton = new Button();

        documentContentPanel = new Panel();

        unsupportedPreviewPanel = new Panel();
        unsupportedPreviewLayout = new TableLayoutPanel();
        previewUnavailableLabel = new Label();
        unsupportedDocumentPreviewMessageLabel = new Label();
        openExternallyButton = new Button();

        workspaceContextMenu = new ContextMenuStrip(components);
        addRelatedDocumentsMenuItem = new ToolStripMenuItem();
        documentInformationMenuItem = new ToolStripMenuItem();
        saveWorkspaceMenuItem = new ToolStripMenuItem();
        removeDocumentMenuItem = new ToolStripMenuItem();
        closeWorkspaceMenuItem = new ToolStripMenuItem();

        workspaceHeaderPanel.SuspendLayout();
        workspaceHeaderLayout.SuspendLayout();
        documentHeaderInfoLayout.SuspendLayout();
        workspaceActionPanel.SuspendLayout();
        unsupportedPreviewPanel.SuspendLayout();
        unsupportedPreviewLayout.SuspendLayout();
        workspaceContextMenu.SuspendLayout();
        SuspendLayout();

        //
        // workspaceHeaderPanel
        //
        workspaceHeaderPanel.BackColor = SystemColors.Window;
        workspaceHeaderPanel.Controls.Add(workspaceHeaderLayout);
        workspaceHeaderPanel.Dock = DockStyle.Top;
        workspaceHeaderPanel.Location = new Point(0, 0);
        workspaceHeaderPanel.Name = "workspaceHeaderPanel";
        workspaceHeaderPanel.Padding = new Padding(28, 18, 28, 18);
        workspaceHeaderPanel.Size = new Size(1500, 104);
        workspaceHeaderPanel.TabIndex = 0;

        //
        // workspaceHeaderLayout
        //
        workspaceHeaderLayout.ColumnCount = 3;

        workspaceHeaderLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        workspaceHeaderLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        workspaceHeaderLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        workspaceHeaderLayout.Controls.Add(
            backButton,
            0,
            0);

        workspaceHeaderLayout.Controls.Add(
            documentHeaderInfoLayout,
            1,
            0);

        workspaceHeaderLayout.Controls.Add(
            workspaceActionPanel,
            2,
            0);

        workspaceHeaderLayout.Dock = DockStyle.Fill;
        workspaceHeaderLayout.Location = new Point(28, 18);
        workspaceHeaderLayout.Margin = new Padding(0);
        workspaceHeaderLayout.Name = "workspaceHeaderLayout";
        workspaceHeaderLayout.RowCount = 1;

        workspaceHeaderLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        workspaceHeaderLayout.Size = new Size(1444, 68);
        workspaceHeaderLayout.TabIndex = 0;

        //
        // backButton
        //
        backButton.Anchor = AnchorStyles.Left;
        backButton.BackColor = SystemColors.Control;
        backButton.Cursor = Cursors.Hand;
        backButton.FlatAppearance.BorderSize = 0;
        backButton.FlatAppearance.MouseDownBackColor =
            SystemColors.ControlDark;
        backButton.FlatAppearance.MouseOverBackColor =
            SystemColors.ControlLight;
        backButton.FlatStyle = FlatStyle.Flat;
        backButton.Font = new Font(
            "Segoe UI Semibold",
            10F);
        backButton.Margin =
            new Padding(0, 12, 20, 12);
        backButton.Name = "backButton";
        backButton.Size =
            new Size(160, 44);
        backButton.TabIndex = 0;
        backButton.Text =
            UiMessages.BackToDocuments;
        backButton.UseVisualStyleBackColor =
            false;
        backButton.Click +=
            backButton_Click;

        //
        // documentHeaderInfoLayout
        //
        documentHeaderInfoLayout.ColumnCount = 1;

        documentHeaderInfoLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        documentHeaderInfoLayout.Controls.Add(
            documentTitleLabel,
            0,
            0);

        documentHeaderInfoLayout.Controls.Add(
            documentMetadataLabel,
            0,
            1);

        documentHeaderInfoLayout.Dock =
            DockStyle.Fill;

        documentHeaderInfoLayout.Location =
            new Point(208, 0);

        documentHeaderInfoLayout.Margin =
            new Padding(0);

        documentHeaderInfoLayout.Name =
            "documentHeaderInfoLayout";

        documentHeaderInfoLayout.RowCount = 2;

        //
        // CHANGED:
        // Give the title row more vertical space so
        // the Segoe UI 'g' descender is not clipped
        // at high DPI scaling.
        //
        documentHeaderInfoLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                60F));

        documentHeaderInfoLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                40F));

        documentHeaderInfoLayout.Size =
            new Size(652, 68);

        documentHeaderInfoLayout.TabIndex = 1;

        //
        // documentTitleLabel
        //
        documentTitleLabel.AutoEllipsis = true;
        documentTitleLabel.AutoSize = false;
        documentTitleLabel.Dock = DockStyle.Fill;
        documentTitleLabel.Font = new Font(
            "Segoe UI Semibold",
            14F);
        documentTitleLabel.Margin =
            new Padding(0, 0, 16, 0);
        documentTitleLabel.Name =
            "documentTitleLabel";
        documentTitleLabel.TabIndex = 0;
        documentTitleLabel.Text =
            UiMessages.DocumentColumnHeader;
        documentTitleLabel.TextAlign =
            ContentAlignment.MiddleLeft;

        //
        // documentMetadataLabel
        //
        documentMetadataLabel.AutoEllipsis = true;
        documentMetadataLabel.AutoSize = false;
        documentMetadataLabel.Dock = DockStyle.Fill;
        documentMetadataLabel.Font = new Font(
            "Segoe UI",
            9F);
        documentMetadataLabel.ForeColor =
            SystemColors.GrayText;
        documentMetadataLabel.Margin =
            new Padding(0, 0, 16, 0);
        documentMetadataLabel.Name =
            "documentMetadataLabel";
        documentMetadataLabel.TabIndex = 1;
        documentMetadataLabel.Text =
            UiMessages.DocumentWorkspaceMetadata;
        documentMetadataLabel.TextAlign =
            ContentAlignment.MiddleLeft;

        //
        // workspaceActionPanel
        //
        workspaceActionPanel.Anchor =
            AnchorStyles.Top | AnchorStyles.Right;

        workspaceActionPanel.AutoSize = true;
        workspaceActionPanel.AutoSizeMode =
            AutoSizeMode.GrowAndShrink;

        workspaceActionPanel.Controls.Add(aiButton);
        workspaceActionPanel.Controls.Add(workspaceMenuButton);
        workspaceActionPanel.Controls.Add(closeButton);

        workspaceActionPanel.FlowDirection =
            FlowDirection.LeftToRight;

        workspaceActionPanel.Location =
            new Point(1088, 0);

        workspaceActionPanel.Margin =
            new Padding(0);

        workspaceActionPanel.Name =
            "workspaceActionPanel";

        workspaceActionPanel.Padding =
            new Padding(0);

        workspaceActionPanel.Size =
            new Size(356, 68);

        workspaceActionPanel.TabIndex = 2;
        workspaceActionPanel.WrapContents = false;

        //
        // aiButton
        //
        aiButton.Anchor =
            AnchorStyles.Top;

        aiButton.BackColor =
            SystemColors.Control;

        aiButton.Cursor =
            Cursors.Hand;

        aiButton.FlatAppearance.BorderSize = 0;

        aiButton.FlatAppearance.MouseDownBackColor =
            SystemColors.ControlDark;

        aiButton.FlatAppearance.MouseOverBackColor =
            SystemColors.ControlLight;

        aiButton.FlatStyle =
            FlatStyle.Flat;

        aiButton.Font =
            new Font(
                "Segoe UI Semibold",
                10F);

        aiButton.Margin =
            new Padding(0, 12, 4, 12);

        aiButton.Name =
            "aiButton";

        aiButton.Size =
            new Size(100, 44);

        aiButton.TabIndex = 0;

        aiButton.Text =
            UiMessages.AiButton;

        aiButton.UseVisualStyleBackColor =
            false;

        aiButton.Visible = false;

        //
        // workspaceMenuButton
        //
        workspaceMenuButton.Anchor =
            AnchorStyles.Top;

        workspaceMenuButton.BackColor =
            SystemColors.Control;

        workspaceMenuButton.Cursor =
            Cursors.Hand;

        workspaceMenuButton.FlatAppearance.BorderSize = 0;

        workspaceMenuButton.FlatAppearance.MouseDownBackColor =
            SystemColors.ControlDark;

        workspaceMenuButton.FlatAppearance.MouseOverBackColor =
            SystemColors.ControlLight;

        workspaceMenuButton.FlatStyle =
            FlatStyle.Flat;

        workspaceMenuButton.Font =
            new Font(
                "Segoe UI Semibold",
                10F);

        workspaceMenuButton.Margin =
            new Padding(4, 12, 4, 12);

        workspaceMenuButton.Name =
            "workspaceMenuButton";

        workspaceMenuButton.Size =
            new Size(100, 44);

        workspaceMenuButton.TabIndex = 1;

        workspaceMenuButton.Text =
            UiMessages.WorkspaceMenuButton;

        workspaceMenuButton.UseVisualStyleBackColor =
            false;

        workspaceMenuButton.Click +=
            workspaceMenuButton_Click;

        //
        // closeButton
        //
        closeButton.Anchor =
            AnchorStyles.Top;

        closeButton.BackColor =
            SystemColors.Control;

        closeButton.Cursor =
            Cursors.Hand;

        closeButton.FlatAppearance.BorderSize = 0;

        closeButton.FlatAppearance.MouseDownBackColor =
            SystemColors.ControlDark;

        closeButton.FlatAppearance.MouseOverBackColor =
            SystemColors.ControlLight;

        closeButton.FlatStyle =
            FlatStyle.Flat;

        closeButton.Font =
            new Font(
                "Segoe UI Semibold",
                10F);

        closeButton.Margin =
            new Padding(4, 12, 0, 12);

        closeButton.Name =
            "closeButton";

        closeButton.Size =
            new Size(144, 44);

        closeButton.TabIndex = 2;

        closeButton.Text =
            UiMessages.CloseButton;

        closeButton.UseVisualStyleBackColor =
            false;

        closeButton.Click +=
            closeButton_Click;

        //
        // documentContentPanel
        //
        documentContentPanel.BackColor =
            SystemColors.Window;

        documentContentPanel.Dock =
            DockStyle.Fill;

        documentContentPanel.Location =
            new Point(0, 104);

        documentContentPanel.Name =
            "documentContentPanel";

        documentContentPanel.Size =
            new Size(1500, 878);

        documentContentPanel.TabIndex = 6;

        //
        // unsupportedPreviewPanel
        //
        unsupportedPreviewPanel.BackColor =
            SystemColors.Control;

        unsupportedPreviewPanel.Controls.Add(
            unsupportedPreviewLayout);

        unsupportedPreviewPanel.Dock =
            DockStyle.Fill;

        unsupportedPreviewPanel.Location =
            new Point(0, 104);

        unsupportedPreviewPanel.Name =
            "unsupportedPreviewPanel";

        unsupportedPreviewPanel.Padding =
            new Padding(28, 24, 28, 24);

        unsupportedPreviewPanel.Size =
            new Size(1500, 878);

        unsupportedPreviewPanel.TabIndex = 7;

        unsupportedPreviewPanel.Visible =
            false;

        //
        // unsupportedPreviewLayout
        //
        unsupportedPreviewLayout.ColumnCount = 3;

        unsupportedPreviewLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                50F));

        unsupportedPreviewLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        unsupportedPreviewLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                50F));

        unsupportedPreviewLayout.Controls.Add(
            previewUnavailableLabel,
            1,
            1);

        unsupportedPreviewLayout.Controls.Add(
            unsupportedDocumentPreviewMessageLabel,
            1,
            2);

        unsupportedPreviewLayout.Controls.Add(
            openExternallyButton,
            1,
            3);

        unsupportedPreviewLayout.Dock =
            DockStyle.Fill;

        unsupportedPreviewLayout.Location =
            new Point(28, 24);

        unsupportedPreviewLayout.Margin =
            new Padding(0);

        unsupportedPreviewLayout.Name =
            "unsupportedPreviewLayout";

        unsupportedPreviewLayout.RowCount = 5;

        unsupportedPreviewLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                50F));

        unsupportedPreviewLayout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        unsupportedPreviewLayout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        unsupportedPreviewLayout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        unsupportedPreviewLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                50F));

        unsupportedPreviewLayout.Size =
            new Size(1444, 830);

        unsupportedPreviewLayout.TabIndex = 0;

        //
        // previewUnavailableLabel
        //
        previewUnavailableLabel.Anchor =
            AnchorStyles.None;

        previewUnavailableLabel.AutoSize = true;

        previewUnavailableLabel.Font =
            new Font(
                "Segoe UI Semibold",
                16F);

        previewUnavailableLabel.Margin =
            new Padding(
                6,
                0,
                6,
                12);

        previewUnavailableLabel.Name =
            "previewUnavailableLabel";

        previewUnavailableLabel.Size =
            new Size(280, 45);

        previewUnavailableLabel.TabIndex = 0;

        previewUnavailableLabel.Text =
            UiMessages.PreviewUnavailableTitle;

        //
        // unsupportedDocumentPreviewMessageLabel
        //
        unsupportedDocumentPreviewMessageLabel.Anchor =
            AnchorStyles.None;

        unsupportedDocumentPreviewMessageLabel.AutoSize =
            true;

        unsupportedDocumentPreviewMessageLabel.Font =
            new Font(
                "Segoe UI",
                10F);

        unsupportedDocumentPreviewMessageLabel.ForeColor =
            SystemColors.GrayText;

        unsupportedDocumentPreviewMessageLabel.Margin =
            new Padding(
                6,
                0,
                6,
                16);

        unsupportedDocumentPreviewMessageLabel.Name =
            "unsupportedDocumentPreviewMessageLabel";

        unsupportedDocumentPreviewMessageLabel.Size =
            new Size(500, 32);

        unsupportedDocumentPreviewMessageLabel.TabIndex = 1;

        unsupportedDocumentPreviewMessageLabel.Text =
            UiMessages.UnsupportedDocumentPreviewMessage;

        //
        // openExternallyButton
        //
        openExternallyButton.Anchor =
            AnchorStyles.None;

        openExternallyButton.BackColor =
            SystemColors.Control;

        openExternallyButton.Cursor =
            Cursors.Hand;

        openExternallyButton.FlatAppearance.BorderSize = 0;

        openExternallyButton.FlatAppearance.MouseDownBackColor =
            SystemColors.ControlDark;

        openExternallyButton.FlatAppearance.MouseOverBackColor =
            SystemColors.ControlLight;

        openExternallyButton.FlatStyle =
            FlatStyle.Flat;

        openExternallyButton.Font =
            new Font(
                "Segoe UI Semibold",
                10F);

        openExternallyButton.Margin =
            new Padding(6);

        openExternallyButton.Name =
            "openExternallyButton";

        openExternallyButton.Size =
            new Size(160, 44);

        openExternallyButton.TabIndex = 2;

        openExternallyButton.Text =
            UiMessages.OpenExternallyButton;

        openExternallyButton.UseVisualStyleBackColor =
            false;

        openExternallyButton.Click +=
            openExternallyButton_Click;

        //
        // workspaceContextMenu
        //
        workspaceContextMenu.ImageScalingSize =
            new Size(20, 20);

        workspaceContextMenu.Items.AddRange(
            new ToolStripItem[]
            {
                addRelatedDocumentsMenuItem,
                documentInformationMenuItem,
                saveWorkspaceMenuItem,
                removeDocumentMenuItem,
                closeWorkspaceMenuItem
            });

        workspaceContextMenu.Name =
            "workspaceContextMenu";

        workspaceContextMenu.Size =
            new Size(260, 164);

        //
        // addRelatedDocumentsMenuItem
        //
        addRelatedDocumentsMenuItem.Name =
            "addRelatedDocumentsMenuItem";

        addRelatedDocumentsMenuItem.Size =
            new Size(259, 32);

        addRelatedDocumentsMenuItem.Text =
            UiMessages.AddRelatedDocuments;

        addRelatedDocumentsMenuItem.Visible = false;

        //
        // documentInformationMenuItem
        //
        documentInformationMenuItem.Name =
            "documentInformationMenuItem";

        documentInformationMenuItem.Size =
            new Size(259, 32);

        documentInformationMenuItem.Text =
            UiMessages.DocumentInformation;

        documentInformationMenuItem.Click +=
            documentInformationMenuItem_Click;

        //
        // saveWorkspaceMenuItem
        //
        saveWorkspaceMenuItem.Name =
            "saveWorkspaceMenuItem";

        saveWorkspaceMenuItem.Size =
            new Size(259, 32);

        saveWorkspaceMenuItem.Text =
            UiMessages.SaveAsWorkspace;

        saveWorkspaceMenuItem.Visible = false;

        //
        // removeDocumentMenuItem
        //
        removeDocumentMenuItem.Name =
            "removeDocumentMenuItem";

        removeDocumentMenuItem.Size =
            new Size(259, 32);

        removeDocumentMenuItem.Text =
            UiMessages.RemoveDocument;

        removeDocumentMenuItem.Click +=
            removeDocumentMenuItem_Click;

        //
        // closeWorkspaceMenuItem
        //
        closeWorkspaceMenuItem.Name =
            "closeWorkspaceMenuItem";

        closeWorkspaceMenuItem.Size =
            new Size(259, 32);

        closeWorkspaceMenuItem.Text =
            UiMessages.CloseWorkspace;

        closeWorkspaceMenuItem.Click +=
            closeWorkspaceMenuItem_Click;

        //
        // DocumentViewForm
        //
        AutoScaleDimensions =
            new SizeF(10F, 25F);

        AutoScaleMode =
            AutoScaleMode.Font;

        BackColor =
            SystemColors.Control;

        ClientSize =
            new Size(1500, 982);

        Controls.Add(
            unsupportedPreviewPanel);

        Controls.Add(
            documentContentPanel);

        Controls.Add(
            workspaceHeaderPanel);

        MinimumSize =
            new Size(1100, 700);

        Name =
            "DocumentViewForm";

        StartPosition =
            FormStartPosition.CenterParent;

        Text =
            UiMessages.DocumentWorkspaceTitle;

        workspaceHeaderPanel.ResumeLayout(false);

        workspaceHeaderLayout.ResumeLayout(false);

        documentHeaderInfoLayout.ResumeLayout(false);

        workspaceActionPanel.ResumeLayout(false);

        unsupportedPreviewLayout.ResumeLayout(false);
        unsupportedPreviewLayout.PerformLayout();

        unsupportedPreviewPanel.ResumeLayout(false);

        workspaceContextMenu.ResumeLayout(false);

        ResumeLayout(false);
    }

    #endregion
}

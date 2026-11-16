using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

partial class DocumentViewForm
{
    private System.ComponentModel.IContainer components = null!;

    private Panel workspaceHeaderPanel = null!;
    private TableLayoutPanel workspaceHeaderLayout = null!;
    private Label documentTitleLabel = null!;
    private Button documentActionsButton = null!;

    private Panel documentContentPanel = null!;
    private Panel unsupportedPreviewPanel = null!;
    private TableLayoutPanel unsupportedPreviewLayout = null!;
    private Label previewUnavailableLabel = null!;
    private Label unsupportedDocumentPreviewMessageLabel = null!;
    private Button openExternallyButton = null!;

    private ContextMenuStrip documentContextMenu = null!;
    private ToolStripMenuItem documentInformationMenuItem = null!;
    private ToolStripMenuItem removeDocumentMenuItem = null!;

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
        documentTitleLabel = new Label();
        documentActionsButton = new Button();

        documentContentPanel = new Panel();

        unsupportedPreviewPanel = new Panel();
        unsupportedPreviewLayout = new TableLayoutPanel();
        previewUnavailableLabel = new Label();
        unsupportedDocumentPreviewMessageLabel = new Label();
        openExternallyButton = new Button();

        documentContextMenu = new ContextMenuStrip(components);
        documentInformationMenuItem = new ToolStripMenuItem();
        removeDocumentMenuItem = new ToolStripMenuItem();

        workspaceHeaderPanel.SuspendLayout();
        workspaceHeaderLayout.SuspendLayout();
        unsupportedPreviewPanel.SuspendLayout();
        unsupportedPreviewLayout.SuspendLayout();
        documentContextMenu.SuspendLayout();
        SuspendLayout();

        //
        // workspaceHeaderPanel
        //
        workspaceHeaderPanel.BackColor =
            SystemColors.Window;

        workspaceHeaderPanel.Controls.Add(
            workspaceHeaderLayout);

        workspaceHeaderPanel.Dock =
            DockStyle.Top;

        workspaceHeaderPanel.Location =
            new Point(0, 0);

        workspaceHeaderPanel.Name =
            "workspaceHeaderPanel";

        workspaceHeaderPanel.Padding =
            new Padding(28, 18, 28, 18);

        workspaceHeaderPanel.Size =
            new Size(1500, 104);

        workspaceHeaderPanel.TabIndex = 0;

        //
        // workspaceHeaderLayout
        //
        workspaceHeaderLayout.ColumnCount = 2;

        workspaceHeaderLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        workspaceHeaderLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        workspaceHeaderLayout.Controls.Add(
            documentTitleLabel,
            0,
            0);

        workspaceHeaderLayout.Controls.Add(
            documentActionsButton,
            1,
            0);

        workspaceHeaderLayout.Dock =
            DockStyle.Fill;

        workspaceHeaderLayout.Location =
            new Point(28, 18);

        workspaceHeaderLayout.Margin =
            new Padding(0);

        workspaceHeaderLayout.Name =
            "workspaceHeaderLayout";

        workspaceHeaderLayout.RowCount = 1;

        workspaceHeaderLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        workspaceHeaderLayout.Size =
            new Size(1444, 68);

        workspaceHeaderLayout.TabIndex = 0;

        //
        // documentTitleLabel
        //
        documentTitleLabel.AutoEllipsis = true;
        documentTitleLabel.AutoSize = false;

        documentTitleLabel.Dock =
            DockStyle.Fill;

        documentTitleLabel.Font =
            new Font(
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
        // documentActionsButton
        //
        documentActionsButton.Anchor =
            AnchorStyles.Top | AnchorStyles.Right;

        documentActionsButton.BackColor =
            SystemColors.Control;

        documentActionsButton.Cursor =
            Cursors.Hand;

        documentActionsButton.FlatAppearance.BorderSize = 0;

        documentActionsButton.FlatAppearance.MouseDownBackColor =
            SystemColors.ControlDark;

        documentActionsButton.FlatAppearance.MouseOverBackColor =
            SystemColors.ControlLight;

        documentActionsButton.FlatStyle =
            FlatStyle.Flat;

        documentActionsButton.Font =
            new Font(
                "Segoe UI Semibold",
                10F);

        documentActionsButton.Margin =
            new Padding(4, 12, 0, 12);

        documentActionsButton.Name =
            "documentActionsButton";

        documentActionsButton.Size =
            new Size(100, 44);

        documentActionsButton.TabIndex = 1;

        documentActionsButton.Text =
            UiMessages.DocumentActionsButton;

        documentActionsButton.UseVisualStyleBackColor =
            false;

        documentActionsButton.Click +=
            documentActionsButton_Click;

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
        // documentContextMenu
        //
        documentContextMenu.ImageScalingSize =
            new Size(20, 20);

        documentContextMenu.Items.AddRange(
            new ToolStripItem[]
            {
                documentInformationMenuItem,
                removeDocumentMenuItem
            });

        documentContextMenu.Name =
            "documentContextMenu";

        documentContextMenu.Size =
            new Size(260, 68);

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
            UiMessages.DocumentViewTitle;

        workspaceHeaderPanel.ResumeLayout(false);

        workspaceHeaderLayout.ResumeLayout(false);

        unsupportedPreviewLayout.ResumeLayout(false);
        unsupportedPreviewLayout.PerformLayout();

        unsupportedPreviewPanel.ResumeLayout(false);

        documentContextMenu.ResumeLayout(false);

        ResumeLayout(false);
    }

    #endregion
}

using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

partial class DocumentViewForm
{
    private System.ComponentModel.IContainer components = null!;

    private Panel workspaceHeaderPanel = null!;
    private Button backButton = null!;
    private Label documentTitleLabel = null!;
    private Label documentMetadataLabel = null!;
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
        backButton = new Button();
        documentTitleLabel = new Label();
        documentMetadataLabel = new Label();
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
        unsupportedPreviewPanel.SuspendLayout();
        unsupportedPreviewLayout.SuspendLayout();
        workspaceContextMenu.SuspendLayout();
        SuspendLayout();

        //
        // workspaceHeaderPanel
        //
        workspaceHeaderPanel.Controls.Add(backButton);
        workspaceHeaderPanel.Controls.Add(documentTitleLabel);
        workspaceHeaderPanel.Controls.Add(documentMetadataLabel);
        workspaceHeaderPanel.Controls.Add(aiButton);
        workspaceHeaderPanel.Controls.Add(workspaceMenuButton);
        workspaceHeaderPanel.Controls.Add(closeButton);
        workspaceHeaderPanel.Dock = DockStyle.Top;
        workspaceHeaderPanel.Location = new Point(0, 0);
        workspaceHeaderPanel.Margin = new Padding(6);
        workspaceHeaderPanel.Name = "workspaceHeaderPanel";
        workspaceHeaderPanel.Size = new Size(1671, 154);
        workspaceHeaderPanel.TabIndex = 0;

        //
        // backButton
        //
        backButton.Location = new Point(22, 38);
        backButton.Margin = new Padding(6);
        backButton.Name = "backButton";
        backButton.Size = new Size(167, 68);
        backButton.TabIndex = 0;
        backButton.Text = UiMessages.BackToDocuments;
        backButton.UseVisualStyleBackColor = true;
        backButton.Click += backButton_Click;

        //
        // documentTitleLabel
        //
        documentTitleLabel.AutoSize = true;
        documentTitleLabel.Location = new Point(223, 30);
        documentTitleLabel.Margin = new Padding(6, 0, 6, 0);
        documentTitleLabel.Name = "documentTitleLabel";
        documentTitleLabel.Size = new Size(126, 32);
        documentTitleLabel.TabIndex = 1;
        documentTitleLabel.Text = UiMessages.DocumentColumnHeader;

        //
        // documentMetadataLabel
        //
        documentMetadataLabel.AutoSize = true;
        documentMetadataLabel.Location = new Point(223, 81);
        documentMetadataLabel.Margin = new Padding(6, 0, 6, 0);
        documentMetadataLabel.Name = "documentMetadataLabel";
        documentMetadataLabel.Size = new Size(248, 32);
        documentMetadataLabel.TabIndex = 2;
        documentMetadataLabel.Text = UiMessages.DocumentWorkspaceMetadata;

        //
        // aiButton
        //
        aiButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        aiButton.Location = new Point(1181, 38);
        aiButton.Margin = new Padding(6);
        aiButton.Name = "aiButton";
        aiButton.Size = new Size(111, 68);
        aiButton.TabIndex = 3;
        aiButton.Text = UiMessages.AiButton;
        aiButton.UseVisualStyleBackColor = true;

        //
        // workspaceMenuButton
        //
        workspaceMenuButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        workspaceMenuButton.Location = new Point(1304, 38);
        workspaceMenuButton.Margin = new Padding(6);
        workspaceMenuButton.Name = "workspaceMenuButton";
        workspaceMenuButton.Size = new Size(111, 68);
        workspaceMenuButton.TabIndex = 4;
        workspaceMenuButton.Text = UiMessages.WorkspaceMenuButton;
        workspaceMenuButton.UseVisualStyleBackColor = true;
        workspaceMenuButton.Click += workspaceMenuButton_Click;

        //
        // closeButton
        //
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeButton.Location = new Point(1426, 38);
        closeButton.Margin = new Padding(6);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(223, 68);
        closeButton.TabIndex = 5;
        closeButton.Text = UiMessages.CloseButton;
        closeButton.UseVisualStyleBackColor = true;
        closeButton.Click += closeButton_Click;

        //
        // documentContentPanel
        //
        documentContentPanel.Dock = DockStyle.Fill;
        documentContentPanel.Location = new Point(0, 154);
        documentContentPanel.Margin = new Padding(6);
        documentContentPanel.Name = "documentContentPanel";
        documentContentPanel.Size = new Size(1671, 1126);
        documentContentPanel.TabIndex = 6;

        //
        // unsupportedPreviewPanel
        //
        unsupportedPreviewPanel.Controls.Add(
            unsupportedPreviewLayout);
        unsupportedPreviewPanel.Dock = DockStyle.Fill;
        unsupportedPreviewPanel.Location = new Point(0, 154);
        unsupportedPreviewPanel.Margin = new Padding(6);
        unsupportedPreviewPanel.Name = "unsupportedPreviewPanel";
        unsupportedPreviewPanel.Padding = new Padding(40);
        unsupportedPreviewPanel.Size = new Size(1671, 1126);
        unsupportedPreviewPanel.TabIndex = 7;
        unsupportedPreviewPanel.Visible = false;

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

        unsupportedPreviewLayout.Dock = DockStyle.Fill;
        unsupportedPreviewLayout.Location = new Point(40, 40);
        unsupportedPreviewLayout.Margin = new Padding(0);
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
            new Size(1591, 1046);

        unsupportedPreviewLayout.TabIndex = 0;

        //
        // previewUnavailableLabel
        //
        previewUnavailableLabel.Anchor = AnchorStyles.None;
        previewUnavailableLabel.AutoSize = true;
        previewUnavailableLabel.Font = new Font(
            "Segoe UI",
            16F,
            FontStyle.Bold,
            GraphicsUnit.Point);
        previewUnavailableLabel.Margin =
            new Padding(6, 0, 6, 12);
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
        unsupportedDocumentPreviewMessageLabel.AutoSize = true;
        unsupportedDocumentPreviewMessageLabel.Margin =
            new Padding(6, 0, 6, 16);
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
        openExternallyButton.Anchor = AnchorStyles.None;
        openExternallyButton.AutoSize = true;
        openExternallyButton.Margin = new Padding(6);
        openExternallyButton.Name =
            "openExternallyButton";
        openExternallyButton.Size =
            new Size(190, 48);
        openExternallyButton.TabIndex = 2;
        openExternallyButton.Text =
            UiMessages.OpenExternallyButton;
        openExternallyButton.UseVisualStyleBackColor = true;
        openExternallyButton.Click +=
            openExternallyButton_Click;

        //
        // workspaceContextMenu
        //
        workspaceContextMenu.ImageScalingSize =
            new Size(32, 32);

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
            new Size(347, 194);

        //
        // addRelatedDocumentsMenuItem
        //
        addRelatedDocumentsMenuItem.Name =
            "addRelatedDocumentsMenuItem";
        addRelatedDocumentsMenuItem.Size =
            new Size(346, 38);
        addRelatedDocumentsMenuItem.Text =
            UiMessages.AddRelatedDocuments;

        //
        // documentInformationMenuItem
        //
        documentInformationMenuItem.Name =
            "documentInformationMenuItem";
        documentInformationMenuItem.Size =
            new Size(346, 38);
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
            new Size(346, 38);
        saveWorkspaceMenuItem.Text =
            UiMessages.SaveAsWorkspace;

        //
        // removeDocumentMenuItem
        //
        removeDocumentMenuItem.Name =
            "removeDocumentMenuItem";
        removeDocumentMenuItem.Size =
            new Size(346, 38);
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
            new Size(346, 38);
        closeWorkspaceMenuItem.Text =
            UiMessages.CloseWorkspace;
        closeWorkspaceMenuItem.Click +=
            closeWorkspaceMenuItem_Click;

        //
        // DocumentViewForm
        //
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1671, 1280);
        Controls.Add(unsupportedPreviewPanel);
        Controls.Add(documentContentPanel);
        Controls.Add(workspaceHeaderPanel);
        Margin = new Padding(6);
        MinimumSize = new Size(1649, 1200);
        Name = "DocumentViewForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = UiMessages.DocumentWorkspaceTitle;

        workspaceHeaderPanel.ResumeLayout(false);
        workspaceHeaderPanel.PerformLayout();

        unsupportedPreviewLayout.ResumeLayout(false);
        unsupportedPreviewLayout.PerformLayout();

        unsupportedPreviewPanel.ResumeLayout(false);

        workspaceContextMenu.ResumeLayout(false);

        ResumeLayout(false);
    }

    #endregion
}

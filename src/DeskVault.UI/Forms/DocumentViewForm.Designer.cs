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

        workspaceContextMenu = new ContextMenuStrip(components);
        addRelatedDocumentsMenuItem = new ToolStripMenuItem();
        documentInformationMenuItem = new ToolStripMenuItem();
        saveWorkspaceMenuItem = new ToolStripMenuItem();
        removeDocumentMenuItem = new ToolStripMenuItem();
        closeWorkspaceMenuItem = new ToolStripMenuItem();

        workspaceHeaderPanel.SuspendLayout();
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
        workspaceHeaderPanel.Name = "workspaceHeaderPanel";
        workspaceHeaderPanel.Size = new Size(900, 72);
        workspaceHeaderPanel.TabIndex = 0;

        //
        // backButton
        //
        backButton.Location = new Point(12, 18);
        backButton.Name = "backButton";
        backButton.Size = new Size(90, 32);
        backButton.TabIndex = 0;
        backButton.Text = UiMessages.BackToDocuments;
        backButton.UseVisualStyleBackColor = true;

        //
        // documentTitleLabel
        //
        documentTitleLabel.AutoSize = true;
        documentTitleLabel.Location = new Point(120, 14);
        documentTitleLabel.Name = "documentTitleLabel";
        documentTitleLabel.Size = new Size(59, 15);
        documentTitleLabel.TabIndex = 1;
        documentTitleLabel.Text = UiMessages.DocumentColumnHeader;

        //
        // documentMetadataLabel
        //
        documentMetadataLabel.AutoSize = true;
        documentMetadataLabel.Location = new Point(120, 38);
        documentMetadataLabel.Name = "documentMetadataLabel";
        documentMetadataLabel.Size = new Size(128, 15);
        documentMetadataLabel.TabIndex = 2;
        documentMetadataLabel.Text = UiMessages.DocumentWorkspaceMetadata;

        //
        // aiButton
        //
        aiButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        aiButton.Location = new Point(636, 18);
        aiButton.Name = "aiButton";
        aiButton.Size = new Size(60, 32);
        aiButton.TabIndex = 3;
        aiButton.Text = UiMessages.AiButton;
        aiButton.UseVisualStyleBackColor = true;

        //
        // workspaceMenuButton
        //
        workspaceMenuButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        workspaceMenuButton.Location = new Point(702, 18);
        workspaceMenuButton.Name = "workspaceMenuButton";
        workspaceMenuButton.Size = new Size(60, 32);
        workspaceMenuButton.TabIndex = 4;
        workspaceMenuButton.Text = UiMessages.WorkspaceMenuButton;
        workspaceMenuButton.UseVisualStyleBackColor = true;

        //
        // closeButton
        //
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeButton.Location = new Point(768, 18);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(120, 32);
        closeButton.TabIndex = 5;
        closeButton.Text = UiMessages.CloseButton;
        closeButton.UseVisualStyleBackColor = true;

        //
        // documentContentPanel
        //
        documentContentPanel.Dock = DockStyle.Fill;
        documentContentPanel.Location = new Point(0, 72);
        documentContentPanel.Name = "documentContentPanel";
        documentContentPanel.Size = new Size(900, 528);
        documentContentPanel.TabIndex = 6;

        //
        // workspaceContextMenu
        //
        workspaceContextMenu.Items.AddRange(
            new ToolStripItem[]
            {
                addRelatedDocumentsMenuItem,
                documentInformationMenuItem,
                saveWorkspaceMenuItem,
                removeDocumentMenuItem,
                closeWorkspaceMenuItem
            });

        workspaceContextMenu.Name = "workspaceContextMenu";
        workspaceContextMenu.Size = new Size(220, 114);

        //
        // addRelatedDocumentsMenuItem
        //
        addRelatedDocumentsMenuItem.Name = "addRelatedDocumentsMenuItem";
        addRelatedDocumentsMenuItem.Size = new Size(219, 22);
        addRelatedDocumentsMenuItem.Text =
            UiMessages.AddRelatedDocuments;

        //
        // documentInformationMenuItem
        //
        documentInformationMenuItem.Name = "documentInformationMenuItem";
        documentInformationMenuItem.Size = new Size(219, 22);
        documentInformationMenuItem.Text =
            UiMessages.DocumentInformation;

        //
        // saveWorkspaceMenuItem
        //
        saveWorkspaceMenuItem.Name = "saveWorkspaceMenuItem";
        saveWorkspaceMenuItem.Size = new Size(219, 22);
        saveWorkspaceMenuItem.Text =
            UiMessages.SaveAsWorkspace;

        //
        // removeDocumentMenuItem
        //
        removeDocumentMenuItem.Name = "removeDocumentMenuItem";
        removeDocumentMenuItem.Size = new Size(219, 22);
        removeDocumentMenuItem.Text =
            UiMessages.RemoveDocument;

        //
        // closeWorkspaceMenuItem
        //
        closeWorkspaceMenuItem.Name = "closeWorkspaceMenuItem";
        closeWorkspaceMenuItem.Size = new Size(219, 22);
        closeWorkspaceMenuItem.Text =
            UiMessages.CloseWorkspace;

        //
        // DocumentViewForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 600);
        Controls.Add(documentContentPanel);
        Controls.Add(workspaceHeaderPanel);
        MinimumSize = new Size(900, 600);
        Name = "DocumentViewForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = UiMessages.DocumentWorkspaceTitle;

        workspaceHeaderPanel.ResumeLayout(false);
        workspaceHeaderPanel.PerformLayout();
        workspaceContextMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion
}

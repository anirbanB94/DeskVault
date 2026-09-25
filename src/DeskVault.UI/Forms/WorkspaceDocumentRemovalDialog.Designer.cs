namespace DeskVault.UI.Forms;

using DeskVault.UI.Resources;

partial class WorkspaceDocumentRemovalDialog
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Label searchLabel;
    private System.Windows.Forms.TextBox searchTextBox;
    private System.Windows.Forms.Label fileTypeLabel;
    private System.Windows.Forms.ComboBox fileTypeComboBox;
    private System.Windows.Forms.ListView documentListView;
    private System.Windows.Forms.ColumnHeader fileNameColumnHeader;
    private System.Windows.Forms.ColumnHeader typeColumnHeader;
    private System.Windows.Forms.ColumnHeader statusColumnHeader;
    private System.Windows.Forms.Label emptyStateLabel;
    private System.Windows.Forms.Label validationLabel;
    private System.Windows.Forms.Button cancelButton;
    private System.Windows.Forms.Button removeButton;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed.</param>
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
        searchLabel = new Label();
        searchTextBox = new TextBox();
        fileTypeLabel = new Label();
        fileTypeComboBox = new ComboBox();
        documentListView = new ListView();
        fileNameColumnHeader = new ColumnHeader();
        typeColumnHeader = new ColumnHeader();
        statusColumnHeader = new ColumnHeader();
        emptyStateLabel = new Label();
        validationLabel = new Label();
        cancelButton = new Button();
        removeButton = new Button();
        SuspendLayout();
        // 
        // searchLabel
        // 
        searchLabel.AutoSize = true;
        searchLabel.Location = new Point(59, 60);
        searchLabel.Margin = new Padding(6, 0, 6, 0);
        searchLabel.Name = "searchLabel";
        searchLabel.Size = new Size(85, 32);
        searchLabel.TabIndex = 0;
        searchLabel.Text = UiMessages.SearchButton;
        // 
        // searchTextBox
        // 
        searchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchTextBox.Location = new Point(59, 107);
        searchTextBox.Margin = new Padding(6);
        searchTextBox.Name = "searchTextBox";
        searchTextBox.Size = new Size(732, 39);
        searchTextBox.TabIndex = 1;
        // 
        // fileTypeLabel
        // 
        fileTypeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        fileTypeLabel.AutoSize = true;
        fileTypeLabel.Location = new Point(825, 60);
        fileTypeLabel.Margin = new Padding(6, 0, 6, 0);
        fileTypeLabel.Name = "fileTypeLabel";
        fileTypeLabel.Size = new Size(109, 32);
        fileTypeLabel.TabIndex = 2;
        fileTypeLabel.Text = UiMessages.FileTypeLabel;
        // 
        // fileTypeComboBox
        // 
        fileTypeComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        fileTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        fileTypeComboBox.FormattingEnabled = true;
        fileTypeComboBox.Location = new Point(825, 107);
        fileTypeComboBox.Margin = new Padding(6);
        fileTypeComboBox.Name = "fileTypeComboBox";
        fileTypeComboBox.Size = new Size(598, 40);
        fileTypeComboBox.TabIndex = 3;
        // 
        // documentListView
        // 
        documentListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        documentListView.CheckBoxes = true;
        documentListView.Columns.AddRange(new ColumnHeader[] { fileNameColumnHeader, typeColumnHeader, statusColumnHeader });
        documentListView.FullRowSelect = true;
        documentListView.GridLines = true;
        documentListView.Location = new Point(59, 196);
        documentListView.Margin = new Padding(6);
        documentListView.Name = "documentListView";
        documentListView.Size = new Size(1363, 678);
        documentListView.TabIndex = 4;
        documentListView.UseCompatibleStateImageBehavior = false;
        documentListView.View = View.Details;
        // 
        // fileNameColumnHeader
        // 
        fileNameColumnHeader.Text = UiMessages.FileNameColumnHeader;
        fileNameColumnHeader.Width = 536;
        // 
        // typeColumnHeader
        // 
        typeColumnHeader.Text = UiMessages.TypeColumnHeader;
        typeColumnHeader.Width = 80;
        // 
        // statusColumnHeader
        // 
        statusColumnHeader.Text = UiMessages.StatusColumnHeader;
        statusColumnHeader.Width = 120;
        // 
        // emptyStateLabel
        // 
        emptyStateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        emptyStateLabel.Location = new Point(59, 196);
        emptyStateLabel.Margin = new Padding(6, 0, 6, 0);
        emptyStateLabel.Name = "emptyStateLabel";
        emptyStateLabel.Size = new Size(1367, 683);
        emptyStateLabel.TabIndex = 5;
        emptyStateLabel.Text = UiMessages.NoDocumentsInWorkspace;
        emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        emptyStateLabel.Visible = false;
        // 
        // validationLabel
        // 
        validationLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        validationLabel.Location = new Point(59, 913);
        validationLabel.Margin = new Padding(6, 0, 6, 0);
        validationLabel.Name = "validationLabel";
        validationLabel.Size = new Size(960, 51);
        validationLabel.TabIndex = 6;
        // 
        // cancelButton
        // 
        cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(1031, 905);
        cancelButton.Margin = new Padding(6);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(167, 68);
        cancelButton.TabIndex = 7;
        cancelButton.Text = UiMessages.CancelButton;
        cancelButton.UseVisualStyleBackColor = true;
        // 
        // removeButton
        // 
        removeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        removeButton.Location = new Point(1210, 905);
        removeButton.Margin = new Padding(6);
        removeButton.Name = "removeButton";
        removeButton.Size = new Size(212, 68);
        removeButton.TabIndex = 8;
        removeButton.Text = UiMessages.RemoveSelectedButton;
        removeButton.UseVisualStyleBackColor = true;
        // 
        // WorkspaceDocumentRemovalDialog
        // 
        AcceptButton = removeButton;
        AutoScaleDimensions = new SizeF(13F, 32F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new Size(1486, 1024);
        Controls.Add(removeButton);
        Controls.Add(cancelButton);
        Controls.Add(validationLabel);
        Controls.Add(emptyStateLabel);
        Controls.Add(documentListView);
        Controls.Add(fileTypeComboBox);
        Controls.Add(fileTypeLabel);
        Controls.Add(searchTextBox);
        Controls.Add(searchLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(6);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WorkspaceDocumentRemovalDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = UiMessages.RemoveDocumentsFromWorkspace;
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}

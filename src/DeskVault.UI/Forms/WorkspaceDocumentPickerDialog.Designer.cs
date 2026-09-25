namespace DeskVault.UI.Forms;

using DeskVault.UI.Resources;

partial class WorkspaceDocumentPickerDialog
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
    private System.Windows.Forms.Button addButton;

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
        this.components = new System.ComponentModel.Container();
        this.searchLabel = new System.Windows.Forms.Label();
        this.searchTextBox = new System.Windows.Forms.TextBox();
        this.fileTypeLabel = new System.Windows.Forms.Label();
        this.fileTypeComboBox = new System.Windows.Forms.ComboBox();
        this.documentListView = new System.Windows.Forms.ListView();
        this.fileNameColumnHeader = new System.Windows.Forms.ColumnHeader();
        this.typeColumnHeader = new System.Windows.Forms.ColumnHeader();
        this.statusColumnHeader = new System.Windows.Forms.ColumnHeader();
        this.emptyStateLabel = new System.Windows.Forms.Label();
        this.validationLabel = new System.Windows.Forms.Label();
        this.cancelButton = new System.Windows.Forms.Button();
        this.addButton = new System.Windows.Forms.Button();
        this.SuspendLayout();
        //
        // searchLabel
        //
        this.searchLabel.AutoSize = true;
        this.searchLabel.Location = new System.Drawing.Point(32, 28);
        this.searchLabel.Name = "searchLabel";
        this.searchLabel.Size = new System.Drawing.Size(42, 15);
        this.searchLabel.TabIndex = 0;
        this.searchLabel.Text = UiMessages.SearchButton;
        //
        // searchTextBox
        //
        this.searchTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.searchTextBox.Location = new System.Drawing.Point(32, 50);
        this.searchTextBox.Name = "searchTextBox";
        this.searchTextBox.Size = new System.Drawing.Size(396, 23);
        this.searchTextBox.TabIndex = 1;
        //
        // fileTypeLabel
        //
        this.fileTypeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.fileTypeLabel.AutoSize = true;
        this.fileTypeLabel.Location = new System.Drawing.Point(444, 28);
        this.fileTypeLabel.Name = "fileTypeLabel";
        this.fileTypeLabel.Size = new System.Drawing.Size(52, 15);
        this.fileTypeLabel.TabIndex = 2;
        this.fileTypeLabel.Text = UiMessages.FileTypeLabel;
        //
        // fileTypeComboBox
        //
        this.fileTypeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.fileTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.fileTypeComboBox.FormattingEnabled = true;
        this.fileTypeComboBox.Location = new System.Drawing.Point(444, 50);
        this.fileTypeComboBox.Name = "fileTypeComboBox";
        this.fileTypeComboBox.Size = new System.Drawing.Size(324, 23);
        this.fileTypeComboBox.TabIndex = 3;
        //
        // documentListView
        //
        this.documentListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.documentListView.CheckBoxes = true;
        this.documentListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.fileNameColumnHeader,
            this.typeColumnHeader,
            this.statusColumnHeader});
        this.documentListView.FullRowSelect = true;
        this.documentListView.GridLines = true;
        this.documentListView.HideSelection = false;
        this.documentListView.Location = new System.Drawing.Point(32, 92);
        this.documentListView.MultiSelect = true;
        this.documentListView.Name = "documentListView";
        this.documentListView.Size = new System.Drawing.Size(736, 320);
        this.documentListView.TabIndex = 4;
        this.documentListView.UseCompatibleStateImageBehavior = false;
        this.documentListView.View = System.Windows.Forms.View.Details;
        //
        // fileNameColumnHeader
        //
        this.fileNameColumnHeader.Text = UiMessages.FileNameColumnHeader;
        this.fileNameColumnHeader.Width = 536;
        //
        // typeColumnHeader
        //
        this.typeColumnHeader.Text = UiMessages.TypeColumnHeader;
        this.typeColumnHeader.Width = 80;
        //
        // statusColumnHeader
        //
        this.statusColumnHeader.Text = UiMessages.StatusColumnHeader;
        this.statusColumnHeader.Width = 120;
        //
        // emptyStateLabel
        //
        this.emptyStateLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right));
        this.emptyStateLabel.AutoSize = false;
        this.emptyStateLabel.Location = new System.Drawing.Point(32, 92);
        this.emptyStateLabel.Name = "emptyStateLabel";
        this.emptyStateLabel.Size = new System.Drawing.Size(736, 320);
        this.emptyStateLabel.TabIndex = 5;
        this.emptyStateLabel.Text = UiMessages.NoDocumentsAvailable;
        this.emptyStateLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this.emptyStateLabel.Visible = false;
        //
        // validationLabel
        //
        this.validationLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.validationLabel.AutoSize = false;
        this.validationLabel.Location = new System.Drawing.Point(32, 428);
        this.validationLabel.Name = "validationLabel";
        this.validationLabel.Size = new System.Drawing.Size(536, 24);
        this.validationLabel.TabIndex = 6;
        //
        // cancelButton
        //
        this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this.cancelButton.Location = new System.Drawing.Point(578, 424);
        this.cancelButton.Name = "cancelButton";
        this.cancelButton.Size = new System.Drawing.Size(90, 32);
        this.cancelButton.TabIndex = 7;
        this.cancelButton.Text = UiMessages.CancelButton;
        this.cancelButton.UseVisualStyleBackColor = true;
        //
        // addButton
        //
        this.addButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.addButton.Location = new System.Drawing.Point(678, 424);
        this.addButton.Name = "addButton";
        this.addButton.Size = new System.Drawing.Size(90, 32);
        this.addButton.TabIndex = 8;
        this.addButton.Text = UiMessages.AddSelectedButton;
        this.addButton.UseVisualStyleBackColor = true;
        //
        // WorkspaceDocumentPickerDialog
        //
        this.AcceptButton = this.addButton;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.CancelButton = this.cancelButton;
        this.ClientSize = new System.Drawing.Size(800, 480);
        this.Controls.Add(this.addButton);
        this.Controls.Add(this.cancelButton);
        this.Controls.Add(this.validationLabel);
        this.Controls.Add(this.emptyStateLabel);
        this.Controls.Add(this.documentListView);
        this.Controls.Add(this.fileTypeComboBox);
        this.Controls.Add(this.fileTypeLabel);
        this.Controls.Add(this.searchTextBox);
        this.Controls.Add(this.searchLabel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "WorkspaceDocumentPickerDialog";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = UiMessages.AddDocumentsToWorkspace;
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion
}

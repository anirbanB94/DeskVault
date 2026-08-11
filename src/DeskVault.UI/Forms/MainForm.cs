using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;

namespace DeskVault.UI.Forms;

public partial class MainForm : Form, IMainFormView
{
    private readonly IApplicationInfoService _applicationInfo;
    private readonly IDocumentViewer _documentViewer;
    private readonly MainFormPresenter _presenter;

    public MainForm(
        IApplicationInfoService applicationInfo,
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        IDocumentViewer documentViewer,
        ListDocumentsHandler listDocumentsHandler)
    {
        InitializeComponent();

        _applicationInfo = applicationInfo;
        _documentViewer = documentViewer;

        _presenter = new MainFormPresenter(
            this,
            importDocumentHandler,
            removeDocumentHandler,
            openDocumentHandler,
            listDocumentsHandler);

        Text = $"{_applicationInfo.ApplicationName} v{_applicationInfo.Version}";

        documentGridView.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "documentNameColumn",
                HeaderText = "Document",
                DataPropertyName = nameof(DocumentListItem.FileName),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

        Load += MainForm_Load;
        importButton.Click += OnImportButtonClick;
        openButton.Click += OnOpenButtonClick;
        removeButton.Click += OnRemoveButtonClick;
        documentGridView.SelectionChanged += OnDocumentSelectionChanged;
        documentGridView.CellDoubleClick += OnDocumentDoubleClick;
    }

    public event EventHandler? ImportRequested;

    public event EventHandler? OpenRequested;

    public event EventHandler? RemoveRequested;

    public event EventHandler? DocumentSelectionChanged;

    public Guid? SelectedDocumentId
    {
        get
        {
            return documentGridView.CurrentRow?.DataBoundItem
                is DocumentListItem item
                    ? item.Id
                    : null;
        }
    }

    public string? SelectedDocumentFileName
    {
        get
        {
            return documentGridView.CurrentRow?.DataBoundItem
                is DocumentListItem item
                    ? item.FileName
                    : null;
        }
    }

    public string? SelectedFilePath
    {
        get
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select a document to import",
                CheckFileExists = true,
                Multiselect = false,
                Filter =
                    "Supported Documents|*.pdf;*.docx;*.txt;*.md;*.csv|" +
                    "All Files|*.*"
            };

            return dialog.ShowDialog(this) == DialogResult.OK
                ? dialog.FileName
                : null;
        }
    }

    private async void MainForm_Load(
        object? sender,
        EventArgs e)
    {
        await _presenter.InitializeAsync();
    }

    private void OnImportButtonClick(
        object? sender,
        EventArgs e)
    {
        ImportRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnOpenButtonClick(
        object? sender,
        EventArgs e)
    {
        OpenRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnRemoveButtonClick(
    object? sender,
    EventArgs e)
    {
        RemoveRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnDocumentSelectionChanged(
        object? sender,
        EventArgs e)
    {
        DocumentSelectionChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnDocumentDoubleClick(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (SelectedDocumentId is not null)
        {
            OpenRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    public void SetSelectedDocumentId(
        Guid? documentId)
    {
        if (documentId is null)
        {
            documentGridView.ClearSelection();
            return;
        }

        foreach (DataGridViewRow row in documentGridView.Rows)
        {
            if (row.DataBoundItem is DocumentListItem item &&
                item.Id == documentId.Value)
            {
                row.Selected = true;
                documentGridView.CurrentCell =
                    row.Cells[0];

                return;
            }
        }
    }

    public void SetImportEnabled(
        bool enabled)
    {
        importButton.Enabled = enabled;
    }

    public void SetOpenEnabled(
        bool enabled)
    {
        openButton.Enabled = enabled;
    }

    public void SetRemoveEnabled(
    bool enabled)
    {
        removeButton.Enabled = enabled;
    }

    public void SetStatus(
        string message)
    {
        statusLabel.Text = message;
    }

    public void ShowInformation(
        string message,
        string title)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    public void ShowWarning(
        string message,
        string title)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    public void ShowError(
        string message,
        string title)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    public bool ConfirmRemoval(
    string fileName)
    {
        var result = MessageBox.Show(
            this,
            UiMessages.ConfirmRemoveDocument(fileName),
            UiMessages.RemoveDocumentTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.Yes;
    }

    public Task ShowDocumentAsync(
    Stream documentStream,
    string fileName,
    CancellationToken cancellationToken = default)
    {
        return _documentViewer.OpenAsync(
            documentStream,
            fileName,
            cancellationToken);
    }

    public void ShowDocuments(
        IReadOnlyList<DocumentListItem> documents)
    {
        documentGridView.DataSource = null;

        documentGridView.DataSource =
            documents.ToList();

        documentGridView.Visible = documents.Count > 0;
        emptyStateLabel.Visible = documents.Count == 0;

        if (documents.Count > 0)
        {
            documentGridView.ClearSelection();

            documentGridView.Rows[0].Selected = true;
            documentGridView.CurrentCell =
                documentGridView.Rows[0].Cells[0];
        }
    }

    public void ShowEmptyState()
    {
        documentGridView.Visible = false;
        emptyStateLabel.Visible = true;
    }
}

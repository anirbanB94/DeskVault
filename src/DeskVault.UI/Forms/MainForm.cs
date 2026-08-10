using DeskVault.Application.Interfaces;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.UI.Presenters;
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
            openDocumentHandler,
            listDocumentsHandler);

        Text = $"{_applicationInfo.ApplicationName} v{_applicationInfo.Version}";

        Load += MainForm_Load;
        importButton.Click += OnImportButtonClick;
        openButton.Click += OnOpenButtonClick;
        documentListBox.SelectedIndexChanged += OnDocumentSelectionChanged;
    }

    public event EventHandler? ImportRequested;

    public event EventHandler? OpenRequested;

    public event EventHandler? DocumentSelectionChanged;

    public Guid? SelectedDocumentId
    {
        get
        {
            return documentListBox.SelectedItem
                is DocumentListItem item
                    ? item.Id
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

    private void OnDocumentSelectionChanged(
    object? sender,
    EventArgs e)
    {
        DocumentSelectionChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void SetSelectedDocumentId(
    Guid? documentId)
    {
        if (documentId is null)
        {
            documentListBox.ClearSelected();
            return;
        }

        for (int index = 0;
             index < documentListBox.Items.Count;
             index++)
        {
            if (documentListBox.Items[index]
                is DocumentListItem item &&
                item.Id == documentId.Value)
            {
                documentListBox.SelectedIndex = index;
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

    public Task OpenDocumentAsync(
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
        documentListBox.DataSource = null;

        documentListBox.DataSource =
            documents
                .Select(document => new DocumentListItem(
                    document.Id,
                    document.FileName))
                .ToList();

        documentListBox.DisplayMember = nameof(
            DocumentListItem.FileName);

        documentListBox.ValueMember = nameof(
            DocumentListItem.Id);

        documentListBox.Visible = documents.Count > 0;
        emptyStateLabel.Visible = documents.Count == 0;

        if (documents.Count > 0)
        {
            documentListBox.SelectedIndex = 0;
        }
    }

    public void ShowEmptyState()
    {
        emptyStateLabel.Visible = true;
    }
}
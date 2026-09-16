using DeskVault.Application.Documents;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Timer = System.Windows.Forms.Timer;

namespace DeskVault.UI.Forms;

public partial class MainForm : Form, IMainFormView
{
    private readonly IApplicationInfoService _applicationInfo;
    private readonly MainFormPresenter _presenter;
    private readonly ILogger<MainForm> _logger;
    private readonly Timer _searchDebounceTimer;
    private readonly List<SearchResultListItem> _searchResults = [];

    private bool _isUpdatingDocumentGrid;

    public MainForm(
        IApplicationInfoService applicationInfo,
        IMainFormPresenterFactory presenterFactory,
        ILogger<MainForm> logger)
    {
        InitializeComponent();

        _applicationInfo = applicationInfo;
        _presenter = presenterFactory.Create(this);
        _logger = logger;

        _searchDebounceTimer = new Timer
        {
            Interval = 300
        };

        _searchDebounceTimer.Tick += OnSearchDebounceTimerTick;

        searchFileTypeComboBox.Items.Clear();
        searchFileTypeComboBox.Items.Add(UiMessages.SearchFileTypeAll);

        foreach (string extension in SupportedFileTypes.GetAll().Order())
        {
            searchFileTypeComboBox.Items.Add(extension);
        }

        searchFileTypeComboBox.SelectedIndex = 0;

        Text =
            $"{_applicationInfo.ApplicationName} v{_applicationInfo.Version}";

        documentGridView.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = "documentNameColumn",
                HeaderText = UiMessages.DocumentColumnHeader,
                DataPropertyName = nameof(DocumentListItem.FileName),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

        Load += MainForm_Load;
        importButton.Click += OnImportButtonClick;
        openButton.Click += OnOpenButtonClick;
        removeButton.Click += OnRemoveButtonClick;
        reprocessButton.Click += OnReprocessButtonClick;
        searchButton.Click += OnSearchButtonClick;
        clearSearchButton.Click += OnClearSearchButtonClick;
        loadMoreButton.Click += OnLoadMoreButtonClick;
        searchTextBox.TextChanged += OnSearchTextBoxTextChanged;
        searchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        documentGridView.SelectionChanged += OnDocumentSelectionChanged;
        documentGridView.CellDoubleClick += OnDocumentDoubleClick;
        FormClosed += OnFormClosed;
    }

    public event EventHandler? ImportRequested;

    public event EventHandler? OpenRequested;

    public event EventHandler? RemoveRequested;

    public event EventHandler? ReprocessRequested;

    public event EventHandler? DocumentSelectionChanged;

    public event EventHandler? SearchRequested;

    public event EventHandler? LoadMoreSearchResultsRequested;

    public Guid? SelectedDocumentId
    {
        get
        {
            return documentGridView.CurrentRow?.DataBoundItem
                is DocumentListItem item
                    ? item.Id
                    : documentGridView.CurrentRow?.DataBoundItem
                        is SearchResultListItem searchResult
                            ? searchResult.DocumentId
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
                    : documentGridView.CurrentRow?.DataBoundItem
                        is SearchResultListItem searchResult
                            ? searchResult.FileName
                            : null;
        }
    }

    public string? SelectedFilePath
    {
        get
        {
            using var dialog = new OpenFileDialog
            {
                Title = UiMessages.SelectDocumentToImportTitle,
                CheckFileExists = true,
                Multiselect = false,
                Filter =
                    UiMessages.SupportedDocumentsFilter +
                    UiMessages.AllFilesFilter
            };

            return dialog.ShowDialog(this) == DialogResult.OK
                ? dialog.FileName
                : null;
        }
    }

    public string SearchText => searchTextBox.Text;

    public string? SearchFileType =>
        searchFileTypeComboBox.SelectedItem is string selectedFileType &&
        selectedFileType != UiMessages.SearchFileTypeAll
            ? selectedFileType
            : null;

    private async void MainForm_Load(
        object? sender,
        EventArgs e)
    {
        _logger.LogInformation(
            LogMessages.MainFormLoadStarted);

        try
        {
            await _presenter.InitializeAsync();

            _logger.LogInformation(
                LogMessages.MainFormLoadCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.MainFormLoadFailed);

            throw;
        }
    }

    private void OnImportButtonClick(
        object? sender,
        EventArgs e)
    {
        ImportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenButtonClick(
        object? sender,
        EventArgs e)
    {
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRemoveButtonClick(
        object? sender,
        EventArgs e)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnReprocessButtonClick(
        object? sender,
        EventArgs e)
    {
        ReprocessRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchButtonClick(
        object? sender,
        EventArgs e)
    {
        _searchDebounceTimer.Stop();
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnClearSearchButtonClick(
        object? sender,
        EventArgs e)
    {
        _searchDebounceTimer.Stop();
        searchTextBox.Clear();
        _searchDebounceTimer.Stop();
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoadMoreButtonClick(
        object? sender,
        EventArgs e)
    {
        LoadMoreSearchResultsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnSearchTextBoxTextChanged(
        object? sender,
        EventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnSearchTextBoxKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        _searchDebounceTimer.Stop();
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchDebounceTimerTick(
        object? sender,
        EventArgs e)
    {
        _searchDebounceTimer.Stop();
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnFormClosed(
        object? sender,
        FormClosedEventArgs e)
    {
        _searchDebounceTimer.Dispose();
    }

    private void OnDocumentSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (_isUpdatingDocumentGrid)
        {
            return;
        }

        DocumentSelectionChanged?.Invoke(this, EventArgs.Empty);
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
            OpenRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetSelectedDocumentId(Guid? documentId)
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
                documentGridView.CurrentCell = row.Cells[0];
                return;
            }

            if (row.DataBoundItem is SearchResultListItem searchResult &&
                searchResult.DocumentId == documentId.Value)
            {
                row.Selected = true;
                documentGridView.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    public void SetImportEnabled(bool enabled)
    {
        importButton.Enabled = enabled;
    }

    public void SetOpenEnabled(bool enabled)
    {
        openButton.Enabled = enabled;
    }

    public void SetRemoveEnabled(bool enabled)
    {
        removeButton.Enabled = enabled;
    }

    public void SetReprocessEnabled(bool enabled)
    {
        reprocessButton.Enabled = enabled;
    }

    public void SetLoadMoreEnabled(bool enabled)
    {
        loadMoreButton.Enabled = enabled;
        loadMoreButton.Visible = enabled;
    }

    public void SetStatus(string message)
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

    public bool ConfirmRemoval(string fileName)
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

    public void ShowDocuments(
        IReadOnlyList<DocumentListItem> documents)
    {
        _isUpdatingDocumentGrid = true;

        try
        {
            _searchResults.Clear();

            documentGridView.DataSource = null;
            documentGridView.Columns.Clear();

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "documentNameColumn",
                    HeaderText = UiMessages.DocumentColumnHeader,
                    DataPropertyName = nameof(DocumentListItem.FileName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.DataSource = documents.ToList();

            documentGridView.Visible = documents.Count > 0;
            emptyStateLabel.Visible = documents.Count == 0;

            if (documents.Count > 0)
            {
                documentGridView.ClearSelection();
                documentGridView.Rows[0].Selected = true;
                documentGridView.CurrentCell =
                    documentGridView.Rows[0].Cells[0];
            }

            SetLoadMoreEnabled(false);
        }
        finally
        {
            _isUpdatingDocumentGrid = false;
        }
    }

    public void ShowSearchResults(
        IReadOnlyList<SearchResultListItem> results)
    {
        _isUpdatingDocumentGrid = true;

        try
        {
            _searchResults.Clear();
            _searchResults.AddRange(results);

            documentGridView.Visible = false;
            documentGridView.DataSource = null;
            documentGridView.Columns.Clear();

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchDisplayNameColumn",
                    HeaderText = "Name",
                    DataPropertyName =
                        nameof(SearchResultListItem.DisplayName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchFileNameColumn",
                    HeaderText = "File",
                    DataPropertyName =
                        nameof(SearchResultListItem.FileName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchSnippetColumn",
                    HeaderText = "Match",
                    DataPropertyName =
                        nameof(SearchResultListItem.Snippet),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchMatchCountColumn",
                    HeaderText = "Matches",
                    DataPropertyName =
                        nameof(SearchResultListItem.MatchCount),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    Width = 80
                });

            documentGridView.DataSource = _searchResults;

            documentGridView.Visible = results.Count > 0;
            emptyStateLabel.Visible = results.Count == 0;
        }
        finally
        {
            _isUpdatingDocumentGrid = false;
        }
    }

    public void AppendSearchResults(
        IReadOnlyList<SearchResultListItem> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        _isUpdatingDocumentGrid = true;

        try
        {
            _searchResults.AddRange(results);

            documentGridView.Visible = false;
            documentGridView.DataSource = null;
            documentGridView.DataSource = _searchResults;

            documentGridView.Visible = true;
            emptyStateLabel.Visible = false;
        }
        finally
        {
            _isUpdatingDocumentGrid = false;
        }
    }

    public void ShowEmptyState()
    {
        _isUpdatingDocumentGrid = true;

        try
        {
            _searchResults.Clear();

            documentGridView.DataSource = null;
            documentGridView.Visible = false;
            emptyStateLabel.Visible = true;

            SetLoadMoreEnabled(false);
        }
        finally
        {
            _isUpdatingDocumentGrid = false;
        }
    }
}

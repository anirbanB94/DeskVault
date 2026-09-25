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
    private bool _isUpdatingWorkspaceGrid;

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
        createWorkspaceButton.Click += OnCreateWorkspaceButtonClick;
        openWorkspaceButton.Click += OnOpenWorkspaceButtonClick;
        removeWorkspaceButton.Click += OnRemoveWorkspaceButtonClick;
        searchButton.Click += OnSearchButtonClick;
        clearSearchButton.Click += OnClearSearchButtonClick;
        loadMoreButton.Click += OnLoadMoreButtonClick;
        searchTextBox.TextChanged += OnSearchTextBoxTextChanged;
        searchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        documentGridView.SelectionChanged += OnDocumentSelectionChanged;
        documentGridView.CellDoubleClick += OnDocumentDoubleClick;
        workspaceGridView.SelectionChanged += OnWorkspaceSelectionChanged;
        workspaceGridView.CellDoubleClick += OnWorkspaceDoubleClick;
        FormClosed += OnFormClosed;
    }

    public event EventHandler? ImportRequested;

    public event EventHandler? OpenRequested;

    public event EventHandler? RemoveRequested;

    public event EventHandler? ReprocessRequested;

    public event EventHandler? DocumentSelectionChanged;

    public event EventHandler? SearchRequested;

    public event EventHandler? LoadMoreSearchResultsRequested;

    public event EventHandler? WorkspaceSelectionChanged;

    public event EventHandler? WorkspaceCreateRequested;

    public event EventHandler? WorkspaceOpenRequested;

    public event EventHandler? WorkspaceRemoveRequested;

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

    public Guid? SelectedWorkspaceId =>
        workspaceGridView.CurrentRow?.DataBoundItem
            is WorkspaceListItem workspace
                ? workspace.Id
                : null;

    public string? SelectedWorkspaceName =>
        workspaceGridView.CurrentRow?.DataBoundItem
            is WorkspaceListItem workspace
                ? workspace.WorkspaceName
                : null;

    public WorkspaceCreateRequest? ShowCreateWorkspaceDialog()
    {
        using var dialog = new WorkspaceCreateDialog();

        return dialog.ShowDialog(this) == DialogResult.OK
            ? new WorkspaceCreateRequest(
                dialog.WorkspaceName,
                dialog.WorkspaceDescription)
            : null;
    }

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

    private void OnCreateWorkspaceButtonClick(
        object? sender,
        EventArgs e)
    {
        WorkspaceCreateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenWorkspaceButtonClick(
        object? sender,
        EventArgs e)
    {
        WorkspaceOpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRemoveWorkspaceButtonClick(
        object? sender,
        EventArgs e)
    {
        WorkspaceRemoveRequested?.Invoke(this, EventArgs.Empty);
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

    private void OnWorkspaceSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (_isUpdatingWorkspaceGrid)
        {
            return;
        }

        WorkspaceSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkspaceDoubleClick(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (SelectedWorkspaceId is not null)
        {
            WorkspaceOpenRequested?.Invoke(this, EventArgs.Empty);
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

    public void SetSelectedWorkspaceId(Guid? workspaceId)
    {
        if (workspaceId is null)
        {
            workspaceGridView.ClearSelection();
            return;
        }

        foreach (DataGridViewRow row in workspaceGridView.Rows)
        {
            if (row.DataBoundItem is WorkspaceListItem workspace &&
                workspace.Id == workspaceId.Value)
            {
                row.Selected = true;
                workspaceGridView.CurrentCell = row.Cells[0];
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

    public void SetWorkspaceOpenEnabled(bool enabled)
    {
        openWorkspaceButton.Enabled = enabled;
    }

    public void SetWorkspaceRemoveEnabled(bool enabled)
    {
        removeWorkspaceButton.Enabled = enabled;
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

    public void SetDocumentsCount(int count)
    {
        documentsCountLabel.Text = string.Format(UiMessages.DocumentCount, count);
    }

    public void SetWorkspacesCount(int count)
    {
        workspacesCountLabel.Text = string.Format(UiMessages.WorkspaceCount, count);
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

    public bool ConfirmWorkspaceRemoval(string workspaceName)
    {
        var result = MessageBox.Show(
            this,
            UiMessages.ConfirmDeleteWorkspace(workspaceName),
            UiMessages.DeleteWorkspace,
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
            documentGridView.AutoGenerateColumns = false;

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "documentNameColumn",
                    HeaderText = UiMessages.FileNameColumnHeader,
                    DataPropertyName = nameof(DocumentListItem.FileName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 40
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "documentTypeColumn",
                    HeaderText = UiMessages.TypeColumnHeader,
                    DataPropertyName = nameof(DocumentListItem.Type),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 15
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "documentImportedColumn",
                    HeaderText = UiMessages.ImportedColumnHeader,
                    DataPropertyName = nameof(DocumentListItem.Imported),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 25,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Format = "g"
                    }
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "documentStatusColumn",
                    HeaderText = UiMessages.StatusColumnHeader,
                    DataPropertyName = nameof(DocumentListItem.Status),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 20
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
                    HeaderText = UiMessages.NameColumnHeader,
                    DataPropertyName =
                        nameof(SearchResultListItem.DisplayName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchFileNameColumn",
                    HeaderText = UiMessages.FileColumnHeader,
                    DataPropertyName =
                        nameof(SearchResultListItem.FileName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchSnippetColumn",
                    HeaderText = UiMessages.MatchColumnHeader,
                    DataPropertyName =
                        nameof(SearchResultListItem.Snippet),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });

            documentGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "searchMatchCountColumn",
                    HeaderText = UiMessages.MatchesColumnHeader,
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

    public void ShowWorkspaces(
        IReadOnlyList<WorkspaceListItem> workspaces)
    {
        _isUpdatingWorkspaceGrid = true;

        try
        {
            workspaceGridView.DataSource = null;
            workspaceGridView.Columns.Clear();
            workspaceGridView.AutoGenerateColumns = false;

            workspaceGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "workspaceNameColumn",
                    HeaderText = UiMessages.WorkspaceNameColumnHeader,
                    DataPropertyName = nameof(WorkspaceListItem.WorkspaceName),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 22
                });

            workspaceGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "workspaceDescriptionColumn",
                    HeaderText = UiMessages.DescriptionColumnHeader,
                    DataPropertyName = nameof(WorkspaceListItem.Description),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 25
                });

            workspaceGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "workspaceFilesColumn",
                    HeaderText = UiMessages.FilesColumnHeader,
                    DataPropertyName = nameof(WorkspaceListItem.Files),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 33
                });

            workspaceGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "workspaceLastUpdatedColumn",
                    HeaderText = UiMessages.LastUpdatedColumnHeader,
                    DataPropertyName = nameof(WorkspaceListItem.LastUpdated),
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 20,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Format = "g"
                    }
                });

            workspaceGridView.DataSource = workspaces.ToList();

            workspaceGridView.Visible = workspaces.Count > 0;
            workspaceEmptyStateLabel.Visible = workspaces.Count == 0;

            if (workspaces.Count > 0)
            {
                workspaceGridView.ClearSelection();
                workspaceGridView.Rows[0].Selected = true;
                workspaceGridView.CurrentCell =
                    workspaceGridView.Rows[0].Cells[0];
            }
        }
        finally
        {
            _isUpdatingWorkspaceGrid = false;
        }

        WorkspaceSelectionChanged?.Invoke(this, EventArgs.Empty);
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

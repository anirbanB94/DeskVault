using DeskVault.Domain.Documents;
using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

public partial class WorkspaceDocumentRemovalDialog : Form
{
    private readonly IReadOnlyList<Document> _documents;

    public WorkspaceDocumentRemovalDialog(
        IReadOnlyList<Document> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        _documents = documents;

        InitializeComponent();

        removeButton.Click += OnRemoveButtonClick;
        searchTextBox.TextChanged += OnSearchTextChanged;
        fileTypeComboBox.SelectedIndexChanged += OnFilterChanged;
        documentListView.Resize += OnDocumentListViewResize;

        LoadDocumentList();
        UpdateColumnWidths();
    }

    public IReadOnlyList<Guid> SelectedDocumentIds { get; private set; } = [];

    private void LoadDocumentList()
    {
        fileTypeComboBox.Items.Clear();
        fileTypeComboBox.Items.Add(
            UiMessages.AllDocumentTypes);

        IEnumerable<string> fileTypes =
            _documents
                .Select(document => Path.GetExtension(document.FileName))
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension =>
                    extension.TrimStart('.').ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension);

        foreach (string fileType in fileTypes)
        {
            fileTypeComboBox.Items.Add(fileType);
        }

        fileTypeComboBox.SelectedIndex = 0;

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        string searchText =
            searchTextBox.Text.Trim();

        string selectedFileType =
            fileTypeComboBox.SelectedItem?.ToString() ??
            UiMessages.AllDocumentTypes;

        IEnumerable<Document> filteredDocuments =
            _documents;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filteredDocuments =
                filteredDocuments.Where(document =>
                    document.FileName.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(
                selectedFileType,
                UiMessages.AllDocumentTypes,
                StringComparison.OrdinalIgnoreCase))
        {
            filteredDocuments =
                filteredDocuments.Where(document =>
                    string.Equals(
                        Path.GetExtension(document.FileName)
                            .TrimStart('.'),
                        selectedFileType,
                        StringComparison.OrdinalIgnoreCase));
        }

        documentListView.Items.Clear();

        foreach (Document document in
                 filteredDocuments.OrderBy(
                     document => document.FileName))
        {
            ListViewItem item =
                new(document.FileName);

            item.SubItems.Add(
                Path.GetExtension(document.FileName)
                    .TrimStart('.')
                    .ToUpperInvariant());

            item.SubItems.Add(
                document.Status.ToString());

            item.Tag = document.Id;

            documentListView.Items.Add(item);
        }

        emptyStateLabel.Visible =
            documentListView.Items.Count == 0;

        UpdateColumnWidths();
    }

    private void UpdateColumnWidths()
    {
        if (documentListView.Columns.Count < 3)
        {
            return;
        }

        int availableWidth =
            documentListView.ClientSize.Width;

        if (availableWidth <= 0)
        {
            return;
        }

        fileNameColumnHeader.Width =
            (int)(availableWidth * 0.70);

        typeColumnHeader.Width =
            (int)(availableWidth * 0.12);

        statusColumnHeader.Width =
            availableWidth -
            fileNameColumnHeader.Width -
            typeColumnHeader.Width;
    }

    private void OnDocumentListViewResize(
        object? sender,
        EventArgs e)
    {
        UpdateColumnWidths();
    }

    private void OnSearchTextChanged(
        object? sender,
        EventArgs e)
    {
        ApplyFilters();
    }

    private void OnFilterChanged(
        object? sender,
        EventArgs e)
    {
        ApplyFilters();
    }

    private void OnRemoveButtonClick(
        object? sender,
        EventArgs e)
    {
        Guid[] selectedDocumentIds =
            documentListView.CheckedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag)
                .OfType<Guid>()
                .Distinct()
                .ToArray();

        if (selectedDocumentIds.Length == 0)
        {
            validationLabel.Text =
                UiMessages.SelectDocumentValidationMessage;

            return;
        }

        validationLabel.Text = string.Empty;
        SelectedDocumentIds = selectedDocumentIds;
        DialogResult = DialogResult.OK;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        UpdateColumnWidths();
        searchTextBox.Focus();
    }
}

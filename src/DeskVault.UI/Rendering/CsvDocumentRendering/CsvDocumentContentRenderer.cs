namespace DeskVault.UI.Rendering.CsvDocumentRendering;

public sealed class CsvDocumentContentRenderer
    : IDocumentContentRenderer
{
    private readonly CsvDocumentParser _parser;

    public int Priority => 0;

    public CsvDocumentContentRenderer(
        CsvDocumentParser parser)
    {
        _parser = parser;
    }

    public bool CanRender(string fileName)
    {
        return string.Equals(
            Path.GetExtension(fileName),
            ".csv",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        CsvDocument document =
            await _parser.ParseAsync(
                documentStream,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentContentHost.Clear(contentHost);

        var workspacePanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        if (document.Warnings.Count > 0)
        {
            var warningLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 42,
                Padding = new Padding(10, 8, 10, 8),
                Text = BuildWarningMessage(
                    document.Warnings),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = SystemColors.Info,
                ForeColor = SystemColors.InfoText
            };

            workspacePanel.Controls.Add(warningLabel);
        }

        if (document.Columns.Count == 0)
        {
            workspacePanel.Controls.Add(
                CreateMessageLabel(
                    "The CSV document is empty."));

            contentHost.Controls.Add(workspacePanel);

            return;
        }

        int previewRowCount =
            document.Rows.Count;

        bool isPreviewTruncated =
            document.HasMoreRows;

        var dataGridView =
            new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false
            };

        foreach (CsvDocumentColumn column
                 in document.Columns)
        {
            dataGridView.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = $"Column{column.Index}",
                    HeaderText = column.Header,
                    SortMode =
                        DataGridViewColumnSortMode.NotSortable
                });
        }

        for (int rowIndex = 0;
             rowIndex < previewRowCount;
             rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> row =
                document.Rows[rowIndex];

            object[] values =
                row
                    .Cast<object>()
                    .ToArray();

            dataGridView.Rows.Add(values);
        }

        if (isPreviewTruncated)
        {
            var previewLabel = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = 36,
                Padding = new Padding(10, 6, 10, 6),
                Text =
                    $"Preview limited to " +
                    $"{previewRowCount:N0} rows. " +
                    $"More rows are available in the source document.",
                TextAlign =
                    ContentAlignment.MiddleLeft,
                BackColor =
                    SystemColors.Control,
                ForeColor =
                    SystemColors.ControlText
            };

            workspacePanel.Controls.Add(
                previewLabel);
        }

        workspacePanel.Controls.Add(dataGridView);

        contentHost.Controls.Add(workspacePanel);
    }

    private static string BuildWarningMessage(
        IReadOnlyList<CsvDocumentWarning> warnings)
    {
        if (warnings.Count == 1)
        {
            return
                $"CSV structure warning: " +
                warnings[0].Message;
        }

        string affectedRows =
            string.Join(
                ", ",
                warnings.Select(
                    warning =>
                        warning.RowNumber.ToString()));

        return
            $"CSV structure warning: " +
            $"{warnings.Count} structural issues detected " +
            $"on rows {affectedRows}.";
    }

    private static Label CreateMessageLabel(
        string message)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = message,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false
        };
    }

}

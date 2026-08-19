namespace DeskVault.Application.Documents.Parsing.Csv;

public sealed class CsvDocument
{
    public static CsvDocument Empty { get; } =
        new(
            Array.Empty<CsvDocumentColumn>(),
            Array.Empty<IReadOnlyList<string>>(),
            Array.Empty<CsvDocumentWarning>(),
            hasMoreRows: false);

    public CsvDocument(
        IReadOnlyList<CsvDocumentColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<CsvDocumentWarning> warnings,
        bool hasMoreRows)
    {
        Columns = columns;
        Rows = rows;
        Warnings = warnings;
        HasMoreRows = hasMoreRows;
    }

    public IReadOnlyList<CsvDocumentColumn> Columns { get; }

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    public IReadOnlyList<CsvDocumentWarning> Warnings { get; }

    public bool HasMoreRows { get; }
}

namespace DeskVault.Application.Documents.Parsing.Csv;

public sealed record CsvDocumentWarning(
    int RowNumber,
    string Message);

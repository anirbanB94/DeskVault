namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchOptions
{
    public const int DefaultPageSize = 20;

    public const string SectionName = "Search";

    public int PageSize { get; set; } = DefaultPageSize;
}

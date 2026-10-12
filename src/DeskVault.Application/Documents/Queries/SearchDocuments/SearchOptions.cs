namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchOptions
{
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Maximum number of document results supported in one search page.
    /// </summary>
    public const int MaximumPageSize = 100;

    public const string SectionName = "Search";

    public int PageSize { get; set; } = DefaultPageSize;
}

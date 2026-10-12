namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchOptions
{
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Maximum number of document results supported in one search page.
    /// </summary>
    public const int MaximumPageSize = 100;

    /// <summary>
    /// Maximum number of representative evidence items retained per search result.
    /// </summary>
    public const int MaximumRetainedEvidenceCount = 1;

    public const string SectionName = "Search";

    public int PageSize { get; set; } = DefaultPageSize;
}

namespace DeskVault.Application.Documents.Processing;

public sealed class DocumentStructureLimitExceededException
    : InvalidOperationException
{
    public DocumentStructureLimitExceededException(
        string resourceName,
        long limit,
        long attempted)
        : base(
            $"Document processing structure limit exceeded for {resourceName}.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (attempted <= limit)
        {
            throw new ArgumentOutOfRangeException(nameof(attempted));
        }

        ResourceName = resourceName;
        Limit = limit;
        Attempted = attempted;
    }

    public string ResourceName { get; }

    public long Limit { get; }

    public long Attempted { get; }
}

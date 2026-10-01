namespace DeskVault.Application.Documents.Provenance;

public sealed record DocumentSourceLocation
{
    public DocumentSourceLocation(
        int startLine,
        int endLine)
    {
        if (startLine <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startLine),
                "Source location start line must be greater than zero.");
        }

        if (endLine < startLine)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endLine),
                "Source location end line must be greater than or equal to the start line.");
        }

        StartLine = startLine;
        EndLine = endLine;
    }

    public int StartLine { get; }

    public int EndLine { get; }
}

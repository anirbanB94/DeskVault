namespace DeskVault.Application.Documents.Chunking;

public readonly record struct DocumentChunkingRuleVersion
{
    public DocumentChunkingRuleVersion(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Chunking-rule version must not be empty.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}

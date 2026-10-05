namespace DeskVault.Application.Documents.Processing;

public readonly record struct DocumentProcessingRuleVersion
{
    public DocumentProcessingRuleVersion(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Processing-rule version must not be empty.",
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

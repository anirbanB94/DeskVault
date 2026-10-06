namespace DeskVault.Domain.Documents;

/// <summary>
/// Describes the availability of one derived knowledge representation
/// for a document and, when applicable, the processing generation that
/// produced the available knowledge.
/// </summary>
public sealed record DocumentKnowledgeAvailability
{
    /// <summary>
    /// Identifies the derived knowledge representation.
    /// </summary>
    public DocumentKnowledgeRepresentationKind Representation { get; }

    /// <summary>
    /// Gets the current availability state of the representation.
    /// </summary>
    public DocumentKnowledgeAvailabilityState State { get; }

    /// <summary>
    /// Gets the processing generation associated with the available
    /// or stale knowledge representation.
    /// </summary>
    public long? LastAvailableProcessingGeneration { get; }

    public DocumentKnowledgeAvailability(
        DocumentKnowledgeRepresentationKind representation,
        DocumentKnowledgeAvailabilityState state,
        long? lastAvailableProcessingGeneration)
    {
        if (!Enum.IsDefined(representation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(representation),
                "Knowledge representation is invalid.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Knowledge availability state is invalid.");
        }

        if (lastAvailableProcessingGeneration is not null &&
            lastAvailableProcessingGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastAvailableProcessingGeneration),
                "Last available processing generation must be greater than zero when provided.");
        }

        if (state is
            DocumentKnowledgeAvailabilityState.Available or
            DocumentKnowledgeAvailabilityState.Stale &&
            lastAvailableProcessingGeneration is null)
        {
            throw new ArgumentException(
                "Available or stale knowledge must reference a processing generation.",
                nameof(lastAvailableProcessingGeneration));
        }

        Representation = representation;
        State = state;
        LastAvailableProcessingGeneration =
            lastAvailableProcessingGeneration;
    }
}

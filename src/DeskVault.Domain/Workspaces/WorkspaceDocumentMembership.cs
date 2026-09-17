namespace DeskVault.Domain.Workspaces;

public sealed class WorkspaceDocumentMembership
{
    public Guid DocumentId { get; }

    public int Order { get; }

    private WorkspaceDocumentMembership(Guid documentId, int order)
    {
        DocumentId = documentId;
        Order = order;
    }

    public static WorkspaceDocumentMembership Create(Guid documentId, int order)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                "Membership order cannot be negative.");
        }

        return new WorkspaceDocumentMembership(documentId, order);
    }
}

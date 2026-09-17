namespace DeskVault.Domain.Workspaces;

public sealed class Workspace
{
    private readonly List<WorkspaceDocumentMembership> _memberships = [];

    public Guid Id { get; }

    public string? Name { get; private set; }

    public WorkspaceType TypeOfWorkspace { get; }

    public IReadOnlyCollection<WorkspaceDocumentMembership> Memberships =>
        _memberships.AsReadOnly();

    public Guid? LastActiveDocumentId { get; private set; }

    private Workspace(
        Guid id,
        string? name,
        WorkspaceType type,
        IEnumerable<WorkspaceDocumentMembership> memberships,
        Guid? lastActiveDocumentId)
    {
        Id = id;
        Name = name;
        TypeOfWorkspace = type;
        _memberships.AddRange(memberships);
        LastActiveDocumentId = lastActiveDocumentId;
    }

    public static Workspace CreateTemporary(Guid id)
    {
        ValidateId(id);

        return new Workspace(
            id,
            null,
            WorkspaceType.Temporary,
            [],
            null);
    }

    public static Workspace CreatePersistent(Guid id, string name)
    {
        ValidateId(id);
        ValidatePersistentName(name);

        return new Workspace(
            id,
            name,
            WorkspaceType.Persistent,
            [],
            null);
    }

    public static Workspace Restore(
        Guid id,
        string? name,
        WorkspaceType type,
        IEnumerable<WorkspaceDocumentMembership> memberships,
        Guid? lastActiveDocumentId)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(memberships);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Workspace type is invalid.");
        }

        if (type == WorkspaceType.Persistent)
        {
            ValidatePersistentName(name);
        }

        var restoredMemberships = memberships.ToList();

        if (restoredMemberships
            .GroupBy(membership => membership.DocumentId)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "A workspace cannot contain the same document more than once.",
                nameof(memberships));
        }

        if (restoredMemberships
            .GroupBy(membership => membership.Order)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Workspace membership order must be unique.",
                nameof(memberships));
        }

        if (lastActiveDocumentId.HasValue &&
            restoredMemberships.All(
                membership =>
                    membership.DocumentId != lastActiveDocumentId.Value))
        {
            throw new ArgumentException(
                "Last active document must be a workspace member.",
                nameof(lastActiveDocumentId));
        }

        return new Workspace(
            id,
            name,
            type,
            restoredMemberships,
            lastActiveDocumentId);
    }

    public void AddDocument(Guid documentId)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        if (_memberships.Any(
                membership => membership.DocumentId == documentId))
        {
            throw new InvalidOperationException(
                "The document is already a member of this workspace.");
        }

        var nextOrder = _memberships.Count == 0
            ? 0
            : _memberships.Max(membership => membership.Order) + 1;

        _memberships.Add(
            WorkspaceDocumentMembership.Create(
                documentId,
                nextOrder));
    }

    public void RemoveDocument(Guid documentId)
    {
        var membership = _memberships.FirstOrDefault(
            existingMembership =>
                existingMembership.DocumentId == documentId);

        if (membership is null)
        {
            throw new InvalidOperationException(
                "The document is not a member of this workspace.");
        }

        _memberships.Remove(membership);

        if (LastActiveDocumentId == documentId)
        {
            LastActiveDocumentId = null;
        }
    }

    public void SetLastActiveDocument(Guid? documentId)
    {
        if (documentId.HasValue &&
            !_memberships.Any(
                membership =>
                    membership.DocumentId == documentId.Value))
        {
            throw new InvalidOperationException(
                "Last active document must be a workspace member.");
        }

        LastActiveDocumentId = documentId;
    }

    public void Rename(string name)
    {
        if (TypeOfWorkspace != WorkspaceType.Persistent)
        {
            throw new InvalidOperationException(
                "Only persistent workspaces can be renamed.");
        }

        ValidatePersistentName(name);
        Name = name;
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Workspace ID cannot be empty.",
                nameof(id));
        }
    }

    private static void ValidatePersistentName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Persistent workspace name is required.",
                nameof(name));
        }
    }
}

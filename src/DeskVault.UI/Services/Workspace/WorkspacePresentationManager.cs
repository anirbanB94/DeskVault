using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspacePresentationManager :
    IWorkspacePresentationManager,
    IDisposable
{
    private readonly IWorkspacePresentationFactory _factory;
    private readonly Dictionary<Guid, WorkspacePresentation> _presentations = [];
    private bool _disposed;

    public WorkspacePresentationManager(
        IWorkspacePresentationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    public WorkspacePresentation? Get(
        Guid workspaceId)
    {
        ThrowIfDisposed();

        return _presentations.TryGetValue(
            workspaceId,
            out WorkspacePresentation? presentation)
            ? presentation
            : null;
    }

    public WorkspacePresentation? FindTemporaryByDocument(
        Guid documentId)
    {
        ThrowIfDisposed();

        return _presentations.Values.FirstOrDefault(
            presentation =>
                presentation.IsTemporary &&
                presentation.IsWorkspaceDocument(documentId));
    }

    public WorkspacePresentation GetOrCreate(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description)
    {
        ThrowIfDisposed();

        if (_presentations.TryGetValue(
                workspaceId,
                out WorkspacePresentation? existing))
        {
            existing.Activate();

            return existing;
        }

        WorkspacePresentation presentation =
            _factory.Create(
                workspaceId,
                workspaceType,
                workspaceName,
                description);

        _presentations.Add(
            workspaceId,
            presentation);

        presentation.Activate();

        return presentation;
    }

    public bool Contains(
        Guid workspaceId)
    {
        ThrowIfDisposed();

        return _presentations.ContainsKey(
            workspaceId);
    }

    public bool Activate(
        Guid workspaceId)
    {
        ThrowIfDisposed();

        if (!_presentations.TryGetValue(
                workspaceId,
                out WorkspacePresentation? presentation))
        {
            return false;
        }

        presentation.Activate();

        return true;
    }

    public bool Close(
        Guid workspaceId)
    {
        ThrowIfDisposed();

        if (!_presentations.Remove(
                workspaceId,
                out WorkspacePresentation? presentation))
        {
            return false;
        }

        presentation.Dispose();

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (WorkspacePresentation presentation
            in _presentations.Values)
        {
            presentation.Dispose();
        }

        _presentations.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}

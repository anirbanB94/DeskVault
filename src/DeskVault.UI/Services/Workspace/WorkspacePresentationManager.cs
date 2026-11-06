using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspacePresentationManager :
    IWorkspacePresentationManager,
    IDisposable
{
    private readonly IWorkspacePresentationFactory _factory;
    private readonly Dictionary<Guid, WorkspacePresentation> _presentations = [];
    private readonly IApplicationSecuritySession _applicationSecuritySession;
    private bool _disposed;

    public WorkspacePresentationManager(
        IWorkspacePresentationFactory factory,
        IApplicationSecuritySession applicationSecuritySession)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(applicationSecuritySession);

        _factory = factory;
        _applicationSecuritySession = applicationSecuritySession;
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

        ApplicationSecuritySessionSnapshot snapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!snapshot.IsProtectedAccessAvailable)
        {
            return null;
        }

        return _presentations.Values.FirstOrDefault(
            presentation =>
                presentation.IsTemporary &&
                presentation.SecuritySessionRevision == snapshot.Revision &&
                presentation.IsWorkspaceDocument(documentId));
    }

    public WorkspacePresentation GetOrCreate(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description,
        long expectedSessionRevision)
    {
        ThrowIfDisposed();

        EnsureSessionRevisionCurrent(expectedSessionRevision);

        if (_presentations.TryGetValue(
                workspaceId,
                out WorkspacePresentation? existing))
        {
            if (existing.SecuritySessionRevision ==
                expectedSessionRevision)
            {
                // Activating a workspace can return false when it has no active
                // document. That does not mean the owning security session is stale.
                existing.Activate();

                if (!IsSessionRevisionCurrent(expectedSessionRevision))
                {
                    throw new OperationCanceledException(
                        "The workspace presentation session was invalidated.");
                }

                return existing;
            }

            // Do not reuse a presentation created by an older session.
            _presentations.Remove(workspaceId);
            existing.Dispose();

            EnsureSessionRevisionCurrent(expectedSessionRevision);
        }

        WorkspacePresentation presentation =
            _factory.Create(
                workspaceId,
                workspaceType,
                workspaceName,
                description,
                expectedSessionRevision);

        if (presentation.SecuritySessionRevision != expectedSessionRevision ||
            !IsSessionRevisionCurrent(expectedSessionRevision))
        {
            presentation.Dispose();

            throw new OperationCanceledException(
                "The workspace creation session was invalidated.");
        }

        _presentations.Add(
            workspaceId,
            presentation);

        try
        {
            // A workspace with no active document is still a valid presentation.
            // Validate the session revision separately from the activation result.
            presentation.Activate();

            if (!IsSessionRevisionCurrent(expectedSessionRevision))
            {
                throw new OperationCanceledException(
                    "The workspace activation session was invalidated.");
            }

            return presentation;
        }
        catch
        {
            _presentations.Remove(workspaceId);
            presentation.Dispose();

            throw;
        }
    }

    public bool Contains(
        Guid workspaceId)
    {
        ThrowIfDisposed();

        return _presentations.ContainsKey(
            workspaceId);
    }

    public bool Activate(Guid workspaceId)
    {
        ThrowIfDisposed();

        if (!_presentations.TryGetValue(
                workspaceId,
                out WorkspacePresentation? presentation))
        {
            return false;
        }

        if (!IsSessionRevisionCurrent(
                presentation.SecuritySessionRevision))
        {
            return false;
        }

        // The workspace view can be activated even when there is no active
        // document. The manager reports whether the owning session remained valid.
        presentation.Activate();

        return IsSessionRevisionCurrent(
            presentation.SecuritySessionRevision);
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

    public void CloseAll()
    {
        ThrowIfDisposed();

        DisposeManagedPresentations();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        DisposeManagedPresentations();
    }

    private void DisposeManagedPresentations()
    {
        WorkspacePresentation[] presentations =
            _presentations.Values.ToArray();

        // Remove all managed presentations from the registry before cleanup.
        _presentations.Clear();

        Exception? disposalException = null;

        // Attempt to dispose every presentation, even when one fails.
        foreach (WorkspacePresentation presentation in presentations)
        {
            try
            {
                presentation.Dispose();
            }
            catch (Exception exception)
            {
                disposalException ??= exception;
            }
        }

        // Preserve the first failure after all cleanup attempts.
        if (disposalException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(disposalException)
                .Throw();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private bool IsSessionRevisionCurrent(
        long expectedSessionRevision)
    {
        ApplicationSecuritySessionSnapshot snapshot =
            _applicationSecuritySession.CurrentSnapshot;

        return snapshot.IsProtectedAccessAvailable &&
               snapshot.Revision == expectedSessionRevision;
    }

    private void EnsureSessionRevisionCurrent(
        long expectedSessionRevision)
    {
        if (!IsSessionRevisionCurrent(expectedSessionRevision))
        {
            throw new OperationCanceledException(
                "Workspace presentation is not authorized for the expected session revision.");
        }
    }
}

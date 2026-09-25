using DeskVault.UI.Presenters;
using DeskVault.UI.Views;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspaceDocumentPresentation : IDisposable
{
    private readonly IDocumentWorkspaceView _view;
    private bool _disposed;

    public WorkspaceDocumentPresentation(
        Guid documentId,
        DocumentWorkspacePresenter presenter,
        IDocumentWorkspaceView view)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(view);

        DocumentId = documentId;
        Presenter = presenter;
        _view = view;
    }

    public Guid DocumentId { get; }

    public DocumentWorkspacePresenter Presenter { get; }

    public string? DisplayName => Presenter.CurrentDocumentDisplayName;

    public string? FileName => Presenter.CurrentDocumentFileName;

    public IDocumentWorkspaceView View => _view;

    public void Activate()
    {
        ThrowIfDisposed();

        Presenter.Activate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Presenter.Dispose();

        if (_view is IDisposable disposableView)
        {
            disposableView.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}

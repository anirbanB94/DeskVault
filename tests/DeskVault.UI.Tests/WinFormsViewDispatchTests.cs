using DeskVault.UI.Forms;
using DeskVault.UI.Rendering;
using DeskVault.UI.Services.Interfaces;
using Moq;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies UI-thread dispatch and lifetime guards for concrete WinForms presentation views.
/// </summary>
public sealed class WinFormsViewDispatchTests
{
    /// <summary>
    /// Verifies that document-view callbacks are dispatched through the captured
    /// synchronization context when the form handle has not been created.
    /// </summary>
    [Fact]
    public void DocumentViewForm_PostToUi_WhenHandleIsNotCreated_DispatchesThroughCapturedSynchronizationContext()
    {
        // Arrange, Act and Assert
        AssertDispatchesThroughCapturedContextBeforeHandleCreation(
            CreateDocumentViewForm,
            static (form, action) => form.PostToUi(action));
    }

    /// <summary>
    /// Verifies that document-view callbacks are ignored after the form has been disposed.
    /// </summary>
    [Fact]
    public void DocumentViewForm_PostToUi_WhenDisposed_DoesNotInvokeCallback()
    {
        // Arrange, Act and Assert
        AssertIgnoresCallbackAfterDisposal(
            CreateDocumentViewForm,
            static (form, action) => form.PostToUi(action));
    }

    /// <summary>
    /// Verifies that document-view callbacks are ignored while the form is disposing.
    /// </summary>
    [Fact]
    public void DocumentViewForm_PostToUi_WhenDisposing_DoesNotInvokeCallback()
    {
        // Arrange, Act and Assert
        AssertIgnoresCallbackDuringDisposal(
            CreateDocumentViewForm,
            static (form, action) => form.PostToUi(action));
    }

    /// <summary>
    /// Verifies that workspace-view callbacks are dispatched through the captured
    /// synchronization context when the form handle has not been created.
    /// </summary>
    [Fact]
    public void WorkspaceViewForm_PostToUi_WhenHandleIsNotCreated_DispatchesThroughCapturedSynchronizationContext()
    {
        // Arrange, Act and Assert
        AssertDispatchesThroughCapturedContextBeforeHandleCreation(
            CreateWorkspaceViewForm,
            static (form, action) => form.PostToUi(action));
    }

    /// <summary>
    /// Verifies that workspace-view callbacks are ignored after the form has been disposed.
    /// </summary>
    [Fact]
    public void WorkspaceViewForm_PostToUi_WhenDisposed_DoesNotInvokeCallback()
    {
        // Arrange, Act and Assert
        AssertIgnoresCallbackAfterDisposal(
            CreateWorkspaceViewForm,
            static (form, action) => form.PostToUi(action));
    }

    /// <summary>
    /// Verifies that workspace-view callbacks are ignored while the form is disposing.
    /// </summary>
    [Fact]
    public void WorkspaceViewForm_PostToUi_WhenDisposing_DoesNotInvokeCallback()
    {
        // Arrange, Act and Assert
        AssertIgnoresCallbackDuringDisposal(
            CreateWorkspaceViewForm,
            static (form, action) => form.PostToUi(action));
    }

    /// <summary>
    /// Creates a document view with its required presentation dependencies.
    /// </summary>
    private static DocumentViewForm CreateDocumentViewForm()
    {
        return new DocumentViewForm(
            new Mock<IDocumentContentRendererResolver>().Object,
            new Mock<ISessionBoundDialogCoordinator>().Object);
    }

    /// <summary>
    /// Creates a workspace view.
    /// </summary>
    private static WorkspaceViewForm CreateWorkspaceViewForm()
    {
        return new WorkspaceViewForm();
    }

    /// <summary>
    /// Verifies that a callback posted from another thread is delivered through
    /// the synchronization context captured before the form handle is created.
    /// </summary>
    private static void AssertDispatchesThroughCapturedContextBeforeHandleCreation<TForm>(
        Func<TForm> createForm,
        Action<TForm, Action> postToUi)
        where TForm : Form
    {
        RunOnStaThread(() =>
        {
            // Arrange
            SynchronizationContext? originalContext =
                SynchronizationContext.Current;

            var uiSynchronizationContext =
                new QueuedSynchronizationContext();

            SynchronizationContext.SetSynchronizationContext(
                uiSynchronizationContext);

            try
            {
                using TForm form = createForm();

                int uiThreadId =
                    Environment.CurrentManagedThreadId;

                Assert.False(form.IsHandleCreated);

                bool callbackInvoked = false;
                int callbackThreadId = -1;

                // Act
                Task.Run(() =>
                    postToUi(
                        form,
                        () =>
                        {
                            callbackInvoked = true;
                            callbackThreadId =
                                Environment.CurrentManagedThreadId;
                        }))
                    .GetAwaiter()
                    .GetResult();

                // Assert
                Assert.False(callbackInvoked);
                Assert.False(form.IsHandleCreated);

                Assert.Equal(
                    1,
                    uiSynchronizationContext.PendingCount);

                uiSynchronizationContext.DispatchNext();

                Assert.True(callbackInvoked);

                Assert.Equal(
                    uiThreadId,
                    callbackThreadId);

                Assert.Equal(
                    0,
                    uiSynchronizationContext.PendingCount);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(
                    originalContext);
            }
        });
    }

    /// <summary>
    /// Verifies that posting a callback after disposal does not invoke it.
    /// </summary>
    private static void AssertIgnoresCallbackAfterDisposal<TForm>(
        Func<TForm> createForm,
        Action<TForm, Action> postToUi)
        where TForm : Form
    {
        RunOnStaThread(() =>
        {
            // Arrange
            using TForm form = createForm();

            form.Dispose();

            bool callbackInvoked = false;

            // Act
            postToUi(
                form,
                () => callbackInvoked = true);

            // Assert
            Assert.False(callbackInvoked);
        });
    }

    /// <summary>
    /// Verifies that posting a callback while the form is disposing does not invoke it.
    /// </summary>
    private static void AssertIgnoresCallbackDuringDisposal<TForm>(
        Func<TForm> createForm,
        Action<TForm, Action> postToUi)
        where TForm : Form
    {
        RunOnStaThread(() =>
        {
            // Arrange
            using TForm form = createForm();

            bool disposalCallbackObserved = false;
            bool callbackInvoked = false;

            form.Controls.Add(
                new DisposalProbeControl(
                    () =>
                    {
                        Assert.True(form.Disposing);

                        disposalCallbackObserved = true;

                        postToUi(
                            form,
                            () => callbackInvoked = true);
                    }));

            // Act
            form.Dispose();

            // Assert
            Assert.True(disposalCallbackObserved);
            Assert.False(callbackInvoked);
        });
    }

    /// <summary>
    /// Executes the supplied action on a dedicated STA thread and propagates failures.
    /// </summary>
    private static void RunOnStaThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(failure)
                .Throw();
        }
    }

    /// <summary>
    /// Provides deterministic callback queuing for synchronization-context dispatch tests.
    /// </summary>
    private sealed class QueuedSynchronizationContext :
        SynchronizationContext
    {
        private readonly ConcurrentQueue<
            (SendOrPostCallback Callback, object? State)> _pending = new();

        public int PendingCount => _pending.Count;

        public override void Post(
            SendOrPostCallback callback,
            object? state)
        {
            ArgumentNullException.ThrowIfNull(callback);

            _pending.Enqueue((callback, state));
        }

        /// <summary>
        /// Executes the next queued callback on the calling thread.
        /// </summary>
        public void DispatchNext()
        {
            if (!_pending.TryDequeue(out var queuedCallback))
            {
                throw new InvalidOperationException(
                    "No synchronization-context callback is pending.");
            }

            queuedCallback.Callback(
                queuedCallback.State);
        }
    }

    /// <summary>
    /// Invokes a callback while its owning form is disposing its child controls.
    /// </summary>
    private sealed class DisposalProbeControl : Control
    {
        private readonly Action _onDispose;

        public DisposalProbeControl(Action onDispose)
        {
            ArgumentNullException.ThrowIfNull(onDispose);

            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _onDispose();
                }
                finally
                {
                    base.Dispose(disposing);
                }

                return;
            }

            base.Dispose(disposing);
        }
    }
}

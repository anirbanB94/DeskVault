using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Hosting;
using DeskVault.UI.Services.Dialogs;
using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class SessionBoundDialogCoordinatorTests
{
    [Fact]
    public void ShowDialog_WhenSessionIsNotAuthorized_DoesNotDisplayDialog()
    {
        RunOnStaThread(() =>
        {
            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Locked,
                    revision: 4);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 4);

            Assert.Equal(DialogResult.Cancel, result);
            Assert.False(dialog.Visible);

            securitySession.VerifyAdd(
                x => x.StateChanged += It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Never);
        });
    }

    [Fact]
    public void ShowDialog_WhenExpectedRevisionIsStale_DoesNotDisplayDialog()
    {
        RunOnStaThread(() =>
        {
            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 12);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 11);

            Assert.Equal(DialogResult.Cancel, result);
            Assert.False(dialog.Visible);

            securitySession.VerifyAdd(
                x => x.StateChanged += It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Never);
        });
    }

    [Fact]
    public void ShowDialog_WhenSessionLocksWhileDialogIsOpen_ClosesDialogWithCancelOnUiThread()
    {
        RunOnStaThread(() =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            int closedThreadId = 0;

            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 20);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();

            Task? transitionTask = null;

            dialog.FormClosed += (_, _) =>
                closedThreadId = Environment.CurrentManagedThreadId;

            dialog.Shown += (_, _) =>
            {
                transitionTask = Task.Run(() =>
                {
                    currentSnapshot = new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        revision: 21);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        currentSnapshot);
                });
            };

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 20);

            transitionTask?.GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, result);
            Assert.Equal(uiThreadId, closedThreadId);
            Assert.False(dialog.Visible);

            securitySession.VerifyAdd(
                x => x.StateChanged += It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);

            securitySession.VerifyRemove(
                x => x.StateChanged -= It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);
        });
    }

    [Fact]
    public void ShowDialog_WhenLockAndReauthenticationOccurBeforeUiDispatch_StillClosesOldDialog()
    {
        RunOnStaThread(() =>
        {
            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 30);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();

            Task? transitionTask = null;

            dialog.Shown += (_, _) =>
            {
                transitionTask = Task.Run(() =>
                {
                    currentSnapshot = new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        revision: 31);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        currentSnapshot);

                    currentSnapshot = new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Authenticated,
                        revision: 32);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        currentSnapshot);
                });
            };

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 30);

            transitionTask?.GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, result);
            Assert.False(dialog.Visible);
            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                currentSnapshot.State);
            Assert.Equal(32, currentSnapshot.Revision);
        });
    }

    [Fact]
    public void ShowDialog_WhenOlderNotificationArrivesAfterAuthoritativeLock_StillClosesDialog()
    {
        RunOnStaThread(() =>
        {
            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 35);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();
            Task? notificationTask = null;

            dialog.Shown += (_, _) =>
            {
                notificationTask = Task.Run(() =>
                {
                    // Simulate a newer authoritative lock snapshot while an
                    // older queued notification is the next one delivered.
                    currentSnapshot = new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        revision: 37);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        new ApplicationSecuritySessionSnapshot(
                            ApplicationSecuritySessionState.Authenticated,
                            revision: 34));
                });
            };

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 35);

            notificationTask?.GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, result);
            Assert.False(dialog.Visible);
        });
    }

    [Fact]
    public void ShowDialog_WhenRepeatedLockNotificationsArrive_ClosesDialogOnlyOnce()
    {
        RunOnStaThread(() =>
        {
            int closedCount = 0;

            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 40);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();

            Task? transitionTask = null;

            dialog.FormClosed += (_, _) =>
                closedCount++;

            dialog.Shown += (_, _) =>
            {
                transitionTask = Task.Run(() =>
                {
                    currentSnapshot = new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        revision: 41);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        currentSnapshot);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        currentSnapshot);
                });
            };

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 40);

            transitionTask?.GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, result);
            Assert.Equal(1, closedCount);
        });
    }

    [Fact]
    public void ShowDialog_WhenMultipleDialogsAreActive_ClosesAllDialogsNewestFirst()
    {
        RunOnStaThread(() =>
        {
            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 50);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var outerDialog = new Form();
            using var innerDialog = new Form();

            Task? transitionTask = null;
            DialogResult innerResult = DialogResult.None;
            int outerClosedCount = 0;
            int innerClosedCount = 0;

            outerDialog.FormClosed += (_, _) =>
                outerClosedCount++;

            innerDialog.FormClosed += (_, _) =>
                innerClosedCount++;

            innerDialog.Shown += (_, _) =>
            {
                transitionTask = Task.Run(() =>
                {
                    currentSnapshot = new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        revision: 51);

                    securitySession.Raise(
                        x => x.StateChanged += null,
                        currentSnapshot);
                });
            };

            // Showing the nested dialog registers a second protected modal
            // surface while the outer dialog remains active.
            outerDialog.Shown += (_, _) =>
                innerResult = coordinator.ShowDialog(
                    innerDialog,
                    expectedSessionRevision: 50);

            DialogResult outerResult = coordinator.ShowDialog(
                outerDialog,
                expectedSessionRevision: 50);

            transitionTask?.GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, innerResult);
            Assert.Equal(DialogResult.Cancel, outerResult);
            Assert.Equal(1, innerClosedCount);
            Assert.Equal(1, outerClosedCount);

            securitySession.VerifyAdd(
                x => x.StateChanged += It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);

            securitySession.VerifyRemove(
                x => x.StateChanged -= It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);
        });
    }

    [Fact]
    public void ShowDialog_WhenUserCompletesNormally_ReturnsResultAndUnsubscribes()
    {
        RunOnStaThread(() =>
        {
            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 60);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();
            dialog.Shown += (_, _) =>
                dialog.DialogResult = DialogResult.OK;

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 60);

            Assert.Equal(DialogResult.OK, result);

            securitySession.VerifyAdd(
                x => x.StateChanged += It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);

            securitySession.VerifyRemove(
                x => x.StateChanged -= It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);
        });
    }

    [Fact]
    public void Dispose_WhenDialogIsActive_UnsubscribesAndClosesOnUiThread()
    {
        RunOnStaThread(() =>
        {
            int uiThreadId = Environment.CurrentManagedThreadId;
            int closedThreadId = 0;

            ApplicationSecuritySessionSnapshot currentSnapshot =
                new(
                    ApplicationSecuritySessionState.Authenticated,
                    revision: 70);

            Mock<IApplicationSecuritySession> securitySession =
                CreateSecuritySession(() => currentSnapshot);

            using var coordinator = new SessionBoundDialogCoordinator(
                securitySession.Object,
                NullLogger<SessionBoundDialogCoordinator>.Instance);

            using var dialog = new Form();
            Task? disposalTask = null;

            dialog.FormClosed += (_, _) =>
                closedThreadId = Environment.CurrentManagedThreadId;

            dialog.Shown += (_, _) =>
                disposalTask = Task.Run(coordinator.Dispose);

            DialogResult result = coordinator.ShowDialog(
                dialog,
                expectedSessionRevision: 70);

            disposalTask?.GetAwaiter().GetResult();

            Assert.Equal(DialogResult.Cancel, result);
            Assert.Equal(uiThreadId, closedThreadId);
            Assert.False(dialog.Visible);

            securitySession.VerifyAdd(
                x => x.StateChanged += It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);

            securitySession.VerifyRemove(
                x => x.StateChanged -= It.IsAny<Action<ApplicationSecuritySessionSnapshot>>(),
                Times.Once);
        });
    }

    [Fact]
    public void HostConfigurator_RegistersSessionBoundDialogCoordinatorAsSingleton()
    {
        RunOnStaThread(() =>
        {
            using IHost host = HostConfigurator.Build();

            ISessionBoundDialogCoordinator first =
                host.Services.GetRequiredService<ISessionBoundDialogCoordinator>();

            ISessionBoundDialogCoordinator second =
                host.Services.GetRequiredService<ISessionBoundDialogCoordinator>();

            Assert.Same(first, second);
            Assert.IsType<SessionBoundDialogCoordinator>(first);
        });
    }

    private static Mock<IApplicationSecuritySession> CreateSecuritySession(
        Func<ApplicationSecuritySessionSnapshot> getCurrentSnapshot)
    {
        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(getCurrentSnapshot);

        return securitySession;
    }

    private static void RunOnStaThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? failure = null;

        var thread = new Thread(() =>
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
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

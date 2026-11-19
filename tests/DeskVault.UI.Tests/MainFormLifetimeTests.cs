using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Timer = System.Windows.Forms.Timer;
using WinForms = System.Windows.Forms;

namespace DeskVault.UI.Tests;

public sealed class MainFormLifetimeTests
{
    [Fact]
    public void Dispose_WhenCalledDirectly_ReleasesOwnedResources()
    {
        RunOnStaThread(
            () =>
            {
                // Arrange
                using IHost host =
                    HostConfigurator.Build();

                MainForm form =
                    host.Services.GetRequiredService<MainForm>();

                MainFormPresenter presenter =
                    GetPrivateField<MainFormPresenter>(
                        form,
                        "_presenter");

                WinFormsUserActivityMessageFilter activityFilter =
                    GetPrivateField<WinFormsUserActivityMessageFilter>(
                        form,
                        "_userActivityMessageFilter");

                Timer debounceTimer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                activityFilter.Start();
                debounceTimer.Start();

                // Act
                form.Dispose();

                // Repeat disposal to verify idempotence.
                form.Dispose();

                // Assert
                Assert.True(
                    form.IsDisposed);

                Assert.Equal(
                    1,
                    GetPrivateField<int>(form,
                    "_ownedResourcesDisposed"));

                Assert.True(
                    GetPrivateField<bool>(
                        presenter,
                        "_disposed"));

                Assert.False(
                    debounceTimer.Enabled);

                Assert.Throws<ObjectDisposedException>(
                    () => activityFilter.Start());
            });
    }

    [Fact]
    public void Close_WhenFormHasBeenShown_ReleasesOwnedResourcesOnce()
    {
        RunOnStaThread(
            () =>
            {
                // Arrange
                using IHost host =
                    HostConfigurator.Build();

                MainForm form =
                    host.Services.GetRequiredService<MainForm>();

                MainFormPresenter presenter =
                    GetPrivateField<MainFormPresenter>(
                        form,
                        "_presenter");

                WinFormsUserActivityMessageFilter activityFilter =
                    GetPrivateField<WinFormsUserActivityMessageFilter>(
                        form,
                        "_userActivityMessageFilter");

                Timer debounceTimer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                form.Show();

                WinForms.Application.DoEvents();

                // Ensure the timer is active when the Form closes.
                debounceTimer.Start();

                // Act
                form.Close();

                WinForms.Application.DoEvents();

                // Repeat disposal after normal close. FormClosed and
                // Disposed must not cause duplicate resource cleanup.
                form.Dispose();

                // Assert
                Assert.True(
                    form.IsDisposed);

                Assert.Equal(
                    1,
                    GetPrivateField<int>(form,
                    "_ownedResourcesDisposed"));

                Assert.True(
                    GetPrivateField<bool>(
                        presenter,
                        "_disposed"));

                Assert.False(
                    debounceTimer.Enabled);

                Assert.Throws<ObjectDisposedException>(
                    () => activityFilter.Start());
            });
    }

    [Fact]
    public void Dispose_WhenPresenterDisposalThrows_StillDisposesMessageFilterAndTimerAndPreservesFailure()
    {
        // Arrange
        const string expectedFailure =
            "Workspace presentation cleanup failed.";

        var workspacePresentationManager =
            new Mock<DeskVault.UI.Services.Interfaces.IWorkspacePresentationManager>();

        workspacePresentationManager
            .Setup(x => x.CloseAll())
            .Throws(new InvalidOperationException(expectedFailure));

        MainForm form =
            MainFormDialogTests.CreateMainForm(
                new Mock<DeskVault.UI.Services.Interfaces.ISessionBoundDialogCoordinator>().Object,
                workspacePresentationManager:
                    workspacePresentationManager.Object);

        WinFormsUserActivityMessageFilter messageFilter =
            GetPrivateField<WinFormsUserActivityMessageFilter>(
                form,
                "_userActivityMessageFilter");

        System.Windows.Forms.Timer timer =
            GetPrivateField<System.Windows.Forms.Timer>(
                form,
                "_searchDebounceTimer");

        bool timerDisposed = false;

        timer.Disposed += (_, _) =>
            timerDisposed = true;

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => form.Dispose());

        // Assert
        Assert.Equal(expectedFailure, exception.Message);

        Assert.Equal(
            1,
            GetPrivateField<int>(
                form,
                "_ownedResourcesDisposed"));

        Assert.True(
            GetPrivateField<bool>(
                messageFilter,
                "_disposed"));

        Assert.True(timerDisposed);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Once);
    }

    private static T GetPrivateField<T>(
        object instance,
        string fieldName)
    {
        FieldInfo field =
            instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Field '{fieldName}' was not found.");

        object? value =
            field.GetValue(instance);

        return value is T typedValue
            ? typedValue
            : throw new InvalidOperationException(
                $"Field '{fieldName}' was not of type '{typeof(T).Name}'.");
    }

    private static void RunOnStaThread(
        Action action)
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

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(failure)
                .Throw();
        }
    }
}

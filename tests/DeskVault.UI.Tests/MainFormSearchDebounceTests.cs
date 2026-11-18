using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DeskVault.UI.Forms;
using DeskVault.UI.Services.Interfaces;
using Moq;
using Timer = System.Windows.Forms.Timer;

namespace DeskVault.UI.Tests;

public sealed class MainFormSearchDebounceTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void SearchTextChanged_RapidEditsDebounceToOneRequestForLatestText()
    {
        RunOnStaThread(() =>
        {
            using MainForm form =
                MainFormDialogTests.CreateMainForm(
                    Mock.Of<ISessionBoundDialogCoordinator>());

            try
            {
                TextBox searchTextBox =
                    GetPrivateField<TextBox>(
                        form,
                        "searchTextBox");

                Timer timer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                List<string> requestedSearchTexts = [];

                form.SearchRequested += (_, _) =>
                    requestedSearchTexts.Add(form.SearchText);

                // Arrange and Act: rapid edits restart the same timer.
                searchTextBox.Text = "s";

                Assert.True(timer.Enabled);

                searchTextBox.Text = "se";

                Assert.True(timer.Enabled);

                searchTextBox.Text = "security";

                Assert.True(timer.Enabled);
                Assert.Equal(300, timer.Interval);
                Assert.Empty(requestedSearchTexts);

                // Act: allow the real WinForms timer to deliver its tick.
                PumpMessages(
                    TimeSpan.FromMilliseconds(700));

                // Assert: only the final text is submitted once.
                Assert.Equal(
                    ["security"],
                    requestedSearchTexts);

                Assert.False(timer.Enabled);
            }
            finally
            {
                form.Close();
            }
        });
    }

    [Fact]
    public void ClearSearch_WhenPopulated_RaisesOneImmediateRequestAndLeavesNoPendingTick()
    {
        RunOnStaThread(() =>
        {
            using MainForm form =
                MainFormDialogTests.CreateMainForm(
                    Mock.Of<ISessionBoundDialogCoordinator>());

            try
            {
                TextBox searchTextBox =
                    GetPrivateField<TextBox>(
                        form,
                        "searchTextBox");

                Timer timer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                List<string> requestedSearchTexts = [];

                form.SearchRequested += (_, _) =>
                    requestedSearchTexts.Add(form.SearchText);

                // Arrange: a normal edit has pending debounce work.
                searchTextBox.Text = "security";

                Assert.True(timer.Enabled);

                // Act: execute the actual clear-button handler.
                Button clearSearchButton =
                    GetPrivateField<Button>(
                        form,
                        "clearSearchButton");

                RaiseClickEvent(clearSearchButton);

                // Assert: clear is immediate and does not restart the timer.
                Assert.Equal(string.Empty, form.SearchText);

                Assert.Equal(
                    [string.Empty],
                    requestedSearchTexts);

                Assert.False(timer.Enabled);

                // Assert: no delayed duplicate arrives after the debounce
                // interval has elapsed and the WinForms message queue is pumped.
                PumpMessages(
                    TimeSpan.FromMilliseconds(700));

                Assert.Single(requestedSearchTexts);
                Assert.False(timer.Enabled);
            }
            finally
            {
                form.Close();
            }
        });
    }

    [Fact]
    public void ClearSearch_WhenAlreadyEmpty_RaisesOneImmediateRequestWithoutPendingTick()
    {
        RunOnStaThread(() =>
        {
            using MainForm form =
                MainFormDialogTests.CreateMainForm(
                    Mock.Of<ISessionBoundDialogCoordinator>());

            try
            {
                Timer timer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                List<string> requestedSearchTexts = [];

                form.SearchRequested += (_, _) =>
                    requestedSearchTexts.Add(form.SearchText);

                // Arrange: the initial search field is already empty.
                Assert.Equal(string.Empty, form.SearchText);
                Assert.False(timer.Enabled);

                // Act: execute the actual clear-button handler.
                InvokePrivateHandler(
                    form,
                    "OnClearSearchButtonClick",
                    EventArgs.Empty);

                // Assert: the explicit clear action still issues one request.
                Assert.Equal(
                    [string.Empty],
                    requestedSearchTexts);

                Assert.False(timer.Enabled);

                PumpMessages(
                    TimeSpan.FromMilliseconds(700));

                Assert.Single(requestedSearchTexts);
                Assert.False(timer.Enabled);
            }
            finally
            {
                form.Close();
            }
        });
    }

    [Fact]
    public void Enter_WhenDebounceIsPending_StopsTimerAndRaisesOneImmediateRequest()
    {
        RunOnStaThread(() =>
        {
            using MainForm form =
                MainFormDialogTests.CreateMainForm(
                    Mock.Of<ISessionBoundDialogCoordinator>());

            try
            {
                TextBox searchTextBox =
                    GetPrivateField<TextBox>(
                        form,
                        "searchTextBox");

                Timer timer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                List<string> requestedSearchTexts = [];

                form.SearchRequested += (_, _) =>
                    requestedSearchTexts.Add(form.SearchText);

                // Arrange: a normal edit has pending debounce work.
                searchTextBox.Text = "security";

                Assert.True(timer.Enabled);

                var keyEventArgs =
                    new KeyEventArgs(Keys.Enter);

                // Act: execute the actual Enter-key handler.
                InvokePrivateHandler(
                    form,
                    "OnSearchTextBoxKeyDown",
                    keyEventArgs);

                // Assert: Enter suppresses the keypress and submits once.
                Assert.True(keyEventArgs.SuppressKeyPress);

                Assert.Equal(
                    ["security"],
                    requestedSearchTexts);

                Assert.False(timer.Enabled);

                PumpMessages(
                    TimeSpan.FromMilliseconds(700));

                Assert.Single(requestedSearchTexts);
                Assert.False(timer.Enabled);
            }
            finally
            {
                form.Close();
            }
        });
    }

    [Fact]
    public void SearchButton_WhenDebounceIsPending_StopsTimerAndRaisesOneImmediateRequest()
    {
        RunOnStaThread(() =>
        {
            using MainForm form =
                MainFormDialogTests.CreateMainForm(
                    Mock.Of<ISessionBoundDialogCoordinator>());

            try
            {
                TextBox searchTextBox =
                    GetPrivateField<TextBox>(
                        form,
                        "searchTextBox");

                Timer timer =
                    GetPrivateField<Timer>(
                        form,
                        "_searchDebounceTimer");

                List<string> requestedSearchTexts = [];

                form.SearchRequested += (_, _) =>
                    requestedSearchTexts.Add(form.SearchText);

                // Arrange: a normal edit has pending debounce work.
                searchTextBox.Text = "security";

                Assert.True(timer.Enabled);

                // Act: execute the actual Search-button handler.
                Button searchButton =
                    GetPrivateField<Button>(
                        form,
                        "searchButton");

                RaiseClickEvent(searchButton);

                // Assert: the explicit request is immediate and unique.
                Assert.Equal(
                    ["security"],
                    requestedSearchTexts);

                Assert.False(timer.Enabled);

                PumpMessages(
                    TimeSpan.FromMilliseconds(700));

                Assert.Single(requestedSearchTexts);
                Assert.False(timer.Enabled);
            }
            finally
            {
                form.Close();
            }
        });
    }

    private static void RaiseClickEvent(
        Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        MethodInfo onClick =
            typeof(Control).GetMethod(
                "OnClick",
                PrivateInstance)
            ?? throw new InvalidOperationException(
                "Control.OnClick was not found.");

        // Raise the WinForms Click event without requiring the
        // parent Form to be displayed during this STA unit test.
        onClick.Invoke(
            control,
            [EventArgs.Empty]);
    }

    private static T GetPrivateField<T>(
        MainForm form,
        string fieldName)
        where T : class
    {
        FieldInfo field =
            typeof(MainForm).GetField(
                fieldName,
                PrivateInstance)
            ?? throw new InvalidOperationException(
                $"Field '{fieldName}' was not found.");

        return Assert.IsType<T>(
            field.GetValue(form));
    }

    private static void InvokePrivateHandler(
        MainForm form,
        string methodName,
        EventArgs eventArgs)
    {
        MethodInfo handler =
            typeof(MainForm).GetMethod(
                methodName,
                PrivateInstance)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' was not found.");

        handler.Invoke(
            form,
            [null, eventArgs]);
    }

    private static void PumpMessages(
        TimeSpan duration)
    {
        Stopwatch stopwatch =
            Stopwatch.StartNew();

        while (stopwatch.Elapsed < duration)
        {
            global::System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        global::System.Windows.Forms.Application.DoEvents();
    }

    private static void RunOnStaThread(
        Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? failure = null;

        var thread =
            new Thread(() =>
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

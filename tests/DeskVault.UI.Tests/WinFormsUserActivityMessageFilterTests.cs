using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Services.Security;

namespace DeskVault.UI.Tests;

public sealed class WinFormsUserActivityMessageFilterTests
{
    [Theory]
    [InlineData(0x0100)] // WM_KEYDOWN
    [InlineData(0x0104)] // WM_SYSKEYDOWN
    [InlineData(0x0290)] // WM_IME_KEYDOWN
    [InlineData(0x0201)] // WM_LBUTTONDOWN
    [InlineData(0x0204)] // WM_RBUTTONDOWN
    [InlineData(0x0207)] // WM_MBUTTONDOWN
    [InlineData(0x020B)] // WM_XBUTTONDOWN
    [InlineData(0x020A)] // WM_MOUSEWHEEL
    [InlineData(0x020E)] // WM_MOUSEHWHEEL
    [InlineData(0x0240)] // WM_TOUCH
    [InlineData(0x0246)] // WM_POINTERDOWN
    [InlineData(0x0119)] // WM_GESTURE
    public void PreFilterMessage_WhenDeliberateInputIsReceived_ReportsCurrentRevision(
        int messageId)
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 12);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();

        Message message = CreateMessage(messageId);

        // Act
        bool messageConsumed =
            filter.PreFilterMessage(ref message);

        // Assert
        Assert.False(messageConsumed);
        Assert.Equal([12L], activityService.ReportedRevisions);
    }

    [Theory]
    [InlineData(0x0200)] // WM_MOUSEMOVE
    [InlineData(0x0113)] // WM_TIMER
    [InlineData(0x000F)] // WM_PAINT
    [InlineData(0x0102)] // WM_CHAR (already represented by key-down)
    [InlineData(0x0202)] // WM_LBUTTONUP
    [InlineData(0x0101)] // WM_KEYUP
    public void PreFilterMessage_WhenMessageIsNotQualifying_DoesNotReportActivity(
        int messageId)
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 15);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();

        Message message = CreateMessage(messageId);

        // Act
        bool messageConsumed =
            filter.PreFilterMessage(ref message);

        // Assert
        Assert.False(messageConsumed);
        Assert.Empty(activityService.ReportedRevisions);
    }

    [Fact]
    public void PreFilterMessage_WhenSessionIsNotAuthenticated_DoesNotReportActivity()
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Locked,
            revision: 4);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();

        Message message = CreateMessage(0x0100);

        // Act
        filter.PreFilterMessage(ref message);

        // Assert
        Assert.Empty(activityService.ReportedRevisions);
    }

    [Fact]
    public void PreFilterMessage_AfterAuthenticationTransition_UsesNewRevision()
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 20);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();

        session.SetSnapshot(
            ApplicationSecuritySessionState.Locked,
            revision: 21);

        session.SetSnapshot(
            ApplicationSecuritySessionState.Authenticated,
            revision: 22);

        Message message = CreateMessage(0x0100);

        // Act
        filter.PreFilterMessage(ref message);

        // Assert
        Assert.Equal([22L], activityService.ReportedRevisions);
    }

    [Fact]
    public void PreFilterMessage_WhenStaleNotificationArrives_DoesNotRestoreOldRevision()
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 30);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();

        ApplicationSecuritySessionSnapshot staleSnapshot =
            new(
                ApplicationSecuritySessionState.Locked,
                revision: 31);

        session.SetSnapshot(
            ApplicationSecuritySessionState.Locked,
            revision: 31);

        session.SetSnapshot(
            ApplicationSecuritySessionState.Authenticated,
            revision: 32);

        // Deliver an older notification after the newer state is already
        // authoritative. The filter must reconcile against CurrentSnapshot.
        session.RaiseStateChanged(staleSnapshot);

        Message message = CreateMessage(0x0100);

        // Act
        filter.PreFilterMessage(ref message);

        // Assert
        Assert.Equal([32L], activityService.ReportedRevisions);
    }

    [Fact]
    public void PreFilterMessage_AfterStop_DoesNotReportActivity()
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 40);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();
        filter.Stop();

        Message message = CreateMessage(0x0100);

        // Act
        filter.PreFilterMessage(ref message);

        // Assert
        Assert.Empty(activityService.ReportedRevisions);
    }

    [Fact]
    public void Start_CalledRepeatedly_RegistersOneActiveFilter()
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 50);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        // Act
        filter.Start();
        filter.Start();

        Message message = CreateMessage(0x0100);

        filter.PreFilterMessage(ref message);

        // Assert
        Assert.Equal([50L], activityService.ReportedRevisions);
    }

    [Fact]
    public void PreFilterMessage_AfterDispose_DoesNotReportActivityAndCannotRestart()
    {
        // Arrange
        var session = new TestSecuritySession(
            ApplicationSecuritySessionState.Authenticated,
            revision: 60);

        var activityService = new TestUserActivityService();

        using var filter =
            new WinFormsUserActivityMessageFilter(
                session,
                activityService);

        filter.Start();

        // Act
        filter.Dispose();
        filter.Dispose();

        Message message = CreateMessage(0x0100);

        bool messageConsumed =
            filter.PreFilterMessage(ref message);

        // Assert
        Assert.False(messageConsumed);
        Assert.Empty(activityService.ReportedRevisions);

        Assert.Throws<ObjectDisposedException>(
            () => filter.Start());
    }
    private static Message CreateMessage(int messageId)
    {
        return Message.Create(
            IntPtr.Zero,
            messageId,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    private sealed class TestUserActivityService :
        IApplicationUserActivityService
    {
        public List<long> ReportedRevisions { get; } = [];

        public bool TryReportQualifyingActivity(
            long observedSessionRevision)
        {
            ReportedRevisions.Add(observedSessionRevision);
            return true;
        }
    }

    private sealed class TestSecuritySession :
        IApplicationSecuritySession
    {
        public TestSecuritySession(
            ApplicationSecuritySessionState state,
            long revision)
        {
            CurrentSnapshot =
                new ApplicationSecuritySessionSnapshot(
                    state,
                    revision);
        }

        public ApplicationSecuritySessionSnapshot CurrentSnapshot
        {
            get;
            private set;
        }

        public event Action<ApplicationSecuritySessionSnapshot>? StateChanged;

        public void SetSnapshot(
            ApplicationSecuritySessionState state,
            long revision)
        {
            CurrentSnapshot =
                new ApplicationSecuritySessionSnapshot(
                    state,
                    revision);

            StateChanged?.Invoke(CurrentSnapshot);
        }

        public void RaiseStateChanged(
            ApplicationSecuritySessionSnapshot snapshot)
        {
            StateChanged?.Invoke(snapshot);
        }

        public ApplicationSecuritySessionAttempt BeginAuthentication() =>
            throw new NotSupportedException();

        public bool TryCompleteAuthentication(
            ApplicationSecuritySessionAttempt attempt,
            ApplicationAuthenticationOutcome outcome) =>
            throw new NotSupportedException();

        public bool TryLock(
            long expectedRevision,
            out ApplicationSecuritySessionSnapshot snapshot)
        {
            snapshot = CurrentSnapshot;

            return false;
        }

        public ApplicationSecuritySessionSnapshot Lock() =>
            throw new NotSupportedException();

        public ApplicationSecuritySessionSnapshot MarkUnavailable() =>
            throw new NotSupportedException();

        public ApplicationSecuritySessionSnapshot RestoreAvailability() =>
            throw new NotSupportedException();
    }
}

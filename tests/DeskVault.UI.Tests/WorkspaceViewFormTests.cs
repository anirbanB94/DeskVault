using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

namespace DeskVault.UI.Tests;

public sealed class WorkspaceViewFormTests
{
    [Fact]
    public void SetWorkspaceIdentity_RendersPersistentIdentityAndWorkspaceActions()
    {
        // Arrange
        using var form = new WorkspaceViewForm();

        const string workspaceName = "Project Alpha";
        const string workspaceDescription = "Customer onboarding";

        // Act
        form.SetWorkspaceIdentity(
            workspaceName,
            workspaceDescription);

        // Assert
        Assert.Equal(
            workspaceName,
            GetControl<Label>(form, "workspaceTitleLabel").Text);

        Assert.Equal(
            workspaceDescription,
            GetControl<Label>(form, "workspaceMetadataLabel").Text);

        Assert.Equal(
            UiMessages.BackToWorkspaces,
            GetControl<Button>(form, "backButton").Text);

        Assert.Equal(
            [
                "documentInformationMenuItem",
            "addDocumentsMenuItem",
            "removeDocumentsMenuItem",
            "updateWorkspaceDetailsMenuItem",
            "deleteWorkspaceMenuItem",
            "workspaceMenuSeparator",
            "viewMenuItem",
            "viewMenuSeparator",
            "closeWorkspaceMenuItem"
            ],
            GetWorkspaceMenuItemNames(form));
    }

    [Fact]
    public void SetTemporaryDocumentPresentation_RendersDocumentIdentityAndTemporaryActions()
    {
        // Arrange
        using var form = new WorkspaceViewForm();

        const string documentFileName = "report.csv";

        // Act
        form.SetTemporaryDocumentPresentation(
            documentFileName);

        // Assert
        Assert.Equal(
            documentFileName,
            GetControl<Label>(form, "workspaceTitleLabel").Text);

        Assert.Equal(
            UiMessages.TemporaryWorkspace,
            GetControl<Label>(form, "workspaceMetadataLabel").Text);

        Assert.Equal(
            UiMessages.BackToDocuments,
            GetControl<Button>(form, "backButton").Text);

        Assert.Equal(
            [
                "documentInformationMenuItem",
            "updateWorkspaceDetailsMenuItem",
            "temporaryMenuSeparator",
            "removeDocumentsMenuItem",
            "closeWorkspaceMenuItem"
            ],
            GetWorkspaceMenuItemNames(form));
    }

    [Fact]
    public void PersistentWorkspace_NavigationMenu_ForwardsNavigationModeChanges()
    {
        // Arrange
        using var form = new WorkspaceViewForm();

        form.SetWorkspaceIdentity(
            "Project Alpha",
            null);

        List<WorkspacePresentationNavigationMode> observedModes = [];

        form.NavigationModeChanged +=
            (_, e) => observedModes.Add(e.NavigationMode);

        // Act
        GetMenuItem(form, "sidebarMenuItem").PerformClick();

        GetMenuItem(form, "tabsMenuItem").PerformClick();

        // Assert
        Assert.Equal(
            [
                WorkspacePresentationNavigationMode.Sidebar,
                WorkspacePresentationNavigationMode.Tabs
            ],
            observedModes);
    }

    [Fact]
    public void WorkspaceViewForm_WhenHiddenAndShownAgain_PreservesSidebarNavigationMode()
    {
        // Arrange
        using var form = new WorkspaceViewForm();

        form.SetWorkspaceIdentity(
            "Workspace",
            null);

        Panel tabsPanel =
            GetControl<Panel>(
                form,
                "documentTabsPanel");

        Panel sidebarPanel =
            GetControl<Panel>(
                form,
                "documentSidebarPanel");

        form.Show();

        form.SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        // Act
        form.Hide();
        form.Show();

        // Assert
        Assert.True(sidebarPanel.Visible);
        Assert.False(tabsPanel.Visible);
    }

    [Fact]
    public void PersistentWorkspace_MenuActions_ForwardWorkspaceRequests()
    {
        // Arrange
        using var form = new WorkspaceViewForm();

        form.SetWorkspaceIdentity(
            "Project Alpha",
            null);

        List<string> observedRequests = [];

        form.AddDocumentsRequested +=
            (_, _) => observedRequests.Add(nameof(form.AddDocumentsRequested));

        form.RemoveDocumentsRequested +=
            (_, _) => observedRequests.Add(nameof(form.RemoveDocumentsRequested));

        form.RemoveDocumentRequested +=
            (_, _) => observedRequests.Add(nameof(form.RemoveDocumentRequested));

        form.UpdateWorkspaceDetailsRequested +=
            (_, _) => observedRequests.Add(nameof(form.UpdateWorkspaceDetailsRequested));

        form.SaveAsWorkspaceRequested +=
            (_, _) => observedRequests.Add(nameof(form.SaveAsWorkspaceRequested));

        form.DeleteWorkspaceRequested +=
            (_, _) => observedRequests.Add(nameof(form.DeleteWorkspaceRequested));

        form.CloseWorkspaceRequested +=
            (_, _) => observedRequests.Add(nameof(form.CloseWorkspaceRequested));

        form.CloseDocumentRequested +=
            (_, _) => observedRequests.Add(nameof(form.CloseDocumentRequested));

        // Act
        GetMenuItem(form, "addDocumentsMenuItem").PerformClick();
        GetMenuItem(form, "removeDocumentsMenuItem").PerformClick();
        GetMenuItem(form, "updateWorkspaceDetailsMenuItem").PerformClick();
        GetMenuItem(form, "deleteWorkspaceMenuItem").PerformClick();
        GetMenuItem(form, "closeWorkspaceMenuItem").PerformClick();

        // Assert
        Assert.Equal(
            [
                nameof(form.AddDocumentsRequested),
                nameof(form.RemoveDocumentsRequested),
                nameof(form.UpdateWorkspaceDetailsRequested),
                nameof(form.DeleteWorkspaceRequested),
                nameof(form.CloseWorkspaceRequested)
            ],
            observedRequests);
    }

    [Fact]
    public void TemporaryWorkspace_MenuActions_ForwardTemporaryDocumentRequests()
    {
        // Arrange
        using var form = new WorkspaceViewForm();

        form.SetTemporaryDocumentPresentation(
            "report.csv");

        List<string> observedRequests = [];

        form.AddDocumentsRequested +=
            (_, _) => observedRequests.Add(nameof(form.AddDocumentsRequested));

        form.RemoveDocumentsRequested +=
            (_, _) => observedRequests.Add(nameof(form.RemoveDocumentsRequested));

        form.RemoveDocumentRequested +=
            (_, _) => observedRequests.Add(nameof(form.RemoveDocumentRequested));

        form.UpdateWorkspaceDetailsRequested +=
            (_, _) => observedRequests.Add(nameof(form.UpdateWorkspaceDetailsRequested));

        form.SaveAsWorkspaceRequested +=
            (_, _) => observedRequests.Add(nameof(form.SaveAsWorkspaceRequested));

        form.DocumentInformationRequested +=
            (_, _) => observedRequests.Add(nameof(form.DocumentInformationRequested));

        form.DeleteWorkspaceRequested +=
            (_, _) => observedRequests.Add(nameof(form.DeleteWorkspaceRequested));

        form.CloseWorkspaceRequested +=
            (_, _) => observedRequests.Add(nameof(form.CloseWorkspaceRequested));

        form.CloseDocumentRequested +=
            (_, _) => observedRequests.Add(nameof(form.CloseDocumentRequested));

        // Act
        GetMenuItem(form, "_documentInformationMenuItem").PerformClick();
        GetMenuItem(form, "removeDocumentsMenuItem").PerformClick();
        GetMenuItem(form, "updateWorkspaceDetailsMenuItem").PerformClick();
        GetMenuItem(form, "closeWorkspaceMenuItem").PerformClick();

        // Assert
        Assert.Equal(
            [
                nameof(form.DocumentInformationRequested),
                nameof(form.RemoveDocumentRequested),
                nameof(form.SaveAsWorkspaceRequested),
                nameof(form.CloseDocumentRequested)
            ],
            observedRequests);
    }

    [Fact]
    public void AddActivateAndMoveDocumentPresentation_UsesSameGenericFormInstanceAcrossTabsAndSidebar()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        Guid documentId = Guid.NewGuid();

        var documentForm = new TestDocumentViewForm();

        using WorkspaceDocumentPresentation presentation =
            CreateDocumentPresentation(
                documentId,
                documentForm);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        Panel sidebarContentPanel =
            GetControl<Panel>(
                workspaceView,
                "documentSidebarContentPanel");

        // Act
        workspaceView.AddDocumentPresentation(
            presentation.DocumentId,
            presentation.FileName,
            presentation.HostedForm);

        TabPage tabPage =
            Assert.Single(
                tabControl.TabPages.Cast<TabPage>());

        // Assert: initial tab hosting uses the generic Form instance.
        Assert.Same(
            documentForm,
            Assert.Single(
                tabPage.Controls.OfType<Form>()));

        Assert.Same(
            documentForm,
            presentation.HostedForm);

        Assert.Same(
            tabPage,
            documentForm.Parent);

        // Act: activate and move the document into sidebar hosting.
        Assert.True(
            workspaceView.ActivateDocumentPresentation(
                documentId));

        workspaceView.SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        // Assert: the same Form instance is hosted in the sidebar.
        Assert.Same(
            documentForm,
            Assert.Single(
                sidebarContentPanel.Controls.OfType<Form>()));

        Assert.Same(
            sidebarContentPanel,
            documentForm.Parent);

        // Act: return to tab hosting.
        workspaceView.SetNavigationMode(
            WorkspacePresentationNavigationMode.Tabs);

        // Assert: the original Form instance returns to the original tab.
        Assert.Same(
            documentForm,
            Assert.Single(
                tabPage.Controls.OfType<Form>()));

        Assert.Same(
            tabPage,
            documentForm.Parent);
    }

    [Fact]
    public void AddDocumentPresentation_SuppressesImplicitSelectionButUserSelectionRaisesDocumentActivated()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        workspaceView.Show();

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        using WorkspaceDocumentPresentation firstPresentation =
            CreateDocumentPresentation(
                firstDocumentId,
                new TestDocumentViewForm());

        using WorkspaceDocumentPresentation secondPresentation =
            CreateDocumentPresentation(
                secondDocumentId,
                new TestDocumentViewForm());

        var activatedDocumentIds = new List<Guid>();

        workspaceView.DocumentActivated +=
            (_, args) => activatedDocumentIds.Add(args.DocumentId);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        // Act
        workspaceView.AddDocumentPresentation(
            firstPresentation.DocumentId,
            firstPresentation.FileName,
            firstPresentation.HostedForm);

        workspaceView.AddDocumentPresentation(
            secondPresentation.DocumentId,
            secondPresentation.FileName,
            secondPresentation.HostedForm);

        // Assert
        Assert.Empty(activatedDocumentIds);

        // Act
        tabControl.SelectedIndex = 1;

        // Assert
        Assert.Equal(
            [secondDocumentId],
            activatedDocumentIds);
    }

    [Fact]
    public void RemoveDocumentPresentation_WhenRemovingSelectedTab_DoesNotRaiseDocumentActivatedForImplicitReplacement()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        workspaceView.Show();

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        using WorkspaceDocumentPresentation firstPresentation =
            CreateDocumentPresentation(
                firstDocumentId,
                new TestDocumentViewForm());

        using WorkspaceDocumentPresentation secondPresentation =
            CreateDocumentPresentation(
                secondDocumentId,
                new TestDocumentViewForm());

        List<Guid> activatedDocumentIds = [];

        workspaceView.DocumentActivated +=
            (_, args) => activatedDocumentIds.Add(args.DocumentId);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        workspaceView.AddDocumentPresentation(
            firstPresentation.DocumentId,
            firstPresentation.FileName,
            firstPresentation.HostedForm);

        workspaceView.AddDocumentPresentation(
            secondPresentation.DocumentId,
            secondPresentation.FileName,
            secondPresentation.HostedForm);

        // Act: select the second tab as the user.
        tabControl.SelectedIndex = 1;

        Assert.Equal(
            [secondDocumentId],
            activatedDocumentIds);

        // Removing the selected tab implicitly selects the first tab.
        bool removed =
            workspaceView.RemoveDocumentPresentation(secondDocumentId);

        // Assert
        Assert.True(removed);
        Assert.Single(tabControl.TabPages);

        Assert.Equal(
            [secondDocumentId],
            activatedDocumentIds);
    }

    [Fact]
    public void RemoveDocumentPresentation_WhenHostedInTabs_DetachesWithoutDisposingOwnedForm()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        Guid documentId = Guid.NewGuid();

        var documentForm = new TestDocumentViewForm();

        using WorkspaceDocumentPresentation presentation =
            CreateDocumentPresentation(
                documentId,
                documentForm);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        workspaceView.AddDocumentPresentation(
            presentation.DocumentId,
            presentation.FileName,
            presentation.HostedForm);

        TabPage tabPage =
            Assert.Single(tabControl.TabPages.Cast<TabPage>());

        Assert.Same(tabPage, documentForm.Parent);

        // Act
        bool removed =
            workspaceView.RemoveDocumentPresentation(documentId);

        // Assert
        Assert.True(removed);
        Assert.Empty(tabControl.TabPages);
        Assert.Null(documentForm.Parent);

        // The workspace view detached the Form but did not dispose it.
        Assert.False(documentForm.IsDisposed);

        // The owning presentation remains responsible for disposal.
        presentation.Dispose();

        Assert.True(documentForm.IsDisposed);
    }

    [Fact]
    public void AddDocumentPresentation_WhenHostingFailsAfterParenting_RollsBackWithoutDisposingOwnedForm()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        Guid documentId = Guid.NewGuid();

        var documentForm = new TestDocumentViewForm(
            throwWhenParented: true);

        using WorkspaceDocumentPresentation presentation =
            CreateDocumentPresentation(
                documentId,
                documentForm);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => workspaceView.AddDocumentPresentation(
                    presentation.DocumentId,
                    presentation.FileName,
                    presentation.HostedForm));

        // Assert: the original hosting failure is preserved.
        Assert.Equal(
            "Document hosting failed.",
            exception.Message);

        // No partial tab or hosted Form remains.
        Assert.Empty(tabControl.TabPages);
        Assert.Null(documentForm.Parent);

        // The workspace view did not dispose the Form.
        Assert.False(documentForm.IsDisposed);

        // The failed hosting attempt left no registry entry.
        Assert.False(
            workspaceView.RemoveDocumentPresentation(
                documentId));

        // The owning presentation remains responsible for disposal.
        presentation.Dispose();

        Assert.True(documentForm.IsDisposed);
    }

    [Fact]
    public void RemoveDocumentPresentation_WhenHostedInSidebar_RemovesGenericForm()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        Guid documentId = Guid.NewGuid();

        var documentForm = new TestDocumentViewForm();

        using WorkspaceDocumentPresentation presentation =
            CreateDocumentPresentation(
                documentId,
                documentForm);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        Panel sidebarContentPanel =
            GetControl<Panel>(
                workspaceView,
                "documentSidebarContentPanel");

        workspaceView.AddDocumentPresentation(
            presentation.DocumentId,
            presentation.FileName,
            presentation.HostedForm);

        Assert.True(
            workspaceView.ActivateDocumentPresentation(
                documentId));

        workspaceView.SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        Assert.Same(
            sidebarContentPanel,
            documentForm.Parent);

        // Act
        bool removed =
            workspaceView.RemoveDocumentPresentation(
                documentId);

        // Assert
        Assert.True(removed);
        Assert.Empty(tabControl.TabPages);

        Assert.DoesNotContain(
            documentForm,
            sidebarContentPanel.Controls.Cast<Control>());

        Assert.Null(documentForm.Parent);

        // The hosted Form must remain alive after workspace removal.
        Assert.False(documentForm.IsDisposed);

        // Its owning presentation performs the actual disposal.
        presentation.Dispose();

        Assert.True(documentForm.IsDisposed);
    }

    [Fact]
    public void ClearPresentation_WhenDocumentsAreHostedAcrossTabsAndSidebar_DetachesWithoutDisposingOwnedForms()
    {
        // Arrange
        using var workspaceView = new WorkspaceViewForm();

        Guid tabDocumentId = Guid.NewGuid();
        Guid sidebarDocumentId = Guid.NewGuid();

        var tabDocumentForm = new TestDocumentViewForm();
        var sidebarDocumentForm = new TestDocumentViewForm();

        using WorkspaceDocumentPresentation tabPresentation =
            CreateDocumentPresentation(
                tabDocumentId,
                tabDocumentForm);

        using WorkspaceDocumentPresentation sidebarPresentation =
            CreateDocumentPresentation(
                sidebarDocumentId,
                sidebarDocumentForm);

        TabControl tabControl =
            GetControl<TabControl>(
                workspaceView,
                "documentTabControl");

        Panel sidebarContentPanel =
            GetControl<Panel>(
                workspaceView,
                "documentSidebarContentPanel");

        workspaceView.AddDocumentPresentation(
            tabPresentation.DocumentId,
            tabPresentation.FileName,
            tabPresentation.HostedForm);

        workspaceView.AddDocumentPresentation(
            sidebarPresentation.DocumentId,
            sidebarPresentation.FileName,
            sidebarPresentation.HostedForm);

        // Move one document into Sidebar while keeping the other in Tabs.
        Assert.True(
            workspaceView.ActivateDocumentPresentation(
                sidebarDocumentId));

        workspaceView.SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        Assert.Contains(
            tabDocumentForm,
            tabControl.TabPages
                .Cast<TabPage>()
                .SelectMany(page => page.Controls.OfType<Form>()));

        Assert.Same(
            sidebarContentPanel,
            sidebarDocumentForm.Parent);

        // Act
        workspaceView.ClearPresentation();

        // Assert: all workspace-owned containers are cleared.
        Assert.Empty(tabControl.TabPages);
        Assert.Empty(sidebarContentPanel.Controls);

        // Both document Forms are detached.
        Assert.Null(tabDocumentForm.Parent);
        Assert.Null(sidebarDocumentForm.Parent);

        // Neither Form was disposed by the workspace view.
        Assert.False(tabDocumentForm.IsDisposed);
        Assert.False(sidebarDocumentForm.IsDisposed);

        // The individual presentation owners remain responsible for disposal.
        tabPresentation.Dispose();
        sidebarPresentation.Dispose();

        Assert.True(tabDocumentForm.IsDisposed);
        Assert.True(sidebarDocumentForm.IsDisposed);
    }

    private static string[] GetWorkspaceMenuItemNames(
        WorkspaceViewForm form)
    {
        FieldInfo? field =
            typeof(WorkspaceViewForm).GetField(
                "workspaceContextMenu",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.NotNull(field);

        ContextMenuStrip? menu =
            field!.GetValue(form) as ContextMenuStrip;

        Assert.NotNull(menu);

        return [.. menu!.Items
            .Cast<ToolStripItem>()
            .Select(static item => item.Name ?? string.Empty)];
    }

    private static T GetControl<T>(
        WorkspaceViewForm form,
        string fieldName)
        where T : Control
    {
        FieldInfo? field =
            typeof(WorkspaceViewForm).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.NotNull(field);

        T? control =
            field!.GetValue(form) as T;

        Assert.NotNull(control);

        return control!;
    }

    private static ToolStripMenuItem GetMenuItem(
        WorkspaceViewForm form,
        string fieldName)
    {
        FieldInfo? field =
            typeof(WorkspaceViewForm).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.NotNull(field);

        ToolStripMenuItem? menuItem =
            field!.GetValue(form) as ToolStripMenuItem;

        Assert.NotNull(menuItem);

        return menuItem!;
    }

    private static WorkspaceDocumentPresentation CreateDocumentPresentation(
        Guid documentId,
        TestDocumentViewForm documentView)
    {
        var documentRepository =
            new Mock<IDocumentRepository>();

        var documentReader =
            new Mock<IDocumentReader>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var getDocumentHandler =
            new GetDocumentHandler(
                documentRepository.Object,
                NullLogger<GetDocumentHandler>.Instance);

        var openDocumentHandler =
            new OpenDocumentHandler(
                documentRepository.Object,
                documentReader.Object,
                new DocumentProcessingOptions(),
                NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        var presenter =
            new DocumentWorkspacePresenter(
                documentView,
                new Mock<IDocumentViewer>().Object,
                getDocumentHandler,
                openDocumentHandler,
                removeDocumentHandler,
                NullLogger<DocumentWorkspacePresenter>.Instance,
                securitySession.Object);

        return new WorkspaceDocumentPresentation(
            documentId,
            presenter,
            documentView);
    }

    private static void AddPresentationToView(
        WorkspaceViewForm workspaceView,
        WorkspaceDocumentPresentation presentation)
    {
        workspaceView.AddDocumentPresentation(
            presentation.DocumentId,
            presentation.FileName,
            presentation.HostedForm);
    }

    private sealed class TestDocumentViewForm :
        Form,
        IDocumentView
    {
        private readonly bool _throwWhenParented;

        public TestDocumentViewForm(
            bool throwWhenParented = false)
        {
            _throwWhenParented = throwWhenParented;
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);

            if (_throwWhenParented && Parent is not null)
            {
                throw new InvalidOperationException(
                    "Document hosting failed.");
            }
        }

        public event EventHandler OpenExternallyRequested
        {
            add { }
            remove { }
        }

        public event EventHandler DocumentInformationRequested
        {
            add { }
            remove { }
        }

        public event EventHandler RemoveDocumentRequested
        {
            add { }
            remove { }
        }

        public Task ShowDocumentAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void ShowUnsupportedPreview(
            string message)
        {
        }

        public void ShowDocumentInformation(
            string displayName,
            string fileName,
            string fileType,
            DateTime importedAt,
            string status,
            string sha256Hash,
            long expectedSessionRevision = 0)
        {
        }

        public bool ConfirmRemoval(
            string fileName,
            long expectedSessionRevision = 0)
        {
            return false;
        }

        public void ClearPresentation()
        {
        }

        public void PostToUi(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
        }

        public void ShowError(
            string message,
            string title,
            long expectedSessionRevision = 0)
        {
        }
    }
}

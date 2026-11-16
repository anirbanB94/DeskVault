using DeskVault.UI.Forms;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Workspace;
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
}

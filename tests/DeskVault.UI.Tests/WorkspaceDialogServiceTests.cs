using System.Windows.Forms;
using DeskVault.Domain.Documents;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class WorkspaceDialogServiceTests
{
    [Fact]
    public void ShowWorkspaceDetails_UsesSessionBoundCoordinator()
    {
        const long expectedRevision = 17;

        var (service, coordinator, shownDialogs) =
            CreateService(expectedRevision);

        WorkspaceDetailsDialogResult? result =
            service.ShowWorkspaceDetails(
                "Workspace",
                "Description",
                "Edit Workspace",
                expectedRevision);

        Assert.Null(result);

        Form dialog = Assert.Single(shownDialogs);

        Assert.Equal(
            "WorkspaceDetailsDialog",
            dialog.GetType().Name);

        Assert.Equal(
            "Edit Workspace",
            dialog.Text);

        coordinator.Verify(
            x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedRevision,
                null),
            Times.Once);
    }

    [Fact]
    public void ShowDocumentPicker_UsesSessionBoundCoordinator()
    {
        const long expectedRevision = 23;

        var (service, coordinator, shownDialogs) =
            CreateService(expectedRevision);

        WorkspaceDocumentSelectionResult? result =
            service.ShowDocumentPicker(
                Array.Empty<Document>(),
                Array.Empty<Guid>(),
                expectedRevision);

        Assert.Null(result);

        Form dialog = Assert.Single(shownDialogs);

        Assert.Equal(
            "WorkspaceDocumentPickerDialog",
            dialog.GetType().Name);

        coordinator.Verify(
            x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedRevision,
                null),
            Times.Once);
    }

    [Fact]
    public void ShowDocumentRemoval_UsesSessionBoundCoordinator()
    {
        const long expectedRevision = 31;

        var (service, coordinator, shownDialogs) =
            CreateService(expectedRevision);

        WorkspaceDocumentSelectionResult? result =
            service.ShowDocumentRemoval(
                Array.Empty<Document>(),
                expectedRevision);

        Assert.Null(result);

        Form dialog = Assert.Single(shownDialogs);

        Assert.Equal(
            "WorkspaceDocumentRemovalDialog",
            dialog.GetType().Name);

        coordinator.Verify(
            x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedRevision,
                null),
            Times.Once);
    }

    private static (
        WorkspaceDialogService Service,
        Mock<ISessionBoundDialogCoordinator> Coordinator,
        List<Form> ShownDialogs)
        CreateService(long expectedRevision)
    {
        List<Form> shownDialogs = [];

        var coordinator =
            new Mock<ISessionBoundDialogCoordinator>();

        coordinator
            .Setup(x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedRevision,
                null))
            .Callback<Form, long, IWin32Window?>(
                (dialog, _, _) => shownDialogs.Add(dialog))
            .Returns(DialogResult.Cancel);

        var service =
            new WorkspaceDialogService(
                coordinator.Object);

        return (
            service,
            coordinator,
            shownDialogs);
    }
}

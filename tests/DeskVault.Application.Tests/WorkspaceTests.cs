using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Tests;

public sealed class WorkspaceTests
{
    [Fact]
    public void CreateTemporary_CreatesEmptyTemporaryWorkspace()
    {
        // Arrange
        Guid workspaceId = Guid.NewGuid();

        // Act
        Workspace workspace =
            Workspace.CreateTemporary(workspaceId);

        // Assert
        Assert.Equal(
            workspaceId,
            workspace.Id);

        Assert.Equal(
            WorkspaceType.Temporary,
            workspace.TypeOfWorkspace);

        Assert.Null(workspace.Name);
        Assert.Null(workspace.Description);
        Assert.Empty(workspace.Memberships);
        Assert.Null(workspace.LastActiveDocumentId);
        Assert.NotEqual(default, workspace.LastUpdated);
    }

    [Fact]
    public void CreatePersistent_CreatesNamedPersistentWorkspace()
    {
        // Arrange
        Guid workspaceId = Guid.NewGuid();
        const string workspaceName = "Research";

        // Act
        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                workspaceName);

        // Assert
        Assert.Equal(
            workspaceId,
            workspace.Id);

        Assert.Equal(
            WorkspaceType.Persistent,
            workspace.TypeOfWorkspace);

        Assert.Equal(
            workspaceName,
            workspace.Name);

        Assert.Null(workspace.Description);

        Assert.NotEqual(
            default,
            workspace.LastUpdated);
    }

    [Fact]
    public void AddDocument_AddsMembershipWithNextOrder()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        // Act
        workspace.AddDocument(firstDocumentId);
        workspace.AddDocument(secondDocumentId);

        // Assert
        Assert.Collection(
            workspace.Memberships,
            firstMembership =>
            {
                Assert.Equal(
                    firstDocumentId,
                    firstMembership.DocumentId);

                Assert.Equal(
                    0,
                    firstMembership.Order);
            },
            secondMembership =>
            {
                Assert.Equal(
                    secondDocumentId,
                    secondMembership.DocumentId);

                Assert.Equal(
                    1,
                    secondMembership.Order);
            });
    }

    [Fact]
    public void AddDocument_RejectsDuplicateMembership()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);

        // Act
        Action action =
            () => workspace.AddDocument(documentId);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AddDocument_AllowsSameDocumentInMultipleWorkspaces()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        Workspace firstWorkspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Workspace secondWorkspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Research");

        // Act
        firstWorkspace.AddDocument(documentId);
        secondWorkspace.AddDocument(documentId);

        // Assert
        Assert.Contains(
            firstWorkspace.Memberships,
            membership => membership.DocumentId == documentId);

        Assert.Contains(
            secondWorkspace.Memberships,
            membership => membership.DocumentId == documentId);
    }

    [Fact]
    public void RemoveDocument_RemovesMembership()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);

        // Act
        workspace.RemoveDocument(documentId);

        // Assert
        Assert.Empty(workspace.Memberships);
    }

    [Fact]
    public void RemoveDocument_ClearsLastActiveDocumentWhenRemoved()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);
        workspace.SetLastActiveDocument(documentId);

        // Act
        workspace.RemoveDocument(documentId);

        // Assert
        Assert.Null(workspace.LastActiveDocumentId);
    }

    [Fact]
    public void SetLastActiveDocument_RequiresWorkspaceMembership()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Guid documentId = Guid.NewGuid();

        // Act
        Action action =
            () => workspace.SetLastActiveDocument(documentId);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void SetLastActiveDocument_AllowsWorkspaceMember()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        Guid documentId = Guid.NewGuid();

        workspace.AddDocument(documentId);

        // Act
        workspace.SetLastActiveDocument(documentId);

        // Assert
        Assert.Equal(
            documentId,
            workspace.LastActiveDocumentId);
    }

    [Fact]
    public void Rename_PersistentWorkspaceChangesName()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Original");

        const string newName = "Renamed";

        // Act
        workspace.Rename(newName);

        // Assert
        Assert.Equal(
            newName,
            workspace.Name);
    }

    [Fact]
    public void Rename_TemporaryWorkspaceIsRejected()
    {
        // Arrange
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        // Act
        Action action =
            () => workspace.Rename("Renamed");

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Restore_RejectsDuplicateDocumentMembership()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        WorkspaceDocumentMembership firstMembership =
            WorkspaceDocumentMembership.Create(
                documentId,
                0);

        WorkspaceDocumentMembership duplicateMembership =
            WorkspaceDocumentMembership.Create(
                documentId,
                1);

        // Act
        Action action =
            () => Workspace.Restore(
                Guid.NewGuid(),
                "Research",
                null,
                WorkspaceType.Persistent,
                [
                    firstMembership,
                    duplicateMembership
                ],
                null,
                DateTimeOffset.UtcNow);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Restore_RejectsDuplicateMembershipOrder()
    {
        // Arrange
        WorkspaceDocumentMembership firstMembership =
            WorkspaceDocumentMembership.Create(
                Guid.NewGuid(),
                0);

        WorkspaceDocumentMembership secondMembership =
            WorkspaceDocumentMembership.Create(
                Guid.NewGuid(),
                0);

        // Act
        Action action =
            () => Workspace.Restore(
                Guid.NewGuid(),
                "Research",
                null,
                WorkspaceType.Persistent,
                [
                    firstMembership,
                    secondMembership
                ],
                null,
                DateTimeOffset.UtcNow);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Restore_RejectsLastActiveDocumentThatIsNotAMember()
    {
        // Arrange
        WorkspaceDocumentMembership membership =
            WorkspaceDocumentMembership.Create(
                Guid.NewGuid(),
                0);

        Guid nonMemberDocumentId = Guid.NewGuid();

        // Act
        Action action =
            () => Workspace.Restore(
                Guid.NewGuid(),
                "Research",
                null,
                WorkspaceType.Persistent,
                [membership],
                nonMemberDocumentId,
                DateTimeOffset.UtcNow);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Restore_PreservesMembershipsAndLastActiveDocument()
    {
        // Arrange
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();
        DateTimeOffset lastUpdated = DateTimeOffset.UtcNow;

        WorkspaceDocumentMembership firstMembership =
            WorkspaceDocumentMembership.Create(
                firstDocumentId,
                0);

        WorkspaceDocumentMembership secondMembership =
            WorkspaceDocumentMembership.Create(
                secondDocumentId,
                1);

        // Act
        Workspace workspace =
            Workspace.Restore(
                Guid.NewGuid(),
                "Research",
                "Research workspace",
                WorkspaceType.Persistent,
                [
                    firstMembership,
                    secondMembership
                ],
                secondDocumentId,
                lastUpdated);

        // Assert
        Assert.Equal(
            WorkspaceType.Persistent,
            workspace.TypeOfWorkspace);

        Assert.Equal(
            "Research",
            workspace.Name);

        Assert.Equal(
            "Research workspace",
            workspace.Description);

        Assert.Equal(
            lastUpdated,
            workspace.LastUpdated);

        Assert.Collection(
            workspace.Memberships,
            restoredFirst =>
            {
                Assert.Equal(
                    firstDocumentId,
                    restoredFirst.DocumentId);

                Assert.Equal(
                    0,
                    restoredFirst.Order);
            },
            restoredSecond =>
            {
                Assert.Equal(
                    secondDocumentId,
                    restoredSecond.DocumentId);

                Assert.Equal(
                    1,
                    restoredSecond.Order);
            });

        Assert.Equal(
            secondDocumentId,
            workspace.LastActiveDocumentId);
    }
}

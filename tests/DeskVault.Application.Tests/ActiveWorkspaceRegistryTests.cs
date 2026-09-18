using DeskVault.Application.Workspaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Tests;

public sealed class ActiveWorkspaceRegistryTests
{
    [Fact]
    public void Add_RegistersWorkspace()
    {
        // Arrange
        var registry = new ActiveWorkspaceRegistry();
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        // Act
        registry.Add(workspace);

        // Assert
        Assert.Same(
            workspace,
            registry.Get(workspace.Id));
    }

    [Fact]
    public void Add_DuplicateWorkspaceId_Throws()
    {
        // Arrange
        var registry = new ActiveWorkspaceRegistry();
        Guid workspaceId = Guid.NewGuid();

        Workspace first =
            Workspace.CreateTemporary(workspaceId);

        Workspace second =
            Workspace.CreateTemporary(workspaceId);

        registry.Add(first);

        // Act
        Action act = () => registry.Add(second);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Get_UnknownWorkspace_ReturnsNull()
    {
        // Arrange
        var registry = new ActiveWorkspaceRegistry();
        Guid workspaceId = Guid.NewGuid();

        // Act
        Workspace? workspace =
            registry.Get(workspaceId);

        // Assert
        Assert.Null(workspace);
    }

    [Fact]
    public void GetAll_ReturnsAllActiveWorkspaces()
    {
        // Arrange
        var registry = new ActiveWorkspaceRegistry();

        Workspace first =
            Workspace.CreateTemporary(Guid.NewGuid());

        Workspace second =
            Workspace.CreateTemporary(Guid.NewGuid());

        registry.Add(first);
        registry.Add(second);

        // Act
        IReadOnlyList<Workspace> workspaces =
            registry.GetAll();

        // Assert
        Assert.Equal(2, workspaces.Count);
        Assert.Contains(first, workspaces);
        Assert.Contains(second, workspaces);
    }

    [Fact]
    public void Remove_RegisteredWorkspace_ReturnsTrueAndRemovesWorkspace()
    {
        // Arrange
        var registry = new ActiveWorkspaceRegistry();
        Workspace workspace =
            Workspace.CreateTemporary(Guid.NewGuid());

        registry.Add(workspace);

        // Act
        bool removed =
            registry.Remove(workspace.Id);

        // Assert
        Assert.True(removed);
        Assert.Null(
            registry.Get(workspace.Id));
    }

    [Fact]
    public void Remove_UnknownWorkspace_ReturnsFalse()
    {
        // Arrange
        var registry = new ActiveWorkspaceRegistry();
        Guid workspaceId = Guid.NewGuid();

        // Act
        bool removed =
            registry.Remove(workspaceId);

        // Assert
        Assert.False(removed);
    }
}

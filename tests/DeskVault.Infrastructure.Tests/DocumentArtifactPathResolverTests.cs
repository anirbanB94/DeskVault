using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentArtifactPathResolverTests
{
    [Fact]
    public void GetPath_WhenDocumentIdIsValid_ReturnsCanonicalManagedArtifactPath()
    {
        // Arrange
        using var context = CreateTestContext();
        Guid documentId = Guid.NewGuid();

        string expectedPath =
            Path.Combine(
                context.DataPaths.DocumentsDirectory,
                $"{documentId}.dvault");

        // Act
        string result =
            context.Resolver.GetPath(documentId);

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetPath_WhenDocumentIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateTestContext();

        // Act
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => context.Resolver.GetPath(Guid.Empty));

        // Assert
        Assert.Equal("documentId", exception.ParamName);
        Assert.Contains(
            "Document ID cannot be empty.",
            exception.Message);
    }

    [Fact]
    public void GetPath_ForDifferentDocumentIds_ReturnsDifferentArtifactPaths()
    {
        // Arrange
        using var context = CreateTestContext();
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        // Act
        string firstPath =
            context.Resolver.GetPath(firstDocumentId);

        string secondPath =
            context.Resolver.GetPath(secondDocumentId);

        // Assert
        Assert.NotEqual(firstPath, secondPath);
        Assert.Contains(
            $"{firstDocumentId}.dvault",
            firstPath);
        Assert.Contains(
            $"{secondDocumentId}.dvault",
            secondPath);
    }

    [Fact]
    public void GetPath_UsesManagedDocumentsDirectory()
    {
        // Arrange
        using var context = CreateTestContext();
        Guid documentId = Guid.NewGuid();

        string expectedDirectory =
            Path.GetFullPath(
                context.DataPaths.DocumentsDirectory);

        // Act
        string result =
            context.Resolver.GetPath(documentId);

        // Assert
        string actualDirectory =
            Path.GetFullPath(
                Path.GetDirectoryName(result)!);

        Assert.Equal(
            expectedDirectory,
            actualDirectory);
    }

    [Fact]
    public void IsOwnedArtifactPath_WhenPathIsCanonical_ReturnsTrue()
    {
        // Arrange
        using TestContext context = new();

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.Resolver.GetPath(
                documentId);

        // Act
        bool result =
            context.Resolver.IsOwnedArtifactPath(
                documentId,
                artifactPath);

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void IsOwnedArtifactPath_WhenPathBelongsToAnotherDocument_ReturnsFalse()
    {
        // Arrange
        using TestContext context = new();

        Guid documentId =
            Guid.NewGuid();

        Guid otherDocumentId =
            Guid.NewGuid();

        string artifactPath =
            context.Resolver.GetPath(
                otherDocumentId);

        // Act
        bool result =
            context.Resolver.IsOwnedArtifactPath(
                documentId,
                artifactPath);

        // Assert
        Assert.False(
            result);
    }

    [Fact]
    public void IsOwnedArtifactPath_WhenSameDocumentIdIsOutsideManagedDirectory_ReturnsFalse()
    {
        // Arrange
        using TestContext context = new();

        Guid documentId =
            Guid.NewGuid();

        string outsideArtifactPath =
            Path.Combine(
                context.RootDirectory,
                "Outside",
                $"{documentId}.dvault");

        // Act
        bool result =
            context.Resolver.IsOwnedArtifactPath(
                documentId,
                outsideArtifactPath);

        // Assert
        Assert.False(
            result);
    }

    private static TestContext CreateTestContext()
    {
        return new TestContext();
    }

    private sealed class TestContext : IDisposable
    {
        public string RootDirectory { get; }

        public DeskVaultDataPaths DataPaths { get; }

        public DocumentArtifactPathResolver Resolver { get; }

        public TestContext()
        {
            RootDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                RootDirectory);

            DataPaths =
                new DeskVaultDataPaths(
                    RootDirectory);

            Resolver =
                new DocumentArtifactPathResolver(
                    DataPaths);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(
                    RootDirectory,
                    recursive: true);
            }
        }
    }
}

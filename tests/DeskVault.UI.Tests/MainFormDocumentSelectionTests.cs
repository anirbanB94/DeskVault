using DeskVault.UI.Forms;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class MainFormDocumentSelectionTests
{
    private static readonly DateTimeOffset ImportedAt =
        new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShowDocuments_WhenPreviouslySelectedDocumentRemainsAvailable_PreservesSelection()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem first =
            CreateDocument("first.txt");

        DocumentListItem selected =
            CreateDocument("selected.md");

        DocumentListItem third =
            CreateDocument("third.csv");

        form.ShowDocuments(
        [
            first,
            selected
        ]);

        form.SetSelectedDocumentId(selected.Id);

        // Act
        form.ShowDocuments(
        [
            third,
            selected,
            first
        ]);

        // Assert
        DataGridView grid = GetDocumentGrid(form);

        Assert.Equal(
            selected.Id,
            form.SelectedDocumentId);

        Assert.Single(grid.SelectedRows);

        Assert.True(
            grid.Rows[1].Selected);

        Assert.Same(
            grid.Rows[1].Cells[0],
            grid.CurrentCell);

        form.Close();
    }

    [Fact]
    public void ShowDocuments_WhenPreviouslySelectedDocumentIsRemoved_ClearsSelectionAndCurrentCell()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem first =
            CreateDocument("first.txt");

        DocumentListItem removed =
            CreateDocument("removed.md");

        form.ShowDocuments(
        [
            first,
            removed
        ]);

        form.SetSelectedDocumentId(removed.Id);

        // Act
        form.ShowDocuments(
        [
            first
        ]);

        // Assert
        AssertSelectionCleared(form);

        form.Close();
    }

    [Fact]
    public void ShowDocuments_WhenNoPreviousSelectionExists_SelectsFirstDocument()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem first =
            CreateDocument("first.txt");

        DocumentListItem second =
            CreateDocument("second.md");

        // Act
        form.ShowDocuments(
        [
            first,
            second
        ]);

        // Assert
        DataGridView grid = GetDocumentGrid(form);

        Assert.Equal(
            first.Id,
            form.SelectedDocumentId);

        Assert.Single(grid.SelectedRows);

        Assert.True(
            grid.Rows[0].Selected);

        Assert.Same(
            grid.Rows[0].Cells[0],
            grid.CurrentCell);

        form.Close();
    }

    [Fact]
    public void ShowDocuments_WhenResultIsEmpty_ClearsSelectionAndCurrentCell()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem selected =
            CreateDocument("selected.txt");

        form.ShowDocuments(
        [
            selected
        ]);

        form.SetSelectedDocumentId(selected.Id);

        // Act
        form.ShowDocuments([]);

        // Assert
        AssertSelectionCleared(form);

        form.Close();
    }

    [Fact]
    public void SetSelectedDocumentId_WhenNull_ClearsSelectionAndCurrentCell()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem document =
            CreateDocument("selected.txt");

        form.ShowDocuments([document]);

        // Act
        form.SetSelectedDocumentId(null);

        // Assert
        AssertSelectionCleared(form);

        form.Close();
    }

    [Fact]
    public void SetSelectedDocumentId_WhenDocumentDoesNotExist_ClearsStaleSelection()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem document =
            CreateDocument("selected.txt");

        form.ShowDocuments([document]);

        Guid missingDocumentId = Guid.NewGuid();

        // Act
        form.SetSelectedDocumentId(missingDocumentId);

        // Assert
        AssertSelectionCleared(form);

        form.Close();
    }

    [Fact]
    public void SetSelectedDocumentId_WhenDocumentExists_SelectsCorrectRowAndCurrentCell()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem first =
            CreateDocument("first.txt");

        DocumentListItem selected =
            CreateDocument("selected.md");

        DocumentListItem third =
            CreateDocument("third.csv");

        form.ShowDocuments(
        [
            first,
            selected,
            third
        ]);

        // Act
        form.SetSelectedDocumentId(selected.Id);

        // Assert
        DataGridView grid = GetDocumentGrid(form);

        Assert.Equal(
            selected.Id,
            form.SelectedDocumentId);

        Assert.Single(grid.SelectedRows);

        Assert.True(
            grid.Rows[1].Selected);

        Assert.Same(
            grid.Rows[1].Cells[0],
            grid.CurrentCell);

        form.Close();
    }

    [Fact]
    public void SetSelectedDocumentId_WhenSearchResultExists_SelectsCorrectRowAndCurrentCell()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        var searchResult =
            new SearchResultListItem(
                Guid.NewGuid(),
                "Security Policy",
                "security-policy.md",
                "Security policy matching content.",
                1);

        form.ShowSearchResults(
        [
            searchResult
        ]);

        // Act
        form.SetSelectedDocumentId(
            searchResult.DocumentId);

        // Assert
        DataGridView grid = GetDocumentGrid(form);

        Assert.Equal(
            searchResult.DocumentId,
            form.SelectedDocumentId);

        Assert.Single(grid.SelectedRows);

        Assert.True(
            grid.Rows[0].Selected);

        Assert.Same(
            grid.Rows[0].Cells[0],
            grid.CurrentCell);

        form.Close();
    }

    [Fact]
    public void ProgrammaticSelectionChanges_DoNotRaiseSelectionEvents_ButUserSelectionDoes()
    {
        // Arrange
        using MainForm form = CreateMainForm();

        DocumentListItem first =
            CreateDocument("first.txt");

        DocumentListItem second =
            CreateDocument("second.md");

        DocumentListItem third =
            CreateDocument("third.csv");

        form.ShowDocuments(
        [
            first,
            second,
            third
        ]);

        int selectionChangedCount = 0;

        form.DocumentSelectionChanged +=
            (_, _) => selectionChangedCount++;

        // Act: Refresh and programmatic selection changes.
        form.ShowDocuments(
        [
            second,
            first,
            third
        ]);

        form.SetSelectedDocumentId(third.Id);
        form.SetSelectedDocumentId(null);
        form.SetSelectedDocumentId(second.Id);

        // Assert: No programmatic selection action was raised.
        Assert.Equal(
            0,
            selectionChangedCount);

        // Act: Simulate an actual user changing the current row.
        DataGridView grid = GetDocumentGrid(form);

        grid.CurrentCell =
            grid.Rows[1].Cells[0];

        // Assert: A user-driven selection change remains observable.
        Assert.True(
            selectionChangedCount > 0);

        Assert.Equal(
            first.Id,
            form.SelectedDocumentId);

        form.Close();
    }

    private static MainForm CreateMainForm()
    {
        return MainFormDialogTests.CreateMainForm(
            new Mock<ISessionBoundDialogCoordinator>().Object);
    }

    private static DocumentListItem CreateDocument(
        string fileName)
    {
        string extension =
            Path.GetExtension(fileName)
                .TrimStart('.')
                .ToUpperInvariant();

        return new DocumentListItem(
            Guid.NewGuid(),
            fileName,
            extension,
            ImportedAt,
            "Ready");
    }

    private static DataGridView GetDocumentGrid(
        MainForm form)
    {
        return Assert.IsType<DataGridView>(
            Assert.Single(
                form.Controls.Find(
                    "documentGridView",
                    true)));
    }

    private static void AssertSelectionCleared(
        MainForm form)
    {
        // Arrange
        DataGridView grid =
            GetDocumentGrid(form);

        // Assert
        Assert.Empty(grid.SelectedRows);
        Assert.Null(grid.CurrentCell);
        Assert.Null(form.SelectedDocumentId);
    }
}

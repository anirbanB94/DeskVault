using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.UI.Rendering.CsvDocumentRendering;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies CSV document rendering and caller-owned source-stream
/// lifetime behaviour.
/// </summary>
public sealed class CsvDocumentContentRendererTests
{
    /// <summary>
    /// Verifies that CSV rendering produces its tabular presentation
    /// without disposing the caller-owned document source stream.
    /// </summary>
    [Fact]
    public async Task RenderAsync_CsvDocument_LeavesCallerOwnedSourceStreamOpen()
    {
        // Arrange
        const string fileName = "document.csv";

        const string csv =
            """
            Id,Name
            1,Alice
            """;

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                new CsvParsingOptions
                {
                    MaxRows = null
                });

        var renderer =
            new CsvDocumentContentRenderer(parser);

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(stream.CanRead);

        Panel workspacePanel =
            Assert.IsType<Panel>(
                Assert.Single(contentHost.Controls));

        DataGridView dataGridView =
            Assert.Single(
                workspacePanel.Controls
                    .OfType<DataGridView>());

        Assert.Equal(1, dataGridView.Rows.Count);

        Assert.Equal(
            "Alice",
            dataGridView.Rows[0].Cells[1].Value);
    }
}

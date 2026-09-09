using DeskVault.UI.Rendering.TextDocumentRendering;

namespace DeskVault.UI.Tests;

public sealed class TextDocumentContentRendererTests
{
    [Fact]
    public async Task RenderAsync_PlainText_DisplaysReadOnlyTextBox()
    {
        // Arrange
        const string fileName = "document.txt";
        const string expectedText = "DeskVault document content.";

        using var stream =
            CreateDocumentStream(expectedText);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            expectedText,
            textBox.Text);

        Assert.True(
            textBox.ReadOnly);

        Assert.False(
            textBox.WordWrap);

        Assert.Equal(
            ScrollBars.Both,
            textBox.ScrollBars);

        Assert.Equal(
            DockStyle.Fill,
            textBox.Dock);
    }

    [Fact]
    public async Task RenderAsync_Json_PreservesOriginalSource()
    {
        // Arrange
        const string fileName = "document.json";

        const string json =
            """
            {
              "name": "DeskVault",
              "version": 1
            }
            """;

        using var stream =
            CreateDocumentStream(json);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            json,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_Xml_PreservesOriginalSource()
    {
        // Arrange
        const string fileName = "document.xml";

        const string xml =
            """
            <configuration>
              <environment>Production</environment>
              <enabled>true</enabled>
            </configuration>
            """;

        using var stream =
            CreateDocumentStream(xml);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            xml,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_Yaml_PreservesOriginalSource()
    {
        // Arrange
        const string fileName = "document.yaml";

        const string yaml =
            """
            name: DeskVault
            version: 1
            """;

        using var stream =
            CreateDocumentStream(yaml);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            yaml,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_Yml_PreservesOriginalSource()
    {
        // Arrange
        const string fileName = "document.yml";

        const string yml =
            """
            database:
              provider: SQLite
              encrypted: true
            """;

        using var stream =
            CreateDocumentStream(yml);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            yml,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_Config_PreservesOriginalSource()
    {
        // Arrange
        const string fileName = "document.config";

        const string config =
            """
            [database]
            provider=SQLite
            encrypted=true
            """;

        using var stream =
            CreateDocumentStream(config);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            config,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_Log_DisplaysRawText()
    {
        // Arrange
        const string fileName = "application.log";

        const string expectedText =
            """
            2026-09-08 09:00:00 INFO Application started.
            2026-09-08 09:00:01 INFO Document processing started.
            2026-09-08 09:00:02 WARN Processing took longer than expected.
            """;

        using var stream =
            CreateDocumentStream(expectedText);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            expectedText,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_SourceCode_DisplaysRawText()
    {
        // Arrange
        const string fileName = "Example.cs";

        const string expectedText =
            """
            public class Example
            {
                public string GetValue()
                {
                    return "DeskVault";
                }
            }
            """;

        using var stream =
            CreateDocumentStream(expectedText);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            expectedText,
            textBox.Text);
    }

    [Theory]
    [InlineData(
        "document.TXT",
        "DeskVault document content.",
        "DeskVault document content.")]
    [InlineData(
        "document.LOG",
        "2026-09-08 INFO DeskVault started.",
        "2026-09-08 INFO DeskVault started.")]
    [InlineData(
        "document.JSON",
        """{"name":"DeskVault"}""",
        """{"name":"DeskVault"}""")]
    [InlineData(
        "document.YAML",
        "name: DeskVault",
        "name: DeskVault")]
    [InlineData(
        "document.INI",
        "name=DeskVault",
        "name=DeskVault")]
    [InlineData(
        "document.CS",
        "public class DeskVault {}",
        "public class DeskVault {}")]
    public async Task RenderAsync_SupportedFormat_IsCaseInsensitive(
        string fileName,
        string documentContent,
        string expectedText)
    {
        // Arrange
        using var stream =
            CreateDocumentStream(documentContent);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            fileName);

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            expectedText,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_ClearsExistingContentBeforeDisplayingText()
    {
        // Arrange
        const string expectedText =
            "Updated document content.";

        using var stream =
            CreateDocumentStream(expectedText);

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        contentHost.Controls.Add(
            new Label
            {
                Text = "Previous content"
            });

        // Act
        await renderer.RenderAsync(
            contentHost,
            stream,
            "document.txt");

        // Assert
        TextBox textBox =
            Assert.IsType<TextBox>(
                Assert.Single(contentHost.Controls));

        Assert.Equal(
            expectedText,
            textBox.Text);
    }

    [Fact]
    public async Task RenderAsync_WhenCancellationRequested_ThrowsCancellation()
    {
        // Arrange
        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        var renderer =
            CreateRenderer();

        using var contentHost =
            new Panel();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task renderTask =
            renderer.RenderAsync(
                contentHost,
                stream,
                "document.txt",
                cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => renderTask);
    }

    private static TextDocumentContentRenderer CreateRenderer()
    {
        return new TextDocumentContentRenderer();
    }

    private static MemoryStream CreateDocumentStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(content));
    }
}

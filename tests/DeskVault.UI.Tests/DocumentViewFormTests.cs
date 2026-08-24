using DeskVault.UI.Forms;
using DeskVault.UI.Rendering;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class DocumentViewFormTests
{
    [Fact]
    public async Task ShowDocumentAsync_UnsupportedRenderer_PropagatesNotSupportedException()
    {
        const string fileName =
            "document.pdf";

        const string expectedMessage =
            "No document renderer is available for 'document.pdf'.";

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        resolver
            .Setup(x => x.Resolve(fileName))
            .Throws(
                new NotSupportedException(
                    expectedMessage));

        using var form =
            new DocumentViewForm(
                resolver.Object);

        using var stream =
            CreateDocumentStream();

        NotSupportedException exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () =>
                    form.ShowDocumentAsync(
                        stream,
                        fileName));

        Assert.Equal(
            expectedMessage,
            exception.Message);

        resolver.Verify(
            x => x.Resolve(fileName),
            Times.Once);
    }

    [Fact]
    public async Task ShowDocumentAsync_SupportedRenderer_CallsRenderAsync()
    {
        const string fileName =
            "document.csv";

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        using var form =
            new DocumentViewForm(
                resolver.Object);

        using var stream =
            CreateDocumentStream();

        await form.ShowDocumentAsync(
            stream,
            fileName);

        resolver.Verify(
            x => x.Resolve(fileName),
            Times.Once);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                stream,
                fileName,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowDocumentAsync_RendererIsCancelled_PropagatesCancellation()
    {
        const string fileName =
            "document.csv";

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        renderer
            .Setup(x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationToken));

        using var form =
            new DocumentViewForm(
                resolver.Object);

        using var stream =
            CreateDocumentStream();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                form.ShowDocumentAsync(
                    stream,
                    fileName,
                    cancellationToken));

        resolver.Verify(
            x => x.Resolve(fileName),
            Times.Once);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                cancellationToken),
            Times.Once);
    }

    private static MemoryStream CreateDocumentStream()
    {
        return new MemoryStream(
            "Id,Name\r\n1,Alice\r\n"u8.ToArray());
    }
}

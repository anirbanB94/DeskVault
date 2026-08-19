using DeskVault.UI.Forms;
using DeskVault.UI.Rendering;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class DocumentViewFormTests
{
    [Fact]
    public async Task ShowDocumentAsync_UnsupportedRenderer_PropagatesNotSupportedException()
    {
        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        resolver
            .Setup(x => x.Resolve("document.pdf"))
            .Throws(
                new NotSupportedException(
                    "No document renderer is available for 'document.pdf'."));

        using var form =
            new DocumentViewForm(
                resolver.Object);

        using var stream =
            new MemoryStream(
                "test document"u8.ToArray());

        NotSupportedException exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () =>
                    form.ShowDocumentAsync(
                        stream,
                        "document.pdf"));

        Assert.Equal(
            "No document renderer is available for 'document.pdf'.",
            exception.Message);

        resolver.Verify(
            x => x.Resolve("document.pdf"),
            Times.Once);
    }

    [Fact]
    public async Task ShowDocumentAsync_SupportedRenderer_CallsRenderAsync()
    {
        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        resolver
            .Setup(x => x.Resolve("document.csv"))
            .Returns(renderer.Object);

        using var form =
            new DocumentViewForm(
                resolver.Object);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        await form.ShowDocumentAsync(
            stream,
            "document.csv");

        resolver.Verify(
            x => x.Resolve("document.csv"),
            Times.Once);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                stream,
                "document.csv",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShowDocumentAsync_RendererIsCancelled_PropagatesCancellation()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        resolver
            .Setup(x => x.Resolve("document.csv"))
            .Returns(renderer.Object);

        renderer
            .Setup(x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                "document.csv",
                cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationToken));

        using var form =
            new DocumentViewForm(
                resolver.Object);

        using var stream =
            new MemoryStream(
                "Id,Name\r\n1,Alice\r\n"u8.ToArray());

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                form.ShowDocumentAsync(
                    stream,
                    "document.csv",
                    cancellationToken));

        resolver.Verify(
            x => x.Resolve("document.csv"),
            Times.Once);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                "document.csv",
                cancellationToken),
            Times.Once);
    }
}

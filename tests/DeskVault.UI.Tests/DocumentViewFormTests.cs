using DeskVault.UI.Forms;
using DeskVault.UI.Rendering;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
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
            CreateForm(resolver.Object);

        using var stream =
            CreateDocumentStream();

        NotSupportedException exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () =>
                    form.ShowDocumentAsync(stream, fileName, TestContext.Current.CancellationToken));

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
            CreateForm(resolver.Object);

        using var stream =
            CreateDocumentStream();

        await form.ShowDocumentAsync(stream, fileName, TestContext.Current.CancellationToken);

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
    public async Task ShowDocumentAsync_AlreadyCancelled_DoesNotResolveRenderer()
    {
        const string fileName = "document.csv";

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

        using var form =
            CreateForm(resolver.Object);

        using var stream =
            CreateDocumentStream();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => form.ShowDocumentAsync(
                stream,
                fileName,
                cancellationToken));

        resolver.Verify(
            x => x.Resolve(fileName),
            Times.Never);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.False(form.Visible);
    }

    [Fact]
    public async Task ClearPresentation_DisposesRenderedControlsAndClearsSensitiveMetadata()
    {
        const string fileName = "confidential-report.csv";

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        var renderedControl =
            new TextBox
            {
                Text = "Sensitive document content"
            };

        renderer
            .Setup(x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Control contentHost,
                Stream documentStream,
                string requestedFileName,
                CancellationToken cancellationToken) =>
            {
                contentHost.Controls.Add(renderedControl);

                return Task.CompletedTask;
            });

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        using var form =
            CreateForm(resolver.Object);

        using var stream =
            CreateDocumentStream();

        await form.ShowDocumentAsync(
            stream,
            fileName,
            TestContext.Current.CancellationToken);

        Panel contentPanel =
            Assert.IsType<Panel>(
                Assert.Single(
                    form.Controls.Find(
                        "documentContentPanel",
                        true)));

        Label titleLabel =
            Assert.IsType<Label>(
                Assert.Single(
                    form.Controls.Find(
                        "documentTitleLabel",
                        true)));

        Label metadataLabel =
            Assert.IsType<Label>(
                Assert.Single(
                    form.Controls.Find(
                        "documentMetadataLabel",
                        true)));

        Assert.Equal(fileName, titleLabel.Text);
        Assert.NotEmpty(contentPanel.Controls);
        Assert.False(renderedControl.IsDisposed);

        form.ClearPresentation();

        Assert.True(renderedControl.IsDisposed);
        Assert.Empty(contentPanel.Controls);
        Assert.Empty(titleLabel.Text);
        Assert.Empty(metadataLabel.Text);
        Assert.False(form.Visible);
    }

    [Fact]
    public async Task ShowDocumentAsync_ClearsPreviousRenderedContentBeforeRenderingNextDocument()
    {
        const string fileName = "document.csv";

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        var previousControl =
            new TextBox
            {
                Text = "Previous document content"
            };

        int renderCount = 0;

        renderer
            .Setup(x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Control contentHost,
                Stream documentStream,
                string requestedFileName,
                CancellationToken cancellationToken) =>
            {
                if (renderCount++ == 0)
                {
                    contentHost.Controls.Add(previousControl);
                }
                else
                {
                    Assert.True(previousControl.IsDisposed);
                    Assert.Empty(contentHost.Controls);
                }

                return Task.CompletedTask;
            });

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        using var form =
            CreateForm(resolver.Object);

        using var firstStream =
            CreateDocumentStream();

        using var secondStream =
            CreateDocumentStream();

        await form.ShowDocumentAsync(
            firstStream,
            fileName,
            TestContext.Current.CancellationToken);

        await form.ShowDocumentAsync(
            secondStream,
            fileName,
            TestContext.Current.CancellationToken);

        Assert.True(previousControl.IsDisposed);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ShowDocumentAsync_CancelledDuringRendering_DoesNotShowForm()
    {
        const string fileName = "document.csv";

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        renderer
            .Setup(x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Control contentHost,
                Stream documentStream,
                string requestedFileName,
                CancellationToken cancellationToken) =>
            {
                contentHost.Controls.Add(
                    new TextBox
                    {
                        Text = "Sensitive rendered content"
                    });

                cancellationTokenSource.Cancel();

                return Task.CompletedTask;
            });

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        using var form =
            CreateForm(resolver.Object);

        using var stream =
            CreateDocumentStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => form.ShowDocumentAsync(
                stream,
                fileName,
                cancellationTokenSource.Token));

        Assert.False(form.Visible);
    }

    [Fact]
    public async Task ShowDocumentAsync_ClearedDuringRendering_DoesNotRetainLateControls()
    {
        const string fileName = "document.csv";

        DocumentViewForm? form = null;

        var lateControl =
            new TextBox
            {
                Text = "Late sensitive content"
            };

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        var renderer =
            new Mock<IDocumentContentRenderer>();

        renderer
            .Setup(x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()))
            .Returns((
                Control contentHost,
                Stream documentStream,
                string requestedFileName,
                CancellationToken cancellationToken) =>
            {
                // Simulate a lock occurring while the renderer is executing.
                form!.ClearPresentation();

                // Simulate renderer work attempting to add content afterwards.
                contentHost.Controls.Add(lateControl);

                return Task.CompletedTask;
            });

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        using var documentViewForm =
            CreateForm(resolver.Object);

        form = documentViewForm;

        using var stream =
            CreateDocumentStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => documentViewForm.ShowDocumentAsync(
                stream,
                fileName,
                TestContext.Current.CancellationToken));

        Panel contentPanel =
            Assert.IsType<Panel>(
                Assert.Single(
                    documentViewForm.Controls.Find(
                        "documentContentPanel",
                        true)));

        Assert.True(lateControl.IsDisposed);
        Assert.Empty(contentPanel.Controls);
        Assert.False(documentViewForm.Visible);
    }

    [Fact]
    public void InformationAndRemovalDialogs_UseSessionBoundCoordinator()
    {
        const long expectedSessionRevision = 47;

        List<string> dialogTitles = [];

        int dialogCount = 0;

        var coordinator =
            new Mock<ISessionBoundDialogCoordinator>();

        coordinator
            .Setup(x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedSessionRevision,
                It.IsAny<IWin32Window?>()))
            .Callback<Form, long, IWin32Window?>(
                (dialog, _, _) =>
                    dialogTitles.Add(dialog.Text))
            .Returns(() =>
                ++dialogCount == 1
                    ? DialogResult.OK
                    : DialogResult.Yes);

        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        using var form =
            new DocumentViewForm(
                resolver.Object,
                coordinator.Object);

        form.ShowDocumentInformation(
            "Confidential report",
            "confidential.csv",
            "CSV",
            DateTime.UtcNow,
            "Processed",
            "test-sha256",
            expectedSessionRevision);

        bool confirmed =
            form.ConfirmRemoval(
                "confidential.csv",
                expectedSessionRevision);

        Assert.True(confirmed);

        Assert.Equal(
            [
                UiMessages.DocumentInformation,
            UiMessages.RemoveDocumentTitle
            ],
            dialogTitles);

        coordinator.Verify(
            x => x.ShowDialog(
                It.IsAny<Form>(),
                expectedSessionRevision,
                form),
            Times.Exactly(2));
    }

    private static MemoryStream CreateDocumentStream()
    {
        return new MemoryStream(
            "Id,Name\r\n1,Alice\r\n"u8.ToArray());
    }

    private static DocumentViewForm CreateForm(
        IDocumentContentRendererResolver resolver)
    {
        return new DocumentViewForm(
            resolver,
            new Mock<ISessionBoundDialogCoordinator>().Object);
    }
}

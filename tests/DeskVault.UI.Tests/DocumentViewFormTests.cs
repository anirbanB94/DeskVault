using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Rendering;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using Moq;
using System.Reflection;

namespace DeskVault.UI.Tests;

public sealed class DocumentViewFormTests
{
    [Fact]
    public void DocumentContextMenu_ContainsOnlyDocumentSpecificActions()
    {
        // Arrange
        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        using var form =
            CreateForm(resolver.Object);

        ContextMenuStrip documentContextMenu =
            GetPrivateField<ContextMenuStrip>(
                form,
                "documentContextMenu");

        // Act
        string[] actualMenuItems =
            [.. documentContextMenu.Items
                .Cast<ToolStripItem>()
                .Select(item => item.Name ?? string.Empty)];

        // Assert
        Assert.Equal(
            [
                "documentInformationMenuItem",
                "removeDocumentMenuItem"
            ],
            actualMenuItems);

        Assert.Empty(
            form.Controls.Find(
                "backButton",
                true));

        Assert.Empty(
            form.Controls.Find(
                "aiButton",
                true));

        Assert.Empty(
            form.Controls.Find(
                "closeButton",
                true));

        Assert.Empty(
            form.Controls.Find(
                "workspaceMenuButton",
                true));

        Assert.Equal(
            UiMessages.DocumentViewTitle,
            form.Text);

        Button documentActionsButton =
            GetPrivateField<Button>(
                form,
                "documentActionsButton");

        Assert.Equal(
            UiMessages.DocumentActionsButton,
            documentActionsButton.Text);
    }

    [Fact]
    public void DocumentContextMenuActions_RaiseDocumentSpecificRequests()
    {
        // Arrange
        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        using var form =
            CreateForm(resolver.Object);

        List<string> observedRequests = [];

        form.DocumentInformationRequested +=
            (_, _) =>
                observedRequests.Add(
                    nameof(form.DocumentInformationRequested));

        form.RemoveDocumentRequested +=
            (_, _) =>
                observedRequests.Add(
                    nameof(form.RemoveDocumentRequested));

        ToolStripMenuItem documentInformationMenuItem =
            GetPrivateField<ToolStripMenuItem>(
                form,
                "documentInformationMenuItem");

        ToolStripMenuItem removeDocumentMenuItem =
            GetPrivateField<ToolStripMenuItem>(
                form,
                "removeDocumentMenuItem");

        // Act
        documentInformationMenuItem.PerformClick();
        removeDocumentMenuItem.PerformClick();

        // Assert
        Assert.Equal(
            [
                nameof(form.DocumentInformationRequested),
                nameof(form.RemoveDocumentRequested)
            ],
            observedRequests);
    }

    [Fact]
    public void DocumentPresentationComponents_DoNotDependOnRendererImplementations()
    {
        // Arrange
        Type[] presentationComponents =
        [
            typeof(DocumentViewForm),
            typeof(DocumentWorkspacePresenter),
            typeof(WorkspaceDocumentPresentationFactory)
        ];

        // Act / Assert
        foreach (Type componentType in presentationComponents)
        {
            Type[] constructorDependencies =
                [.. componentType.GetConstructors(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .SelectMany(constructor =>
                        constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType)];

            Type[] fieldDependencies =
                [.. componentType.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly)
                    .Select(field => field.FieldType)];

            IEnumerable<Type> dependencies =
                constructorDependencies.Concat(fieldDependencies);

            Assert.DoesNotContain(
                dependencies,
                dependencyType =>
                    typeof(IDocumentContentRenderer)
                        .IsAssignableFrom(dependencyType));
        }

        Assert.Contains(
            typeof(IDocumentContentRendererResolver),
            typeof(DocumentViewForm)
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void SetDocumentChromeVisible_TogglesDocumentHeaderVisibility()
    {
        // Arrange
        var resolver =
            new Mock<IDocumentContentRendererResolver>();

        using var form =
            CreateForm(resolver.Object);

        form.Show();

        Panel workspaceHeaderPanel =
            GetPrivateField<Panel>(
                form,
                "workspaceHeaderPanel");

        Assert.True(workspaceHeaderPanel.Visible);

        // Act: hide the document chrome.
        form.SetDocumentChromeVisible(false);

        // Assert: the complete document header is hidden.
        Assert.False(workspaceHeaderPanel.Visible);

        // Act: restore the document chrome.
        form.SetDocumentChromeVisible(true);

        // Assert: the document header is visible again.
        Assert.True(workspaceHeaderPanel.Visible);
    }

    [Fact]
    public void ShowUnsupportedPreview_UpdatesContentWithoutShowingOrActivatingForm()
    {
        // Arrange
        const string previewMessage = "This file cannot be previewed.";
        var resolver = new Mock<IDocumentContentRendererResolver>();
        using var form = CreateForm(resolver.Object);

        int shownEventCount = 0;
        int activatedEventCount = 0;

        form.Shown += (_, _) => shownEventCount++;
        form.Activated += (_, _) => activatedEventCount++;

        Label previewMessageLabel = Assert.IsType<Label>(
            Assert.Single(form.Controls.Find(
                "unsupportedDocumentPreviewMessageLabel",
                true)));

        // Act
        form.ShowUnsupportedPreview(previewMessage);

        // Assert
        Assert.Equal(previewMessage, previewMessageLabel.Text);
        Assert.False(form.Visible);
        Assert.Equal(0, shownEventCount);
        Assert.Equal(0, activatedEventCount);
    }

    [Fact]
    public void ShowUnsupportedPreview_RetainsExternalOpenAction()
    {
        // Arrange
        const string previewMessage = "This file cannot be previewed.";
        var resolver = new Mock<IDocumentContentRendererResolver>();
        using var form = CreateForm(resolver.Object);

        int openExternallyRequestCount = 0;

        form.OpenExternallyRequested +=
            (_, _) => openExternallyRequestCount++;

        Button openExternallyButton = Assert.IsType<Button>(
            Assert.Single(form.Controls.Find(
                "openExternallyButton",
                true)));

        // Act
        form.ShowUnsupportedPreview(previewMessage);

        // The test explicitly displays the Form so that the button
        // is selectable. ShowUnsupportedPreview itself must not do this.
        form.Show();

        openExternallyButton.PerformClick();

        // Assert
        Assert.Equal(1, openExternallyRequestCount);
    }

    [Fact]
    public void Close_UsesNormalFormClosingSemantics()
    {
        // Arrange
        var resolver = new Mock<IDocumentContentRendererResolver>();
        using var form = CreateForm(resolver.Object);

        bool formClosingRaised = false;
        bool? closeCancelled = null;

        form.FormClosing += (_, e) =>
        {
            formClosingRaised = true;
            closeCancelled = e.Cancel;
        };

        form.Show();

        // Act
        form.Close();

        // Assert
        Assert.True(formClosingRaised);
        Assert.False(closeCancelled);
        Assert.True(form.IsDisposed);
    }

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
    public async Task ShowDocumentAsync_SupportedRenderer_PassesOriginalStreamAndActiveTokenWithoutShowingOrActivatingForm()
    {
        // Arrange
        const string fileName =
            "document.csv";

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken activeCancellationToken =
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

        int shownEventCount = 0;
        int activatedEventCount = 0;

        form.Shown +=
            (_, _) => shownEventCount++;

        form.Activated +=
            (_, _) => activatedEventCount++;

        // Act
        await form.ShowDocumentAsync(
            stream,
            fileName,
            activeCancellationToken);

        // Assert
        resolver.Verify(
            x => x.Resolve(fileName),
            Times.Once);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.Is<Stream>(
                    actualStream =>
                        ReferenceEquals(actualStream, stream)),
                fileName,
                It.Is<CancellationToken>(
                    actualToken =>
                        actualToken == activeCancellationToken)),
            Times.Once);

        // Verify there was exactly one renderer invocation in total.
        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The caller retains ownership of the original stream.
        Assert.True(stream.CanRead);

        Assert.False(form.Visible);
        Assert.Equal(0, shownEventCount);
        Assert.Equal(0, activatedEventCount);
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

        Assert.Equal(fileName, titleLabel.Text);
        Assert.NotEmpty(contentPanel.Controls);
        Assert.False(renderedControl.IsDisposed);

        form.ClearPresentation();

        Assert.True(renderedControl.IsDisposed);
        Assert.Empty(contentPanel.Controls);
        Assert.Empty(titleLabel.Text);
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
    public async Task ShowDocumentAsync_RendererFails_DisposesUncommittedRenderHostAndChildControls()
    {
        const string fileName = "document.csv";

        Panel? renderHost = null;
        TextBox? renderedControl = null;

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
                renderHost = Assert.IsType<Panel>(contentHost);

                renderedControl =
                    new TextBox
                    {
                        Text = "Partially rendered document content"
                    };

                contentHost.Controls.Add(renderedControl);

                return Task.FromException(
                    new InvalidOperationException("Renderer failure."));
            });

        resolver
            .Setup(x => x.Resolve(fileName))
            .Returns(renderer.Object);

        using var form =
            CreateForm(resolver.Object);

        using var stream =
            CreateDocumentStream();

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => form.ShowDocumentAsync(
                    stream,
                    fileName,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("Renderer failure.", exception.Message);

        resolver.Verify(
            x => x.Resolve(fileName),
            Times.Once);

        renderer.Verify(
            x => x.RenderAsync(
                It.IsAny<Control>(),
                It.IsAny<Stream>(),
                fileName,
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(renderHost);
        Assert.True(renderHost!.IsDisposed);

        Assert.NotNull(renderedControl);
        Assert.True(renderedControl!.IsDisposed);

        // The view must not take ownership of the caller's stream.
        Assert.True(stream.CanRead);
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

    [Fact]
    public void ParentChanged_DoesNotHideDocumentHeader()
    {
        // Arrange
        var resolver = new Mock<IDocumentContentRendererResolver>();

        using var hostForm = new Form();
        using var documentForm = CreateForm(resolver.Object);

        var hostPanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        hostForm.Controls.Add(hostPanel);
        hostForm.Show();

        Panel workspaceHeaderPanel = Assert.IsType<Panel>(
            Assert.Single(documentForm.Controls.Find(
                "workspaceHeaderPanel",
                true)));

        // Act
        documentForm.TopLevel = false;
        documentForm.FormBorderStyle = FormBorderStyle.None;
        documentForm.Dock = DockStyle.Fill;
        documentForm.ShowInTaskbar = false;

        hostPanel.Controls.Add(documentForm);
        documentForm.Show();

        // Assert
        Assert.True(workspaceHeaderPanel.Visible);
    }

    private static T GetPrivateField<T>(
        DocumentViewForm form,
        string fieldName)
        where T : class
    {
        FieldInfo? field =
            typeof(DocumentViewForm).GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        Assert.NotNull(field);

        return Assert.IsType<T>(
            field!.GetValue(form));
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

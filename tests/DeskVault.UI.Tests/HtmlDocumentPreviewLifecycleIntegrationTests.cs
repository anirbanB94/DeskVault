using AngleSharp.Html.Parser;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Documents;
using DeskVault.UI.Forms;
using DeskVault.UI.Hosting;
using DeskVault.UI.Rendering.HtmlDocumentRendering;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies HTML preview behavior through the production workspace,
/// presentation, rendering and security-session composition.
/// </summary>
public sealed class HtmlDocumentPreviewLifecycleIntegrationTests
{
    private static readonly TimeSpan OperationTimeout =
        TimeSpan.FromSeconds(30);

    /// <summary>
    /// Verifies that HTML and HTM documents are presented through the
    /// production workspace factory using the dedicated InPrivate profile.
    /// </summary>
    [Theory]
    [InlineData("production-preview.html")]
    [InlineData("production-preview.htm")]
    public Task WorkspacePresentation_RendersHtmlAndHtmUsingDedicatedInPrivateProfile(
        string fileName)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        return RunOnStaThreadAsync(
            async ownerForm =>
            {
                // Arrange: build the real production host.
                await using ProductionSecuritySessionTestHost testHost =
                    ProductionSecuritySessionTestHost.Create();

                ImportedHtmlDocument importedDocument =
                    await InitializeHostAndImportHtmlDocumentAsync(
                        testHost,
                        fileName,
                        cancellationToken);

                IServiceProvider services =
                    testHost.Services;

                IApplicationSecuritySession securitySession =
                    services.GetRequiredService<IApplicationSecuritySession>();

                ApplicationSecuritySessionSnapshot owningSnapshot =
                    securitySession.CurrentSnapshot;

                Assert.Equal(
                    ApplicationSecuritySessionState.Authenticated,
                    owningSnapshot.State);

                Assert.True(
                    owningSnapshot.IsProtectedAccessAvailable);

                // Use the actual production presentation factory.
                IWorkspaceDocumentPresentationFactory presentationFactory =
                    services.GetRequiredService<
                        IWorkspaceDocumentPresentationFactory>();

                WorkspaceDocumentPresentation presentation =
                    presentationFactory.Create(
                        importedDocument.DocumentId,
                        owningSnapshot.Revision);

                string? profileFolderPath = null;

                try
                {
                    // The factory must create the real DocumentViewForm.
                    DocumentViewForm documentView =
                        Assert.IsType<DocumentViewForm>(
                            presentation.View);

                    // Show the real form so WebView2 receives its UI handle
                    // and can run under the WinForms message loop.
                    presentation.HostedForm.Show(ownerForm);

                    // Open the imported encrypted document through the
                    // real DocumentWorkspacePresenter and OpenDocumentHandler.
                    await presentation.Presenter.OpenAsync(
                        importedDocument.DocumentId,
                        cancellationToken);

                    Assert.Equal(
                        importedDocument.DocumentId,
                        presentation.Presenter.CurrentDocumentId);

                    Assert.Equal(
                        importedDocument.Metadata.FileName,
                        presentation.Presenter.CurrentDocumentFileName);

                    // Inspect the WebView2 control hosted by DocumentViewForm.
                    WebView2 webView =
                        FindWebView(documentView);

                    Assert.NotNull(webView.CoreWebView2);

                    string renderedText =
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.body.innerText");

                    Assert.Contains(
                        importedDocument.HtmlMarker,
                        renderedText,
                        StringComparison.Ordinal);

                    // Verify that the embedded image was actually decoded
                    // by the WebView2 runtime.
                    bool imageCompleted = false;
                    int imageWidth = 0;
                    int imageHeight = 0;
                    string? renderedImageMarker = null;

                    for (int attempt = 0; attempt < 40; attempt++)
                    {
                        string imageStateJson =
                            await webView.CoreWebView2.ExecuteScriptAsync(
                                """
                                 (() => {
                                     const image =
                                         document.querySelector("#embedded-image");

                                     return {
                                         complete: image?.complete ?? false,
                                         naturalWidth: image?.naturalWidth ?? 0,
                                         naturalHeight: image?.naturalHeight ?? 0,
                                         alt: image?.alt ?? null
                                     };
                                 })()
                                 """);

                        using JsonDocument imageState =
                            JsonDocument.Parse(imageStateJson);

                        JsonElement image =
                            imageState.RootElement;

                        imageCompleted =
                            image.GetProperty("complete").GetBoolean();

                        imageWidth =
                            image.GetProperty("naturalWidth").GetInt32();

                        imageHeight =
                            image.GetProperty("naturalHeight").GetInt32();

                        renderedImageMarker =
                            image.GetProperty("alt").GetString();

                        if (imageCompleted
                            && imageWidth > 0
                            && imageHeight > 0)
                        {
                            break;
                        }

                        await Task.Delay(
                            50,
                            cancellationToken);
                    }

                    Assert.True(imageCompleted);
                    Assert.Equal(2, imageWidth);
                    Assert.Equal(2, imageHeight);

                    Assert.Equal(
                        importedDocument.ImageMarker,
                        renderedImageMarker);

                    byte[] embeddedImagePayload =
                        await ReadEmbeddedImagePayloadAsync(webView);

                    Assert.Contains(
                        importedDocument.ImageMarker,
                        Encoding.ASCII.GetString(embeddedImagePayload),
                        StringComparison.Ordinal);

                    // Verify the actual runtime profile configuration.
                    Assert.True(
                        webView.CoreWebView2.Profile.IsInPrivateModeEnabled);

                    profileFolderPath =
                        Path.GetFullPath(
                            webView.CoreWebView2.Environment.UserDataFolder);

                    Assert.True(
                        Directory.Exists(profileFolderPath));

                    // The production HTML profile uses a dedicated,
                    // unique user-data folder rather than the default
                    // persistent WebView2 / Markdown profile.
                    string expectedHtmlProfileRoot =
                        Path.GetFullPath(
                            Path.Combine(
                                Path.GetTempPath(),
                                "DeskVault",
                                "HtmlPreviewProfiles"));

                    string actualProfileRoot =
                        Path.GetFullPath(
                            Path.GetDirectoryName(profileFolderPath)!);

                    Assert.Equal(
                        expectedHtmlProfileRoot,
                        actualProfileRoot,
                        ignoreCase: true);

                    Assert.True(
                        Guid.TryParse(
                            Path.GetFileName(profileFolderPath),
                            out _));

                    // Preview must not modify the canonical encrypted artifact.
                    byte[] canonicalArtifactAfterPreview =
                        await File.ReadAllBytesAsync(
                            importedDocument.CanonicalArtifactPath,
                            cancellationToken);

                    Assert.Equal(
                        importedDocument.CanonicalArtifactBytes,
                        canonicalArtifactAfterPreview);

                    // Preview must not modify persisted document metadata.
                    IDocumentRepository documentRepository =
                        services.GetRequiredService<IDocumentRepository>();

                    Document metadataAfterPreview =
                        await documentRepository.GetByIdAsync(
                            importedDocument.DocumentId,
                            cancellationToken) ??
                        throw new InvalidOperationException(
                            "Document metadata could not be retrieved after preview.");

                    AssertDocumentMetadataUnchanged(
                        importedDocument.Metadata,
                        metadataAfterPreview);
                }
                finally
                {
                    // Disposing the workspace presentation releases the
                    // presenter and the actual DocumentViewForm.
                    presentation.Dispose();

                    // Form disposal starts asynchronous HTML profile cleanup.
                    // Wait for the actual physical deletion postcondition.
                    if (profileFolderPath is not null)
                    {
                        await WaitForProfileCleanupCompletionAsync(
                            profileFolderPath,
                            OperationTimeout,
                            cancellationToken);
                    }
                }
                IDocumentRepository documentRepositoryAfterClosure =
                    services.GetRequiredService<IDocumentRepository>();

                Document metadataAfterClosure =
                    await documentRepositoryAfterClosure.GetByIdAsync(
                        importedDocument.DocumentId,
                        cancellationToken) ??
                    throw new InvalidOperationException(
                        "Document metadata could not be retrieved after presentation closure.");

                AssertDocumentMetadataUnchanged(
                    importedDocument.Metadata,
                    metadataAfterClosure);
            });
    }

    /// <summary>
    /// Verifies that production session revocation clears a rendered HTML
    /// preview, completes profile teardown, and does not restore the old
    /// content after re-authentication.
    /// </summary>
    [Fact]
    public Task SessionRevocation_ClearsHtmlPreviewAndRequiresNewPresentationAfterReauthentication()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        return RunOnStaThreadAsync(
            async ownerForm =>
            {
                // Arrange: start the real production host and import
                // an encrypted HTML document.
                await using ProductionSecuritySessionTestHost testHost =
                    ProductionSecuritySessionTestHost.Create();

                ImportedHtmlDocument revokedDocument =
                    await InitializeHostAndImportHtmlDocumentAsync(
                        testHost,
                        "revoked-preview.html",
                        cancellationToken);

                IServiceProvider services =
                    testHost.Services;

                DeskVaultStartupCoordinator startupCoordinator =
                    services.GetRequiredService<DeskVaultStartupCoordinator>();

                IApplicationSecuritySession securitySession =
                    services.GetRequiredService<IApplicationSecuritySession>();

                IApplicationSecuritySessionLifecycleService lifecycleService =
                    services.GetRequiredService<
                        IApplicationSecuritySessionLifecycleService>();

                IWorkspaceDocumentPresentationFactory presentationFactory =
                    services.GetRequiredService<
                        IWorkspaceDocumentPresentationFactory>();

                ApplicationSecuritySessionSnapshot owningSnapshot =
                    securitySession.CurrentSnapshot;

                Assert.Equal(
                    ApplicationSecuritySessionState.Authenticated,
                    owningSnapshot.State);

                // Use the actual production presentation factory.
                WorkspaceDocumentPresentation revokedPresentation =
                    presentationFactory.Create(
                        revokedDocument.DocumentId,
                        owningSnapshot.Revision);

                HtmlDocumentPreviewSurface? revokedSurface = null;
                WorkspaceDocumentPresentation? newPresentation = null;
                HtmlDocumentPreviewSurface? newSurface = null;

                string? revokedProfilePath = null;
                string? newProfilePath = null;

                try
                {
                    // Act: show the actual production DocumentViewForm.
                    DocumentViewForm revokedView =
                        Assert.IsType<DocumentViewForm>(
                            revokedPresentation.View);

                    revokedPresentation.HostedForm.Show(ownerForm);

                    // Open through the actual production presenter and encrypted reader.
                    await revokedPresentation.Presenter.OpenAsync(
                        revokedDocument.DocumentId,
                        cancellationToken);

                    Assert.Equal(
                        revokedDocument.DocumentId,
                        revokedPresentation.Presenter.CurrentDocumentId);

                    // Capture the real WebView2 surface and its profile.
                    WebView2 revokedWebView =
                        FindWebView(revokedView);

                    revokedSurface =
                        FindPreviewSurface(revokedView);

                    Assert.NotNull(revokedWebView.CoreWebView2);

                    Assert.True(
                        revokedWebView.CoreWebView2.Profile.IsInPrivateModeEnabled);

                    revokedProfilePath =
                        Path.GetFullPath(
                            revokedWebView.CoreWebView2.Environment.UserDataFolder);

                    Assert.True(
                        Directory.Exists(revokedProfilePath));

                    // Verify that the original HTML content was really rendered.
                    string revokedRenderedText =
                        await revokedWebView.CoreWebView2.ExecuteScriptAsync(
                            "document.body.innerText");

                    Assert.Contains(
                        revokedDocument.HtmlMarker,
                        revokedRenderedText,
                        StringComparison.Ordinal);

                    // Verify that the embedded PNG contains its unique marker.
                    byte[] revokedImagePayload =
                        await ReadEmbeddedImagePayloadAsync(
                            revokedWebView);

                    Assert.Contains(
                        revokedDocument.ImageMarker,
                        Encoding.ASCII.GetString(revokedImagePayload),
                        StringComparison.Ordinal);

                    string revokedImageWidth =
                        await revokedWebView.CoreWebView2.ExecuteScriptAsync(
                            "document.querySelector('#embedded-image').naturalWidth");

                    Assert.Equal("2", revokedImageWidth);

                    // Act: revoke protected access using the authoritative
                    // production application-session lifecycle.
                    lifecycleService.Lock();

                    ApplicationSecuritySessionSnapshot lockedSnapshot =
                        securitySession.CurrentSnapshot;

                    // Assert: authorization was revoked.
                    Assert.Equal(
                        ApplicationSecuritySessionState.Locked,
                        lockedSnapshot.State);

                    Assert.False(
                        lockedSnapshot.IsProtectedAccessAvailable);

                    Assert.True(
                        lockedSnapshot.Revision > owningSnapshot.Revision);

                    // The real DocumentViewForm dispatches synchronously when
                    // revocation occurs on its owning UI thread.
                    Assert.Null(
                        revokedPresentation.Presenter.CurrentDocumentId);

                    Assert.Null(
                        revokedPresentation.Presenter.CurrentDocumentFileName);

                    Assert.False(
                        ContainsControl(
                            revokedView,
                            revokedWebView));

                    // Await the actual cleanup task. Do not treat Control.Dispose()
                    // returning as proof that physical profile deletion completed.
                    await revokedSurface.DisposeAsync();

                    Assert.True(revokedSurface.IsDisposed);
                    Assert.True(revokedWebView.IsDisposed);

                    Assert.False(
                        Directory.Exists(revokedProfilePath),
                        "The revoked HTML preview profile was not physically deleted.");

                    IDocumentRepository documentRepository =
                        services.GetRequiredService<IDocumentRepository>();



                    // Act: authenticate a new security-session revision.
                    bool reauthenticated =
                        await startupCoordinator.AuthenticateAsync(
                            cancellationToken);

                    Assert.True(reauthenticated);

                    Document metadataAfterRevocation =
                        await documentRepository.GetByIdAsync(
                            revokedDocument.DocumentId,
                            cancellationToken) ??
                        throw new InvalidOperationException(
                            "Revoked document metadata could not be retrieved after profile teardown.");

                    AssertDocumentMetadataUnchanged(
                        revokedDocument.Metadata,
                        metadataAfterRevocation);

                    ApplicationSecuritySessionSnapshot reauthenticatedSnapshot =
                        securitySession.CurrentSnapshot;

                    Assert.Equal(
                        ApplicationSecuritySessionState.Authenticated,
                        reauthenticatedSnapshot.State);

                    Assert.True(
                        reauthenticatedSnapshot.IsProtectedAccessAvailable);

                    Assert.True(
                        reauthenticatedSnapshot.Revision > lockedSnapshot.Revision);

                    // Assert: the old presenter remains bound to the invalidated
                    // revision and does not restore its previous document.
                    Assert.Null(
                        revokedPresentation.Presenter.CurrentDocumentId);

                    Assert.Null(
                        revokedPresentation.Presenter.CurrentDocumentFileName);

                    Assert.False(
                        ContainsControl(
                            revokedView,
                            revokedWebView));

                    // Arrange a different encrypted HTML document.
                    ImportedHtmlDocument independentlyAuthorizedDocument =
                        await ImportHtmlDocumentAsync(
                            testHost,
                            "new-authorized-preview.html",
                            cancellationToken);

                    Assert.NotEqual(
                        revokedDocument.DocumentId,
                        independentlyAuthorizedDocument.DocumentId);

                    Assert.NotEqual(
                        revokedDocument.HtmlMarker,
                        independentlyAuthorizedDocument.HtmlMarker);

                    Assert.NotEqual(
                        revokedDocument.ImageMarker,
                        independentlyAuthorizedDocument.ImageMarker);

                    // Act: create a completely new presentation bound to
                    // the newer authenticated security-session revision.
                    newPresentation =
                        presentationFactory.Create(
                            independentlyAuthorizedDocument.DocumentId,
                            reauthenticatedSnapshot.Revision);

                    DocumentViewForm newView =
                        Assert.IsType<DocumentViewForm>(
                            newPresentation.View);

                    newPresentation.HostedForm.Show(ownerForm);

                    await newPresentation.Presenter.OpenAsync(
                        independentlyAuthorizedDocument.DocumentId,
                        cancellationToken);

                    WebView2 newWebView =
                        FindWebView(newView);

                    newSurface =
                        FindPreviewSurface(newView);

                    Assert.NotNull(newWebView.CoreWebView2);

                    Assert.True(
                        newWebView.CoreWebView2.Profile.IsInPrivateModeEnabled);

                    newProfilePath =
                        Path.GetFullPath(
                            newWebView.CoreWebView2.Environment.UserDataFolder);

                    Assert.True(
                        Directory.Exists(newProfilePath));

                    Assert.NotEqual(
                        revokedProfilePath,
                        newProfilePath);

                    // Assert: the independent preview renders its new content,
                    // not the content from the revoked presentation.
                    string newRenderedText =
                        await newWebView.CoreWebView2.ExecuteScriptAsync(
                            "document.body.innerText");

                    Assert.Contains(
                        independentlyAuthorizedDocument.HtmlMarker,
                        newRenderedText,
                        StringComparison.Ordinal);

                    Assert.DoesNotContain(
                        revokedDocument.HtmlMarker,
                        newRenderedText,
                        StringComparison.Ordinal);

                    byte[] newImagePayload =
                        await ReadEmbeddedImagePayloadAsync(
                            newWebView);

                    string newImagePayloadText =
                        Encoding.ASCII.GetString(newImagePayload);

                    Assert.Contains(
                        independentlyAuthorizedDocument.ImageMarker,
                        newImagePayloadText,
                        StringComparison.Ordinal);

                    Assert.DoesNotContain(
                        revokedDocument.ImageMarker,
                        newImagePayloadText,
                        StringComparison.Ordinal);

                    // Verify the original encrypted artifact was not changed
                    // by preview, lock, cleanup, or re-authentication.
                    byte[] canonicalArtifactAfterLifecycle =
                        await File.ReadAllBytesAsync(
                            revokedDocument.CanonicalArtifactPath,
                            cancellationToken);

                    Assert.Equal(
                        revokedDocument.CanonicalArtifactBytes,
                        canonicalArtifactAfterLifecycle);

                    // Close the independent presentation and await its real
                    // profile teardown before inspecting its disk-backed data.
                    newPresentation.Dispose();

                    await newSurface.DisposeAsync();

                    Assert.True(newSurface.IsDisposed);

                    Assert.False(
                        Directory.Exists(newProfilePath),
                        "The independently authorized HTML preview profile was not physically deleted.");

                    Document newMetadataAfterClosure =
                        await documentRepository.GetByIdAsync(
                            independentlyAuthorizedDocument.DocumentId,
                            cancellationToken) ??
                        throw new InvalidOperationException(
                            "Independent preview metadata could not be retrieved after profile teardown.");

                    AssertDocumentMetadataUnchanged(
                        independentlyAuthorizedDocument.Metadata,
                        newMetadataAfterClosure);

                    byte[] newCanonicalArtifactAfterClosure =
                        await File.ReadAllBytesAsync(
                            independentlyAuthorizedDocument.CanonicalArtifactPath,
                            cancellationToken);

                    Assert.Equal(
                        independentlyAuthorizedDocument.CanonicalArtifactBytes,
                        newCanonicalArtifactAfterClosure);

                    newPresentation = null;
                }
                finally
                {
                    if (newPresentation is not null)
                    {
                        newPresentation.Dispose();

                        if (newSurface is not null)
                        {
                            await newSurface.DisposeAsync();
                        }
                    }

                    revokedPresentation.Dispose();

                    if (revokedSurface is not null)
                    {
                        await revokedSurface.DisposeAsync();
                    }
                }
            });
    }

    /// <summary>
    /// Verifies that structurally malformed but recoverable HTML is
    /// normalized and presented through the production workspace without
    /// truncating content following the malformed markup.
    /// </summary>
    [Fact]
    public Task WorkspacePresentation_RendersRecoverableMalformedHtmlWithoutTruncatingContent()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string beforeMarker =
            $"DESKVAULT_HTML_BEFORE_PARSE_ERROR_{Guid.NewGuid():N}";

        string afterMarker =
            $"DESKVAULT_HTML_AFTER_PARSE_ERROR_{Guid.NewGuid():N}";

        string malformedHtml =
            $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <title>Recoverable malformed HTML</title>
            </head>
            <body>
                <section>
                    <p>{{beforeMarker}}</article>
                    <p>{{afterMarker}}</p>
                </section>
            </body>
            </html>
            """;

        // Verify that the fixture genuinely produces an HTML parse error.
        var parser =
            new HtmlParser(
                new HtmlParserOptions
                {
                    IsScripting = false
                });

        bool parseErrorOccurred = false;

        parser.Error +=
            (_, _) => parseErrorOccurred = true;

        parser.ParseDocument(malformedHtml);

        Assert.True(
            parseErrorOccurred,
            "The malformed HTML fixture must produce a parser error.");

        return RunOnStaThreadAsync(
            async ownerForm =>
            {
                // Arrange: use the real production host and encrypted import path.
                await using ProductionSecuritySessionTestHost testHost =
                    ProductionSecuritySessionTestHost.Create();

                ImportedHtmlDocument importedDocument =
                    await InitializeHostAndImportHtmlDocumentAsync(
                        testHost,
                        "recoverable-malformed-preview.html",
                        cancellationToken,
                        malformedHtml);

                IServiceProvider services =
                    testHost.Services;

                IApplicationSecuritySession securitySession =
                    services.GetRequiredService<IApplicationSecuritySession>();

                ApplicationSecuritySessionSnapshot owningSnapshot =
                    securitySession.CurrentSnapshot;

                Assert.Equal(
                    ApplicationSecuritySessionState.Authenticated,
                    owningSnapshot.State);

                Assert.True(
                    owningSnapshot.IsProtectedAccessAvailable);

                IWorkspaceDocumentPresentationFactory presentationFactory =
                    services.GetRequiredService<
                        IWorkspaceDocumentPresentationFactory>();

                WorkspaceDocumentPresentation presentation =
                    presentationFactory.Create(
                        importedDocument.DocumentId,
                        owningSnapshot.Revision);

                HtmlDocumentPreviewSurface? previewSurface = null;
                string? profileFolderPath = null;

                try
                {
                    // Act: open through the actual production workspace presentation.
                    DocumentViewForm documentView =
                        Assert.IsType<DocumentViewForm>(
                            presentation.View);

                    presentation.HostedForm.Show(ownerForm);

                    await presentation.Presenter.OpenAsync(
                        importedDocument.DocumentId,
                        cancellationToken);

                    Assert.Equal(
                        importedDocument.DocumentId,
                        presentation.Presenter.CurrentDocumentId);

                    previewSurface =
                        FindPreviewSurface(documentView);

                    WebView2 webView =
                        FindWebView(previewSurface);

                    Assert.NotNull(webView.CoreWebView2);

                    Assert.True(
                        webView.CoreWebView2.Profile.IsInPrivateModeEnabled);

                    Assert.False(
                        webView.CoreWebView2.Settings.IsScriptEnabled);

                    profileFolderPath =
                        Path.GetFullPath(
                            webView.CoreWebView2.Environment.UserDataFolder);

                    Assert.True(
                        Directory.Exists(profileFolderPath));

                    // Assert: both sides of the malformed markup remain visible.
                    string renderedText =
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.body.innerText");

                    Assert.Contains(
                        beforeMarker,
                        renderedText,
                        StringComparison.Ordinal);

                    Assert.Contains(
                        afterMarker,
                        renderedText,
                        StringComparison.Ordinal);

                    // The parser recovered both paragraphs rather than
                    // presenting only the content preceding the parse error.
                    string paragraphCount =
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.querySelectorAll('body section p').length");

                    Assert.Equal("2", paragraphCount);

                    string documentStructureAvailable =
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            "Boolean(document.documentElement && document.body)");

                    Assert.Equal("true", documentStructureAvailable);
                }
                finally
                {
                    presentation.Dispose();

                    if (previewSurface is not null)
                    {
                        await previewSurface.DisposeAsync();
                    }
                }

                string completedProfilePath =
                    Assert.IsType<string>(profileFolderPath);

                Assert.False(
                    Directory.Exists(completedProfilePath),
                    "The malformed-HTML preview profile was not deleted after teardown.");
            });
    }

    /// <summary>
    /// Verifies that locking the owning security session while the real HTML
    /// profile is initialized but before the renderer receives it prevents
    /// the pending presentation from continuing and waits for physical cleanup.
    /// </summary>
    [Fact]
    public Task SessionRevocation_DuringPendingHtmlProfileInitialization_RejectsStalePresentationAndWaitsForProfileCleanup()
    {
        var blockingProfileFactory =
            new BlockingHtmlDocumentWebView2ProfileSessionFactory();

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        return RunOnStaThreadAsync(
            async ownerForm =>
            {
                await using ProductionSecuritySessionTestHost testHost =
                    ProductionSecuritySessionTestHost.Create(
                        htmlPreviewProfileSessionFactory: blockingProfileFactory);

                ImportedHtmlDocument importedDocument =
                    await InitializeHostAndImportHtmlDocumentAsync(
                        testHost,
                        "pending-revocation-preview.html",
                        cancellationToken);

                IServiceProvider services =
                    testHost.Services;

                DeskVaultStartupCoordinator startupCoordinator =
                    services.GetRequiredService<DeskVaultStartupCoordinator>();

                IApplicationSecuritySession securitySession =
                    services.GetRequiredService<IApplicationSecuritySession>();

                IApplicationSecuritySessionLifecycleService lifecycleService =
                    services.GetRequiredService<
                        IApplicationSecuritySessionLifecycleService>();

                IWorkspaceDocumentPresentationFactory presentationFactory =
                    services.GetRequiredService<
                        IWorkspaceDocumentPresentationFactory>();

                ApplicationSecuritySessionSnapshot owningSnapshot =
                    securitySession.CurrentSnapshot;

                WorkspaceDocumentPresentation? presentation = null;
                HtmlDocumentPreviewSurface? previewSurface = null;
                Task? openTask = null;
                string? profileFolderPath = null;

                try
                {
                    // Arrange: create the real production document presentation.
                    presentation =
                        presentationFactory.Create(
                            importedDocument.DocumentId,
                            owningSnapshot.Revision);

                    DocumentViewForm documentView =
                        Assert.IsType<DocumentViewForm>(
                            presentation.View);

                    presentation.HostedForm.Show(ownerForm);

                    // Act: begin opening the encrypted HTML document.
                    // The profile factory will pause after the real WebView2
                    // profile has initialized and before it returns to the renderer.
                    openTask =
                        presentation.Presenter.OpenAsync(
                            importedDocument.DocumentId,
                            cancellationToken);

                    profileFolderPath =
                        await blockingProfileFactory.SessionInitialized.WaitAsync(
                            OperationTimeout,
                            cancellationToken);

                    previewSurface =
                        FindPreviewSurface(documentView);

                    WebView2 webView =
                        FindWebView(previewSurface);

                    // The production WebView2 profile has initialized.
                    Assert.NotNull(webView.CoreWebView2);

                    Assert.True(
                        webView.CoreWebView2.Profile.IsInPrivateModeEnabled);

                    Assert.True(
                        Directory.Exists(profileFolderPath));

                    // The wrapper is deliberately holding the initialized
                    // profile session. The renderer has not yet received it,
                    // applied its HTML policy, or started document navigation.

                    // Act: revoke authorization while rendering is still pending.
                    lifecycleService.Lock();

                    ApplicationSecuritySessionSnapshot lockedSnapshot =
                        securitySession.CurrentSnapshot;

                    // Assert: the authoritative session is locked.
                    Assert.Equal(
                        ApplicationSecuritySessionState.Locked,
                        lockedSnapshot.State);

                    Assert.False(
                        lockedSnapshot.IsProtectedAccessAvailable);

                    Assert.True(
                        lockedSnapshot.Revision > owningSnapshot.Revision);

                    // The presenter must clear its current document immediately.
                    Assert.Null(
                        presentation.Presenter.CurrentDocumentId);

                    Assert.Null(
                        presentation.Presenter.CurrentDocumentFileName);

                    // The UI clear path removes the preview surface from the form.
                    Assert.False(
                        ContainsControl(
                            documentView,
                            webView));

                    // Cleanup is waiting for the pending profile initialization
                    // operation. Physical deletion must not be claimed yet.
                    Assert.False(
                        previewSurface.IsDisposed);

                    Assert.False(
                        webView.IsDisposed);

                    Assert.True(
                        Directory.Exists(profileFolderPath),
                        "The profile directory must remain until its pending initialization handoff is released.");

                    // Act: allow the already-initialized production session
                    // to return to the surface after the security session is locked.
                    blockingProfileFactory.ReleaseSession();

                    // The old rendering operation must be rejected because its
                    // security-session revision is no longer current.
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => openTask);

                    // Await the renderer-owned asynchronous cleanup.
                    await previewSurface.DisposeAsync();

                    // Assert: browser control and surface disposal completed.
                    Assert.True(previewSurface.IsDisposed);
                    Assert.True(webView.IsDisposed);

                    // Physical profile deletion is now a verified postcondition.
                    Assert.False(
                        Directory.Exists(profileFolderPath));



                    // Re-authenticate under a new security-session revision.
                    bool reauthenticated =
                        await startupCoordinator.AuthenticateAsync(
                            cancellationToken);

                    Assert.True(reauthenticated);

                    ApplicationSecuritySessionSnapshot reauthenticatedSnapshot =
                        securitySession.CurrentSnapshot;

                    Assert.Equal(
                        ApplicationSecuritySessionState.Authenticated,
                        reauthenticatedSnapshot.State);

                    Assert.True(
                        reauthenticatedSnapshot.IsProtectedAccessAvailable);

                    Assert.True(
                        reauthenticatedSnapshot.Revision > lockedSnapshot.Revision);

                    IDocumentRepository documentRepository =
                        services.GetRequiredService<IDocumentRepository>();

                    Document metadataAfterPendingRevocation =
                        await documentRepository.GetByIdAsync(
                            importedDocument.DocumentId,
                            cancellationToken) ??
                        throw new InvalidOperationException(
                            "Document metadata could not be retrieved after pending-preview teardown.");

                    AssertDocumentMetadataUnchanged(
                        importedDocument.Metadata,
                        metadataAfterPendingRevocation);

                    // Re-authentication must not restore the old presentation.
                    Assert.Null(
                        presentation.Presenter.CurrentDocumentId);

                    Assert.Null(
                        presentation.Presenter.CurrentDocumentFileName);

                    Assert.False(
                        ContainsControl(
                            documentView,
                            webView));

                    // The canonical encrypted artifact must remain unchanged.
                    byte[] canonicalArtifactAfterLifecycle =
                        await File.ReadAllBytesAsync(
                            importedDocument.CanonicalArtifactPath,
                            cancellationToken);

                    Assert.Equal(
                        importedDocument.CanonicalArtifactBytes,
                        canonicalArtifactAfterLifecycle);
                }
                finally
                {
                    // Always release the test barrier so a failed assertion
                    // cannot leave profile initialization permanently pending.
                    blockingProfileFactory.ReleaseSession();

                    presentation?.Dispose();

                    if (previewSurface is not null)
                    {
                        await previewSurface.DisposeAsync();
                    }

                    if (openTask is not null)
                    {
                        try
                        {
                            await openTask;
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when the owning session was revoked.
                        }
                    }
                }
            });
    }

    private static async Task<ImportedHtmlDocument> InitializeHostAndImportHtmlDocumentAsync(
            ProductionSecuritySessionTestHost testHost,
            string fileName,
            CancellationToken cancellationToken,
            string? htmlContentOverride = null)
    {
        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticated);

        await startupCoordinator.InitializeDatabaseAsync(
            cancellationToken);

        return await ImportHtmlDocumentAsync(
            testHost,
            fileName,
            cancellationToken,
            htmlContentOverride);
    }

    private static async Task<ImportedHtmlDocument> ImportHtmlDocumentAsync(
        ProductionSecuritySessionTestHost testHost,
        string fileName,
        CancellationToken cancellationToken,
        string? htmlContentOverride = null)
    {
        IServiceProvider services =
            testHost.Services;

        string htmlMarker =
            $"DESKVAULT_HTML_PREVIEW_{Guid.NewGuid():N}";

        string imageMarker =
            $"DESKVAULT_HTML_IMAGE_{Guid.NewGuid():N}";

        string imageDataUri =
            CreateEmbeddedPngDataUri(imageMarker);

        string defaultHtmlContent =
            $$"""
             <!DOCTYPE html>
             <html>
             <head>
                 <meta charset="utf-8" />
                 <title>DeskVault production HTML preview</title>
             </head>
             <body>
                 <h1>{{htmlMarker}}</h1>
                 <p>Production workspace HTML preview integration test.</p>
                 <img
                     id="embedded-image"
                     alt="{{imageMarker}}"
                     src="{{imageDataUri}}" />
             </body>
             </html>
             """;

        string htmlContent =
            htmlContentOverride ?? defaultHtmlContent;

        string sourceFilePath =
            Path.Combine(
                testHost.DataPaths.RootDirectory,
                $"{Guid.NewGuid():N}-{fileName}");

        await File.WriteAllTextAsync(
            sourceFilePath,
            htmlContent,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        ImportDocumentHandler importDocumentHandler =
            services.GetRequiredService<ImportDocumentHandler>();

        ImportDocumentResult importResult =
            await importDocumentHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "HTML Preview Lifecycle Integration Test"),
                cancellationToken);

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        IDocumentRepository documentRepository =
            services.GetRequiredService<IDocumentRepository>();

        Document metadata =
            await documentRepository.GetByIdAsync(
                documentId,
                cancellationToken) ??
            throw new InvalidOperationException(
                "Imported HTML document metadata could not be retrieved.");

        Assert.EndsWith(
            Path.GetExtension(fileName),
            metadata.FileName,
            StringComparison.OrdinalIgnoreCase);

        Assert.EndsWith(
            ".dvault",
            metadata.StoredFilePath,
            StringComparison.OrdinalIgnoreCase);

        byte[] canonicalArtifactBytes =
            await File.ReadAllBytesAsync(
                metadata.StoredFilePath,
                cancellationToken);

        Assert.NotEmpty(canonicalArtifactBytes);

        return new ImportedHtmlDocument(
            documentId,
            metadata,
            metadata.StoredFilePath,
            canonicalArtifactBytes,
            htmlMarker,
            imageMarker);
    }

    private static void AssertDocumentMetadataUnchanged(
        Document expected,
        Document actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.FileName, actual.FileName);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.Sha256Hash, actual.Sha256Hash);
        Assert.Equal(expected.ImportedAt, actual.ImportedAt);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.LifecycleState, actual.LifecycleState);
        Assert.Equal(expected.ProcessingState, actual.ProcessingState);
        Assert.Equal(expected.StoredFilePath, actual.StoredFilePath);
        Assert.Equal(expected.ProcessingGeneration, actual.ProcessingGeneration);
        Assert.Equal(
            expected.LastSuccessfulProcessingGeneration,
            actual.LastSuccessfulProcessingGeneration);
        Assert.Equal(
            expected.LastSuccessfulProcessingRuleVersion,
            actual.LastSuccessfulProcessingRuleVersion);

        Assert.Equal(
            expected.KnowledgeAvailability,
            actual.KnowledgeAvailability);
    }

    private static async Task WaitForProfileCleanupCompletionAsync(
        string profileFolderPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTime deadline =
            DateTime.UtcNow + timeout;

        while (Directory.Exists(profileFolderPath)
               && DateTime.UtcNow < deadline)
        {
            await Task.Delay(
                50,
                cancellationToken);
        }

        Assert.False(
            Directory.Exists(profileFolderPath),
            "The HTML preview profile directory was not physically deleted after presentation disposal.");
    }

    private static WebView2 FindWebView(
        Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is WebView2 webView)
            {
                return webView;
            }

            try
            {
                return FindWebView(child);
            }
            catch (InvalidOperationException)
            {
                // Continue searching sibling controls.
            }
        }

        throw new InvalidOperationException(
            "The production HTML preview does not contain a WebView2 control.");
    }

    private static Task<bool> RunOnStaThreadAsync(
        Func<Form, Task> testBody)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(
            () =>
            {
                try
                {
                    using var form =
                        new Form
                        {
                            Width = 900,
                            Height = 650,
                            ShowInTaskbar = false,
                            StartPosition = FormStartPosition.Manual,
                            Location = new Point(-2000, -2000)
                        };

                    form.Shown +=
                        async (_, _) =>
                        {
                            try
                            {
                                await testBody(form);
                                completion.TrySetResult(true);
                            }
                            catch (Exception exception)
                            {
                                completion.TrySetException(exception);
                            }
                            finally
                            {
                                form.Close();
                            }
                        };

                    System.Windows.Forms.Application.Run(form);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.WaitAsync(
            TimeSpan.FromSeconds(90));
    }

    private static HtmlDocumentPreviewSurface FindPreviewSurface(
        Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is HtmlDocumentPreviewSurface surface)
            {
                return surface;
            }

            try
            {
                return FindPreviewSurface(child);
            }
            catch (InvalidOperationException)
            {
                // Continue searching sibling controls.
            }
        }

        throw new InvalidOperationException(
            "The production HTML preview surface could not be found.");
    }

    private static bool ContainsControl(
        Control parent,
        Control target)
    {
        foreach (Control child in parent.Controls)
        {
            if (ReferenceEquals(child, target)
                || ContainsControl(child, target))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<byte[]> ReadEmbeddedImagePayloadAsync(
        WebView2 webView)
    {
        string sourceJson =
            await webView.CoreWebView2.ExecuteScriptAsync(
                "document.querySelector('#embedded-image').src");

        string imageDataUri =
            JsonSerializer.Deserialize<string>(sourceJson) ??
            throw new InvalidOperationException(
                "The embedded image data URI could not be read.");

        const string DataUriPrefix = "data:image/png;base64,";

        Assert.StartsWith(
            DataUriPrefix,
            imageDataUri,
            StringComparison.OrdinalIgnoreCase);

        return Convert.FromBase64String(
            imageDataUri[DataUriPrefix.Length..]);
    }

    private static string CreateEmbeddedPngDataUri(
        string marker)
    {
        byte[] pngPayload =
            CreatePngWithTextMarker(marker);

        return "data:image/png;base64,"
            + Convert.ToBase64String(pngPayload);
    }

    private static byte[] CreatePngWithTextMarker(
        string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        using var output = new MemoryStream();

        // PNG signature.
        output.Write(
            [
                137, 80, 78, 71, 13, 10, 26, 10
            ]);

        // Valid 2x2, 8-bit RGBA image header.
        byte[] imageHeader = new byte[13];

        BinaryPrimitives.WriteUInt32BigEndian(
            imageHeader.AsSpan(0, 4),
            2);

        BinaryPrimitives.WriteUInt32BigEndian(
            imageHeader.AsSpan(4, 4),
            2);

        imageHeader[8] = 8;
        imageHeader[9] = 6;

        WritePngChunk(
            output,
            "IHDR",
            imageHeader);

        // Preserve the synthetic marker inside PNG image metadata.
        byte[] textData =
            Encoding.ASCII.GetBytes(
                $"Comment\0{marker}");

        WritePngChunk(
            output,
            "tEXt",
            textData);

        // Two rows of two RGBA pixels.
        byte[] scanline =
        [
            0, 255, 0, 0, 255, 0, 255, 0, 255,
        0, 0, 0, 255, 255, 0, 0, 0, 255
        ];

        using var compressedImage = new MemoryStream();

        using (var zlib = new ZLibStream(
                   compressedImage,
                   CompressionLevel.Optimal,
                   leaveOpen: true))
        {
            zlib.Write(scanline);
        }

        WritePngChunk(
            output,
            "IDAT",
            compressedImage.ToArray());

        WritePngChunk(
            output,
            "IEND",
            []);

        return output.ToArray();
    }

    private static void WritePngChunk(
        Stream output,
        string chunkType,
        byte[] data)
    {
        byte[] typeBytes =
            Encoding.ASCII.GetBytes(chunkType);

        if (typeBytes.Length != 4)
        {
            throw new ArgumentException(
                "A PNG chunk type must contain exactly four ASCII characters.",
                nameof(chunkType));
        }

        Span<byte> lengthBuffer = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            lengthBuffer,
            checked((uint)data.Length));

        output.Write(lengthBuffer);
        output.Write(typeBytes);
        output.Write(data);

        uint crc =
            CalculatePngCrc(
                typeBytes,
                data);

        Span<byte> crcBuffer = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            crcBuffer,
            crc);

        output.Write(crcBuffer);
    }

    private static uint CalculatePngCrc(
        ReadOnlySpan<byte> chunkType,
        ReadOnlySpan<byte> data)
    {
        const uint Polynomial = 0xEDB88320;

        uint crc = 0xFFFFFFFF;

        foreach (byte value in chunkType)
        {
            crc = UpdatePngCrc(
                crc,
                value,
                Polynomial);
        }

        foreach (byte value in data)
        {
            crc = UpdatePngCrc(
                crc,
                value,
                Polynomial);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdatePngCrc(
        uint crc,
        byte value,
        uint polynomial)
    {
        crc ^= value;

        for (int bit = 0; bit < 8; bit++)
        {
            crc =
                (crc & 1) != 0
                    ? (crc >> 1) ^ polynomial
                    : crc >> 1;
        }

        return crc;
    }

    private sealed class BlockingHtmlDocumentWebView2ProfileSessionFactory
        : IHtmlDocumentWebView2ProfileSessionFactory
    {
        private readonly HtmlDocumentWebView2ProfileSessionFactory _innerFactory =
            new();

        private readonly TaskCompletionSource<string> _sessionInitialized =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _releaseSession =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> SessionInitialized =>
            _sessionInitialized.Task;

        public void ReleaseSession()
        {
            _releaseSession.TrySetResult();
        }

        public async Task<HtmlDocumentWebView2ProfileSession> CreateAsync(
            WebView2 webView,
            CancellationToken cancellationToken)
        {
            // Run the actual production profile initialization.
            HtmlDocumentWebView2ProfileSession profileSession =
                await _innerFactory.CreateAsync(
                    webView,
                    cancellationToken);

            // Signal that WebView2 has initialized and its isolated profile
            // directory physically exists.
            _sessionInitialized.TrySetResult(
                profileSession.UserDataFolderPath);

            // Deliberately pause the handoff to the renderer. The test controls
            // when this barrier is released, independently of cancellation.
            await _releaseSession.Task;

            return profileSession;
        }
    }

    private sealed record ImportedHtmlDocument(
        Guid DocumentId,
        Document Metadata,
        string CanonicalArtifactPath,
        byte[] CanonicalArtifactBytes,
        string HtmlMarker,
        string ImageMarker);
}

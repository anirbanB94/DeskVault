using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces;
using DeskVault.Application.Workspaces.Commands.ActivateWorkspace;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Application.Workspaces.Queries.GetWorkspace;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddSingleton<IImportDocumentValidator, ImportDocumentValidator>();

        services.AddSingleton<ImportDocumentHandler>();

        services.AddSingleton<OpenDocumentHandler>();

        services.AddSingleton<ListDocumentsHandler>();

        services.AddSingleton<RemoveDocumentHandler>();

        services.AddSingleton<RecoverDocumentArtifactsHandler>();

        services.AddSingleton<ISearchDocumentsRanker, SearchDocumentsRanker>();

        services.AddSingleton<SearchDocumentsHandler>();

        services.AddSingleton<ReconcileDocumentArtifactsHandler>();

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new TextDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new MarkdownDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new CsvDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new JsonDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new XmlDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new YamlDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextExtractor>(
            serviceProvider =>
                new IniDocumentTextExtractor(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<DocumentTextExtractorResolver>();

        services.AddSingleton<ProcessDocumentHandler>();

        services.AddSingleton<IDocumentProcessingService, DocumentProcessingService>();

        services.AddSingleton<IDocumentTextNormalizer>(
            serviceProvider =>
                new DocumentTextNormalizer(
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>()));

        services.AddSingleton<IDocumentTextChunker>(
            serviceProvider =>
            {
                DocumentProcessingOptions options =
                    serviceProvider.GetRequiredService<DocumentProcessingOptions>();

                return new DocumentTextChunker(
                    options.MaxChunkSize,
                    options.ChunkOverlap);
            });

        services.AddSingleton<IActiveWorkspaceRegistry, ActiveWorkspaceRegistry>();

        services.AddSingleton<CreateWorkspaceHandler>();

        services.AddSingleton<AddDocumentToWorkspaceHandler>();

        services.AddSingleton<RemoveDocumentFromWorkspaceHandler>();

        services.AddSingleton<ActivateWorkspaceHandler>();

        services.AddSingleton<SaveTemporaryWorkspaceHandler>();

        services.AddSingleton<CloseWorkspaceHandler>();

        services.AddSingleton<RenameWorkspaceHandler>();

        services.AddSingleton<UpdateWorkspaceDescriptionHandler>();

        services.AddSingleton<OpenWorkspaceHandler>();

        services.AddSingleton<DeleteWorkspaceHandler>();

        services.AddSingleton<GetWorkspaceHandler>();

        services.AddSingleton<GetWorkspacesHandler>();

        return services;
    }
}

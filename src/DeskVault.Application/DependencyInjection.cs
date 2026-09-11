using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.ProcessDocument;
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

        services.AddSingleton<SearchDocumentsHandler>();

        services.AddSingleton<ReconcileDocumentArtifactsHandler>();

        services.AddSingleton<IDocumentTextExtractor, TextDocumentTextExtractor>();

        services.AddSingleton<IDocumentTextExtractor, MarkdownDocumentTextExtractor>();

        services.AddSingleton<IDocumentTextExtractor, CsvDocumentTextExtractor>();

        services.AddSingleton<IDocumentTextExtractor, JsonDocumentTextExtractor>();

        services.AddSingleton<IDocumentTextExtractor, XmlDocumentTextExtractor>();

        services.AddSingleton<IDocumentTextExtractor, YamlDocumentTextExtractor>();

        services.AddSingleton<IDocumentTextExtractor, IniDocumentTextExtractor>();

        services.AddSingleton<DocumentTextExtractorResolver>();

        services.AddSingleton<ProcessDocumentHandler>();

        services.AddSingleton<IDocumentProcessingService, DocumentProcessingService>();

        services.AddSingleton<IDocumentTextNormalizer, DocumentTextNormalizer>();

        services.AddSingleton<IDocumentTextChunker>(
            _ => new DocumentTextChunker(
                maxChunkSize: 4000));

        return services;
    }
}

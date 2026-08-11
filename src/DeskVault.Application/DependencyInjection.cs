using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddTransient<IImportDocumentValidator, ImportDocumentValidator>();

        services.AddTransient<ImportDocumentHandler>();

        services.AddTransient<OpenDocumentHandler>();

        services.AddTransient<ListDocumentsHandler>();

        services.AddTransient<RemoveDocumentHandler>();

        return services;
    }
}

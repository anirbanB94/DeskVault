using DeskVault.Application.Documents.Commands.ImportDocument;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddTransient<IImportDocumentValidator, ImportDocumentValidator>();

        services.AddTransient<ImportDocumentHandler>();

        return services;
    }
}
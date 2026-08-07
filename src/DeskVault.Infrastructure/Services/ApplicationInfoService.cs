using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DeskVault.Infrastructure.Services;

public class ApplicationInfoService : IApplicationInfoService
{
    private readonly IConfiguration _configuration;

    public ApplicationInfoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ApplicationName =>
        _configuration["Application:Name"] ?? "Unknown";

    public string Version =>
        _configuration["Application:Version"] ?? "Unknown";
}
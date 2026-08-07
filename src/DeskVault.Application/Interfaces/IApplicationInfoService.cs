namespace DeskVault.Application.Interfaces
{

    public interface IApplicationInfoService
    {
        string ApplicationName { get; }

        string Version { get; }
    }
}
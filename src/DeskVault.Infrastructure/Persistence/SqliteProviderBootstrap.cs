namespace DeskVault.Infrastructure.Persistence;

internal static class SqliteProviderBootstrap
{
    private static readonly Lazy<bool> Initialization = new(
        static () =>
        {
            SQLitePCL.Batteries_V2.Init();
            return true;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    internal static void EnsureInitialized()
    {
        _ = Initialization.Value;
    }
}
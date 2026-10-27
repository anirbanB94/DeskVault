using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Tests.TestDoubles;

public sealed class RecordingLogger<TCategory> : ILogger<TCategory>
{
    private readonly List<RecordedLogEntry> _entries = [];

    public IReadOnlyList<RecordedLogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
            NoopDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        Dictionary<string, object?> properties = [];

        if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
        {
            foreach (KeyValuePair<string, object?> property in structuredState)
            {
                properties[property.Key] = property.Value;
            }
        }

        _entries.Add(
            new RecordedLogEntry(
                logLevel,
                message,
                exception,
                properties));
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

public sealed record RecordedLogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
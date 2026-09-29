namespace DeskVault.Application.Documents.Processing;

public sealed class ResourceLimitExceededException : InvalidOperationException
{
    public ResourceLimitExceededException(
        long limitBytes,
        long attemptedBytes)
        : base(
            $"Document processing resource limit exceeded. " +
            $"Limit: {limitBytes} bytes; attempted: {attemptedBytes} bytes.")
    {
        LimitBytes = limitBytes;
        AttemptedBytes = attemptedBytes;
    }

    public long LimitBytes { get; }

    public long AttemptedBytes { get; }
}

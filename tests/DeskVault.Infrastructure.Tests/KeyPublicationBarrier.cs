namespace DeskVault.Infrastructure.Tests;

internal sealed class KeyPublicationBarrier
{
    private readonly int _participantCount;

    private readonly TaskCompletionSource<bool> _allParticipantsArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<bool> _publicationReleased =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _arrivalCount;

    public KeyPublicationBarrier(int participantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            participantCount);

        _participantCount = participantCount;
    }

    public int ArrivalCount =>
        Volatile.Read(ref _arrivalCount);

    public async Task WaitAtPublicationAsync(
        CancellationToken cancellationToken)
    {
        int arrivalCount =
            Interlocked.Increment(ref _arrivalCount);

        if (arrivalCount > _participantCount)
        {
            throw new InvalidOperationException(
                "More key initializers reached the publication barrier than expected.");
        }

        if (arrivalCount == _participantCount)
        {
            _allParticipantsArrived.TrySetResult(true);
        }

        await _publicationReleased.Task.WaitAsync(
            cancellationToken);
    }

    public Task WaitForAllArrivalsAsync(
        TimeSpan timeout)
    {
        return _allParticipantsArrived.Task.WaitAsync(
            timeout);
    }

    public void Release()
    {
        _publicationReleased.TrySetResult(true);
    }
}

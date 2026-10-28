using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Parsing.Csv;

internal sealed class CsvResourceLimitTextReader : TextReader
{
    private readonly TextReader _inner;
    private readonly CancellationToken _cancellationToken;
    private readonly int _maximumColumns;
    private readonly int _maximumFieldLength;
    private readonly long _maximumCells;

    private int _currentColumns = 1;
    private long _currentFieldLength;
    private long _totalCells;

    private bool _insideQuotedField;
    private bool _quotePending;
    private bool _atFieldStart = true;
    private bool _recordHasCharacters;
    private bool _skipLineFeedAfterCarriageReturn;
    private bool _completed;

    public CsvResourceLimitTextReader(
        TextReader inner,
        int maximumColumns,
        int maximumFieldLength,
        long maximumCells,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (maximumColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumColumns));
        }

        if (maximumFieldLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFieldLength));
        }

        if (maximumCells <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCells));
        }

        _inner = inner;
        _cancellationToken = cancellationToken;
        _maximumColumns = maximumColumns;
        _maximumFieldLength = maximumFieldLength;
        _maximumCells = maximumCells;
    }

    public override int Peek()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return _inner.Peek();
    }

    public override int Read()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        int value = _inner.Read();

        if (value < 0)
        {
            CompleteAtEndOfInput();
            return -1;
        }

        Span<char> character = stackalloc char[1];
        character[0] = (char)value;
        Inspect(character);

        _cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    public override int Read(
        char[] buffer,
        int index,
        int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _cancellationToken.ThrowIfCancellationRequested();

        int read = _inner.Read(buffer, index, count);

        if (read == 0 && count > 0)
        {
            CompleteAtEndOfInput();
        }
        else if (read > 0)
        {
            Inspect(buffer.AsSpan(index, read));
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return read;
    }

    public override int Read(Span<char> buffer)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        int read = _inner.Read(buffer);

        if (read == 0 && !buffer.IsEmpty)
        {
            CompleteAtEndOfInput();
        }
        else if (read > 0)
        {
            Inspect(buffer[..read]);
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return read;
    }

    public override async Task<int> ReadAsync(
        char[] buffer,
        int index,
        int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _cancellationToken.ThrowIfCancellationRequested();

        int read = await _inner.ReadAsync(
            buffer.AsMemory(index, count),
            _cancellationToken).ConfigureAwait(false);

        if (read == 0 && count > 0)
        {
            CompleteAtEndOfInput();
        }
        else if (read > 0)
        {
            Inspect(buffer.AsSpan(index, read));
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<char> buffer,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? linkedSource = null;
        CancellationToken effectiveToken;

        if (cancellationToken.CanBeCanceled &&
            _cancellationToken.CanBeCanceled &&
            cancellationToken != _cancellationToken)
        {
            linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _cancellationToken);
            effectiveToken = linkedSource.Token;
        }
        else
        {
            effectiveToken = cancellationToken.CanBeCanceled
                ? cancellationToken
                : _cancellationToken;
        }

        try
        {
            effectiveToken.ThrowIfCancellationRequested();

            int read = await _inner.ReadAsync(
                buffer,
                effectiveToken).ConfigureAwait(false);

            if (read == 0 && !buffer.IsEmpty)
            {
                CompleteAtEndOfInput();
            }
            else if (read > 0)
            {
                Inspect(buffer.Span[..read]);
            }

            effectiveToken.ThrowIfCancellationRequested();
            return read;
        }
        finally
        {
            linkedSource?.Dispose();
        }
    }

    private void Inspect(ReadOnlySpan<char> characters)
    {
        foreach (char character in characters)
        {
            InspectCharacter(character);
        }
    }

    private void InspectCharacter(char character)
    {
        if (_insideQuotedField)
        {
            if (_quotePending)
            {
                if (character == '"')
                {
                    // An escaped quote represents one field character.
                    _quotePending = false;
                    _recordHasCharacters = true;
                    CountFieldCharacter();
                    return;
                }

                // The pending quote closed the quoted field.
                _insideQuotedField = false;
                _quotePending = false;

                ProcessOutsideQuotes(character);
                return;
            }

            if (character == '"')
            {
                _quotePending = true;
            }
            else
            {
                // Commas and line breaks inside a quoted field are data.
                _recordHasCharacters = true;
                CountFieldCharacter();
            }

            return;
        }

        ProcessOutsideQuotes(character);
    }

    private void ProcessOutsideQuotes(char character)
    {
        if (_skipLineFeedAfterCarriageReturn)
        {
            _skipLineFeedAfterCarriageReturn = false;

            if (character == '\n')
            {
                return;
            }
        }

        if (character == '"' && _atFieldStart)
        {
            // Opening quote syntax is not part of the field value.
            _insideQuotedField = true;
            _quotePending = false;
            _atFieldStart = false;
            _recordHasCharacters = true;
            return;
        }

        if (character == ',')
        {
            _recordHasCharacters = true;
            CountCell();

            _currentColumns++;

            if (_currentColumns > _maximumColumns)
            {
                throw new DocumentStructureLimitExceededException(
                    "CSV columns",
                    _maximumColumns,
                    _currentColumns);
            }

            _atFieldStart = true;
            _currentFieldLength = 0;
            return;
        }

        if (character is '\r' or '\n')
        {
            FinishRecord();

            _skipLineFeedAfterCarriageReturn =
                character == '\r';

            return;
        }

        _recordHasCharacters = true;
        _atFieldStart = false;
        CountFieldCharacter();
    }

    private void CountFieldCharacter()
    {
        _currentFieldLength++;

        if (_currentFieldLength > _maximumFieldLength)
        {
            throw new DocumentStructureLimitExceededException(
                "CSV field length",
                _maximumFieldLength,
                _currentFieldLength);
        }
    }

    private void FinishRecord()
    {
        if (_recordHasCharacters)
        {
            CountCell();
        }

        _currentColumns = 1;
        _currentFieldLength = 0;
        _atFieldStart = true;
        _recordHasCharacters = false;
    }

    private void CountCell()
    {
        _totalCells++;

        if (_totalCells > _maximumCells)
        {
            throw new DocumentStructureLimitExceededException(
                "CSV cells",
                _maximumCells,
                _totalCells);
        }
    }

    private void CompleteAtEndOfInput()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;

        if (_quotePending)
        {
            // A pending quote at EOF closes the quoted field.
            _quotePending = false;
            _insideQuotedField = false;
        }

        // Leave unterminated quoted content to CsvHelper to reject.
        if (_insideQuotedField)
        {
            return;
        }

        FinishRecord();
    }
}

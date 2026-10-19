using System;
using System.IO;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Performs filesystem operations for temporary protected-key candidates.
/// </summary>
internal sealed class KeyCandidateFileOperations :
    IKeyCandidateFileOperations
{
    private const int BufferSize = 4096;

    public string GetCandidateFilePath(
        string securityDirectory,
        string canonicalKeyFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            securityDirectory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            canonicalKeyFileName);

        return Path.Combine(
            securityDirectory,
            $"{canonicalKeyFileName}.{Guid.NewGuid():N}.tmp");
    }

    public Stream CreateCandidateFile(
        string candidateFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            candidateFilePath);

        return new FileStream(
            candidateFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: BufferSize,
            useAsync: true);
    }

    public void FlushCandidateFileToDisk(
        Stream candidateFile)
    {
        ArgumentNullException.ThrowIfNull(
            candidateFile);

        if (candidateFile is not FileStream fileStream)
        {
            throw new ArgumentException(
                "The candidate stream does not support durable file flushing.",
                nameof(candidateFile));
        }

        fileStream.Flush(
            flushToDisk: true);
    }

    public void PublishCandidateFile(
        string candidateFilePath,
        string canonicalKeyFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            candidateFilePath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            canonicalKeyFilePath);

        // File.Move without overwrite preserves an existing canonical key.
        File.Move(
            candidateFilePath,
            canonicalKeyFilePath);
    }

    public void DeleteCandidateFile(
        string candidateFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            candidateFilePath);

        File.Delete(
            candidateFilePath);
    }
}

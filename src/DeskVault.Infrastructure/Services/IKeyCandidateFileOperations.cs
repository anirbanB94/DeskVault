using System.IO;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Encapsulates filesystem operations for temporary protected-key candidates.
/// </summary>
/// <remarks>
/// Callers must track candidate ownership explicitly. Generating a candidate
/// path does not establish ownership; ownership begins only after exclusive
/// creation succeeds and ends when publication successfully moves the file.
/// </remarks>
internal interface IKeyCandidateFileOperations
{
    /// <summary>
    /// Generates a unique temporary candidate path in the specified directory.
    /// </summary>
    string GetCandidateFilePath(
        string securityDirectory,
        string canonicalKeyFileName);

    /// <summary>
    /// Exclusively creates a new temporary candidate file.
    /// </summary>
    /// <remarks>
    /// If creation fails, the caller must not assume that it owns the path.
    /// </remarks>
    Stream CreateCandidateFile(
        string candidateFilePath);

    /// <summary>
    /// Flushes candidate contents to the underlying storage device.
    /// </summary>
    void FlushCandidateFileToDisk(
        Stream candidateFile);

    /// <summary>
    /// Publishes a completed candidate without overwriting the canonical file.
    /// </summary>
    void PublishCandidateFile(
        string candidateFilePath,
        string canonicalKeyFilePath);

    /// <summary>
    /// Attempts to delete a candidate file.
    /// The caller is responsible for verifying that it still owns the candidate.
    /// </summary>
    void DeleteCandidateFile(
        string candidateFilePath);
}

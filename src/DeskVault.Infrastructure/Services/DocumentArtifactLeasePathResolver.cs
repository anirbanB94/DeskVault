namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Resolves the persistent coordination-file path for a document-artifact lease.
/// </summary>
/// <remarks>
/// Coordination files are stored below the canonical vault root in
/// Security\Coordination\DocumentArtifacts. Each directory is physically
/// validated before the next directory is created so that a reparse point
/// cannot redirect coordination outside the expected vault directory layout.
/// The resulting lock-file path is independent of the canonical document
/// artifact enumeration directory.
/// </remarks>
public sealed class DocumentArtifactLeasePathResolver
{
    private const string SecurityDirectoryName = "Security";
    private const string CoordinationDirectoryName = "Coordination";
    private const string DocumentArtifactsDirectoryName = "DocumentArtifacts";
    private const string LeaseFileExtension = ".lock";

    private readonly CanonicalVaultDirectoryResolver _directoryResolver;

    /// <summary>
    /// Initializes the resolver using the canonical vault directory resolver.
    /// </summary>
    /// <param name="directoryResolver">
    /// The resolver used to establish physical vault and child-directory identities.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// The supplied directory resolver is null.
    /// </exception>
    public DocumentArtifactLeasePathResolver(
        CanonicalVaultDirectoryResolver directoryResolver)
    {
        ArgumentNullException.ThrowIfNull(directoryResolver);

        _directoryResolver = directoryResolver;
    }

    /// <summary>
    /// Gets the persistent coordination-file path for the specified document.
    /// </summary>
    /// <param name="documentId">
    /// The non-empty document identity.
    /// </param>
    /// <returns>
    /// The absolute path of the document's coordination file.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The document identifier is empty.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The configured vault directory does not exist.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Access to the vault or coordination directory is denied.
    /// </exception>
    /// <exception cref="IOException">
    /// The coordination directory cannot be established at its expected
    /// physical location within the canonical vault.
    /// </exception>
    public string GetPath(
        Guid documentId)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        // Resolve the physical vault root first so that equivalent configured
        // paths to the same vault produce the same coordination location.
        string canonicalVaultRoot =
            _directoryResolver.Resolve();

        string securityDirectory =
            Path.Combine(
                canonicalVaultRoot,
                SecurityDirectoryName);

        // Validate each directory before using it as the parent of the next
        // directory. This prevents an existing reparse point from redirecting
        // subsequent coordination directories outside the vault.
        string canonicalSecurityDirectory =
            EnsureCanonicalDirectory(
                canonicalVaultRoot,
                securityDirectory);

        string coordinationDirectory =
            Path.Combine(
                canonicalSecurityDirectory,
                CoordinationDirectoryName);

        string canonicalCoordinationDirectory =
            EnsureCanonicalDirectory(
                canonicalVaultRoot,
                coordinationDirectory);

        string documentArtifactsDirectory =
            Path.Combine(
                canonicalCoordinationDirectory,
                DocumentArtifactsDirectoryName);

        string canonicalDocumentArtifactsDirectory =
            EnsureCanonicalDirectory(
                canonicalVaultRoot,
                documentArtifactsDirectory);

        // A document ID determines one persistent coordination file within
        // this vault. The file's existence does not indicate lease ownership;
        // ownership is determined exclusively by the operating-system handle.
        return Path.Combine(
            canonicalDocumentArtifactsDirectory,
            $"{documentId:D}{LeaseFileExtension}");
    }

    /// <summary>
    /// Creates a coordination directory when necessary and verifies that its
    /// resolved physical location matches the expected path inside the vault.
    /// </summary>
    /// <param name="canonicalVaultRoot">
    /// The resolved physical root directory of the configured vault.
    /// </param>
    /// <param name="directoryPath">
    /// The expected path of the coordination directory to create or validate.
    /// </param>
    /// <returns>
    /// The validated physical path of the coordination directory.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    /// The directory or one of its required parent directories cannot be found.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Access to the directory is denied.
    /// </exception>
    /// <exception cref="IOException">
    /// The resolved directory differs from the expected path or is outside
    /// the canonical vault root.
    /// </exception>
    private static string EnsureCanonicalDirectory(
        string canonicalVaultRoot,
        string directoryPath)
    {
        // Create only the current directory level. Its physical location is
        // checked before GetPath proceeds to create its child directory.
        Directory.CreateDirectory(
            directoryPath);

        string resolvedDirectory =
            CanonicalVaultDirectoryResolver.ResolveDirectory(
                directoryPath);

        string expectedDirectory =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    directoryPath));

        // String comparison catches a redirected directory even when its
        // resolved target is still somewhere inside the vault.
        if (!string.Equals(
                expectedDirectory,
                resolvedDirectory,
                StringComparison.OrdinalIgnoreCase) ||
            !IsDescendantDirectory(
                canonicalVaultRoot,
                resolvedDirectory))
        {
            throw new IOException(
                "The document-artifact coordination directory does not resolve to its expected location inside the canonical vault.");
        }

        return resolvedDirectory;
    }

    /// <summary>
    /// Determines whether a candidate directory is a strict descendant of
    /// the canonical vault root.
    /// </summary>
    /// <param name="canonicalVaultRoot">
    /// The physical root directory of the vault.
    /// </param>
    /// <param name="candidateDirectory">
    /// The resolved physical directory to validate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the candidate is strictly below the vault
    /// root; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsDescendantDirectory(
        string canonicalVaultRoot,
        string candidateDirectory)
    {
        // GetRelativePath provides a path-boundary-aware containment check.
        // A simple StartsWith comparison could incorrectly accept a sibling
        // directory whose name merely begins with the vault directory name.
        string relativePath =
            Path.GetRelativePath(
                canonicalVaultRoot,
                candidateDirectory);

        // Reject paths on a different root or volume.
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        // The coordination directory must be below the vault, not the vault
        // root itself.
        if (string.Equals(
                relativePath,
                ".",
                StringComparison.Ordinal))
        {
            return false;
        }

        // Reject the vault's parent directory.
        if (string.Equals(
                relativePath,
                "..",
                StringComparison.Ordinal))
        {
            return false;
        }

        string parentDirectoryPrefix =
            $"..{Path.DirectorySeparatorChar}";

        string alternateParentDirectoryPrefix =
            $"..{Path.AltDirectorySeparatorChar}";

        // Reject any path that traverses above the vault root.
        return
            !relativePath.StartsWith(
                parentDirectoryPrefix,
                StringComparison.Ordinal) &&
            !relativePath.StartsWith(
                alternateParentDirectoryPrefix,
                StringComparison.Ordinal);
    }
}

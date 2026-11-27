using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Resolves the configured vault directory to its physical Windows directory identity.
/// </summary>
/// <remarks>
/// Resolution follows directory junctions and symbolic links. The returned path
/// is absolute, normalized, and suitable for identifying the same vault when
/// it is accessed through different path aliases.
/// </remarks>
public sealed partial class CanonicalVaultDirectoryResolver
{
    private const uint FileReadAttributes = 0x00000080;

    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;

    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    private const uint FileAttributeDirectory = 0x00000010;

    private const int FileAttributeTagInfo = 9;
    private const uint FileAttributeTagInformationSize = sizeof(uint) * 2;

    private const uint FileNameNormalized = 0x00000000;
    private const uint VolumeNameDos = 0x00000000;

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;

    private const int InitialFinalPathBufferLength = 512;
    private const int MaximumFinalPathLength = 32767;

    private const string ExtendedUncPathPrefix = @"\\?\UNC\";
    private const string ExtendedPathPrefix = @"\\?\";

    private readonly DeskVaultDataPaths _dataPaths;

    /// <summary>
    /// Initializes the resolver for the configured vault.
    /// </summary>
    /// <param name="dataPaths">
    /// The configured application data paths.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// The supplied data paths are null.
    /// </exception>
    public CanonicalVaultDirectoryResolver(
        DeskVaultDataPaths dataPaths)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);

        _dataPaths = dataPaths;
    }

    /// <summary>
    /// Resolves the configured vault root to its physical directory path.
    /// </summary>
    /// <returns>
    /// The normalized physical directory path, with directory reparse points
    /// resolved.
    /// </returns>
    public string Resolve()
    {
        string rootDirectory =
            _dataPaths.RootDirectory;

        // Validate here so callers receive the configured property name
        // rather than the parameter name of the delegated resolver.
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootDirectory);

        return ResolveDirectory(
            rootDirectory);
    }

    /// <summary>
    /// Resolves a directory path to its physical Windows directory identity.
    /// </summary>
    /// <param name="directoryPath">
    /// The directory path to resolve.
    /// </param>
    /// <returns>
    /// The normalized physical directory path, with directory reparse points
    /// resolved.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The supplied directory path is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">
    /// The current operating system is not Windows.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The specified directory does not exist.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Access to the specified directory is denied.
    /// </exception>
    /// <exception cref="IOException">
    /// The path does not identify a directory or Windows could not resolve
    /// its physical directory identity.
    /// </exception>
    public static string ResolveDirectory(
        string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            directoryPath);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Canonical vault directory resolution requires Windows.");
        }

        string absolutePath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    directoryPath));

        string extendedLengthPath =
            ToExtendedLengthPath(
                absolutePath);

        using SafeFileHandle directoryHandle =
            CreateFileW(
                extendedLengthPath,
                FileReadAttributes,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics,
                IntPtr.Zero);

        if (directoryHandle.IsInvalid)
        {
            int errorCode =
                Marshal.GetLastPInvokeError();

            throw CreateNativeFailureException(
                "open the configured vault directory",
                errorCode);
        }

        if (!GetFileInformationByHandleEx(
                directoryHandle,
                FileAttributeTagInfo,
                out FileAttributeTagInformation fileInformation,
                FileAttributeTagInformationSize))
        {
            int errorCode =
                Marshal.GetLastPInvokeError();

            throw CreateNativeFailureException(
                "read the configured vault directory attributes",
                errorCode);
        }

        if ((fileInformation.FileAttributes & FileAttributeDirectory) == 0)
        {
            throw new IOException(
                "The configured vault root path does not identify a directory.");
        }

        string finalPath =
            GetFinalPathName(
                directoryHandle);

        return NormalizeFinalPath(
            finalPath);
    }

    private static string ToExtendedLengthPath(
        string absolutePath)
    {
        if (absolutePath.StartsWith(
                ExtendedPathPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath;
        }

        if (absolutePath.StartsWith(
                @"\\",
                StringComparison.Ordinal))
        {
            return ExtendedUncPathPrefix +
                   absolutePath[2..];
        }

        return ExtendedPathPrefix +
               absolutePath;
    }

    private static string GetFinalPathName(
        SafeFileHandle directoryHandle)
    {
        int bufferLength =
            InitialFinalPathBufferLength;

        while (true)
        {
            char[] buffer =
                new char[bufferLength];

            uint resultLength;

            // Pin the output buffer while Windows writes the final path.
            unsafe
            {
                fixed (char* bufferPointer = buffer)
                {
                    resultLength =
                        GetFinalPathNameByHandleW(
                            directoryHandle,
                            bufferPointer,
                            (uint)buffer.Length,
                            FileNameNormalized | VolumeNameDos);
                }
            }

            if (resultLength == 0)
            {
                int errorCode =
                    Marshal.GetLastPInvokeError();

                throw CreateNativeFailureException(
                    "resolve the physical vault directory path",
                    errorCode);
            }

            if (resultLength < (uint)buffer.Length)
            {
                return new string(
                    buffer,
                    startIndex: 0,
                    length: checked((int)resultLength));
            }

            if (resultLength > MaximumFinalPathLength)
            {
                throw new IOException(
                    "The resolved vault directory path exceeds the supported Windows path length.");
            }

            // Windows reports the required buffer length when the supplied
            // buffer is insufficient.
            bufferLength =
                checked((int)resultLength + 1);
        }
    }

    private static string NormalizeFinalPath(
        string finalPath)
    {
        string normalizedPath;

        if (finalPath.StartsWith(
                ExtendedUncPathPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath =
                @"\\" + finalPath[ExtendedUncPathPrefix.Length..];
        }
        else if (finalPath.StartsWith(
                     ExtendedPathPrefix,
                     StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath =
                finalPath[ExtendedPathPrefix.Length..];
        }
        else
        {
            normalizedPath =
                finalPath;
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                normalizedPath));
    }

    private static Exception CreateNativeFailureException(
        string operation,
        int errorCode)
    {
        var nativeException =
            new Win32Exception(
                errorCode);

        string message =
            $"Unable to {operation} for the configured vault root directory.";

        return errorCode switch
        {
            ErrorFileNotFound or ErrorPathNotFound =>
                new DirectoryNotFoundException(
                    message,
                    nativeException),

            ErrorAccessDenied =>
                new UnauthorizedAccessException(
                    message,
                    nativeException),

            _ =>
                new IOException(
                    message,
                    nativeException)
        };
    }

    // Source-generated Windows API imports avoid runtime-generated
    // marshalling stubs and retain SafeHandle-based resource management.
    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetFileInformationByHandleEx",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandleEx(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        out FileAttributeTagInformation fileInformation,
        uint bufferSize);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        SetLastError = true)]
    private static unsafe partial uint GetFinalPathNameByHandleW(
        SafeFileHandle fileHandle,
        char* filePath,
        uint filePathLength,
        uint flags);

    /// <summary>
    /// Represents the Windows FILE_ATTRIBUTE_TAG_INFO structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInformation
    {
        public uint FileAttributes;

        public uint ReparseTag;
    }
}

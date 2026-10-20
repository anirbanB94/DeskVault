using DeskVault.Application.Documents.Processing;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDocumentReaderTests
{
    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsValid_ReturnsDecryptedContent()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId,
                ComputeSha256Hash(originalContent));

        // Assert
        Assert.Equal(
            0,
            result.Position);

        Assert.True(
            result.CanRead,
            "The successfully returned document stream must remain readable.");

        byte[] returnedContent =
            new byte[originalContent.Length];

        try
        {
            int totalRead = 0;

            while (totalRead < returnedContent.Length)
            {
                int bytesRead = await result.ReadAsync(
                    returnedContent.AsMemory(totalRead));

                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
            }

            Assert.Equal(
                originalContent.Length,
                totalRead);

            Assert.Equal(
                originalContent,
                returnedContent);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                returnedContent);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenMaximumIsExceededAfterStagingMultipleChunks_ClearsCurrentAndRetiredBuffers()
    {
        using var context = CreateTestContext();

        const int chunkSize = 1024 * 1024;
        const long maximumPlaintextBytes = 2L * chunkSize;

        byte[] originalContent = new byte[(2 * chunkSize) + 37];
        RandomNumberGenerator.Fill(originalContent);

        Guid documentId = Guid.NewGuid();

        string artifactPath = context.PathResolver.GetPath(documentId);

        ZeroingMemoryStream? observedStagingStream = null;

        try
        {
            await CreateEncryptedFileAsync(
                artifactPath,
                documentId,
                originalContent,
                context.EncryptionService);

            context.Reader.DecryptedStagingStreamFactoryForTesting =
                () => observedStagingStream =
                    new ZeroingMemoryStream(
                        trackRetiredBuffersForTesting: true);

            ResourceLimitExceededException exception =
                await Assert.ThrowsAsync<ResourceLimitExceededException>(
                    () => context.Reader.OpenReadAsync(
                        documentId,
                        ComputeSha256Hash(originalContent),
                        maximumPlaintextBytes: maximumPlaintextBytes));

            Assert.Equal(
                maximumPlaintextBytes,
                exception.LimitBytes);

            Assert.Equal(
                originalContent.Length,
                exception.AttemptedBytes);

            Assert.NotNull(observedStagingStream);

            // The current backing array must be clear after failure and disposal.
            AssertStagingBufferCleared(observedStagingStream);

            // Growth must have retired at least one buffer large enough to have
            // held a complete plaintext chunk, and all retired arrays must be zero.
            Assert.Contains(
                observedStagingStream!.RetiredBuffersForTesting,
                buffer => buffer.Length >= chunkSize);

            Assert.All(
                observedStagingStream.RetiredBuffersForTesting,
                buffer => Assert.True(
                    buffer.All(value => value == 0),
                    "A superseded plaintext staging buffer was not cleared."));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalContent);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenPlaintextExceedsMaximum_ThrowsResourceLimitExceededException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader resource limit test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        long maximumPlaintextBytes =
            originalContent.Length - 1;

        // Act
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    context.Reader.OpenReadAsync(
                        documentId,
                        ComputeSha256Hash(originalContent),
                        maximumPlaintextBytes:
                            maximumPlaintextBytes));

        // Assert
        Assert.Equal(
            maximumPlaintextBytes,
            exception.LimitBytes);

        Assert.Equal(
            originalContent.Length,
            exception.AttemptedBytes);
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsTampered_ThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault tampered document test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        byte[] encryptedContent =
            await File.ReadAllBytesAsync(
                artifactPath);

        Assert.True(
            encryptedContent.Length > 24);

        encryptedContent[^1] ^= 0xFF;

        await File.WriteAllBytesAsync(
            artifactPath,
            encryptedContent);

        MemoryStream? observedStagingStream = null;

        context.Reader.DecryptedStagingStreamFactoryForTesting =
            () => observedStagingStream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId,
                    ComputeSha256Hash(originalContent)));

        AssertStagingBufferCleared(observedStagingStream);
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredArtifactDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        Guid documentId =
            Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId,
                    ComputeSha256Hash(Array.Empty<byte>())));
    }

    [Fact]
    public async Task OpenReadAsync_WhenCancellationOccursAfterPlaintextStaging_ClearsStagingBuffer()
    {
        using var context = CreateTestContext();

        byte[] originalContent = Encoding.UTF8.GetBytes(
            "Verify staged plaintext is cleared when decryption is cancelled.");

        Guid documentId = Guid.NewGuid();

        string artifactPath = context.PathResolver.GetPath(documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellingMemoryStream? observedStagingStream = null;

        context.Reader.DecryptedStagingStreamFactoryForTesting =
            () => observedStagingStream =
                new CancellingMemoryStream(cancellationTokenSource);

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => context.Reader.OpenReadAsync(
                    documentId,
                    ComputeSha256Hash(originalContent),
                    cancellationTokenSource.Token));

            AssertStagingBufferCleared(observedStagingStream);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalContent);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault cancellation test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId,
                    ComputeSha256Hash(originalContent),
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task OpenReadAsync_UsesCanonicalArtifactForDocumentIdentity()
    {
        // Arrange
        using var context =
            CreateTestContext();

        Guid documentId =
            Guid.NewGuid();

        Guid unrelatedDocumentId =
            Guid.NewGuid();

        byte[] canonicalContent =
            Encoding.UTF8.GetBytes(
                "canonical-document-content");

        byte[] unrelatedContent =
            Encoding.UTF8.GetBytes(
                "unrelated-document-content");

        string canonicalArtifactPath =
            context.PathResolver.GetPath(
                documentId);

        string unrelatedArtifactPath =
            context.PathResolver.GetPath(
                unrelatedDocumentId);

        await CreateEncryptedFileAsync(
            canonicalArtifactPath,
            documentId,
            canonicalContent,
            context.EncryptionService);

        await CreateEncryptedFileAsync(
            unrelatedArtifactPath,
            unrelatedDocumentId,
            unrelatedContent,
            context.EncryptionService);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId,
                ComputeSha256Hash(canonicalContent));

        using var memory =
            new MemoryStream();

        await result.CopyToAsync(
            memory);

        // Assert
        Assert.Equal(
            canonicalContent,
            memory.ToArray());

        Assert.NotEqual(
            unrelatedContent,
            memory.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileUsesLegacyVersionTwo_ReturnsDecryptedContent()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault legacy version two compatibility test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService,
            useLegacyFormatV2: true);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId,
                ComputeSha256Hash(originalContent));

        using var memory =
            new MemoryStream();

        await result.CopyToAsync(
            memory);

        // Assert
        Assert.Equal(
            originalContent,
            memory.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_WhenDecryptedContentDoesNotMatchExpectedHash_ThrowsDocumentContentIntegrityException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault document content integrity test.");

        byte[] differentContent =
            Encoding.UTF8.GetBytes(
                "Different persisted document content.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        string expectedSha256Hash =
            ComputeSha256Hash(
                differentContent);

        MemoryStream? observedStagingStream = null;

        context.Reader.DecryptedStagingStreamFactoryForTesting =
            () => observedStagingStream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<DocumentContentIntegrityException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId,
                    expectedSha256Hash));

        AssertStagingBufferCleared(observedStagingStream);
    }

    [Fact]
    public async Task OpenReadAsync_WhenContentIntegrityFails_DoesNotLogPlaintextHashOrCryptographicMaterial()
    {
        // Arrange
        using var context = CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "SENSITIVE-PLAINTEXT-RECORD-228");

        Guid documentId = Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService);

        byte[] encryptedRecord =
            await File.ReadAllBytesAsync(artifactPath);

        const int finalRecordSize = sizeof(int) + 12 + 16;

        int finalRecordOffset =
            encryptedRecord.Length - finalRecordSize;

        string dataNonce =
            Convert.ToHexString(
                encryptedRecord.AsSpan(12, 12));

        string dataTag =
            Convert.ToHexString(
                encryptedRecord.AsSpan(24, 16));

        string finalNonce =
            Convert.ToHexString(
                encryptedRecord.AsSpan(finalRecordOffset + sizeof(int), 12));

        string finalTag =
            Convert.ToHexString(
                encryptedRecord.AsSpan(finalRecordOffset + sizeof(int) + 12, 16));

        string actualHash =
            ComputeSha256Hash(originalContent);

        string expectedHash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        "Different expected document content.")));

        // Act
        await Assert.ThrowsAsync<DocumentContentIntegrityException>(
            () => context.Reader.OpenReadAsync(
                documentId,
                expectedHash));

        string diagnostics =
            string.Join(
                Environment.NewLine,
                context.ReaderLogger.Entries);

        // Assert
        Assert.NotEmpty(context.ReaderLogger.Entries);

        Assert.DoesNotContain(
            Encoding.UTF8.GetString(originalContent),
            diagnostics,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            actualHash,
            diagnostics,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            expectedHash,
            diagnostics,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            Convert.ToHexString(context.TestKey),
            diagnostics,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            dataNonce,
            diagnostics,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            dataTag,
            diagnostics,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            finalNonce,
            diagnostics,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            finalTag,
            diagnostics,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenReadAsync_WhenLegacyVersionTwoDocumentIsEmpty_ReturnsEmptyContent()
    {
        // Arrange
        using var context =
            CreateTestContext();

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        byte[] originalContent =
            Array.Empty<byte>();

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService,
            useLegacyFormatV2: true);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId,
                ComputeSha256Hash(originalContent));

        using var memory =
            new MemoryStream();

        await result.CopyToAsync(
            memory);

        // Assert
        Assert.Empty(
            memory.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_WhenLegacyVersionTwoDocumentSpansMultipleChunks_ReturnsCompleteContent()
    {
        // Arrange
        using var context =
            CreateTestContext();

        const int chunkSize =
            1024 * 1024;

        byte[] originalContent =
            new byte[chunkSize + 37];

        RandomNumberGenerator.Fill(
            originalContent);

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService,
            useLegacyFormatV2: true);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId,
                ComputeSha256Hash(originalContent));

        using var memory =
            new MemoryStream();

        await result.CopyToAsync(
            memory);

        // Assert
        Assert.Equal(
            originalContent,
            memory.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_WhenLegacyVersionTwoArtifactIsTruncatedAtCompleteChunkBoundary_ThrowsDocumentContentIntegrityException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        const int chunkSize =
            1024 * 1024;

        byte[] originalContent =
            new byte[chunkSize + 37];

        RandomNumberGenerator.Fill(
            originalContent);

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            documentId,
            originalContent,
            context.EncryptionService,
            useLegacyFormatV2: true);

        // Retain the header and first complete chunk, removing the
        // second chunk while leaving the format-2 record structurally complete.
        long firstChunkEnd =
            sizeof(uint) * 2L
            + sizeof(int)
            + 12
            + 16
            + chunkSize;

        await using (
            var artifact = new FileStream(
                artifactPath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None))
        {
            artifact.SetLength(
                firstChunkEnd);
        }

        // Act & Assert
        await Assert.ThrowsAsync<DocumentContentIntegrityException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId,
                    ComputeSha256Hash(originalContent)));
    }

    private static TestContext CreateTestContext()
    {
        return new TestContext();
    }

    private static async Task CreateEncryptedFileAsync(
        string filePath,
        Guid documentId,
        byte[] content,
        DocumentEncryptionService encryptionService,
        bool useLegacyFormatV2 = false)
    {
        string directory =
            Path.GetDirectoryName(
                filePath)!;

        Directory.CreateDirectory(
            directory);

        await using var source =
            new MemoryStream(
                content);

        await using var destination =
            new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

        if (useLegacyFormatV2)
        {
            await encryptionService.EncryptLegacyVersion2Async(
                source,
                destination);
        }
        else
        {
            await encryptionService.EncryptAsync(
                source,
                destination,
                documentId);
        }
    }

    private sealed class CancellingMemoryStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellingMemoryStream(
            CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken);

            if (!buffer.IsEmpty)
            {
                // Cancel after plaintext has been written to the private
                // staging stream. The next decryption read should observe it.
                _cancellationTokenSource.Cancel();
            }
        }
    }

    private sealed class TestContext : IDisposable
    {
        public string RootDirectory { get; }

        public DeskVaultDataPaths DataPaths { get; }

        public DocumentArtifactPathResolver PathResolver { get; }

        public DocumentEncryptionService EncryptionService { get; }

        public EncryptedDocumentReader Reader { get; }

        public byte[] TestKey { get; }

        public RecordingLogger<EncryptedDocumentReader> ReaderLogger { get; }

        public TestContext()
        {
            RootDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                RootDirectory);

            DataPaths =
                new DeskVaultDataPaths(
                    RootDirectory);

            Directory.CreateDirectory(
                DataPaths.DocumentsDirectory);

            PathResolver =
                new DocumentArtifactPathResolver(
                    DataPaths);

            TestKey =
                RandomNumberGenerator.GetBytes(32);

            EncryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(
                        TestKey),
                    NullLogger<DocumentEncryptionService>.Instance);

            Reader =
                new EncryptedDocumentReader(
                    EncryptionService,
                    PathResolver,
                    ReaderLogger =
                        new RecordingLogger<EncryptedDocumentReader>());
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    RootDirectory))
            {
                Directory.Delete(
                    RootDirectory,
                    recursive: true);
            }

            CryptographicOperations.ZeroMemory(TestKey);
        }
    }

    private sealed class TestEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;
        private readonly Guid _keyId = Guid.NewGuid();

        public TestEncryptionKeyService(
            byte[] key)
        {
            _key = key;
        }

        public Task<EncryptionKeyMaterial> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateMaterial());
        }

        private EncryptionKeyMaterial CreateMaterial()
        {
            return new EncryptionKeyMaterial(
                _key.ToArray(),
                _keyId,
                keyVersion: 1);
        }
    }

    private static string ComputeSha256Hash(byte[] content)
    {
        return Convert.ToHexString(
            SHA256.HashData(content))
            .ToLowerInvariant();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add(formatter(state, exception));

            if (exception is not null)
            {
                _entries.Add(exception.ToString());
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private static void AssertStagingBufferCleared(
        MemoryStream? stagingStream)
    {
        Assert.NotNull(stagingStream);

        byte[] buffer = stagingStream!.GetBuffer();

        Assert.NotEmpty(buffer);

        Assert.True(
            buffer.All(value => value == 0),
            "The failed plaintext staging buffer was not cleared.");
    }
}

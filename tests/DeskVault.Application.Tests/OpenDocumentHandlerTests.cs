using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class OpenDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentExists_OpensUsingDocumentIdentity()
    {
        // Arrange
        const long maximumPlaintextBytes = 1024;

        Guid documentId =
            Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                "C:\\OtherDocument\\foreign.dvault");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var documentReader =
            new Mock<IDocumentReader>();

        var expectedContent =
            new MemoryStream(
                "document-content"u8.ToArray());

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                maximumPlaintextBytes,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContent);

        var handler =
            new OpenDocumentHandler(
                repository.Object,
                documentReader.Object,
                new DocumentProcessingOptions
                {
                    MaxDecryptedDocumentBytes =
                        maximumPlaintextBytes
                },
                NullLogger<OpenDocumentHandler>.Instance);

        // Act
        OpenDocumentResult result =
            await handler.HandleAsync(new OpenDocumentQuery(
                    documentId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(
            expectedContent,
            result.Content);

        Assert.Equal(
            document.FileName,
            result.FileName);

        documentReader.Verify(
            x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                maximumPlaintextBytes,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var documentReader =
            new Mock<IDocumentReader>();

        var handler =
            new OpenDocumentHandler(
                repository.Object,
                documentReader.Object,
                new DocumentProcessingOptions(),
                NullLogger<OpenDocumentHandler>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                handler.HandleAsync(new OpenDocumentQuery(
                        documentId), TestContext.Current.CancellationToken));

        documentReader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_WhenMaximumPlaintextBytesIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var repository =
            new Mock<IDocumentRepository>();

        var documentReader =
            new Mock<IDocumentReader>();

        var processingOptions =
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes = 0
            };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new OpenDocumentHandler(
                    repository.Object,
                    documentReader.Object,
                    processingOptions,
                    NullLogger<OpenDocumentHandler>.Instance));
    }
}

using DeskVault.Application.Documents.Commands.ImportDocument;

namespace DeskVault.Application.Tests;

public sealed class ImportDocumentValidatorTests
{
    private readonly ImportDocumentValidator _validator = new();

    [Fact]
    public void Validate_WhenFilePathIsEmpty_ReturnsValidationFailed()
    {
        ImportDocumentCommand command = new(
            string.Empty,
            null);

        ImportDocumentResult result = _validator.Validate(command);

        Assert.Equal(
            ImportDocumentResultStatus.ValidationFailed,
            result.Status);

        Assert.Equal(
            "File path is required.",
            result.Description);
    }

    [Fact]
    public void Validate_WhenFileDoesNotExist_ReturnsFileNotFound()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.txt");

        ImportDocumentCommand command = new(
            filePath,
            null);

        ImportDocumentResult result = _validator.Validate(command);

        Assert.Equal(
            ImportDocumentResultStatus.FileNotFound,
            result.Status);

        Assert.Equal(
            "The specified file does not exist.",
            result.Description);
    }

    [Fact]
    public void Validate_WhenFileTypeIsUnsupported_ReturnsUnsupportedFileType()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.exe");

        File.WriteAllText(filePath, "test");

        try
        {
            ImportDocumentCommand command = new(
                filePath,
                null);

            ImportDocumentResult result =
                _validator.Validate(command);

            Assert.Equal(
                ImportDocumentResultStatus.UnsupportedFileType,
                result.Status);

            Assert.Equal(
                "The file type '.exe' is not supported.",
                result.Description);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Validate_WhenSupportedFileIsEmpty_ReturnsValidationFailed()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.txt");

        File.WriteAllText(filePath, string.Empty);

        try
        {
            ImportDocumentCommand command = new(
                filePath,
                null);

            ImportDocumentResult result =
                _validator.Validate(command);

            Assert.Equal(
                ImportDocumentResultStatus.ValidationFailed,
                result.Status);

            Assert.Equal(
                "The file is empty.",
                result.Description);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Validate_WhenSupportedFileIsNonEmpty_ReturnsSuccess()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.txt");

        File.WriteAllText(filePath, "DeskVault test document");

        try
        {
            ImportDocumentCommand command = new(
                filePath,
                null);

            ImportDocumentResult result =
                _validator.Validate(command);

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                result.Status);

            Assert.Equal(
                "Validation successful.",
                result.Description);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

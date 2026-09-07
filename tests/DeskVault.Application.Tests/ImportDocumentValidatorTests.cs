using DeskVault.Application.Documents.Commands.ImportDocument;

namespace DeskVault.Application.Tests;

public sealed class ImportDocumentValidatorTests
{
    private readonly ImportDocumentValidator _validator = new();

    [Fact]
    public void Validate_WhenFilePathIsEmpty_ReturnsValidationFailed()
    {
        ImportDocumentCommand command =
            new(
                string.Empty,
                null);

        ImportDocumentResult result =
            _validator.Validate(command);

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
        string filePath =
            CreateTemporaryFilePath(".txt");

        ImportDocumentCommand command =
            new(
                filePath,
                null);

        ImportDocumentResult result =
            _validator.Validate(command);

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
        string filePath =
            CreateTemporaryFilePath(".exe");

        try
        {
            File.WriteAllText(
                filePath,
                "test");

            ImportDocumentCommand command =
                new(
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
            DeleteFileIfExists(filePath);
        }
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".csv")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".yaml")]
    [InlineData(".yml")]
    [InlineData(".ini")]
    [InlineData(".config")]
    [InlineData(".log")]
    [InlineData(".c")]
    [InlineData(".cpp")]
    [InlineData(".h")]
    [InlineData(".hpp")]
    [InlineData(".cs")]
    [InlineData(".java")]
    [InlineData(".py")]
    [InlineData(".js")]
    [InlineData(".ts")]
    [InlineData(".css")]
    [InlineData(".sql")]
    [InlineData(".ps1")]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void Validate_WhenMvpSupportedFileIsNonEmpty_ReturnsSuccess(
        string extension)
    {
        string filePath =
            CreateTemporaryFilePath(extension);

        try
        {
            File.WriteAllText(
                filePath,
                "DeskVault test document");

            ImportDocumentCommand command =
                new(
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
            DeleteFileIfExists(filePath);
        }
    }

    [Fact]
    public void Validate_WhenSupportedFileIsEmpty_ReturnsValidationFailed()
    {
        string filePath =
            CreateTemporaryFilePath(".txt");

        try
        {
            File.WriteAllText(
                filePath,
                string.Empty);

            ImportDocumentCommand command =
                new(
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
            DeleteFileIfExists(filePath);
        }
    }

    [Fact]
    public void Validate_WhenSupportedFileIsNonEmpty_ReturnsSuccess()
    {
        string filePath =
            CreateTemporaryFilePath(".txt");

        try
        {
            File.WriteAllText(
                filePath,
                "DeskVault test document");

            ImportDocumentCommand command =
                new(
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
            DeleteFileIfExists(filePath);
        }
    }

    private static string CreateTemporaryFilePath(
        string extension)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}{extension}");
    }

    private static void DeleteFileIfExists(
        string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

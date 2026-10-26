
using System.Text.Json;
using System.Text.RegularExpressions;
using DeskVault.Application.Configurations;
using DeskVault.UI.Hosting;
using Microsoft.Extensions.Configuration;

namespace DeskVault.UI.Tests;

[CollectionDefinition(
    "Environment variable configuration tests",
    DisableParallelization = true)]
public sealed class EnvironmentVariableConfigurationCollection
{
}

[Collection("Environment variable configuration tests")]
public sealed class HostConfigurationPolicyTests
{
    private const string AppsettingsFileName = "appsettings.json";

    private static readonly Regex SensitivePropertyPattern = new(
        @"(?i)(?:password|passwd|pwd|secret|token|api[_-]?key|client[_-]?secret|connectionstrings?|credential|private[_-]?key|access[_-]?key)",
        RegexOptions.CultureInvariant);

    private static readonly Regex CredentialValuePattern = new(
        @"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----" +
        @"|\bAKIA[0-9A-Z]{16}\b" +
        @"|\b(?:gh[pousr]_|github_pat_|sk_live_|sk_test_)[A-Za-z0-9_]{16,}\b" +
        @"|\b(?:password|passwd|pwd|client_secret|api[_-]?key|access[_-]?token|sharedaccesskey|accountkey)\s*[:=]\s*[^;\s,]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void CreateBuilder_LoadsConfigurationFromSpecifiedContentRoot()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        WriteAppsettings(
            directory.Path,
            """
            {
              "Application": {
                "Name": "ConfigurationPolicyTest"
              },
              "Search": {
                "PageSize": 37
              }
            }
            """);

        // Act
        var builder = HostConfigurator.CreateBuilder(directory.Path);
        using var configuration = builder.Configuration;

        // Assert
        Assert.Equal(
            "ConfigurationPolicyTest",
            configuration["Application:Name"]);

        Assert.Equal(
            "37",
            configuration["Search:PageSize"]);
    }

    [Fact]
    public void CreateBuilder_EnvironmentVariablesOverrideJsonSettings()
    {
        // Arrange
        const string variableName = "MarkdownRendering__AllowRawHtml";

        using var environmentVariable =
            new EnvironmentVariableScope(variableName, "true");

        using var directory = new TemporaryDirectory();

        WriteAppsettings(
            directory.Path,
            """
            {
              "MarkdownRendering": {
                "AllowRawHtml": false
              }
            }
            """);

        // Act
        var builder = HostConfigurator.CreateBuilder(directory.Path);
        using var configuration = builder.Configuration;

        // Assert
        Assert.Equal(
            "true",
            configuration["MarkdownRendering:AllowRawHtml"]);
    }

    [Fact]
    public void CreateBuilder_EnvironmentVariablesOverrideJsonResourceLimits()
    {
        // Arrange
        const string variableName = "DocumentProcessing__MaxChunkSize";

        using var environmentVariable =
            new EnvironmentVariableScope(variableName, "1234");

        using var directory = new TemporaryDirectory();

        WriteAppsettings(
            directory.Path,
            """
            {
              "DocumentProcessing": {
                "MaxChunkSize": 4000
              }
            }
            """);

        // Act
        var builder = HostConfigurator.CreateBuilder(directory.Path);
        using var configuration = builder.Configuration;

        var options = configuration
            .GetSection(DocumentProcessingOptions.SectionName)
            .Get<DocumentProcessingOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal(1234, options!.MaxChunkSize);
    }

    [Fact]
    public void CreateBuilder_ThrowsWhenAppsettingsFileIsMissing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var expectedPath = GetAppsettingsPath(directory.Path);

        // Act
        var exception = Assert.Throws<FileNotFoundException>(
            () => HostConfigurator.CreateBuilder(directory.Path));

        // Assert
        Assert.Equal(expectedPath, exception.FileName);
    }

    [Fact]
    public void ShippedAppsettings_DoesNotContainRecognizedSecretPatterns()
    {
        // Arrange
        var assemblyDirectory = Path.GetDirectoryName(
            typeof(HostConfigurator).Assembly.Location);

        Assert.False(
            string.IsNullOrWhiteSpace(assemblyDirectory),
            "Could not determine the UI assembly directory.");

        var appsettingsPath = GetAppsettingsPath(assemblyDirectory!);

        Assert.True(
            File.Exists(appsettingsPath),
            $"Expected shipped appsettings.json beside the UI assembly at '{appsettingsPath}'.");

        using var document = JsonDocument.Parse(
            File.ReadAllText(appsettingsPath));

        var sensitiveProperties = new List<string>();
        var credentialValues = new List<string>();

        // Act
        CollectConfigurationFindings(
            document.RootElement,
            "$",
            sensitiveProperties,
            credentialValues);

        // Assert
        Assert.True(
            sensitiveProperties.Count == 0,
            "Secret-shaped configuration property names were found: " +
            string.Join(", ", sensitiveProperties));

        Assert.True(
            credentialValues.Count == 0,
            "Recognizable credential patterns were found in configuration values at: " +
            string.Join(", ", credentialValues));
    }

    private static string GetAppsettingsPath(string contentRoot) =>
        Path.Combine(contentRoot, AppsettingsFileName);

    private static string WriteAppsettings(
        string contentRoot,
        string json)
    {
        var path = GetAppsettingsPath(contentRoot);
        File.WriteAllText(path, json);
        return path;
    }

    private static void CollectConfigurationFindings(
        JsonElement element,
        string path,
        List<string> sensitiveProperties,
        List<string> credentialValues)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{path}.{property.Name}";

                    if (SensitivePropertyPattern.IsMatch(property.Name))
                    {
                        sensitiveProperties.Add(propertyPath);
                    }

                    CollectConfigurationFindings(
                        property.Value,
                        propertyPath,
                        sensitiveProperties,
                        credentialValues);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    CollectConfigurationFindings(
                        item,
                        $"{path}[{index}]",
                        sensitiveProperties,
                        credentialValues);

                    index++;
                }

                break;

            case JsonValueKind.String:
                var value = element.GetString();

                if (value is not null &&
                    CredentialValuePattern.IsMatch(value))
                {
                    credentialValues.Add(path);
                }

                break;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"DeskVault.ConfigurationPolicyTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);

            Environment.SetEnvironmentVariable(_name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}

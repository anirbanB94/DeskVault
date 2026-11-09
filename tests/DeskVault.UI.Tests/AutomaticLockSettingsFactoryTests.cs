using DeskVault.Application.Configurations;
using DeskVault.UI.Hosting;
using Microsoft.Extensions.Configuration;

namespace DeskVault.UI.Tests;

public sealed class AutomaticLockSettingsFactoryTests
{
    [Fact]
    public void Create_WhenSectionIsAbsent_ReturnsDisabledPolicy()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration();

        // Act
        AutomaticLockSettings settings =
            AutomaticLockSettingsFactory.Create(configuration);

        // Assert
        Assert.False(settings.Enabled);
        Assert.Null(settings.InactivityTimeoutMinutes);
        Assert.Null(settings.InactivityTimeout);
    }

    [Fact]
    public void Create_WhenSectionIsEmpty_Throws()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock", string.Empty));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Fact]
    public void Create_WhenJsonSectionIsPresentButEmpty_Throws()
    {
        // Arrange
        using var stream = new System.IO.MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                """{"Security":{"AutomaticLock":{}}}"""));

        using var configuration =
            (ConfigurationRoot)new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Fact]
    public void Create_WhenEnabledIsMissing_Throws()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:InactivityTimeoutMinutes", "10"));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("enabled")]
    public void Create_WhenEnabledIsNotBoolean_Throws(
        string enabledValue)
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", enabledValue));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Fact]
    public void Create_WhenEnabledIsTrueAndTimeoutIsMissing_Throws()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "true"));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1440", 1440)]
    public void Create_WhenEnabledTimeoutIsValid_ReturnsValidatedPolicy(
        string timeoutValue,
        int expectedMinutes)
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "true"),
                ("Security:AutomaticLock:InactivityTimeoutMinutes", timeoutValue));

        // Act
        AutomaticLockSettings settings =
            AutomaticLockSettingsFactory.Create(configuration);

        // Assert
        Assert.True(settings.Enabled);

        Assert.Equal(
            expectedMinutes,
            settings.InactivityTimeoutMinutes);

        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            settings.InactivityTimeout);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1441")]
    [InlineData("1.5")]
    [InlineData("invalid")]
    [InlineData("")]
    public void Create_WhenEnabledTimeoutIsInvalid_Throws(
        string timeoutValue)
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "true"),
                ("Security:AutomaticLock:InactivityTimeoutMinutes", timeoutValue));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Fact]
    public void Create_WhenDisabledAndTimeoutIsMissing_ReturnsDisabledPolicy()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "false"));

        // Act
        AutomaticLockSettings settings =
            AutomaticLockSettingsFactory.Create(configuration);

        // Assert
        Assert.False(settings.Enabled);
        Assert.Null(settings.InactivityTimeoutMinutes);
    }

    [Fact]
    public void Create_WhenDisabledAndValidTimeoutIsSupplied_AcceptsPolicy()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "false"),
                ("Security:AutomaticLock:InactivityTimeoutMinutes", "20"));

        // Act
        AutomaticLockSettings settings =
            AutomaticLockSettingsFactory.Create(configuration);

        // Assert
        Assert.False(settings.Enabled);
        Assert.Equal(20, settings.InactivityTimeoutMinutes);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("invalid")]
    public void Create_WhenDisabledAndSuppliedTimeoutIsInvalid_Throws(
        string timeoutValue)
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "false"),
                ("Security:AutomaticLock:InactivityTimeoutMinutes", timeoutValue));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(
            () => AutomaticLockSettingsFactory.Create(configuration));
    }

    [Fact]
    public void Create_ReturnsSnapshotUnaffectedByConfigurationChanges()
    {
        // Arrange
        using ConfigurationRoot configuration =
            BuildConfiguration(
                ("Security:AutomaticLock:Enabled", "true"),
                ("Security:AutomaticLock:InactivityTimeoutMinutes", "15"));

        AutomaticLockSettings settings =
            AutomaticLockSettingsFactory.Create(configuration);

        // Act
        configuration["Security:AutomaticLock:Enabled"] = "false";

        configuration["Security:AutomaticLock:InactivityTimeoutMinutes"] = "60";

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(15, settings.InactivityTimeoutMinutes);
        Assert.Equal(TimeSpan.FromMinutes(15), settings.InactivityTimeout);
    }

    private static ConfigurationRoot BuildConfiguration(
        params (string Key, string? Value)[] entries)
    {
        Dictionary<string, string?> values =
            entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);

        return (ConfigurationRoot)new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

[Collection("Environment variable configuration tests")]
public sealed class AutomaticLockSettingsEnvironmentVariableTests
{
    [Fact]
    public void Create_EnvironmentVariablesOverrideJsonSettings()
    {
        // Arrange
        using var enabledVariable =
            new EnvironmentVariableScope(
                "Security__AutomaticLock__Enabled",
                "true");

        using var timeoutVariable =
            new EnvironmentVariableScope(
                "Security__AutomaticLock__InactivityTimeoutMinutes",
                "12");

        using var directory = new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(directory.Path, "appsettings.json"),
            """
            {
              "Security": {
                "AutomaticLock": {
                  "Enabled": false,
                  "InactivityTimeoutMinutes": 5
                }
              }
            }
            """);

        var builder =
            HostConfigurator.CreateBuilder(
                directory.Path);

        using var configurationLifetime =
            (IDisposable)builder.Configuration;

        IConfiguration configuration =
            builder.Configuration;

        // Act
        AutomaticLockSettings settings =
            AutomaticLockSettingsFactory.Create(configuration);

        // Assert
        Assert.True(settings.Enabled);
        Assert.Equal(12, settings.InactivityTimeoutMinutes);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"DeskVault.AutomaticLockTests.{Guid.NewGuid():N}");

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

        public EnvironmentVariableScope(
            string name,
            string value)
        {
            _name = name;
            _originalValue =
                Environment.GetEnvironmentVariable(name);

            Environment.SetEnvironmentVariable(_name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(
                _name,
                _originalValue);
        }
    }
}

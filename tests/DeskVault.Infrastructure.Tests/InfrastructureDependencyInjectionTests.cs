using DeskVault.Application;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DeskVault.Infrastructure.Tests;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersDocumentProcessingPipeline()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentRepository>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentProcessingStore>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentReader>());
    }

    [Fact]
    public void AddInfrastructure_RegistersExpectedConcreteImplementations()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        Assert.IsType<SqliteDocumentRepository>(
            serviceProvider.GetRequiredService<IDocumentRepository>());

        Assert.IsType<SqliteDocumentProcessingStore>(
            serviceProvider.GetRequiredService<IDocumentProcessingStore>());

        Assert.IsType<EncryptedDocumentReader>(
            serviceProvider.GetRequiredService<IDocumentReader>());
    }

    [Fact]
    public void AddInfrastructure_RegistersSessionAuthorizedDatabaseKeyRetrieval()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        IDatabaseEncryptionKeyService retrievalService =
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>();

        Assert.IsType<SessionAuthorizedDatabaseEncryptionKeyService>(
            retrievalService);

        Assert.Same(
            retrievalService,
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>());

        IApplicationSecuritySession firstSecuritySession =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        IApplicationSecuritySession secondSecuritySession =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        Assert.Same(
            firstSecuritySession,
            secondSecuritySession);
    }

    [Fact]
    public void AddInfrastructure_RegistersSessionAuthorizedMasterKeyRetrievalAndIsolatedProvisioning()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        IEncryptionKeyService retrievalService =
            serviceProvider.GetRequiredService<IEncryptionKeyService>();

        IEncryptionKeyProvisioningService provisioningService =
            serviceProvider.GetRequiredService<IEncryptionKeyProvisioningService>();

        Assert.IsType<SessionAuthorizedEncryptionKeyService>(
            retrievalService);

        Assert.IsType<WindowsEncryptionKeyProvisioningService>(
            provisioningService);

        Assert.NotSame(
            retrievalService,
            provisioningService);

        Assert.False(
            provisioningService is IEncryptionKeyService);

        Assert.Same(
            retrievalService,
            serviceProvider.GetRequiredService<IEncryptionKeyService>());

        Assert.Same(
            provisioningService,
            serviceProvider.GetRequiredService<IEncryptionKeyProvisioningService>());
    }

    [Fact]
    public void AddInfrastructure_RegistersDatabaseRetrievalAndProvisioningAsIsolatedCapabilities()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        IDatabaseEncryptionKeyService retrievalService =
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>();

        IDatabaseEncryptionKeyProvisioningService provisioningService =
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyProvisioningService>();

        Assert.IsType<SessionAuthorizedDatabaseEncryptionKeyService>(
            retrievalService);

        Assert.IsType<WindowsDatabaseEncryptionKeyProvisioningService>(
            provisioningService);

        Assert.NotSame(
            retrievalService,
            provisioningService);

        Assert.False(
            provisioningService is IDatabaseEncryptionKeyService);

        Assert.Same(
            retrievalService,
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>());

        Assert.Same(
            provisioningService,
            serviceProvider.GetRequiredService<IDatabaseEncryptionKeyProvisioningService>());
    }

    [Fact]
    public void AddInfrastructure_DoesNotRegisterRawWindowsKeyServices()
    {
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => serviceProvider.GetRequiredService<WindowsEncryptionKeyService>());

        Assert.Throws<InvalidOperationException>(
            () => serviceProvider.GetRequiredService<WindowsDatabaseEncryptionKeyService>());
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Authenticating)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    [InlineData(ApplicationSecuritySessionState.Unavailable)]
    public async Task AddInfrastructure_DeniesDocumentAndDatabaseKeyRetrievalForEveryNonAuthorizedState(
        ApplicationSecuritySessionState state)
    {
        // Arrange
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        IApplicationSecuritySession securitySession =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        SetSessionState(
            securitySession,
            state);

        Assert.Equal(
            state,
            securitySession.CurrentSnapshot.State);

        // Act / Assert
        await AssertBothKeyRetrievalInterfacesDeniedAsync(
            serviceProvider);
    }

    [Theory]
    [InlineData(
        false,
        ApplicationAuthenticationOutcome.Failed,
        ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(
        false,
        ApplicationAuthenticationOutcome.Cancelled,
        ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(
        false,
        ApplicationAuthenticationOutcome.Unavailable,
        ApplicationSecuritySessionState.Unavailable)]
    [InlineData(
        true,
        ApplicationAuthenticationOutcome.Failed,
        ApplicationSecuritySessionState.Locked)]
    [InlineData(
        true,
        ApplicationAuthenticationOutcome.Cancelled,
        ApplicationSecuritySessionState.Locked)]
    [InlineData(
        true,
        ApplicationAuthenticationOutcome.Unavailable,
        ApplicationSecuritySessionState.Unavailable)]
    public async Task AddInfrastructure_DeniesDocumentAndDatabaseKeyRetrievalAfterUnsuccessfulAuthenticationOrUnlock(
        bool beginFromLockedState,
        ApplicationAuthenticationOutcome outcome,
        ApplicationSecuritySessionState expectedState)
    {
        // Arrange
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        IApplicationSecuritySession securitySession =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        if (beginFromLockedState)
        {
            CompleteSuccessfulAuthentication(
                securitySession);

            securitySession.Lock();
        }

        ApplicationSecuritySessionAttempt attempt =
            securitySession.BeginAuthentication();

        // Act
        bool accepted =
            securitySession.TryCompleteAuthentication(
                attempt,
                outcome);

        // Assert
        Assert.True(accepted);

        Assert.Equal(
            expectedState,
            securitySession.CurrentSnapshot.State);

        await AssertBothKeyRetrievalInterfacesDeniedAsync(
            serviceProvider);
    }

    [Fact]
    public void KeyContracts_ExposeRetrievalAndProvisioningAsSeparateCapabilities()
    {
        Assert.Collection(
            typeof(IEncryptionKeyService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IEncryptionKeyService.GetKeyAsync),
                    method.Name));

        Assert.Collection(
            typeof(IDatabaseEncryptionKeyService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IDatabaseEncryptionKeyService.GetKeyAsync),
                    method.Name));

        Assert.Collection(
            typeof(IEncryptionKeyProvisioningService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IEncryptionKeyProvisioningService.GetOrCreateKeyAsync),
                    method.Name));

        Assert.Collection(
            typeof(IDatabaseEncryptionKeyProvisioningService).GetMethods(),
            method =>
                Assert.Equal(
                    nameof(IDatabaseEncryptionKeyProvisioningService.GetOrCreateKeyAsync),
                    method.Name));

        Assert.Empty(
            typeof(IEncryptionKeyProvisioningService).GetInterfaces());

        Assert.Empty(
            typeof(IDatabaseEncryptionKeyProvisioningService).GetInterfaces());
    }

    [Fact]
    public void AddInfrastructure_RegistersWindowsHelloAuthenticationProviderAndPlatformApiAsSingletons()
    {
        // Arrange
        using ServiceProvider serviceProvider =
            CreateServiceProvider();

        // Act
        IApplicationAuthenticationProvider firstProvider =
            serviceProvider.GetRequiredService<IApplicationAuthenticationProvider>();

        IApplicationAuthenticationProvider secondProvider =
            serviceProvider.GetRequiredService<IApplicationAuthenticationProvider>();

        IWindowsHelloPlatformApi firstPlatformApi =
            serviceProvider.GetRequiredService<IWindowsHelloPlatformApi>();

        IWindowsHelloPlatformApi secondPlatformApi =
            serviceProvider.GetRequiredService<IWindowsHelloPlatformApi>();

        // Assert
        Assert.IsType<WindowsHelloAuthenticationProvider>(
            firstProvider);

        Assert.Same(
            firstProvider,
            secondProvider);

        Assert.IsType<WindowsHelloPlatformApi>(
            firstPlatformApi);

        Assert.Same(
            firstPlatformApi,
            secondPlatformApi);
    }

    private static void SetSessionState(
        IApplicationSecuritySession securitySession,
        ApplicationSecuritySessionState state)
    {
        switch (state)
        {
            case ApplicationSecuritySessionState.Unauthenticated:
                return;

            case ApplicationSecuritySessionState.Authenticating:
                _ = securitySession.BeginAuthentication();
                return;

            case ApplicationSecuritySessionState.Authenticated:
                CompleteSuccessfulAuthentication(
                    securitySession);
                return;

            case ApplicationSecuritySessionState.Locked:
                CompleteSuccessfulAuthentication(
                    securitySession);

                securitySession.Lock();
                return;

            case ApplicationSecuritySessionState.Unavailable:
                securitySession.MarkUnavailable();
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "Unsupported application security-session state.");
        }
    }

    private static void CompleteSuccessfulAuthentication(
        IApplicationSecuritySession securitySession)
    {
        ApplicationSecuritySessionAttempt attempt =
            securitySession.BeginAuthentication();

        Assert.True(
            securitySession.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));
    }

    private static async Task AssertBothKeyRetrievalInterfacesDeniedAsync(
        IServiceProvider serviceProvider)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => serviceProvider
                .GetRequiredService<IEncryptionKeyService>()
                .GetKeyAsync(TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => serviceProvider
                .GetRequiredService<IDatabaseEncryptionKeyService>()
                .GetKeyAsync(TestContext.Current.CancellationToken));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton<IWindowsHelloWindowHandleProvider>(
            Mock.Of<IWindowsHelloWindowHandleProvider>());

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddLogging();

        services.AddApplication();

        services.AddInfrastructure(
            configuration);

        return services.BuildServiceProvider();
    }
}

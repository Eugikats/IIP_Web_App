using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using InsuranceIntelligence.Infrastructure.Graph;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CommunicationProviderRegistrationTests
{
    [Fact]
    public void InfrastructureRegistration_UsesGraphSenderForGraphEmailMode()
    {
        using var provider = BuildProvider("GraphEmail");

        Assert.IsType<GraphCommunicationSendingProvider>(provider.GetRequiredService<ICommunicationSendingProvider>());
        // ISmsProvider is always TenantAwareSmsProvider now (the three-step resolver), it is no
        // longer coupled to CommunicationProvider ("GraphEmail" only ever governed email mode).
        // See TenantAwareSmsProviderTests for behavioral coverage of the resolution order itself.
        Assert.IsType<InsuranceIntelligence.Infrastructure.Communications.TenantAwareSmsProvider>(provider.GetRequiredService<ISmsProvider>());
    }

    [Fact]
    public void InfrastructureRegistration_UsesDisabledSenderWhenCommunicationIsDisabled()
    {
        using var provider = BuildProvider("Disabled");

        Assert.IsType<DisabledCommunicationSendingProvider>(provider.GetRequiredService<ICommunicationSendingProvider>());
        Assert.IsType<InsuranceIntelligence.Infrastructure.Communications.TenantAwareSmsProvider>(provider.GetRequiredService<ISmsProvider>());

        // WhatsApp inbound/outbound providers are registered unconditionally now (blueprint §3.3,
        // §4.4): a validated tenant connection can go live with no redeploy. Without
        // INSURANCEINTELLIGENCE_CHANNEL_PROVIDER=WhatsAppCloudApi or a saved connection in this
        // test's isolated database, the real provider still honestly reports IsConfigured == false
        //, the same guarantee DisabledWhatsAppInboundProvider used to provide by being a different
        // type, now provided by the real type's own state instead.
        Assert.False(provider.GetRequiredService<IWhatsAppInboundProvider>().IsConfigured);
    }

    [Fact]
    public void InfrastructureRegistration_WhatsAppProvidersAreAlwaysTheRealType()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INSURANCEINTELLIGENCE_STORAGE_PROVIDER"] = "SqlServer",
                ["INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING"] = "Server=(localdb)\\MSSQLLocalDB;Database=IipRegistrationTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["INSURANCEINTELLIGENCE_EMAIL_PROVIDER"] = "Graph",
                ["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] = "Disabled",
                ["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] = "Disabled",
                ["INSURANCEINTELLIGENCE_CHANNEL_PROVIDER"] = "WhatsAppCloudApi",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInsuranceIntelligenceInfrastructure(configuration, new TestEnvironment("Development"));
        services.AddDataProtection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWhatsAppAttachmentProcessor)
                && descriptor.ImplementationType == typeof(WhatsAppAttachmentProcessor));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<WhatsAppCloudApiInboundProvider>(provider.GetRequiredService<IWhatsAppInboundProvider>());
        // Every send passes the content guard and the per-number rate limit before the live sender.
        Assert.IsType<GuardedWhatsAppOutboundProvider>(provider.GetRequiredService<IWhatsAppOutboundProvider>());
    }

    private static ServiceProvider BuildProvider(string communicationProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INSURANCEINTELLIGENCE_STORAGE_PROVIDER"] = "SqlServer",
                ["INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING"] = "Server=(localdb)\\MSSQLLocalDB;Database=IipRegistrationTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["INSURANCEINTELLIGENCE_EMAIL_PROVIDER"] = "Graph",
                ["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] = communicationProvider,
                ["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] = "Disabled"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInsuranceIntelligenceInfrastructure(configuration, new TestEnvironment("Development"));
        services.AddDataProtection();
        return services.BuildServiceProvider();
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "InsuranceIntelligence.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

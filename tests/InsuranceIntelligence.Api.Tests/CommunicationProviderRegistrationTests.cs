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
        Assert.IsType<DisabledSmsProvider>(provider.GetRequiredService<ISmsProvider>());
    }

    [Fact]
    public void InfrastructureRegistration_UsesDisabledSenderWhenCommunicationIsDisabled()
    {
        using var provider = BuildProvider("Disabled");

        Assert.IsType<DisabledCommunicationSendingProvider>(provider.GetRequiredService<ICommunicationSendingProvider>());
        Assert.IsType<DisabledSmsProvider>(provider.GetRequiredService<ISmsProvider>());
        Assert.IsType<DisabledWhatsAppInboundProvider>(provider.GetRequiredService<IWhatsAppInboundProvider>());
    }

    [Fact]
    public void InfrastructureRegistration_UsesWhatsAppCloudApiWhenChannelConfigured()
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

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWhatsAppAttachmentProcessor)
                && descriptor.ImplementationType == typeof(WhatsAppAttachmentProcessor));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<WhatsAppCloudApiInboundProvider>(provider.GetRequiredService<IWhatsAppInboundProvider>());
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

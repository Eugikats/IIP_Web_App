namespace InsuranceIntelligence.Api.CommandCenter;

public sealed record SystemCapabilityStatusDto(
    bool ActiveAi,
    bool ActiveDocumentExtraction,
    bool ActiveKnowledgeRetrieval,
    bool ActiveEmailIngestion,
    bool ActiveSmsDelivery,
    bool ActiveTeamsNotification,
    bool ActiveChannels,
    bool ActiveOnboarding,
    string Environment,
    string AuthMode);

public interface ISystemCapabilityStatusService
{
    Task<SystemCapabilityStatusDto> GetAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed class SystemCapabilityStatusService(
    InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions options,
    IHostEnvironment environment) : ISystemCapabilityStatusService
{
    public Task<SystemCapabilityStatusDto> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var envProfile = Azure.AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);

        var status = new SystemCapabilityStatusDto(
            ActiveAi: !options.AiProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveDocumentExtraction: !options.DocumentProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveKnowledgeRetrieval: !options.RetrievalProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveEmailIngestion: !options.EmailProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveSmsDelivery: !options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveTeamsNotification: !options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveChannels: !options.ChannelProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            ActiveOnboarding: !options.ProvisionerProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase),
            Environment: envProfile,
            AuthMode: options.AuthMode);

        return Task.FromResult(status);
    }
}

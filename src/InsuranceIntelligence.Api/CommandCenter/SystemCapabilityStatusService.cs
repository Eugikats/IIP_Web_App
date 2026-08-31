using InsuranceIntelligence.Infrastructure.Configuration;

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
            ActiveAi: ProviderModeSupport.IsLiveProviderMode(options.AiProvider, "AzureOpenAI"),
            ActiveDocumentExtraction: ProviderModeSupport.IsLiveProviderMode(options.DocumentProvider, "AzureDocumentIntelligence"),
            ActiveKnowledgeRetrieval: ProviderModeSupport.IsLiveProviderMode(options.RetrievalProvider, "AzureAiSearch"),
            ActiveEmailIngestion: ProviderModeSupport.IsLiveProviderMode(options.EmailProvider, "Graph"),
            ActiveSmsDelivery: ProviderModeSupport.IsLiveProviderMode(options.SmsProvider, "AfricasTalking"),
            ActiveTeamsNotification: ProviderModeSupport.IsLiveProviderMode(options.NotificationProvider, "IncomingWebhook"),
            ActiveChannels: ProviderModeSupport.IsLiveProviderMode(options.ChannelProvider, "WhatsAppCloudApi"),
            ActiveOnboarding: ProviderModeSupport.IsLiveProviderMode(options.ProvisionerProvider, "Entra"),
            Environment: envProfile,
            AuthMode: options.AuthMode);

        return Task.FromResult(status);
    }
}

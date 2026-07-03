using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Integrations;

public sealed record CreateMailboxRequest(
    string DisplayName,
    string MailboxAddress,
    bool IngestionEnabled,
    string DefaultChannel,
    string? ProductLine = null,
    string? ProcessedFolderName = null);

public sealed record ProcessEmailMessageRequest(Guid MessageId);

public sealed record EmailIntegrationHealthResponse(
    string IntegrationId,
    string DisplayName,
    string OverallStatus,
    IReadOnlyCollection<ProviderHealthItem> Providers,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);

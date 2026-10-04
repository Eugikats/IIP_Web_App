using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Communications;

public interface ICommunicationRepository
{
    Task SaveAsync(Communication communication, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Finds the communication a delivery-status webhook refers to, by the sending
    /// provider's own message id. Used by SmsIntegrationEndpoints to turn a raw "Success"/"Failed"
    /// callback into a real delivery-status update on the right record.</summary>
    Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default);

    /// <summary>The communication the gateway's own message id refers to, in whichever tenant sent it.
    /// Null when none matches, or when more than one does (two tenants' gateway accounts reusing an
    /// id): an ambiguous delivery receipt updates nothing rather than the wrong insurer's record.</summary>
    Task<Communication?> FindByProviderMessageIdAcrossTenantsAsync(string providerMessageId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Communication?>(null);
}

public interface ICommunicationSendingProvider
{
    Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);

    Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);
}

public interface ISmsProvider
{
    Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Constructs a live ISmsProvider from a provider key and a flat settings dictionary, either
/// decrypted per-tenant connection settings or deployment env-var settings. This is the seam that
/// makes "add a new SMS gateway" a new class registered here, not a re-architecture: see
/// EgoSmsProvider and AfricasTalkingSmsProvider in InsuranceIntelligence.Infrastructure.Communications.
/// </summary>
public interface ISmsProviderFactory
{
    /// <summary>Provider keys this factory can construct (e.g. "AfricasTalking", "EgoSms").</summary>
    IReadOnlyCollection<string> SupportedProviderKeys { get; }

    /// <summary>Builds a provider from tenant-supplied connection settings. Returns null if
    /// providerKey is not recognized, the caller treats that as a real configuration error, not a
    /// silent fallback.</summary>
    ISmsProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings);

    /// <summary>Builds the deployment-level fallback provider from environment configuration,
    /// the second step of the three-step resolution order (tenant connection, then env fallback,
    /// then Not configured). Never null: resolves to a provider that honestly reports
    /// ConfigurationRequired when no environment provider is configured.</summary>
    ISmsProvider CreateFallback();
}

/// <summary>Sends outbound email, separated from ICommunicationSendingProvider (SMS + Email
/// together) so email has its own tenant-connectable seam (blueprint §7.2), the same way
/// ISmsProvider does for SMS.</summary>
public interface IEmailSendingProvider
{
    Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Constructs a live IEmailSendingProvider from a provider key and settings, the email
/// equivalent of ISmsProviderFactory. Only SMTP is tenant-connectable (a username/password any
/// insurer can supply); Graph requires an Entra app registration and stays deployment-level, so it
/// is available only via CreateFallback, never via Create.</summary>
public interface IEmailSendingProviderFactory
{
    IReadOnlyCollection<string> SupportedProviderKeys { get; }

    IEmailSendingProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings);

    IEmailSendingProvider CreateFallback();
}

public sealed record CommunicationSendRequest(
    TenantScope Scope,
    // Outcome O: null for a message that is not about any claim (a retention reminder), since
    // every existing caller already passes a real case id, this stays additive.
    Guid? CaseId,
    CommunicationChannel Channel,
    string Recipient,
    string MessageBody,
    string SentByUserId,
    bool Approved,
    bool LowRisk,
    string CorrelationId);

public sealed record CommunicationSendResult(string DeliveryStatus, string ProviderMessageId, string? SafeError);

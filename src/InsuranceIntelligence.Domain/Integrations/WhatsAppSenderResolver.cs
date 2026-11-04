using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>A WhatsApp number a workspace can send from, and the provider that sends for it.</summary>
public sealed record WhatsAppSender(string BusinessPhoneNumberId, IWhatsAppOutboundProvider Provider);

/// <summary>
/// Finds the WhatsApp sender for a workspace (build-loop item 21). Sending a customer update on WhatsApp
/// needs the workspace's own business number and a provider that is really connected. Returning null is the
/// honest answer when there is none, never a pretend sender. Item 27 makes the answer per tenant.
/// </summary>
public interface IWhatsAppSenderResolver
{
    Task<WhatsAppSender?> ResolveAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

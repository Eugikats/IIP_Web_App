using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>
/// Issues the claimant portal link the WhatsApp assistant hands to a customer right after it
/// opens their claim. Same single-use, hash-only token the magic-link page issues
/// (ClaimantAuthenticationEndpoints.IssueAndSendAsync): the raw value exists only in the message
/// and the customer's browser, and redeeming it creates a session scoped to that one claim.
///
/// It lives longer than the magic-link page's 15 minutes because of where it is sent. A link the
/// customer asked for on a web page is used at once; a link in a WhatsApp conversation is opened
/// when the customer has the photos and papers to hand, often hours later. It is delivered only
/// to the WhatsApp number the conversation came from, and stays single-use.
/// </summary>
public sealed class WhatsAppClaimantAccessLinkIssuer(
    IClaimantMagicLinkTokenRepository tokenRepository,
    InfrastructureOptions options) : IClaimantAccessLinkIssuer
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    public async Task<string?> IssueForClaimAsync(
        TenantScope scope,
        Guid caseId,
        string policyNumber,
        string contactPhone,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ResolveBaseUrl(options);
        if (baseUrl is null)
        {
            return null;
        }

        var rawToken = ClaimantTokenGenerator.GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        await tokenRepository.SaveAsync(new ClaimantMagicLinkToken
        {
            TokenId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            PolicyNumber = policyNumber,
            ContactValue = contactPhone,
            Channel = ClaimantContactChannel.Sms,
            TokenHash = ClaimantTokenGenerator.Hash(rawToken),
            ExpiresAt = now.Add(LinkLifetime),
            CreatedAt = now,
            CorrelationId = correlationId,
            // Delivered inside the WhatsApp reply itself, which the assistant records as a sent
            // message on the conversation.
            DeliveryStatus = "SentInWhatsAppConversation",
        }, cancellationToken);

        return $"{baseUrl}/access/verify?token={Uri.EscapeDataString(rawToken)}";
    }

    // Development falls back to the local portal, the same rule the magic-link page applies; any
    // other profile without a configured address gets no link rather than a broken one.
    private static string? ResolveBaseUrl(InfrastructureOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientPortalBaseUrl))
        {
            return options.ClientPortalBaseUrl.TrimEnd('/');
        }

        return options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            ? "http://localhost:3000"
            : null;
    }
}

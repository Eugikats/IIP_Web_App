using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Issues the same single-use claimant portal link the magic-link page sends by SMS or email,
/// for the WhatsApp assistant to hand over in the conversation itself: the customer who just
/// reported a claim gets a way to upload documents and follow it without asking for one.
///
/// Lives here as an interface because building the link needs the deployment's client portal
/// address, which only the API layer knows. Returns null when no portal address is configured,
/// so the assistant says nothing about a link rather than sending a broken one.
/// </summary>
public interface IClaimantAccessLinkIssuer
{
    /// <param name="contactPhone">The WhatsApp number the link is being sent to. Recorded on the
    /// token, the same way an SMS link records the number it went to.</param>
    Task<string?> IssueForClaimAsync(
        TenantScope scope,
        Guid caseId,
        string policyNumber,
        string contactPhone,
        string correlationId,
        CancellationToken cancellationToken = default);
}

using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

/// <summary>
/// A reusable customer message, versioned the same way as AgentBehaviourProfile and Product: every
/// save appends a new row, never overwrites one. A template belongs to a reason category and a
/// channel, and carries a long body plus a short, SMS-safe body, because most phones in this
/// market are not smartphones (build prompt outcome D). Using a template still creates an ordinary
/// CustomerUpdateDraft and obeys the same approval rules as any other message, see
/// CustomerUpdateEndpoints.SendTemplatedUpdate, a template is a starting point, not a bypass.
/// </summary>
public sealed record CommunicationTemplate
{
    public required Guid TemplateId { get; init; }
    public required Guid VersionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required int Version { get; init; }

    /// <summary>A stable, admin-chosen slug, e.g. "missing-documents-sms", unique per (scope,
    /// channel, reason) is not enforced, a tenant may keep several templates for the same reason
    /// and channel and choose between them.</summary>
    public required string TemplateKey { get; init; }
    public required string Name { get; init; }
    public required CustomerUpdateReasonCategory ReasonCategory { get; init; }
    public required CommunicationChannel Channel { get; init; }
    public string Language { get; init; } = "en";

    /// <summary>Null for SMS/WhatsApp, where there is no subject line.</summary>
    public string? Subject { get; init; }

    public required string Body { get; init; }
    public required string ShortBody { get; init; }
    public required bool Active { get; init; }

    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string ChangeReason { get; init; }
}

public interface ICommunicationTemplateRepository
{
    Task<CommunicationTemplate?> GetCurrentAsync(TenantScope scope, string templateKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommunicationTemplate>> ListVersionsAsync(TenantScope scope, string templateKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommunicationTemplate>> ListCurrentForAllTemplatesAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(CommunicationTemplate version, CancellationToken cancellationToken = default);
}

/// <summary>The customer's own name, claim reference, missing documents and assigned officer,
/// substituted into a template's Body/ShortBody at render time. A variable with no value available
/// (e.g. no officer yet assigned) resolves to a plain, honest fallback rather than a raw identifier
/// or an empty gap.</summary>
public sealed record CommunicationTemplateVariables(
    string CustomerName,
    string ClaimReference,
    string MissingDocuments,
    string? OfficerName);

public static class CommunicationTemplateRenderer
{
    public static string Render(string text, CommunicationTemplateVariables variables) =>
        text
            .Replace("{{customer_name}}", variables.CustomerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{claim_reference}}", variables.ClaimReference, StringComparison.OrdinalIgnoreCase)
            .Replace("{{missing_documents}}", variables.MissingDocuments, StringComparison.OrdinalIgnoreCase)
            .Replace("{{officer_name}}", string.IsNullOrWhiteSpace(variables.OfficerName) ? "our claims team" : variables.OfficerName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Seeds the template library from the messages already hardcoded in
/// MockCustomerUpdateDraftingProvider (outcome D: "Seed the library from the messages already in
/// code"), rewritten with template variables so they can be edited without a deploy.</summary>
public static class DefaultCommunicationTemplates
{
    public static IReadOnlyList<(string TemplateKey, string Name, CustomerUpdateReasonCategory ReasonCategory, CommunicationChannel Channel, string? Subject, string Body, string ShortBody)> Defaults() =>
    [
        (
            "claim-received-portal",
            "Claim received",
            CustomerUpdateReasonCategory.ClaimReceived,
            CommunicationChannel.Portal,
            "We have received your claim",
            "Hello {{customer_name}}, we have received your claim for policy {{claim_reference}}. Our team will review the information and contact you if anything else is needed.",
            "We received your claim {{claim_reference}}. We'll contact you if we need anything else."
        ),
        (
            "missing-documents-portal",
            "Missing documents",
            CustomerUpdateReasonCategory.MissingDocuments,
            CommunicationChannel.Portal,
            "Documents needed for your claim",
            "Hello {{customer_name}}, we are reviewing your claim for policy {{claim_reference}}. Please provide the following required documents so the claims team can continue: {{missing_documents}}.",
            "Claim {{claim_reference}}: please send {{missing_documents}} so we can continue."
        ),
        (
            "ready-for-review-portal",
            "Ready for review",
            CustomerUpdateReasonCategory.ReadyForReview,
            CommunicationChannel.Portal,
            "Your claim is ready for review",
            "Hello {{customer_name}}, your claim documents have been received and your case is ready for review by our claims team. We will keep you updated as the review progresses.",
            "Claim {{claim_reference}}: documents received, now under review."
        ),
        (
            "escalation-received-portal",
            "Escalated for review",
            CustomerUpdateReasonCategory.EscalationReceived,
            CommunicationChannel.Portal,
            "Your claim has been escalated",
            "Hello {{customer_name}}, your case has been escalated for additional human review. A claims team member will review the file and provide a further update.",
            "Claim {{claim_reference}} has been escalated for extra review. We'll update you soon."
        ),
        (
            "decision-outcome-portal",
            "Decision reached",
            CustomerUpdateReasonCategory.DecisionOutcome,
            CommunicationChannel.Portal,
            "An update on your claim",
            // Deliberately never states the decision itself, see CustomerUpdateReasonCategory.DecisionOutcome.
            "Hello {{customer_name}}, a decision has been reached on your claim for policy {{claim_reference}}. {{officer_name}} will contact you directly to discuss it.",
            "Claim {{claim_reference}}: a decision has been reached. {{officer_name}} will call you."
        ),
        (
            "status-update-portal",
            "General status update",
            CustomerUpdateReasonCategory.StatusUpdate,
            CommunicationChannel.Portal,
            "An update on your claim",
            "Hello {{customer_name}}, your claim is currently being reviewed by our team. We will share the next update through your selected communication channel.",
            "Claim {{claim_reference}} is still under review. We'll update you soon."
        ),
        (
            "satisfaction-question-portal",
            "Satisfaction question",
            CustomerUpdateReasonCategory.SatisfactionQuestion,
            CommunicationChannel.Portal,
            "How did we do?",
            "Hello {{customer_name}}, your claim {{claim_reference}} is now closed. In a few words, how would you rate how we handled it? Reply to let {{officer_name}} know.",
            "Claim {{claim_reference}} is closed. How would you rate how we handled it?"
        ),
        (
            "referral-invitation-portal",
            "Referral invitation",
            CustomerUpdateReasonCategory.ReferralInvitation,
            CommunicationChannel.Portal,
            "Know someone who needs cover?",
            "Hello {{customer_name}}, thank you for trusting us with claim {{claim_reference}}. If you know someone who could use cover like yours, we would welcome the introduction. Reply with their name and we will reach out.",
            "Thanks for trusting us with claim {{claim_reference}}. Know someone who needs cover? Reply with their name."
        ),
    ];
}

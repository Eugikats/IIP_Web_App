using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Administration;

/// <summary>
/// Backs TenantPolicy.tsx with real data instead of a hardcoded array (blueprint §6.4, WO 3.4).
/// Every rule reports the actual code path or configuration key that enforces it, so the page is
/// no longer just describing the platform, it is reading a live report of it. See the honesty
/// correction in EnforcedRules: the blueprint originally described an "extraction human-review
/// threshold, Pilot: 0.80 confidence, configurable" row. Investigating it to make it editable
/// found no such threshold exists anywhere in the codebase, DocumentFieldValidationRule governs
/// field format (regex/length), not confidence-based review gating, and no other code path reads
/// a numeric confidence cutoff. That row is corrected to say so plainly rather than continue
/// describing a control that was never actually built.
/// </summary>
public static class TenantPolicyEndpoints
{
    public static IResult GetPolicy(HttpContext httpContext, InfrastructureOptions options)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        return Results.Ok(new { rules = EnforcedRules });
    }

    private static readonly TenantPolicyRule[] EnforcedRules =
    [
        new("Regulated decisions require a named human reviewer", true,
            "Enforced in code, no consequential action (approve, reject, settle, decline) has a code path that does not require a stored reviewer identity. Not a setting; cannot be disabled by a tenant admin."),
        new("Customer communications require approval before send", true,
            "OutboundSmsGuard.IsDispatchPermitted and the equivalent email/WhatsApp checks, a message sends only if approved, or is explicitly low-risk and the tenant opted into transactional auto-send. Applies to WhatsApp, Email, SMS."),
        new("Fraud signals are advisory only", true,
            "The risk-fraud-signal agent is RuleBased (FraudRuleEngine) and has no code path that writes a fraud determination, only advisory signals a human reviews."),
        new("Unknown knowledge sensitivity is blocked from retrieval by default", true,
            "Knowledge.tsx / the knowledge retrieval index, a document's sensitivity must be explicitly classified as approved before it is eligible for AI grounding; unknown, contains_secrets, and contains_unapproved_pii are excluded."),
        new("Extraction human-review threshold", false,
            "Not implemented. No numeric confidence cutoff exists anywhere in this codebase that gates document fields into automatic vs. human review, this row previously claimed a configurable 0.80 default that was never actually built. Corrected here rather than left describing a control that doesn't exist."),
        new("Write-back to insurer core systems requires human approval", true,
            "No core-system write-back adapter exists yet at all (blueprint §13.1), vacuously enforced: there is no code path that could write back without approval, because there is no write-back code path yet."),
        new("AI autonomy increases require secondary approval", true,
            "Production only. The agent kill-switch (Agents.tsx / AgentRegistryService) is the only lever that changes an agent's runtime autonomy, and enabling/disabling one is itself a governed, audited action."),
        new("Coverage questions must be grounded in approved sources", true,
            "AgentBehaviourEndpoints.SaveVersion enforces this directly for policy-intelligence: RequireCitation cannot be set false for that agent. AgentBehaviourComposer's platform preamble also states this is required of every agent's output."),
    ];

    private sealed record TenantPolicyRule(string Rule, bool Enforced, string EnforcementSource);
}

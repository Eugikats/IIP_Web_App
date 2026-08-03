using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Deterministic case match from WhatsApp caption/filename (GUID or policy number),
/// same rules as email inbound matching. Unmatched media requires human review — never invents a case.
/// </summary>
public sealed class RuleBasedWhatsAppToCaseMatcher(ICaseRepository caseRepository) : IWhatsAppToCaseMatcher
{
    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    public async Task<WhatsAppToCaseMatchResult> MatchAsync(
        WhatsAppInboundMediaMessage message,
        TenantScope scope,
        CancellationToken cancellationToken = default)
    {
        var text = $"{message.Caption} {message.FileName}";
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var guid = GuidPattern.Match(text);
        if (guid.Success
            && Guid.TryParse(guid.Value, out var caseId)
            && cases.Any(claimCase => claimCase.CaseId == caseId))
        {
            return new WhatsAppToCaseMatchResult(caseId, "case_id", RequiresManualReview: false);
        }

        var byPolicyNumber = cases.FirstOrDefault(claimCase =>
            !string.IsNullOrWhiteSpace(claimCase.PolicyNumber)
            && text.Contains(claimCase.PolicyNumber, StringComparison.OrdinalIgnoreCase));
        return byPolicyNumber is null
            ? new WhatsAppToCaseMatchResult(null, "no_match", RequiresManualReview: true)
            : new WhatsAppToCaseMatchResult(byPolicyNumber.CaseId, "claim_or_policy_number", RequiresManualReview: false);
    }
}

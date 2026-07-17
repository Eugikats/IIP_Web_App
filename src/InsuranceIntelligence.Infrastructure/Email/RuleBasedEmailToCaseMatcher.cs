using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Email;

public sealed class RuleBasedEmailToCaseMatcher(ICaseRepository caseRepository) : IEmailToCaseMatcher
{
    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    public async Task<EmailToCaseMatchResult> MatchAsync(
        InboundEmailMessage message,
        TenantScope scope,
        CancellationToken cancellationToken = default)
    {
        var text = $"{message.Subject} {message.BodyPreview}";
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var guid = GuidPattern.Match(text);
        if (guid.Success
            && Guid.TryParse(guid.Value, out var caseId)
            && cases.Any(claimCase => claimCase.CaseId == caseId))
        {
            return new EmailToCaseMatchResult(caseId, "case_id", false);
        }

        var byPolicyNumber = cases.FirstOrDefault(claimCase =>
            !string.IsNullOrWhiteSpace(claimCase.PolicyNumber)
            && text.Contains(claimCase.PolicyNumber, StringComparison.OrdinalIgnoreCase));
        return byPolicyNumber is null
            ? new EmailToCaseMatchResult(null, "no_match", true)
            : new EmailToCaseMatchResult(byPolicyNumber.CaseId, "claim_or_policy_number", false);
    }
}

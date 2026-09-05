using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>
/// Deterministic case match from email subject/body (GUID or policy number). When neither matches,
/// falls back to identifying the sender's From address against a customer's email on file — never
/// against the message content, which the sender fully controls (a spoofed or unrelated From
/// header can claim any policy number in its body) — so <see cref="EmailToCaseMatchResult.CreateNewCase"/>
/// carries a verified identity, not just an unverified claim in free text.
/// </summary>
public sealed class RuleBasedEmailToCaseMatcher(
    ICaseRepository caseRepository,
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository) : IEmailToCaseMatcher
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
        if (byPolicyNumber is not null)
        {
            return new EmailToCaseMatchResult(byPolicyNumber.CaseId, "claim_or_policy_number", false);
        }

        var identifiedSender = await IdentifySenderAsync(message.Sender, scope, cancellationToken);
        return new EmailToCaseMatchResult(null, identifiedSender is null ? "no_match" : "identified_sender_no_case", true, identifiedSender);
    }

    private async Task<IdentifiedSender?> IdentifySenderAsync(
        string senderEmail,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        var customers = await customerRepository.ListAsync(scope, cancellationToken);
        var customer = customers.FirstOrDefault(c => ClaimantContactMatcher.Matches(senderEmail, c.Email));
        if (customer is null)
        {
            return null;
        }

        var policies = await policyRepository.ListByCustomerAsync(customer.CustomerId, scope, cancellationToken);
        var policy = policies.FirstOrDefault();
        return policy is null
            ? null
            : new IdentifiedSender(customer.CustomerId, customer.FullName, customer.Email, customer.Phone, policy.PolicyNumber, policy.ProductLine);
    }
}

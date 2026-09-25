using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Deterministic case match from WhatsApp caption/filename (GUID or policy number), same rules as
/// email inbound matching. When neither matches, falls back to identifying the sender's WA ID
/// against a customer's phone number on file, never against the message content, which the
/// sender fully controls, so an inbound-ingestion pipeline can tell a genuinely identified
/// customer apart from an unverified stranger before deciding whether to auto-create a case.
/// Still never invents a case for a sender it cannot identify.
/// </summary>
public sealed class RuleBasedWhatsAppToCaseMatcher(
    ICaseRepository caseRepository,
    ICustomerRepository customerRepository,
    IPolicyRepository policyRepository) : IWhatsAppToCaseMatcher
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
        if (byPolicyNumber is not null)
        {
            return new WhatsAppToCaseMatchResult(byPolicyNumber.CaseId, "claim_or_policy_number", RequiresManualReview: false);
        }

        var identifiedSender = await IdentifySenderAsync(message.SenderWaId, scope, cancellationToken);
        if (identifiedSender is null)
        {
            return new WhatsAppToCaseMatchResult(null, "no_match", RequiresManualReview: true);
        }

        // A recognised customer sending photos after reporting a claim is adding to that claim.
        // Previously every photo without a claim reference in its caption opened a brand-new
        // claim, so three photos of one accident became three claims for an officer to merge.
        var openClaim = cases
            .Where(claimCase => claimCase.Status is not (CaseStatus.Settled or CaseStatus.Closed))
            .Where(claimCase =>
                identifiedSender.PolicyNumbers.Contains(claimCase.PolicyNumber, StringComparer.OrdinalIgnoreCase)
                || PhoneNumbers.SameNumber(claimCase.CustomerPhone, message.SenderWaId))
            .OrderByDescending(claimCase => claimCase.CreatedAt)
            .FirstOrDefault();
        return openClaim is not null
            ? new WhatsAppToCaseMatchResult(openClaim.CaseId, "identified_sender_open_claim", RequiresManualReview: false, identifiedSender)
            : new WhatsAppToCaseMatchResult(null, "identified_sender_no_case", RequiresManualReview: false, identifiedSender);
    }

    private async Task<IdentifiedSender?> IdentifySenderAsync(
        string senderWaId,
        TenantScope scope,
        CancellationToken cancellationToken)
    {
        var customers = (await customerRepository.ListAsync(scope, cancellationToken))
            .Where(c => PhoneNumbers.SameNumber(senderWaId, c.Phone))
            .ToArray();
        if (customers.Length == 0)
        {
            return null;
        }

        // One number on file for two different people: never guess which of them is writing.
        var people = customers
            .Select(c => string.Join(' ', c.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (people > 1)
        {
            return null;
        }

        var policies = new List<Policy>();
        foreach (var customer in customers)
        {
            policies.AddRange(await policyRepository.ListByCustomerAsync(customer.CustomerId, scope, cancellationToken));
        }

        var policy = policies.FirstOrDefault();
        var first = customers[0];
        return policy is null
            ? null
            : new IdentifiedSender(first.CustomerId, first.FullName, first.Email, first.Phone, policy.PolicyNumber, policy.ProductLine)
            {
                PolicyNumbers = policies.Select(p => p.PolicyNumber).ToArray(),
            };
    }
}

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// A channel message's sender (WhatsApp WA ID or email From address) matched against a real
/// customer/policy on file. Distinct from a case match: this says "we know who sent this," not
/// "this belongs to an existing claim." Channel matchers return this so an inbound-ingestion
/// pipeline can auto-create a case for a genuinely identified sender instead of either inventing a
/// case for an unverified stranger or dropping a real customer's message into manual review.
/// A case created from this is always <c>RequiresConfirmation = true</c> — identity is verified,
/// but the claim details are still a best-effort guess from raw channel content until a human
/// reviews it.
/// </summary>
public sealed record IdentifiedSender(
    string CustomerId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string PolicyNumber,
    string ProductLine);

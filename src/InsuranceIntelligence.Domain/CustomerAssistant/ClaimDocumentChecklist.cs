namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// What the WhatsApp assistant asks a customer to send right after it opens a claim, in words a
/// customer understands, and how that list reads in a message.
///
/// The insurer's own configured list always wins: when the claim is pinned to a product whose
/// document matrix is set up (Admin, Products), the assistant uses that matrix's customer labels.
/// These defaults only cover a claim with no configured product, so the customer is never left
/// without guidance. Asking is all this does; a claims officer still decides what the claim needs.
/// </summary>
public static class ClaimDocumentChecklist
{
    public static IReadOnlyList<string> DefaultFor(string? productLine) => productLine?.Trim().ToLowerInvariant() switch
    {
        "motor" => ["photos of the damage", "the police abstract", "your driving permit"],
        "medical" => ["the medical report", "the hospital invoice or receipts"],
        "life" => ["the death certificate or maturity documents", "a copy of the claimant's national ID"],
        "general" => ["photos of the damage or loss", "a police report if anything was stolen"],
        "agriculture" => ["photos of the affected crops or animals"],
        _ => ["photos or documents that show what happened"],
    };

    /// <summary>"a", "a and b", "a, b and c".</summary>
    public static string AsSentenceList(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };
}

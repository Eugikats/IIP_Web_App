using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.BankEvents;

/// <summary>
/// Turns what is happening to claims into events, so chasing documents and keeping the claimant informed go through the same rules
/// and checks as everything else. It says only what the claim record says: which papers are still missing (from the same checklist
/// the claims team sees), that the claim arrived, and where it has got to in words that decide nothing. It never says a claim is
/// approved, rejected or will be paid.
/// </summary>
public sealed class CaseSweepService(
    ICaseRepository cases,
    ICaseDocumentRepository documents,
    IDocumentChecklistProvider checklists,
    EventService events,
    TimeProvider clock)
{
    private static readonly TimeSpan ChaseEvery = TimeSpan.FromDays(3);

    public sealed record Result(int Noticed, int New);

    /// <summary>What a claimant is told about a stage. Plain descriptions of where it is, never an outcome.</summary>
    public static string? StatusWords(CaseStatus status) => status switch
    {
        CaseStatus.Assigned => "it has been given to a claims officer",
        CaseStatus.InReview => "a claims officer is reviewing it",
        CaseStatus.ReadyForReview => "all the papers are in and it is ready for a claims officer",
        CaseStatus.Settled => "it has been concluded. A claims officer will contact you about it",
        CaseStatus.Closed => "it has been closed",
        _ => null,
    };

    public async Task<Result> SweepAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var noticed = 0;
        var created = 0;

        async Task Emit(ClaimCase claim, string type, string reference, Dictionary<string, string> extra)
        {
            noticed++;
            extra["customer_name"] = FirstName(claim.CustomerName);
            extra["claim_reference"] = ClaimReference.For(claim.CaseId);
            try
            {
                var made = await events.RecordAsync(
                    scope, new EventInput(type, claim.CustomerPhone, now.AddMinutes(-1), $"{claim.CaseId}:{type}:{reference}", Extra: extra),
                    EventSource.PolicyDates, $"claim {ClaimReference.For(claim.CaseId)}", "system:claim-sweep", cancellationToken);
                if (made.Created) created++;
            }
            catch (EventException)
            {
                // A claim with no usable phone cannot be messaged.
            }
        }

        foreach (var claim in await cases.ListAsync(scope, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(claim.CustomerPhone) || claim.RequiresConfirmation)
            {
                continue;
            }

            var age = now - claim.CreatedAt;
            if (age >= TimeSpan.Zero && age <= TimeSpan.FromDays(2))
            {
                await Emit(claim, "claim_received", "received", []);
            }

            if (claim.Status == CaseStatus.MissingDocuments)
            {
                var papers = await documents.ListForCaseAsync(claim.CaseId, scope, cancellationToken);
                var checklist = await checklists.BuildChecklistAsync(claim, papers, cancellationToken);
                var missing = checklist.MissingRequiredDocuments.Where(item => item.Required).Select(item => item.Label).ToArray();
                if (missing.Length > 0)
                {
                    // One chase every three days for as long as the papers are missing: each is its own event.
                    var round = (int)Math.Max(0, (now - claim.CreatedAt).TotalDays / ChaseEvery.TotalDays);
                    await Emit(claim, "claim_documents_needed", $"round-{round}", new Dictionary<string, string> { ["documents_needed"] = string.Join(", ", missing) });
                }
            }
            else if (StatusWords(claim.Status) is { } words)
            {
                await Emit(claim, "claim_status_update", claim.Status.ToString(), new Dictionary<string, string> { ["status"] = words });
            }
        }

        return new Result(noticed, created);
    }

    private static string FirstName(string? name) =>
        (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() is { Length: > 0 } first ? first : "there";
}

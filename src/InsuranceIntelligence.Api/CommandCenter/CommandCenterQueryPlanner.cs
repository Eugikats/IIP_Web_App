using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Workflow;
using Vocab = InsuranceIntelligence.Api.CommandCenter.CommandCenterBlockVocabulary;

namespace InsuranceIntelligence.Api.CommandCenter;

/// <summary>
/// Presentation-boundary view of one claim, already resolved to the contract's SLA vocabulary and
/// missing-document list. Built by the endpoint from tenant-scoped repositories so the planner can
/// stay a pure, deterministic, easily-tested function with no I/O.
/// </summary>
public sealed record CommandCenterCaseView(
    Guid CaseId,
    string Customer,
    string ProductLine,
    string Status,
    SlaStatus RawSlaStatus,
    string SlaToken,
    string DueLabel,
    decimal Completeness,
    string AssignedTo,
    IReadOnlyList<string> MissingDocuments);

/// <summary>Everything the planner needs, already tenant-scoped and materialised by the endpoint.</summary>
public sealed record CommandCenterQueryData(
    CommandCenterMetricsResponse Metrics,
    IReadOnlyList<CommandCenterCaseView> Cases,
    IReadOnlyList<CommandCenterApprovalView> PendingApprovals,
    IReadOnlyList<ProviderHealthItem> MissingProviders);

public sealed record CommandCenterApprovalView(Guid ApprovalId, Guid CaseId, string Title, string Status);

/// <summary>
/// Deterministic (NOT LLM) Command Center planner. Maps a natural-language question to a set of
/// typed answer blocks assembled purely from real, tenant-scoped data. Draft-only: it never
/// proposes an executable decision — every proposed action requires human approval.
/// </summary>
public static class CommandCenterQueryPlanner
{
    /// <summary>Maps the SLA enum plus due date to the contract's four-state SLA vocabulary.</summary>
    public static (string Token, string DueLabel) ResolveSla(ClaimCase claimCase, SlaStatus status, DateTimeOffset now)
    {
        switch (status)
        {
            case SlaStatus.Breached:
                return (Vocab.SlaBreached, DueLabel(claimCase.SlaDueAt, now, "Overdue"));
            case SlaStatus.AtRisk:
                return (Vocab.SlaAtRisk, DueLabel(claimCase.SlaDueAt, now, "Due soon"));
            default:
                if (claimCase.SlaDueAt is { } due && due.UtcDateTime.Date == now.UtcDateTime.Date)
                {
                    return (Vocab.SlaDueToday, "Due today");
                }

                return (Vocab.SlaOnTrack, DueLabel(claimCase.SlaDueAt, now, "On track"));
        }
    }

    public static IReadOnlyList<CommandCenterResponseBlock> Plan(string question, CommandCenterQueryData data)
    {
        var normalized = (question ?? string.Empty).Trim().ToLowerInvariant();
        var blocks = new List<CommandCenterResponseBlock>();

        if (Mentions(normalized, "approval", "approve", "sign off", "sign-off"))
        {
            blocks.Add(Narrate($"{data.PendingApprovals.Count} approval(s) are waiting on a human decision."));
            blocks.Add(BuildApprovalQueue(data));
            AddActionProposals(blocks, data, includeApprovals: true, includeDocs: false, includeSla: false);
        }
        else if (Mentions(normalized, "document", "missing", "incomplete", "checklist", "evidence pack"))
        {
            var gaps = DocumentGaps(data);
            blocks.Add(Narrate($"{gaps.Count} claim(s) are missing required documents."));
            blocks.Add(new DocumentGapSummaryBlock(gaps));
            AddActionProposals(blocks, data, includeApprovals: false, includeDocs: true, includeSla: false);
        }
        else if (Mentions(normalized, "sla", "breach", "overdue", "at risk", "at-risk", "due", "deadline"))
        {
            var sla = SlaItems(data);
            blocks.Add(Narrate($"{data.Metrics.SlaBreached} breached and {data.Metrics.SlaAtRisk} at-risk claim(s) need attention."));
            blocks.Add(new SlaSummaryBlock(sla));
            AddActionProposals(blocks, data, includeApprovals: false, includeDocs: false, includeSla: true);
        }
        else if (Mentions(normalized, "claim", "case", "workload", "queue"))
        {
            blocks.Add(Narrate($"{data.Metrics.OpenClaims} open claim(s), sorted by attention required."));
            blocks.Add(new ClaimsTableBlock(ClaimRows(data)));
        }
        else
        {
            // Default "what needs my attention today" briefing.
            blocks.Add(Narrate(BriefingSummary(data)));
            blocks.Add(BuildMetricPills(data));
            blocks.Add(new SlaSummaryBlock(SlaItems(data)));
            blocks.Add(new DocumentGapSummaryBlock(DocumentGaps(data)));
            blocks.Add(BuildApprovalQueue(data));
            AddActionProposals(blocks, data, includeApprovals: true, includeDocs: true, includeSla: true);
        }

        blocks.Add(BuildEvidence(data));
        blocks.AddRange(NotConfiguredBlocks(data));
        return blocks;
    }

    private static TextBlock Narrate(string markdown) => new(markdown);

    private static string BriefingSummary(CommandCenterQueryData data)
    {
        var m = data.Metrics;
        return $"You have {m.OpenClaims} open claim(s): {m.SlaBreached} breached and {m.SlaAtRisk} at-risk on SLA, "
             + $"{m.IncompleteClaims} incomplete, and {m.PendingApprovals} approval(s) pending your review. "
             + "AI prepared this summary from live data; every action below stays a draft for you to decide.";
    }

    private static MetricPillsBlock BuildMetricPills(CommandCenterQueryData data)
    {
        var m = data.Metrics;
        var pills = new List<MetricPill>
        {
            new("Open claims", m.OpenClaims.ToString(), Vocab.ToneInfo),
            new("SLA breached", m.SlaBreached.ToString(), m.SlaBreached > 0 ? Vocab.ToneDanger : Vocab.ToneNeutral),
            new("SLA at risk", m.SlaAtRisk.ToString(), m.SlaAtRisk > 0 ? Vocab.ToneWarning : Vocab.ToneNeutral),
            new("Incomplete", m.IncompleteClaims.ToString(), m.IncompleteClaims > 0 ? Vocab.ToneWarning : Vocab.ToneNeutral),
            new("Pending approvals", m.PendingApprovals.ToString(), m.PendingApprovals > 0 ? Vocab.ToneInfo : Vocab.ToneNeutral),
        };
        return new MetricPillsBlock(pills);
    }

    private static IReadOnlyList<ClaimRow> ClaimRows(CommandCenterQueryData data) =>
        data.Cases
            .OrderBy(AttentionRank)
            .ThenBy(view => view.Completeness)
            .Select(view => new ClaimRow(
                view.CaseId.ToString(),
                view.Customer,
                view.ProductLine,
                view.Status,
                view.SlaToken,
                view.Completeness,
                view.AssignedTo,
                CaseHref(view.CaseId)))
            .ToList();

    private static IReadOnlyList<SlaItem> SlaItems(CommandCenterQueryData data) =>
        data.Cases
            .Where(view => view.SlaToken != Vocab.SlaOnTrack)
            .OrderBy(AttentionRank)
            .Select(view => new SlaItem(view.CaseId.ToString(), view.Customer, view.SlaToken, view.DueLabel))
            .ToList();

    private static IReadOnlyList<DocGapItem> DocumentGaps(CommandCenterQueryData data) =>
        data.Cases
            .Where(view => view.MissingDocuments.Count > 0)
            .OrderBy(view => view.Completeness)
            .Select(view => new DocGapItem(view.CaseId.ToString(), view.Customer, view.MissingDocuments))
            .ToList();

    private static ApprovalQueueBlock BuildApprovalQueue(CommandCenterQueryData data) =>
        new(data.PendingApprovals
            .Select(approval => new ApprovalItem(
                approval.ApprovalId.ToString(),
                approval.CaseId.ToString(),
                approval.Title,
                approval.Status,
                "/approvals"))
            .ToList());

    private static void AddActionProposals(
        List<CommandCenterResponseBlock> blocks,
        CommandCenterQueryData data,
        bool includeApprovals,
        bool includeDocs,
        bool includeSla)
    {
        var actions = new List<ProposedAction>();

        if (includeApprovals && data.PendingApprovals.Count > 0)
        {
            actions.Add(new ProposedAction(
                "Review pending approvals",
                $"{data.PendingApprovals.Count} approval(s) need a human decision before anything proceeds.",
                "/approvals"));
        }

        if (includeDocs)
        {
            var firstGap = DocumentGaps(data).FirstOrDefault();
            if (firstGap is not null)
            {
                actions.Add(new ProposedAction(
                    "Request missing documents",
                    $"Draft a customer update for {firstGap.Customer} to collect the outstanding documents.",
                    CaseHref(Guid.Parse(firstGap.CaseId))));
            }
        }

        if (includeSla)
        {
            var firstSla = SlaItems(data).FirstOrDefault();
            if (firstSla is not null)
            {
                actions.Add(new ProposedAction(
                    "Prioritise SLA-at-risk claim",
                    $"Open {firstSla.Customer}'s claim to progress it before the SLA breaches.",
                    CaseHref(Guid.Parse(firstSla.CaseId))));
            }
        }

        if (actions.Count > 0)
        {
            blocks.Add(new ActionProposalBlock(actions));
        }
    }

    private static EvidenceListBlock BuildEvidence(CommandCenterQueryData data) =>
        new(data.Cases
            .OrderBy(AttentionRank)
            .ThenBy(view => view.Completeness)
            .Take(5)
            .Select(view => new EvidenceSource(
                view.Customer,
                $"{view.ProductLine} claim · {ShortId(view.CaseId)}",
                CaseHref(view.CaseId)))
            .ToList());

    private static IEnumerable<NotConfiguredBlock> NotConfiguredBlocks(CommandCenterQueryData data) =>
        data.MissingProviders.Select(provider => new NotConfiguredBlock(
            provider.Name,
            string.IsNullOrWhiteSpace(provider.Detail)
                ? $"Configure {provider.Name} in Admin → Providers."
                : provider.Detail));

    private static int AttentionRank(CommandCenterCaseView view) => view.SlaToken switch
    {
        Vocab.SlaBreached => 0,
        Vocab.SlaAtRisk => 1,
        Vocab.SlaDueToday => 2,
        _ => 3,
    };

    private static bool Mentions(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static string CaseHref(Guid caseId) => $"/cases/{caseId}";

    private static string ShortId(Guid caseId) => caseId.ToString()[..8];

    private static string DueLabel(DateTimeOffset? dueAt, DateTimeOffset now, string fallback)
    {
        if (dueAt is not { } due)
        {
            return fallback;
        }

        var days = (int)Math.Round((due.UtcDateTime.Date - now.UtcDateTime.Date).TotalDays);
        return days switch
        {
            < 0 => $"Overdue by {-days}d",
            0 => "Due today",
            1 => "Due tomorrow",
            _ => $"Due in {days}d",
        };
    }
}

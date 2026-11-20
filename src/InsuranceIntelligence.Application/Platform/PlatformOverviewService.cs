using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Platform;

namespace InsuranceIntelligence.Application.Platform;

/// <summary>A Command Center card: an inbox, intent or automation notification.</summary>
public sealed record PlatformCardDto(
    string Id,
    string Kind,
    string Kicker,
    string Title,
    string Summary,
    string IconKey,
    string Tone,
    int? Count,
    string? AskPrompt,
    string? Href,
    string Source);

public sealed record PlatformNotificationsDto(PlatformInboxCountsDto Counts, IReadOnlyList<PlatformCardDto> Cards, IReadOnlyList<PlatformIntentDto> Intents);

public sealed record PlatformInsightsDto(IReadOnlyList<PlatformCardDto> Cards, string Source, DateTimeOffset GeneratedAt);

public sealed record PlatformReportDto(
    string Id,
    string Title,
    string Type,
    string Status,
    string WorkspaceName,
    string Origin,
    string? CreatedBy,
    DateTimeOffset? GeneratedAt,
    DateTimeOffset UpdatedAt,
    string? Href);

public sealed record PlatformConversationDto(Guid Id, string Title, string WorkspaceId, string WorkspaceName, DateTimeOffset LastMessageAt, string Href);

public interface IPlatformOverviewService
{
    Task<PlatformNotificationsDto> NotificationsAsync(Guid? workspaceId, CancellationToken cancellationToken);
    Task<PlatformInsightsDto> InsightsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformReportDto>> ReportsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformConversationDto>> ConversationsAsync(CancellationToken cancellationToken);
}

public sealed class PlatformOverviewService(
    IOrgWorkspaceStore workspaceStore,
    IPlatformAutomationStore automationStore,
    IPlatformRunStore runStore,
    IPlatformWorkspaceService workspaces,
    IPlatformInboxService inbox,
    ICommandCenterConversationService conversationService,
    PlatformAccess access) : IPlatformOverviewService
{
    /// <summary>The bell count, the inbox cards and the intents strip. With a workspace, only that workspace's signals.</summary>
    public async Task<PlatformNotificationsDto> NotificationsAsync(Guid? workspaceId, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        await workspaces.EnsurePrimaryAsync(cancellationToken);
        var counts = await inbox.CountsAsync(workspaceId, cancellationToken);
        var items = await inbox.ListAsync(null, workspaceId, cancellationToken);
        var intents = (await inbox.IntentsAsync(cancellationToken))
            .Where(i => i.Status == "active" && (workspaceId is null || i.WorkspaceId is null || i.WorkspaceId == workspaceId))
            .OrderBy(i => i.Health switch { "off_track" => 0, "at_risk" => 1, _ => 2 })
            .ToList();
        var cards = new List<PlatformCardDto>();

        if (counts.AwaitingApproval > 0)
        {
            var first = items.FirstOrDefault(i => i.Status == "awaiting_approval");
            cards.Add(new PlatformCardDto("inbox-approvals", "inbox", "Awaiting approval",
                $"{counts.AwaitingApproval} approval{Plural(counts.AwaitingApproval)} waiting for a decision",
                first is null ? "Prepared for you; nothing happens until a person decides." : $"Next: {first.Title}",
                "approval", "amber", counts.AwaitingApproval, null, $"{PlatformRoutes.Automations}?tab=inbox&status=awaiting_approval", "inbox"));
        }

        if (counts.NeedsAttention > 0)
        {
            var top = items.FirstOrDefault(i => i.Status == PlatformInboxStatuses.NeedsAttention);
            cards.Add(new PlatformCardDto("inbox-attention", "inbox", "Needs attention",
                $"{counts.NeedsAttention} signal{Plural(counts.NeedsAttention)} need attention",
                top?.Title ?? "Raised by your automations.", "risk", "red", counts.NeedsAttention,
                top is null ? null : $"Explain this signal and what I should do next: {top.Title}. {top.Body}",
                $"{PlatformRoutes.Automations}?tab=inbox&status=needs_attention", "inbox"));
        }

        var atRisk = intents.Where(i => i.Health != "on_track").ToList();
        if (atRisk.Count > 0)
        {
            var worst = atRisk[0];
            cards.Add(new PlatformCardDto("intents-at-risk", "intent", "Intents",
                $"{atRisk.Count} intent{Plural(atRisk.Count)} {(atRisk.Count == 1 ? "is" : "are")} off course",
                $"{worst.Name}: {worst.LatestSignal ?? $"{worst.OpenItems} open signals"}", "target", worst.Health == "off_track" ? "red" : "amber", atRisk.Count,
                $"How are we doing on the intent \"{worst.Name}\" and what is putting it at risk?", $"{PlatformRoutes.Automations}?tab=intents", "intent"));
        }
        else if (intents.Count > 0)
        {
            cards.Add(new PlatformCardDto("intents-on-track", "intent", "Intents",
                $"{intents.Count} intent{Plural(intents.Count)} on track", string.Join(", ", intents.Take(3).Select(i => i.Name)),
                "target", "green", intents.Count, null, $"{PlatformRoutes.Automations}?tab=intents", "intent"));
        }

        if (counts.Monitoring > 0)
        {
            cards.Add(new PlatformCardDto("automations-active", "automation", "Automations",
                $"{counts.Monitoring} automation{Plural(counts.Monitoring)} watching",
                counts.Recommendations > 0 ? $"{counts.Recommendations} recommendation{Plural(counts.Recommendations)} to review." : "Running on schedule; results arrive in the inbox.",
                "automation", "blue", counts.Monitoring, null, PlatformRoutes.Automations, "automation"));
        }

        return new PlatformNotificationsDto(counts, cards, intents);
    }

    /// <summary>The organisation Command Center cards. IIP has no separate workspace insight feed yet, so these are the platform's own signals.</summary>
    public async Task<PlatformInsightsDto> InsightsAsync(CancellationToken cancellationToken)
    {
        var notifications = await NotificationsAsync(null, cancellationToken);
        return new PlatformInsightsDto(notifications.Cards, "platform", access.Now);
    }

    /// <summary>Automation runs that found something, newest first. A report here is the output of an automation; nothing is invented.</summary>
    public async Task<IReadOnlyList<PlatformReportDto>> ReportsAsync(CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        await workspaces.EnsurePrimaryAsync(cancellationToken);
        var scope = access.Scope;
        var visible = (await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).ToHashSet();
        var names = (await workspaceStore.ListAsync(scope, 500, cancellationToken)).ToDictionary(w => w.Id, w => w.Name);
        var automations = (await automationStore.ListAsync(scope, 1000, cancellationToken)).Where(a => visible.Contains(a.WorkspaceId)).ToDictionary(a => a.Id);
        var runs = (await runStore.ListAsync(scope, 1000, cancellationToken))
            .Where(r => r.Kind == "run" && r.Findings > 0 && automations.ContainsKey(r.AutomationId))
            .OrderByDescending(r => r.StartedAt).Take(100).ToList();
        return runs.Select(r =>
        {
            var a = automations[r.AutomationId];
            return new PlatformReportDto($"run-{r.Id:N}", $"{a.Name}, {r.StartedAt:dd MMM yyyy HH:mm}", "Automation output", r.Status,
                names.GetValueOrDefault(a.WorkspaceId, ""), $"Automation v{r.AutomationVersion}", null, r.FinishedAt, r.StartedAt,
                PlatformRoutes.Automation(r.AutomationId));
        }).ToList();
    }

    /// <summary>The signed-in person's own Command Center conversations (the conversation service only returns their own).</summary>
    public async Task<IReadOnlyList<PlatformConversationDto>> ConversationsAsync(CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var primary = await workspaces.EnsurePrimaryAsync(cancellationToken);
        var list = await conversationService.ListAsync(false, 100, cancellationToken);
        return list.Items.Select(c => new PlatformConversationDto(
            c.ConversationId, string.IsNullOrWhiteSpace(c.Title) ? "Untitled conversation" : c.Title, primary.Id.ToString("D"), primary.Name,
            c.LastActivityAt, $"/app/conversations/{c.ConversationId}")).ToList();
    }

    private static string Plural(int n) => n == 1 ? "" : "s";
}

using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Platform;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Platform;

/// <summary>Where the platform's screens live in the portal. One place, so a route change is one edit.</summary>
public static class PlatformRoutes
{
    public const string Base = "/app/platform";
    public const string Automations = Base + "/automations";
    public const string Workspaces = Base + "/workspaces";
    public const string Approvals = "/app/work-items";

    public static string Automation(Guid id) => $"{Automations}/{id}";
}

public sealed record InboxRaise(
    Guid? WorkspaceId,
    Guid? AutomationId,
    Guid? IntentId,
    string Kind,
    string Status,
    string Severity,
    string Title,
    string? Body,
    object? Evidence,
    string Source,
    string? SourceRef,
    string DedupeKey);

public sealed record PlatformInboxItemDto(
    string Id,
    string Kind,
    string Status,
    string Severity,
    string Title,
    string? Body,
    Guid? WorkspaceId,
    string WorkspaceName,
    Guid? AutomationId,
    string? AutomationName,
    Guid? IntentId,
    string Source,
    int Occurrences,
    JsonElement Evidence,
    IReadOnlyList<string> Actions,
    string? Href,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNote);

public sealed record PlatformInboxCountsDto(int NeedsAttention, int Recommendations, int AwaitingApproval, int Monitoring, int Resolved, int Total);

public sealed record PlatformIntentDto(
    Guid Id,
    string Name,
    string? Description,
    string? Measure,
    string? Target,
    string Status,
    string Health,
    int OpenItems,
    int HighItems,
    string? LatestSignal,
    DateTimeOffset? LatestSignalAt,
    IReadOnlyList<Guid> AutomationIds,
    Guid? WorkspaceId,
    DateTimeOffset CreatedAt);

public sealed record PlatformIntentRequest(string? Name, string? Description, string? Measure, string? Target, Guid? WorkspaceId, IReadOnlyList<Guid>? AutomationIds, string? Status);

public interface IPlatformInboxService
{
    Task<bool> RaiseAsync(InboxRaise raise, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformInboxItemDto>> ListAsync(string? status, Guid? workspaceId, CancellationToken cancellationToken);
    Task<PlatformInboxCountsDto> CountsAsync(Guid? workspaceId, CancellationToken cancellationToken);
    Task<PlatformResult<object>> ActAsync(string id, string action, string? note, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformIntentDto>> IntentsAsync(CancellationToken cancellationToken);
    Task<PlatformResult<PlatformIntentDto>> SaveIntentAsync(Guid? id, PlatformIntentRequest request, CancellationToken cancellationToken);
    Task<PlatformResult<object>> IntentActionAsync(Guid id, string action, CancellationToken cancellationToken);
}

/// <summary>
/// The Intelligence Inbox. Signals come from automations. Approvals are the approvals already waiting in IIP; they are
/// listed here with a link, never decided here: a decision needs the explicit verb, the consequence preview and the
/// audit reason of the approvals screen.
/// </summary>
public sealed class PlatformInboxService(
    IPlatformInboxStore inboxStore,
    IPlatformIntentStore intentStore,
    IOrgWorkspaceStore workspaceStore,
    IPlatformAutomationStore automationStore,
    IApprovalRepository approvalRepository,
    IPermissionService permissionService,
    PlatformAccess access,
    PlatformAudit audit) : IPlatformInboxService
{
    internal static readonly string[] Open = [PlatformInboxStatuses.NeedsAttention, PlatformInboxStatuses.Recommendation, PlatformInboxStatuses.Acknowledged];

    /// <summary>One item per condition: seeing it again updates the open item. Returns true only for a new item.</summary>
    public async Task<bool> RaiseAsync(InboxRaise raise, CancellationToken cancellationToken)
    {
        var scope = access.Scope;
        var key = PlatformAccess.Clip(raise.DedupeKey, 256);
        var existing = await inboxStore.FindByDedupeKeyAsync(scope, key, cancellationToken);
        if (existing is not null && (Open.Contains(existing.Status) || existing.Status == PlatformInboxStatuses.Dismissed))
        {
            if (existing.Status != PlatformInboxStatuses.Dismissed)
            {
                existing.Occurrences += 1;
                existing.Title = PlatformAccess.Clip(raise.Title, 256);
                existing.Body = PlatformAccess.Clip(raise.Body, 2000);
                existing.Severity = raise.Severity;
                existing.EvidenceJson = PlatformAccess.Write(raise.Evidence ?? new { });
                existing.UpdatedAt = access.Now;
                await inboxStore.SaveAsync(scope, existing, cancellationToken);
            }

            return false;
        }

        await inboxStore.SaveAsync(scope, new PlatformInboxItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = raise.WorkspaceId,
            AutomationId = raise.AutomationId,
            IntentId = raise.IntentId,
            Kind = raise.Kind,
            Status = raise.Status,
            Severity = raise.Severity,
            Title = PlatformAccess.Clip(raise.Title, 256),
            Body = PlatformAccess.Clip(raise.Body, 2000),
            EvidenceJson = PlatformAccess.Write(raise.Evidence ?? new { }),
            Source = PlatformAccess.Clip(raise.Source, 64),
            SourceRef = raise.SourceRef is null ? null : PlatformAccess.Clip(raise.SourceRef, 128),
            DedupeKey = key,
            CreatedAt = access.Now,
            UpdatedAt = access.Now
        }, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PlatformInboxItemDto>> ListAsync(string? status, Guid? workspaceId, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var scope = access.Scope;
        var workspaces = await workspaceStore.ListAsync(scope, 500, cancellationToken);
        var names = workspaces.ToDictionary(w => w.Id, w => w.Name);
        var primary = workspaces.FirstOrDefault(w => w.IsPrimary)?.Id;
        var result = new List<PlatformInboxItemDto>();

        if (status is null or "awaiting_approval")
        {
            if (workspaceId is null || workspaceId == primary) result.AddRange(await ApprovalsAsync(primary, names, cancellationToken));
        }

        var automations = await automationStore.ListAsync(scope, 1000, cancellationToken);
        if (status is null or "monitoring")
        {
            result.AddRange(automations
                .Where(a => a.Status == PlatformAutomationStatuses.Active && (workspaceId == null || a.WorkspaceId == workspaceId))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Take(100)
                .Select(a => new PlatformInboxItemDto(
                    $"mon-{a.Id:N}", "monitoring", "monitoring", "low", a.Name,
                    a.LastRunAt is null ? "Active, waiting for its first run." : $"Last run {a.LastRunAt:dd MMM HH:mm}: {a.LastRunMessage}",
                    a.WorkspaceId, names.GetValueOrDefault(a.WorkspaceId, ""), a.Id, a.Name, null, "automation", 1,
                    PlatformAccess.Element("{}"), [], PlatformRoutes.Automation(a.Id), a.CreatedAt, a.UpdatedAt, null, null)));
        }

        if (status is not ("awaiting_approval" or "monitoring"))
        {
            var items = (await inboxStore.ListAsync(scope, 1000, cancellationToken)).AsEnumerable();
            items = status switch
            {
                null => items.Where(i => Open.Contains(i.Status)),
                "resolved" => items.Where(i => i.Status == PlatformInboxStatuses.Resolved || i.Status == PlatformInboxStatuses.Dismissed),
                _ => items.Where(i => i.Status == status)
            };
            if (workspaceId is { } ws) items = items.Where(i => i.WorkspaceId == ws);
            var automationNames = automations.ToDictionary(a => a.Id, a => a.Name);
            result.AddRange(items.OrderByDescending(i => i.UpdatedAt).Take(200).Select(i => new PlatformInboxItemDto(
                i.Id.ToString("D"), i.Kind, i.Status, i.Severity, i.Title, i.Body, i.WorkspaceId,
                i.WorkspaceId is { } w ? names.GetValueOrDefault(w, "") : "Organisation",
                i.AutomationId, i.AutomationId is { } a ? automationNames.GetValueOrDefault(a) : null, i.IntentId, i.Source, i.Occurrences,
                PlatformAccess.Element(i.EvidenceJson),
                Open.Contains(i.Status) ? (i.Status == PlatformInboxStatuses.Acknowledged ? ["resolve", "dismiss"] : ["acknowledge", "resolve", "dismiss"]) : ["reopen"],
                i.AutomationId is { } aid ? PlatformRoutes.Automation(aid) : null,
                i.CreatedAt, i.UpdatedAt, i.ResolvedAt, i.ResolutionNote)));
        }

        return result
            .OrderBy(i => i.Status switch { "awaiting_approval" => 0, PlatformInboxStatuses.NeedsAttention => 1, PlatformInboxStatuses.Recommendation => 2, PlatformInboxStatuses.Acknowledged => 3, "monitoring" => 4, _ => 5 })
            .ThenBy(i => i.Severity switch { "critical" => 0, "high" => 1, "medium" => 2, _ => 3 })
            .ThenByDescending(i => i.UpdatedAt)
            .ToList();
    }

    public async Task<PlatformInboxCountsDto> CountsAsync(Guid? workspaceId, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var scope = access.Scope;
        var items = (await inboxStore.ListAsync(scope, 1000, cancellationToken)).Where(i => workspaceId == null || i.WorkspaceId == workspaceId).ToList();
        int Count(params string[] statuses) => items.Count(i => statuses.Contains(i.Status));
        var primary = (await workspaceStore.ListAsync(scope, 500, cancellationToken)).FirstOrDefault(w => w.IsPrimary)?.Id;
        var approvals = workspaceId is null || workspaceId == primary ? (await ApprovalsAsync(primary, new Dictionary<Guid, string>(), cancellationToken)).Count : 0;
        var monitoring = (await automationStore.ListAsync(scope, 1000, cancellationToken))
            .Count(a => a.Status == PlatformAutomationStatuses.Active && (workspaceId == null || a.WorkspaceId == workspaceId));
        var needs = Count(PlatformInboxStatuses.NeedsAttention);
        var recs = Count(PlatformInboxStatuses.Recommendation, PlatformInboxStatuses.Acknowledged);
        return new PlatformInboxCountsDto(needs, recs, approvals, monitoring, Count(PlatformInboxStatuses.Resolved, PlatformInboxStatuses.Dismissed), needs + recs + approvals);
    }

    public async Task<PlatformResult<object>> ActAsync(string id, string action, string? note, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        action = (action ?? "").Trim().ToLowerInvariant();
        if (id.StartsWith("apr-", StringComparison.Ordinal))
            return PlatformResult<object>.Fail("Approvals are decided on the approvals screen, where the evidence and the consequence are shown.", 409);

        if (!Guid.TryParse(id, out var itemId)) return PlatformResult<object>.Fail("Item not found.", 404);
        var scope = access.Scope;
        var item = await inboxStore.GetAsync(scope, itemId, cancellationToken);
        if (item is null) return PlatformResult<object>.Fail("Item not found.", 404);

        var to = action switch
        {
            "acknowledge" => PlatformInboxStatuses.Acknowledged,
            "resolve" => PlatformInboxStatuses.Resolved,
            "dismiss" => PlatformInboxStatuses.Dismissed,
            "reopen" => PlatformInboxStatuses.NeedsAttention,
            _ => null
        };
        if (to is null) return PlatformResult<object>.Fail("Unknown action.");
        var before = item.Status;
        item.Status = to;
        if (to is PlatformInboxStatuses.Resolved or PlatformInboxStatuses.Dismissed)
        {
            item.ResolvedAt = access.Now;
            item.ResolvedByUserId = access.UserId;
            item.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : PlatformAccess.Clip(note.Trim(), 1000);
        }
        else if (action == "reopen")
        {
            item.ResolvedAt = null;
            item.ResolvedByUserId = null;
        }

        item.UpdatedAt = access.Now;
        await inboxStore.SaveAsync(scope, item, cancellationToken);
        await audit.LogAsync($"platform.inbox.{action}", item.Id, before, item.Status, cancellationToken, item.ResolutionNote);
        return PlatformResult<object>.Success(new { item.Id, item.Status });
    }

    // ---------------------------------------------------------------- intents

    public async Task<IReadOnlyList<PlatformIntentDto>> IntentsAsync(CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var scope = access.Scope;
        var intents = (await intentStore.ListAsync(scope, 500, cancellationToken)).Where(i => i.Status != "archived").OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var open = (await inboxStore.ListAsync(scope, 1000, cancellationToken)).Where(i => Open.Contains(i.Status)).ToList();
        return intents.Select(intent =>
        {
            var linked = open.Where(o => o.IntentId == intent.Id || o.AutomationId is { } a && intent.AutomationIds.Contains(a)).ToList();
            var high = linked.Count(l => l.Severity is "high" or "critical");
            var latest = linked.OrderByDescending(l => l.UpdatedAt).FirstOrDefault();
            return new PlatformIntentDto(intent.Id, intent.Name, intent.Description, intent.Measure, intent.Target, intent.Status,
                Health(linked.Count, high), linked.Count, high, latest?.Title, latest?.UpdatedAt, intent.AutomationIds, intent.WorkspaceId, intent.CreatedAt);
        }).ToList();
    }

    /// <summary>On track with nothing open; off track with a high-severity signal or three or more open items.</summary>
    public static string Health(int open, int high) => open == 0 ? "on_track" : high > 0 || open >= 3 ? "off_track" : "at_risk";

    public async Task<PlatformResult<PlatformIntentDto>> SaveIntentAsync(Guid? id, PlatformIntentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var scope = access.Scope;
        PlatformIntent? intent;
        if (id is { } existingId)
        {
            intent = await intentStore.GetAsync(scope, existingId, cancellationToken);
            if (intent is null) return PlatformResult<PlatformIntentDto>.Fail("Intent not found.", 404);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return PlatformResult<PlatformIntentDto>.Fail("Give the intent a name.");
            intent = new PlatformIntent { Id = Guid.NewGuid(), OwnerUserId = access.UserId, CreatedAt = access.Now };
        }

        if (!string.IsNullOrWhiteSpace(request.Name)) intent.Name = PlatformAccess.Clip(request.Name.Trim(), 160);
        if (request.Description is not null) intent.Description = PlatformAccess.Clip(request.Description.Trim(), 1000);
        if (request.Measure is not null) intent.Measure = PlatformAccess.Clip(request.Measure.Trim(), 256);
        if (request.Target is not null) intent.Target = PlatformAccess.Clip(request.Target.Trim(), 128);
        if (request.WorkspaceId is not null) intent.WorkspaceId = request.WorkspaceId;
        if (request.AutomationIds is not null) intent.AutomationIds = request.AutomationIds.Distinct().Take(50).ToList();
        var before = intent.Status;
        if (request.Status is "active" or "paused" or "achieved") intent.Status = request.Status;
        intent.UpdatedAt = access.Now;
        await intentStore.SaveAsync(scope, intent, cancellationToken);
        await audit.LogAsync(id is null ? "platform.intent.created" : "platform.intent.updated", intent.Id, id is null ? null : before, intent.Status, cancellationToken);
        return PlatformResult<PlatformIntentDto>.Success((await IntentsAsync(cancellationToken)).First(i => i.Id == intent.Id));
    }

    public async Task<PlatformResult<object>> IntentActionAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var scope = access.Scope;
        var intent = await intentStore.GetAsync(scope, id, cancellationToken);
        if (intent is null) return PlatformResult<object>.Fail("Intent not found.", 404);
        var before = intent.Status;
        intent.Status = action switch { "pause" => "paused", "resume" => "active", "achieve" => "achieved", "archive" => "archived", _ => intent.Status };
        intent.UpdatedAt = access.Now;
        await intentStore.SaveAsync(scope, intent, cancellationToken);
        await audit.LogAsync($"platform.intent.{action}", intent.Id, before, intent.Status, cancellationToken);
        return PlatformResult<object>.Success(new { intent.Id, intent.Status });
    }

    // ---------------------------------------------------------------- sources

    /// <summary>Approvals waiting for a decision. Only people who can decide an approval see them, and only as links.</summary>
    private async Task<List<PlatformInboxItemDto>> ApprovalsAsync(Guid? primary, IReadOnlyDictionary<Guid, string> names, CancellationToken cancellationToken)
    {
        if (!await permissionService.HasPermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken)) return [];
        var rows = (await approvalRepository.ListAsync(access.Scope, cancellationToken))
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.CreatedAt).Take(100).ToList();
        return rows.Select(a => new PlatformInboxItemDto(
            $"apr-{a.ApprovalId:D}", "approval", "awaiting_approval", string.Equals(a.RiskLevel, "high", StringComparison.OrdinalIgnoreCase) ? "high" : "medium",
            $"{a.ApprovalType.Replace('_', ' ')} for {ClaimReference.For(a.CaseId)}", PlatformAccess.Clip(a.EvidenceSummary, 400), primary,
            primary is { } p ? names.GetValueOrDefault(p, "Insurance Intelligence") : "Insurance Intelligence", null, null, null, "approval", 1,
            PlatformAccess.Element("{}"), [], $"{PlatformRoutes.Approvals}", a.CreatedAt, a.UpdatedAt, null, null)).ToList();
    }
}

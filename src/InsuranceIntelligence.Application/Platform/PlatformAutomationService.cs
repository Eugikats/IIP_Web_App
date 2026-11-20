using System.Globalization;
using System.Text.Json;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Platform;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Platform;

public sealed record PlatformAutomationDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string TemplateKey,
    string TemplateName,
    string Name,
    string? Objective,
    JsonElement Trigger,
    JsonElement Logic,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> Recipients,
    IReadOnlyList<string> Approvers,
    JsonElement Policy,
    string Status,
    int Version,
    bool TestedCurrentVersion,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt,
    string? LastRunStatus,
    string? LastRunMessage,
    bool HasRunner,
    IReadOnlyList<string> AllowedActions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlatformAutomationRequest(
    Guid? WorkspaceId,
    string? TemplateKey,
    string? Name,
    string? Objective,
    JsonElement? Trigger,
    JsonElement? Logic,
    IReadOnlyList<string>? Outputs,
    IReadOnlyList<string>? Recipients,
    IReadOnlyList<string>? Approvers,
    JsonElement? Policy);

public sealed record PlatformRunDto(
    Guid Id,
    Guid AutomationId,
    int AutomationVersion,
    string Kind,
    string Status,
    string? Summary,
    int Findings,
    int NewFindings,
    JsonElement Evidence,
    string TriggeredBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record AutomationFinding(string Key, string Title, string Body, string Severity, object Evidence);

public sealed record AutomationEvaluation(string Status, string Summary, IReadOnlyList<AutomationFinding> Findings, object Evidence);

public interface IPlatformAutomationService
{
    Task<IReadOnlyList<PlatformAutomationDto>> ListAsync(Guid? workspaceId, CancellationToken cancellationToken);
    Task<PlatformAutomationDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformRunDto>> RunsAsync(Guid id, CancellationToken cancellationToken);
    Task<PlatformResult<PlatformAutomationDto>> CreateAsync(PlatformAutomationRequest request, CancellationToken cancellationToken);
    Task<PlatformResult<PlatformAutomationDto>> UpdateAsync(Guid id, PlatformAutomationRequest request, CancellationToken cancellationToken);
    Task<PlatformResult<object>> ActAsync(Guid id, string action, CancellationToken cancellationToken);
    Task<PlatformRunDto?> RunAsync(PlatformAutomation automation, string kind, string triggeredBy, CancellationToken cancellationToken);

    /// <summary>Runs every active automation in the current scope whose next run time has passed. Used by the background poller.</summary>
    Task<int> RunDueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The governed automation lifecycle: Draft, Tested, Awaiting approval, Approved, Active, Paused, Failed, Retired.
/// A test is a dry run that never writes to the inbox. Editing what an automation does creates a new version and returns
/// it to Draft. Runners are read-only over IIP data and only ever raise inbox items: no runner changes a claim, a policy
/// or an approval, and none decides anything.
/// </summary>
public sealed class PlatformAutomationService(
    IPlatformAutomationStore automationStore,
    IPlatformRunStore runStore,
    IOrgWorkspaceStore workspaceStore,
    IPlatformInboxStore inboxStore,
    IPlatformWorkspaceService workspaces,
    IPlatformInboxService inbox,
    ICaseRepository caseRepository,
    IPolicyRepository policyRepository,
    IApprovalRepository approvalRepository,
    PlatformAccess access,
    PlatformAudit audit) : IPlatformAutomationService
{
    /// <summary>Which lifecycle action is allowed from which status, and where it leads.</summary>
    internal static readonly IReadOnlyDictionary<string, (string[] From, string To)> Transitions = new Dictionary<string, (string[], string)>
    {
        ["submit"] = ([PlatformAutomationStatuses.Tested], PlatformAutomationStatuses.AwaitingApproval),
        ["approve"] = ([PlatformAutomationStatuses.AwaitingApproval], PlatformAutomationStatuses.Approved),
        ["reject"] = ([PlatformAutomationStatuses.AwaitingApproval], PlatformAutomationStatuses.Draft),
        ["activate"] = ([PlatformAutomationStatuses.Approved, PlatformAutomationStatuses.Paused], PlatformAutomationStatuses.Active),
        ["pause"] = ([PlatformAutomationStatuses.Active, PlatformAutomationStatuses.Failed], PlatformAutomationStatuses.Paused),
        ["resume"] = ([PlatformAutomationStatuses.Paused, PlatformAutomationStatuses.Failed], PlatformAutomationStatuses.Active),
        ["retire"] = ([PlatformAutomationStatuses.Draft, PlatformAutomationStatuses.Tested, PlatformAutomationStatuses.AwaitingApproval,
            PlatformAutomationStatuses.Approved, PlatformAutomationStatuses.Active, PlatformAutomationStatuses.Paused, PlatformAutomationStatuses.Failed],
            PlatformAutomationStatuses.Retired)
    };

    internal static readonly HashSet<string> ApproverActions = ["approve", "reject", "activate", "resume"];

    private static readonly CaseStatus[] ClosedStatuses = [CaseStatus.Settled, CaseStatus.Closed];

    public static PlatformAutomation NewFromTemplate(AutomationTemplate template, Guid workspaceId, PlatformAccess access) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = workspaceId,
        TemplateKey = template.Key,
        Name = template.Name,
        Objective = template.Objective,
        TriggerJson = PlatformAccess.Write(new { type = template.DefaultTrigger, schedule = template.DefaultSchedule, time = "07:00" }),
        LogicJson = PlatformAccess.Write(template.Fields.ToDictionary(f => f.Key, f => f.Default)),
        Outputs = ["inbox"],
        Recipients = [],
        Approvers = [],
        PolicyJson = PlatformAccess.Write(new { requireApproval = true, maxItemsPerRun = 25 }),
        Status = PlatformAutomationStatuses.Draft,
        Version = 1,
        CreatedByUserId = access.UserId,
        CreatedAt = access.Now,
        UpdatedAt = access.Now
    };

    public async Task<IReadOnlyList<PlatformAutomationDto>> ListAsync(Guid? workspaceId, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        await workspaces.EnsurePrimaryAsync(cancellationToken);
        var visible = (await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).ToHashSet();
        var rows = (await automationStore.ListAsync(access.Scope, 1000, cancellationToken))
            .Where(a => a.Status != PlatformAutomationStatuses.Retired && visible.Contains(a.WorkspaceId) && (workspaceId == null || a.WorkspaceId == workspaceId))
            .Take(200).ToList();
        var names = await WorkspaceNamesAsync(cancellationToken);
        var canApprove = await access.CanApproveAsync(cancellationToken);
        return rows.Select(a => ToDto(a, names, canApprove)).ToList();
    }

    public async Task<PlatformAutomationDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var row = await automationStore.GetAsync(access.Scope, id, cancellationToken);
        if (row is null || !(await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).Contains(row.WorkspaceId)) return null;
        return ToDto(row, await WorkspaceNamesAsync(cancellationToken), await access.CanApproveAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<PlatformRunDto>> RunsAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var row = await automationStore.GetAsync(access.Scope, id, cancellationToken);
        if (row is null || !(await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).Contains(row.WorkspaceId)) return [];
        return (await runStore.ListForAutomationAsync(access.Scope, id, 30, cancellationToken)).Select(ToRunDto).ToList();
    }

    public async Task<PlatformResult<PlatformAutomationDto>> CreateAsync(PlatformAutomationRequest request, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var template = PlatformTemplates.Automation(request.TemplateKey);
        if (template is null) return PlatformResult<PlatformAutomationDto>.Fail("Choose an automation template.");
        var primary = await workspaces.EnsurePrimaryAsync(cancellationToken);
        var workspaceId = request.WorkspaceId ?? primary.Id;
        if (!(await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).Contains(workspaceId))
            return PlatformResult<PlatformAutomationDto>.Fail("Workspace not found.", 404);

        var automation = NewFromTemplate(template, workspaceId, access);
        Apply(automation, request);
        await automationStore.SaveAsync(access.Scope, automation, cancellationToken);
        await audit.LogAsync("platform.automation.created", automation.Id, null, automation.Status, cancellationToken);
        return PlatformResult<PlatformAutomationDto>.Success(ToDto(automation, await WorkspaceNamesAsync(cancellationToken), await access.CanApproveAsync(cancellationToken)));
    }

    /// <summary>Editing what an automation does creates a new version and sends it back to Draft for testing and approval.</summary>
    public async Task<PlatformResult<PlatformAutomationDto>> UpdateAsync(Guid id, PlatformAutomationRequest request, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var automation = await automationStore.GetAsync(access.Scope, id, cancellationToken);
        if (automation is null || !(await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).Contains(automation.WorkspaceId))
            return PlatformResult<PlatformAutomationDto>.Fail("Automation not found.", 404);
        if (automation.Status == PlatformAutomationStatuses.Retired) return PlatformResult<PlatformAutomationDto>.Fail("A retired automation cannot be changed.");

        var before = automation.Status;
        var fingerprint = Fingerprint(automation);
        Apply(automation, request);
        if (Fingerprint(automation) != fingerprint)
        {
            automation.Version += 1;
            automation.Status = PlatformAutomationStatuses.Draft;
            automation.ApprovedAt = null;
            automation.ApprovedByUserId = null;
            automation.NextRunAt = null;
        }

        automation.UpdatedAt = access.Now;
        await automationStore.SaveAsync(access.Scope, automation, cancellationToken);
        await audit.LogAsync("platform.automation.updated", automation.Id, before, automation.Status, cancellationToken);
        return PlatformResult<PlatformAutomationDto>.Success(ToDto(automation, await WorkspaceNamesAsync(cancellationToken), await access.CanApproveAsync(cancellationToken)));
    }

    public async Task<PlatformResult<object>> ActAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var automation = await automationStore.GetAsync(access.Scope, id, cancellationToken);
        if (automation is null || !(await workspaces.VisibleWorkspaceIdsAsync(cancellationToken)).Contains(automation.WorkspaceId))
            return PlatformResult<object>.Fail("Automation not found.", 404);
        action = (action ?? "").Trim().ToLowerInvariant();
        var canApprove = await access.CanApproveAsync(cancellationToken);

        if (action == "test")
        {
            if (automation.Status == PlatformAutomationStatuses.Retired) return PlatformResult<object>.Fail("A retired automation cannot be tested.", 409);
            var test = await RunAsync(automation, "test", access.UserId, cancellationToken);
            return PlatformResult<object>.Success(new { automation = ToDto(automation, await WorkspaceNamesAsync(cancellationToken), canApprove), run = test });
        }

        if (action == "run")
        {
            if (automation.Status != PlatformAutomationStatuses.Active) return PlatformResult<object>.Fail("Only an active automation can run. Test, approve and activate it first.");
            var run = await RunAsync(automation, "run", access.UserId, cancellationToken);
            return PlatformResult<object>.Success(new { automation = ToDto(automation, await WorkspaceNamesAsync(cancellationToken), canApprove), run });
        }

        if (!Transitions.TryGetValue(action, out var rule)) return PlatformResult<object>.Fail("Unknown action.");
        if (!rule.From.Contains(automation.Status)) return PlatformResult<object>.Fail($"You cannot {action} an automation that is {automation.Status.Replace('_', ' ')}.", 409);
        if (ApproverActions.Contains(action) && !canApprove) return PlatformResult<object>.Fail("Only an approver can do this.", 403);
        if (action == "submit" && automation.LastTestedVersion != automation.Version) return PlatformResult<object>.Fail("Test this version before submitting it.", 409);
        if (action is "activate" or "resume" && !(PlatformTemplates.Automation(automation.TemplateKey)?.HasRunner ?? false))
            return PlatformResult<object>.Fail("This automation needs its data and logic configured before it can run.", 409);

        var before = automation.Status;
        automation.Status = rule.To;
        if (action == "approve")
        {
            automation.ApprovedAt = access.Now;
            automation.ApprovedByUserId = access.UserId;
        }

        automation.NextRunAt = automation.Status == PlatformAutomationStatuses.Active ? NextRun(automation, access.Now) : null;
        automation.UpdatedAt = access.Now;
        await automationStore.SaveAsync(access.Scope, automation, cancellationToken);
        await audit.LogAsync($"platform.automation.{action}", automation.Id, before, automation.Status, cancellationToken);
        return PlatformResult<object>.Success(new { automation = ToDto(automation, await WorkspaceNamesAsync(cancellationToken), canApprove) });
    }

    /// <summary>A test is a dry run: it reads the data and reports what it would raise, but never writes to the inbox.</summary>
    public async Task<PlatformRunDto?> RunAsync(PlatformAutomation automation, string kind, string triggeredBy, CancellationToken cancellationToken)
    {
        var scope = access.Scope;
        var started = access.Now;
        AutomationEvaluation evaluation;
        try
        {
            evaluation = await EvaluateAsync(automation, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evaluation = new AutomationEvaluation("failed", "The automation could not read its data: " + ex.Message, [], new { });
        }

        var policy = PlatformAccess.Read<Dictionary<string, JsonElement>>(automation.PolicyJson, []);
        var cap = policy.TryGetValue("maxItemsPerRun", out var max) && max.ValueKind == JsonValueKind.Number && max.TryGetInt32(out var n) ? Math.Clamp(n, 1, 200) : 25;
        var newFindings = 0;
        if (kind == "run" && evaluation.Status != "failed")
        {
            foreach (var finding in evaluation.Findings.Take(cap))
            {
                var fresh = await inbox.RaiseAsync(new InboxRaise(
                    automation.WorkspaceId, automation.Id, null, "insight",
                    finding.Severity is "high" or "critical" ? PlatformInboxStatuses.NeedsAttention : PlatformInboxStatuses.Recommendation,
                    finding.Severity, finding.Title, finding.Body, finding.Evidence, "automation", automation.Id.ToString("N"),
                    $"auto:{automation.Id:N}:{finding.Key}"), cancellationToken);
                if (fresh) newFindings++;
            }
        }

        var run = new PlatformAutomationRun
        {
            Id = Guid.NewGuid(),
            AutomationId = automation.Id,
            AutomationVersion = automation.Version,
            Kind = kind,
            Status = evaluation.Status,
            Summary = PlatformAccess.Clip(evaluation.Summary, 1000),
            Findings = evaluation.Findings.Count,
            NewFindings = newFindings,
            EvidenceJson = PlatformAccess.Write(new
            {
                summary = evaluation.Evidence,
                findings = evaluation.Findings.Take(cap).Select(f => new { f.Key, f.Title, f.Severity })
            }),
            TriggeredBy = PlatformAccess.Clip(triggeredBy, 64),
            StartedAt = started,
            FinishedAt = access.Now
        };
        await runStore.SaveAsync(scope, run, CancellationToken.None);

        if (kind == "test")
        {
            if (evaluation.Status != "failed" && automation.Status is PlatformAutomationStatuses.Draft or PlatformAutomationStatuses.Tested)
            {
                automation.Status = PlatformAutomationStatuses.Tested;
                automation.LastTestedVersion = automation.Version;
            }
        }
        else
        {
            automation.LastRunAt = access.Now;
            automation.LastRunStatus = evaluation.Status;
            automation.LastRunMessage = PlatformAccess.Clip(evaluation.Summary, 1000);
            if (evaluation.Status == "failed") automation.Status = PlatformAutomationStatuses.Failed;
            automation.NextRunAt = automation.Status == PlatformAutomationStatuses.Active ? NextRun(automation, access.Now) : null;
        }

        automation.UpdatedAt = access.Now;
        await automationStore.SaveAsync(scope, automation, CancellationToken.None);
        return ToRunDto(run);
    }

    public async Task<int> RunDueAsync(CancellationToken cancellationToken)
    {
        var now = access.Now;
        var due = (await automationStore.ListAsync(access.Scope, 1000, cancellationToken))
            .Where(a => a.Status == PlatformAutomationStatuses.Active && a.NextRunAt is { } next && next <= now)
            .ToList();
        var ran = 0;
        foreach (var automation in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RunAsync(automation, "run", "system:scheduler", cancellationToken);
            ran++;
        }

        return ran;
    }

    // ---------------------------------------------------------------- runners (read-only over IIP data)

    private async Task<AutomationEvaluation> EvaluateAsync(PlatformAutomation automation, CancellationToken cancellationToken)
    {
        var logic = PlatformAccess.Read<Dictionary<string, JsonElement>>(automation.LogicJson, []);
        decimal Num(string key, decimal fallback) =>
            logic.TryGetValue(key, out var v) && (v.ValueKind == JsonValueKind.Number ? v.TryGetDecimal(out var d) : decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) ? d : fallback;

        var now = access.Now;
        var scope = access.Scope;
        switch (automation.TemplateKey)
        {
            case "claims_sla":
            {
                var hours = (double)Math.Max(0, Num("hours", 24));
                var horizon = now.AddHours(hours);
                var claims = OpenClaims(await caseRepository.ListAsync(scope, cancellationToken))
                    .Where(c => c.SlaDueAt is { } due && due <= horizon)
                    .OrderBy(c => c.SlaDueAt).Take(200).ToList();
                var findings = claims.Select(c =>
                {
                    var past = c.SlaDueAt!.Value <= now;
                    var when = c.SlaDueAt!.Value;
                    return new AutomationFinding($"claim:{c.CaseId:N}",
                        past ? $"{ClaimReference.For(c.CaseId)} is past its service deadline" : $"{ClaimReference.For(c.CaseId)} is due within {hours:0} hours",
                        $"{c.CustomerName}, policy {c.PolicyNumber}. Deadline {when:dd MMM HH:mm} UTC, status {Words(c.Status.ToString())}.",
                        past ? "high" : "medium", ClaimEvidence(c));
                }).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} open claims are past or near their service deadline.", findings, new { hours, count = findings.Count });
            }
            case "claims_stalled":
            {
                var days = (int)Math.Max(1, Num("days", 5));
                var cutoff = now.AddDays(-days);
                var claims = OpenClaims(await caseRepository.ListAsync(scope, cancellationToken))
                    .Where(c => c.UpdatedAt <= cutoff).OrderBy(c => c.UpdatedAt).Take(200).ToList();
                var findings = claims.Select(c =>
                {
                    var waited = (int)(now - c.UpdatedAt).TotalDays;
                    return new AutomationFinding($"claim:{c.CaseId:N}", $"{ClaimReference.For(c.CaseId)} has not moved for {waited} days",
                        $"{c.CustomerName}, policy {c.PolicyNumber}. Status {Words(c.Status.ToString())} since {c.UpdatedAt:dd MMM}.",
                        waited >= days * 2 ? "high" : "medium", ClaimEvidence(c));
                }).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} open claims have had no update for more than {days} days.", findings, new { days, count = findings.Count });
            }
            case "claims_missing_documents":
            {
                var days = (int)Math.Max(0, Num("days", 2));
                var cutoff = now.AddDays(-days);
                var claims = OpenClaims(await caseRepository.ListAsync(scope, cancellationToken))
                    .Where(c => c.Status == CaseStatus.MissingDocuments && c.UpdatedAt <= cutoff)
                    .OrderBy(c => c.UpdatedAt).Take(200).ToList();
                var findings = claims.Select(c =>
                {
                    var waited = (int)(now - c.UpdatedAt).TotalDays;
                    return new AutomationFinding($"claim:{c.CaseId:N}", $"{ClaimReference.For(c.CaseId)} has waited {waited} days for documents",
                        $"{c.CustomerName}, policy {c.PolicyNumber}. File completeness {c.CompletenessScore:0}%.",
                        waited >= Math.Max(days, 1) * 2 ? "high" : "medium", ClaimEvidence(c));
                }).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} claims have waited on documents for more than {days} days.", findings, new { days, count = findings.Count });
            }
            case "claims_unassigned":
            {
                var hours = (double)Math.Max(0, Num("hours", 4));
                var cutoff = now.AddHours(-hours);
                var claims = OpenClaims(await caseRepository.ListAsync(scope, cancellationToken))
                    .Where(c => string.IsNullOrWhiteSpace(c.AssignedUserId) && c.CreatedAt <= cutoff)
                    .OrderBy(c => c.CreatedAt).Take(200).ToList();
                var findings = claims.Select(c =>
                {
                    var waited = (int)(now - c.CreatedAt).TotalHours;
                    return new AutomationFinding($"claim:{c.CaseId:N}", $"{ClaimReference.For(c.CaseId)} has no owner",
                        $"{c.CustomerName}, policy {c.PolicyNumber}. Waiting {waited} hours since it was filed.",
                        waited >= hours * 3 ? "high" : "medium", ClaimEvidence(c));
                }).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} open claims have no owner.", findings, new { hours, count = findings.Count });
            }
            case "claims_high_risk":
            {
                var claims = OpenClaims(await caseRepository.ListAsync(scope, cancellationToken))
                    .Where(c => string.Equals(c.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => c.UpdatedAt).Take(200).ToList();
                var findings = claims.Select(c => new AutomationFinding($"claim:{c.CaseId:N}", $"{ClaimReference.For(c.CaseId)} is flagged high risk",
                    $"{c.CustomerName}, policy {c.PolicyNumber}. The rule engine flagged it for a person to review; it has not decided anything.",
                    "high", ClaimEvidence(c))).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} open claims are flagged high risk.", findings, new { count = findings.Count });
            }
            case "policies_expiring":
            {
                var days = (int)Math.Max(1, Num("days", 30));
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                var until = today.AddDays(days);
                var book = await policyRepository.ListAsync(scope, cancellationToken);
                if (book.Count == 0) return NoData("There are no policies yet. Import the policy book or connect the policy administration system.");
                var policies = book
                    .Where(p => p.EffectiveTo is { } end && end >= today && end <= until)
                    .OrderBy(p => p.EffectiveTo).Take(200).ToList();
                var findings = policies.Select(p =>
                {
                    var left = p.EffectiveTo!.Value.DayNumber - today.DayNumber;
                    return new AutomationFinding($"policy:{p.PolicyId}", $"Policy {p.PolicyNumber} expires in {left} day{(left == 1 ? "" : "s")}",
                        $"{p.ProductLine}, ends {p.EffectiveTo:dd MMM yyyy}. Follow up on the renewal.",
                        left <= 7 ? "high" : "medium", new { policyNumber = p.PolicyNumber, p.ProductLine, expires = p.EffectiveTo, daysLeft = left });
                }).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} policies expire within {days} days.", findings, new { days, count = findings.Count });
            }
            case "approvals_waiting":
            {
                var hours = (double)Math.Max(0, Num("hours", 24));
                var cutoff = now.AddHours(-hours);
                var approvals = (await approvalRepository.ListAsync(scope, cancellationToken))
                    .Where(a => a.Status == ApprovalStatus.Pending && a.CreatedAt <= cutoff)
                    .OrderBy(a => a.CreatedAt).Take(200).ToList();
                var findings = approvals.Select(a =>
                {
                    var waited = (int)(now - a.CreatedAt).TotalHours;
                    return new AutomationFinding($"approval:{a.ApprovalId:N}", $"{a.ApprovalType.Replace('_', ' ')} for {ClaimReference.For(a.CaseId)} has waited {waited} hours",
                        "It is waiting for a person to decide. Open the approvals screen to review the evidence.",
                        waited >= hours * 3 ? "high" : "medium", new { claim = ClaimReference.For(a.CaseId), caseId = a.CaseId, type = a.ApprovalType, requested = a.CreatedAt });
                }).ToList();
                return new AutomationEvaluation("succeeded", $"{findings.Count} approvals have waited more than {hours:0} hours.", findings, new { hours, count = findings.Count });
            }
            case "daily_briefing":
            {
                var claims = OpenClaims(await caseRepository.ListAsync(scope, cancellationToken)).ToList();
                var late = claims.Count(c => c.SlaDueAt is { } due && due <= now);
                var missing = claims.Count(c => c.Status == CaseStatus.MissingDocuments);
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                var expiring = (await policyRepository.ListAsync(scope, cancellationToken)).Count(p => p.EffectiveTo is { } end && end >= today && end <= today.AddDays(30));
                var approvals = (await approvalRepository.ListAsync(scope, cancellationToken)).Count(a => a.Status == ApprovalStatus.Pending);
                var attention = (await inboxStore.ListAsync(scope, 1000, cancellationToken)).Count(i => i.Status == PlatformInboxStatuses.NeedsAttention);
                var evidence = new { openClaims = claims.Count, pastDeadline = late, missingDocuments = missing, policiesExpiring30Days = expiring, approvalsWaiting = approvals, needsAttention = attention };
                var day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var body = $"{claims.Count} open claims, {late} past their deadline, {missing} missing documents, {expiring} policies expiring in 30 days, {approvals} approvals waiting, {attention} items need attention.";
                var finding = new AutomationFinding($"brief:{day}", $"Insurance briefing for {now:dddd d MMMM}", body, "low", evidence);
                return new AutomationEvaluation("succeeded", body, [finding], evidence);
            }
            default:
                return new AutomationEvaluation("needs_configuration", "This automation needs its data and logic configured by a person before it can run.", [], new { });
        }
    }

    /// <summary>Claims a person is working: not settled or closed, and not an unconfirmed claim IIP created on its own.</summary>
    private static IEnumerable<ClaimCase> OpenClaims(IEnumerable<ClaimCase> claims) =>
        claims.Where(c => !ClosedStatuses.Contains(c.Status) && !c.RequiresConfirmation);

    private static object ClaimEvidence(ClaimCase c) => new
    {
        claim = ClaimReference.For(c.CaseId),
        caseId = c.CaseId,
        policyNumber = c.PolicyNumber,
        status = c.Status.ToString(),
        slaDueAt = c.SlaDueAt,
        updatedAt = c.UpdatedAt
    };

    private static string Words(string pascal) =>
        string.Concat(pascal.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

    private static AutomationEvaluation NoData(string message) => new("needs_data", message, [], new { });

    // ---------------------------------------------------------------- helpers

    /// <summary>The next run time, in UTC. Hourly, daily at the set time, or weekly on Monday at the set time.</summary>
    public static DateTimeOffset NextRun(PlatformAutomation automation, DateTimeOffset now)
    {
        var trigger = PlatformAccess.Read<Dictionary<string, JsonElement>>(automation.TriggerJson, []);
        var schedule = trigger.TryGetValue("schedule", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "daily" : "daily";
        var time = trigger.TryGetValue("time", out var t) && t.ValueKind == JsonValueKind.String && TimeOnly.TryParse(t.GetString(), CultureInfo.InvariantCulture, out var parsed) ? parsed : new TimeOnly(7, 0);
        var utc = now.UtcDateTime;
        switch (schedule)
        {
            case "hourly":
                return now.AddHours(1);
            case "weekly":
            {
                var next = utc.Date.Add(time.ToTimeSpan());
                while (next <= utc || next.DayOfWeek != DayOfWeek.Monday) next = next.AddDays(1);
                return new DateTimeOffset(DateTime.SpecifyKind(next, DateTimeKind.Utc));
            }
            default:
            {
                var next = utc.Date.Add(time.ToTimeSpan());
                if (next <= utc) next = next.AddDays(1);
                return new DateTimeOffset(DateTime.SpecifyKind(next, DateTimeKind.Utc));
            }
        }
    }

    private static void Apply(PlatformAutomation automation, PlatformAutomationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name)) automation.Name = PlatformAccess.Clip(request.Name.Trim(), 160);
        if (request.Objective is not null) automation.Objective = PlatformAccess.Clip(request.Objective.Trim(), 1000);
        if (request.Trigger is { ValueKind: JsonValueKind.Object } trigger) automation.TriggerJson = trigger.GetRawText();
        if (request.Logic is { ValueKind: JsonValueKind.Object } logic) automation.LogicJson = logic.GetRawText();
        if (request.Outputs is not null) automation.Outputs = request.Outputs.Take(10).ToList();
        if (request.Recipients is not null) automation.Recipients = request.Recipients.Where(r => !string.IsNullOrWhiteSpace(r)).Take(50).ToList();
        if (request.Approvers is not null) automation.Approvers = request.Approvers.Where(r => !string.IsNullOrWhiteSpace(r)).Take(20).ToList();
        if (request.Policy is { ValueKind: JsonValueKind.Object } policy) automation.PolicyJson = policy.GetRawText();
    }

    private static string Fingerprint(PlatformAutomation a) =>
        string.Join("|", a.TriggerJson, a.LogicJson, PlatformAccess.Write(a.Outputs), PlatformAccess.Write(a.Recipients), PlatformAccess.Write(a.Approvers), a.PolicyJson);

    private async Task<Dictionary<Guid, string>> WorkspaceNamesAsync(CancellationToken cancellationToken) =>
        (await workspaceStore.ListAsync(access.Scope, 500, cancellationToken)).ToDictionary(w => w.Id, w => w.Name);

    private static PlatformAutomationDto ToDto(PlatformAutomation a, IReadOnlyDictionary<Guid, string> names, bool canApprove)
    {
        var template = PlatformTemplates.Automation(a.TemplateKey);
        var allowed = Transitions.Where(t => t.Value.From.Contains(a.Status) && (!ApproverActions.Contains(t.Key) || canApprove))
            .Select(t => t.Key).ToList();
        if (a.Status != PlatformAutomationStatuses.Retired) allowed.Insert(0, "test");
        if (a.Status == PlatformAutomationStatuses.Active) allowed.Insert(1, "run");
        if (a.LastTestedVersion != a.Version) allowed.Remove("submit");
        return new PlatformAutomationDto(
            a.Id, a.WorkspaceId, names.GetValueOrDefault(a.WorkspaceId, ""), a.TemplateKey, template?.Name ?? a.TemplateKey,
            a.Name, a.Objective,
            PlatformAccess.Element(a.TriggerJson), PlatformAccess.Element(a.LogicJson),
            a.Outputs, a.Recipients, a.Approvers, PlatformAccess.Element(a.PolicyJson),
            a.Status, a.Version, a.LastTestedVersion == a.Version, a.ApprovedAt, a.NextRunAt, a.LastRunAt, a.LastRunStatus, a.LastRunMessage,
            template?.HasRunner ?? false, allowed, a.CreatedAt, a.UpdatedAt);
    }

    private static PlatformRunDto ToRunDto(PlatformAutomationRun r) => new(
        r.Id, r.AutomationId, r.AutomationVersion, r.Kind, r.Status, r.Summary, r.Findings, r.NewFindings,
        PlatformAccess.Element(r.EvidenceJson), r.TriggeredBy, r.StartedAt, r.FinishedAt);
}

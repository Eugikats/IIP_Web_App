using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Platform;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Platform;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The organisation platform ported from CIP: the lifecycle that keeps an automation from running until a person has tested
/// and approved it, the inbox that raises each condition once, intent health, and the tenant boundary around all of it.
/// </summary>
public sealed class OrganisationPlatformTests : IDisposable
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-operations");
    private readonly InsuranceIntelligenceDbContext db = NewDb($"platform-{Guid.NewGuid():N}");

    public void Dispose() => db.Dispose();

    private static InsuranceIntelligenceDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(name).Options);

    private sealed record Platform(
        PlatformWorkspaceService Workspaces,
        PlatformInboxService Inbox,
        PlatformAutomationService Automations,
        FakeCases Cases,
        FakePolicies Policies,
        FakeApprovals Approvals,
        List<AuditEvent> Audit);

    private Platform Build(PlatformRole role, TenantScope? scope = null, InsuranceIntelligenceDbContext? database = null, TimeProvider? time = null)
    {
        var useDb = database ?? db;
        var context = new FixedContext(new TenantContext((scope ?? Scope).TenantId, (scope ?? Scope).WorkspaceId, "user-1", [role.ToString()]));
        var permissions = new RolePermissions(role);
        var access = new PlatformAccess(context, permissions, time);
        var audit = new PlatformAudit(new RecordingAudit(out var events), context, time);
        var workspaceStore = new EfOrgWorkspaceStore(useDb);
        var automationStore = new EfPlatformAutomationStore(useDb);
        var inboxStore = new EfPlatformInboxStore(useDb);
        var cases = new FakeCases();
        var policies = new FakePolicies();
        var approvals = new FakeApprovals();
        var workspaces = new PlatformWorkspaceService(workspaceStore, automationStore, inboxStore, new EfPlatformSettingsStore(useDb), null!, null!, access, audit);
        var inbox = new PlatformInboxService(inboxStore, new EfPlatformIntentStore(useDb), workspaceStore, automationStore, approvals, permissions, access, audit);
        var automations = new PlatformAutomationService(automationStore, new EfPlatformRunStore(useDb), workspaceStore, inboxStore, workspaces, inbox, cases, policies, approvals, access, audit);
        return new Platform(workspaces, inbox, automations, cases, policies, approvals, events);
    }

    private static PlatformAutomationRequest Template(string key) => new(null, key, null, null, null, null, null, null, null, null);

    private static ClaimCase Claim(string customer, int updatedDaysAgo = 0, DateTimeOffset? sla = null, CaseStatus status = CaseStatus.InReview, string? assigned = "officer-1", string risk = "Low", bool confirm = false) => new()
    {
        CaseId = Guid.NewGuid(),
        Scope = Scope,
        CustomerName = customer,
        PolicyNumber = "POL-" + customer.Length,
        ProductLine = "Motor",
        ClaimType = "Accident",
        Channel = "WhatsApp",
        IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
        IncidentDescription = "Collision.",
        Status = status,
        CompletenessScore = 80,
        RiskLevel = risk,
        SlaDueAt = sla,
        RequiresConfirmation = confirm,
        AssignedUserId = assigned,
        CreatedByUserId = "officer-1",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-updatedDaysAgo - 1),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-updatedDaysAgo),
        CorrelationId = "test",
    };

    [Fact]
    public async Task PrimaryWorkspace_IsTheExistingIipWorkspace_AndIsCreatedOnce()
    {
        var p = Build(PlatformRole.ClaimsOfficer);

        var first = await p.Workspaces.EnsurePrimaryAsync(CancellationToken.None);
        var second = await p.Workspaces.EnsurePrimaryAsync(CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("/app/workspace", first.Route);
        Assert.Equal(OrgWorkspaceStatuses.Live, first.Status);
        Assert.Single(db.OrgWorkspaces);
    }

    [Fact]
    public async Task OnlyAdministrators_CreateWorkspaces_AndSuggestedAutomationsStartAsDrafts()
    {
        var request = new PlatformWorkspaceRequest("claims_management", "Claims", null, null, null, null, null, null, ["claims_sla", "claims_stalled"]);

        var denied = await Build(PlatformRole.ClaimsOfficer).Workspaces.CreateAsync(request, CancellationToken.None);
        Assert.Equal(403, denied.Status);

        var created = await Build(PlatformRole.ITAdmin).Workspaces.CreateAsync(request, CancellationToken.None);

        Assert.True(created.Ok);
        Assert.Equal(OrgWorkspaceStatuses.AwaitingData, created.Value!.Status);
        Assert.Equal(2, db.PlatformAutomations.Count());
        Assert.All(db.PlatformAutomations, a => Assert.Equal(PlatformAutomationStatuses.Draft, a.Status));
    }

    [Fact]
    public async Task Lifecycle_RequiresTestThenApproval_AndRunsRaiseEachConditionOnce()
    {
        var p = Build(PlatformRole.Admin);
        var stalled = Claim("Acme Traders", updatedDaysAgo: 9);
        p.Cases.Items.Add(stalled);
        var id = (await p.Automations.CreateAsync(Template("claims_stalled"), CancellationToken.None)).Value!.Id;

        Assert.Equal(409, (await p.Automations.ActAsync(id, "submit", CancellationToken.None)).Status);

        await p.Automations.ActAsync(id, "test", CancellationToken.None);
        Assert.Empty(db.PlatformInboxItems); // a test never raises anything
        Assert.True((await p.Automations.ActAsync(id, "submit", CancellationToken.None)).Ok);
        Assert.True((await p.Automations.ActAsync(id, "approve", CancellationToken.None)).Ok);
        Assert.True((await p.Automations.ActAsync(id, "activate", CancellationToken.None)).Ok);

        await p.Automations.ActAsync(id, "run", CancellationToken.None);
        await p.Automations.ActAsync(id, "run", CancellationToken.None);

        var row = Assert.Single(db.PlatformInboxItems);
        var item = JsonSerializer.Deserialize<PlatformInboxItem>(row.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Contains(ClaimReference.For(stalled.CaseId), item.Title);
        Assert.Equal(2, item.Occurrences);
        var runs = db.PlatformAutomationRuns.AsEnumerable()
            .Select(r => JsonSerializer.Deserialize<PlatformAutomationRun>(r.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .Where(r => r.Kind == "run").OrderBy(r => r.StartedAt).ToList();
        Assert.Equal([1, 0], runs.Select(r => r.NewFindings));
        var automation = (await p.Automations.GetAsync(id, CancellationToken.None))!;
        Assert.NotNull(automation.NextRunAt);
    }

    [Fact]
    public async Task OnlyApprovers_ApproveAutomations()
    {
        var p = Build(PlatformRole.ClaimsOfficer);
        var id = (await p.Automations.CreateAsync(Template("daily_briefing"), CancellationToken.None)).Value!.Id;
        await p.Automations.ActAsync(id, "test", CancellationToken.None);
        await p.Automations.ActAsync(id, "submit", CancellationToken.None);

        var result = await p.Automations.ActAsync(id, "approve", CancellationToken.None);

        Assert.Equal(403, result.Status);
        Assert.Equal(PlatformAutomationStatuses.AwaitingApproval, (await p.Automations.GetAsync(id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task ChangingWhatAnAutomationDoes_CreatesANewVersion_BackInDraft()
    {
        var p = Build(PlatformRole.Admin);
        var id = (await p.Automations.CreateAsync(Template("claims_stalled"), CancellationToken.None)).Value!.Id;
        foreach (var step in new[] { "test", "submit", "approve", "activate" }) await p.Automations.ActAsync(id, step, CancellationToken.None);

        var renamed = await p.Automations.UpdateAsync(id, new PlatformAutomationRequest(null, null, "Renamed", null, null, null, null, null, null, null), CancellationToken.None);
        Assert.Equal(PlatformAutomationStatuses.Active, renamed.Value!.Status);
        Assert.Equal(1, renamed.Value.Version);

        var logic = JsonDocument.Parse("""{"days":10}""").RootElement;
        var changed = await p.Automations.UpdateAsync(id, new PlatformAutomationRequest(null, null, null, null, null, logic, null, null, null, null), CancellationToken.None);

        Assert.Equal(PlatformAutomationStatuses.Draft, changed.Value!.Status);
        Assert.Equal(2, changed.Value.Version);
        Assert.False(changed.Value.TestedCurrentVersion);
    }

    [Fact]
    public async Task AnAutomationWithNoRunner_CannotBeActivated()
    {
        var p = Build(PlatformRole.Admin);
        var id = (await p.Automations.CreateAsync(Template("custom"), CancellationToken.None)).Value!.Id;
        foreach (var step in new[] { "test", "submit", "approve" }) await p.Automations.ActAsync(id, step, CancellationToken.None);

        var result = await p.Automations.ActAsync(id, "activate", CancellationToken.None);

        Assert.Equal(409, result.Status);
    }

    [Fact]
    public async Task OrganisationsNeverSeeEachOthersAutomationsOrInbox()
    {
        var a = Build(PlatformRole.Admin);
        await a.Automations.CreateAsync(Template("daily_briefing"), CancellationToken.None);
        await a.Inbox.RaiseAsync(new InboxRaise(null, null, null, "insight", PlatformInboxStatuses.NeedsAttention, "high", "Only for A", null, null, "test", null, "k1"), CancellationToken.None);

        var b = Build(PlatformRole.Admin, TenantScope.Create("tenant-b", "workspace-operations"));

        Assert.Empty(await b.Automations.ListAsync(null, CancellationToken.None));
        Assert.DoesNotContain(await b.Inbox.ListAsync(null, null, CancellationToken.None), i => i.Title == "Only for A");
        Assert.Equal(0, (await b.Inbox.CountsAsync(null, CancellationToken.None)).NeedsAttention);
    }

    [Fact]
    public async Task ASecondOrganisationCannotOpenAnotherOrganisationsAutomationById()
    {
        var a = Build(PlatformRole.Admin);
        var id = (await a.Automations.CreateAsync(Template("daily_briefing"), CancellationToken.None)).Value!.Id;

        var b = Build(PlatformRole.Admin, TenantScope.Create("tenant-b", "workspace-operations"));

        Assert.Null(await b.Automations.GetAsync(id, CancellationToken.None));
        Assert.Equal(404, (await b.Automations.ActAsync(id, "test", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task PendingApprovals_AppearInTheInbox_ButAreOnlyLinkedNeverDecidedHere()
    {
        var p = Build(PlatformRole.ClaimsSupervisor);
        var claim = Claim("Olive Nakato");
        p.Approvals.Items.Add(Approval(claim.CaseId));

        var waiting = Assert.Single(await p.Inbox.ListAsync("awaiting_approval", null, CancellationToken.None));
        Assert.StartsWith("apr-", waiting.Id);
        Assert.Contains(ClaimReference.For(claim.CaseId), waiting.Title);
        Assert.Equal(1, (await p.Inbox.CountsAsync(null, CancellationToken.None)).AwaitingApproval);

        var decided = await p.Inbox.ActAsync(waiting.Id, "resolve", null, CancellationToken.None);

        Assert.False(decided.Ok);
        Assert.Equal(409, decided.Status);
    }

    [Fact]
    public async Task PeopleWhoCannotDecideApprovals_DoNotSeeThemInTheInbox()
    {
        var p = Build(PlatformRole.CustomerService);
        p.Approvals.Items.Add(Approval(Guid.NewGuid()));

        Assert.Empty(await p.Inbox.ListAsync("awaiting_approval", null, CancellationToken.None));
    }

    [Fact]
    public async Task IntentHealth_FollowsTheOpenSignalsOfItsAutomations()
    {
        var p = Build(PlatformRole.Admin);
        var automation = (await p.Automations.CreateAsync(Template("claims_stalled"), CancellationToken.None)).Value!;
        var intent = (await p.Inbox.SaveIntentAsync(null, new PlatformIntentRequest("Decide every claim within 5 days", null, "Turnaround", "5 days", null, [automation.Id], null), CancellationToken.None)).Value!;
        Assert.Equal("on_track", intent.Health);

        await p.Inbox.RaiseAsync(new InboxRaise(null, automation.Id, null, "insight", PlatformInboxStatuses.Recommendation, "medium", "Claim waiting", null, null, "automation", null, "x1"), CancellationToken.None);
        Assert.Equal("at_risk", (await p.Inbox.IntentsAsync(CancellationToken.None)).Single().Health);

        await p.Inbox.RaiseAsync(new InboxRaise(null, automation.Id, null, "insight", PlatformInboxStatuses.NeedsAttention, "high", "Claim waiting long", null, null, "automation", null, "x2"), CancellationToken.None);
        Assert.Equal("off_track", (await p.Inbox.IntentsAsync(CancellationToken.None)).Single().Health);
    }

    [Theory]
    [InlineData(0, 0, "on_track")]
    [InlineData(1, 0, "at_risk")]
    [InlineData(3, 0, "off_track")]
    [InlineData(1, 1, "off_track")]
    public void Health_Rules(int open, int high, string expected) => Assert.Equal(expected, PlatformInboxService.Health(open, high));

    [Fact]
    public async Task ADismissedSignal_IsNotRaisedAgainWhileTheConditionPersists_AndAResolvedOneIs()
    {
        var p = Build(PlatformRole.Admin);
        InboxRaise Raise() => new(null, null, null, "insight", PlatformInboxStatuses.NeedsAttention, "high", "Same thing", null, null, "test", null, "same");

        Assert.True(await p.Inbox.RaiseAsync(Raise(), CancellationToken.None));
        var id = (await p.Inbox.ListAsync(null, null, CancellationToken.None)).Single(i => i.Title == "Same thing").Id;
        await p.Inbox.ActAsync(id, "dismiss", null, CancellationToken.None);
        Assert.False(await p.Inbox.RaiseAsync(Raise(), CancellationToken.None));

        await p.Inbox.ActAsync(id, "reopen", null, CancellationToken.None);
        await p.Inbox.ActAsync(id, "resolve", "Fixed", CancellationToken.None);
        Assert.True(await p.Inbox.RaiseAsync(Raise(), CancellationToken.None));
    }

    [Fact]
    public async Task SlaRunner_FlagsOverdueAndNearDueClaims_ButNotUnconfirmedOrClosedOnes()
    {
        var p = Build(PlatformRole.Admin);
        var overdue = Claim("Overdue Co", sla: DateTimeOffset.UtcNow.AddHours(-3));
        var near = Claim("Near Co", sla: DateTimeOffset.UtcNow.AddHours(5));
        p.Cases.Items.AddRange([
            overdue, near,
            Claim("Far Co", sla: DateTimeOffset.UtcNow.AddDays(10)),
            Claim("Unconfirmed", sla: DateTimeOffset.UtcNow.AddHours(-3), confirm: true),
            Claim("Settled Co", sla: DateTimeOffset.UtcNow.AddHours(-3), status: CaseStatus.Settled),
            Claim("No Deadline", sla: null)]);
        var id = (await p.Automations.CreateAsync(Template("claims_sla"), CancellationToken.None)).Value!.Id;
        foreach (var step in new[] { "test", "submit", "approve", "activate" }) await p.Automations.ActAsync(id, step, CancellationToken.None);

        var run = (await p.Automations.ActAsync(id, "run", CancellationToken.None)).Value!;

        var items = await p.Inbox.ListAsync("needs_attention", null, CancellationToken.None);
        Assert.Contains(items, i => i.Title.Contains(ClaimReference.For(overdue.CaseId)) && i.Severity == "high");
        var recommendations = await p.Inbox.ListAsync("recommendation", null, CancellationToken.None);
        Assert.Contains(recommendations, i => i.Title.Contains(ClaimReference.For(near.CaseId)));
        Assert.Equal(2, items.Count + recommendations.Count);
        Assert.NotNull(run);
    }

    [Fact]
    public async Task PoliciesExpiringRunner_ReportsNoDataHonestly_WhenThereAreNoPolicies()
    {
        var p = Build(PlatformRole.Admin);
        var id = (await p.Automations.CreateAsync(Template("policies_expiring"), CancellationToken.None)).Value!.Id;

        var test = (await p.Automations.ActAsync(id, "test", CancellationToken.None)).Value!;

        var json = JsonSerializer.Serialize(test, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("needs_data", json);
        Assert.Empty(db.PlatformInboxItems);
    }

    [Fact]
    public async Task RunDue_RunsOnlyActiveAutomationsWhoseTimeHasCome()
    {
        var time = new MovableTime(DateTimeOffset.UtcNow);
        var p = Build(PlatformRole.Admin, time: time);
        p.Cases.Items.Add(Claim("Late Co", sla: DateTimeOffset.UtcNow.AddHours(-3)));
        var id = (await p.Automations.CreateAsync(Template("claims_sla"), CancellationToken.None)).Value!.Id;
        foreach (var step in new[] { "test", "submit", "approve", "activate" }) await p.Automations.ActAsync(id, step, CancellationToken.None);
        var draft = (await p.Automations.CreateAsync(Template("claims_high_risk"), CancellationToken.None)).Value!.Id;

        Assert.Equal(0, await p.Automations.RunDueAsync(CancellationToken.None));

        time.Advance(TimeSpan.FromHours(2));
        Assert.Equal(1, await p.Automations.RunDueAsync(CancellationToken.None));
        Assert.Equal(PlatformAutomationStatuses.Draft, (await p.Automations.GetAsync(draft, CancellationToken.None))!.Status);
        Assert.Single(await p.Inbox.ListAsync("needs_attention", null, CancellationToken.None));
    }

    [Fact]
    public async Task EveryLifecycleChange_IsAudited_WithoutCustomerWords()
    {
        var p = Build(PlatformRole.Admin);
        p.Cases.Items.Add(Claim("Private Person", updatedDaysAgo: 9));
        var id = (await p.Automations.CreateAsync(Template("claims_stalled"), CancellationToken.None)).Value!.Id;
        foreach (var step in new[] { "test", "submit", "approve", "activate", "run" }) await p.Automations.ActAsync(id, step, CancellationToken.None);

        Assert.Contains(p.Audit, e => e.ActionName == "platform.automation.approve");
        Assert.DoesNotContain(p.Audit, e => (e.AuditReason ?? "").Contains("Private Person") || (e.AfterStatus ?? "").Contains("Private Person"));
    }

    [Fact]
    public void NextRun_FollowsTheSchedule()
    {
        var now = new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.Zero); // a Wednesday
        PlatformAutomation Make(string schedule, string time) => new() { TriggerJson = $$"""{"schedule":"{{schedule}}","time":"{{time}}"}""" };

        Assert.Equal(now.AddHours(1), PlatformAutomationService.NextRun(Make("hourly", "07:00"), now));
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 7, 0, 0, TimeSpan.Zero), PlatformAutomationService.NextRun(Make("daily", "07:00"), now));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 17, 0, 0, TimeSpan.Zero), PlatformAutomationService.NextRun(Make("daily", "17:00"), now));
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 7, 0, 0, TimeSpan.Zero), PlatformAutomationService.NextRun(Make("weekly", "07:00"), now));
    }

    [Fact]
    public void ThePlatformPermissions_AreHeldByTheRolesThatNeedThem()
    {
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ITAdmin, Permission.ManagePlatform));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsSupervisor, Permission.ApprovePlatformAutomations));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.ClaimsOfficer, Permission.ApprovePlatformAutomations));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.ClaimsOfficer, Permission.ManagePlatform));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsOfficer, Permission.ViewPlatform));
    }

    [Fact]
    public void NoTemplateTextUsesAnEmDash()
    {
        var text = JsonSerializer.Serialize(new { PlatformTemplates.Workspaces, PlatformTemplates.Automations, PlatformTemplates.Connectors });
        Assert.DoesNotContain((char)0x2014, text);
        Assert.DoesNotContain("\\u2014", text);
    }

    private static ApprovalRequest Approval(Guid caseId) => new()
    {
        ApprovalId = Guid.NewGuid(),
        Scope = Scope,
        CaseId = caseId,
        ApprovalType = "settlement",
        Status = ApprovalStatus.Pending,
        RiskLevel = "Medium",
        EvidenceSummary = "Assessment attached.",
        SourceReferences = [],
        RequestedByUserId = "officer-1",
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-30),
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-30),
        CorrelationId = "test",
    };

    // ---------------------------------------------------------------- fakes

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public void Advance(TimeSpan by) => now += by;
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedContext(TenantContext context) : ITenantContextAccessor
    {
        public TenantContext Current => context;
        public bool HasCurrent => true;
    }

    private sealed class RolePermissions(PlatformRole role) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(RolePermissionMap.HasPermission(role, permission));

        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            RolePermissionMap.HasPermission(role, permission) ? Task.CompletedTask : throw new PermissionDeniedException(permission);
    }

    private sealed class RecordingAudit : IAuditRepository
    {
        private readonly List<AuditEvent> events = [];

        public RecordingAudit(out List<AuditEvent> list) => list = events;

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(events.Where(e => e.CaseId == caseId).ToArray());

        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(events.ToArray());
    }

    private sealed class FakeCases : ICaseRepository
    {
        public List<ClaimCase> Items { get; } = [];
        public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(c => c.CaseId == caseId));
        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ClaimCase>>(Items.Where(c => c.Scope.Matches(scope)).ToArray());
    }

    private sealed class FakePolicies : IPolicyRepository
    {
        public List<Policy> Items { get; } = [];
        public Task<Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(p => p.PolicyId == policyId));
        public Task<Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(p => p.PolicyNumber == policyNumber));
        public Task<IReadOnlyCollection<Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Policy>>(Items.ToArray());
        public Task<IReadOnlyCollection<Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Policy>>([]);
        public Task UpsertAsync(Policy policy, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeApprovals : IApprovalRepository
    {
        public List<ApprovalRequest> Items { get; } = [];
        public Task SaveAsync(ApprovalRequest approval, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ApprovalRequest?> GetAsync(Guid approvalId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(a => a.ApprovalId == approvalId));
        public Task<IReadOnlyCollection<ApprovalRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ApprovalRequest>>(Items.ToArray());
        public Task<IReadOnlyCollection<ApprovalRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ApprovalRequest>>(Items.Where(a => a.CaseId == caseId).ToArray());
    }
}

using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Platform;

namespace InsuranceIntelligence.Application.Platform;

public sealed record PlatformWorkspaceDto(
    Guid Id,
    string TemplateKey,
    string TemplateName,
    string Name,
    string Department,
    string? Purpose,
    string Status,
    string? Route,
    bool IsPrimary,
    IReadOnlyList<string> Kpis,
    IReadOnlyList<PlatformConnectorStateDto> Connectors,
    IReadOnlyList<string> Members,
    string? OwnerUserId,
    int Automations,
    int OpenInboxItems,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlatformConnectorStateDto(string Key, string Name, string Status);

public sealed record PlatformWorkspaceRequest(
    string? TemplateKey,
    string? Name,
    string? Department,
    string? Purpose,
    IReadOnlyList<string>? Kpis,
    IReadOnlyList<string>? Connectors,
    IReadOnlyList<string>? Members,
    string? OwnerUserId,
    IReadOnlyList<string>? Automations);

public sealed record PlatformMeDto(
    string UserId,
    string Name,
    string Email,
    string Role,
    bool IsOrgAdmin,
    bool CanApproveAutomations,
    string AccessMode,
    string OrganisationName,
    Guid PrimaryWorkspaceId);

public interface IPlatformWorkspaceService
{
    Task<PlatformMeDto> MeAsync(CancellationToken cancellationToken);
    Task<OrgWorkspace> EnsurePrimaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformWorkspaceDto>> ListAsync(CancellationToken cancellationToken);
    Task<PlatformWorkspaceDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PlatformResult<PlatformWorkspaceDto>> CreateAsync(PlatformWorkspaceRequest request, CancellationToken cancellationToken);
    Task<PlatformResult<PlatformWorkspaceDto>> UpdateAsync(Guid id, PlatformWorkspaceRequest request, CancellationToken cancellationToken);
    Task<string> AccessModeAsync(CancellationToken cancellationToken);
    Task<PlatformResult<string>> SetAccessModeAsync(string mode, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> VisibleWorkspaceIdsAsync(CancellationToken cancellationToken);
}

public sealed class PlatformWorkspaceService(
    IOrgWorkspaceStore workspaceStore,
    IPlatformAutomationStore automationStore,
    IPlatformInboxStore inboxStore,
    IPlatformSettingsStore settingsStore,
    IUserAccountRepository userAccountRepository,
    ITenantConfigurationRepository tenantConfigurationRepository,
    PlatformAccess access,
    PlatformAudit audit) : IPlatformWorkspaceService
{
    private static readonly string[] OpenStatuses = [PlatformInboxStatuses.NeedsAttention, PlatformInboxStatuses.Recommendation, PlatformInboxStatuses.Acknowledged];

    public async Task<PlatformMeDto> MeAsync(CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var primary = await EnsurePrimaryAsync(cancellationToken);
        var scope = access.Scope;
        var user = await userAccountRepository.GetAsync(access.UserId, scope, cancellationToken);
        var configuration = await tenantConfigurationRepository.GetAsync(scope, cancellationToken);
        return new PlatformMeDto(
            access.UserId,
            user?.Name ?? "",
            user?.Email ?? "",
            user?.Role.ToString() ?? "",
            await access.IsOrgAdminAsync(cancellationToken),
            await access.CanApproveAsync(cancellationToken),
            await AccessModeAsync(cancellationToken),
            configuration.InsurerName,
            primary.Id);
    }

    /// <summary>Every organisation has its Insurance Intelligence workspace: the existing IIP portal.</summary>
    public async Task<OrgWorkspace> EnsurePrimaryAsync(CancellationToken cancellationToken)
    {
        var scope = access.Scope;
        var existing = (await workspaceStore.ListAsync(scope, 200, cancellationToken)).FirstOrDefault(w => w.IsPrimary);
        if (existing is not null) return existing;

        var template = PlatformTemplates.Workspace(PlatformTemplates.InsuranceIntelligence)!;
        var workspace = new OrgWorkspace
        {
            Id = Guid.NewGuid(),
            TemplateKey = template.Key,
            Name = template.Name,
            Department = template.Department,
            Purpose = template.Purpose,
            Status = OrgWorkspaceStatuses.Live,
            Route = "/app/workspace",
            IsPrimary = true,
            Kpis = [.. template.Kpis],
            Connectors = [.. template.Connectors],
            CreatedAt = access.Now,
            UpdatedAt = access.Now
        };
        if (await workspaceStore.TryCreatePrimaryAsync(scope, workspace, cancellationToken)) return workspace;

        // Another request created it first.
        return (await workspaceStore.ListAsync(scope, 200, cancellationToken)).First(w => w.IsPrimary);
    }

    public async Task<IReadOnlyList<PlatformWorkspaceDto>> ListAsync(CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        await EnsurePrimaryAsync(cancellationToken);
        var visible = (await VisibleWorkspaceIdsAsync(cancellationToken)).ToHashSet();
        var rows = (await workspaceStore.ListAsync(access.Scope, 500, cancellationToken))
            .Where(w => w.Status != OrgWorkspaceStatuses.Archived && visible.Contains(w.Id))
            .ToList();
        var counts = await CountsAsync(cancellationToken);
        return rows
            .OrderByDescending(w => w.IsPrimary)
            .ThenBy(w => w.Department, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .Select(w => ToDto(w, counts))
            .ToList();
    }

    public async Task<PlatformWorkspaceDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        var visible = await VisibleWorkspaceIdsAsync(cancellationToken);
        if (!visible.Contains(id)) return null;
        var row = await workspaceStore.GetAsync(access.Scope, id, cancellationToken);
        return row is null ? null : ToDto(row, await CountsAsync(cancellationToken));
    }

    public async Task<PlatformResult<PlatformWorkspaceDto>> CreateAsync(PlatformWorkspaceRequest request, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        if (!await access.IsOrgAdminAsync(cancellationToken)) return PlatformResult<PlatformWorkspaceDto>.Fail("Only organisation administrators can create workspaces.", 403);
        var template = PlatformTemplates.Workspace(request.TemplateKey);
        if (template is null) return PlatformResult<PlatformWorkspaceDto>.Fail("Choose a workspace template.");
        if (template.IsLiveTemplate) return PlatformResult<PlatformWorkspaceDto>.Fail("The Insurance Intelligence workspace already exists for this organisation.");
        var name = (request.Name ?? "").Trim();
        if (name.Length < 2) return PlatformResult<PlatformWorkspaceDto>.Fail("Give the workspace a name.");

        var scope = access.Scope;
        var workspace = new OrgWorkspace
        {
            Id = Guid.NewGuid(),
            TemplateKey = template.Key,
            Name = PlatformAccess.Clip(name, 160),
            Department = PlatformAccess.Clip(string.IsNullOrWhiteSpace(request.Department) ? template.Department : request.Department.Trim(), 96),
            Purpose = PlatformAccess.Clip(string.IsNullOrWhiteSpace(request.Purpose) ? template.Purpose : request.Purpose.Trim(), 1000),
            Status = OrgWorkspaceStatuses.AwaitingData,
            Kpis = CleanList(request.Kpis) ?? [.. template.Kpis],
            Connectors = CleanConnectors(request.Connectors) ?? [.. template.Connectors],
            Members = CleanList(request.Members) ?? [],
            OwnerUserId = string.IsNullOrWhiteSpace(request.OwnerUserId) ? access.UserId : request.OwnerUserId.Trim(),
            CreatedByUserId = access.UserId,
            CreatedAt = access.Now,
            UpdatedAt = access.Now
        };
        await workspaceStore.SaveAsync(scope, workspace, cancellationToken);

        // Suggested automations start as drafts: nothing runs until it is tested and approved.
        var drafts = new List<PlatformAutomation>();
        foreach (var key in (request.Automations ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var automationTemplate = PlatformTemplates.Automation(key);
            if (automationTemplate is null) continue;
            drafts.Add(PlatformAutomationService.NewFromTemplate(automationTemplate, workspace.Id, access));
        }

        if (drafts.Count > 0) await automationStore.SaveManyAsync(scope, drafts, cancellationToken);
        await audit.LogAsync("platform.workspace.created", workspace.Id, null, workspace.Status, cancellationToken);
        return PlatformResult<PlatformWorkspaceDto>.Success(ToDto(workspace, await CountsAsync(cancellationToken)));
    }

    public async Task<PlatformResult<PlatformWorkspaceDto>> UpdateAsync(Guid id, PlatformWorkspaceRequest request, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        if (!await access.IsOrgAdminAsync(cancellationToken)) return PlatformResult<PlatformWorkspaceDto>.Fail("Only organisation administrators can change workspaces.", 403);
        var workspace = await workspaceStore.GetAsync(access.Scope, id, cancellationToken);
        if (workspace is null) return PlatformResult<PlatformWorkspaceDto>.Fail("Workspace not found.", 404);

        if (!string.IsNullOrWhiteSpace(request.Name)) workspace.Name = PlatformAccess.Clip(request.Name.Trim(), 160);
        if (!string.IsNullOrWhiteSpace(request.Department)) workspace.Department = PlatformAccess.Clip(request.Department.Trim(), 96);
        if (request.Purpose is not null) workspace.Purpose = PlatformAccess.Clip(request.Purpose.Trim(), 1000);
        if (CleanList(request.Kpis) is { } kpis) workspace.Kpis = kpis;
        if (CleanConnectors(request.Connectors) is { } connectors) workspace.Connectors = connectors;
        if (CleanList(request.Members) is { } members) workspace.Members = members;
        if (!string.IsNullOrWhiteSpace(request.OwnerUserId)) workspace.OwnerUserId = request.OwnerUserId.Trim();
        workspace.UpdatedAt = access.Now;
        await workspaceStore.SaveAsync(access.Scope, workspace, cancellationToken);
        await audit.LogAsync("platform.workspace.updated", workspace.Id, workspace.Status, workspace.Status, cancellationToken);
        return PlatformResult<PlatformWorkspaceDto>.Success(ToDto(workspace, await CountsAsync(cancellationToken)));
    }

    public async Task<string> AccessModeAsync(CancellationToken cancellationToken) =>
        (await settingsStore.ListAsync(access.Scope, 1, cancellationToken)).FirstOrDefault()?.AccessMode ?? "open";

    public async Task<PlatformResult<string>> SetAccessModeAsync(string mode, CancellationToken cancellationToken)
    {
        await access.RequireViewAsync(cancellationToken);
        if (!await access.IsOrgAdminAsync(cancellationToken)) return PlatformResult<string>.Fail("Only organisation administrators can change access.", 403);
        if (mode is not ("open" or "strict")) return PlatformResult<string>.Fail("Access mode must be open or strict.");
        var scope = access.Scope;
        var settings = (await settingsStore.ListAsync(scope, 1, cancellationToken)).FirstOrDefault() ?? new PlatformSettings { Id = Guid.NewGuid() };
        var before = settings.AccessMode;
        settings.AccessMode = mode;
        settings.UpdatedByUserId = access.UserId;
        settings.UpdatedAt = access.Now;
        await settingsStore.SaveAsync(scope, settings, cancellationToken);
        await audit.LogAsync("platform.access_mode.changed", settings.Id, before, mode, cancellationToken);
        return PlatformResult<string>.Success(mode);
    }

    /// <summary>Open mode: every member sees every workspace. Strict: administrators, owners and listed members only.
    /// The primary Insurance Intelligence workspace stays governed by IIP's existing permissions.</summary>
    public async Task<IReadOnlyList<Guid>> VisibleWorkspaceIdsAsync(CancellationToken cancellationToken)
    {
        var rows = await workspaceStore.ListAsync(access.Scope, 500, cancellationToken);
        if (await access.IsOrgAdminAsync(cancellationToken) || await AccessModeAsync(cancellationToken) == "open") return rows.Select(r => r.Id).ToList();
        var user = access.UserId;
        return rows
            .Where(r => r.IsPrimary
                        || string.Equals(r.OwnerUserId, user, StringComparison.OrdinalIgnoreCase)
                        || r.Members.Contains(user, StringComparer.OrdinalIgnoreCase))
            .Select(r => r.Id)
            .ToList();
    }

    private async Task<(Dictionary<Guid, int> Automations, Dictionary<Guid, int> Inbox)> CountsAsync(CancellationToken cancellationToken)
    {
        var scope = access.Scope;
        var automations = (await automationStore.ListAsync(scope, 1000, cancellationToken))
            .Where(a => a.Status != PlatformAutomationStatuses.Retired)
            .GroupBy(a => a.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Count());
        var inbox = (await inboxStore.ListAsync(scope, 1000, cancellationToken))
            .Where(i => i.WorkspaceId is not null && OpenStatuses.Contains(i.Status))
            .GroupBy(i => i.WorkspaceId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        return (automations, inbox);
    }

    private static PlatformWorkspaceDto ToDto(OrgWorkspace w, (Dictionary<Guid, int> Automations, Dictionary<Guid, int> Inbox) counts)
    {
        var template = PlatformTemplates.Workspace(w.TemplateKey);
        var connectors = w.Connectors
            .Select(key => PlatformTemplates.Connectors.FirstOrDefault(c => c.Key == key) is { } c
                ? new PlatformConnectorStateDto(c.Key, c.Name, w.IsPrimary && c.Key is "iip_core" or "channels" or "knowledge" ? "connected" : "not_connected")
                : new PlatformConnectorStateDto(key, key, "not_connected"))
            .ToList();
        return new PlatformWorkspaceDto(
            w.Id,
            w.TemplateKey,
            template?.Name ?? w.TemplateKey,
            w.Name,
            w.Department,
            w.Purpose,
            w.Status,
            w.Route,
            w.IsPrimary,
            w.Kpis,
            connectors,
            w.Members,
            w.OwnerUserId,
            counts.Automations.GetValueOrDefault(w.Id),
            counts.Inbox.GetValueOrDefault(w.Id),
            w.CreatedAt,
            w.UpdatedAt);
    }

    private static List<string>? CleanList(IReadOnlyList<string>? values) =>
        values is null ? null : values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => PlatformAccess.Clip(v.Trim(), 120)).Distinct().Take(20).ToList();

    private static List<string>? CleanConnectors(IReadOnlyList<string>? values) =>
        values is null ? null : values.Where(v => PlatformTemplates.Connectors.Any(c => c.Key == v)).Distinct().ToList();
}

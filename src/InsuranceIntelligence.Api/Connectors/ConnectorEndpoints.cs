using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.Connectors;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Connectors;

/// <summary>
/// Admin surface for folder connectors (<c>/api/v1/connectors</c>). A connector reads with its owner's own sign-in, so
/// creating one needs the owner to have connected their account first, and what it can reach never exceeds what the
/// owner can open. Everything it brings in is a pending Knowledge document. Each action is audited; the audit holds
/// counts and names, never file contents.
/// </summary>
public static class ConnectorEndpoints
{
    public const int MaxFolders = 20;
    public static readonly TimeSpan SyncNowCooldown = TimeSpan.FromMinutes(1);

    public sealed record FolderInput(string? FolderId, string? DisplayName, string? DriveId, string? ProductLine, string? DocumentType, bool IsFile, string? Purpose = null);

    public sealed record CreateRequest(string? Provider, IReadOnlyList<FolderInput>? Folders);

    public sealed record FoldersRequest(IReadOnlyList<FolderInput>? Folders);

    public static async Task<IResult> List(
        IDataConnectorRepository connectors, IConnectorItemRepository items, IConnectorRunRepository runs,
        ITenantContextSetter setter, ITenantContextAccessor tenant, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        var scope = tenant.Current.Scope;
        var result = new List<object>();
        foreach (var connector in await connectors.ListAsync(scope, ct))
        {
            var all = await items.ListAsync(scope, connector.ConnectorId, ct);
            var recent = await runs.ListAsync(scope, connector.ConnectorId, 5, ct);
            result.Add(Summary(connector, all, recent));
        }

        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { connectors = result });
    }

    public static async Task<IResult> Create(
        CreateRequest request, IDataConnectorRepository connectors, IFileAccessTokenProvider tokens, IUserFileService files, IAuditWriter audit,
        ITenantContextSetter setter, ITenantContextAccessor tenant, TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        if (!TryProvider(request.Provider, out var provider)) return Invalid("provider", "Choose Google Drive or Microsoft.");
        if (!TryFolders(provider, request.Folders, out var folders, out var folderError)) return Invalid("folders", folderError);

        var context = tenant.Current;
        try
        {
            await tokens.GetAccessTokenAsync(context.Scope, context.UserId, provider, ct);
        }
        catch (FileConnectionException)
        {
            return Results.Json(new { code = "not_connected", message = $"Connect your own {ProviderName(provider)} account first (Settings, Integrations). A connector reads with your sign-in." }, statusCode: StatusCodes.Status409Conflict);
        }

        string? account = null;
        try
        {
            account = (await files.StatusAsync(ct)).FirstOrDefault(s => string.Equals(s.Provider, provider == FileProvider.Google ? "google" : "microsoft", StringComparison.OrdinalIgnoreCase))?.AccountEmail;
        }
        catch (FileConnectionException)
        {
            // The account name is only for display.
        }

        var connector = new DataConnector
        {
            ConnectorId = Guid.NewGuid(), Scope = context.Scope, Provider = provider, OwnerUserId = context.UserId, AccountEmail = account,
            Folders = folders, CreatedBy = context.UserId, CreatedAt = clock.GetUtcNow(),
        };
        await connectors.SaveAsync(connector, ct);
        await Audit(audit, context, "connector.created", $"{ProviderName(provider)} connector created with {folders.Count} {(provider == FileProvider.Google ? "file" : "folder")}(s): {Names(folders)}.", http, clock);
        return Results.Created($"/api/v1/connectors/{connector.ConnectorId}", Summary(connector, [], []));
    }

    public static async Task<IResult> ReplaceFolders(
        Guid connectorId, FoldersRequest request, IDataConnectorRepository connectors, IConnectorSyncService sync, IAuditWriter audit,
        ITenantContextSetter setter, ITenantContextAccessor tenant, TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        var context = tenant.Current;
        var connector = await connectors.GetAsync(context.Scope, connectorId, ct);
        if (connector is null) return Results.NotFound(new { message = "That connector was not found." });
        if (!TryFolders(connector.Provider, request.Folders, out var folders, out var folderError)) return Invalid("folders", folderError);

        var removedIds = connector.Folders.Select(f => f.FolderId).Except(folders.Select(f => f.FolderId), StringComparer.Ordinal).ToArray();
        await connectors.SaveAsync(connector with { Folders = folders }, ct);
        // A folder taken off the connector stops feeding retrieval at once, not at the next run.
        var retired = removedIds.Length == 0 ? 0 : await sync.RetireFoldersAsync(connector, removedIds, "The folder was removed from the connector.", http.TraceIdentifier, ct);
        await Audit(audit, context, "connector.folders_changed", $"Folders now: {Names(folders)}. {removedIds.Length} removed, {retired} file(s) taken out of use.", http, clock);
        return Results.Ok(new { retired, folders = folders.Select(FolderView) });
    }

    public static async Task<IResult> SyncNow(
        Guid connectorId, IDataConnectorRepository connectors, IConnectorSyncService sync, ITenantContextSetter setter, ITenantContextAccessor tenant,
        TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        var connector = await connectors.GetAsync(tenant.Current.Scope, connectorId, ct);
        if (connector is null) return Results.NotFound(new { message = "That connector was not found." });
        if (connector.LastRunAt is { } last && clock.GetUtcNow() - last < SyncNowCooldown)
        {
            return Results.Json(new { message = "It was synced a moment ago. Try again in a minute." }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        var run = await sync.RunAsync(connector, http.TraceIdentifier, ct);
        return Results.Ok(RunView(run));
    }

    public static Task<IResult> Pause(Guid connectorId, IDataConnectorRepository connectors, IAuditWriter audit, ITenantContextSetter setter, ITenantContextAccessor tenant, TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        SetStatus(connectorId, ConnectorStatus.Paused, "connector.paused", connectors, audit, setter, tenant, clock, options, http, ct);

    public static Task<IResult> Resume(Guid connectorId, IDataConnectorRepository connectors, IAuditWriter audit, ITenantContextSetter setter, ITenantContextAccessor tenant, TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct) =>
        SetStatus(connectorId, ConnectorStatus.Active, "connector.resumed", connectors, audit, setter, tenant, clock, options, http, ct);

    public static async Task<IResult> Delete(
        Guid connectorId, IDataConnectorRepository connectors, IConnectorItemRepository items, IConnectorSyncService sync, IAuditWriter audit,
        ITenantContextSetter setter, ITenantContextAccessor tenant, TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        var context = tenant.Current;
        var connector = await connectors.GetAsync(context.Scope, connectorId, ct);
        if (connector is null) return Results.NotFound(new { message = "That connector was not found." });

        var retired = await sync.RetireFoldersAsync(connector, connector.Folders.Select(f => f.FolderId).ToArray(), "The connector was deleted.", http.TraceIdentifier, ct);
        await items.DeleteForConnectorAsync(context.Scope, connectorId, ct);
        await connectors.DeleteAsync(context.Scope, connectorId, ct);
        await Audit(audit, context, "connector.deleted", $"{ProviderName(connector.Provider)} connector deleted. {retired} file(s) taken out of use.", http, clock);
        return Results.Ok(new { retired });
    }

    public static async Task<IResult> Items(
        Guid connectorId, string? state, IDataConnectorRepository connectors, IConnectorItemRepository items,
        ITenantContextSetter setter, ITenantContextAccessor tenant, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        var scope = tenant.Current.Scope;
        if (await connectors.GetAsync(scope, connectorId, ct) is null) return Results.NotFound(new { message = "That connector was not found." });
        var all = await items.ListAsync(scope, connectorId, ct);
        var shown = Enum.TryParse<ConnectorItemState>(state, true, out var wanted) ? all.Where(i => i.State == wanted) : all;
        return Results.Ok(new
        {
            items = shown.OrderByDescending(i => i.LastSeenAt).Take(500).Select(i => new
            {
                i.ExternalId, name = i.FileName, folderId = i.FolderId, state = i.State.ToString(), reason = i.SkipReason, i.ModifiedAt, lastSeenAt = i.LastSeenAt, knowledgeDocumentId = i.KnowledgeDocumentId,
            }),
        });
    }

    private static async Task<IResult> SetStatus(
        Guid connectorId, ConnectorStatus status, string action, IDataConnectorRepository connectors, IAuditWriter audit,
        ITenantContextSetter setter, ITenantContextAccessor tenant, TimeProvider clock, InfrastructureOptions options, HttpContext http, CancellationToken ct)
    {
        if (Guard(http, options, setter, out var failure)) return failure;
        var context = tenant.Current;
        var connector = await connectors.GetAsync(context.Scope, connectorId, ct);
        if (connector is null) return Results.NotFound(new { message = "That connector was not found." });
        await connectors.SaveAsync(connector with { Status = status }, ct);
        await Audit(audit, context, action, $"{ProviderName(connector.Provider)} connector {(status == ConnectorStatus.Paused ? "paused" : "resumed")}.", http, clock);
        return Results.Ok(new { status = status.ToString() });
    }

    private static bool Guard(HttpContext http, InfrastructureOptions options, ITenantContextSetter setter, out IResult failure)
    {
        if (DemoAuthorization.Require(http, options, Permission.ManageKnowledgeBase) is { } forbidden)
        {
            failure = forbidden;
            return true;
        }

        if (!CommandCenterQueryEndpoints.TrySetTenantContext(http, setter, options, out var error))
        {
            failure = error;
            return true;
        }

        failure = Results.Empty;
        return false;
    }

    private static bool TryProvider(string? value, out FileProvider provider)
    {
        provider = FileProvider.Google;
        if (string.Equals(value, "google", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value, "microsoft", StringComparison.OrdinalIgnoreCase)) { provider = FileProvider.Microsoft; return true; }
        return false;
    }

    private static bool TryFolders(FileProvider provider, IReadOnlyList<FolderInput>? input, out IReadOnlyList<ConnectorFolder> folders, out string error)
    {
        folders = [];
        error = string.Empty;
        if (input is null || input.Count == 0) { error = provider == FileProvider.Google ? "Pick at least one file." : "Choose at least one folder."; return false; }
        if (input.Count > MaxFolders) { error = $"A connector can hold up to {MaxFolders}. Make another connector for more."; return false; }

        var list = new List<ConnectorFolder>();
        foreach (var entry in input)
        {
            var id = entry.FolderId?.Trim() ?? string.Empty;
            var name = entry.DisplayName?.Trim() ?? string.Empty;
            var line = entry.ProductLine?.Trim() ?? string.Empty;
            if (id.Length is 0 or > 300) { error = "Every entry needs an id from the provider."; return false; }
            if (name.Length is 0 or > 200) { error = "Every entry needs a name."; return false; }
            if (line.Length is 0 or > 80) { error = "Say which product line each entry's files belong to, so they are filed correctly."; return false; }
            if (!Enum.TryParse<KnowledgeDocumentType>(entry.DocumentType, true, out var type)) { error = "Say what kind of document each entry holds."; return false; }
            if (provider == FileProvider.Google && !entry.IsFile) { error = "Google Drive files are picked one at a time. Pick files, not a folder."; return false; }
            if (provider == FileProvider.Microsoft && entry.IsFile) { error = "OneDrive and SharePoint are synced by folder. Choose a folder."; return false; }
            if (list.Any(f => f.FolderId == id)) { error = "The same entry is listed twice."; return false; }
            var purpose = ConnectorFolderPurpose.Knowledge;
            if (!string.IsNullOrWhiteSpace(entry.Purpose) && !Enum.TryParse(entry.Purpose, true, out purpose)) { error = "A folder is for knowledge or for bank events."; return false; }
            list.Add(new ConnectorFolder(id, name, string.IsNullOrWhiteSpace(entry.DriveId) ? null : entry.DriveId.Trim(), line, type, entry.IsFile, purpose));
        }

        folders = list;
        return true;
    }

    private static IResult Invalid(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static string ProviderName(FileProvider provider) => provider == FileProvider.Google ? "Google Drive" : "OneDrive and SharePoint";

    private static string Names(IReadOnlyList<ConnectorFolder> folders) => string.Join(", ", folders.Select(f => f.DisplayName));

    private static async Task Audit(IAuditWriter audit, TenantContext context, string action, string reason, HttpContext http, TimeProvider clock) =>
        await audit.WriteAsync(
            new AuditEvent(Guid.NewGuid(), context.Scope, null, null, context.UserId, ActorType.User, action, clock.GetUtcNow(), null, null, [], "Connectors", null, "recorded", null, reason, http.TraceIdentifier),
            http.RequestAborted);

    private static object FolderView(ConnectorFolder f) => new { folderId = f.FolderId, name = f.DisplayName, driveId = f.DriveId, productLine = f.ProductLine, documentType = f.DocumentType.ToString(), isFile = f.IsFile, purpose = f.Purpose.ToString() };

    private static object RunView(ConnectorRun r) => new
    {
        startedAt = r.StartedAt, finishedAt = r.FinishedAt, outcome = r.Outcome?.ToString(), found = r.Found, added = r.Added, updated = r.Updated, removed = r.Removed, skipped = r.Skipped, failed = r.Failed, message = r.SafeError,
    };

    private static object Summary(DataConnector c, IReadOnlyList<ConnectorItem> items, IReadOnlyList<ConnectorRun> runs) => new
    {
        id = c.ConnectorId,
        provider = c.Provider == FileProvider.Google ? "google" : "microsoft",
        providerName = ProviderName(c.Provider),
        owner = c.OwnerUserId,
        account = c.AccountEmail,
        status = c.Status.ToString(),
        statusLabel = c.Status switch { ConnectorStatus.Active => "Active", ConnectorStatus.Paused => "Paused", ConnectorStatus.NeedsReconnect => "Needs attention", _ => "Needs attention" },
        statusNote = c.Status switch
        {
            ConnectorStatus.NeedsReconnect => "The sign-in this connector reads with has expired or was removed. Nothing was changed. The person who set it up needs to connect their account again.",
            ConnectorStatus.Error => "The last run could not finish. It will be tried again.",
            _ => null,
        },
        folders = c.Folders.Select(FolderView),
        lastRunAt = c.LastRunAt,
        lastOutcome = c.LastRunOutcome?.ToString(),
        counts = new
        {
            pending = items.Count(i => i.State == ConnectorItemState.Pending),
            skipped = items.Count(i => i.State == ConnectorItemState.Skipped),
            failed = items.Count(i => i.State == ConnectorItemState.Failed),
            removed = items.Count(i => i.State == ConnectorItemState.Removed),
        },
        recentRuns = runs.Select(RunView),
    };
}

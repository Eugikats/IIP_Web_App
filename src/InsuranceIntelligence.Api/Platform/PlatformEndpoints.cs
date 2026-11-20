using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Platform;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Api.Platform;

public sealed record PlatformInboxActionRequest(string? Note);

public sealed record PlatformAccessModeRequest(string? Mode);

/// <summary>
/// The organisation platform above the Insurance Intelligence workspace: department workspaces built from templates,
/// governed automations, the Intelligence Inbox, intents, and the organisation-wide reports and conversations lists.
/// Viewing needs <see cref="Permission.ViewPlatform"/>. Structure changes and approvals are checked again inside the
/// services against <see cref="Permission.ManagePlatform"/> and <see cref="Permission.ApprovePlatformAutomations"/>, from the
/// signed-in person's roles on the server.
/// </summary>
public static class PlatformEndpoints
{
    public const string Base = "/api/v1/org-platform";

    public static void Map(WebApplication app)
    {
        var group = app.MapGroup(Base)
            .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewPlatform))
            // No resolved organisation and person (no header scope in development, no membership when deployed) is a
            // refusal, never a server error: the services below all read the signed-in scope.
            .AddEndpointFilter(async (context, next) =>
                context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>().HasCurrent
                    ? await next(context)
                    : Results.Problem("No organisation is selected for this request.", statusCode: StatusCodes.Status403Forbidden));

        group.MapGet("/me", async (IPlatformWorkspaceService workspaces, CancellationToken token) =>
            Results.Ok(await workspaces.MeAsync(token)));

        group.MapGet("/templates", () => Results.Ok(new
        {
            workspaces = PlatformTemplates.Workspaces,
            automations = PlatformTemplates.Automations,
            connectors = PlatformTemplates.Connectors
        }));

        // ---------------------------------------------------------------- workspaces

        group.MapGet("/workspaces", async (IPlatformWorkspaceService workspaces, CancellationToken token) =>
            Results.Ok(await workspaces.ListAsync(token)));

        group.MapGet("/workspaces/{id:guid}", async (Guid id, IPlatformWorkspaceService workspaces, CancellationToken token) =>
            await workspaces.GetAsync(id, token) is { } workspace ? Results.Ok(workspace) : Results.Problem("Workspace not found.", statusCode: StatusCodes.Status404NotFound));

        group.MapPost("/workspaces", async (PlatformWorkspaceRequest request, IPlatformWorkspaceService workspaces, CancellationToken token) =>
            Respond(await workspaces.CreateAsync(request, token)));

        group.MapPatch("/workspaces/{id:guid}", async (Guid id, PlatformWorkspaceRequest request, IPlatformWorkspaceService workspaces, CancellationToken token) =>
            Respond(await workspaces.UpdateAsync(id, request, token)));

        // ---------------------------------------------------------------- automations

        group.MapGet("/automations", async (Guid? workspaceId, IPlatformAutomationService automations, CancellationToken token) =>
            Results.Ok(await automations.ListAsync(workspaceId, token)));

        group.MapGet("/automations/{id:guid}", async (Guid id, IPlatformAutomationService automations, CancellationToken token) =>
            await automations.GetAsync(id, token) is { } automation
                ? Results.Ok(new { automation, runs = await automations.RunsAsync(id, token) })
                : Results.Problem("Automation not found.", statusCode: StatusCodes.Status404NotFound));

        group.MapPost("/automations", async (PlatformAutomationRequest request, IPlatformAutomationService automations, CancellationToken token) =>
            Respond(await automations.CreateAsync(request, token)));

        group.MapPatch("/automations/{id:guid}", async (Guid id, PlatformAutomationRequest request, IPlatformAutomationService automations, CancellationToken token) =>
            Respond(await automations.UpdateAsync(id, request, token)));

        group.MapPost("/automations/{id:guid}/{action}", async (Guid id, string action, IPlatformAutomationService automations, CancellationToken token) =>
            Respond(await automations.ActAsync(id, action, token)));

        // ---------------------------------------------------------------- inbox, intents, notifications

        group.MapGet("/inbox", async (string? status, Guid? workspaceId, IPlatformInboxService inbox, CancellationToken token) =>
            Results.Ok(await inbox.ListAsync(string.IsNullOrWhiteSpace(status) ? null : status, workspaceId, token)));

        group.MapGet("/inbox/counts", async (Guid? workspaceId, IPlatformInboxService inbox, CancellationToken token) =>
            Results.Ok(await inbox.CountsAsync(workspaceId, token)));

        group.MapPost("/inbox/{id}/{action}", async (string id, string action, PlatformInboxActionRequest? request, IPlatformInboxService inbox, CancellationToken token) =>
            Respond(await inbox.ActAsync(id, action, request?.Note, token)));

        group.MapGet("/intents", async (IPlatformInboxService inbox, CancellationToken token) =>
            Results.Ok(await inbox.IntentsAsync(token)));

        group.MapPost("/intents", async (PlatformIntentRequest request, IPlatformInboxService inbox, CancellationToken token) =>
            Respond(await inbox.SaveIntentAsync(null, request, token)));

        group.MapPatch("/intents/{id:guid}", async (Guid id, PlatformIntentRequest request, IPlatformInboxService inbox, CancellationToken token) =>
            Respond(await inbox.SaveIntentAsync(id, request, token)));

        group.MapPost("/intents/{id:guid}/{action}", async (Guid id, string action, IPlatformInboxService inbox, CancellationToken token) =>
            Respond(await inbox.IntentActionAsync(id, action, token)));

        // Bell count, inbox cards and the intents strip; pass the workspace to scope them to one workspace.
        group.MapGet("/notifications", async (Guid? workspaceId, bool? primary, IPlatformWorkspaceService workspaces, IPlatformOverviewService overview, CancellationToken token) =>
        {
            if (primary == true) workspaceId = (await workspaces.EnsurePrimaryAsync(token)).Id;
            return Results.Ok(await overview.NotificationsAsync(workspaceId, token));
        });

        group.MapGet("/insights", async (IPlatformOverviewService overview, CancellationToken token) =>
            Results.Ok(await overview.InsightsAsync(token)));

        group.MapGet("/reports", async (IPlatformOverviewService overview, CancellationToken token) =>
            Results.Ok(await overview.ReportsAsync(token)));

        group.MapGet("/conversations", async (IPlatformOverviewService overview, CancellationToken token) =>
            Results.Ok(await overview.ConversationsAsync(token)));

        group.MapPut("/settings/access", async (PlatformAccessModeRequest request, IPlatformWorkspaceService workspaces, CancellationToken token) =>
            Respond(await workspaces.SetAccessModeAsync(request.Mode ?? "", token)));
    }

    private static IResult Respond<T>(PlatformResult<T> result) =>
        result.Ok ? Results.Ok(result.Value) : Results.Problem(result.Error ?? "The request could not be completed.", statusCode: result.Status);
}

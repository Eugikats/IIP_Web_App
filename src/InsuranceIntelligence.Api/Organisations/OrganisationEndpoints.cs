using System.Security.Claims;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Organisations;

public sealed record CreateOrganisationRequest(string? Name, string? Type, string? Product = null);
public sealed record AdoptTenantRequest(string? Name, string? Type = null, string? Product = null);
public sealed record InviteRequest(string? Email, string? Role);
public sealed record AcceptInvitationRequest(string? Token);
public sealed record LinkRequest(string? OrganisationId);
public sealed record CloseRequest(string? ConfirmName);

/// <summary>
/// Sign-up, invitations, and broker/insurer links. Creating an organisation and accepting an
/// invitation are done by someone who is signed in but belongs to nothing yet, so they use the
/// signed-in identity only. Everything else acts inside the organisation the caller has chosen
/// (X-Organisation-Id), which the membership store has already confirmed they belong to.
/// </summary>
public static class OrganisationEndpoints
{
    public static Task<IResult> Create(
        CreateOrganisationRequest request, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (SignUpClosed(options) is { } closed)
            {
                return closed;
            }

            if (SignedInUser(httpContext, options) is not { } userId)
            {
                return Results.Unauthorized();
            }

            if (!Enum.TryParse<OrganisationType>(request.Type, ignoreCase: true, out var type))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Choose Insurer or Broker."] });
            }

            var product = OrganisationProduct.Insurance;
            if (!string.IsNullOrWhiteSpace(request.Product) && !Enum.TryParse(request.Product, ignoreCase: true, out product))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Choose Insurance or Bancassurance."] });
            }

            var organisation = await service.CreateAsync(userId, request.Name ?? string.Empty, type, CorrelationId(httpContext), cancellationToken, product);
            return Results.Created($"/api/v1/organisations/{organisation.OrganisationId}", Describe(organisation));
        });

    /// <summary>Registers the workspace the caller is signed in to as an organisation, keeping all its data in
    /// place. Used once, when a deployment that predates organisations switches to multi-tenant mode.</summary>
    public static Task<IResult> AdoptCurrentTenant(
        AdoptTenantRequest request, OrganisationService service, InsuranceIntelligence.Application.Core.ITenantContextAccessor tenantContext,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var type = OrganisationType.Insurer;
            if (!string.IsNullOrWhiteSpace(request.Type) && !Enum.TryParse(request.Type, ignoreCase: true, out type))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Choose Insurer or Broker."] });
            }

            var product = OrganisationProduct.Insurance;
            if (!string.IsNullOrWhiteSpace(request.Product) && !Enum.TryParse(request.Product, ignoreCase: true, out product))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Choose Insurance or Bancassurance."] });
            }

            var context = tenantContext.Current;
            var organisation = await service.AdoptExistingTenantAsync(
                context.Scope, context.UserId, request.Name ?? string.Empty, type, product, CorrelationId(httpContext), cancellationToken);
            return Results.Ok(Describe(organisation));
        });

    /// <summary>The organisations this person can act in: their own, and insurers their broker works with.</summary>
    public static Task<IResult> Mine(
        OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () => SignedInUser(httpContext, options) is { } userId
            ? Results.Ok(await service.ListMineAsync(userId, cancellationToken))
            : Results.Unauthorized());

    public static Task<IResult> Accept(
        AcceptInvitationRequest request, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (SignUpClosed(options) is { } closed)
            {
                return closed;
            }

            if (SignedInUser(httpContext, options) is not { } userId)
            {
                return Results.Unauthorized();
            }

            var organisation = await service.AcceptInvitationAsync(
                userId, ResolveEmail(httpContext, options), request.Token ?? string.Empty, CorrelationId(httpContext), cancellationToken);
            return Results.Ok(Describe(organisation));
        });

    public static Task<IResult> ListInvitations(
        OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, _) => Results.Ok(await service.ListInvitationsAsync(organisationId, cancellationToken)));

    public static Task<IResult> Invite(
        InviteRequest request, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
        {
            var created = await service.InviteAsync(organisationId, userId, request.Email ?? string.Empty, request.Role ?? string.Empty, CorrelationId(httpContext), cancellationToken);
            var baseUrl = options.ClientPortalBaseUrl?.TrimEnd('/');
            return Results.Ok(new
            {
                created.InvitationId,
                created.Email,
                created.RoleCode,
                created.ExpiresAt,
                // Shown once, here. Only a hash is kept, so it cannot be shown again.
                created.Token,
                joinLink = baseUrl is null ? null : $"{baseUrl}/join?token={created.Token}",
            });
        });

    public static Task<IResult> RevokeInvitation(
        Guid invitationId, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
        {
            await service.RevokeInvitationAsync(organisationId, userId, invitationId, CorrelationId(httpContext), cancellationToken);
            return Results.NoContent();
        });

    public static Task<IResult> ListLinks(
        OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, _) => Results.Ok(await service.ListLinksAsync(organisationId, cancellationToken)));

    public static Task<IResult> RequestLink(
        LinkRequest request, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
            Results.Ok(await service.RequestLinkAsync(organisationId, userId, request.OrganisationId ?? string.Empty, CorrelationId(httpContext), cancellationToken)));

    public static Task<IResult> Approve(
        Guid linkId, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
            Results.Ok(await service.DecideLinkAsync(organisationId, userId, linkId, approve: true, CorrelationId(httpContext), cancellationToken)));

    public static Task<IResult> Decline(
        Guid linkId, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
            Results.Ok(await service.DecideLinkAsync(organisationId, userId, linkId, approve: false, CorrelationId(httpContext), cancellationToken)));

    public static Task<IResult> Revoke(
        Guid linkId, OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
            Results.Ok(await service.RevokeLinkAsync(organisationId, userId, linkId, CorrelationId(httpContext), cancellationToken)));

    /// <summary>One view across every insurer this broker works with. Any broker-organisation member may open it.</summary>
    public static Task<IResult> BrokerDashboard(
        OrganisationService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (DemoAuthorization.Require(httpContext, options, Permission.CreateClaim) is { } forbidden)
            {
                return forbidden;
            }

            return WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out _)
                ? Results.Ok(await service.BrokerDashboardAsync(scope.TenantId, cancellationToken))
                : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

    public static Task<IResult> Onboarding(
        OrganisationOnboardingService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, _) => Results.Ok(await service.ChecklistAsync(organisationId, cancellationToken)));

    public static Task<IResult> EnableSandbox(
        OrganisationOnboardingService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
        {
            var scope = await service.EnableSandboxAsync(organisationId, userId, cancellationToken);
            return Results.Ok(new { organisationId = scope.TenantId, workspaceId = scope.WorkspaceId });
        });

    public static Task<IResult> Usage(
        InsuranceIntelligence.Domain.Usage.IUsageMeter meter, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, _) =>
            await meter.GetAsync(InsuranceIntelligence.Domain.Common.TenantScope.Create(organisationId, Organisation.DefaultWorkspace), cancellationToken) is { } snapshot
                ? Results.Ok(snapshot)
                : Results.NotFound());

    /// <summary>Everything the organisation owns, as one file, without stored credentials.</summary>
    public static Task<IResult> ExportData(
        InsuranceIntelligence.Infrastructure.Persistence.TenantDataService data, OrganisationService service, InfrastructureOptions options,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, _) =>
            Results.Text(await data.ExportAsync(organisationId, cancellationToken), "application/json"));

    /// <summary>Closes the organisation and deletes everything it owns. Irreversible; needs its exact name.</summary>
    public static Task<IResult> Close(
        CloseRequest request, InsuranceIntelligence.Infrastructure.Persistence.TenantDataService data, OrganisationService service,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
        {
            await service.CloseAsync(organisationId, userId, request.ConfirmName ?? string.Empty, CorrelationId(httpContext), cancellationToken);
            var removed = await data.PurgeAsync(organisationId, cancellationToken);
            return Results.Ok(new { closed = true, recordsRemoved = removed });
        });

    private static Task<IResult> Admin(
        HttpContext httpContext, InfrastructureOptions options, Func<string, string, Task<IResult>> action) =>
        Run(async () =>
        {
            if (DemoAuthorization.Require(httpContext, options, Permission.ManageOrganisation) is { } forbidden)
            {
                return forbidden;
            }

            // The organisation acted in is the one membership was confirmed for, never a body field.
            if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out _) || string.IsNullOrWhiteSpace(userId))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return await action(scope.TenantId, userId);
        });

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (OrganisationException exception)
        {
            var status = exception.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "forbidden" => StatusCodes.Status403Forbidden,
                "conflict" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(exception.Message, statusCode: status);
        }
    }

    private static object Describe(Organisation organisation) => new
    {
        organisation.OrganisationId,
        organisation.Name,
        type = organisation.Type.ToString(),
        workspaceId = organisation.DefaultWorkspaceId,
        organisation.PlanId,
        product = organisation.Product.ToString(),
    };

    private static string CorrelationId(HttpContext httpContext) => httpContext.TraceIdentifier;

    /// <summary>The signed-in person, whether or not they belong to any organisation yet.</summary>
    /// <summary>
    /// Creating organisations and joining by invitation are open only in multi-tenant mode, where each
    /// organisation's data, credentials and channels are kept apart. In a single-tenant deployment (the
    /// Managed App, or the SaaS build before it is switched on) they stay shut, so nobody who can sign in can
    /// found an organisation there. Local development is allowed for testing.
    /// </summary>
    public static IResult? SignUpClosed(InfrastructureOptions options) =>
        options.MultiTenant || options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            ? null
            : Results.Json(new { message = "Sign-up for IIP is not open yet. Contact info@oticgroup.net to get started." }, statusCode: StatusCodes.Status403Forbidden);

    private static string? SignedInUser(HttpContext httpContext, InfrastructureOptions options)
    {
        var development = options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
        if (!development && httpContext.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);

        // A system holding an API key is not a person: it cannot found organisations or accept invitations.
        return string.IsNullOrWhiteSpace(userId) || InsuranceIntelligence.Domain.Organisations.ApiKeyRecord.IsMachineUser(userId) ? null : userId;
    }

    private static readonly string[] EmailClaims = ["email", ClaimTypes.Email, "preferred_username", "upn", "unique_name"];

    internal static string? ResolveEmail(HttpContext httpContext, InfrastructureOptions options)
    {
        foreach (var type in EmailClaims)
        {
            var value = httpContext.User.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value) && value.Contains('@'))
            {
                return value;
            }
        }

        // Development only: there is no token to read an address from.
        return options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && httpContext.Request.Headers.TryGetValue("X-User-Email", out var header)
                ? header.ToString()
                : null;
    }
}

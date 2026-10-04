using System.Security.Claims;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Organisations;

public sealed record CreateOrganisationRequest(string? Name, string? Type);
public sealed record InviteRequest(string? Email, string? Role);
public sealed record AcceptInvitationRequest(string? Token);
public sealed record LinkRequest(string? OrganisationId);

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
            if (SignedInUser(httpContext, options) is not { } userId)
            {
                return Results.Unauthorized();
            }

            if (!Enum.TryParse<OrganisationType>(request.Type, ignoreCase: true, out var type))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Choose Insurer or Broker."] });
            }

            var organisation = await service.CreateAsync(userId, request.Name ?? string.Empty, type, CorrelationId(httpContext), cancellationToken);
            return Results.Created($"/api/v1/organisations/{organisation.OrganisationId}", Describe(organisation));
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
    };

    private static string CorrelationId(HttpContext httpContext) => httpContext.TraceIdentifier;

    /// <summary>The signed-in person, whether or not they belong to any organisation yet.</summary>
    private static string? SignedInUser(HttpContext httpContext, InfrastructureOptions options)
    {
        var development = options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
        if (!development && httpContext.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
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

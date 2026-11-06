using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Organisations;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// Moves a connected WhatsApp number from one workspace to another that the same person administers, so a
/// presenter (or an organisation with a test workspace) can point the number at the workspace they are
/// working in. A number belongs to one workspace only, and its credentials are write-only, so without this the
/// only way to move it is to disconnect and type every credential again.
///
/// Nothing secret leaves the server: the saved connection is read, re-saved under the other workspace and removed
/// from the first. The caller must hold the integrations permission in the workspace receiving the number and be
/// an administrator of the organisation giving it up. The move is audited in both workspaces.
/// </summary>
public static class WhatsAppNumberMoveEndpoints
{
    public sealed record MoveRequest(string? FromTenantId, string? FromWorkspaceId);

    /// <summary>The connections held by other workspaces that the caller administers and could bring here.</summary>
    public static async Task<IResult> ListElsewhere(
        IWhatsAppChannelConfigurationRepository repository,
        OrganisationService organisations,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        if (OrganisationEndpoints.SignedInUser(httpContext, options) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var administered = await AdministeredAsync(organisations, userId, cancellationToken);
        var active = await repository.ListActiveAsync(cancellationToken);
        var heldHere = active.Any(item => item.Scope.Matches(scope));
        var elsewhere = active
            .Where(item => !item.Scope.Matches(scope))
            .Select(item => (Configuration: item, Organisation: FindAdministered(administered, item.Scope)))
            .Where(item => item.Organisation is not null)
            .Select(item => new
            {
                fromTenantId = item.Configuration.Scope.TenantId,
                fromWorkspaceId = item.Configuration.Scope.WorkspaceId,
                organisationName = item.Organisation!.Name,
                phoneNumberId = item.Configuration.PhoneNumberId,
                businessDisplayName = item.Configuration.BusinessDisplayName,
            })
            .ToArray();

        return Results.Ok(new { heldHere, elsewhere });
    }

    public static async Task<IResult> MoveHere(
        MoveRequest request,
        IWhatsAppChannelConfigurationRepository repository,
        IWhatsAppRuntimeSettingsProvider settingsProvider,
        IAuditRepository auditRepository,
        OrganisationService organisations,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var target, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        if (OrganisationEndpoints.SignedInUser(httpContext, options) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["auditReason"] = ["An audit reason is required to move the WhatsApp number."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.FromTenantId) || string.IsNullOrWhiteSpace(request.FromWorkspaceId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["from"] = ["Say which workspace the number is being moved from."]
            });
        }

        var source = TenantScope.Create(request.FromTenantId.Trim(), request.FromWorkspaceId.Trim());
        if (source.Matches(target))
        {
            return Results.Conflict(new { message = "That number is already connected to this workspace." });
        }

        // The person must administer the organisation giving the number up. A refusal says nothing about whether
        // that workspace holds a connection at all.
        var administered = await AdministeredAsync(organisations, userId, cancellationToken);
        if (FindAdministered(administered, source) is null)
        {
            return Results.Json(
                new { message = "You can only move a number from a workspace you administer." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if ((await repository.GetAsync(target, cancellationToken)) is { IsActive: true })
        {
            return Results.Conflict(new
            {
                message = "This workspace already has a WhatsApp number connected. Disconnect it first, then bring the other number here.",
            });
        }

        var held = await repository.GetAsync(source, cancellationToken);
        if (held is not { IsActive: true })
        {
            return Results.NotFound(new { message = "That workspace has no WhatsApp number connected." });
        }

        var now = DateTimeOffset.UtcNow;
        var moved = held with { Scope = target, ConfiguredByUserId = userId, ConfiguredAt = now };

        // Save first, remove second: if the removal fails the copy is taken back, so the number is never left
        // with two owners (which would fail closed and answer nobody) or none.
        await repository.SaveAsync(moved, cancellationToken);
        try
        {
            await repository.DeleteAsync(source, cancellationToken);
        }
        catch
        {
            await repository.DeleteAsync(target, CancellationToken.None);
            throw;
        }

        await settingsProvider.RefreshForInboundAsync(cancellationToken);

        var correlationId = httpContext.TraceIdentifier;
        await auditRepository.WriteAsync(
            AuditEvent.WhatsAppConfigurationRemoved(source, userId, held.PhoneNumberId, $"Moved to another workspace. {auditReason}", correlationId),
            cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.WhatsAppConfigurationSaved(target, userId, held.PhoneNumberId, $"Moved here from another workspace. {auditReason}", correlationId),
            cancellationToken);

        return Results.Ok(new
        {
            message = "The WhatsApp number now belongs to this workspace. Earlier messages stay in the workspace where they arrived.",
            phoneNumberId = held.PhoneNumberId,
        });
    }

    private static async Task<IReadOnlyList<MyOrganisation>> AdministeredAsync(
        OrganisationService organisations, string userId, CancellationToken cancellationToken) =>
        (await organisations.ListMineAsync(userId, cancellationToken))
            .Where(item => item.Roles.Contains(nameof(PlatformRole.Admin), StringComparer.OrdinalIgnoreCase))
            .ToArray();

    private static MyOrganisation? FindAdministered(IReadOnlyList<MyOrganisation> administered, TenantScope scope) =>
        administered.FirstOrDefault(item =>
            string.Equals(item.OrganisationId, scope.TenantId, StringComparison.Ordinal)
            && string.Equals(item.WorkspaceId, scope.WorkspaceId, StringComparison.Ordinal));
}

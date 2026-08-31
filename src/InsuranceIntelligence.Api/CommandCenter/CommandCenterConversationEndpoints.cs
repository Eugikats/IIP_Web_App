using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.CommandCenter;

public static class CommandCenterConversationEndpoints
{
    public static async Task<IResult> List(
        bool? archived,
        int? limit,
        ICommandCenterConversationService service,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var error)) return error;
        try
        {
            return Results.Ok(await service.ListAsync(archived ?? false, limit ?? 20, cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> Get(
        Guid conversationId,
        ICommandCenterConversationService service,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var error)) return error;
        try
        {
            return Results.Ok(await service.GetAsync(conversationId, CommandCenterQueryEndpoints.GetCorrelationId(httpContext), cancellationToken));
        }
        catch (CommandCenterConversationNotFoundException)
        {
            return Results.NotFound();
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static Task<IResult> Rename(
        Guid conversationId,
        RenameConversationRequest request,
        ICommandCenterConversationService service,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        Change(tenantContextSetter, options, httpContext,
            () => service.RenameAsync(conversationId, request, CommandCenterQueryEndpoints.GetCorrelationId(httpContext), cancellationToken));

    public static Task<IResult> Archive(
        Guid conversationId,
        ChangeConversationStatusRequest request,
        ICommandCenterConversationService service,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        Change(tenantContextSetter, options, httpContext,
            () => service.ArchiveAsync(conversationId, request, CommandCenterQueryEndpoints.GetCorrelationId(httpContext), cancellationToken));

    public static Task<IResult> Restore(
        Guid conversationId,
        ChangeConversationStatusRequest request,
        ICommandCenterConversationService service,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        Change(tenantContextSetter, options, httpContext,
            () => service.RestoreAsync(conversationId, request, CommandCenterQueryEndpoints.GetCorrelationId(httpContext), cancellationToken));

    private static async Task<IResult> Change(
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        Func<Task<ConversationSummaryDto>> action)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var error)) return error;
        try
        {
            return Results.Ok(await action());
        }
        catch (CommandCenterConversationNotFoundException)
        {
            return Results.NotFound();
        }
        catch (CommandCenterConversationConflictException)
        {
            return Results.Conflict(new { code = "conversation_conflict", message = "The conversation changed. Refresh before trying again." });
        }
        catch (CommandCenterConversationValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }
}

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Local;

/// <summary>
/// Turns a missing-provider failure into an honest, structured response instead of letting it
/// fall through to the generic 500 the default exception handler returns for everything else.
/// Registered before AddProblemDetails' fallback (see Program.cs) so it only intercepts this one
/// exception type. Never includes the provider class name, a stack trace, or any other internal
/// detail — only the stable capability identifier a caller can act on (AGENTS.md I9).
/// </summary>
public sealed class ProviderNotConfiguredExceptionHandler(ILogger<ProviderNotConfiguredExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ProviderNotConfiguredException notConfigured)
        {
            return false;
        }

        logger.LogWarning(
            "Request to {Path} could not proceed: {Capability} is not configured.",
            httpContext.Request.Path,
            notConfigured.Capability);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Type = "https://iip.internal/errors/provider-not-configured",
                Title = "Capability not configured",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = $"{notConfigured.Capability} is not configured for this workspace. Ask a workspace administrator to configure it.",
                Extensions = { ["capability"] = notConfigured.Capability },
            },
            cancellationToken);

        return true;
    }
}

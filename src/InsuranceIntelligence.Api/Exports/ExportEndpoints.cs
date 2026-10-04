using System.Text;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Exports;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Exports;

/// <summary>Data going out to the insurer's own tools: finance, actuarial, regulatory returns.</summary>
public static class ExportEndpoints
{
    /// <summary>GET /api/v1/exports/claims-register?from=2026-01-01&amp;to=2026-03-31&amp;status=Settled</summary>
    public static async Task<IResult> ClaimsRegister(
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string? from = null,
        string? to = null,
        string? status = null)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        if (!TryDate(from, out var reportedFrom) || !TryDate(to, out var reportedTo))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use dates as yyyy-MM-dd."] });
        }

        var filter = new ClaimsRegisterFilter(reportedFrom, reportedTo, status);
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var csv = ClaimsRegisterExporter.BuildCsv(cases, filter);
        var rows = ClaimsRegisterExporter.Select(cases, filter).Count;

        // Taking a register out is a consequential act; it is recorded with how many rows left.
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, userId, ActorType.User, "export.claims_register", DateTimeOffset.UtcNow,
                null, null, [], "Export", null, "recorded", null,
                $"Claims register exported: {rows} claim(s), from {from ?? "start"} to {to ?? "today"}{(string.IsNullOrWhiteSpace(status) ? string.Empty : $", status {status}")}.",
                httpContext.TraceIdentifier),
            cancellationToken);

        // The byte order mark makes Excel read the UTF-8 names correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return Results.File(bytes, "text/csv; charset=utf-8", $"claims-register-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The column list, so an integrator can build against it without downloading data.</summary>
    public static IResult ClaimsRegisterColumns() => Results.Ok(new { columns = ClaimsRegisterExporter.Columns });

    private static bool TryDate(string? text, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", out var parsed))
        {
            date = parsed;
            return true;
        }

        return false;
    }
}

using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Admin;

public sealed record StatusWordingRow(
    string Status,
    string StaffLabel,
    string DefaultHeadline,
    string DefaultDetail,
    string Headline,
    string Detail,
    bool Customised);

public sealed record StatusWordingInput(string? Headline, string? Detail);

/// <summary>The workspace's own wording. A status left out (or sent with both fields empty) goes back to the default.</summary>
public sealed record SaveStatusWordingRequest(IReadOnlyDictionary<string, StatusWordingInput>? Wording);

/// <summary>
/// What a customer reads about their claim's status, per status, for the caller's workspace (build-loop
/// item 19). Needs ManageTenantSettings. Every change is audited by name. The default wording is a
/// starting point to be confirmed by the insurer and legal.
/// </summary>
public static class ClaimStatusWordingEndpoints
{
    public static async Task<IResult> Get(
        ITenantSettingsRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(Rows((await repository.GetAsync(scope, cancellationToken)).ClaimantStatusWording));
    }

    public static async Task<IResult> Put(
        SaveStatusWordingRequest request,
        ITenantSettingsRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to change settings.");
        }

        var supplied = (request?.Wording ?? new Dictionary<string, StatusWordingInput>())
            .ToDictionary(pair => pair.Key, pair => new StatusWording(pair.Value?.Headline ?? string.Empty, pair.Value?.Detail ?? string.Empty));
        var unknown = supplied.Keys.Where(key => !Enum.TryParse<CaseStatus>(key?.Trim(), ignoreCase: true, out var status) || !Enum.IsDefined(status)).ToArray();
        if (unknown.Length > 0)
        {
            return Validation("wording", $"These are not claim statuses: {string.Join(", ", unknown)}.");
        }

        var problems = ClaimantStatusWords.Validate(supplied);
        if (problems.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["wording"] = problems.ToArray() });
        }

        var current = await repository.GetAsync(scope, cancellationToken);
        var updated = current with
        {
            ClaimantStatusWording = ClaimantStatusWords.Normalise(supplied),
            UpdatedByUserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        if (ClaimantStatusWords.SameAs(updated.ClaimantStatusWording, current.ClaimantStatusWording))
        {
            return Results.Ok(Rows(current.ClaimantStatusWording));
        }

        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.TenantSettingsUpdated(scope, userId, [nameof(TenantSettings.ClaimantStatusWording)], httpContext.TraceIdentifier),
            cancellationToken);
        return Results.Ok(Rows(updated.ClaimantStatusWording));
    }

    private static IReadOnlyList<StatusWordingRow> Rows(IReadOnlyDictionary<string, StatusWording>? overrides) =>
        Enum.GetValues<CaseStatus>()
            .OrderBy(status => ClaimantTimeline.Rank(status))
            .ThenBy(status => (int)status)
            .Select(status =>
            {
                var fallback = ClaimantStatusWords.DefaultFor(status);
                var shown = ClaimantStatusWords.For(status, overrides);
                return new StatusWordingRow(
                    status.ToString(),
                    InsuranceIntelligence.Application.CommandCenter.ClaimStatusLabels.For(status),
                    fallback.Headline,
                    fallback.Detail,
                    shown.Headline,
                    shown.Detail,
                    shown != fallback);
            })
            .ToArray();

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

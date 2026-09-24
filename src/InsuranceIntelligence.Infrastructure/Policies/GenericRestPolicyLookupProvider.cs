using System.Text.Json;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Policies;

/// <summary>
/// Settings for the generic REST policy adapter (blueprint §14 Phase 7, WO 7.2), parsed from the
/// flat Connection settings dictionary, mirrors EgoSmsSettings.TryParse's defensive parsing
/// style. Field-mapping settings default to this platform's own Policy field names, so a partner
/// whose API happens to use the same shape needs to configure only BaseUrl and the auth header.
/// </summary>
public sealed record GenericRestPolicySettings(
    string BaseUrl,
    string? AuthHeaderName,
    string? AuthHeaderValue,
    string PolicyNumberField,
    string ProductLineField,
    string CoverTypeField,
    string CustomerIdField,
    string EffectiveFromField,
    string EffectiveToField,
    int RequestTimeoutSeconds)
{
    public static GenericRestPolicySettings FromSettings(IReadOnlyDictionary<string, string> settings) =>
        new(
            settings.GetValueOrDefault("baseUrl", string.Empty).TrimEnd('/'),
            settings.GetValueOrDefault("authHeaderName"),
            settings.GetValueOrDefault("authHeaderValue"),
            settings.GetValueOrDefault("policyNumberField", "policyNumber"),
            settings.GetValueOrDefault("productLineField", "productLine"),
            settings.GetValueOrDefault("coverTypeField", "coverType"),
            settings.GetValueOrDefault("customerIdField", "customerId"),
            settings.GetValueOrDefault("effectiveFromField", "effectiveFrom"),
            settings.GetValueOrDefault("effectiveToField", "effectiveTo"),
            int.TryParse(settings.GetValueOrDefault("requestTimeoutSeconds"), out var timeout) ? timeout : 15);
}

/// <summary>
/// The configurable REST adapter WO 7.2 asks for: a real, working HTTP client that calls a
/// partner's policy API using tenant-configured base URL, auth header, and response field mapping
///, not a specific insurer's specific integration (that needs a partnership and a real API
/// contract this codebase cannot invent), but the seam that turns "no integration" into "an
/// integration awaiting a partner's endpoint." Convention: GET {baseUrl}/policies/{policyNumber}
/// and GET {baseUrl}/customers/{customerId}/policies, documented, not negotiable per-tenant in
/// this pass; a real partner's actual path shape is exactly the kind of detail a partnership
/// deliverable settles, not something guessable in advance.
/// </summary>
public sealed class GenericRestPolicyLookupProvider(
    GenericRestPolicySettings settings,
    HttpClient httpClient,
    ILogger<GenericRestPolicyLookupProvider> logger) : IPolicyLookupProvider
{
    public async Task<Policy?> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return null; // Honest no-op, never fabricate a policy from an unconfigured adapter.
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{settings.BaseUrl}/policies/{Uri.EscapeDataString(policyNumber)}");
        ApplyAuth(request);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 60)));
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return MapPolicy(document.RootElement, scope);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Generic REST policy lookup failed. Tenant={TenantId} Workspace={WorkspaceId}", scope.TenantId, scope.WorkspaceId);
            return null;
        }
    }

    public async Task<IReadOnlyCollection<Policy>> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{settings.BaseUrl}/customers/{Uri.EscapeDataString(customerId)}/policies");
        ApplyAuth(request);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 60)));
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement.EnumerateArray()
                .Select(element => MapPolicy(element, scope))
                .Where(policy => policy is not null)
                .Select(policy => policy!)
                .ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Generic REST policy lookup by customer failed. Tenant={TenantId} Workspace={WorkspaceId}", scope.TenantId, scope.WorkspaceId);
            return [];
        }
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(settings.AuthHeaderName) && !string.IsNullOrWhiteSpace(settings.AuthHeaderValue))
        {
            request.Headers.TryAddWithoutValidation(settings.AuthHeaderName, settings.AuthHeaderValue);
        }
    }

    private Policy? MapPolicy(JsonElement root, TenantScope scope)
    {
        var policyNumber = GetString(root, settings.PolicyNumberField);
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            return null; // The one field every mapping must resolve, without it there is no policy.
        }

        return new Policy(
            PolicyId: policyNumber,
            TenantId: scope.TenantId,
            WorkspaceId: scope.WorkspaceId,
            PolicyNumber: policyNumber,
            ProductLine: GetString(root, settings.ProductLineField) ?? "Unknown",
            CoverType: GetString(root, settings.CoverTypeField),
            CustomerId: GetString(root, settings.CustomerIdField),
            BranchId: null,
            EffectiveFrom: GetDate(root, settings.EffectiveFromField),
            EffectiveTo: GetDate(root, settings.EffectiveToField));
    }

    private static string? GetString(JsonElement root, string fieldName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateOnly? GetDate(JsonElement root, string fieldName) =>
        GetString(root, fieldName) is { } raw && DateOnly.TryParse(raw, out var date) ? date : null;
}

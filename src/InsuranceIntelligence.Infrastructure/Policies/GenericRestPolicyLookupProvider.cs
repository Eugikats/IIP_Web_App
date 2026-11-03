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
    private const string Source = "ExternalPolicySystem";

    public async Task<PolicyLookupResult> FindPolicyByNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return new(PolicyLookupStatus.NotConfigured, null, Source);
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
                return new(MapStatus(response.StatusCode), null, Source);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var policy = MapPolicy(document.RootElement, scope);
            return policy is null
                ? new(PolicyLookupStatus.Malformed, null, Source)
                : new(PolicyLookupStatus.Found, policy, Source);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Generic REST policy lookup failed. Tenant={TenantId} Workspace={WorkspaceId}", scope.TenantId, scope.WorkspaceId);
            return new(PolicyLookupStatus.Unavailable, null, Source);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(PolicyLookupStatus.Unavailable, null, Source);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Generic REST policy response was malformed. Tenant={TenantId} Workspace={WorkspaceId}", scope.TenantId, scope.WorkspaceId);
            return new(PolicyLookupStatus.Malformed, null, Source);
        }
    }

    public async Task<PolicyCollectionLookupResult> FindPoliciesByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return new(PolicyLookupStatus.NotConfigured, [], Source);
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
                return new(MapStatus(response.StatusCode), [], Source);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new(PolicyLookupStatus.Malformed, [], Source);
            }

            var policies = new List<Policy>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var policy = MapPolicy(element, scope);
                if (policy is null)
                {
                    return new(PolicyLookupStatus.Malformed, [], Source);
                }
                policies.Add(policy);
            }

            return new(policies.Count == 0 ? PolicyLookupStatus.NotFound : PolicyLookupStatus.Found, policies, Source);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Generic REST policy lookup by customer failed. Tenant={TenantId} Workspace={WorkspaceId}", scope.TenantId, scope.WorkspaceId);
            return new(PolicyLookupStatus.Unavailable, [], Source);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(PolicyLookupStatus.Unavailable, [], Source);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Generic REST policy response by customer was malformed. Tenant={TenantId} Workspace={WorkspaceId}", scope.TenantId, scope.WorkspaceId);
            return new(PolicyLookupStatus.Malformed, [], Source);
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
        var productLine = GetString(root, settings.ProductLineField);
        if (string.IsNullOrWhiteSpace(policyNumber) || string.IsNullOrWhiteSpace(productLine))
        {
            return null;
        }

        var effectiveFrom = GetDate(root, settings.EffectiveFromField, out var effectiveFromValid);
        var effectiveTo = GetDate(root, settings.EffectiveToField, out var effectiveToValid);
        if (!effectiveFromValid || !effectiveToValid || (effectiveFrom is not null && effectiveTo is not null && effectiveTo < effectiveFrom))
        {
            return null;
        }

        return new Policy(
            PolicyId: policyNumber,
            TenantId: scope.TenantId,
            WorkspaceId: scope.WorkspaceId,
            PolicyNumber: policyNumber,
            ProductLine: productLine,
            CoverType: GetString(root, settings.CoverTypeField),
            CustomerId: GetString(root, settings.CustomerIdField),
            BranchId: null,
            EffectiveFrom: effectiveFrom,
            EffectiveTo: effectiveTo,
            RecordSource: PolicyRecordSource.ExternalLookup,
            LastRefreshedAt: DateTimeOffset.UtcNow);
    }

    private static string? GetString(JsonElement root, string fieldName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateOnly? GetDate(JsonElement root, string fieldName, out bool valid)
    {
        valid = true;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(fieldName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String || !DateOnly.TryParse(value.GetString(), out var date))
        {
            valid = false;
            return null;
        }

        return date;
    }

    private static PolicyLookupStatus MapStatus(System.Net.HttpStatusCode statusCode) => statusCode switch
    {
        System.Net.HttpStatusCode.NotFound => PolicyLookupStatus.NotFound,
        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => PolicyLookupStatus.Unauthorized,
        System.Net.HttpStatusCode.TooManyRequests => PolicyLookupStatus.Unavailable,
        _ when (int)statusCode >= 500 => PolicyLookupStatus.Unavailable,
        _ => PolicyLookupStatus.ProviderRejected
    };
}

using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Policies;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class GenericRestPolicyLookupProviderTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-pas", "workspace-claims");

    [Fact]
    public async Task FindPolicyByNumberAsync_MapsAResponseUsingTheConfiguredFieldNames()
    {
        var handler = new CapturingHandler("""
            {"policyNumber":"NSA/MO/2026/00033","productLine":"Motor","coverType":"Comprehensive","customerId":"cust-1","effectiveFrom":"2026-01-01","effectiveTo":"2026-12-31"}
            """);
        var provider = CreateProvider(handler);

        var policy = await provider.FindPolicyByNumberAsync("NSA/MO/2026/00033", Scope);

        Assert.NotNull(policy);
        Assert.Equal("NSA/MO/2026/00033", policy!.PolicyNumber);
        Assert.Equal("Motor", policy.ProductLine);
        Assert.Equal("Comprehensive", policy.CoverType);
        Assert.Equal("cust-1", policy.CustomerId);
        Assert.Equal(new DateOnly(2026, 1, 1), policy.EffectiveFrom);
        Assert.Equal(Scope.TenantId, policy.TenantId);
        Assert.Contains("/policies/NSA%2FMO%2F2026%2F00033", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task FindPolicyByNumberAsync_AppliesTheConfiguredAuthHeader()
    {
        var handler = new CapturingHandler("""{"policyNumber":"POL-1","productLine":"Motor"}""");
        var provider = CreateProvider(handler, authHeaderName: "X-Api-Key", authHeaderValue: "secret-token-1");

        await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.True(handler.LastRequestHeaders!.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("secret-token-1", values!.Single());
    }

    [Fact]
    public async Task FindPolicyByNumberAsync_NotFoundResponse_ReturnsNullRatherThanThrowing()
    {
        var handler = new CapturingHandler("""{"message":"not found"}""", HttpStatusCode.NotFound);
        var provider = CreateProvider(handler);

        var policy = await provider.FindPolicyByNumberAsync("POL-UNKNOWN", Scope);

        Assert.Null(policy);
    }

    [Fact]
    public async Task FindPolicyByNumberAsync_MissingBaseUrl_ReturnsNullWithoutCallingAnything()
    {
        var handler = new CapturingHandler("""{}""");
        var settings = new GenericRestPolicySettings(string.Empty, null, null, "policyNumber", "productLine", "coverType", "customerId", "effectiveFrom", "effectiveTo", 15);
        var provider = new GenericRestPolicyLookupProvider(settings, new HttpClient(handler), NullLogger<GenericRestPolicyLookupProvider>.Instance);

        var policy = await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.Null(policy);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task FindPoliciesByCustomerAsync_MapsEveryElementOfTheArrayResponse()
    {
        var handler = new CapturingHandler("""
            [{"policyNumber":"POL-1","productLine":"Motor"},{"policyNumber":"POL-2","productLine":"Medical"}]
            """);
        var provider = CreateProvider(handler);

        var policies = await provider.FindPoliciesByCustomerAsync("cust-1", Scope);

        Assert.Equal(2, policies.Count);
        Assert.Contains(policies, p => p.PolicyNumber == "POL-1" && p.ProductLine == "Motor");
        Assert.Contains(policies, p => p.PolicyNumber == "POL-2" && p.ProductLine == "Medical");
    }

    [Fact]
    public async Task MapPolicy_ResponseMissingThePolicyNumberField_IsSkippedNotFabricated()
    {
        var handler = new CapturingHandler("""{"productLine":"Motor"}""");
        var provider = CreateProvider(handler);

        var policy = await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.Null(policy);
    }

    private static GenericRestPolicyLookupProvider CreateProvider(CapturingHandler handler, string? authHeaderName = null, string? authHeaderValue = null)
    {
        var settings = new GenericRestPolicySettings(
            "https://pas.partner.example/api", authHeaderName, authHeaderValue,
            "policyNumber", "productLine", "coverType", "customerId", "effectiveFrom", "effectiveTo", 15);
        return new GenericRestPolicyLookupProvider(settings, new HttpClient(handler), NullLogger<GenericRestPolicyLookupProvider>.Instance);
    }

    private sealed class CapturingHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public System.Net.Http.Headers.HttpRequestHeaders? LastRequestHeaders { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestHeaders = request.Headers;
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
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

        var lookup = await provider.FindPolicyByNumberAsync("NSA/MO/2026/00033", Scope);

        Assert.Equal(PolicyLookupStatus.Found, lookup.Status);
        Assert.Equal("ExternalPolicySystem", lookup.Source);
        Assert.NotNull(lookup.Policy);
        Assert.Equal("NSA/MO/2026/00033", lookup.Policy!.PolicyNumber);
        Assert.Equal("Motor", lookup.Policy.ProductLine);
        Assert.Equal("Comprehensive", lookup.Policy.CoverType);
        Assert.Equal("cust-1", lookup.Policy.CustomerId);
        Assert.Equal(new DateOnly(2026, 1, 1), lookup.Policy.EffectiveFrom);
        Assert.Equal(Scope.TenantId, lookup.Policy.TenantId);
        Assert.Equal(PolicyRecordSource.ExternalLookup, lookup.Policy.RecordSource);
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
    public async Task FindPolicyByNumberAsync_NotFoundResponse_IsDistinguishedFromProviderFailure()
    {
        var handler = new CapturingHandler("""{"message":"not found"}""", HttpStatusCode.NotFound);
        var provider = CreateProvider(handler);

        var lookup = await provider.FindPolicyByNumberAsync("POL-UNKNOWN", Scope);

        Assert.Equal(PolicyLookupStatus.NotFound, lookup.Status);
        Assert.Null(lookup.Policy);
    }

    [Fact]
    public async Task FindPolicyByNumberAsync_MissingBaseUrlIsNotConfiguredAndDoesNotCallAnything()
    {
        var handler = new CapturingHandler("""{}""");
        var settings = new GenericRestPolicySettings(string.Empty, null, null, "policyNumber", "productLine", "coverType", "customerId", "effectiveFrom", "effectiveTo", 15);
        var provider = new GenericRestPolicyLookupProvider(settings, new HttpClient(handler), NullLogger<GenericRestPolicyLookupProvider>.Instance);

        var lookup = await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.Equal(PolicyLookupStatus.NotConfigured, lookup.Status);
        Assert.Null(lookup.Policy);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task FindPoliciesByCustomerAsync_MapsEveryElementOfTheArrayResponse()
    {
        var handler = new CapturingHandler("""
            [{"policyNumber":"POL-1","productLine":"Motor"},{"policyNumber":"POL-2","productLine":"Medical"}]
            """);
        var provider = CreateProvider(handler);

        var lookup = await provider.FindPoliciesByCustomerAsync("cust-1", Scope);

        Assert.Equal(PolicyLookupStatus.Found, lookup.Status);
        Assert.Equal(2, lookup.Policies.Count);
        Assert.Contains(lookup.Policies, p => p.PolicyNumber == "POL-1" && p.ProductLine == "Motor");
        Assert.Contains(lookup.Policies, p => p.PolicyNumber == "POL-2" && p.ProductLine == "Medical");
    }

    [Fact]
    public async Task MapPolicy_ResponseMissingThePolicyNumberField_IsSkippedNotFabricated()
    {
        var handler = new CapturingHandler("""{"productLine":"Motor"}""");
        var provider = CreateProvider(handler);

        var lookup = await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.Equal(PolicyLookupStatus.Malformed, lookup.Status);
        Assert.Null(lookup.Policy);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, PolicyLookupStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, PolicyLookupStatus.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable, PolicyLookupStatus.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, PolicyLookupStatus.ProviderRejected)]
    public async Task FindPolicyByNumberAsync_PreservesExternalFailureCategory(HttpStatusCode statusCode, PolicyLookupStatus expected)
    {
        var provider = CreateProvider(new CapturingHandler("{}", statusCode));

        var result = await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Policy);
    }

    [Fact]
    public async Task FindPolicyByNumberAsync_InvalidMappedFieldsAreMalformedInsteadOfUnknownPolicy()
    {
        var handler = new CapturingHandler("""{"policyNumber":"POL-1","productLine":"Motor","effectiveFrom":"not-a-date"}""");
        var provider = CreateProvider(handler);

        var result = await provider.FindPolicyByNumberAsync("POL-1", Scope);

        Assert.Equal(PolicyLookupStatus.Malformed, result.Status);
        Assert.Null(result.Policy);
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

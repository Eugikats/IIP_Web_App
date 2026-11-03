using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Policies;

namespace InsuranceIntelligence.Api.Tests;

public sealed class GenericRestPolicyConnectionValidatorTests
{
    private readonly GenericRestPolicyConnectionValidator validator = new();

    [Fact]
    public void ConnectionType_IsPolicySystem()
    {
        Assert.Equal(ConnectionType.PolicySystem, validator.ConnectionType);
    }

    [Fact]
    public async Task ValidateAsync_NoBaseUrl_Fails()
    {
        var result = await validator.ValidateAsync(new Dictionary<string, string>());

        Assert.False(result.Success);
        Assert.Contains("base URL", result.SafeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_MalformedBaseUrl_Fails()
    {
        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["baseUrl"] = "not-a-url" });

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("http://pas.partner.example/api")]
    [InlineData("https://user:password@pas.partner.example/api")]
    [InlineData("https://localhost:5167/api")]
    public async Task ValidateAsync_InsecureOrLocalDestination_Fails(string baseUrl)
    {
        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["baseUrl"] = baseUrl });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_AuthHeaderNameWithoutValue_Fails()
    {
        var result = await validator.ValidateAsync(new Dictionary<string, string>
        {
            ["baseUrl"] = "https://pas.partner.example/api",
            ["authHeaderName"] = "X-Api-Key",
        });

        Assert.False(result.Success);
        Assert.Contains("auth header value", result.SafeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_ValidBaseUrlWithNoAuthHeader_Succeeds()
    {
        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["baseUrl"] = "https://pas.partner.example/api" });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_ValidBaseUrlAndCompleteAuthHeader_Succeeds()
    {
        var result = await validator.ValidateAsync(new Dictionary<string, string>
        {
            ["baseUrl"] = "https://pas.partner.example/api",
            ["authHeaderName"] = "X-Api-Key",
            ["authHeaderValue"] = "secret-token-1",
        });

        Assert.True(result.Success);
    }
}

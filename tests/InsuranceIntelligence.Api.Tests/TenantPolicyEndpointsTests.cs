using System.Text.Json;
using InsuranceIntelligence.Api.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class TenantPolicyEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetPolicy_ReportsTheExtractionThresholdRowAsNotImplemented()
    {
        // Locks in a real, verified finding: no numeric confidence threshold gating document
        // review exists anywhere in this codebase (blueprint §14 WO 3.4). This row previously
        // claimed "Pilot: 0.80 confidence — configurable" with no code behind it — this test
        // exists so nobody accidentally reintroduces that false claim without also building the
        // feature it describes.
        var result = TenantPolicyEndpoints.GetPolicy(Context(), Options());

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);

        var rules = body.GetProperty("rules").EnumerateArray().ToArray();
        var thresholdRule = rules.Single(r => r.GetProperty("rule").GetString() == "Extraction human-review threshold");
        Assert.False(thresholdRule.GetProperty("enforced").GetBoolean());
        Assert.Contains("Not implemented", thresholdRule.GetProperty("enforcementSource").GetString());
    }

    [Fact]
    public async Task GetPolicy_ReportsEveryOtherRuleAsEnforcedWithARealSource()
    {
        var result = TenantPolicyEndpoints.GetPolicy(Context(), Options());

        var (_, body) = await ExecuteAsync<JsonElement>(result);
        var rules = body.GetProperty("rules").EnumerateArray().ToArray();

        Assert.True(rules.Length >= 7);
        foreach (var rule in rules.Where(r => r.GetProperty("rule").GetString() != "Extraction human-review threshold"))
        {
            Assert.True(rule.GetProperty("enforced").GetBoolean(), $"Expected '{rule.GetProperty("rule").GetString()}' to be enforced.");
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("enforcementSource").GetString()));
        }
    }

    private static InfrastructureOptions Options() => new() { AuthMode = "Development" };

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-tenant-policy" };
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }
}

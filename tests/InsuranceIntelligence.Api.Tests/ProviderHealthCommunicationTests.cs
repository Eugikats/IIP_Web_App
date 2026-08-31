using System.Text.Json;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ProviderHealthCommunicationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProviderHealth_DisabledCommunicationAndNotificationsAreNotConfigured()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Entra",
            CommunicationProvider = "Disabled",
            NotificationProvider = "Disabled"
        };

        var result = AdminEndpoints.GetProviderHealth(options, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "communications"
            && item.GetProperty("status").GetString() == "not_configured"
            && item.GetProperty("detail").GetString()!.Contains("not configured", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "notifications"
            && item.GetProperty("status").GetString() == "not_configured"
            && item.GetProperty("detail").GetString()!.Contains("not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProviderHealth_UnsupportedCommunicationProviderDoesNotAppearConfigured()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Entra",
            CommunicationProvider = "FutureSmsProvider",
            NotificationProvider = "FutureNotificationProvider"
        };

        var result = AdminEndpoints.GetProviderHealth(options, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "communications"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "notifications"
            && item.GetProperty("status").GetString() == "missing");
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-provider-health" };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "provider-admin"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("tenant_id", "tenant-demo"),
            new Claim("workspace_id", "workspace-claims")
        ], "Entra"));
        // ADR-0005: deployed-mode authz reads the store-resolved permissions the middleware stashes.
        context.Items[InsuranceIntelligence.Api.Auth.DemoAuthorization.EffectivePermissionsItemKey] =
            (IReadOnlyCollection<InsuranceIntelligence.Domain.Administration.Permission>)
                InsuranceIntelligence.Domain.Administration.RolePermissionMap.ForRole(
                    InsuranceIntelligence.Domain.Administration.PlatformRole.Admin).ToArray();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = Context();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default!
            : (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }
}

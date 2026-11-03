using System.Text.Json;
using InsuranceIntelligence.Api.Notifications;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Api.Tests;

public sealed class NotificationRoutingEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Get_ReturnsOnlyActiveUsersInResolvedWorkspace_AndTruthfulDestinations()
    {
        var users = new InMemoryUserAccountRepository();
        await users.SaveAsync(User("active", Scope()));
        await users.SaveAsync(User("inactive", Scope()) with { Status = "Disabled" });
        await users.SaveAsync(User("other-workspace", TenantScope.Create("tenant-demo", "workspace-other")));

        var result = await NotificationRoutingEndpoints.Get(
            new InMemoryTenantConfigurationRepository(), users, Options(), Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<NotificationRoutingResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("active", Assert.Single(body!.Users).UserId);
        Assert.Equal(["InApp"], body.AvailableDestinations);
    }

    [Fact]
    public async Task Update_RejectsUsersOutsideTheActiveWorkspace()
    {
        var user = User("officer", Scope());
        var users = new InMemoryUserAccountRepository();
        await users.SaveAsync(user with { Scope = TenantScope.Create("tenant-other", "workspace-claims") });
        var request = new UpdateNotificationRoutingRequest([
            new UpdateNotificationRoutingRule("ClaimEscalated", ["officer"], ["InApp"])
        ]);

        var result = await NotificationRoutingEndpoints.Update(
            request, new InMemoryTenantConfigurationRepository(), users, new InMemoryAuditRepository(), Options(),
            Context("Route escalations to this workspace's claims leads."), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task Update_StoresValidatedRoutingAndAuditsWithoutRecipientData()
    {
        var configuration = new InMemoryTenantConfigurationRepository();
        var users = new InMemoryUserAccountRepository();
        await users.SaveAsync(User("backup-officer", Scope()));
        var audit = new InMemoryAuditRepository();
        var request = new UpdateNotificationRoutingRequest([
            new UpdateNotificationRoutingRule("ClaimEscalated", ["backup-officer"], ["InApp"])
        ]);

        var result = await NotificationRoutingEndpoints.Update(
            request, configuration, users, audit, Options(), Context("Route claim escalations to a backup officer."), CancellationToken.None);
        var (status, body) = await ExecuteAsync<NotificationRoutingResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("ClaimEscalated", Assert.Single(body!.Configuration.Rules).Type);
        Assert.Equal("backup-officer", Assert.Single(Assert.Single(body.Configuration.Rules).AdditionalRecipientUserIds));
        var saved = await configuration.GetAsync(Scope());
        Assert.Equal(NotificationDestination.InApp, Assert.Single(Assert.Single(saved.NotificationRouting.Rules).AdditionalDestinations));
        var auditEvent = Assert.Single(await audit.ListAsync(Scope()));
        Assert.Equal("admin.notification_routing.updated", auditEvent.ActionName);
        Assert.Equal("Route claim escalations to a backup officer.", auditEvent.AuditReason);
        Assert.DoesNotContain(auditEvent.EvidenceIds, value => value.Contains("backup-officer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_RejectsTeamsWhenDeploymentDoesNotHaveThatCapability()
    {
        var request = new UpdateNotificationRoutingRequest([
            new UpdateNotificationRoutingRule("ClaimEscalated", [], ["MicrosoftTeams"])
        ]);
        var result = await NotificationRoutingEndpoints.Update(
            request, new InMemoryTenantConfigurationRepository(), new InMemoryUserAccountRepository(),
            new InMemoryAuditRepository(), Options(), Context("Configure Teams routing."), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task Update_RejectsInAppRuleWithoutRecipients()
    {
        var request = new UpdateNotificationRoutingRequest([
            new UpdateNotificationRoutingRule("ClaimEscalated", [], ["InApp"])
        ]);
        var result = await NotificationRoutingEndpoints.Update(
            request, new InMemoryTenantConfigurationRepository(), new InMemoryUserAccountRepository(),
            new InMemoryAuditRepository(), Options(), Context("Configure escalation routing."), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task Get_OffersTeamsOnlyWhenLiveDeploymentProviderIsSelected()
    {
        var result = await NotificationRoutingEndpoints.Get(
            new InMemoryTenantConfigurationRepository(), new InMemoryUserAccountRepository(),
            Options() with { NotificationProvider = "IncomingWebhook" }, Context(), CancellationToken.None,
            Microsoft.Extensions.Options.Options.Create(new InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProviderOptions
            {
                WebhookUrl = "https://teams.example.invalid/webhook/secret",
            }));
        var (status, body) = await ExecuteAsync<NotificationRoutingResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("MicrosoftTeams", body!.AvailableDestinations);
    }

    [Fact]
    public async Task Get_ProviderModeWithoutHttpsWebhookDoesNotOfferTeams()
    {
        var result = await NotificationRoutingEndpoints.Get(
            new InMemoryTenantConfigurationRepository(), new InMemoryUserAccountRepository(),
            Options() with { NotificationProvider = "IncomingWebhook" }, Context(), CancellationToken.None,
            Microsoft.Extensions.Options.Options.Create(new InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProviderOptions
            {
                WebhookUrl = "http://teams.example.invalid/webhook/secret",
            }));
        var (status, body) = await ExecuteAsync<NotificationRoutingResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.DoesNotContain("MicrosoftTeams", body!.AvailableDestinations);
    }

    private static InsuranceUserAccount User(string userId, TenantScope scope) => new()
    {
        UserId = userId,
        Scope = scope,
        Name = userId,
        Email = $"{userId}@example.invalid",
        Role = PlatformRole.ClaimsOfficer,
    };

    private static TenantScope Scope() => TenantScope.Create("tenant-demo", "workspace-claims");

    private static InfrastructureOptions Options() => new() { AuthMode = "Development", NotificationProvider = "Mock" };

    private static DefaultHttpContext Context(string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-notification-routing" };
        context.Request.Headers["X-Tenant-Id"] = Scope().TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope().WorkspaceId;
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        if (auditReason is not null) context.Request.Headers["X-Audit-Reason"] = auditReason;
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = context.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(context.Response.Body, JsonOptions);
        return (context.Response.StatusCode, body);
    }
}

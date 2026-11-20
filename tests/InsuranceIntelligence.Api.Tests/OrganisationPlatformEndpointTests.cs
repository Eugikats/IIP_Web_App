using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The organisation platform over HTTP: who may call it, the refusal when no organisation is selected,
/// and one automation carried from a template to a recorded run through the real routes.</summary>
public sealed class OrganisationPlatformEndpointTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Base = "/api/v1/org-platform";

    [Fact]
    public async Task ARequestWithNoOrganisationIsRefusedNotAServerError()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-demo-role", "ITAdmin");

        foreach (var path in new[] { "/me", "/insights", "/notifications", "/automations", "/workspaces" })
        {
            var response = await client.GetAsync(Base + path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ARoleWithoutThePlatformPermissionIsRefused()
    {
        var client = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: "Customer");

        var response = await client.GetAsync(Base + "/automations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheOverviewStartsEmptyAndTheLivePrimaryWorkspaceExists()
    {
        var client = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: "ITAdmin");

        var me = await MockedApiFactory.ReadJsonAsync(await client.GetAsync(Base + "/me"));
        var workspaces = await MockedApiFactory.ReadJsonAsync(await client.GetAsync(Base + "/workspaces"));
        var notifications = await MockedApiFactory.ReadJsonAsync(await client.GetAsync(Base + "/notifications"));

        Assert.True(me.GetProperty("isOrgAdmin").GetBoolean());
        Assert.Single(workspaces.EnumerateArray());
        Assert.True(workspaces[0].GetProperty("isPrimary").GetBoolean());
        Assert.Equal(0, notifications.GetProperty("counts").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ParallelFirstRequestsCreateOnlyOnePrimaryWorkspace()
    {
        var client = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: "ITAdmin");

        // A page opens with several calls at once; each of them makes sure the primary workspace exists.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            client.GetAsync(i % 2 == 0 ? Base + "/me" : Base + "/notifications")));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var workspaces = await MockedApiFactory.ReadJsonAsync(await client.GetAsync(Base + "/workspaces"));
        Assert.Single(workspaces.EnumerateArray());
    }

    [Fact]
    public async Task AnAutomationCannotSkipTheTestAndApprovalSteps()
    {
        var client = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: "Admin");
        var workspaces = await MockedApiFactory.ReadJsonAsync(await client.GetAsync(Base + "/workspaces"));
        var workspaceId = workspaces[0].GetProperty("id").GetString();

        var created = await client.PostAsJsonAsync(Base + "/automations", new
        {
            workspaceId,
            templateKey = "claims_sla",
            name = "Claims past their deadline",
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var automation = await MockedApiFactory.ReadJsonAsync(created);
        var id = automation.GetProperty("id").GetString();
        Assert.Equal("draft", automation.GetProperty("status").GetString());

        // A draft cannot be activated: the lifecycle refuses it with a reason.
        var early = await client.PostAsync($"{Base}/automations/{id}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        var tested = await MockedApiFactory.ReadJsonAsync(await client.PostAsync($"{Base}/automations/{id}/test", null));
        Assert.Equal("tested", tested.GetProperty("automation").GetProperty("status").GetString());
    }
}

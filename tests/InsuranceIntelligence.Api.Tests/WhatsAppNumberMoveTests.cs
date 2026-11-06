using System.Net;
using System.Net.Http.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Organisations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// A connected WhatsApp number can be moved to another workspace the same person administers, without anyone
/// typing the credentials again. These tests pin who may do it, what refuses it, and that the number ends with
/// exactly one owner.
/// </summary>
public sealed class WhatsAppNumberMoveTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private sealed record World(Organisation Bank, Organisation Insurer, string Phone, string UserId);

    private static async Task<World> StartAsync(WebApplicationFactory<Program> host, bool connectToBank = true)
    {
        var userId = $"mover-{Guid.NewGuid():N}"[..14];
        using var services = host.Services.CreateScope();
        var organisations = services.ServiceProvider.GetRequiredService<OrganisationService>();
        var bank = await organisations.CreateAsync(userId, "Mavuno Bank (test)", OrganisationType.Insurer, "corr");
        var insurer = await organisations.CreateAsync(userId, "Nile Life (test)", OrganisationType.Insurer, "corr");
        var phone = $"phone-{Guid.NewGuid():N}"[..18];
        if (connectToBank)
        {
            await SaveConnectionAsync(services.ServiceProvider, bank.Scope, phone, userId);
        }

        return new World(bank, insurer, phone, userId);
    }

    private static async Task SaveConnectionAsync(IServiceProvider services, TenantScope scope, string phone, string userId)
    {
        var protector = services.GetRequiredService<IWhatsAppCredentialProtector>();
        await services.GetRequiredService<IWhatsAppChannelConfigurationRepository>().SaveAsync(new WhatsAppChannelConfiguration
        {
            Scope = scope,
            PhoneNumberId = phone,
            WabaId = "waba-1",
            BusinessDisplayName = "Test number",
            EncryptedAccessToken = protector.Protect("token-secret"),
            EncryptedAppSecret = protector.Protect("app-secret"),
            EncryptedVerifyToken = protector.Protect("verify-secret"),
            ConfiguredByUserId = userId,
            ConfiguredAt = DateTimeOffset.UtcNow,
        });
    }

    private static HttpClient ClientFor(WebApplicationFactory<Program> host, Organisation organisation, string userId)
    {
        var client = host.CreateScopedClient(organisation.OrganisationId, organisation.Scope.WorkspaceId, "Admin", userId);
        client.DefaultRequestHeaders.Add("X-Audit-Reason", "Presenting the insurer demonstration");
        return client;
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, Organisation from) =>
        client.PostAsJsonAsync("/api/v1/integrations/whatsapp/configuration/move-here",
            new { fromTenantId = from.OrganisationId, fromWorkspaceId = from.Scope.WorkspaceId });

    [Fact]
    public async Task ThePersonWhoAdministersBoth_SeesTheNumberHeldElsewhere()
    {
        var world = await StartAsync(factory);
        using var client = ClientFor(factory, world.Insurer, world.UserId);

        var json = await MockedApiFactory.ReadJsonAsync(await client.GetAsync("/api/v1/integrations/whatsapp/configuration/elsewhere"));

        Assert.False(json.GetProperty("heldHere").GetBoolean());
        var held = Assert.Single(json.GetProperty("elsewhere").EnumerateArray(), item => item.GetProperty("phoneNumberId").GetString() == world.Phone);
        Assert.Equal("Mavuno Bank (test)", held.GetProperty("organisationName").GetString());
        // Nothing secret is offered.
        Assert.DoesNotContain("secret", json.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SomeoneWhoDoesNotAdministerTheOtherWorkspace_IsNotToldItHoldsANumber()
    {
        var world = await StartAsync(factory);
        using var stranger = ClientFor(factory, world.Insurer, "someone-else-1");

        var json = await MockedApiFactory.ReadJsonAsync(await stranger.GetAsync("/api/v1/integrations/whatsapp/configuration/elsewhere"));

        Assert.DoesNotContain(json.GetProperty("elsewhere").EnumerateArray(), item => item.GetProperty("phoneNumberId").GetString() == world.Phone);
    }

    [Fact]
    public async Task MovingTheNumber_GivesItToThisWorkspace_TakesItFromTheOther_AndKeepsTheCredentials()
    {
        var world = await StartAsync(factory);
        using var client = ClientFor(factory, world.Insurer, world.UserId);

        var response = await MoveAsync(client, world.Bank);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var services = factory.Services.CreateScope();
        var repository = services.ServiceProvider.GetRequiredService<IWhatsAppChannelConfigurationRepository>();
        var protector = services.ServiceProvider.GetRequiredService<IWhatsAppCredentialProtector>();
        Assert.Null(await repository.GetAsync(world.Bank.Scope));
        var moved = await repository.GetAsync(world.Insurer.Scope);
        Assert.NotNull(moved);
        Assert.Equal(world.Phone, moved!.PhoneNumberId);
        Assert.Equal("token-secret", protector.Unprotect(moved.EncryptedAccessToken));
        Assert.Single(await repository.ListActiveAsync(), item => item.PhoneNumberId == world.Phone);
    }

    [Fact]
    public async Task AMove_IsAuditedInBothWorkspaces()
    {
        var world = await StartAsync(factory);
        using var client = ClientFor(factory, world.Insurer, world.UserId);

        await MoveAsync(client, world.Bank);

        using var services = factory.Services.CreateScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditRepository>();
        Assert.Contains(await audit.ListAsync(world.Bank.Scope), item => item.ActionName == "integration.whatsapp.configuration_removed");
        Assert.Contains(await audit.ListAsync(world.Insurer.Scope), item => item.ActionName == "integration.whatsapp.configuration_saved");
    }

    [Fact]
    public async Task SomeoneWhoDoesNotAdministerTheWorkspaceGivingTheNumberUp_IsRefused_AndNothingMoves()
    {
        var world = await StartAsync(factory);
        using var stranger = ClientFor(factory, world.Insurer, "someone-else-2");

        var response = await MoveAsync(stranger, world.Bank);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var services = factory.Services.CreateScope();
        var repository = services.ServiceProvider.GetRequiredService<IWhatsAppChannelConfigurationRepository>();
        Assert.NotNull(await repository.GetAsync(world.Bank.Scope));
        Assert.Null(await repository.GetAsync(world.Insurer.Scope));
    }

    [Fact]
    public async Task AWorkspaceThatAlreadyHoldsANumber_IsToldToDisconnectItFirst()
    {
        var world = await StartAsync(factory);
        using (var services = factory.Services.CreateScope())
        {
            await SaveConnectionAsync(services.ServiceProvider, world.Insurer.Scope, $"phone-{Guid.NewGuid():N}"[..18], world.UserId);
        }

        using var client = ClientFor(factory, world.Insurer, world.UserId);
        var response = await MoveAsync(client, world.Bank);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.NotNull(await check.ServiceProvider.GetRequiredService<IWhatsAppChannelConfigurationRepository>().GetAsync(world.Bank.Scope));
    }

    [Fact]
    public async Task AWorkspaceWithNoNumber_HasNothingToGiveUp()
    {
        var world = await StartAsync(factory, connectToBank: false);
        using var client = ClientFor(factory, world.Insurer, world.UserId);

        var response = await MoveAsync(client, world.Bank);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMoveNeedsAnAuditReason()
    {
        var world = await StartAsync(factory);
        using var client = factory.CreateScopedClient(world.Insurer.OrganisationId, world.Insurer.Scope.WorkspaceId, "Admin", world.UserId);

        var response = await MoveAsync(client, world.Bank);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var services = factory.Services.CreateScope();
        Assert.NotNull(await services.ServiceProvider.GetRequiredService<IWhatsAppChannelConfigurationRepository>().GetAsync(world.Bank.Scope));
    }

    [Fact]
    public async Task ARoleWithoutTheIntegrationsPermission_CannotMoveANumber()
    {
        var world = await StartAsync(factory);
        using var viewer = factory.CreateScopedClient(world.Insurer.OrganisationId, world.Insurer.Scope.WorkspaceId, "Viewer", world.UserId);
        viewer.DefaultRequestHeaders.Add("X-Audit-Reason", "Trying");

        var response = await MoveAsync(viewer, world.Bank);

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        using var services = factory.Services.CreateScope();
        Assert.NotNull(await services.ServiceProvider.GetRequiredService<IWhatsAppChannelConfigurationRepository>().GetAsync(world.Bank.Scope));
    }
}

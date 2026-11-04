using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 22: staff send a claimant the link to their claim, by the claim's channel or one
/// they choose, respecting consent, audited, with the link never kept in a record.</summary>
public sealed class StaffClaimLinkApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly string Phone = "+256700555111";

    private static async Task<(Guid CaseId, string Email, string PhoneNumber)> CreateClaimAsync(HttpClient staff, string channel = "Phone", string? phone = null, string? email = null)
    {
        var contactEmail = email ?? $"link-{Guid.NewGuid():N}"[..16] + "@example.com";
        var contactPhone = phone ?? "+2567" + Random.Shared.Next(10_000_000, 99_999_999);
        var response = await staff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Eugene Katusiime",
            customerEmail = contactEmail,
            customerPhone = contactPhone,
            policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            productLine = "Motor",
            claimType = "Own damage",
            channel,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision on the highway.",
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return ((await MockedApiFactory.ReadJsonAsync(response)).GetProperty("caseId").GetGuid(), contactEmail, contactPhone);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid caseId, string? channel = null) =>
        client.PostAsJsonAsync($"/api/v1/cases/{caseId}/claim-link", new { channel });

    private static async Task<JsonElement[]> LinkMessagesAsync(HttpClient staff) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/dev/mock-outbox"))).EnumerateArray()
            .Where(message => message.GetProperty("body").GetString()!.Contains("/access/verify?token="))
            .ToArray();

    private static string TokenIn(JsonElement message) =>
        Uri.UnescapeDataString(Regex.Match(message.GetProperty("body").GetString()!, "token=([^\\s]+)").Groups[1].Value);

    private async Task SeedConsentAsync(string tenant, string identifier, string purpose, bool withdrawn)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IConsentRepository>().SaveAsync(new ConsentRecord
        {
            RecordId = Guid.NewGuid(),
            Scope = TenantScope.Create(tenant, "workspace-main"),
            CustomerIdentifier = identifier,
            Basis = withdrawn ? ConsentBasis.Withdrawn : ConsentBasis.ExplicitOptIn,
            Purpose = purpose,
            Channel = "test",
            WithdrawnAt = withdrawn ? DateTimeOffset.UtcNow : null,
            RecordedByUserId = "test",
            RecordedAt = DateTimeOffset.UtcNow,
            CorrelationId = "test",
        });
    }

    [Fact]
    public async Task AnOfficerSendsTheLinkAndTheCustomerCanOpenTheirClaimWithItExactlyOnce()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        using var officer = factory.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");
        var (caseId, _, phone) = await CreateClaimAsync(admin, phone: Phone);

        var response = await SendAsync(officer, caseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Sent", body.GetProperty("outcome").GetString());
        Assert.Equal("SMS", body.GetProperty("channel").GetString());
        Assert.Equal("+256*******11", body.GetProperty("sentTo").GetString());
        Assert.DoesNotContain(phone, body.GetRawText());

        var message = Assert.Single(await LinkMessagesAsync(admin));
        Assert.Equal(phone, message.GetProperty("recipient").GetString());
        Assert.Contains("CLM-", message.GetProperty("body").GetString());

        // The customer opens the claim with it, and the same link will not work twice.
        using var portal = factory.CreateClient();
        portal.DefaultRequestHeaders.Add("X-Tenant-Id", tenant);
        portal.DefaultRequestHeaders.Add("X-Workspace-Id", "workspace-main");
        var redeem = await portal.PostAsJsonAsync("/api/v1/client-portal/magic-links/redeem", new { token = TokenIn(message) });
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        Assert.Equal(caseId, (await MockedApiFactory.ReadJsonAsync(redeem)).GetProperty("caseId").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/client-portal/magic-links/redeem", new { token = TokenIn(message) })).StatusCode);
    }

    [Fact]
    public async Task TheSendIsAuditedWithTheChannelAndOutcomeButNotTheLink()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, caseId)).StatusCode);

        var audit = await admin.GetStringAsync("/api/v1/audit/events?pageSize=200");
        Assert.Contains("claim.link.sent", audit);
        Assert.DoesNotContain("/access/verify", audit);
        var token = TokenIn(Assert.Single(await LinkMessagesAsync(admin)));
        Assert.DoesNotContain(token, audit);
    }

    [Fact]
    public async Task StaffCanChooseEmailAndTheLinkIsNotKeptInTheClaimsMessageRecords()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, email, _) = await CreateClaimAsync(admin);

        var response = await SendAsync(admin, caseId, "Email");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Email", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("channel").GetString());
        Assert.Equal(email, Assert.Single(await LinkMessagesAsync(admin)).GetProperty("recipient").GetString());
        var token = TokenIn(Assert.Single(await LinkMessagesAsync(admin)));
        Assert.DoesNotContain(token, await admin.GetStringAsync($"/api/v1/cases/{caseId}/customer-updates"));
        Assert.DoesNotContain(token, await admin.GetStringAsync($"/api/v1/cases/{caseId}"));
    }

    [Fact]
    public async Task ATextOrEmailIsNeverSentAfterTheCustomerWithdrewConsentForThatChannel()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, phone) = await CreateClaimAsync(admin, phone: Phone);
        await SeedConsentAsync(tenant, phone, ConsentPurposes.Sms, withdrawn: true);

        var response = await SendAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ConsentWithdrawn", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("outcome").GetString());
        Assert.Empty(await LinkMessagesAsync(admin));
    }

    [Fact]
    public async Task WhatsAppNeedsARecordedConsentAndThenSendsInsideTheWindowButKeepsNoLink()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin, channel: "WhatsApp", phone: Phone);

        var without = await SendAsync(admin, caseId);
        Assert.Equal(HttpStatusCode.Conflict, without.StatusCode);
        Assert.Equal("ConsentRequired", (await MockedApiFactory.ReadJsonAsync(without)).GetProperty("outcome").GetString());

        var waId = new string(Phone.Where(char.IsDigit).ToArray());
        await SeedConsentAsync(tenant, waId, ConsentPurposes.WhatsApp, withdrawn: false);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRepository>().SaveAsync(new WhatsAppMessage
            {
                MessageId = Guid.NewGuid(),
                Scope = TenantScope.Create(tenant, "workspace-main"),
                ThreadId = InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService.BuildThreadId(MockWhatsAppSenderResolver.MockBusinessPhoneNumberId, waId),
                BusinessPhoneNumberId = MockWhatsAppSenderResolver.MockBusinessPhoneNumberId,
                ContactWaId = waId,
                Direction = WhatsAppMessageDirection.Inbound,
                ContentType = WhatsAppContentType.Text,
                CustomerSafeBody = "Hello",
                DeliveryState = WhatsAppDeliveryState.Received,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                CorrelationId = "test",
            });
        }

        var response = await SendAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("WhatsApp", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("channel").GetString());
        Assert.Single(await LinkMessagesAsync(admin));
        using var read = factory.Services.CreateScope();
        var thread = await read.ServiceProvider.GetRequiredService<IWhatsAppMessageRepository>().ListThreadAsync(
            TenantScope.Create(tenant, "workspace-main"),
            InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService.BuildThreadId(MockWhatsAppSenderResolver.MockBusinessPhoneNumberId, waId));
        var outbound = Assert.Single(thread, message => message.Direction == WhatsAppMessageDirection.Outbound);
        Assert.DoesNotContain("/access/verify", outbound.CustomerSafeBody);
        Assert.Equal("A secure link to the claim was sent to the customer.", outbound.CustomerSafeBody);
    }

    [Fact]
    public async Task OutsideTheWindowWhatsAppRefusesAndAnUnusedLinkIsNotLeftWorking()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin, channel: "WhatsApp", phone: Phone);
        await SeedConsentAsync(tenant, new string(Phone.Where(char.IsDigit).ToArray()), ConsentPurposes.WhatsApp, withdrawn: false);

        var response = await SendAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TemplateRequired", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("outcome").GetString());
        Assert.Empty(await LinkMessagesAsync(admin));
    }

    [Fact]
    public async Task AChannelTheClaimHasNoContactForIsRefusedWithAReason()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin);

        var bad = await SendAsync(admin, caseId, "Carrier pigeon");

        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        Assert.Equal("ChannelHasNoContact", (await MockedApiFactory.ReadJsonAsync(bad)).GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData("Broker")]
    [InlineData("Compliance")]
    [InlineData("Client")]
    public async Task OnlyRolesThatMayMessageCustomersCanSendALink(string role)
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin);
        using var other = factory.CreateScopedClient(tenant, role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(other, caseId)).StatusCode);
        Assert.Empty(await LinkMessagesAsync(admin));
    }

    [Fact]
    public async Task AnAutomatedActorCannotSendALink()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin);
        using var bot = factory.CreateScopedClient(tenant, role: "Admin", userId: "system:claim-triage");

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(bot, caseId)).StatusCode);
        Assert.Empty(await LinkMessagesAsync(admin));
    }

    [Fact]
    public async Task AnotherWorkspaceCannotSendALinkForThisClaim()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _, _) = await CreateClaimAsync(admin);
        using var intruder = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var otherWorkspace = factory.CreateScopedClient(tenant, workspaceId: "workspace-other");

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(intruder, caseId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(otherWorkspace, caseId)).StatusCode);
        Assert.Empty(await LinkMessagesAsync(admin));
        Assert.Empty(await LinkMessagesAsync(intruder));
    }
}

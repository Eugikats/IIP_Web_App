using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Api.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 21: a status change tells the customer, in the workspace's own wording, on the
/// channel they reached us by, with the approval rules, consent and WhatsApp's 24 hour window respected.</summary>
public sealed class ClaimStatusNotificationApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly string Phone = "+256700123456";

    private static async Task<Guid> CreateClaimAsync(HttpClient staff, string channel = "Phone", string? phone = null, string? email = null)
    {
        var response = await staff.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Eugene Katusiime",
            customerEmail = email ?? $"customer-{Guid.NewGuid():N}"[..20] + "@example.com",
            customerPhone = phone ?? "+2567" + Random.Shared.Next(10_000_000, 99_999_999),
            policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            productLine = "Motor",
            claimType = "Own damage",
            channel,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision on the highway.",
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("caseId").GetGuid();
    }

    private static async Task SetNotificationsAsync(HttpClient admin, bool notify, bool autoSend, object? whatsAppTemplates = null)
    {
        var response = await admin.PutAsJsonAsync("/api/v1/admin/status-notifications", new
        {
            notifyCustomerOnStatusChange = notify,
            autoSendStatusMessages = autoSend,
            whatsAppTemplates,
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, Guid caseId, string to) =>
        client.PostAsJsonAsync($"/api/v1/cases/{caseId}/status", new { toStatus = to, reason = "Working the claim." });

    private static async Task<JsonElement[]> UpdatesAsync(HttpClient staff, Guid caseId) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{caseId}/customer-updates"))).EnumerateArray().ToArray();

    /// <summary>What the mock senders were asked to send for a status change. Case intake also sends its own
    /// acknowledgement, which is not what these tests are about.</summary>
    private static async Task<JsonElement[]> OutboxAsync(HttpClient staff) =>
        (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/dev/mock-outbox"))).EnumerateArray()
            .Where(message => message.GetProperty("body").GetString()!.Contains("An update on your claim") || message.GetProperty("body").GetString()!.StartsWith("[template"))
            .ToArray();

    /// <summary>The outbox lists the channel as the enum's number.</summary>
    private static string ChannelOf(JsonElement message) =>
        message.GetProperty("channel").ValueKind == JsonValueKind.Number
            ? ((InsuranceIntelligence.Domain.Workflow.CommunicationChannel)message.GetProperty("channel").GetInt32()).ToString()
            : message.GetProperty("channel").GetString()!;

    private static string Ref(Guid caseId) => $"CLM-{caseId.ToString("N")[..8].ToUpperInvariant()}";

    [Fact]
    public async Task NothingIsSentUntilTheWorkspaceTurnsStatusMessagesOn()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var caseId = await CreateClaimAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(admin, caseId, "InReview")).StatusCode);

        Assert.Empty(await UpdatesAsync(admin, caseId));
        Assert.Empty(await OutboxAsync(admin));
        var settings = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/status-notifications"));
        Assert.False(settings.GetProperty("notifyCustomerOnStatusChange").GetBoolean());
        Assert.False(settings.GetProperty("autoSendStatusMessages").GetBoolean());
    }

    [Fact]
    public async Task WhenOnButNotAutomaticTheMessageWaitsForAPersonInTheWorkspacesWording()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: false);
        var caseId = await CreateClaimAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(admin, caseId, "InReview")).StatusCode);

        var update = Assert.Single(await UpdatesAsync(admin, caseId));
        Assert.Equal("Draft", update.GetProperty("status").GetString());
        Assert.Equal("SMS", update.GetProperty("channel").GetString());
        Assert.Contains($"claim {Ref(caseId)}: We are reviewing your claim.", update.GetProperty("messageBody").GetString());
        Assert.StartsWith("Hello Eugene.", update.GetProperty("messageBody").GetString());
        Assert.Empty(await OutboxAsync(admin));
    }

    [Fact]
    public async Task WhenAutomaticTheMessageGoesOutAtOnceOnTheCustomersChannelAndIsAudited()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin, phone: Phone);

        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(admin, caseId, "InReview")).StatusCode);

        var sent = Assert.Single(await OutboxAsync(admin));
        Assert.Equal("SMS", ChannelOf(sent));
        Assert.Equal(Phone, sent.GetProperty("recipient").GetString());
        Assert.Contains("We are reviewing your claim", sent.GetProperty("body").GetString());
        var update = Assert.Single(await UpdatesAsync(admin, caseId));
        Assert.Equal("Sent", update.GetProperty("status").GetString());
        var audit = await admin.GetStringAsync("/api/v1/audit/events?pageSize=200");
        Assert.Contains("claim.status.notified", audit);
    }

    [Fact]
    public async Task TheWorkspacesOwnWordingIsWhatTheCustomerIsTold()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var put = await admin.PutAsJsonAsync("/api/v1/admin/claim-status-wording", new
        {
            wording = new Dictionary<string, object> { ["InReview"] = new { headline = "Our team is on it", detail = "You will hear from us within two working days." } },
        });
        Assert.True(put.IsSuccessStatusCode);
        var caseId = await CreateClaimAsync(admin);

        await ChangeAsync(admin, caseId, "InReview");

        var body = Assert.Single(await OutboxAsync(admin)).GetProperty("body").GetString();
        Assert.Contains("Our team is on it. You will hear from us within two working days.", body);
    }

    [Fact]
    public async Task AMessageThePolicyCallsSensitiveStillNeedsAPersonEvenWhenAutomaticIsOn()
    {
        const string status = "ReadyForReview";
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin);

        await ChangeAsync(admin, caseId, "InReview");
        Assert.Single(await OutboxAsync(admin));
        await ChangeAsync(admin, caseId, status);

        Assert.Single(await OutboxAsync(admin));
        var queued = (await UpdatesAsync(admin, caseId)).Single(update => update.GetProperty("status").GetString() == "PendingApproval");
        Assert.Contains("ready for a final look", queued.GetProperty("messageBody").GetString());
    }

    [Fact]
    public async Task AnOutcomeMessageIsNeverSentWithoutAPersonApprovingIt()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin);

        var escalate = await admin.PostAsJsonAsync($"/api/v1/cases/{caseId}/escalate", new { auditReason = "Needs a senior look.", assignedTeam = "Claims" });
        Assert.True(escalate.IsSuccessStatusCode);
        using var resolve = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{caseId}/resolve") { Content = JsonContent.Create(new { resolution = "Settled" }) };
        resolve.Headers.Add("X-Audit-Reason", "Paid.");
        Assert.True((await admin.SendAsync(resolve)).IsSuccessStatusCode);

        Assert.Empty(await OutboxAsync(admin));
        var updates = await UpdatesAsync(admin, caseId);
        var outcome = updates.Single(update => update.GetProperty("messageBody").GetString()!.Contains("has been settled"));
        Assert.Equal("PendingApproval", outcome.GetProperty("status").GetString());
        Assert.Contains(updates, update => update.GetProperty("messageBody").GetString()!.Contains("senior colleague"));
    }

    [Fact]
    public async Task NewAndWaitingOnDocumentsSendNoStatusMessageOfTheirOwn()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(admin, caseId, "MissingDocuments")).StatusCode);

        Assert.Empty(await UpdatesAsync(admin, caseId));
        Assert.Empty(await OutboxAsync(admin));
    }

    [Fact]
    public async Task TheOtherPathsThatChangeAStatusTellTheCustomerToo()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin);

        var assign = await admin.PostAsJsonAsync($"/api/v1/cases/{caseId}/assign", new { assignedUserId = "officer-1", auditReason = "Taking this on." });
        Assert.True(assign.IsSuccessStatusCode);

        var sent = Assert.Single(await OutboxAsync(admin));
        Assert.Contains("Someone is looking after your claim", sent.GetProperty("body").GetString());
    }

    [Fact]
    public async Task OneWorkspacesSettingsAndMessagesNeverTouchAnother()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var adminA = factory.CreateScopedClient(tenantA);
        using var adminB = factory.CreateScopedClient(tenantB);
        await SetNotificationsAsync(adminA, notify: true, autoSend: true);
        var claimB = await CreateClaimAsync(adminB);
        var claimA = await CreateClaimAsync(adminA);

        await ChangeAsync(adminB, claimB, "InReview");
        await ChangeAsync(adminA, claimA, "InReview");

        Assert.Empty(await UpdatesAsync(adminB, claimB));
        Assert.Empty(await OutboxAsync(adminB));
        Assert.Single(await OutboxAsync(adminA));
        Assert.False((await MockedApiFactory.ReadJsonAsync(await adminB.GetAsync("/api/v1/admin/status-notifications"))).GetProperty("notifyCustomerOnStatusChange").GetBoolean());
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("ClaimsSupervisor")]
    [InlineData("Broker")]
    public async Task OnlyAdministratorsCanReadOrChangeTheNotificationSettings(string role)
    {
        using var other = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/v1/admin/status-notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync("/api/v1/admin/status-notifications", new { notifyCustomerOnStatusChange = true })).StatusCode);
    }

    [Fact]
    public async Task WhatsAppTemplatesAreCheckedWhenSaved()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var bad = await admin.PutAsJsonAsync("/api/v1/admin/status-notifications", new
        {
            whatsAppTemplates = new Dictionary<string, object> { ["InReview"] = new { name = "Has Spaces", language = "en" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var good = await admin.PutAsJsonAsync("/api/v1/admin/status-notifications", new
        {
            whatsAppTemplates = new Dictionary<string, object> { ["InReview"] = new { name = "claim_in_review", language = "en" } },
        });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        var row = (await MockedApiFactory.ReadJsonAsync(good)).GetProperty("statuses").EnumerateArray().Single(item => item.GetProperty("status").GetString() == "InReview");
        Assert.Equal("claim_in_review", row.GetProperty("whatsAppTemplateName").GetString());
    }

    // ---- WhatsApp ----

    private async Task SeedWhatsAppAsync(string tenant, string phone, bool consent, TimeSpan? lastInboundAge)
    {
        using var scope = factory.Services.CreateScope();
        var tenantScope = TenantScope.Create(tenant, "workspace-main");
        var waId = new string(phone.Where(char.IsDigit).ToArray());
        if (consent)
        {
            await scope.ServiceProvider.GetRequiredService<IConsentRepository>().SaveAsync(new ConsentRecord
            {
                RecordId = Guid.NewGuid(),
                Scope = tenantScope,
                CustomerIdentifier = waId,
                Basis = ConsentBasis.CustomerInitiatedConversation,
                Purpose = ConsentPurposes.WhatsApp,
                Channel = "WhatsApp",
                RecordedByUserId = "test",
                RecordedAt = DateTimeOffset.UtcNow,
                CorrelationId = "test",
            });
        }

        if (lastInboundAge is { } age)
        {
            var at = DateTimeOffset.UtcNow - age;
            await scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRepository>().SaveAsync(new WhatsAppMessage
            {
                MessageId = Guid.NewGuid(),
                Scope = tenantScope,
                ThreadId = InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService.BuildThreadId(MockWhatsAppSenderResolver.MockBusinessPhoneNumberId, waId),
                BusinessPhoneNumberId = MockWhatsAppSenderResolver.MockBusinessPhoneNumberId,
                ContactWaId = waId,
                Direction = WhatsAppMessageDirection.Inbound,
                ContentType = WhatsAppContentType.Text,
                CustomerSafeBody = "Hello",
                DeliveryState = WhatsAppDeliveryState.Received,
                CreatedAt = at,
                UpdatedAt = at,
                CorrelationId = "test",
            });
        }
    }

    [Fact]
    public async Task WhatsAppNeedsTheCustomersRecordedConsentAndSaysSoWhenItHasNone()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin, channel: "WhatsApp", phone: Phone);
        await SeedWhatsAppAsync(tenant, Phone, consent: false, lastInboundAge: TimeSpan.FromMinutes(5));

        await ChangeAsync(admin, caseId, "InReview");

        Assert.Empty(await OutboxAsync(admin));
        var update = Assert.Single(await UpdatesAsync(admin, caseId));
        Assert.Equal("WhatsApp", update.GetProperty("channel").GetString());
        Assert.NotEqual("Sent", update.GetProperty("status").GetString());
        Assert.Equal("ConsentRequired", update.GetProperty("deliveryStatus").GetString());
    }

    [Fact]
    public async Task InsideTheTwentyFourHourWindowAWhatsAppMessageIsPlainText()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin, channel: "WhatsApp", phone: Phone);
        await SeedWhatsAppAsync(tenant, Phone, consent: true, lastInboundAge: TimeSpan.FromHours(3));

        await ChangeAsync(admin, caseId, "InReview");

        var sent = Assert.Single(await OutboxAsync(admin));
        Assert.Equal("WhatsApp", ChannelOf(sent));
        Assert.Equal("256700123456", sent.GetProperty("recipient").GetString());
        Assert.Contains("We are reviewing your claim", sent.GetProperty("body").GetString());
        Assert.DoesNotContain("[template", sent.GetProperty("body").GetString());
        Assert.Equal("Sent", Assert.Single(await UpdatesAsync(admin, caseId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task OutsideTheWindowOnlyAnApprovedTemplateIsSentAndWithoutOneNothingIsPretended()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true);
        var caseId = await CreateClaimAsync(admin, channel: "WhatsApp", phone: Phone);
        await SeedWhatsAppAsync(tenant, Phone, consent: true, lastInboundAge: TimeSpan.FromHours(30));

        await ChangeAsync(admin, caseId, "InReview");

        Assert.Empty(await OutboxAsync(admin));
        var waiting = Assert.Single(await UpdatesAsync(admin, caseId));
        Assert.Equal("TemplateRequired", waiting.GetProperty("deliveryStatus").GetString());
        Assert.NotEqual("Sent", waiting.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ApprovingAndSendingAWhatsAppStatusMessageOutsideTheWindowSendsTheTemplate()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await SetNotificationsAsync(admin, notify: true, autoSend: true, whatsAppTemplates: new Dictionary<string, object>
        {
            ["ReadyForReview"] = new { name = "claim_ready_for_review", language = "en" },
        });
        var caseId = await CreateClaimAsync(admin, channel: "WhatsApp", phone: Phone);
        await SeedWhatsAppAsync(tenant, Phone, consent: true, lastInboundAge: TimeSpan.FromHours(48));
        await ChangeAsync(admin, caseId, "InReview");
        await ChangeAsync(admin, caseId, "ReadyForReview");
        var pending = (await UpdatesAsync(admin, caseId)).Single(update => update.GetProperty("status").GetString() == "PendingApproval");
        var updateId = pending.GetProperty("updateId").GetGuid();

        Assert.True((await admin.PostAsJsonAsync($"/api/v1/customer-updates/{updateId}/approve", new { })).IsSuccessStatusCode);
        var send = await admin.PostAsJsonAsync($"/api/v1/customer-updates/{updateId}/send", new { recipient = Phone });
        Assert.True(send.IsSuccessStatusCode, await send.Content.ReadAsStringAsync());

        var sentMessages = (await OutboxAsync(admin)).Where(message => ChannelOf(message) == "WhatsApp").ToArray();
        var sent = sentMessages.Single(message => message.GetProperty("body").GetString()!.StartsWith("[template claim_ready_for_review (en)]"));
        Assert.Contains($"claim_reference={Ref(caseId)}", sent.GetProperty("body").GetString());
        Assert.Contains("status=Your claim is ready for a final look", sent.GetProperty("body").GetString());
    }
}

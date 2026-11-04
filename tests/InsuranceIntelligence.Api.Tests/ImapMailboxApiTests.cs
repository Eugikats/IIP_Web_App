using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 28 over HTTP, on the pretend mail host: connect an IMAP mailbox, collect, turn mail into a claim with its
/// files, and keep every workspace's mailbox, password and mail to itself.</summary>
public sealed class ImapMailboxApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Reason = "Connecting the claims mailbox for the test.";

    private static async Task<HttpResponseMessage> AddAsync(HttpClient admin, string host, string username, string password = "app-password", string? product = null, string? folder = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/email/mailboxes")
        {
            Content = JsonContent.Create(new
            {
                displayName = "Claims",
                mailboxAddress = username,
                ingestionEnabled = true,
                defaultChannel = "Email",
                productLine = product,
                processedFolderName = folder,
                source = "Imap",
                imapHost = host,
                imapPort = 993,
                imapUsername = username,
                imapPassword = password,
            }),
        };
        request.Headers.Add("X-Audit-Reason", Reason);
        return await admin.SendAsync(request);
    }

    private static Task<HttpResponseMessage> DeliverAsync(HttpClient admin, string host, string username, string subject, params (string File, string Text)[] files) =>
        admin.PostAsJsonAsync("/api/v1/dev/mock-mailbox/deliver", new
        {
            host,
            username,
            from = "customer@example.test",
            subject,
            body = "My car was hit near Kira roundabout.",
            attachments = files.Select(file => new { fileName = file.File, contentType = "application/pdf", text = file.Text }).ToArray(),
        });

    private static Task<HttpResponseMessage> CollectAsync(HttpClient admin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/email/ingest");
        request.Headers.Add("X-Audit-Reason", "Collect now.");
        return admin.SendAsync(request);
    }

    [Fact]
    public async Task AnImapMailboxIsTriedAgainstTheHostBeforeItIsSaved_AndItsPasswordIsNeverSentBack()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var host = $"imap.{tenant}.test";

        var response = await AddAsync(admin, host, "claims@insurer.test", password: "very-secret-pass");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Imap", created.GetProperty("source").GetString());
        Assert.True(created.GetProperty("imapPasswordSet").GetBoolean());
        var listing = await (await admin.GetAsync("/api/v1/integrations/email/mailboxes")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("very-secret-pass", listing);
        Assert.DoesNotContain("encryptedImapPassword", listing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("very-secret-pass", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("wrong-password", "imap.example.test", "refused the username or password")]
    [InlineData("app-password", "imap.unreachable.test", "could not be reached")]
    [InlineData("app-password", "https://imap.example.test", "plain host name")]
    [InlineData("app-password", "imap.example.test:993", "plain host name")]
    public async Task AMailboxThatCannotWorkIsRefusedWithAReasonAndNotSaved(string password, string host, string expected)
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);

        var response = await AddAsync(admin, host, "claims@insurer.test", password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
        var saved = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/integrations/email/mailboxes"));
        Assert.Equal(0, saved.GetArrayLength());
    }

    [Fact]
    public async Task AnImapMailboxNeedsAHostAUsernameAndAPassword()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var noPassword = await AddAsync(admin, "imap.example.test", "claims@insurer.test", password: "");
        var noUser = await admin.PostAsJsonAsync("/api/v1/integrations/email/mailboxes", new
        {
            displayName = "Claims", mailboxAddress = "claims@insurer.test", ingestionEnabled = true, defaultChannel = "Email",
            source = "Imap", imapHost = "imap.example.test", imapPassword = "x",
        });

        Assert.Equal(HttpStatusCode.BadRequest, noPassword.StatusCode);
        Assert.Contains("password", await noPassword.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, noUser.StatusCode);
    }

    [Fact]
    public async Task MailIsCollectedIntoTheWorkspace_TurnedIntoAClaimWithItsFiles_AndMarkedReadAtTheHost()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var host = $"imap.{tenant}.test";
        await AddAsync(admin, host, "claims@insurer.test", product: "Motor", folder: "Processed by IIP");
        await DeliverAsync(admin, host, "claims@insurer.test", "Accident at Kira", ("police_report.pdf", "plain text of a police letter"));

        var collected = await MockedApiFactory.ReadJsonAsync(await CollectAsync(admin));

        Assert.Equal(1, collected.GetProperty("mailboxesPolled").GetInt32());
        Assert.Equal(1, collected.GetProperty("messagesIngested").GetInt32());
        var messages = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/integrations/email/messages"));
        var message = messages.EnumerateArray().Single();
        Assert.Equal("Accident at Kira", message.GetProperty("subject").GetString());
        Assert.Equal("police_report.pdf", message.GetProperty("attachments")[0].GetProperty("fileName").GetString());

        // Collecting again finds the same unread message: it is not duplicated.
        var again = await MockedApiFactory.ReadJsonAsync(await CollectAsync(admin));
        Assert.Equal(0, again.GetProperty("messagesIngested").GetInt32());
        Assert.Equal(1, again.GetProperty("alreadyPresent").GetInt32());

        using var process = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/email/process-message")
        {
            Content = JsonContent.Create(new { messageId = message.GetProperty("messageId").GetGuid() }),
        };
        process.Headers.Add("X-Audit-Reason", "Turn the email into a claim.");
        var processed = await admin.SendAsync(process);
        Assert.True(processed.IsSuccessStatusCode, await processed.Content.ReadAsStringAsync());
        var result = await MockedApiFactory.ReadJsonAsync(processed);
        var caseId = result.GetProperty("caseId").GetGuid();

        var documents = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{caseId}/documents"));
        var document = documents.EnumerateArray().Single();
        Assert.Equal("police_report.pdf", document.GetProperty("originalFileName").GetString());

        // The host now has it marked read, so it is not offered again.
        var third = await MockedApiFactory.ReadJsonAsync(await CollectAsync(admin));
        Assert.Equal(0, third.GetProperty("messagesIngested").GetInt32());
        Assert.Equal(0, third.GetProperty("alreadyPresent").GetInt32());
    }

    [Fact]
    public async Task TwoWorkspacesOnTheSameMailHostEachKeepTheirOwnCopyAndTheirOwnPassword()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var adminA = factory.CreateScopedClient(tenantA);
        using var adminB = factory.CreateScopedClient(tenantB);
        var host = $"imap.shared-{tenantA}.test";
        await AddAsync(adminA, host, "claims@insurer.test", password: "password-of-a");
        await AddAsync(adminB, host, "claims@insurer.test", password: "password-of-b");
        await DeliverAsync(adminA, host, "claims@insurer.test", "Shared host mail");

        await CollectAsync(adminA);
        await CollectAsync(adminB);

        var a = await MockedApiFactory.ReadJsonAsync(await adminA.GetAsync("/api/v1/integrations/email/messages"));
        var b = await MockedApiFactory.ReadJsonAsync(await adminB.GetAsync("/api/v1/integrations/email/messages"));
        Assert.Single(a.EnumerateArray());
        Assert.Single(b.EnumerateArray());
        Assert.NotEqual(a[0].GetProperty("messageId").GetGuid(), b[0].GetProperty("messageId").GetGuid());
        Assert.DoesNotContain("password-of-", await (await adminA.GetAsync("/api/v1/integrations/email/mailboxes")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await adminB.GetAsync($"/api/v1/integrations/email/messages/{a[0].GetProperty("messageId").GetGuid()}")).StatusCode);
    }

    [Fact]
    public async Task AWorkspaceCannotChangeAnotherWorkspacesMailbox_AndOnlyAdministratorsMayConnectOne()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var adminA = factory.CreateScopedClient(tenantA);
        using var adminB = factory.CreateScopedClient(tenantB);
        using var officer = factory.CreateScopedClient(tenantA, role: "ClaimsOfficer", userId: "officer-1");
        var created = await MockedApiFactory.ReadJsonAsync(await AddAsync(adminA, $"imap.{tenantA}.test", "claims@insurer.test"));
        var id = created.GetProperty("mailboxConnectionId").GetGuid();

        using var crossed = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/integrations/email/mailboxes/{id}")
        {
            Content = JsonContent.Create(new { ingestionEnabled = false }),
        };
        crossed.Headers.Add("X-Audit-Reason", "Not my mailbox.");
        var notFound = await adminB.SendAsync(crossed);
        var forbidden = await AddAsync(officer, $"imap.{tenantA}.test", "other@insurer.test");

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ChangingAMailbox_KeepsThePasswordWhenBlank_TriesANewOneAgainstTheHost_AndCanSwitchCollectionOff()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var created = await MockedApiFactory.ReadJsonAsync(await AddAsync(admin, $"imap.{tenant}.test", "claims@insurer.test"));
        var id = created.GetProperty("mailboxConnectionId").GetGuid();

        async Task<HttpResponseMessage> PutAsync(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/integrations/email/mailboxes/{id}") { Content = JsonContent.Create(body) };
            request.Headers.Add("X-Audit-Reason", "Changing the mailbox.");
            return await admin.SendAsync(request);
        }

        var off = await PutAsync(new { ingestionEnabled = false, displayName = "Claims (paused)" });
        var badPassword = await PutAsync(new { imapPassword = "wrong-password" });
        var rotated = await PutAsync(new { imapPassword = "a-new-password" });

        Assert.True(off.StatusCode == HttpStatusCode.OK, await off.Content.ReadAsStringAsync());
        var paused = await MockedApiFactory.ReadJsonAsync(off);
        Assert.False(paused.GetProperty("ingestionEnabled").GetBoolean());
        Assert.True(paused.GetProperty("imapPasswordSet").GetBoolean());
        Assert.Equal("Claims (paused)", paused.GetProperty("displayName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, badPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        // Switched off, so there is nothing to collect and the screen is told so plainly.
        Assert.Equal(HttpStatusCode.BadRequest, (await CollectAsync(admin)).StatusCode);
    }

    [Fact]
    public async Task AMicrosoft365MailboxCanNameItsOwnDirectory_AndAMalformedOneIsRefused()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        async Task<HttpResponseMessage> AddGraphAsync(string address, string directory)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/email/mailboxes")
            {
                Content = JsonContent.Create(new { displayName = "M365", mailboxAddress = address, ingestionEnabled = false, defaultChannel = "Email", source = "Graph", entraTenantId = directory }),
            };
            request.Headers.Add("X-Audit-Reason", "Connect Microsoft 365.");
            return await admin.SendAsync(request);
        }

        var good = await AddGraphAsync("claims@insurer.test", Guid.NewGuid().ToString());
        var bad = await AddGraphAsync("motor@insurer.test", "not a directory!");

        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        Assert.Equal("Graph", (await MockedApiFactory.ReadJsonAsync(good)).GetProperty("source").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}

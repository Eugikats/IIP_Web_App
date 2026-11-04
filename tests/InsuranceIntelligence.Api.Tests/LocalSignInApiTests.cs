using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Self-serve company sign-up, the platform's own sign-in and staff invitations (build-loop item 30), end to end on the pretend mail host:
/// a company opens a workspace, signs in with a password, invites a colleague, and nothing it does reaches another company.
/// </summary>
public sealed class LocalSignInApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private const string Password = "a-long-enough-phrase-7";
    private static readonly TenantMail PlatformMail = new("iip-platform", "identity");

    private sealed record TenantMail(string Tenant, string Workspace);

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private WebApplicationFactory<Program> With(ManualTime? time = null, Dictionary<string, string>? settings = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings ?? [])
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                if (time is not null)
                {
                    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time));
                }
            });
        });

    private static string NewEmail() => $"p{Guid.NewGuid():N}@example.test";

    // The test database is shared by every test in the run, so each test names its company uniquely; its link name follows from it.
    private static string Company(string prefix) => $"{prefix} {new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(6).ToArray()).PadRight(6, 'x')}";

    private static async Task<JsonElement[]> OutboxAsync(WebApplicationFactory<Program> host, TenantMail mail)
    {
        using var admin = host.CreateScopedClient(mail.Tenant, mail.Workspace);
        return (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/dev/mock-outbox"))).EnumerateArray().ToArray();
    }

    private static async Task<string> LinkTokenAsync(WebApplicationFactory<Program> host, TenantMail mail, string recipient, string pathPart)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var body = (await OutboxAsync(host, mail))
                .Where(message => message.GetProperty("recipient").GetString() == recipient)
                .Select(message => message.GetProperty("body").GetString()!)
                .LastOrDefault(text => text.Contains(pathPart, StringComparison.Ordinal));
            var match = body is null ? null : Regex.Match(body, @"token=([A-Za-z0-9_-]+)");
            if (match is { Success: true })
            {
                return Uri.UnescapeDataString(match.Groups[1].Value);
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException($"No email with {pathPart} reached {recipient}.");
    }

    private static object SignUpBody(string email, string company = "Acme Insurance", string password = Password, bool terms = true, string? website = null) =>
        new { companyName = company, adminName = "Amina Okello", email, password, acceptTerms = terms, website };

    private static async Task<string> OpenWorkspaceAsync(WebApplicationFactory<Program> host, string email, string company = "Acme Insurance")
    {
        using var visitor = host.CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(email, company))).StatusCode);
        var token = await LinkTokenAsync(host, PlatformMail, email, "signup/verify");
        var verified = await visitor.PostAsJsonAsync("/api/v1/signup/verify", new { token });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        return token;
    }

    private static async Task<(string Token, JsonElement User)> SignInAsync(
        WebApplicationFactory<Program> host, string email, string password = Password, string? workspaceId = null, string? tenantId = null)
    {
        using var visitor = host.CreateClient();
        var response = await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password, workspaceId, tenantId });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await MockedApiFactory.ReadJsonAsync(response);
        return (body.GetProperty("accessToken").GetString()!, body.GetProperty("user"));
    }

    private static HttpClient Bearer(WebApplicationFactory<Program> host, string token)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---- opening a workspace ----

    [Fact]
    public async Task ACompanyOpensAWorkspaceByConfirmingItsEmail_AndGetsItsAdministratorPortalNameAndStarterProducts()
    {
        using var host = With();
        var email = NewEmail();
        var company = Company("Acme Insurance");
        await OpenWorkspaceAsync(host, email, company);

        var (token, user) = await SignInAsync(host, email);
        using var admin = Bearer(host, token);

        Assert.Equal("Admin", user.GetProperty("role").GetString());
        var settings = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/tenant-settings"));
        Assert.Equal(company, settings.GetProperty("assistantOrganisation").GetString());
        var identity = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/portal-identity"));
        Assert.Equal(company.ToLowerInvariant().Replace(' ', '-'), identity.GetProperty("slug").GetString());
        Assert.Equal(company, identity.GetProperty("displayName").GetString());
        var products = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/products"));
        Assert.Equal(["Home and property", "Medical inpatient", "Motor comprehensive"], products.EnumerateArray().Select(product => product.GetProperty("name").GetString()!).Order().ToArray());
        Assert.All(products.EnumerateArray(), product => Assert.Equal("Draft", product.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task EachCompanyGetsItsOwnWorkspace_AndOnlyItsOwn()
    {
        using var host = With();
        var emailA = NewEmail();
        var emailB = NewEmail();
        var company = Company("Twin Insurance");
        await OpenWorkspaceAsync(host, emailA, company);
        await OpenWorkspaceAsync(host, emailB, company);

        var (tokenA, userA) = await SignInAsync(host, emailA);
        var (tokenB, userB) = await SignInAsync(host, emailB);
        using var adminA = Bearer(host, tokenA);
        using var adminB = Bearer(host, tokenB);

        Assert.NotEqual(userA.GetProperty("tenantId").GetString(), userB.GetProperty("tenantId").GetString());
        var slugA = (await MockedApiFactory.ReadJsonAsync(await adminA.GetAsync("/api/v1/admin/portal-identity"))).GetProperty("slug").GetString();
        var slugB = (await MockedApiFactory.ReadJsonAsync(await adminB.GetAsync("/api/v1/admin/portal-identity"))).GetProperty("slug").GetString();
        Assert.NotEqual(slugA, slugB); // two companies with the same name still get different link names
        var productsA = await MockedApiFactory.ReadJsonAsync(await adminA.GetAsync("/api/v1/products"));
        var productsB = await MockedApiFactory.ReadJsonAsync(await adminB.GetAsync("/api/v1/products"));
        Assert.Empty(productsA.EnumerateArray().Select(p => p.GetProperty("productId").GetGuid()).Intersect(productsB.EnumerateArray().Select(p => p.GetProperty("productId").GetGuid())));
    }

    [Fact]
    public async Task ASignUpThatIsNotValidIsRefusedWithReasons()
    {
        using var host = With();
        using var visitor = host.CreateClient();

        var weak = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(NewEmail(), password: "short"));
        var noTerms = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(NewEmail(), terms: false));
        var badEmail = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody("not-an-email"));
        var noCompany = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(NewEmail(), company: " "));

        Assert.All(new[] { weak, noTerms, badEmail, noCompany }, response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        Assert.Contains("at least 12", await weak.Content.ReadAsStringAsync());
        Assert.Contains("terms", await noTerms.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ASignUpFromAnEmailThatAlreadyHasAnAccount_LooksTheSameButTheEmailPointsToSignIn()
    {
        using var host = With();
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();

        var again = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(email));

        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        var mails = (await OutboxAsync(host, PlatformMail)).Where(m => m.GetProperty("recipient").GetString() == email).Select(m => m.GetProperty("body").GetString()!).ToArray();
        Assert.Contains(mails, body => body.Contains("already has an account", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARobotThatFillsTheHiddenFieldGetsASmilingNothing()
    {
        using var host = With();
        var email = NewEmail();
        using var visitor = host.CreateClient();

        var response = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(email, website: "http://spam.example"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(await OutboxAsync(host, PlatformMail), m => m.GetProperty("recipient").GetString() == email);
    }

    [Fact]
    public async Task AConfirmationLinkWorksOnce_AndNotAfterItExpires()
    {
        var time = new ManualTime();
        using var host = With(time);
        var email = NewEmail();
        var expiring = NewEmail();
        var token = await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();

        var second = await visitor.PostAsJsonAsync("/api/v1/signup/verify", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(expiring));
        var late = await LinkTokenAsync(host, PlatformMail, expiring, "signup/verify");
        time.Advance(TimeSpan.FromHours(25));
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/signup/verify", new { token = late })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/signup/verify", new { token = "made-up" })).StatusCode);
    }

    [Fact]
    public async Task SelfServeSignUpCanBeSwitchedOff_ForADeploymentDedicatedToOneInsurer()
    {
        using var host = With(settings: new() { ["INSURANCEINTELLIGENCE_SELF_SERVE_SIGNUP"] = "false" });
        using var visitor = host.CreateClient();

        var capabilities = await MockedApiFactory.ReadJsonAsync(await visitor.GetAsync("/api/v1/auth/capabilities"));
        var attempt = await visitor.PostAsJsonAsync("/api/v1/signup", SignUpBody(NewEmail()));

        Assert.False(capabilities.GetProperty("signUp").GetBoolean());
        Assert.True(capabilities.GetProperty("passwordSignIn").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
    }

    // ---- signing in ----

    [Fact]
    public async Task ATokenFromSigningInOpensTheWorkspaceWithNoOtherHeaders_AndNeverAnotherOne()
    {
        using var host = With();
        var emailA = NewEmail();
        var emailB = NewEmail();
        var companyA = Company("Alpha Insurance");
        await OpenWorkspaceAsync(host, emailA, companyA);
        await OpenWorkspaceAsync(host, emailB, Company("Beta Insurance"));
        var (tokenA, userA) = await SignInAsync(host, emailA);
        var (_, userB) = await SignInAsync(host, emailB);
        using var adminA = Bearer(host, tokenA);

        var me = await MockedApiFactory.ReadJsonAsync(await adminA.GetAsync("/api/v1/me"));
        Assert.Equal(userA.GetProperty("tenantId").GetString(), me.GetProperty("tenantId").GetString());

        // Naming the other company in a header changes nothing for a signed-in person, and header identities are refused outright.
        using var spoof = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/tenant-settings");
        spoof.Headers.Add("X-Tenant-Id", userB.GetProperty("tenantId").GetString());
        spoof.Headers.Add("X-Workspace-Id", userB.GetProperty("workspaceId").GetString());
        var settings = await MockedApiFactory.ReadJsonAsync(await adminA.SendAsync(spoof));
        Assert.Equal(companyA, settings.GetProperty("assistantOrganisation").GetString());
        using var withRole = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/tenant-settings");
        withRole.Headers.Add("x-demo-role", "Admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await adminA.SendAsync(withRole)).StatusCode);
    }

    [Fact]
    public async Task AWrongPasswordAndAnUnknownEmailLookExactlyTheSame()
    {
        using var host = With();
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();

        var wrongPassword = await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = "not-the-password-1" });
        var unknown = await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = NewEmail(), password = "not-the-password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FiveWrongPasswordsPauseTheAccountForFifteenMinutes_EvenForTheRightPassword_ThenItWorksAgain()
    {
        var time = new ManualTime();
        using var host = With(time);
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();

        for (var i = 0; i < LocalIdentityService.MaxFailedAttempts; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = "wrong-password-xyz" })).StatusCode);
        }

        var paused = await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, paused.StatusCode);
        time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task ARightPasswordInBetweenWrongOnesClearsTheCount()
    {
        using var host = With();
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();

        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < LocalIdentityService.MaxFailedAttempts - 1; i++)
            {
                await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = "wrong-password-xyz" });
            }

            Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        }
    }

    [Fact]
    public async Task ATokenThatWasTamperedWithOrSignedByAnotherKeyOrIsExpiredIsRefused()
    {
        using var host = With();
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        var (token, user) = await SignInAsync(host, email);
        var handler = new JwtSecurityTokenHandler();
        var parts = token.Split('.');
        var tamperedPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1]))).Replace(user.GetProperty("workspaceId").GetString()!, "workspace-other"))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string Forged(byte[] key, DateTime expires) => handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = LocalAuthOptions.Issuer, Audience = LocalAuthOptions.Audience, NotBefore = expires.AddHours(-9), Expires = expires,
            Subject = new ClaimsIdentity([new Claim("sub", user.GetProperty("userId").GetString()!)]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        }));
        var realKey = host.Services.GetRequiredService<LocalAuthOptions>().SigningKey!;

        foreach (var bad in new[]
        {
            $"{parts[0]}.{tamperedPayload}.{parts[2]}",
            Forged(new byte[48], DateTime.UtcNow.AddHours(1)),
            Forged(realKey, DateTime.UtcNow.AddHours(-1)),
            "not.a.token",
        })
        {
            // In a developer runtime a token that does not check out is simply not a sign-in (no headers either), so nothing opens.
            using var client = Bearer(host, bad);
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/tenant-settings")).StatusCode);
        }

        using var good = Bearer(host, token);
        Assert.Equal(HttpStatusCode.OK, (await good.GetAsync("/api/v1/me")).StatusCode);

        static string Pad(string value) => value.Replace('-', '+').Replace('_', '/').PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
    }

    // ---- passwords ----

    [Fact]
    public async Task ForgottenPasswordsAreResetThroughAnEmailLinkThatWorksOnce()
    {
        using var host = With();
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();

        var known = await visitor.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email });
        var unknown = await visitor.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email = NewEmail() });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        var token = await LinkTokenAsync(host, PlatformMail, email, "reset-password");
        var weak = await visitor.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode); // a weak choice does not spend the link
        const string Replacement = "another-long-phrase-99";
        Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, password = Replacement })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, password = "yet-another-phrase-1" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email, password = Replacement })).StatusCode);
    }

    [Fact]
    public async Task AResetLinkExpiresAfterAnHour()
    {
        var time = new ManualTime();
        using var host = With(time);
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        using var visitor = host.CreateClient();
        await visitor.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email });
        var token = await LinkTokenAsync(host, PlatformMail, email, "reset-password");

        time.Advance(TimeSpan.FromMinutes(61));

        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/auth/password/reset", new { token, password = "another-long-phrase-99" })).StatusCode);
    }

    [Fact]
    public async Task ASignedInPersonChangesTheirPasswordOnlyWithTheCurrentOne()
    {
        using var host = With();
        var email = NewEmail();
        await OpenWorkspaceAsync(host, email);
        var (token, _) = await SignInAsync(host, email);
        using var signedIn = Bearer(host, token);

        var wrong = await signedIn.PostAsJsonAsync("/api/v1/auth/password/change", new { currentPassword = "not-my-password-1", newPassword = "another-long-phrase-99" });
        var ok = await signedIn.PostAsJsonAsync("/api/v1/auth/password/change", new { currentPassword = Password, newPassword = "another-long-phrase-99" });
        using var anonymous = host.CreateClient();
        var noToken = await anonymous.PostAsJsonAsync("/api/v1/auth/password/change", new { currentPassword = Password, newPassword = "another-long-phrase-99" });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        await SignInAsync(host, email, "another-long-phrase-99");
    }

    // ---- invitations ----

    [Fact]
    public async Task AnAdministratorInvitesAColleague_WhoJoinsWithTheRoleInvitedAndNothingMore()
    {
        using var host = With();
        var adminEmail = NewEmail();
        var colleague = NewEmail();
        var company = Company("Invite Insurance");
        await OpenWorkspaceAsync(host, adminEmail, company);
        var (adminToken, adminUser) = await SignInAsync(host, adminEmail);
        using var admin = Bearer(host, adminToken);

        var invited = await admin.PostAsJsonAsync("/api/v1/admin/invitations", new { email = colleague, role = "ClaimsOfficer" });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        var workspace = new TenantMail(adminUser.GetProperty("tenantId").GetString()!, adminUser.GetProperty("workspaceId").GetString()!);
        var secret = await LinkTokenAsync(host, workspace, colleague, "accept-invite");

        using var visitor = host.CreateClient();
        var preview = await MockedApiFactory.ReadJsonAsync(await visitor.GetAsync($"/api/v1/invitations/preview?token={Uri.EscapeDataString(secret)}"));
        Assert.Equal(company, preview.GetProperty("workspaceName").GetString());
        Assert.Equal("ClaimsOfficer", preview.GetProperty("role").GetString());
        Assert.False(preview.GetProperty("hasAccount").GetBoolean());

        var accepted = await visitor.PostAsJsonAsync("/api/v1/invitations/accept", new { token = secret, name = "Brian Okello", password = Password });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/invitations/accept", new { token = secret, name = "Brian Okello", password = Password })).StatusCode);

        var (officerToken, officer) = await SignInAsync(host, colleague);
        Assert.Equal("ClaimsOfficer", officer.GetProperty("role").GetString());
        Assert.Equal(adminUser.GetProperty("tenantId").GetString(), officer.GetProperty("tenantId").GetString());
        using var asOfficer = Bearer(host, officerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await asOfficer.GetAsync("/api/v1/admin/tenant-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asOfficer.PostAsJsonAsync("/api/v1/admin/invitations", new { email = NewEmail(), role = "Admin" })).StatusCode);
        var list = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/invitations"));
        Assert.Equal("Accepted", list[0].GetProperty("status").GetString());
        Assert.DoesNotContain(secret, await (await admin.GetAsync("/api/v1/admin/invitations")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnInvitationCanBeWithdrawnAndThenNoLongerWorks_AndAnOtherCompanyCannotTouchIt()
    {
        using var host = With();
        var adminEmail = NewEmail();
        var otherEmail = NewEmail();
        var colleague = NewEmail();
        await OpenWorkspaceAsync(host, adminEmail);
        await OpenWorkspaceAsync(host, otherEmail);
        var (adminToken, adminUser) = await SignInAsync(host, adminEmail);
        var (otherToken, _) = await SignInAsync(host, otherEmail);
        using var admin = Bearer(host, adminToken);
        using var other = Bearer(host, otherToken);
        var created = await MockedApiFactory.ReadJsonAsync(await admin.PostAsJsonAsync("/api/v1/admin/invitations", new { email = colleague, role = "ClaimsOfficer" }));
        var id = created.GetProperty("invitationId").GetGuid();
        var workspace = new TenantMail(adminUser.GetProperty("tenantId").GetString()!, adminUser.GetProperty("workspaceId").GetString()!);
        var secret = await LinkTokenAsync(host, workspace, colleague, "accept-invite");

        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/admin/invitations/{id}")).StatusCode);
        Assert.Empty((await MockedApiFactory.ReadJsonAsync(await other.GetAsync("/api/v1/admin/invitations"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/invitations/{id}")).StatusCode);

        using var visitor = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/v1/invitations/preview?token={Uri.EscapeDataString(secret)}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/invitations/accept", new { token = secret, name = "Brian Okello", password = Password })).StatusCode);
    }

    [Fact]
    public async Task InvitationsRespectTheSeatLimit_ExpireAfterAWeek_AndRefuseSomeoneAlreadyInside()
    {
        var time = new ManualTime();
        using var host = With(time);
        var adminEmail = NewEmail();
        var colleague = NewEmail();
        await OpenWorkspaceAsync(host, adminEmail);
        var (adminToken, adminUser) = await SignInAsync(host, adminEmail);
        using var admin = Bearer(host, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/invitations", new { email = adminEmail, role = "ClaimsOfficer" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/invitations", new { email = colleague, role = "NotARole" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { seatLimit = 1 })).StatusCode);
        var full = await admin.PostAsJsonAsync("/api/v1/admin/invitations", new { email = colleague, role = "ClaimsOfficer" });
        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Contains("limit", await full.Content.ReadAsStringAsync());

        await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { seatLimit = 5 });
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/admin/invitations", new { email = colleague, role = "ClaimsOfficer" })).StatusCode);
        var workspace = new TenantMail(adminUser.GetProperty("tenantId").GetString()!, adminUser.GetProperty("workspaceId").GetString()!);
        var secret = await LinkTokenAsync(host, workspace, colleague, "accept-invite");
        time.Advance(TimeSpan.FromDays(8));
        using var visitor = host.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/invitations/accept", new { token = secret, name = "Brian Okello", password = Password })).StatusCode);
    }

    [Fact]
    public async Task APersonWhoAlreadyHasASignInJoinsASecondWorkspaceWithTheirPassword_AndThenChoosesWhichToEnter()
    {
        using var host = With();
        var adminOne = NewEmail();
        var adminTwo = NewEmail();
        var firstName = Company("First Insurer");
        var secondName = Company("Second Insurer");
        await OpenWorkspaceAsync(host, adminOne, firstName);
        await OpenWorkspaceAsync(host, adminTwo, secondName);
        var (tokenTwo, userTwo) = await SignInAsync(host, adminTwo);
        using var second = Bearer(host, tokenTwo);
        await second.PostAsJsonAsync("/api/v1/admin/invitations", new { email = adminOne, role = "ClaimsSupervisor" });
        var secret = await LinkTokenAsync(host, new TenantMail(userTwo.GetProperty("tenantId").GetString()!, userTwo.GetProperty("workspaceId").GetString()!), adminOne, "accept-invite");
        using var visitor = host.CreateClient();

        var preview = await MockedApiFactory.ReadJsonAsync(await visitor.GetAsync($"/api/v1/invitations/preview?token={Uri.EscapeDataString(secret)}"));
        Assert.True(preview.GetProperty("hasAccount").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/v1/invitations/accept", new { token = secret, password = "not-the-password-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/invitations/accept", new { token = secret, password = Password })).StatusCode);

        var chooser = await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = adminOne, password = Password });
        Assert.Equal(HttpStatusCode.Conflict, chooser.StatusCode);
        var choices = (await MockedApiFactory.ReadJsonAsync(chooser)).GetProperty("workspaces").EnumerateArray().ToArray();
        Assert.Equal(new[] { firstName, secondName }.Order().ToArray(), choices.Select(choice => choice.GetProperty("name").GetString()!).Order().ToArray());
        var second1 = choices.Single(choice => choice.GetProperty("name").GetString() == secondName);
        var (_, entered) = await SignInAsync(host, adminOne, workspaceId: second1.GetProperty("workspaceId").GetString(), tenantId: second1.GetProperty("tenantId").GetString());
        Assert.Equal(second1.GetProperty("tenantId").GetString(), entered.GetProperty("tenantId").GetString());
        Assert.Equal("ClaimsSupervisor", entered.GetProperty("role").GetString());
    }

    // ---- limits ----

    [Fact]
    public async Task TheDoorsToAStrangerAreRateLimitedPerAddress()
    {
        using var host = With(settings: new() { ["INSURANCEINTELLIGENCE_IDENTITY_RATE_LIMIT_PER_MINUTE"] = "3" });
        using var visitor = host.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await visitor.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = NewEmail(), password = "whatever-it-is-1" })).StatusCode);
        }

        Assert.Equal(3, statuses.Count(status => status == HttpStatusCode.Unauthorized));
        Assert.Equal(2, statuses.Count(status => status == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task WithNoSigningKeyOutsideDevelopmentTheSignInSaysItIsNotConfigured()
    {
        var service = new LocalAuthOptions { SigningKey = null };

        Assert.False(service.Enabled);
        Assert.False(new LocalTokenIssuer(service).IsConfigured);
        Assert.Throws<InvalidOperationException>(() => new LocalTokenIssuer(service).Issue("u", "e@x.test", "n", InsuranceIntelligence.Domain.Common.TenantScope.Create("t", "w"), "Admin"));
        await Task.CompletedTask;
    }
}

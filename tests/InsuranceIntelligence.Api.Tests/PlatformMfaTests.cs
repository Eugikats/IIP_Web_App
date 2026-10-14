using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Api.Security;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Security;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The second step after sign-in: authenticator codes, backup codes, email and SMS codes, the policy and the gate.</summary>
public sealed class PlatformMfaTests
{
    // ---------------------------------------------------------------- the code maths

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Authenticator_codes_match_the_RFC_6238_test_vectors(long seconds, string expected)
    {
        // The RFC's SHA-1 test secret is the ASCII text 12345678901234567890.
        var secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));

        Assert.Equal(expected, Totp.Code(secret, seconds / 30));
    }

    [Fact]
    public void Base32_round_trips_and_a_code_works_once()
    {
        var secret = Totp.NewSecret();
        Assert.Equal(secret, Totp.Base32Encode(Totp.Base32Decode(secret)));

        var now = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var step = Totp.StepAt(now);
        var code = Totp.Code(secret, step);

        Assert.Equal(step, Totp.Verify(secret, code, now, lastUsedStep: 0));
        Assert.Null(Totp.Verify(secret, code, now, lastUsedStep: step));
        Assert.Equal(step - 1, Totp.Verify(secret, Totp.Code(secret, step - 1), now, lastUsedStep: 0));
        Assert.Null(Totp.Verify(secret, Totp.Code(secret, step + 5), now, lastUsedStep: 0));
        Assert.Null(Totp.Verify(secret, "12345", now, lastUsedStep: 0));
    }

    [Fact]
    public void The_provisioning_address_carries_the_secret_and_the_issuer()
    {
        var uri = Totp.ProvisioningUri("Insurance Intelligence Platform", "ada@example.test", "ABCDEFGH");

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=ABCDEFGH", uri);
        Assert.Contains("issuer=Insurance%20Intelligence%20Platform", uri);
    }

    // ---------------------------------------------------------------- the gate

    [Fact]
    public async Task Nothing_changes_while_the_policy_is_off()
    {
        var h = new Harness();

        Assert.Equal(MfaGate.Allowed, await h.Service.EvaluateGateAsync(h.Scope, "user-1"));
        var status = await h.Service.GetStatusAsync();
        Assert.False(status.PolicyRequired);
        Assert.False(status.ShouldPrompt);
        Assert.False(status.MustVerify);
    }

    [Fact]
    public async Task When_required_a_person_without_a_method_is_asked_to_set_one_up_and_may_postpone_for_a_day()
    {
        var h = new Harness();
        await h.RequireAsync();

        Assert.Equal(MfaGate.EnrolRequired, await h.Service.EvaluateGateAsync(h.Scope, "newcomer"));

        h.Context.UserId = "newcomer";
        Assert.True((await h.Service.GetStatusAsync()).CanSnooze);
        Assert.True((await h.Service.SnoozeAsync()).Ok);
        Assert.Equal(MfaGate.Allowed, await h.Service.EvaluateGateAsync(h.Scope, "newcomer"));

        h.Clock.Advance(TimeSpan.FromHours(25));
        Assert.Equal(MfaGate.EnrolRequired, await h.Service.EvaluateGateAsync(h.Scope, "newcomer"));
    }

    [Fact]
    public async Task Postponing_is_limited_and_can_be_switched_off()
    {
        var h = new Harness();
        await h.RequireAsync();
        h.Context.UserId = "newcomer";

        for (var i = 0; i < 7; i++)
        {
            Assert.True((await h.Service.SnoozeAsync()).Ok);
            h.Clock.Advance(TimeSpan.FromHours(25));
        }

        Assert.Equal("not_allowed", (await h.Service.SnoozeAsync()).Code);

        h.Context.UserId = "admin";
        await h.Service.SetPolicyAsync(true, allowSnooze: false, 1, MfaMethods.All);
        h.Context.UserId = "other";
        Assert.Equal("not_allowed", (await h.Service.SnoozeAsync()).Code);
    }

    [Fact]
    public async Task A_person_with_a_method_must_verify_before_the_api_answers_and_signing_out_ends_it()
    {
        var h = new Harness();
        await h.EnrolAdminWithAuthenticatorAsync();
        await h.RequireAsync();

        // Enrolling counted as verifying, so the administrator who just set it up is not locked out.
        Assert.Equal(MfaGate.Allowed, await h.Service.EvaluateGateAsync(h.Scope, "admin"));

        await h.Service.SignOutAsync();
        Assert.Equal(MfaGate.VerifyRequired, await h.Service.EvaluateGateAsync(h.Scope, "admin"));
        Assert.True((await h.Service.GetStatusAsync()).MustVerify);

        h.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True((await h.Service.VerifyAsync(MfaMethods.Totp, h.NextCode())).Ok);
        Assert.Equal(MfaGate.Allowed, await h.Service.EvaluateGateAsync(h.Scope, "admin"));

        h.Clock.Advance(TimeSpan.FromHours(13));
        Assert.Equal(MfaGate.VerifyRequired, await h.Service.EvaluateGateAsync(h.Scope, "admin"));
    }

    [Fact]
    public async Task One_organisations_policy_never_touches_another()
    {
        var h = new Harness();
        await h.EnrolAdminWithAuthenticatorAsync();
        await h.RequireAsync();

        var other = TenantScope.Create("another-insurer", "workspace-operations");
        Assert.Equal(MfaGate.Allowed, await h.Service.EvaluateGateAsync(other, "admin"));
    }

    // ---------------------------------------------------------------- the authenticator app

    [Fact]
    public async Task Setting_up_an_authenticator_app_stores_the_secret_encrypted_and_never_returns_it_again()
    {
        var h = new Harness();
        var begin = await h.Service.BeginTotpAsync("Microsoft Authenticator", "ada@example.test");

        Assert.True(begin.Ok);
        Assert.Contains(begin.Value!.Secret, begin.Value.ProvisioningUri);
        var stored = (await h.Profiles.GetAsync(h.Scope, "admin"))!.Authenticators.Single();
        Assert.DoesNotContain(begin.Value.Secret, stored.ProtectedSecret);
        Assert.Equal(begin.Value.Secret, h.Protector.Unprotect(stored.ProtectedSecret));

        Assert.Equal("invalid_code", (await h.Service.ConfirmTotpAsync(begin.Value.Id, "000000")).Code);
        var code = Totp.Code(begin.Value.Secret, Totp.StepAt(h.Clock.GetUtcNow()));
        Assert.True((await h.Service.ConfirmTotpAsync(begin.Value.Id, code)).Ok);

        var status = await h.Service.GetStatusAsync();
        Assert.Contains(MfaMethods.Totp, status.EnrolledMethods);
        Assert.Single(status.Credentials);
        Assert.DoesNotContain(h.Audit.Events, e => (e.AuditReason ?? string.Empty).Contains(begin.Value.Secret));
        Assert.Contains(h.Audit.Events, e => e.ActionName == "security.mfa.enrolled");
    }

    [Fact]
    public async Task The_same_code_cannot_be_used_twice()
    {
        var h = new Harness();
        await h.EnrolAdminWithAuthenticatorAsync();
        await h.Service.SignOutAsync();

        h.Clock.Advance(TimeSpan.FromSeconds(31));
        var code = h.NextCode();
        Assert.True((await h.Service.VerifyAsync(MfaMethods.Totp, code)).Ok);
        await h.Service.SignOutAsync();

        Assert.Equal("invalid_code", (await h.Service.VerifyAsync(MfaMethods.Totp, code)).Code);
    }

    [Fact]
    public async Task Five_wrong_codes_lock_verification_for_a_while()
    {
        var h = new Harness();
        await h.EnrolAdminWithAuthenticatorAsync();
        await h.Service.SignOutAsync();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal("invalid_code", (await h.Service.VerifyAsync(MfaMethods.Totp, "000000")).Code);
        }

        // Even the right code is refused while locked, so guessing cannot continue.
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal("locked", (await h.Service.VerifyAsync(MfaMethods.Totp, h.NextCode())).Code);
        Assert.True((await h.Service.GetStatusAsync()).LockedOut);
        Assert.Contains(h.Audit.Events, e => e.ActionName == "security.mfa.locked");

        h.Clock.Advance(TimeSpan.FromMinutes(16));
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True((await h.Service.VerifyAsync(MfaMethods.Totp, h.NextCode())).Ok);
    }

    // ---------------------------------------------------------------- backup codes

    [Fact]
    public async Task Backup_codes_need_another_method_first_are_stored_as_hashes_and_work_once()
    {
        var h = new Harness();
        Assert.Equal("needs_method", (await h.Service.RotateBackupCodesAsync()).Code);

        await h.EnrolAdminWithAuthenticatorAsync();
        var rotate = await h.Service.RotateBackupCodesAsync();
        Assert.True(rotate.Ok);
        Assert.Equal(8, rotate.Value!.Count);
        var profile = (await h.Profiles.GetAsync(h.Scope, "admin"))!;
        Assert.All(rotate.Value, code => Assert.DoesNotContain(profile.BackupCodes, b => b.Hash.Contains(code.Replace("-", ""))));

        await h.Service.SignOutAsync();
        var first = rotate.Value[0];
        Assert.True((await h.Service.VerifyAsync(MfaMethods.BackupCodes, first.ToLowerInvariant())).Ok);
        await h.Service.SignOutAsync();
        Assert.Equal("invalid_code", (await h.Service.VerifyAsync(MfaMethods.BackupCodes, first)).Code);
        Assert.Equal(7, (await h.Service.GetStatusAsync()).UnusedBackupCodes);
    }

    // ---------------------------------------------------------------- changing methods

    [Fact]
    public async Task Changing_methods_needs_a_person_who_has_one_to_have_verified_recently()
    {
        var h = new Harness();
        await h.EnrolAdminWithAuthenticatorAsync();
        await h.Service.SignOutAsync();

        Assert.Equal("needs_verification", (await h.Service.BeginTotpAsync("Another", "ada")).Code);
        Assert.Equal("needs_verification", (await h.Service.RotateBackupCodesAsync()).Code);
        var id = (await h.Service.GetStatusAsync()).Credentials.Single().Id;
        Assert.Equal("needs_verification", (await h.Service.RemoveCredentialAsync(MfaMethods.Totp, id)).Code);

        h.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True((await h.Service.VerifyAsync(MfaMethods.Totp, h.NextCode())).Ok);
        Assert.True((await h.Service.RemoveCredentialAsync(MfaMethods.Totp, id)).Ok);
        Assert.Empty((await h.Service.GetStatusAsync()).Credentials);
    }

    // ---------------------------------------------------------------- email and SMS codes

    [Fact]
    public async Task An_email_code_is_sent_stored_as_a_hash_and_enrols_the_method_when_typed_back()
    {
        var h = new Harness();

        var sent = await h.Service.SendCodeAsync("email", "enroll", null, "ada@example.test");
        Assert.True(sent.Ok);
        Assert.Equal("a**@example.test", sent.Value!.DestinationMasked);
        var code = h.LastCode();
        Assert.DoesNotContain(code, (await h.Profiles.GetAsync(h.Scope, "admin"))!.PendingChallenge!.CodeHash);

        Assert.Equal("invalid_code", (await h.Service.ConfirmCodeEnrolmentAsync("email", "000000")).Code);
        Assert.True((await h.Service.ConfirmCodeEnrolmentAsync("email", code)).Ok);
        Assert.Contains(MfaMethods.EmailOtp, (await h.Service.GetStatusAsync()).EnrolledMethods);
    }

    [Fact]
    public async Task A_code_expires_and_cannot_be_asked_for_again_straight_away()
    {
        var h = new Harness();
        await h.Service.SendCodeAsync("email", "enroll", null, "ada@example.test");
        var code = h.LastCode();

        Assert.Equal("cooldown", (await h.Service.SendCodeAsync("email", "enroll", null, "ada@example.test")).Code);

        h.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal("expired", (await h.Service.ConfirmCodeEnrolmentAsync("email", code)).Code);
    }

    [Fact]
    public async Task Five_wrong_attempts_on_one_code_burn_it()
    {
        var h = new Harness();
        await h.Service.SendCodeAsync("email", "enroll", null, "ada@example.test");
        var code = h.LastCode();

        for (var i = 0; i < 4; i++) Assert.Equal("invalid_code", (await h.Service.ConfirmCodeEnrolmentAsync("email", "000000")).Code);
        Assert.Equal("expired", (await h.Service.ConfirmCodeEnrolmentAsync("email", "000000")).Code);

        // Five wrong codes also lock the person out for a while, so the right code is refused too...
        Assert.Equal("locked", (await h.Service.ConfirmCodeEnrolmentAsync("email", code)).Code);

        // ...and once the lock ends the burned code is gone: a new one must be asked for.
        h.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal("no_code", (await h.Service.ConfirmCodeEnrolmentAsync("email", code)).Code);
    }

    [Fact]
    public async Task An_sms_number_is_saved_encrypted_after_its_code_is_confirmed_and_used_for_login_codes()
    {
        var h = new Harness();
        Assert.Equal("bad_number", (await h.Service.SendCodeAsync("sms", "enroll", "12", null)).Code);

        Assert.True((await h.Service.SendCodeAsync("sms", "enroll", "+256 700 000 111", null)).Ok);
        Assert.Equal("+256700000111", h.Sms.LastRecipient);
        Assert.True((await h.Service.ConfirmCodeEnrolmentAsync("sms", h.LastCode())).Ok);

        var profile = (await h.Profiles.GetAsync(h.Scope, "admin"))!;
        Assert.DoesNotContain("700000111", profile.ProtectedSmsNumber);
        Assert.Equal("+256*******11", profile.SmsNumberMasked);

        // A later login code goes to the saved number, not to a number typed in the request.
        await h.Service.SignOutAsync();
        h.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True((await h.Service.SendCodeAsync("sms", "login", "+256999999999", null)).Ok);
        Assert.Equal("+256700000111", h.Sms.LastRecipient);
        Assert.True((await h.Service.VerifyAsync(MfaMethods.SmsOtp, h.LastCode())).Ok);
    }

    [Fact]
    public async Task A_channel_that_is_not_connected_says_so_and_nothing_is_stored()
    {
        var h = new Harness();
        h.Email.Status = "ConfigurationRequired";

        var sent = await h.Service.SendCodeAsync("email", "enroll", null, "ada@example.test");

        Assert.Equal("not_configured", sent.Code);
        Assert.Null((await h.Profiles.GetAsync(h.Scope, "admin"))?.PendingChallenge);
    }

    // ---------------------------------------------------------------- the organisation's policy

    [Fact]
    public async Task Only_someone_who_manages_settings_can_change_the_policy_or_reset_a_person()
    {
        var h = new Harness(allowed: false);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => h.Service.SetPolicyAsync(true, true, 1, MfaMethods.All));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => h.Service.ResetUserAsync("someone"));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => h.Service.GetMetricsAsync());
        Assert.False((await h.Service.GetPolicyAsync()).Required);
    }

    [Fact]
    public async Task An_administrator_cannot_require_it_before_setting_up_their_own_method()
    {
        var h = new Harness();

        var refused = await h.Service.SetPolicyAsync(true, true, 1, MfaMethods.All);
        Assert.Equal("enrol_first", refused.Code);
        Assert.False((await h.Service.GetPolicyAsync()).Required);

        await h.EnrolAdminWithAuthenticatorAsync();
        Assert.True((await h.Service.SetPolicyAsync(true, true, 1, MfaMethods.All)).Ok);
        Assert.Contains(h.Audit.Events, e => e.ActionName == "security.mfa.policy_changed");
    }

    [Fact]
    public async Task The_policy_rejects_nonsense_and_a_switched_off_method_cannot_be_used()
    {
        var h = new Harness();

        Assert.Equal("invalid", (await h.Service.SetPolicyAsync(false, true, 0, MfaMethods.All)).Code);
        Assert.Equal("invalid", (await h.Service.SetPolicyAsync(false, true, 2, [MfaMethods.Totp])).Code);

        await h.Service.SetPolicyAsync(false, true, 1, [MfaMethods.Passkey, MfaMethods.BackupCodes]);
        Assert.Equal("not_offered", (await h.Service.BeginTotpAsync("App", "ada")).Code);
        Assert.Equal("not_offered", (await h.Service.SendCodeAsync("email", "enroll", null, "ada@example.test")).Code);
    }

    [Fact]
    public async Task Resetting_a_person_clears_their_methods_and_an_administrator_cannot_reset_themselves()
    {
        var h = new Harness();
        await h.EnrolAdminWithAuthenticatorAsync();
        Assert.Equal("not_allowed", (await h.Service.ResetUserAsync("admin")).Code);

        h.Context.UserId = "colleague";
        var begin = await h.Service.BeginTotpAsync("App", "colleague");
        await h.Service.ConfirmTotpAsync(begin.Value!.Id, Totp.Code(begin.Value.Secret, Totp.StepAt(h.Clock.GetUtcNow())));
        Assert.NotEmpty((await h.Service.GetStatusAsync()).EnrolledMethods);

        h.Context.UserId = "admin";
        Assert.True((await h.Service.ResetUserAsync("colleague")).Ok);
        Assert.Contains(h.Audit.Events, e => e.ActionName == "security.mfa.user_reset");

        h.Context.UserId = "colleague";
        Assert.Empty((await h.Service.GetStatusAsync()).EnrolledMethods);
        h.Context.UserId = "admin";
        Assert.Equal(1, (await h.Service.GetMetricsAsync()).PeopleWithAMethod);
    }

    // ---------------------------------------------------------------- the middleware

    [Theory]
    [InlineData(MfaGate.EnrolRequired, "MFA_ENROLL_REQUIRED")]
    [InlineData(MfaGate.VerifyRequired, "MFA_REQUIRED")]
    public async Task The_middleware_answers_403_with_the_reason_when_the_gate_is_closed(MfaGate gate, string expectedCode)
    {
        var (status, body, reached) = await RunMiddlewareAsync("/api/v1/claims", gate);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Contains(expectedCode, body);
        Assert.False(reached);
    }

    [Theory]
    [InlineData("/api/v1/security/mfa/verify")]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/organisations")]
    [InlineData("/health/ready")]
    public async Task The_routes_a_person_needs_to_enrol_or_verify_are_never_blocked(string path)
    {
        var (status, _, reached) = await RunMiddlewareAsync(path, MfaGate.VerifyRequired);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(reached);
    }

    [Fact]
    public async Task The_middleware_does_nothing_when_the_gate_is_open_or_no_one_is_signed_in()
    {
        var open = await RunMiddlewareAsync("/api/v1/claims", MfaGate.Allowed);
        Assert.True(open.Reached);

        var anonymous = await RunMiddlewareAsync("/api/v1/claims", MfaGate.VerifyRequired, hasContext: false);
        Assert.True(anonymous.Reached);
    }

    private static async Task<(int Status, string Body, bool Reached)> RunMiddlewareAsync(string path, MfaGate gate, bool hasContext = true)
    {
        var reached = false;
        var middleware = new PlatformMfaEnforcementMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(http, new FixedTenant(hasContext), new GateOnly(gate));
        http.Response.Body.Position = 0;
        return (http.Response.StatusCode, await new StreamReader(http.Response.Body).ReadToEndAsync(), reached);
    }

    // ---------------------------------------------------------------- test doubles

    private sealed class Harness
    {
        private static int counter;

        public TenantScope Scope { get; }
        public MutableContext Context { get; }
        public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
        public FakeProtector Protector { get; } = new();
        public FakeSms Sms { get; } = new();
        public FakeEmail Email { get; } = new();
        public AuditLog Audit { get; } = new();
        public ProfileStore Profiles { get; } = new();
        public PlatformMfaService Service { get; }
        private string? lastSecret;

        public Harness(bool allowed = true)
        {
            // A new organisation per test, so the service's short policy cache never carries over.
            var tenant = $"tenant-{Interlocked.Increment(ref counter)}-{Guid.NewGuid():N}";
            Scope = TenantScope.Create(tenant, "workspace-operations");
            Context = new MutableContext(tenant, "workspace-operations") { UserId = "admin" };
            Service = new PlatformMfaService(
                Context, new Perms(allowed), Profiles, new PolicyStore(), Protector, Sms, Email, Audit,
                new PlatformMfaOptions { PolicyCacheFor = TimeSpan.Zero }, Clock);
        }

        public Task RequireAsync() => RequireInternalAsync();

        private async Task RequireInternalAsync()
        {
            // The administrator who turns it on must have a method of their own first.
            var original = Context.UserId;
            Context.UserId = "admin";
            if ((await Service.GetStatusAsync()).EnrolledMethods.Count == 0) await EnrolAdminWithAuthenticatorAsync();
            var result = await Service.SetPolicyAsync(true, true, 1, MfaMethods.All);
            Assert.True(result.Ok, result.Message);
            Context.UserId = original;
        }

        public async Task EnrolAdminWithAuthenticatorAsync()
        {
            var begin = await Service.BeginTotpAsync("Authenticator app", "admin@example.test");
            lastSecret = begin.Value!.Secret;
            var confirm = await Service.ConfirmTotpAsync(begin.Value.Id, Totp.Code(lastSecret, Totp.StepAt(Clock.GetUtcNow())));
            Assert.True(confirm.Ok, confirm.Message);
        }

        /// <summary>The code the authenticator app would show now.</summary>
        public string NextCode() => Totp.Code(lastSecret!, Totp.StepAt(Clock.GetUtcNow()));

        public string LastCode()
        {
            var body = Sms.LastBody is not null && (Email.LastBody is null || Sms.SentAt > Email.SentAt) ? Sms.LastBody : Email.LastBody!;
            return Regex.Match(body, @"\b\d{6}\b").Value;
        }
    }

    private sealed class MutableContext(string tenantId, string workspaceId) : ITenantContextAccessor
    {
        public string UserId { get; set; } = "admin";
        public TenantContext Current => new(tenantId, workspaceId, UserId, ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class FixedTenant(bool has) : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant", "workspace", "user-1", ["Admin"]);
        public bool HasCurrent => has;
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class FakeProtector : IConnectionSecretProtector
    {
        public string Protect(string plaintext) => "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(plaintext.Reverse().ToArray())));
        public string Unprotect(string ciphertext) => new string(Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext["enc:".Length..])).Reverse().ToArray());
    }

    private sealed class FakeSms : ISmsProvider
    {
        public string? LastBody { get; private set; }
        public string? LastRecipient { get; private set; }
        public DateTimeOffset SentAt { get; private set; }
        public string Status { get; set; } = "Sent";

        public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            LastBody = request.MessageBody;
            LastRecipient = request.Recipient;
            SentAt = DateTimeOffset.UtcNow;
            return Task.FromResult(new CommunicationSendResult(Status, "id", Status == "ConfigurationRequired" ? "not configured" : null));
        }
    }

    private sealed class FakeEmail : IEmailSendingProvider
    {
        public string? LastBody { get; private set; }
        public DateTimeOffset SentAt { get; private set; }
        public string Status { get; set; } = "Sent";

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            LastBody = request.MessageBody;
            SentAt = DateTimeOffset.UtcNow;
            return Task.FromResult(new CommunicationSendResult(Status, "id", Status == "ConfigurationRequired" ? "not configured" : null));
        }
    }

    private sealed class Perms(bool allowed) : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) =>
            allowed ? Task.CompletedTask : throw new PermissionDeniedException(permission);

        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
    }

    private sealed class AuditLog : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ProfileStore : IUserSecurityRepository
    {
        private readonly Dictionary<Guid, UserSecurityProfile> items = [];

        public Task<UserSecurityProfile?> GetAsync(TenantScope scope, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.GetValueOrDefault(UserSecurityProfile.StorageKey(scope, userId)));

        public Task SaveAsync(UserSecurityProfile profile, CancellationToken cancellationToken = default)
        {
            items[UserSecurityProfile.StorageKey(profile.Scope, profile.UserId)] = profile;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserSecurityProfile>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserSecurityProfile>>(items.Values.Where(p => p.Scope == scope).ToArray());
    }

    private sealed class PolicyStore : IMfaPolicyRepository
    {
        private readonly Dictionary<Guid, MfaPolicy> items = [];

        public Task<MfaPolicy?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.GetValueOrDefault(MfaPolicy.StorageKey(scope)));

        public Task SaveAsync(MfaPolicy policy, CancellationToken cancellationToken = default)
        {
            items[MfaPolicy.StorageKey(policy.Scope)] = policy;
            return Task.CompletedTask;
        }
    }

    /// <summary>Stands in for the service where only the gate decision matters.</summary>
    private sealed class GateOnly(MfaGate gate) : IPlatformMfaService
    {
        public Task<MfaGate> EvaluateGateAsync(TenantScope scope, string userId, CancellationToken cancellationToken = default) => Task.FromResult(gate);

        public Task<MfaStatus> GetStatusAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<TotpEnrolment>> BeginTotpAsync(string label, string accountName, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> ConfirmTotpAsync(Guid id, string code, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> RemoveCredentialAsync(string method, Guid id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<IReadOnlyList<string>>> RotateBackupCodesAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<MfaCodeSent>> SendCodeAsync(string channel, string purpose, string? destination, string? accountEmail, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> ConfirmCodeEnrolmentAsync(string channel, string code, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> DisableChannelAsync(string channel, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> VerifyAsync(string method, string code, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> SnoozeAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task SignOutAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaPolicy> GetPolicyAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<MfaPolicy>> SetPolicyAsync(bool required, bool allowSnooze, int minMethods, IReadOnlyList<string> enabledMethods, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> ResetUserAsync(string userId, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaMetrics> GetMetricsAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<UserSecurityProfile> GetProfileAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> BeginPasskeyCeremonyAsync(string purpose, string optionsJson, CancellationToken c = default) => throw new NotSupportedException();
        public Task<string?> TakePendingPasskeyCeremonyAsync(string purpose, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> AddPasskeyAsync(PasskeyCredential credential, CancellationToken c = default) => throw new NotSupportedException();
        public Task<MfaResult<bool>> CompletePasskeyVerificationAsync(string credentialId, uint signCount, CancellationToken c = default) => throw new NotSupportedException();
    }
}

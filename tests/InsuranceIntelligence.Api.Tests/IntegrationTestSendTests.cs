using System.Text.Json;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// "Send a test" goes through the real provider, only for people who may configure integrations, with fixed text, a
/// small hourly limit, and an audit entry that never holds the message or the full address.
/// </summary>
public sealed class IntegrationTestSendTests
{
    [Theory]
    [InlineData("aisha@example.test", true)]
    [InlineData("aisha@example", false)]
    [InlineData("not an email", false)]
    [InlineData("", false)]
    public void EmailAddressesAreChecked(string value, bool valid) => Assert.Equal(valid, IntegrationTestSendEndpoints.IsEmail(value));

    [Theory]
    [InlineData("+256700000000", true)]
    [InlineData("+256 700 000 000", true)]
    [InlineData("0700000000", false)]
    [InlineData("+25670", false)]
    [InlineData("+2567000000001234567", false)]
    [InlineData("+256700abc000", false)]
    public void PhoneNumbersNeedTheirCountryCode(string value, bool valid) => Assert.Equal(valid, IntegrationTestSendEndpoints.IsPhone(value));

    [Fact]
    public async Task AnEmailTestGoesThroughTheProvider_WithFixedText_AndIsAuditedWithoutTheMessageOrFullAddress()
    {
        var rig = new Rig();

        var result = await rig.SendAsync("email", "aisha@example.test");

        var body = Json(result);
        Assert.True(body.GetProperty("accepted").GetBoolean());
        var sent = Assert.Single(rig.Email.Sent);
        Assert.Equal("aisha@example.test", sent.Recipient);
        Assert.Equal(IntegrationTestSendEndpoints.TestText, sent.MessageBody);
        Assert.True(sent.Approved);
        var audit = Assert.Single(rig.Audit.Events);
        Assert.Equal("integration.test_sent", audit.ActionName);
        Assert.DoesNotContain("aisha@example.test", audit.AuditReason ?? string.Empty);
        Assert.DoesNotContain(IntegrationTestSendEndpoints.TestText, audit.AuditReason ?? string.Empty);
    }

    [Fact]
    public async Task AnSmsTestUsesTheSmsProvider_AndAProviderThatIsNotSetUpSaysSo()
    {
        var rig = new Rig { SmsResult = new CommunicationSendResult("ConfigurationRequired", string.Empty, null) };

        var body = Json(await rig.SendAsync("sms", "+256700000000"));

        Assert.False(body.GetProperty("accepted").GetBoolean());
        Assert.Contains("not set up", body.GetProperty("message").GetString());
        Assert.Single(rig.Sms.Sent);
        Assert.Empty(rig.Email.Sent);
    }

    [Fact]
    public async Task AProviderThatThrowsIsReportedInWords_NeverWithItsError()
    {
        var rig = new Rig { EmailThrows = new InvalidOperationException("secret-token-abc123 rejected") };

        var json = JsonSerializer.Serialize((await rig.SendAsync("email", "aisha@example.test") as IValueHttpResult)?.Value);

        Assert.DoesNotContain("secret-token", json);
        Assert.Contains("\"accepted\":false", json);
    }

    [Fact]
    public async Task AFourthTestInAnHourIsRefused()
    {
        var rig = new Rig();
        for (var i = 0; i < IntegrationTestSendEndpoints.MaxPerHour; i++) await rig.SendAsync("email", "aisha@example.test");

        var fourth = await rig.SendAsync("email", "aisha@example.test");

        Assert.Equal(StatusCodes.Status429TooManyRequests, (fourth as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(IntegrationTestSendEndpoints.MaxPerHour, rig.Email.Sent.Count);
    }

    [Fact]
    public async Task ARecipientThatIsNotValidSendsNothing()
    {
        var rig = new Rig();
        var result = await rig.SendAsync("sms", "0700000000");

        Assert.Empty(rig.Sms.Sent);
        Assert.Empty(rig.Audit.Events);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task APersonWithoutTheIntegrationsPermissionCannotSend()
    {
        var rig = new Rig { Role = "ClaimsOfficer" };

        var result = await rig.SendAsync("email", "aisha@example.test");

        Assert.Equal(StatusCodes.Status403Forbidden, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Empty(rig.Email.Sent);
    }

    [Fact]
    public async Task OnlyEmailAndSmsCanBeTested()
    {
        var rig = new Rig();
        var result = await rig.SendAsync("whatsapp", "+256700000000");
        Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
    }

    private static JsonElement Json(IResult result) =>
        JsonSerializer.SerializeToElement(((IValueHttpResult)result).Value);

    private sealed class Rig
    {
        public string Role { get; init; } = "ITAdmin";
        public CommunicationSendResult SmsResult { get; init; } = new("Sent", "msg-1", null);
        public Exception? EmailThrows { get; init; }
        public FakeEmail Email { get; } = new();
        public FakeSms Sms { get; } = new();
        public FakeAudit Audit { get; } = new();
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        private readonly TenantContextAccessor tenant = new();

        public Task<IResult> SendAsync(string channel, string recipient)
        {
            Email.Throws = EmailThrows;
            Sms.Result = SmsResult;
            var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
            context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
            context.Request.Headers["X-Workspace-Id"] = "workspace-operations";
            context.Request.Headers["X-User-Id"] = "user-admin";
            context.Request.Headers["x-demo-user-id"] = "user-admin";
            context.Request.Headers["x-demo-role"] = Role;
            tenant.SetCurrent(new TenantContext("tenant-demo", "workspace-operations", "user-admin", [Role]));
            return IntegrationTestSendEndpoints.Send(
                channel, new IntegrationTestSendEndpoints.TestSendRequest(recipient), Email, Sms, Audit, cache, TimeProvider.System,
                tenant, tenant, new InfrastructureOptions { AuthMode = "Development" }, context, CancellationToken.None);
        }
    }

    private sealed class FakeEmail : IEmailSendingProvider
    {
        public List<CommunicationSendRequest> Sent { get; } = [];
        public Exception? Throws { get; set; }

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Throws is null ? Task.FromResult(new CommunicationSendResult("Sent", "msg-1", null)) : Task.FromException<CommunicationSendResult>(Throws);
        }
    }

    private sealed class FakeSms : ISmsProvider
    {
        public List<CommunicationSendRequest> Sent { get; } = [];
        public CommunicationSendResult Result { get; set; } = new("Sent", "msg-1", null);

        public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAudit : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }
}

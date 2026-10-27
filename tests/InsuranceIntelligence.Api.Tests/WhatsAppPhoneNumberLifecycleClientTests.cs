using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppPhoneNumberLifecycleClientTests
{
    [Fact]
    public async Task RequestCodeUsesMetaEndpointAndNeverExposesTokenInTheResult()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"success\":true}");
        var client = Create(handler);

        var result = await client.RequestCodeAsync("phone-123", "SMS", "en_US", "secret-token");

        Assert.True(result.Success);
        Assert.Equal("https://graph.example/v24.0/phone-123/request_code", handler.RequestUri);
        Assert.Equal("Bearer secret-token", handler.Authorization);
        Assert.Contains("\"code_method\":\"SMS\"", handler.Body);
        Assert.DoesNotContain("secret-token", result.SafeMessage ?? string.Empty);
    }

    [Theory]
    [InlineData("The code is incorrect", WhatsAppPhoneNumberFailure.WrongCode)]
    [InlineData("The code has expired", WhatsAppPhoneNumberFailure.CodeExpired)]
    [InlineData("This number is already registered to a WhatsApp account", WhatsAppPhoneNumberFailure.NumberInUse)]
    [InlineData("Too many attempts", WhatsAppPhoneNumberFailure.RateLimited)]
    public void MetaErrorsAreMappedToSafeActions(string message, WhatsAppPhoneNumberFailure expected)
    {
        var result = MetaPhoneNumberLifecycleClient.MapError($"{{\"error\":{{\"message\":\"{message}\"}}}}");

        Assert.False(result.Success);
        Assert.Equal(expected, result.Failure);
        Assert.DoesNotContain("access_token", result.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrationPassesPinOnlyToMetaAndNotInResult()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"success\":true}");
        var result = await Create(handler).RegisterAsync("phone-123", "123456", "secret-token");

        Assert.True(result.Success);
        Assert.Contains("\"pin\":\"123456\"", handler.Body);
        Assert.DoesNotContain("123456", result.SafeMessage ?? string.Empty);
    }

    private static MetaPhoneNumberLifecycleClient Create(RecordingHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new InfrastructureOptions { WhatsApp = new WhatsAppOptions { GraphBaseUrl = "https://graph.example/v24.0" } }));

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}

using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Graph;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>A Microsoft 365 mailbox is read through the insurer's own Microsoft Entra directory when it names one, and through the
/// deployment's directory otherwise (build-loop item 28).</summary>
public sealed class GraphMailDirectoryTests
{
    private static MailboxConnection Mailbox(string? directory) =>
        new()
        {
            MailboxConnectionId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-a", "workspace-a"),
            DisplayName = "Claims",
            MailboxAddress = "claims@insurer.test",
            IngestionEnabled = true,
            DefaultChannel = "Email",
            CreatedAt = DateTimeOffset.UtcNow,
            EntraTenantId = directory,
        };

    private static InfrastructureOptions Options(string? deploymentDirectory) => new()
    {
        EmailProvider = "Graph",
        GraphMail = new GraphMailOptions { TenantId = deploymentDirectory, ClientId = "app-id", ClientSecret = "app-secret" },
    };

    [Fact]
    public async Task AMailboxInTheInsurersOwnDirectoryGetsATokenFromThatDirectory()
    {
        var handler = new Recorder();
        var client = new MicrosoftGraphMailClient(new HttpClient(handler), Options("deployment-directory"));

        await client.ReadMessagesAsync(Mailbox("insurer-directory-id"));

        Assert.Contains("/insurer-directory-id/oauth2/v2.0/token", handler.Urls[0]);
        Assert.DoesNotContain("deployment-directory", handler.Urls[0]);
    }

    [Fact]
    public async Task AMailboxWithoutADirectoryUsesTheDeploymentsAndOneWithNeitherIsNotConfigured()
    {
        var handler = new Recorder();

        await new MicrosoftGraphMailClient(new HttpClient(handler), Options("deployment-directory")).ReadMessagesAsync(Mailbox(null));
        Assert.Contains("/deployment-directory/oauth2/v2.0/token", handler.Urls[0]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MicrosoftGraphMailClient(new HttpClient(new Recorder()), Options(null)).ReadMessagesAsync(Mailbox(null)));
    }

    [Fact]
    public async Task DownloadingAnAttachmentUsesTheSameDirectoryAsReadingTheMailbox()
    {
        var handler = new Recorder();
        var client = new MicrosoftGraphMailClient(new HttpClient(handler), Options("deployment-directory"));

        await client.DownloadAttachmentAsync("claims@insurer.test", "m1", "a1", "insurer-directory-id");

        Assert.Contains("/insurer-directory-id/oauth2/v2.0/token", handler.Urls[0]);
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            var body = request.RequestUri.ToString().Contains("/oauth2/", StringComparison.Ordinal)
                ? """{"access_token":"token"}"""
                : request.RequestUri.ToString().Contains("/attachments/", StringComparison.Ordinal)
                    ? """{"name":"a.pdf","contentType":"application/pdf","size":3,"contentBytes":"AQID"}"""
                    : """{"value":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

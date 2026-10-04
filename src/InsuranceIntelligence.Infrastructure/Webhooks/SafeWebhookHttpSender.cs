using System.Net;
using System.Net.Sockets;
using System.Text;
using InsuranceIntelligence.Application.Webhooks;

namespace InsuranceIntelligence.Infrastructure.Webhooks;

/// <summary>
/// Calls an insurer's webhook address. The address is resolved and checked at the moment of
/// connecting, and the connection goes to the address that was checked, so a name that later points
/// somewhere private (DNS rebinding) cannot be used to reach the platform's own network. Redirects
/// are not followed, for the same reason.
/// </summary>
public sealed class SafeWebhookHttpSender : IWebhookHttpSender, IDisposable
{
    private readonly HttpClient client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = ConnectToPublicAddressOnlyAsync,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public async Task<WebhookSendResult> SendAsync(
        Uri url, IReadOnlyDictionary<string, string> headers, string body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.UserAgent.ParseAdd("IIP-Webhooks/1.0");
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            return new WebhookSendResult((int)response.StatusCode, null);
        }
        catch (HttpRequestException)
        {
            return new WebhookSendResult(null, "Could not connect to the address.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebhookSendResult(null, "The receiver took too long to answer.");
        }
    }

    private static async ValueTask<Stream> ConnectToPublicAddressOnlyAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var allowed = addresses.FirstOrDefault(WebhookUrlPolicy.IsPublicAddress)
            ?? throw new HttpRequestException("The address resolves only to private network addresses.");

        var socket = new Socket(allowed.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(allowed, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public void Dispose() => client.Dispose();
}

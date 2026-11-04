using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Infrastructure.Email;

namespace InsuranceIntelligence.Infrastructure.ClientPortal;

public sealed record HostnameCheck(bool Verified, string? SafeReason);

/// <summary>Proves a workspace controls a web address before customers are sent there (build-loop item 29).</summary>
public interface IHostnameVerifier
{
    /// <summary>The path on the workspace's own address where it must publish the challenge.</summary>
    const string ChallengePath = "/.well-known/iip-portal-verification";

    Task<HostnameCheck> VerifyAsync(string hostname, string expectedChallenge, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads <c>https://{hostname}/.well-known/iip-portal-verification</c> and compares it with the challenge the platform issued.
/// Without this, any workspace could claim another company's web address and, once that company pointed its name at the
/// platform, receive its customers. The address is typed by a workspace administrator, so the request is made only to a public
/// internet address that was checked first (the same rule as IMAP hosts), over TLS with certificates validated, without
/// following redirects, and reads at most a few hundred bytes.
/// </summary>
public sealed class HttpHostnameVerifier(ImapEndpointPolicy endpointPolicy) : IHostnameVerifier
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int MaxBytes = 512;

    public async Task<HostnameCheck> VerifyAsync(string hostname, string expectedChallenge, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await endpointPolicy.ResolveAsync(hostname, cancellationToken);
        }
        catch (ImapEndpointRefusedException refused)
        {
            return new HostnameCheck(false, refused.Message);
        }

        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, token) =>
            {
                var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        using var client = new HttpClient(handler) { Timeout = Timeout };
        try
        {
            using var response = await client.GetAsync($"https://{hostname}{IHostnameVerifier.ChallengePath}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new HostnameCheck(false, $"The address did not publish the verification text (it answered {(int)response.StatusCode}).");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[MaxBytes];
            var read = await stream.ReadAsync(buffer, cancellationToken);
            var text = Encoding.UTF8.GetString(buffer, 0, read).Trim();
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes(expectedChallenge))
                ? new HostnameCheck(true, null)
                : new HostnameCheck(false, "The address published different text from the verification text.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or System.Security.Authentication.AuthenticationException)
        {
            return new HostnameCheck(false, "The address could not be reached over a secure connection.");
        }
    }
}

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace InsuranceIntelligence.Application.Webhooks;

/// <summary>
/// An insurer gives us a web address to call. Without care that is a way to make our servers call
/// anything the insurer names: a database on the private network, the cloud metadata service. So
/// only public https addresses are ever called, and the address actually connected to is checked
/// at the moment of connecting, not just when the subscription was made.
/// </summary>
public static class WebhookUrlPolicy
{
    public static bool TryValidate(string? url, out Uri? uri, out string error)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2000 || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
        {
            error = "Enter a full web address, for example https://erp.example.com/iip-events.";
            return false;
        }

        if (!string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            error = "The address must start with https://.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            error = "Do not put a username or password in the address; the shared secret signs every call.";
            return false;
        }

        var host = parsed.IdnHost;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var literal) && !IsPublicAddress(literal)))
        {
            error = "That address points inside a private network, which cannot be reached from the internet.";
            return false;
        }

        uri = parsed;
        error = string.Empty;
        return true;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (bytes[0] & 0xFE) == 0xFC); // unique local fc00::/7
        }

        var b = address.GetAddressBytes();
        return !(b[0] == 10
            || b[0] == 127
            || b[0] == 0
            || (b[0] == 100 && b[1] is >= 64 and <= 127)    // carrier-grade NAT
            || (b[0] == 169 && b[1] == 254)                 // link-local, includes the cloud metadata service
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)
            || b[0] >= 224);                                // multicast and reserved
    }
}

/// <summary>
/// Signs what we send so the receiver can tell it is from IIP and has not been changed or replayed.
/// The signature covers the timestamp as well as the body: sha256 HMAC of "{timestamp}.{body}", sent as
/// "sha256={hex}" with the timestamp in a separate header. A receiver rejects calls whose timestamp
/// is more than a few minutes old.
/// </summary>
public static class WebhookSignature
{
    public const string SignatureHeader = "X-IIP-Signature";
    public const string TimestampHeader = "X-IIP-Timestamp";
    public const string EventHeader = "X-IIP-Event";
    public const string DeliveryHeader = "X-IIP-Delivery";

    public static string Compute(string secret, long unixSeconds, string body)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{unixSeconds}.{body}"));
        return "sha256=" + Convert.ToHexString(mac).ToLowerInvariant();
    }

    public static bool Verify(string secret, long unixSeconds, string body, string? signature, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(signature) || Math.Abs((now - DateTimeOffset.FromUnixTimeSeconds(unixSeconds)).TotalSeconds) > tolerance.TotalSeconds)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Compute(secret, unixSeconds, body)), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

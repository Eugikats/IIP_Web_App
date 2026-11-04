using System.Net;
using System.Net.Sockets;

namespace InsuranceIntelligence.Infrastructure.Email;

/// <summary>Raised when a mail host a workspace named may not be connected to. The message is safe to show.</summary>
public sealed class ImapEndpointRefusedException(string message) : InvalidOperationException(message);

/// <summary>
/// Decides which mail hosts a workspace may point the platform at (build-loop item 28). The host is typed by an insurer's
/// administrator, so without a check it could be made to open connections to the platform's own network (its database,
/// cloud metadata service or other tenants' services) and report what answered. Only hosts that resolve entirely to public
/// internet addresses are allowed, and the connection is made to the address that was checked, so a name that changes its
/// answer between the check and the connection gains nothing. A developer or demonstration runtime may also reach private
/// addresses, so a local test mail server can be used.
/// </summary>
public sealed class ImapEndpointPolicy(bool allowPrivateNetworks)
{
    public const int MaxHostLength = 253;

    /// <summary>A plain host name or address: no scheme, port, path or spaces.</summary>
    public static string? ValidateHostSyntax(string? host)
    {
        var value = host?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > MaxHostLength)
        {
            return "Enter the mail host name, for example imap.example.com.";
        }

        // A colon is only for an IPv6 address; a port is entered separately.
        var isAddress = IPAddress.TryParse(value, out _);
        if (value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' || (character == ':' && isAddress))) || value.Contains("..", StringComparison.Ordinal))
        {
            return "The mail host must be a plain host name or address, without http://, a port or a path.";
        }

        return null;
    }

    public static string? ValidatePort(int port) =>
        port is < 1 or > 65535 ? "The port must be between 1 and 65535." : null;

    /// <summary>The addresses to connect to, every one of them checked. Throws <see cref="ImapEndpointRefusedException"/> when any
    /// answer is not a public address, or when the name does not resolve at all.</summary>
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (ValidateHostSyntax(host) is { } problem)
        {
            throw new ImapEndpointRefusedException(problem);
        }

        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(host.Trim(), out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host.Trim(), cancellationToken);
        }
        catch (SocketException)
        {
            throw new ImapEndpointRefusedException("The mail host could not be found. Check the host name.");
        }

        if (addresses.Length == 0)
        {
            throw new ImapEndpointRefusedException("The mail host could not be found. Check the host name.");
        }

        if (!allowPrivateNetworks && addresses.Any(address => !IsPublic(address)))
        {
            throw new ImapEndpointRefusedException("That mail host is not on the public internet, so it cannot be used.");
        }

        return addresses;
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 10
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)            // link-local, including the cloud metadata address
                || (b[0] == 100 && b[1] is >= 64 and <= 127) // carrier-grade NAT
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 198 && b[1] is 18 or 19)
                || b[0] >= 224);                            // multicast and reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC);                   // unique local fc00::/7
        }

        return false;
    }
}

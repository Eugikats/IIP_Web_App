using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.ClientPortal;

/// <summary>
/// How a workspace's customers reach it and what they see (build-loop item 29). Several insurers share one deployment, so the
/// claimant pages cannot know whose they are from a build-time setting: a customer's link carries the workspace's short name (the
/// slug), or the page is opened on the workspace's own verified web address, and the platform looks the workspace up from that.
/// The same record holds what customers see on their pages (name, logo, colour, who to call) and the secret address the SMS
/// gateway reports delivery to, so one workspace's gateway can never write into another's.
/// </summary>
public sealed record PortalIdentity
{
    public required TenantScope Scope { get; init; }

    /// <summary>The short name in the workspace's customer links. Unique across the deployment.</summary>
    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>A six-digit hex colour for the customer pages' main button, such as #1F5EFF. Must be dark enough for white text.</summary>
    public string? AccentColour { get; init; }

    /// <summary>An https address of the workspace's logo. Shown as an image on the customer pages.</summary>
    public string? LogoUrl { get; init; }

    public string? SupportPhone { get; init; }
    public string? SupportEmail { get; init; }

    /// <summary>The workspace's own web address for the customer pages, such as claims.insurer.com. Used only once it is verified.</summary>
    public string? Hostname { get; init; }

    /// <summary>What the workspace must publish at the hostname to prove it controls it. Cleared once verified.</summary>
    public string? HostnameChallenge { get; init; }

    public DateTimeOffset? HostnameVerifiedAt { get; init; }

    /// <summary>SHA-256 of the secret in the SMS gateway's callback address. The secret itself is shown once when made and never kept.</summary>
    public string? SmsWebhookSecretHash { get; init; }

    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public bool HostnameIsVerified => Hostname is not null && HostnameVerifiedAt is not null;

    public static Guid StorageKey(TenantScope scope)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.WorkspaceId}:portal-identity"));
        return new Guid(hash);
    }

    public static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    // ---- rules ----

    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.Ordinal)
    {
        "www", "api", "app", "admin", "portal", "access", "static", "assets", "support", "help", "mail", "email", "login", "signin",
        "signup", "auth", "demo", "test", "staging", "status", "billing", "iip", "otic", "claims", "shared", "new", "null", "undefined",
    };

    public static string? ValidateSlug(string? slug)
    {
        var value = slug ?? string.Empty;
        if (value.Length is < 3 or > 40)
        {
            return "The short name must be between 3 and 40 characters.";
        }

        if (!char.IsAsciiLetterLower(value[0]) || !value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') || value.EndsWith('-') || value.Contains("--", StringComparison.Ordinal))
        {
            return "Use lower-case letters, digits and single hyphens, starting with a letter, such as acme-insurance.";
        }

        return ReservedSlugs.Contains(value) ? "That short name is reserved. Choose another." : null;
    }

    public static string? ValidateHostname(string? hostname)
    {
        var value = hostname ?? string.Empty;
        if (value.Length is < 4 or > 253 || value != value.ToLowerInvariant())
        {
            return "Enter the web address in lower case, such as claims.insurer.com.";
        }

        var labels = value.Split('.');
        if (!value.Contains('.'))
        {
            return "Enter a full web address, such as claims.insurer.com.";
        }

        if (labels.Any(label => label.Length is 0 or > 63 || label.StartsWith('-') || label.EndsWith('-') || !label.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')))
        {
            return "The web address must be a plain host name, without http://, a port or a path.";
        }

        return value.All(c => char.IsAsciiDigit(c) || c == '.') ? "Use a host name, not an IP address." : null;
    }

    /// <summary>A six-digit hex colour that white text is readable on (contrast of at least 4.5 to 1).</summary>
    public static string? ValidateAccentColour(string? colour)
    {
        if (colour is null || colour.Length != 7 || colour[0] != '#' || !colour[1..].All(char.IsAsciiHexDigit))
        {
            return "Use a six-digit colour such as #1F5EFF.";
        }

        return ContrastWithWhite(colour) >= 4.5 ? null : "That colour is too light for white button text. Choose a darker one.";
    }

    public static double ContrastWithWhite(string colour)
    {
        double Channel(int start)
        {
            var value = int.Parse(colour.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var luminance = 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
        return 1.05 / (luminance + 0.05);
    }

    public static string? ValidateLogoUrl(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps && url.Length <= 500 && string.IsNullOrEmpty(parsed.UserInfo)
            ? null
            : "The logo must be an https address of up to 500 characters.";

    public static string? ValidateDisplayName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80 ? "Enter the name customers know you by, up to 80 characters." : null;
}

public interface IPortalIdentityRepository
{
    Task<PortalIdentity?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(PortalIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Every workspace's identity. Deliberately not scoped: a customer's link or web address names a workspace and the
    /// platform has to find which. Used only to build the routing snapshot and to keep short names and web addresses unique.</summary>
    Task<IReadOnlyList<PortalIdentity>> ListAllAsync(CancellationToken cancellationToken = default);
}

using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Organisations;

public enum LicenceKind
{
    Insurer,
    Broker,
    Agent,
    BancassuranceDistributor,
}

/// <summary>Who the organisation is, in its own words, and the licence it trades under. Kept in the organisation's own workspace.</summary>
public sealed record CompanyProfile(
    string LegalName,
    string? TradingName,
    LicenceKind LicenceKind,
    string LicenceNumber,
    string Regulator,
    string Country,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? Website,
    string About,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);

/// <summary>A named person accepting, for the organisation, a specific version of the terms and of the data processing agreement.</summary>
public sealed record TermsAcceptance(
    string TermsVersion,
    string DataProcessingVersion,
    string AcceptedByUserId,
    string AcceptedByName,
    string AcceptedByTitle,
    DateTimeOffset AcceptedAt);

/// <summary>A named person confirming they read how the organisation's data is protected, for the version of the statements in force.</summary>
public sealed record SecurityAcknowledgement(string Version, string AcknowledgedByUserId, string AcknowledgedByName, DateTimeOffset AcknowledgedAt);

public sealed class CompanyProfileException(string message) : Exception(message);

/// <summary>
/// The organisation's profile and its acceptance of the terms and the data processing agreement. The acceptance is tied to the
/// version in force: when a version changes the acceptance no longer counts and the organisation is asked again, and the old
/// record stays as the record of what was accepted then.
/// </summary>
public sealed class CompanyProfileService(IWorkspaceRecordStore store, TimeProvider clock)
{
    public const string ProfileKind = "company-profile";
    public const string TermsKind = "terms-acceptance";
    public const string Key = "current";
    public const string SecurityKind = "security-ack";

    /// <summary>The versions in force. Raise one when the document changes; everyone is asked again.</summary>
    public const string CurrentTermsVersion = "2026-10-01";
    public const string CurrentDataProcessingVersion = "2026-10-01";

    private static readonly Regex LicencePattern = new(@"^[A-Za-z0-9][A-Za-z0-9 \-/.]{1,38}[A-Za-z0-9]$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<CompanyProfile?> GetProfileAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, ProfileKind, Key, cancellationToken);
        return record is null ? null : Read<CompanyProfile>(record.PayloadJson);
    }

    public async Task<CompanyProfile> SaveProfileAsync(TenantScope scope, CompanyProfile input, string actorId, CancellationToken cancellationToken = default)
    {
        var clean = Validate(input) with { UpdatedAt = clock.GetUtcNow(), UpdatedBy = actorId };
        await store.SaveAsync(scope, ProfileKind, Key, JsonSerializer.Serialize(clean, Json), cancellationToken);
        return clean;
    }

    /// <summary>The acceptance on record, whether or not it is still for the version in force.</summary>
    public async Task<TermsAcceptance?> GetAcceptanceAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, TermsKind, Key, cancellationToken);
        return record is null ? null : Read<TermsAcceptance>(record.PayloadJson);
    }

    public async Task<bool> HasAcceptedCurrentAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await GetAcceptanceAsync(scope, cancellationToken) is { } accepted
        && accepted.TermsVersion == CurrentTermsVersion && accepted.DataProcessingVersion == CurrentDataProcessingVersion;

    public async Task<SecurityAcknowledgement?> GetSecurityAcknowledgementAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, SecurityKind, Key, cancellationToken);
        return record is null ? null : Read<SecurityAcknowledgement>(record.PayloadJson);
    }

    public async Task<bool> HasReadSecurityAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await GetSecurityAcknowledgementAsync(scope, cancellationToken) is { } ack && ack.Version == SecurityStatements.Version;

    public async Task<SecurityAcknowledgement> AcknowledgeSecurityAsync(
        TenantScope scope, string actorId, string? fullName, string? version, CancellationToken cancellationToken = default)
    {
        var name = Squash(fullName);
        if (name.Length is < 2 or > 100)
        {
            throw new CompanyProfileException("Type your full name to confirm you have read it.");
        }

        if (version != SecurityStatements.Version)
        {
            throw new CompanyProfileException("The security statements were updated. Reload the page and read the current version.");
        }

        var ack = new SecurityAcknowledgement(SecurityStatements.Version, actorId, name, clock.GetUtcNow());
        await store.SaveAsync(scope, SecurityKind, Key, JsonSerializer.Serialize(ack, Json), cancellationToken);
        return ack;
    }

    public async Task<TermsAcceptance> AcceptAsync(
        TenantScope scope, string actorId, string? fullName, string? title, bool authorised, string? termsVersion, string? dataProcessingVersion,
        CancellationToken cancellationToken = default)
    {
        var name = Squash(fullName);
        if (name.Length is < 2 or > 100)
        {
            throw new CompanyProfileException("Type your full name to accept for the organisation.");
        }

        var role = Squash(title);
        if (role.Length is < 2 or > 80)
        {
            throw new CompanyProfileException("Say your position in the organisation, for example Managing Director.");
        }

        if (!authorised)
        {
            throw new CompanyProfileException("Confirm that you are authorised to accept on the organisation's behalf.");
        }

        // The person must have been shown the versions in force, not an older page they had open.
        if (termsVersion != CurrentTermsVersion || dataProcessingVersion != CurrentDataProcessingVersion)
        {
            throw new CompanyProfileException("The terms were updated. Reload the page and read the current version before accepting.");
        }

        var accepted = new TermsAcceptance(CurrentTermsVersion, CurrentDataProcessingVersion, actorId, name, role, clock.GetUtcNow());
        await store.SaveAsync(scope, TermsKind, Key, JsonSerializer.Serialize(accepted, Json), cancellationToken);
        return accepted;
    }

    public static CompanyProfile Validate(CompanyProfile input)
    {
        var legal = Squash(input.LegalName);
        if (legal.Length is < 2 or > 120)
        {
            throw new CompanyProfileException("Give the organisation's registered name, up to 120 characters.");
        }

        var licence = Squash(input.LicenceNumber);
        if (!LicencePattern.IsMatch(licence))
        {
            throw new CompanyProfileException("Give the licence or registration number exactly as it appears on the certificate, 3 to 40 letters, numbers, spaces, dashes or slashes.");
        }

        if (!Enum.IsDefined(input.LicenceKind))
        {
            throw new CompanyProfileException("Say what kind of licence it is.");
        }

        var contact = Squash(input.ContactName);
        if (contact.Length is < 2 or > 100)
        {
            throw new CompanyProfileException("Give the name of the person we contact about this account.");
        }

        var email = (input.ContactEmail ?? string.Empty).Trim();
        if (email.Length > 160 || !MailAddress.TryCreate(email, out var address) || address.Address != email || !email.Contains('.'))
        {
            throw new CompanyProfileException("Give a working email address for that person.");
        }

        var website = string.IsNullOrWhiteSpace(input.Website) ? null : input.Website.Trim();
        if (website is not null && (website.Length > 200 || !Uri.TryCreate(website, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
        {
            throw new CompanyProfileException("The website must be a full address starting with https://.");
        }

        var about = Squash(input.About);
        if (about.Length is < 20 or > 600)
        {
            throw new CompanyProfileException("Describe the organisation in 20 to 600 characters: who it serves and what it sells.");
        }

        var regulator = Squash(input.Regulator);
        var country = Squash(input.Country);
        return input with
        {
            LegalName = legal,
            TradingName = string.IsNullOrWhiteSpace(input.TradingName) ? null : Squash(input.TradingName),
            LicenceNumber = licence,
            Regulator = regulator.Length == 0 ? "Insurance Regulatory Authority of Uganda" : regulator,
            Country = country.Length == 0 ? "Uganda" : country,
            ContactName = contact,
            ContactEmail = email,
            ContactPhone = string.IsNullOrWhiteSpace(input.ContactPhone) ? null : Squash(input.ContactPhone),
            Website = website,
            About = about,
        };
    }

    private static string Squash(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static T? Read<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

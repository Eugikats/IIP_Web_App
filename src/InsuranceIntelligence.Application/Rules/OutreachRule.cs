using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InsuranceIntelligence.Application.Rules;

public enum RuleStatus
{
    /// <summary>Being written, or changed since it was last approved. Does nothing.</summary>
    Draft,

    /// <summary>A person approved exactly these words and settings. It runs by itself within them.</summary>
    Approved,

    /// <summary>Switched off. Nothing new is considered while it is paused; what already went out stays in the log.</summary>
    Paused,
}

/// <summary>What must be true of the event for the rule to apply. Empty means any event of the type.</summary>
public sealed record RuleConditions(decimal? MinAmount = null, int? MinTermMonths = null);

/// <summary>Sending hours in East Africa time, as whole hours. A message due outside them waits for the next window.</summary>
public sealed record RuleHours(int FromHour = 9, int ToHour = 18);

/// <summary>How much a rule may send. These are what stop a long file from turning into a flood, and a person from hearing too often.</summary>
public sealed record RuleLimits(int OnePerPersonDays = 30, int SameCoverDays = 90, int DailyLimit = 50);

/// <summary>
/// A standing instruction, approved once: when this happens, offer this, in these words, within these limits. A person
/// approves the rule and the wording, not each message. Changing any part of it takes the approval away, and it is approved
/// again before it runs. The fingerprint ties an approval to the exact content that was approved.
/// </summary>
public sealed record OutreachRule(
    string RuleId,
    string Name,
    int Version,
    string EventType,
    RuleConditions Conditions,
    int WaitDays,
    IReadOnlyList<string> AllowedProductLines,
    string TemplateName,
    string Language,
    RuleHours Hours,
    RuleLimits Limits,
    int? FollowUpAfterDays,
    string? FollowUpTemplate,
    RuleStatus Status,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string? ApprovedBy = null,
    string? ApprovedByRole = null,
    DateTimeOffset? ApprovedAt = null,
    string? ApprovedFingerprint = null)
{
    private static readonly JsonSerializerOptions Canonical = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>A hash of everything a person approves: what triggers it, what it offers, how it speaks, and its limits.</summary>
    public string Fingerprint()
    {
        var content = new
        {
            EventType, Conditions, WaitDays, Lines = AllowedProductLines.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            TemplateName, Language, Hours, Limits, FollowUpAfterDays, FollowUpTemplate,
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content, Canonical)))).ToLowerInvariant();
    }

    /// <summary>True only when approved and still exactly what was approved.</summary>
    public bool IsLive => Status == RuleStatus.Approved && ApprovedFingerprint == Fingerprint();
}

public sealed class RuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public static RuleException Invalid(string message) => new("invalid", message);
    public static RuleException NotFound(string message) => new("not_found", message);
    public static RuleException Forbidden(string message) => new("forbidden", message);
    public static RuleException Conflict(string message) => new("conflict", message);
}

/// <summary>Who may approve a rule. The workspace's administrator chooses, because a bank decides for itself whether marketing,
/// compliance or the bancassurance lead signs off.</summary>
public sealed record RuleApprovalSettings(IReadOnlyList<string> AllowedRoles, bool RequireDifferentApprover = false)
{
    public static readonly RuleApprovalSettings Default = new(["Admin"]);
}

/// <summary>The words a template's placeholders may stand for when a rule fills them. A rule can only use a template whose
/// placeholders are all in this list for its trigger.</summary>
public static class RuleValues
{
    public static readonly IReadOnlySet<string> ForEvents = new HashSet<string>(StringComparer.Ordinal)
    {
        "customer_name", "organisation", "bank_relationship", "cover_name", "product",
    };
}

using System.Net.Mail;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Compliance;

/// <summary>When a claims pack is emailed without anyone pressing a button (build-loop item 25).</summary>
public enum ClaimsPackTrigger
{
    /// <summary>Only when staff press "Send pack now".</summary>
    Manual,

    /// <summary>When the claim's documents become complete (100 percent).</summary>
    Complete,

    /// <summary>When the claim reaches one chosen status.</summary>
    Status,
}

/// <summary>The rules for emailing a claims pack: who may receive it, when it goes, and whether it is attached or linked.</summary>
public static class ClaimsPackDeliveryRules
{
    public const int MaxRecipients = 10;

    /// <summary>A pack at or under this goes attached to the email. Larger ones go as a secure link that expires.</summary>
    public const long MaxAttachmentBytes = 3L * 1024 * 1024;

    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(7);
    public const int MaxLinkDownloads = 10;

    public static bool TryParseTrigger(string? value, out ClaimsPackTrigger trigger) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out trigger) && Enum.IsDefined(trigger);

    public static IReadOnlyList<string> Normalise(IEnumerable<string>? recipients) =>
        (recipients ?? [])
            .Select(address => address?.Trim() ?? string.Empty)
            .Where(address => address.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<string> Validate(IReadOnlyList<string>? recipients, string? trigger, string? triggerStatus)
    {
        var errors = new List<string>();
        var list = recipients ?? [];
        if (list.Count > MaxRecipients)
        {
            errors.Add($"A claims pack can go to at most {MaxRecipients} email addresses.");
        }

        foreach (var address in list.Where(address => !MailAddress.TryCreate(address, out var parsed) || !string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase) || !address.Contains('.')))
        {
            errors.Add($"{address} is not a valid email address.");
        }

        if (!string.IsNullOrWhiteSpace(trigger) && !TryParseTrigger(trigger, out _))
        {
            errors.Add("Choose when the pack is sent: only by hand, when the documents are complete, or when a claim reaches a status.");
        }

        if (TryParseTrigger(trigger, out var parsedTrigger) && parsedTrigger == ClaimsPackTrigger.Status
            && (!Enum.TryParse<CaseStatus>(triggerStatus?.Trim(), ignoreCase: true, out var status) || !Enum.IsDefined(status)))
        {
            errors.Add("Choose the status that sends the pack.");
        }

        if (TryParseTrigger(trigger, out var automatic) && automatic != ClaimsPackTrigger.Manual && list.Count == 0)
        {
            errors.Add("Add at least one email address before the pack can be sent automatically.");
        }

        return errors;
    }

    /// <summary>Whether a claim's completeness sends the pack: it is at 100 percent. The pack goes once per claim, so
    /// asking again while it stays complete does nothing.</summary>
    public static bool CompletenessTriggers(ClaimsPackTrigger trigger, decimal after) =>
        trigger == ClaimsPackTrigger.Complete && after >= 100m;

    public static bool StatusTriggers(ClaimsPackTrigger trigger, string? triggerStatus, CaseStatus before, CaseStatus after) =>
        trigger == ClaimsPackTrigger.Status && before != after && string.Equals(triggerStatus, after.ToString(), StringComparison.OrdinalIgnoreCase);

    public static string MaskAddress(string address)
    {
        var at = address.IndexOf('@');
        return at > 1 ? address[0] + "***" + address[at..] : address;
    }
}

/// <summary>One time a claims pack was emailed, or tried to be. Keeps who it went to (masked), how it went and why.</summary>
public sealed record ClaimsPackDispatch
{
    public required Guid DispatchId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }

    /// <summary>What caused it: "manual" with a fresh id each time, "complete", or "status:Closed". An automatic reason is sent at most once per claim.</summary>
    public required string ReasonKey { get; init; }
    public required string Reason { get; init; }
    public required string SentBy { get; init; }
    public required DateTimeOffset At { get; init; }
    public required IReadOnlyList<string> RecipientsMasked { get; init; }

    /// <summary>Attached, Link or NotSent.</summary>
    public required string Mode { get; init; }
    public required string Outcome { get; init; }
    public int FileCount { get; init; }
    public long PackBytes { get; init; }
    public DateTimeOffset? LinkExpiresAt { get; init; }
}

public interface IClaimsPackDispatchRepository
{
    Task SaveAsync(ClaimsPackDispatch dispatch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimsPackDispatch>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);
}

/// <summary>A download link for one stored pack, valid for a week and a few downloads. Only a fingerprint of the link is kept.</summary>
public sealed record ClaimsPackShareLink
{
    public required Guid LinkId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required string TokenHash { get; init; }
    public required string ObjectKey { get; init; }
    public required string FileName { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public int DownloadCount { get; init; }
    public int MaxDownloads { get; init; } = ClaimsPackDeliveryRules.MaxLinkDownloads;

    public bool IsUsableAt(DateTimeOffset now) => now < ExpiresAt && DownloadCount < MaxDownloads;
}

public interface IClaimsPackShareLinkRepository
{
    Task SaveAsync(ClaimsPackShareLink link, CancellationToken cancellationToken = default);

    /// <summary>Finds a link by the fingerprint of its token. The token is long and random, so it identifies the link on its own.</summary>
    Task<ClaimsPackShareLink?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}

using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>An app subscribed to a WhatsApp Business Account, and the address it overrides, if any.</summary>
public sealed record WhatsAppSubscribedApp(string AppId, string? Name, string? OverrideCallbackUri);

/// <summary>What Meta says about where a number's messages are sent. Read with the connection's own token, never stored.</summary>
public sealed record WhatsAppMetaFacts(
    string? Error,
    string? PhoneStatus,
    string? AccountMode,
    string? PhoneLevelCallback,
    string? AccountLevelCallback,
    string? AppLevelCallback,
    IReadOnlyList<WhatsAppSubscribedApp> SubscribedApps);

/// <summary>One plain sentence about delivery. Level is "ok", "warning" or "problem".</summary>
public sealed record DeliveryFinding(string Level, string Text);

/// <summary>
/// Turns what Meta reports into the questions a person would ask: is the number connected, and does Meta send its
/// messages to IIP or somewhere else? Written after 12 October 2026, when IIP's connection looked healthy for days
/// while Meta's messages were going elsewhere and nothing on screen said so.
/// </summary>
public static class WhatsAppDeliveryAnalysis
{
    public static IReadOnlyList<DeliveryFinding> Analyse(string? iipCallback, WhatsAppMetaFacts facts)
    {
        var findings = new List<DeliveryFinding>();
        if (!string.IsNullOrWhiteSpace(facts.Error))
        {
            findings.Add(new DeliveryFinding("problem", $"Meta could not be asked: {facts.Error}"));
            return findings;
        }

        if (!string.IsNullOrWhiteSpace(facts.PhoneStatus))
        {
            findings.Add(string.Equals(facts.PhoneStatus, "CONNECTED", StringComparison.OrdinalIgnoreCase)
                ? new DeliveryFinding("ok", "The number is connected on Meta.")
                : new DeliveryFinding("problem", $"Meta reports this number as {facts.PhoneStatus}, so it may not be sending or receiving."));
        }

        if (string.Equals(facts.AccountMode, "SANDBOX", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new DeliveryFinding("warning", "The number is in sandbox mode, so only test messages reach it."));
        }

        var ours = Normalise(iipCallback);
        var phone = Normalise(facts.PhoneLevelCallback);
        var account = Normalise(facts.AccountLevelCallback);
        var overridden = facts.SubscribedApps.Any(app => ours is not null && Normalise(app.OverrideCallbackUri) == ours);

        if (phone is not null && phone != ours)
        {
            findings.Add(new DeliveryFinding("problem",
                $"This number is set to send its messages to {Host(phone)}, not to IIP. A setting on the number itself outranks the account's. Remove it in Meta, or save the connection again."));
        }
        else if (ours is not null && (phone == ours || account == ours || overridden))
        {
            findings.Add(new DeliveryFinding("ok", "Meta is set to send this number's messages to IIP."));
        }
        else if (ours is null)
        {
            findings.Add(new DeliveryFinding("warning", "IIP has no public address of its own set, so it cannot tell whether Meta is pointed at it."));
        }
        else
        {
            findings.Add(new DeliveryFinding("problem", "Nothing on Meta sends this number's messages to IIP. Save the WhatsApp connection again to register it."));
        }

        var app = Normalise(facts.AppLevelCallback);
        if (app is not null && app != ours)
        {
            findings.Add(new DeliveryFinding("ok",
                $"The Meta app's own default address is {Host(app)}. IIP's address overrides it for this account, so that default does not take messages away from IIP."));
        }

        var others = facts.SubscribedApps.Where(a => !(ours is not null && Normalise(a.OverrideCallbackUri) == ours)).ToArray();
        if (others.Length > 0)
        {
            findings.Add(new DeliveryFinding("warning",
                $"{others.Length} other app{(others.Length == 1 ? " is" : "s are")} also subscribed to this WhatsApp account ({string.Join(", ", others.Select(a => a.Name ?? a.AppId))}). Each receives messages at its own address."));
        }

        findings.Add(new DeliveryFinding("warning",
            "Meta only delivers real customer messages to an app that is published. This check cannot see that setting: in Meta's dashboard, open the app and check it says Live and that the messages field is subscribed."));
        return findings;
    }

    private static string? Normalise(string? address) =>
        string.IsNullOrWhiteSpace(address) ? null : address.Trim().TrimEnd('/').ToLowerInvariant();

    private static string Host(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : address;
}

/// <summary>One call to the webhook, as the platform saw it. Holds counts and an outcome, never message content.</summary>
public sealed record WhatsAppWebhookReceipt
{
    public required Guid Id { get; init; }
    public required TenantScope Scope { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public required string PhoneNumberId { get; init; }

    /// <summary>"accepted", "duplicate_only", "refused_signature" or "no_messages".</summary>
    public required string Outcome { get; init; }

    public int MessagesInCall { get; init; }
    public int MessagesNew { get; init; }
    public int DeliveryUpdates { get; init; }

    public static Guid NewKey(TenantScope scope) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.WorkspaceId}:whatsapp-receipt:{Guid.NewGuid():N}")));
}

public interface IWhatsAppWebhookReceiptRepository
{
    Task AddAsync(WhatsAppWebhookReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<WhatsAppWebhookReceipt>> ListAsync(TenantScope scope, int limit, CancellationToken cancellationToken = default);
}

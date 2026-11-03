using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;

namespace InsuranceIntelligence.Application.Rules;

public enum OutcomeKind
{
    /// <summary>They wrote back, but not in a way that settles anything.</summary>
    Replied,

    /// <summary>They asked for details or said yes. A person may follow up.</summary>
    Interested,

    /// <summary>Not now. Nothing more is offered in this conversation.</summary>
    NotNow,

    /// <summary>No thanks. Nothing more is offered.</summary>
    Declined,

    /// <summary>They asked to renew. A person completes it.</summary>
    RenewalRequested,

    /// <summary>They said stop. Consent is withdrawn and nothing more is sent.</summary>
    Stopped,

    /// <summary>They took the cover up.</summary>
    Applied,
}

/// <summary>
/// What came of an offer: how the person answered, kept with the rule, the cover and the opening words that prompted it. An
/// answer that needs a person (they want details, to renew, or to buy) is also a hand-off: it is listed for the team with all
/// of that context until someone marks it done.
/// </summary>
public sealed record OfferOutcome(
    string OutcomeId,
    string Phone,
    string RuleId,
    string RuleName,
    string EventType,
    string Cover,
    string Insurer,
    string OpeningText,
    DateTimeOffset OfferedAt,
    OutcomeKind Kind,
    DateTimeOffset At,
    string? Reply,
    bool NeedsPerson,
    string? LeadId,
    string? CustomerName,
    bool Done = false,
    DateTimeOffset? DoneAt = null,
    string? DoneBy = null);

public sealed record ReplyHandled(OutcomeKind? Kind, bool Stop);

public sealed class OutcomeService(
    IWorkspaceRecordStore store,
    DecisionLog log,
    ISalesLeadWriteRepository leads,
    IConsentRepository? consents = null)
{
    public const string Kind = "outcome";
    public static readonly TimeSpan ReplyWindow = TimeSpan.FromDays(14);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly Regex StopPattern = new(
        @"^\s*(stop|stop (all|messages|messaging|sending|texting|it)|unsubscribe|opt ?out|cancel messages|no more messages|do not message me|dont message me|don't message me|leave me alone|simama)\W*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> NotNowWords = new(StringComparer.Ordinal) { "not now", "later", "maybe later", "not today", "another time", "next time" };
    private static readonly HashSet<string> DeclineWords = new(StringComparer.Ordinal) { "no", "no thanks", "no thank you", "not interested", "nope", "nah" };
    private static readonly HashSet<string> YesWords = new(StringComparer.Ordinal)
    {
        "yes", "yes show me", "show me", "tell me more", "yes please", "ok", "okay", "sure", "interested", "i am interested", "more details", "details", "yes tell me more",
    };
    private static readonly HashSet<string> RenewWords = new(StringComparer.Ordinal) { "renew", "yes renew", "yes renew it", "renew it", "please renew", "i want to renew" };

    /// <summary>Reads what a short reply means. Deliberately narrow: only the replies the opening message's buttons and the obvious
    /// words produce; anything else is simply a reply, and the assistant carries on the conversation.</summary>
    public static OutcomeKind? Classify(string text, bool renewalContext)
    {
        var flat = Regex.Replace((text ?? string.Empty).ToLowerInvariant(), @"[^\p{L}\p{N}' ]+", " ").Trim();
        flat = Regex.Replace(flat, @"\s+", " ");
        if (StopPattern.IsMatch(text ?? string.Empty))
        {
            return OutcomeKind.Stopped;
        }

        if (RenewWords.Contains(flat) || (renewalContext && flat is "yes" or "yes renew" or "ok" or "yes please"))
        {
            return OutcomeKind.RenewalRequested;
        }

        if (NotNowWords.Contains(flat)) return OutcomeKind.NotNow;
        if (DeclineWords.Contains(flat)) return OutcomeKind.Declined;
        if (YesWords.Contains(flat)) return OutcomeKind.Interested;
        return null;
    }

    /// <summary>
    /// Looks at a reply from someone a rule wrote to. A STOP is recognised from anyone, with or without a rule behind it. Any
    /// other reply inside the window is recorded against the offer that began the conversation.
    /// </summary>
    public async Task<ReplyHandled> RecordReplyAsync(TenantScope scope, string phone, string text, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var reason = await log.GetReasonAsync(scope, phone, cancellationToken);
        var active = reason is not null && now - reason.SentAt <= ReplyWindow;
        var kind = Classify(text, renewalContext: active && reason!.EventType.StartsWith("policy_", StringComparison.Ordinal));
        if (kind == OutcomeKind.Stopped && !active)
        {
            return new ReplyHandled(OutcomeKind.Stopped, Stop: true);
        }

        if (!active || reason is null)
        {
            return new ReplyHandled(null, Stop: false);
        }

        var lead = await leads.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, phone, cancellationToken);
        var recorded = kind ?? OutcomeKind.Replied;
        var id = IdOf(phone, reason);
        var previous = await GetAsync(scope, id, cancellationToken);
        // Once someone has said stop, or declined, a later "ok" does not undo it.
        if (previous is { Kind: OutcomeKind.Stopped or OutcomeKind.Declined } && recorded is OutcomeKind.Replied or OutcomeKind.Interested)
        {
            return new ReplyHandled(previous.Kind, Stop: false);
        }

        var needsPerson = recorded is OutcomeKind.Interested or OutcomeKind.RenewalRequested;
        var outcome = new OfferOutcome(
            id, phone, reason.RuleId, reason.RuleName, reason.EventType, reason.Cover, reason.Insurer, reason.OpeningText, reason.SentAt,
            recorded, now, Excerpt(text), needsPerson, lead?.LeadId, lead?.CustomerName);
        await store.SaveAsync(scope, Kind, id, JsonSerializer.Serialize(outcome, Json), cancellationToken);

        if (lead is not null && recorded != OutcomeKind.Replied)
        {
            var line = recorded switch
            {
                OutcomeKind.Interested => $"Asked for details of {reason.Cover} after the offer ({reason.RuleName}). A colleague can follow up.",
                OutcomeKind.RenewalRequested => $"Asked to renew {reason.Cover}. A colleague completes the renewal.",
                OutcomeKind.NotNow => $"Said not now to the offer of {reason.Cover}.",
                OutcomeKind.Declined => $"Declined the offer of {reason.Cover}.",
                OutcomeKind.Stopped => "Asked for no more messages.",
                _ => "Replied to the offer.",
            };
            await leads.CreateAsync(scope.TenantId, scope.WorkspaceId, lead with { Activity = [.. lead.Activity, new ProspectActivity(now, line, "Kato")] }, cancellationToken);
        }

        return new ReplyHandled(recorded, Stop: recorded == OutcomeKind.Stopped);
    }

    /// <summary>Takes away the agreement to be contacted: on the prospect record and in the consent history, so no later message
    /// from any rule, template or person can go out. Done after the one acknowledgement is sent.</summary>
    public async Task WithdrawAsync(TenantScope scope, string phone, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var lead = await leads.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, phone, cancellationToken);
        if (lead is not null)
        {
            await leads.CreateAsync(
                scope.TenantId, scope.WorkspaceId,
                lead with
                {
                    Consent = new ContactConsent(ContactConsentStatus.Withdrawn, now, "Replied STOP to a message from us.", "WhatsApp"),
                    Activity = [.. lead.Activity, new ProspectActivity(now, "Replied STOP. Consent to be contacted was withdrawn and nothing more will be sent.", "Kato")],
                },
                cancellationToken);
        }

        if (consents is not null)
        {
            await consents.SaveAsync(new ConsentRecord
            {
                RecordId = Guid.NewGuid(),
                Scope = scope,
                CustomerIdentifier = phone,
                Basis = ConsentBasis.Withdrawn,
                Purpose = ConsentPurposes.WhatsApp,
                Channel = "whatsapp",
                WithdrawnAt = now,
                RecordedByUserId = "customer",
                RecordedAt = now,
                CorrelationId = $"stop-{Guid.NewGuid():N}",
            }, cancellationToken);
        }

        var reason = await log.GetReasonAsync(scope, phone, cancellationToken);
        if (reason is not null)
        {
            var id = IdOf(phone, reason);
            var existing = await GetAsync(scope, id, cancellationToken);
            if (existing is null || existing.Kind != OutcomeKind.Stopped)
            {
                var stopped = new OfferOutcome(
                    id, phone, reason.RuleId, reason.RuleName, reason.EventType, reason.Cover, reason.Insurer, reason.OpeningText, reason.SentAt,
                    OutcomeKind.Stopped, now, "STOP", false, lead?.LeadId, lead?.CustomerName);
                await store.SaveAsync(scope, Kind, id, JsonSerializer.Serialize(stopped, Json), cancellationToken);
            }
        }
    }

    public async Task<OfferOutcome?> GetAsync(TenantScope scope, string outcomeId, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(scope, Kind, outcomeId, cancellationToken);
        return record is null ? null : Read(record.PayloadJson);
    }

    public async Task<IReadOnlyList<OfferOutcome>> ListAsync(TenantScope scope, bool? needsPerson = null, int take = 500, CancellationToken cancellationToken = default) =>
        (await store.ListAsync(scope, Kind, 5000, cancellationToken))
            .Select(record => Read(record.PayloadJson))
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item => needsPerson is null || (item.NeedsPerson && !item.Done) == needsPerson)
            .OrderByDescending(item => item.At)
            .Take(Math.Clamp(take, 1, 5000))
            .ToArray();

    public async Task<OfferOutcome> MarkDoneAsync(TenantScope scope, string outcomeId, string actorId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var outcome = await GetAsync(scope, outcomeId, cancellationToken) ?? throw RuleException.NotFound("That reply is not in this workspace.");
        var done = outcome with { Done = true, DoneAt = now, DoneBy = actorId };
        await store.SaveAsync(scope, Kind, outcomeId, JsonSerializer.Serialize(done, Json), cancellationToken);
        return done;
    }

    /// <summary>True when the person has answered the offer in any way, so a follow-up nudge is not needed.</summary>
    public async Task<bool> HasAnsweredAsync(TenantScope scope, string phone, ConversationReason reason, CancellationToken cancellationToken = default) =>
        await GetAsync(scope, IdOf(phone, reason), cancellationToken) is not null;

    private static string IdOf(string phone, ConversationReason reason) => $"{phone}:{reason.RuleId}:{reason.SentAt.ToUnixTimeSeconds()}";

    private static string Excerpt(string text)
    {
        var line = string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 160 ? line : line[..157] + "...";
    }

    private static OfferOutcome? Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OfferOutcome>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

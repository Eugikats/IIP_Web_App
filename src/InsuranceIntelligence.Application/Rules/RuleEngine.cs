using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Templates;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Rules;

public sealed record RuleRunSummary(int Considered, int Sent, int DemoSent, int Skipped, int Scheduled, int Blocked);

/// <summary>
/// Looks at each bank event that has not been looked at, against every rule that is approved and unchanged, and decides
/// whether to message the person. It does exactly what a person approved and no more: the consent, ownership of cover,
/// contact limits, hours and daily limit are all checked, in the open, and every decision is logged whether it sends or
/// not. The cover is chosen from what the rule allows by how well it fits the person's recorded facts; what the cover pays
/// the bank never enters into it.
/// </summary>
public sealed class RuleEngine(
    RuleService rules,
    DecisionLog log,
    EventService events,
    WorkspaceBrandService brands,
    TemplateCatalogService templates,
    TemplateMessageService messages,
    ISalesLeadWriteRepository leads,
    ICustomerRepository customers,
    IPolicyRepository policies,
    IWhatsAppMessageRepository messageRepository,
    TimeProvider clock)
{
    private static readonly TimeSpan EastAfrica = TimeSpan.FromHours(3);
    public const string DemoNumberId = "demo-number";

    public async Task<RuleRunSummary> RunAsync(TenantScope scope, string actorId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var liveRules = (await rules.ListAsync(scope, cancellationToken)).Where(rule => rule.IsLive).ToArray();
        int considered = 0, sent = 0, demo = 0, skipped = 0, scheduled = 0, blocked = 0;
        if (liveRules.Length == 0)
        {
            return new RuleRunSummary(0, 0, 0, 0, 0, 0);
        }

        var brand = await brands.ResolveAsync(scope, cancellationToken);
        var settings = await brands.GetAsync(scope, cancellationToken);
        var existing = (await log.ListAsync(scope, take: 5000, cancellationToken: cancellationToken)).ToDictionary(decision => decision.DecisionId, StringComparer.Ordinal);

        foreach (var item in (await events.ListAsync(scope, take: 1000, cancellationToken: cancellationToken)).OrderBy(item => item.OccurredAt))
        {
            var applicable = liveRules.Where(rule => rule.EventType == item.EventType).ToArray();
            var allFinal = true;
            foreach (var rule in applicable)
            {
                var id = $"{item.EventId}:{rule.RuleId}";
                if (existing.TryGetValue(id, out var previous) && previous.IsFinal)
                {
                    continue;
                }

                if (previous is { NotBefore: { } waitUntil } && waitUntil > now)
                {
                    allFinal = false;
                    continue;
                }

                considered++;
                var decision = await DecideAsync(scope, rule, item, brand, settings?.IsDemonstration == true, now, actorId, existing.Values, cancellationToken);
                await log.SaveAsync(scope, decision, cancellationToken);
                existing[id] = decision;
                allFinal &= decision.IsFinal;
                switch (decision.Outcome)
                {
                    case DecisionOutcome.Sent: sent++; break;
                    case DecisionOutcome.DemoSent: demo++; break;
                    case DecisionOutcome.Skipped: skipped++; break;
                    case DecisionOutcome.Scheduled: scheduled++; break;
                    default: blocked++; break;
                }
            }

            if (allFinal && item.State == EventProcessingState.Pending)
            {
                await events.MarkProcessedAsync(scope, item.EventId, cancellationToken);
            }
        }

        return new RuleRunSummary(considered, sent, demo, skipped, scheduled, blocked);
    }

    private async Task<RuleDecision> DecideAsync(
        TenantScope scope, OutreachRule rule, WorkspaceEvent item, KatoBrand brand, bool demonstration, DateTimeOffset now, string actorId,
        IEnumerable<RuleDecision> history, CancellationToken cancellationToken)
    {
        var gates = new List<DecisionGate>();
        RuleDecision Done(DecisionOutcome outcome, string reason, DateTimeOffset? notBefore = null, MicroinsuranceCatalog.MicroProduct? cover = null, string? lead = null, string? name = null, string? messageId = null) =>
            new($"{item.EventId}:{rule.RuleId}", rule.RuleId, rule.Name, rule.Version, item.EventId, item.EventType, item.Phone, lead, name, gates, outcome, reason, now,
                notBefore, cover?.Name, cover?.Insurer, rule.TemplateName, messageId);

        bool Gate(string gateName, bool passed, string detail)
        {
            gates.Add(new DecisionGate(gateName, passed, detail));
            return passed;
        }

        // The event itself
        if (rule.Conditions.MinAmount is { } minAmount)
        {
            if (!Gate("Loan size", item.Amount >= minAmount, item.Amount is null ? "The event has no amount." : $"UGX {item.Amount:N0}, the rule needs at least UGX {minAmount:N0}."))
            {
                return Done(DecisionOutcome.Skipped, "The amount is below what the rule applies to.");
            }
        }

        if (rule.Conditions.MinTermMonths is { } minTerm)
        {
            if (!Gate("Loan term", item.TermMonths >= minTerm, item.TermMonths is null ? "The event has no term." : $"{item.TermMonths} months, the rule needs at least {minTerm}."))
            {
                return Done(DecisionOutcome.Skipped, "The term is shorter than the rule applies to.");
            }
        }

        // The person
        var lead = await leads.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, item.Phone, cancellationToken);
        if (!Gate("Known customer", lead is not null, lead is null ? "No customer record for this phone number in this workspace." : "Found on the customer list."))
        {
            return Done(DecisionOutcome.Skipped, "This phone number is not a customer in this workspace.");
        }

        var leadId = lead!.LeadId;
        var name = lead.CustomerName;
        var consent = lead.Consent;
        var agreed = consent?.Status == ContactConsentStatus.Agreed;
        if (!Gate("Agreed to be contacted", agreed, agreed
                ? $"Agreed on {consent!.At:d MMM yyyy} ({consent.Channel})."
                : consent?.Status is ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn ? "Asked not to be contacted." : "No agreement to be contacted is recorded."))
        {
            return Done(DecisionOutcome.Skipped, consent?.Status is ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn
                ? "They asked not to be contacted." : "There is no recorded agreement to be contacted.", lead: leadId, name: name);
        }

        // Cover already held
        var onPhone = (await customers.ListAsync(scope, cancellationToken)).Where(customer => PhoneNumbers.SameNumber(customer.Phone, item.Phone)).Select(customer => customer.CustomerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var held = (await policies.ListAsync(scope, cancellationToken))
            .Where(policy => policy.CustomerId is not null && onPhone.Contains(policy.CustomerId) && (policy.EffectiveTo is null || policy.EffectiveTo >= DateOnly.FromDateTime(now.UtcDateTime)))
            .Select(policy => policy.ProductLine)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = brand.Products.Where(product => rule.AllowedProductLines.Contains(product.ProductLine, StringComparer.OrdinalIgnoreCase) && !held.Contains(product.ProductLine)).ToArray();
        if (!Gate("Does not already hold the cover", candidates.Length > 0,
                candidates.Length > 0 ? (held.Count == 0 ? "Holds no cover with this organisation." : $"Holds {string.Join(", ", held)}; the rest can be offered.") : "Already holds every cover the rule may offer."))
        {
            return Done(DecisionOutcome.Skipped, "They already hold the covers this rule offers.", lead: leadId, name: name);
        }

        // Not contacted too often
        var mine = history.Where(decision => decision.Phone == item.Phone && decision.WentOut && decision.Kind == "offer").ToArray();
        var recent = mine.FirstOrDefault(decision => now - decision.DecidedAt < TimeSpan.FromDays(rule.Limits.OnePerPersonDays));
        if (!Gate("Not contacted recently", recent is null,
                recent is null ? $"No offer in the last {rule.Limits.OnePerPersonDays} days." : $"Offered {recent.Cover} on {recent.DecidedAt:d MMM yyyy}, and the limit is one offer every {rule.Limits.OnePerPersonDays} days."))
        {
            return Done(DecisionOutcome.Skipped, "They were offered something too recently.", lead: leadId, name: name);
        }

        // Which cover
        MicroinsuranceCatalog.MicroProduct cover;
        string fitDetail;
        if (candidates.Length == 1)
        {
            cover = candidates[0];
            fitDetail = $"{cover.Name} is the only cover this rule offers.";
        }
        else
        {
            var inputs = new ProspectInputs(
                lead.Facts, lead.Interests, lead.BankCustomer, HoldsCover: false, Registered: false, consent!.Status, HasPhone: true,
                lead.LastInteractionAt ?? lead.CreatedAt, CustomerMessages: 0, Applied: false);
            var triage = ProspectScoring.Score(inputs, ProductTargeting.ProfilesFor(candidates), now);
            var best = triage.Fits.OrderByDescending(fit => fit.Score).FirstOrDefault();
            cover = candidates.FirstOrDefault(product => product.Name == best?.Product) ?? candidates[0];
            fitDetail = best is null
                ? $"No cover stood out, so {cover.Name} (the first the rule allows) was chosen."
                : $"{cover.Name} fits best: {string.Join("; ", best.Reasons.Take(3))}.";
        }

        Gate("Best-fit cover", true, fitDetail);
        var sameCover = mine.FirstOrDefault(decision => decision.Cover == cover.Name && now - decision.DecidedAt < TimeSpan.FromDays(rule.Limits.SameCoverDays));
        if (!Gate("Same cover not offered twice", sameCover is null, sameCover is null ? $"{cover.Name} has not been offered in the last {rule.Limits.SameCoverDays} days." : $"{cover.Name} was offered on {sameCover.DecidedAt:d MMM yyyy}."))
        {
            return Done(DecisionOutcome.Skipped, "This cover was offered to them recently.", cover: cover, lead: leadId, name: name);
        }

        // When
        var due = item.OccurredAt.AddDays(rule.WaitDays);
        if (!Gate("Waited long enough", due <= now, due <= now ? $"{rule.WaitDays} day(s) have passed since the event." : $"Due on {(due + EastAfrica):d MMM yyyy HH:mm} East Africa time."))
        {
            return Done(DecisionOutcome.Scheduled, $"Waiting until {(due + EastAfrica):d MMM yyyy}.", due, cover, leadId, name);
        }

        var local = now + EastAfrica;
        var inHours = local.Hour >= rule.Hours.FromHour && local.Hour < rule.Hours.ToHour;
        if (!Gate("Inside sending hours", inHours, $"{rule.Hours.FromHour:00}:00 to {rule.Hours.ToHour:00}:00 East Africa time; it is {local:HH:mm}."))
        {
            var next = NextWindow(now, rule.Hours.FromHour);
            return Done(DecisionOutcome.Scheduled, $"Outside sending hours; will go at {(next + EastAfrica):d MMM HH:mm}.", next, cover, leadId, name);
        }

        var today = (now + EastAfrica).Date;
        var sentToday = history.Count(decision => decision.RuleId == rule.RuleId && decision.WentOut && (decision.DecidedAt + EastAfrica).Date == today);
        if (!Gate("Within the daily limit", sentToday < rule.Limits.DailyLimit, $"{sentToday} of {rule.Limits.DailyLimit} sent today."))
        {
            var next = NextWindow(now.AddDays(1), rule.Hours.FromHour);
            return Done(DecisionOutcome.Scheduled, "The daily limit has been reached; will go tomorrow.", next, cover, leadId, name);
        }

        // The words
        var template = await templates.GetAsync(scope, rule.TemplateName, rule.Language, cancellationToken)
            ?? await templates.GetAsync(scope, rule.TemplateName, DefaultTemplates.Language, cancellationToken);
        if (template is null)
        {
            Gate("Template", false, $"There is no template called {rule.TemplateName}.");
            return Done(DecisionOutcome.Blocked, $"There is no template called {rule.TemplateName}.", cover: cover, lead: leadId, name: name);
        }

        var values = template.Parameters.Select(parameter => ValueOf(parameter, lead, brand, cover)).ToArray();
        var consentBasis = $"Agreed to be contacted about insurance on {consent!.At:yyyy-MM-dd} ({consent.Channel})";
        var sendResult = await messages.SendAsync(
            scope,
            new TemplateSendRequest(rule.TemplateName, rule.Language, item.Phone, values, $"rule:{rule.RuleId}", consentBasis, $"rule:{rule.RuleId}:{item.EventId}", $"rule:{rule.RuleId}"),
            cancellationToken);

        if (sendResult.Sent)
        {
            Gate("Template and delivery", true, $"Sent the approved template {rule.TemplateName}.");
            await RememberAsync(scope, rule, item, cover, sendResult.Text!, demonstration: false, now, cancellationToken);
            return Done(DecisionOutcome.Sent, $"Offered {cover.Name} by WhatsApp.", cover: cover, lead: leadId, name: name, messageId: sendResult.MessageId?.ToString("N"));
        }

        // Not live. In a demonstration workspace it is shown as it would have gone, and said plainly.
        if (demonstration && IsNotLive(sendResult.Reason) && TryRender(template, values) is { } text)
        {
            var messageId = await RecordDemoMessageAsync(scope, item.Phone, text, rule, now, cancellationToken);
            Gate("Template and delivery", true, $"Not delivered to a phone ({sendResult.Reason}). Recorded as it would have been sent.");
            await RememberAsync(scope, rule, item, cover, text, demonstration: true, now, cancellationToken);
            return Done(DecisionOutcome.DemoSent, $"Shown as sent: {cover.Name}. Not delivered to a phone.", cover: cover, lead: leadId, name: name, messageId: messageId);
        }

        Gate("Template and delivery", false, sendResult.Reason ?? "The message was not sent.");
        return Done(DecisionOutcome.Blocked, sendResult.Reason ?? "The message was not sent.", cover: cover, lead: leadId, name: name);
    }

    private static string? TryRender(MessageTemplate template, string[] values)
    {
        try
        {
            return template.Render(values);
        }
        catch (TemplateRenderException)
        {
            return null;
        }
    }

    private static bool IsNotLive(string? reason) =>
        reason is not null && (reason.Contains("not been approved", StringComparison.Ordinal)
            || reason.Contains("not connected", StringComparison.Ordinal)
            || reason.Contains("not configured", StringComparison.Ordinal)
            || reason.Contains("no template called", StringComparison.Ordinal));

    private static DateTimeOffset NextWindow(DateTimeOffset from, int startHour)
    {
        var local = from + EastAfrica;
        var day = local.Hour < startHour ? local.Date : local.Date.AddDays(1);
        return new DateTimeOffset(day.AddHours(startHour), EastAfrica).ToUniversalTime();
    }

    private async Task RememberAsync(
        TenantScope scope, OutreachRule rule, WorkspaceEvent item, MicroinsuranceCatalog.MicroProduct cover, string text, bool demonstration, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var type = BankEventCatalog.Find(item.EventType);
        await log.SaveReasonAsync(
            scope,
            new ConversationReason(item.Phone, rule.RuleId, rule.Name, item.EventType, type?.Label ?? item.EventType, cover.Name, cover.Insurer, rule.TemplateName, text, now, demonstration),
            cancellationToken);
    }

    private async Task<string> RecordDemoMessageAsync(TenantScope scope, string phone, string text, OutreachRule rule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var messageId = Guid.NewGuid();
        await messageRepository.SaveAsync(new WhatsAppMessage
        {
            MessageId = messageId,
            Scope = scope,
            ThreadId = WhatsAppMessagingService.BuildThreadId(DemoNumberId, phone),
            BusinessPhoneNumberId = DemoNumberId,
            ContactWaId = phone,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = WhatsAppContentType.Template,
            CustomerSafeBody = text,
            TemplateName = rule.TemplateName,
            Language = rule.Language,
            ProviderMessageId = $"demo.{messageId:N}",
            ApprovedByUserId = $"rule:{rule.RuleId}",
            ApprovedAt = now,
            DeliveryState = WhatsAppDeliveryState.Delivered,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = "demo-rule",
        }, cancellationToken);
        return messageId.ToString("N");
    }

    // ---- What a template's placeholders stand for

    internal static string ValueOf(string parameter, SalesLeadDetailReadModel lead, KatoBrand brand, MicroinsuranceCatalog.MicroProduct cover) => parameter switch
    {
        "customer_name" => FirstName(lead.CustomerName),
        "organisation" => brand.ShortName,
        "cover_name" or "product" => cover.Name,
        "bank_relationship" => BankRelationship(lead),
        _ => string.Empty,
    };

    private static string FirstName(string? name)
    {
        var first = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "there" : first;
    }

    /// <summary>"a salary account, a savings account and a salary loan": only the kind of product, never an amount or a balance.</summary>
    public static string BankRelationship(SalesLeadDetailReadModel lead)
    {
        var text = lead.Facts.FirstOrDefault(fact => fact.Key == "bank_products")?.Value;
        var parts = (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => part.ToLowerInvariant()).ToArray();
        if (parts.Length == 0)
        {
            return "an account with us";
        }

        var phrases = parts.Select(part => (("aeiou".Contains(part[0]) ? "an " : "a ") + part)).ToArray();
        return phrases.Length == 1 ? phrases[0] : string.Join(", ", phrases[..^1]) + " and " + phrases[^1];
    }
}

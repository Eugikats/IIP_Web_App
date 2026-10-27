using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.SalesLeads;

public interface IOutreachService
{
    /// <summary>Writes an introduction for each named prospect, or for everyone with a recommended cover.</summary>
    Task<OutreachDraftResult> DraftAsync(IReadOnlyList<string>? leadIds, CancellationToken cancellationToken = default);

    /// <summary>Finds people who have gone quiet and policies that are ending, and drafts a message for each.</summary>
    Task<FollowUpResult> DraftFollowUpsAsync(CancellationToken cancellationToken = default);

    Task<OutreachDraft?> EditAsync(string leadId, Guid draftId, string text, CancellationToken cancellationToken = default);

    Task<OutreachSendResult> SendAsync(string leadId, Guid draftId, CancellationToken cancellationToken = default);

    Task<bool> DismissAsync(string leadId, Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Sends each approved draft on its own merits: one that cannot go does not stop the rest.</summary>
    Task<IReadOnlyList<OutreachSendResult>> SendManyAsync(IReadOnlyList<OutreachRef> drafts, CancellationToken cancellationToken = default);
}

public sealed record OutreachRef(string LeadId, Guid DraftId);

public sealed record OutreachDraftResult(int Drafted, int Skipped);

public sealed record FollowUpResult(int Quiet, int Ending, int Skipped);

public sealed record OutreachSendResult(string LeadId, Guid DraftId, string Status, string StatusLabel, string? Reason);

/// <summary>What this deployment may do when a draft cannot be delivered to a real phone.</summary>
public sealed record OutreachOptions(bool AllowDemoDelivery);

/// <summary>
/// Drafts, edits and sends outreach. The drafting agent only proposes; a named person approves, and the
/// words they approve are the words that go out. A send is blocked, with the reason kept on the draft,
/// unless the person agreed to be contacted, it is within sending hours, and WhatsApp's own rules allow
/// it (the person wrote in the last 24 hours, or an approved template is used). Nothing is reported as
/// sent that was not.
/// </summary>
public sealed class OutreachService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository readRepository,
    ISalesLeadWriteRepository writeRepository,
    IAuditWriter auditWriter,
    IWhatsAppMessagingService messaging,
    IWhatsAppMessageRepository messageRepository,
    OutreachOptions options,
    IUserAccountRepository? userAccountRepository = null,
    KatoBrand? brand = null,
    ICustomerRepository? customerRepository = null,
    IPolicyRepository? policyRepository = null,
    IAgentRunRecorder? agentRunRecorder = null,
    InsuranceIntelligence.Application.Workspaces.IWorkspaceBrandResolver? brands = null) : IOutreachService
{
    public const string AgentKey = "outreach";
    public static readonly TimeSpan QuietAfter = TimeSpan.FromDays(3);
    public static readonly TimeSpan EndingWithin = TimeSpan.FromDays(30);

    private KatoBrand Brand => brand ?? KatoBrand.Current;

    public const int MaxBulk = 50;
    private const int MaxLength = 1000;
    private static readonly TimeSpan EastAfrica = TimeSpan.FromHours(3);
    private static readonly TimeSpan SendFrom = TimeSpan.FromHours(7);
    private static readonly TimeSpan SendUntil = TimeSpan.FromHours(20);

    public async Task<OutreachDraftResult> DraftAsync(IReadOnlyList<string>? leadIds, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var context = tenantContextAccessor.Current;
        using var workspaceBrand = KatoBrand.Use(brand ?? (brands is null ? KatoBrand.Current : await brands.ResolveAsync(context.Scope, cancellationToken)));
        var now = DateTimeOffset.UtcNow;
        var author = await NameOfAsync(context.UserId, cancellationToken) ?? "A colleague";
        var wanted = leadIds is { Count: > 0 } ? leadIds.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

        var drafted = 0;
        var skipped = 0;
        foreach (var lead in await readRepository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken))
        {
            if (wanted is not null && !wanted.Contains(lead.LeadId))
            {
                continue;
            }

            if (!CanBeContacted(lead) || lead.Triage is not { Fits.Count: > 0 } triage
                || lead.Outreach.Any(draft => draft.Kind == OutreachKind.Introduction && draft.Status is OutreachStatus.Draft or OutreachStatus.Edited or OutreachStatus.Sent or OutreachStatus.DemoSent))
            {
                skipped++;
                continue;
            }

            var fit = triage.Fits[0];
            var product = Brand.Products.FirstOrDefault(item => string.Equals(item.Name, fit.Product, StringComparison.OrdinalIgnoreCase));
            if (product is null)
            {
                skipped++;
                continue;
            }

            var text = OutreachDrafting.Introduction(
                Brand.Organisation, lead.CustomerName, OutreachDrafting.CustomerReason(fit.Reasons), product.Explanation, fit.Insurer);
            var draft = new OutreachDraft(Guid.NewGuid(), OutreachKind.Introduction, text, fit.Product, fit.Insurer, OutreachStatus.Draft, now, "Outreach drafting agent");
            await writeRepository.CreateAsync(
                context.TenantId, context.WorkspaceId,
                lead with
                {
                    Outreach = [.. lead.Outreach, draft],
                    Activity = [.. lead.Activity, new ProspectActivity(now, $"A message about {fit.Product} was drafted for review", "Outreach drafting agent")],
                },
                cancellationToken);
            drafted++;
        }

        await RecordRunAsync("draft", cancellationToken);
        await AuditAsync("outreach.drafted", null, $"Drafted {drafted} messages ({skipped} skipped) for review. Message text omitted.", cancellationToken);
        _ = author;
        return new OutreachDraftResult(drafted, skipped);
    }

    public async Task<FollowUpResult> DraftFollowUpsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var context = tenantContextAccessor.Current;
        using var workspaceBrand = KatoBrand.Use(brand ?? (brands is null ? KatoBrand.Current : await brands.ResolveAsync(context.Scope, cancellationToken)));
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var customers = customerRepository is null ? [] : (await customerRepository.ListAsync(context.Scope, cancellationToken)).ToArray();
        var policies = policyRepository is null ? [] : (await policyRepository.ListAsync(context.Scope, cancellationToken)).ToArray();

        var quiet = 0;
        var ending = 0;
        var skipped = 0;
        foreach (var lead in await readRepository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken))
        {
            if (lead.Consent?.Status != ContactConsentStatus.Agreed
                || string.IsNullOrWhiteSpace(lead.Phone)
                || lead.Outreach.Any(draft => draft.Status is OutreachStatus.Draft or OutreachStatus.Edited))
            {
                skipped++;
                continue;
            }

            var added = new List<OutreachDraft>();

            // A policy ending soon, matched through the customer's phone number on the insurer's own records.
            var ids = customers.Where(customer => PhoneNumbers.SameNumber(customer.Phone, lead.Phone)).Select(customer => customer.CustomerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var endingPolicy = policies
                .Where(policy => policy.CustomerId is not null && ids.Contains(policy.CustomerId) && policy.EffectiveTo is { } end
                    && end >= today && end.ToDateTime(TimeOnly.MinValue) <= now.UtcDateTime + EndingWithin)
                .OrderBy(policy => policy.EffectiveTo)
                .FirstOrDefault();
            if (endingPolicy is not null)
            {
                if (!lead.Outreach.Any(draft => draft.Kind == OutreachKind.PolicyEnding && draft.Product == endingPolicy.ProductLine && draft.Status != OutreachStatus.Dismissed))
                {
                    added.Add(new OutreachDraft(
                        Guid.NewGuid(), OutreachKind.PolicyEnding,
                        OutreachDrafting.PolicyEnding(Brand.Organisation, lead.CustomerName, endingPolicy.ProductLine, endingPolicy.EffectiveTo!.Value),
                        endingPolicy.ProductLine, null, OutreachStatus.Draft, now, "Outreach agent"));
                    ending++;
                }
            }
            else if (IsQuiet(lead, now))
            {
                var interest = lead.Interests.Where(item => item.Strength != InterestStrength.NotNow).OrderByDescending(item => item.Strength).First();
                var product = Brand.Products.FirstOrDefault(item => string.Equals(item.Name, interest.Product, StringComparison.OrdinalIgnoreCase))
                    ?? Brand.Find(interest.Product);
                var name = product?.Name ?? interest.Product;
                added.Add(new OutreachDraft(
                    Guid.NewGuid(), OutreachKind.QuietFollowUp,
                    OutreachDrafting.QuietFollowUp(Brand.Organisation, lead.CustomerName, name, product?.Insurer ?? "the insurer"),
                    name, product?.Insurer, OutreachStatus.Draft, now, "Outreach agent"));
                quiet++;
            }

            if (added.Count == 0)
            {
                skipped++;
                continue;
            }

            await writeRepository.CreateAsync(
                context.TenantId, context.WorkspaceId,
                lead with
                {
                    Outreach = [.. lead.Outreach, .. added],
                    Activity = [.. lead.Activity, new ProspectActivity(now, added[0].Kind == OutreachKind.PolicyEnding ? "A renewal message was drafted for review" : "A follow-up message was drafted for review", "Outreach agent")],
                },
                cancellationToken);
        }

        await RecordRunAsync("follow_up", cancellationToken);
        await AuditAsync("outreach.follow_ups_drafted", null, $"Drafted {quiet} quiet-client and {ending} policy-ending messages for review. Message text omitted.", cancellationToken);
        return new FollowUpResult(quiet, ending, skipped);
    }

    internal static bool IsQuiet(SalesLeadDetailReadModel lead, DateTimeOffset now)
    {
        var last = lead.LastInteractionAt ?? lead.CreatedAt;
        var interested = lead.Interests.Any(item => item.Strength is InterestStrength.Interested or InterestStrength.WantsIt)
            && lead.AppliedFor is null && lead.HandedToPersonAt is null;
        return interested && last is { } at && now - at >= QuietAfter;
    }

    private Task RecordRunAsync(string trigger, CancellationToken cancellationToken)
    {
        if (agentRunRecorder is null)
        {
            return Task.CompletedTask;
        }

        var context = tenantContextAccessor.Current;
        return agentRunRecorder.RecordAsync(
            context.Scope, AgentKey, trigger, [], AgentRunStatus.Completed, DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"), context.UserId, caseId: null, cancellationToken: cancellationToken);
    }

    public async Task<OutreachDraft?> EditAsync(string leadId, Guid draftId, string text, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var body = text?.Trim() ?? string.Empty;
        if (body.Length == 0)
        {
            throw new SalesLeadQueryValidationException("text", "Write the message first.");
        }

        if (body.Length > MaxLength)
        {
            throw new SalesLeadQueryValidationException("text", $"A message can be up to {MaxLength:N0} characters.");
        }

        var context = tenantContextAccessor.Current;
        var lead = await LoadAsync(leadId, cancellationToken);
        var draft = lead?.Outreach.FirstOrDefault(item => item.DraftId == draftId);
        if (lead is null || draft is null || draft.Status is OutreachStatus.Sent or OutreachStatus.DemoSent)
        {
            return null;
        }

        var edited = draft with { Text = body, Status = OutreachStatus.Edited, BlockedReason = null };
        await Save(lead, edited, cancellationToken);
        await AuditAsync("outreach.edited", lead.LeadId, "A drafted message was edited by staff. Message text omitted.", cancellationToken);
        _ = context;
        return edited;
    }

    public async Task<bool> DismissAsync(string leadId, Guid draftId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var lead = await LoadAsync(leadId, cancellationToken);
        var draft = lead?.Outreach.FirstOrDefault(item => item.DraftId == draftId);
        if (lead is null || draft is null || draft.Status is OutreachStatus.Sent or OutreachStatus.DemoSent)
        {
            return false;
        }

        await Save(lead, draft with { Status = OutreachStatus.Dismissed, DecidedAt = DateTimeOffset.UtcNow, DecidedBy = tenantContextAccessor.Current.UserId }, cancellationToken);
        await AuditAsync("outreach.dismissed", lead.LeadId, "A drafted message was dismissed by staff.", cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<OutreachSendResult>> SendManyAsync(IReadOnlyList<OutreachRef> drafts, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        if (drafts.Count == 0)
        {
            throw new SalesLeadQueryValidationException("drafts", "Choose at least one message to send.");
        }

        if (drafts.Count > MaxBulk)
        {
            throw new SalesLeadQueryValidationException("drafts", $"Send up to {MaxBulk} messages at a time.");
        }

        var results = new List<OutreachSendResult>();
        foreach (var item in drafts)
        {
            results.Add(await SendAsync(item.LeadId, item.DraftId, cancellationToken));
        }

        return results;
    }

    public async Task<OutreachSendResult> SendAsync(string leadId, Guid draftId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var context = tenantContextAccessor.Current;
        var lead = await LoadAsync(leadId, cancellationToken);
        var draft = lead?.Outreach.FirstOrDefault(item => item.DraftId == draftId);
        if (lead is null || draft is null)
        {
            return new OutreachSendResult(leadId, draftId, "NotFound", "Not found", "This message is no longer in this workspace.");
        }

        if (draft.Status is OutreachStatus.Sent or OutreachStatus.DemoSent)
        {
            return Result(lead, draft);
        }

        var now = DateTimeOffset.UtcNow;
        var blocked = BlockReason(lead, now);
        string? messageId = null;
        var status = OutreachStatus.Blocked;

        if (blocked is null)
        {
            var open = await OpenWindowAsync(lead, context, now, cancellationToken);
            if (open is not null && !WithinSendingHours(now))
            {
                blocked = "Outside sending hours: messages go out between 07:00 and 20:00 East Africa time.";
            }
            else if (open is { } window)
            {
                var thread = window;
                try
                {
                    var sent = await messaging.SendAsync(
                        new SendWhatsAppMessageCommand(
                            null, thread.ThreadId, thread.ContactWaId, draft.Text, "customer_conversation",
                            "The customer agreed to be contacted and wrote to us in the last 24 hours", "en", null, HumanApproved: true,
                            $"outreach:{draft.DraftId:N}", null),
                        cancellationToken);
                    if (sent.DeliveryState == WhatsAppDeliveryState.Failed)
                    {
                        blocked = sent.SafeError ?? "WhatsApp did not accept the message.";
                    }
                    else
                    {
                        status = OutreachStatus.Sent;
                        messageId = sent.MessageId.ToString("N");
                    }
                }
                catch (WhatsAppPolicyException exception)
                {
                    blocked = exception.Message;
                }
            }
            else if (options.AllowDemoDelivery && IsDemoContact(lead))
            {
                messageId = await RecordDemoMessageAsync(lead, draft, context, now, cancellationToken);
                status = OutreachStatus.DemoSent;
            }
            else
            {
                blocked = "Needs an approved WhatsApp template: this person has not written in the last 24 hours, and none has been approved yet.";
            }
        }

        var decided = draft with
        {
            Status = status,
            DecidedAt = now,
            DecidedBy = context.UserId,
            BlockedReason = status == OutreachStatus.Blocked ? blocked : null,
            SentMessageId = messageId,
        };
        await Save(lead, decided, cancellationToken);
        await AuditAsync(
            status == OutreachStatus.Blocked ? "outreach.blocked" : status == OutreachStatus.DemoSent ? "outreach.demo_sent" : "outreach.sent",
            lead.LeadId, status == OutreachStatus.Blocked ? $"A message was approved but not sent: {blocked}" : "A message was approved and sent. Message text omitted.", cancellationToken);
        return Result(lead, decided);
    }

    internal static bool CanBeContacted(SalesLeadDetailReadModel lead) =>
        lead.Consent?.Status is not (ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn);

    /// <summary>Why this person cannot be messaged right now, or null when they can.</summary>
    internal static string? BlockReason(SalesLeadDetailReadModel lead, DateTimeOffset now)
    {
        if (lead.Consent?.Status != ContactConsentStatus.Agreed)
        {
            return lead.Consent?.Status is ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn
                ? "This person asked not to be contacted."
                : "There is no recorded agreement to be contacted.";
        }

        if (string.IsNullOrWhiteSpace(lead.Phone))
        {
            return "There is no phone number on record.";
        }

        return null;
    }

    public static bool WithinSendingHoursForTests(DateTimeOffset now) => WithinSendingHours(now);

    internal static bool WithinSendingHours(DateTimeOffset now)
    {
        var local = (now + EastAfrica).TimeOfDay;
        return local >= SendFrom && local < SendUntil;
    }

    private async Task<(string ThreadId, string ContactWaId)?> OpenWindowAsync(
        SalesLeadDetailReadModel lead, TenantContext context, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lead.ThreadId))
        {
            return null;
        }

        var inbound = (await messageRepository.ListThreadAsync(context.Scope, lead.ThreadId, cancellationToken))
            .Where(message => message.Direction == WhatsAppMessageDirection.Inbound)
            .OrderByDescending(message => message.CreatedAt)
            .FirstOrDefault();
        return inbound is not null && now - inbound.CreatedAt <= TimeSpan.FromHours(24)
            ? (lead.ThreadId, inbound.ContactWaId)
            : null;
    }

    private static bool IsDemoContact(SalesLeadDetailReadModel lead) =>
        (lead.Phone ?? string.Empty).StartsWith("2567900", StringComparison.Ordinal);

    private async Task<string> RecordDemoMessageAsync(
        SalesLeadDetailReadModel lead, OutreachDraft draft, TenantContext context, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var threadId = WhatsAppMessagingService.BuildThreadId(DemoNumberId, lead.Phone!);
        var messageId = Guid.NewGuid();
        await messageRepository.SaveAsync(new WhatsAppMessage
        {
            MessageId = messageId,
            Scope = context.Scope,
            ThreadId = threadId,
            BusinessPhoneNumberId = DemoNumberId,
            ContactWaId = lead.Phone!,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = WhatsAppContentType.Text,
            CustomerSafeBody = draft.Text,
            ProviderMessageId = $"demo.{messageId:N}",
            ApprovedByUserId = context.UserId,
            ApprovedAt = now,
            DeliveryState = WhatsAppDeliveryState.Delivered,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = "demo-outreach",
        }, cancellationToken);
        return messageId.ToString("N");
    }

    public const string DemoNumberId = "demo-number";

    private OutreachSendResult Result(SalesLeadDetailReadModel lead, OutreachDraft draft) =>
        new(lead.LeadId, draft.DraftId, draft.Status.ToString(), Label(draft.Status), draft.BlockedReason);

    public static string Label(OutreachStatus status) => status switch
    {
        OutreachStatus.Draft => "Draft",
        OutreachStatus.Edited => "Edited, not sent",
        OutreachStatus.Sent => "Sent",
        OutreachStatus.Blocked => "Could not send",
        OutreachStatus.DemoSent => "Demo send, not delivered to a phone",
        _ => "Dismissed",
    };

    private async Task Save(SalesLeadDetailReadModel lead, OutreachDraft updated, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        var actor = await NameOfAsync(context.UserId, cancellationToken) ?? "A colleague";
        var activity = updated.Status switch
        {
            OutreachStatus.Sent => "Approved and sent a message",
            OutreachStatus.DemoSent => "Approved a message (demo send, not delivered to a phone)",
            OutreachStatus.Blocked => "Approved a message that could not be sent",
            OutreachStatus.Dismissed => "Dismissed a drafted message",
            OutreachStatus.Edited => "Edited a drafted message",
            _ => null,
        };
        await writeRepository.CreateAsync(
            context.TenantId, context.WorkspaceId,
            lead with
            {
                Outreach = lead.Outreach.Select(item => item.DraftId == updated.DraftId ? updated : item).ToArray(),
                Activity = activity is null ? lead.Activity : [.. lead.Activity, new ProspectActivity(DateTimeOffset.UtcNow, activity, actor)],
                LastInteractionAt = updated.Status is OutreachStatus.Sent or OutreachStatus.DemoSent ? DateTimeOffset.UtcNow : lead.LastInteractionAt,
            },
            cancellationToken);
    }

    private async Task<SalesLeadDetailReadModel?> LoadAsync(string leadId, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        return string.IsNullOrWhiteSpace(leadId)
            ? null
            : await readRepository.GetLeadDetailAsync(context.TenantId, context.WorkspaceId, leadId.Trim(), cancellationToken);
    }

    private async Task<string?> NameOfAsync(string userId, CancellationToken cancellationToken)
    {
        if (userAccountRepository is null)
        {
            return null;
        }

        return (await userAccountRepository.GetAsync(userId, tenantContextAccessor.Current.Scope, cancellationToken))?.Name;
    }

    private Task AuditAsync(string action, string? leadId, string reason, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        return auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, action,
                DateTimeOffset.UtcNow, BeforeStatus: null, AfterStatus: null, EvidenceIds: leadId is null ? [] : [leadId], ModelProvider: "Outreach",
                Confidence: null, ValidationResult: "recorded", ApprovalId: null, AuditReason: reason, CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);
    }
}

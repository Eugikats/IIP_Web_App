using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Holds the WhatsApp conversation with a customer and routes it to a real outcome: a grounded,
/// cited answer (only when the five-condition gate permits auto-send, otherwise a holding reply
/// while a person reviews it), a new provisional claim collected over a few messages, a claim
/// status, a sales lead, or a person. Blueprint §8.3.
///
/// A customer writing from a number on file is recognised: they are greeted by name, never asked
/// for a policy number they rarely remember, and asked which cover only when they hold more than
/// one. The claim is opened once they have said what happened, and the reply hands them a secure
/// link to upload documents; photos sent in the chat land on the same claim. Every WhatsApp claim
/// still waits for a claims officer to confirm it.
///
/// This is a system-actor service, not a staff-facing one: it is invoked from the unauthenticated
/// WhatsApp webhook, where there is no signed-in user and no ITenantContextAccessor/
/// IPermissionService context to resolve, Meta calls the webhook directly, verified by HMAC
/// signature, not a bearer token. Every write this service makes goes through the same
/// system-actor pattern already established by WhatsAppMessagingService.WriteIntegrationAuditAsync
/// ("system:whatsapp-webhook", ActorType.Integration) and ClaimantAuthenticationEndpoints
/// ("system:claimant-magic-link"), not through the permission-gated CaseIntakeService callers or
/// SalesLeadCommandService, both of which assume a real staff user and would throw here.
///
/// Outbound sends go through IWhatsAppOutboundProvider directly, not
/// IWhatsAppMessagingService.SendAsync, that higher-level service requires a case-linked,
/// previously-verified thread and two staff permissions, which is exactly right for an officer
/// replying to an existing case, and exactly wrong for the platform replying to a first inbound
/// message with no case yet. See docs/01-strategy-requirements-roadmap/
/// IIP_HACKATHON_READINESS_BLUEPRINT.md §14 Phase 4 for this finding in full.
/// </summary>
public interface ICustomerAssistantService
{
    /// <summary>autoReplyEnabled is the tenant/deployment opt-in for auto-sending grounded,
    /// cited GeneralQuestion answers (blueprint §8.3 gate condition 5, AssistantAutoReplyEnabled
    /// in InfrastructureOptions). Passed in by the caller rather than read from InfrastructureOptions
    /// directly: InsuranceIntelligence.Application has no reference to InsuranceIntelligence.Infrastructure
    /// (correctly, Infrastructure depends on Application, not the reverse), so this setting has to
    /// cross that boundary as a parameter, not a dependency.</summary>
    /// <returns>The claims this call opened, so the caller can start the same automatic review a
    /// claim filed by staff or on the claimant portal gets.</returns>
    Task<IReadOnlyList<Guid>> HandleInboundMessagesAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> messages,
        string correlationId,
        bool autoReplyEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>For a caller that gave up on a batch (a timeout, a shutdown): tells each customer a
    /// person will follow up and records the escalation, exactly as a failure inside the assistant
    /// does. Text messages only; a photo is already stored on the claim and needs no reply.</summary>
    Task EscalateUnhandledAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> messages,
        string correlationId,
        Exception reason,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerAssistantService(
    ICustomerIntentClassifier intentClassifier,
    IRetrievalProvider retrievalProvider,
    IAiAssistantProvider assistantProvider,
    ICaseRepository caseRepository,
    IPolicyRepository policyRepository,
    ICustomerRepository customerRepository,
    ISalesLeadWriteRepository salesLeadWriteRepository,
    IWhatsAppOutboundProvider outboundProvider,
    IWhatsAppMessageRepository whatsAppMessageRepository,
    ICustomerConversationRepository conversationRepository,
    IAgentBehaviourProfileRepository behaviourProfileRepository,
    IConsentRepository consentRepository,
    IAgentGate agentGate,
    IAuditRepository auditRepository,
    IProductRepository productRepository,
    INotificationEmitter notificationEmitter,
    // Optional: a deployment with Teams routing not configured for leads simply skips the
    // broadcast, the lead itself is still created and visible in the existing acquisition list.
    ITeamsNotificationQueue? teamsNotificationQueue = null,
    // Optional: without a model the assistant still works on keywords alone; with one, a message
    // no keyword matches ("a taxi rammed into me at the roundabout") is still understood.
    IChatCompletionProvider? chatCompletionProvider = null,
    // Optional: without a configured client portal the claim is still opened, the reply simply
    // says documents can be sent in the chat instead of carrying a link.
    IClaimantAccessLinkIssuer? accessLinkIssuer = null) : ICustomerAssistantService
{
    private const string SystemActorId = "system:customer-assistant";
    private const string AgentKey = "customer-assistant";
    private const string GroundingAgentKey = "policy-intelligence"; // the same grounded-answer capability, reused for customers

    /// <summary>WO 6.1's real referent for what every hardcoded "customer_initiated_conversation"
    /// ConsentBasis string in this codebase used to stand in for.</summary>
    private const string WhatsAppMessagingPurpose = "whatsapp_messaging";

    /// <summary>A burst of photos sent together gets one acknowledgement, not one per photo.</summary>
    private static readonly TimeSpan DocumentAcknowledgementQuietPeriod = TimeSpan.FromMinutes(2);

    // The demo-seed policy number format (NSA/{product}/{year}/{seq}) plus a looser generic
    // fallback, a real customer will not always type the format perfectly, and this must not be
    // the only path into the flow, but it is the primary one this pass implements.
    private static readonly Regex PolicyNumberPattern = new(@"[A-Z]{2,4}/[A-Z]{2}/\d{4}/\d{4,6}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<IReadOnlyList<Guid>> HandleInboundMessagesAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> messages,
        string correlationId,
        bool autoReplyEnabled,
        CancellationToken cancellationToken = default)
    {
        var opened = new List<Guid>();
        foreach (var inbound in messages)
        {
            try
            {
                if (inbound.ContentType is WhatsAppContentType.Image or WhatsAppContentType.Document)
                {
                    // The file itself is stored on the claim by WhatsAppInboundDocumentIngestionService
                    // before this runs; this only tells the customer it arrived.
                    await HandleMediaMessageAsync(scope, inbound, correlationId, cancellationToken);
                    continue;
                }

                if (inbound.ContentType != WhatsAppContentType.Text || string.IsNullOrWhiteSpace(inbound.CustomerSafeBody))
                {
                    continue;
                }

                var openedCaseId = await HandleTextMessageAsync(scope, inbound, correlationId, autoReplyEnabled, cancellationToken);
                if (openedCaseId is { } caseId)
                {
                    opened.Add(caseId);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The message is already saved, and this now runs after Meta has been answered, so
                // nobody is waiting on it. Without this a failure would leave the customer with no
                // reply and no route to a person, and would be visible only in a log. Hand it to a
                // person instead, and keep going with the rest of the batch.
                await EscalateAfterFailureAsync(scope, inbound, correlationId, exception, cancellationToken);
            }
        }

        return opened;
    }

    public async Task EscalateUnhandledAsync(
        TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, string correlationId, Exception reason, CancellationToken cancellationToken = default)
    {
        foreach (var inbound in messages.Where(m => m.ContentType == WhatsAppContentType.Text && !string.IsNullOrWhiteSpace(m.CustomerSafeBody)))
        {
            await EscalateAfterFailureAsync(scope, inbound, correlationId, reason, cancellationToken);
        }
    }

    /// <summary>The safety net for an assistant that failed on one message: tell the customer a
    /// person will follow up, record an escalated turn so the conversation view and the Inbox show
    /// it needs a human, and audit the failure. Every step is best effort and none may throw, because
    /// the failure being handled may well be a broken send or a broken database.</summary>
    private async Task EscalateAfterFailureAsync(
        TenantScope scope, WhatsAppInboundMessage inbound, string correlationId, Exception exception, CancellationToken cancellationToken)
    {
        var waId = NormalizeWaId(inbound.SenderWaId);
        var threadId = WhatsAppMessagingService.BuildThreadId(inbound.BusinessPhoneNumberId, waId);
        string? reply = DefaultEscalationMessage();

        // The customer wrote to us first, which is the basis for answering them. That is normally
        // recorded at the start of handling, but a failure or a timeout can arrive before it was, and
        // an escalation nobody is permitted to send would leave the customer in silence.
        try { await RecordConsentAsync(scope, waId, threadId, correlationId, cancellationToken); }
        catch { }

        try { await SendReplyAsync(scope, inbound, threadId, waId, reply, caseId: null, cancellationToken); }
        catch { reply = null; } // Nothing was sent, so the turn must not claim it was.

        try
        {
            await conversationRepository.SaveAsync(new CustomerConversationTurn
            {
                TurnId = Guid.NewGuid(),
                Scope = scope,
                ThreadId = threadId,
                ContactWaId = waId,
                InboundMessageText = inbound.CustomerSafeBody,
                ClassifiedIntent = CustomerIntent.Unknown,
                OutboundReplyText = reply,
                Outcome = CustomerAssistantOutcome.Escalated,
                CaseId = null,
                LeadId = null,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
            }, cancellationToken);
        }
        catch { } // Best effort: the failure being handled may be the store itself.

        try
        {
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, null, null, SystemActorId, ActorType.Integration,
                    "customer_assistant.processing_failed", DateTimeOffset.UtcNow,
                    null, null, [], "CustomerAssistant", null,
                    "failed", null,
                    $"WhatsApp customer assistant failed on a message ({exception.GetType().Name}) and escalated it to a person. Message body omitted from evidence.",
                    correlationId),
                cancellationToken);
        }
        catch { } // Best effort: the failure being handled may be the store itself.
    }

    private async Task<Guid?> HandleTextMessageAsync(TenantScope scope, WhatsAppInboundMessage inbound, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken)
    {
        var text = inbound.CustomerSafeBody.Trim();
        var waId = NormalizeWaId(inbound.SenderWaId);
        var threadId = WhatsAppMessagingService.BuildThreadId(inbound.BusinessPhoneNumberId, waId);

        // Fail-closed kill switch (Admin > Agents): this is the one agent in the catalog that can
        // act on an unauthenticated inbound message with no staff user in the loop at all, so a
        // tenant disabling it must actually stop it, not just record a database row nothing reads.
        // No ITenantContextAccessor exists here (no signed-in user on the WhatsApp webhook path),
        // this is exactly what IAgentGate's scope-explicit overload exists for.
        var gate = await agentGate.CheckAsync(scope, AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            await HandleDisabledAsync(scope, inbound, threadId, waId, text, gate, correlationId, cancellationToken);
            return null;
        }

        // WO 6.1 capture point: the customer messaging first is the real basis every hardcoded
        // "customer_initiated_conversation" string in this codebase was standing in for, record
        // it as a real, queryable, auditable grant instead of an ambient assumption. Every inbound
        // message refreshes it (append-only, so the full history survives), which is also what
        // keeps SendReplyAsync's enforcement check below permitted for an active conversation.
        await RecordConsentAsync(scope, waId, threadId, correlationId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var sender = await IdentifySenderAsync(scope, waId, cancellationToken);
        var thread = (await conversationRepository.ListForThreadAsync(scope, threadId, cancellationToken))
            .OrderBy(turn => turn.OccurredAt)
            .ToArray();
        var previousTurn = thread.Length == 0 ? null : thread[^1];

        var classified = intentClassifier.Classify(text);
        if (CustomerMessageSignals.IsBareHumanRequest(text))
        {
            classified = CustomerIntent.HumanRequested;
        }

        var continuation = ConversationContinuation.Resolve(classified, ExtractPolicyNumber(text) is not null, thread, now);
        var answeringOurQuestion = continuation.EarlierMessageText is not null;

        var intent = continuation.Intent;
        if (intent == CustomerIntent.Unknown && !answeringOurQuestion && ShouldAskModelForIntent(text))
        {
            intent = await ClassifyWithModelAsync(scope, text, correlationId, cancellationToken);
        }

        var outcome = await DecideAsync(
            scope, inbound, text, waId, intent, continuation, answeringOurQuestion, previousTurn, sender, correlationId, autoReplyEnabled, now, cancellationToken);

        // GeneralQuestion replies are model prose and arrive with markdown in them, the staff
        // portal renders that, WhatsApp does not (its bold is *single* asterisks, so a model's
        // ** reaches the customer as literal characters). Flatten before the reply is either sent
        // or recorded, so the stored CustomerConversationTurn is exactly what the customer got.
        // A no-op for the fixed replies, which contain no markdown to begin with.
        var reply = outcome.Reply is null ? null : PlainTextForCustomerChannel.Convert(outcome.Reply);

        // QueuedForHumanApproval means the five-condition gate (CustomerAutoReplyGate) declined to
        // auto-send an AI-composed answer, it must be logged for a human to review, never sent to
        // the customer on our behalf. The customer still gets a fixed holding acknowledgement, so
        // the conversation never goes silent; that message discloses nothing and is the same
        // lower-risk template category as every other non-AI reply here (a claim reference,
        // "which cover is this about?", a lead confirmation), which the gate does not govern.
        if (reply is not null)
        {
            var textToSend = outcome.Outcome == CustomerAssistantOutcome.QueuedForHumanApproval ? HoldingReply(sender) : reply;
            await SendReplyAsync(scope, inbound, threadId, waId, textToSend, outcome.CaseId, cancellationToken);
        }

        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = text,
            ClassifiedIntent = outcome.Intent,
            OutboundReplyText = reply,
            Outcome = outcome.Outcome,
            CitationTitles = outcome.Citations,
            CaseId = outcome.CaseId,
            LeadId = outcome.LeadId,
            PolicyNumber = outcome.PolicyNumber,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, outcome.CaseId, null, SystemActorId, ActorType.Integration,
                $"customer_assistant.{outcome.Outcome.ToString().ToLowerInvariant()}", DateTimeOffset.UtcNow,
                null, null, outcome.LeadId is null ? [] : [outcome.LeadId], "CustomerAssistant", null,
                "recorded", null,
                $"WhatsApp customer assistant classified this message as {outcome.Intent} and took action {outcome.Outcome}{(sender is null ? "" : ", sender recognised by phone number")}. Message body omitted from evidence.",
                correlationId),
            cancellationToken);

        return outcome.OpenedCase ? outcome.CaseId : null;
    }

    private async Task<AssistantOutcome> DecideAsync(
        TenantScope scope,
        WhatsAppInboundMessage inbound,
        string text,
        string waId,
        CustomerIntent intent,
        ContinuationResult continuation,
        bool answeringOurQuestion,
        CustomerConversationTurn? previousTurn,
        SenderProfile? sender,
        string correlationId,
        bool autoReplyEnabled,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Honest about what it is: the conversation should feel natural, but a customer who asks
        // is told they are talking to the automated assistant, and how to reach a person.
        if (CustomerMessageSignals.IsAskingIfAutomated(text))
        {
            return AssistantOutcome.Of(
                $"I'm the automated assistant for our claims team{(sender is null ? "" : $", {sender.FirstName}")}, so I can help any time of day. If you'd rather talk to a person, just say so and I'll bring someone in.",
                CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
        }

        if (!answeringOurQuestion && intent is CustomerIntent.Unknown or CustomerIntent.GeneralQuestion)
        {
            if (CustomerMessageSignals.IsThanksOrClose(text))
            {
                return AssistantOutcome.Of(
                    sender is null
                        ? "You're welcome. I'm here whenever you need anything."
                        : $"You're welcome, {sender.FirstName}. I'm here whenever you need anything.",
                    CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
            }

            if (CustomerMessageSignals.IsAccessLinkRequest(text))
            {
                return await HandleAccessLinkRequestAsync(scope, waId, sender, previousTurn, correlationId, cancellationToken);
            }
        }

        return intent switch
        {
            CustomerIntent.NewClaim => await HandleNewClaimAsync(scope, text, waId, continuation, previousTurn, sender, correlationId, now, cancellationToken),
            CustomerIntent.ExistingClaimStatus => await HandleExistingClaimStatusAsync(scope, text, waId, sender, cancellationToken),
            CustomerIntent.GeneralQuestion => await HandleGeneralQuestionAsync(scope, text, correlationId, autoReplyEnabled, cancellationToken),
            CustomerIntent.ProductInterest => await HandleProductInterestAsync(scope, text, waId, sender, correlationId, cancellationToken),
            CustomerIntent.HumanRequested => await HandleHumanRequestedAsync(scope, text, waId, sender, correlationId, cancellationToken),
            _ when ConversationContinuation.IsGreeting(text) =>
                AssistantOutcome.Of(Welcome(sender), CustomerIntent.Unknown, CustomerAssistantOutcome.AwaitingMoreInformation),
            _ => ClarifyOrEscalate(previousTurn, sender, now),
        };
    }

    /// <summary>An unclear message gets one clarifying question, never an immediate hand-off. Only
    /// a second unclear message in a row brings in a person, the same way a good receptionist
    /// asks once before transferring the call.</summary>
    private static AssistantOutcome ClarifyOrEscalate(CustomerConversationTurn? previousTurn, SenderProfile? sender, DateTimeOffset now)
    {
        var alreadyAsked = previousTurn is { Outcome: CustomerAssistantOutcome.Clarifying }
            && now - previousTurn.OccurredAt <= ConversationContinuation.Window;
        if (alreadyAsked)
        {
            return AssistantOutcome.Of(
                $"Let me bring in someone from our team so you get the right help{(sender is null ? "" : $", {sender.FirstName}")}. They'll reply to you here shortly.",
                CustomerIntent.Unknown, CustomerAssistantOutcome.Escalated);
        }

        return AssistantOutcome.Of(
            "Sorry, I didn't quite catch that. Are you reporting something that has happened, checking on a claim, or asking about your cover? You can also say \"agent\" to talk to someone from our team.",
            CustomerIntent.Unknown, CustomerAssistantOutcome.Clarifying);
    }

    private async Task<AssistantOutcome> HandleNewClaimAsync(
        TenantScope scope,
        string text,
        string waId,
        ContinuationResult continuation,
        CustomerConversationTurn? previousTurn,
        SenderProfile? sender,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var earlier = continuation.EarlierMessageText;
        var everything = string.IsNullOrWhiteSpace(earlier) ? text : $"{earlier}\n{text}";

        // The current message wins: after "I couldn't find that policy" the customer retypes it,
        // and the earlier, wrong number must not be picked up again.
        var typedPolicyNumber = ExtractPolicyNumber(text) ?? (earlier is null ? null : ExtractPolicyNumber(earlier));
        Policy? policy = null;
        if (typedPolicyNumber is not null)
        {
            policy = await policyRepository.GetByPolicyNumberAsync(typedPolicyNumber, scope, cancellationToken);
            if (policy is null)
            {
                return AssistantOutcome.Of(
                    $"I couldn't find a policy matching {typedPolicyNumber}. Please double-check the number, or say \"agent\" and someone from our team will help.",
                    CustomerIntent.NewClaim, CustomerAssistantOutcome.Declined);
            }
        }
        else if (continuation.PolicyNumber is not null)
        {
            policy = await policyRepository.GetByPolicyNumberAsync(continuation.PolicyNumber, scope, cancellationToken);
        }
        else if (sender is { Policies.Count: > 0 })
        {
            var choosing = previousTurn?.Outcome == CustomerAssistantOutcome.AwaitingPolicyChoice;
            policy = ChoosePolicy(sender.Policies, choosing ? text : everything, choosing);
            if (policy is null)
            {
                return AssistantOutcome.Of(
                    $"I'm sorry to hear that, {sender.FirstName}. Which cover is this about?\n{PolicyChoiceList(sender.Policies)}\nReply with the number or the name.",
                    CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingPolicyChoice);
            }
        }

        if (policy is null)
        {
            return AssistantOutcome.Of(
                "I'm sorry to hear that. To start your claim, please reply with your policy number. It's on your policy schedule or certificate.",
                CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingMoreInformation);
        }

        var description = IncidentDescription(everything);
        if (!CustomerMessageSignals.HasEnoughIncidentDetail(description))
        {
            var name = sender is null ? "" : $", {sender.FirstName}";
            return AssistantOutcome.Of(
                $"I'm sorry to hear that{name}, I hope everyone is safe. Tell me in your own words what happened, when it happened and where, and I'll open the claim on your {policy.ProductLine} policy ({policy.PolicyNumber}).",
                CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingIncidentDetails, policyNumber: policy.PolicyNumber);
        }

        var customer = string.IsNullOrWhiteSpace(policy.CustomerId) ? null : await customerRepository.GetAsync(policy.CustomerId, scope, cancellationToken);
        var caseIntake = new CaseIntakeService(caseRepository, auditRepository, productRepository);
        var claimCase = await caseIntake.CreateCaseAsync(new CreateCaseCommand(
            scope,
            CustomerName: customer?.FullName ?? sender?.FullName ?? "WhatsApp customer",
            CustomerEmail: customer?.Email,
            CustomerPhone: customer?.Phone ?? $"+{waId}",
            PolicyNumber: policy.PolicyNumber,
            ProductLine: policy.ProductLine,
            ClaimType: "Unclassified, reported via WhatsApp",
            Channel: "WhatsApp",
            IncidentDate: CustomerMessageSignals.IncidentDate(description, DateOnly.FromDateTime(now.UtcDateTime)),
            IncidentDescription: description,
            CreatedByUserId: SystemActorId,
            CorrelationId: correlationId,
            RequiresConfirmation: true), cancellationToken);

        if (!string.IsNullOrWhiteSpace(claimCase.AssignedUserId))
        {
            try
            {
                await notificationEmitter.NotifyAsync(
                    claimCase.Scope,
                    claimCase.AssignedUserId,
                    NotificationType.ClaimAssigned,
                    $"New WhatsApp claim assigned to you on the {claimCase.AssignedTeam} team, awaiting confirmation.",
                    claimCase.CaseId,
                    cancellationToken);
            }
            catch
            {
                // Best effort, the claim itself has already been created; a notification failure
                // must never surface to the customer or block their WhatsApp reply.
            }
        }

        var reference = ClaimReference.For(claimCase.CaseId);
        var documents = await DocumentsToRequestAsync(claimCase, cancellationToken);
        var link = await IssueAccessLinkAsync(scope, claimCase, waId, correlationId, cancellationToken);

        var greeting = sender is null ? "Thank you." : $"Thank you, {sender.FirstName}.";
        var message =
            $"{greeting} I've opened your claim on your {policy.ProductLine} policy. Your reference is {reference}. A claims officer will review it and keep you updated here.\n\n" +
            $"To help us move quickly, please send {ClaimDocumentChecklist.AsSentenceList(documents)}. " +
            (link is null
                ? "You can send photos and documents right here in this chat."
                : $"You can send photos right here in this chat, or upload them securely at this link, which works for 24 hours:\n{link}");

        return AssistantOutcome.Of(message, CustomerIntent.NewClaim, CustomerAssistantOutcome.CaseOpened,
            caseId: claimCase.CaseId, policyNumber: policy.PolicyNumber, openedCase: true);
    }

    private async Task<AssistantOutcome> HandleExistingClaimStatusAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, CancellationToken cancellationToken)
    {
        var policyNumber = ExtractPolicyNumber(text);
        var cases = await caseRepository.ListAsync(scope, cancellationToken);

        // A recognised sender is identified by the number WhatsApp itself verified, so they are
        // shown only the claims tied to that number or to their own policies, without being asked
        // for a policy number first.
        if (policyNumber is null && sender is not null)
        {
            var theirs = CasesFor(cases, sender, waId).OrderByDescending(c => c.CreatedAt).ToArray();
            var open = theirs.Where(c => ClaimStatusLabels.IsOpen(c.Status)).Take(3).ToArray();
            if (open.Length > 0)
            {
                var lines = string.Join("\n", open.Select(c =>
                    $"- {ClaimReference.For(c.CaseId)}, {c.ProductLine}, reported {c.CreatedAt:d MMM}: {ClaimStatusLabels.For(c.Status)}"));
                return AssistantOutcome.Of(
                    $"Hi {sender.FirstName}, here's where things stand:\n{lines}\n\nA claims officer will contact you here if they need anything. Reply \"link\" if you'd like to view your claim or add documents.",
                    CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied, caseId: open[0].CaseId);
            }

            if (theirs.Length > 0)
            {
                var latest = theirs[0];
                return AssistantOutcome.Of(
                    $"Hi {sender.FirstName}, you don't have any open claims right now. Your most recent claim, {ClaimReference.For(latest.CaseId)} ({latest.ProductLine}), is {ClaimStatusLabels.For(latest.Status).ToLowerInvariant()}. If something new has happened, tell me and I'll start a claim.",
                    CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied, caseId: latest.CaseId);
            }

            return AssistantOutcome.Of(
                $"Hi {sender.FirstName}, I can't see any claims on your policies yet. If something has happened, tell me what and I'll start one for you.",
                CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied);
        }

        if (policyNumber is null)
        {
            return AssistantOutcome.Of(
                "To check your claim status, please reply with your policy number so I can verify it's you.",
                CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AwaitingMoreInformation);
        }

        // For a sender not on file, the policy-number match IS the identity verification
        // (blueprint §8.3 WO 4.4), a bare WhatsApp number is never, on its own, enough to disclose
        // anything about a case.
        var policy = await policyRepository.GetByPolicyNumberAsync(policyNumber, scope, cancellationToken);
        if (policy is null)
        {
            return AssistantOutcome.Of(
                $"I couldn't verify a policy matching {policyNumber}. Please double-check the number, or say \"agent\" and someone from our team will help.",
                CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.Declined);
        }

        var matchingCase = cases
            .Where(c => string.Equals(c.PolicyNumber, policy.PolicyNumber, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        if (matchingCase is null)
        {
            return AssistantOutcome.Of(
                $"I verified policy {policyNumber}, but there's no claim on file for it yet. If something has happened, tell me what and I'll start one.",
                CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.Declined, policyNumber: policy.PolicyNumber);
        }

        return AssistantOutcome.Of(
            $"Your claim {ClaimReference.For(matchingCase.CaseId)} on policy {policyNumber} is currently: {ClaimStatusLabels.For(matchingCase.Status)}. A claims officer will contact you here if they need anything.",
            CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied, caseId: matchingCase.CaseId, policyNumber: policy.PolicyNumber);
    }

    private async Task<AssistantOutcome> HandleAccessLinkRequestAsync(
        TenantScope scope, string waId, SenderProfile? sender, CustomerConversationTurn? previousTurn, string correlationId, CancellationToken cancellationToken)
    {
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var claimCase = sender is null
            ? null
            : CasesFor(cases, sender, waId).Where(c => ClaimStatusLabels.IsOpen(c.Status)).OrderByDescending(c => c.CreatedAt).FirstOrDefault();

        // Someone not on file can still get the link for the claim this same conversation opened.
        if (claimCase is null && previousTurn?.CaseId is { } threadCaseId)
        {
            claimCase = cases.FirstOrDefault(c => c.CaseId == threadCaseId && ClaimStatusLabels.IsOpen(c.Status));
        }

        if (claimCase is null)
        {
            return AssistantOutcome.Of(
                sender is null
                    ? "I couldn't find an open claim linked to this number. Please reply with your policy number or claim reference, or tell me what happened and I'll start a claim."
                    : $"You don't have an open claim with us right now, {sender.FirstName}. If something has happened, tell me what and I'll start one.",
                CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
        }

        var link = await IssueAccessLinkAsync(scope, claimCase, waId, correlationId, cancellationToken);
        var reference = ClaimReference.For(claimCase.CaseId);
        return AssistantOutcome.Of(
            link is null
                ? $"You can send photos and documents for claim {reference} right here in this chat, and I'll add them to it."
                : $"Here's your secure link for claim {reference}. It works for 24 hours:\n{link}\n\nYou can also send photos right here in this chat.",
            CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied, caseId: claimCase.CaseId);
    }

    private async Task HandleMediaMessageAsync(TenantScope scope, WhatsAppInboundMessage inbound, string correlationId, CancellationToken cancellationToken)
    {
        var waId = NormalizeWaId(inbound.SenderWaId);
        var threadId = WhatsAppMessagingService.BuildThreadId(inbound.BusinessPhoneNumberId, waId);

        var gate = await agentGate.CheckAsync(scope, AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return; // The file is still stored for a person to handle; the assistant stays silent.
        }

        await RecordConsentAsync(scope, waId, threadId, correlationId, cancellationToken);

        var sender = await IdentifySenderAsync(scope, waId, cancellationToken);
        var thread = (await conversationRepository.ListForThreadAsync(scope, threadId, cancellationToken))
            .OrderBy(turn => turn.OccurredAt)
            .ToArray();
        var previousTurn = thread.Length == 0 ? null : thread[^1];

        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var claimCase = sender is null
            ? null
            : CasesFor(cases, sender, waId).Where(c => ClaimStatusLabels.IsOpen(c.Status)).OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        if (claimCase is null && thread.LastOrDefault(turn => turn.CaseId is not null)?.CaseId is { } threadCaseId)
        {
            claimCase = cases.FirstOrDefault(c => c.CaseId == threadCaseId && ClaimStatusLabels.IsOpen(c.Status));
        }

        var burst = previousTurn is { Outcome: CustomerAssistantOutcome.DocumentReceived }
            && DateTimeOffset.UtcNow - previousTurn.OccurredAt <= DocumentAcknowledgementQuietPeriod;

        string? reply = null;
        if (!burst)
        {
            var thanks = sender is null ? "Thank you." : $"Thank you, {sender.FirstName}.";
            reply = claimCase is null
                ? $"{thanks} I've received your file. To add it to the right claim, please reply with your policy number or claim reference."
                : $"{thanks} I've added that to your claim {ClaimReference.For(claimCase.CaseId)}. Send anything else you have whenever you're ready.";
            await SendReplyAsync(scope, inbound, threadId, waId, reply, claimCase?.CaseId, cancellationToken);
        }

        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = inbound.CustomerSafeBody,
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = reply,
            Outcome = CustomerAssistantOutcome.DocumentReceived,
            CaseId = claimCase?.CaseId,
            PolicyNumber = claimCase?.PolicyNumber,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);
    }

    /// <summary>The kill-switch's actual effect: no classification, no grounded answer, no case or
    /// lead created, every inbound message is escalated to a human while the agent is disabled.
    /// The customer sees the same generic escalation message any other Unknown-intent message
    /// gets; the internal reason (who disabled it, when, why) goes to the audit trail only, an
    /// admin's kill-switch note is not something to hand back to a WhatsApp customer.</summary>
    private async Task HandleDisabledAsync(
        TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string text,
        AgentGateResult gate, string correlationId, CancellationToken cancellationToken)
    {
        var reply = DefaultEscalationMessage();
        await SendReplyAsync(scope, inbound, threadId, waId, reply, caseId: null, cancellationToken);

        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = text,
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = reply,
            Outcome = CustomerAssistantOutcome.Escalated,
            CaseId = null,
            LeadId = null,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, SystemActorId, ActorType.Integration,
                "customer_assistant.agent_disabled", DateTimeOffset.UtcNow,
                null, null, [], "CustomerAssistant", null,
                "refused", null,
                $"WhatsApp customer assistant is disabled for this workspace{(gate.DisabledByUserId is null ? "" : $" (by {gate.DisabledByUserId} on {gate.DisabledAt:yyyy-MM-dd}{(string.IsNullOrWhiteSpace(gate.Reason) ? "" : $": {gate.Reason}")})")}, message escalated without classification. Message body omitted from evidence.",
                correlationId),
            cancellationToken);
    }

    private async Task<AssistantOutcome> HandleGeneralQuestionAsync(
        TenantScope scope, string text, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken)
    {
        var question = new AssistantQuestion(scope, null, text, null, "Customer", SystemActorId, correlationId);
        var retrieval = await retrievalProvider.RetrieveAsync(question, cancellationToken);

        if (retrieval.Chunks.Count == 0)
        {
            // Nothing approved to ground an answer in, never guess from model memory for a customer.
            var fallback = "I don't have an approved answer for that yet, I've passed this to our team and someone will follow up with you.";
            return AssistantOutcome.Of(fallback, CustomerIntent.GeneralQuestion, CustomerAssistantOutcome.QueuedForHumanApproval);
        }

        var answer = await assistantProvider.AnswerPolicyQuestionAsync(question, retrieval, cancellationToken);
        var behaviourProfile = await behaviourProfileRepository.GetCurrentAsync(scope, GroundingAgentKey, cancellationToken);
        var citationTitles = answer.Sources.Select(s => s.DocumentName).ToArray();

        var permitted = CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion,
            hasApprovedCitation: citationTitles.Length > 0,
            carriesCustomerSpecificAssertion: answer.EscalationRequired,
            replyLength: answer.AnswerText.Length,
            maxResponseCharacters: behaviourProfile?.MaxResponseCharacters,
            tenantOptedIntoAutoReply: autoReplyEnabled);

        return AssistantOutcome.Of(
            answer.AnswerText,
            CustomerIntent.GeneralQuestion,
            permitted ? CustomerAssistantOutcome.AutoReplied : CustomerAssistantOutcome.QueuedForHumanApproval,
            citations: citationTitles);
    }

    private async Task<AssistantOutcome> HandleProductInterestAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, string correlationId, CancellationToken cancellationToken)
    {
        var leadId = await CreateLeadAsync(scope, text, waId, sender, "WhatsApp", correlationId, cancellationToken);
        return AssistantOutcome.Of(
            $"Thanks for your interest{(sender is null ? "" : $", {sender.FirstName}")}. I've passed your details to our sales team, and someone will reach out with options that fit what you need.",
            CustomerIntent.ProductInterest, CustomerAssistantOutcome.LeadCreated, leadId: leadId);
    }

    private async Task<AssistantOutcome> HandleHumanRequestedAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, string correlationId, CancellationToken cancellationToken)
    {
        var leadId = await CreateLeadAsync(scope, text, waId, sender, "WhatsApp - human requested", correlationId, cancellationToken);
        return AssistantOutcome.Of(
            $"Of course{(sender is null ? "" : $", {sender.FirstName}")}. I've asked a member of our team to pick this up, and they'll reply to you here shortly.",
            CustomerIntent.HumanRequested, CustomerAssistantOutcome.Escalated, leadId: leadId);
    }

    private async Task<string> CreateLeadAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, string leadSource, string correlationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var leadId = $"LEAD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var lead = new SalesLeadDetailReadModel(
            LeadId: leadId,
            LeadSource: leadSource,
            CustomerNeed: text,
            ConversationSummary: $"Captured automatically from an inbound WhatsApp message on {now:yyyy-MM-dd}.",
            ProductFit: "To be assessed",
            QuoteStatus: "New",
            RequiredInformation: ["Confirm contact details", "Confirm insurable interest", "Capture risk details for quote"],
            FollowUpActions: ["Contact customer to confirm details", "Prepare indicative quote for human review"],
            AssignedSalesOfficer: null,
            ConversionProbability: null,
            CustomerName: sender?.FullName ?? "WhatsApp customer",
            ContactInfo: $"+{waId}",
            CreatedAt: now,
            CreatedBy: SystemActorId);

        await salesLeadWriteRepository.CreateAsync(scope.TenantId, scope.WorkspaceId, lead, cancellationToken);

        if (teamsNotificationQueue is not null)
        {
            try
            {
                await teamsNotificationQueue.EnqueueAsync(new TeamsNotification
                {
                    Scope = scope,
                    CaseId = null,
                    NotificationType = TeamsNotificationType.LeadCreated,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { leadId, leadSource, customerNeed = text, correlationId }),
                }, cancellationToken);
            }
            catch
            {
                // Best effort: the lead itself is already saved and visible in the acquisition
                // list, a routing-notification failure must never be reported back to the customer.
            }
        }

        return leadId;
    }

    /// <summary>
    /// Recognises a customer by the number WhatsApp delivered their message from. WhatsApp itself
    /// verified that number when the customer registered it, which makes a match against the
    /// number on file a stronger identity signal than a policy number anyone holding the paperwork
    /// could type.
    ///
    /// When one number is on file for two different people (a shared family phone, a recycled SIM),
    /// nobody is recognised and the assistant falls back to asking for a policy number: it never
    /// guesses which person is writing. Several records of the same person (the same name
    /// imported twice) count as that one person.
    /// </summary>
    private async Task<SenderProfile?> IdentifySenderAsync(TenantScope scope, string waId, CancellationToken cancellationToken)
    {
        var customers = (await customerRepository.ListAsync(scope, cancellationToken))
            .Where(customer => PhoneNumbers.SameNumber(customer.Phone, waId))
            .ToArray();
        if (customers.Length == 0)
        {
            return null;
        }

        var distinctPeople = customers
            .Select(customer => string.Join(' ', customer.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctPeople > 1)
        {
            return null;
        }

        var policies = new List<Policy>();
        foreach (var customer in customers)
        {
            policies.AddRange(await policyRepository.ListByCustomerAsync(customer.CustomerId, scope, cancellationToken));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inForce = policies.Where(policy => policy.EffectiveTo is null || policy.EffectiveTo >= today).ToArray();
        var usable = (inForce.Length > 0 ? inForce : policies.ToArray())
            .DistinctBy(policy => policy.PolicyNumber, StringComparer.OrdinalIgnoreCase)
            .OrderBy(policy => policy.ProductLine, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fullName = customers[0].FullName.Trim();
        var firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
        return new SenderProfile(fullName, firstName, usable);
    }

    private static IEnumerable<ClaimCase> CasesFor(IEnumerable<ClaimCase> cases, SenderProfile sender, string waId) =>
        cases.Where(claimCase =>
            sender.Policies.Any(policy => string.Equals(policy.PolicyNumber, claimCase.PolicyNumber, StringComparison.OrdinalIgnoreCase))
            || PhoneNumbers.SameNumber(claimCase.CustomerPhone, waId));

    /// <summary>The one policy a claim message points at: the only one they hold, the one they
    /// picked from the numbered list, or the one whose product line the message names ("my car",
    /// "the hospital"). Null when it is genuinely unclear, so the customer is asked.</summary>
    private static Policy? ChoosePolicy(IReadOnlyList<Policy> policies, string text, bool answeringChoice)
    {
        if (policies.Count == 1)
        {
            return policies[0];
        }

        if (answeringChoice && CustomerMessageSignals.ChoiceNumber(text) is { } choice && choice <= policies.Count)
        {
            return policies[choice - 1];
        }

        var hint = CustomerMessageSignals.ProductLineHint(text);
        if (hint is null)
        {
            return null;
        }

        var matching = policies.Where(policy => string.Equals(policy.ProductLine, hint, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matching.Length == 1 ? matching[0] : null;
    }

    private static string PolicyChoiceList(IReadOnlyList<Policy> policies) =>
        string.Join("\n", policies.Select((policy, index) => $"{index + 1}. {policy.ProductLine} ({policy.PolicyNumber})"));

    /// <summary>The customer's own words for the claim, without the replies that only answered
    /// "which cover?" ("2", "motor") or carried a policy number on its own.</summary>
    private static string IncidentDescription(string everything)
    {
        var lines = everything
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => CustomerMessageSignals.ChoiceNumber(line) is null)
            .Where(line => !PolicyNumberPattern.IsMatch(line) || PolicyNumberPattern.Replace(line, string.Empty).Trim().Length > 0)
            .Where(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1 || CustomerMessageSignals.ProductLineHint(line) is null)
            .ToArray();
        return string.Join(" ", lines).Trim();
    }

    private async Task<IReadOnlyList<string>> DocumentsToRequestAsync(ClaimCase claimCase, CancellationToken cancellationToken)
    {
        if (claimCase.ProductId is { } productId)
        {
            var product = claimCase.ProductVersion is { } version
                ? await productRepository.GetVersionAsync(claimCase.Scope, productId, version, cancellationToken)
                : await productRepository.GetCurrentAsync(claimCase.Scope, productId, cancellationToken);
            var configured = product?.DocumentMatrix
                .Where(requirement => requirement.IsRequired && !string.IsNullOrWhiteSpace(requirement.CustomerLabel))
                .Select(requirement => requirement.CustomerLabel.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();
            if (configured is { Length: > 0 })
            {
                return configured;
            }
        }

        return ClaimDocumentChecklist.DefaultFor(claimCase.ProductLine);
    }

    private async Task<string?> IssueAccessLinkAsync(TenantScope scope, ClaimCase claimCase, string waId, string correlationId, CancellationToken cancellationToken)
    {
        if (accessLinkIssuer is null)
        {
            return null;
        }

        try
        {
            return await accessLinkIssuer.IssueForClaimAsync(scope, claimCase.CaseId, claimCase.PolicyNumber, $"+{waId}", correlationId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The claim exists either way; without a link the reply says to send documents in the chat.
            return null;
        }
    }

    private static bool ShouldAskModelForIntent(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3
        && !ConversationContinuation.IsGreeting(text)
        && !CustomerMessageSignals.IsThanksOrClose(text)
        && !CustomerMessageSignals.IsAskingIfAutomated(text)
        && !CustomerMessageSignals.IsAccessLinkRequest(text);

    /// <summary>
    /// One model reading of a message no keyword matched, so "a taxi rammed into me at the
    /// roundabout" starts a claim instead of confusing the assistant. The model only picks a label;
    /// what happens next (asking for details, opening a provisional claim a claims officer
    /// confirms) stays the deterministic flow above. Any failure, timeout or unexpected answer is
    /// Unknown, which asks the customer rather than guessing.
    /// </summary>
    private async Task<CustomerIntent> ClassifyWithModelAsync(TenantScope scope, string text, string correlationId, CancellationToken cancellationToken)
    {
        if (chatCompletionProvider is null)
        {
            return CustomerIntent.Unknown;
        }

        var prompt =
            "Read one WhatsApp message a customer sent to an insurance company and label what they want. " +
            "Reply with exactly one label and nothing else.\n" +
            "NEW_CLAIM: they are reporting a loss, accident, damage, theft, illness, injury or death, or want to start a claim.\n" +
            "CLAIM_STATUS: they are asking about a claim they already made.\n" +
            "POLICY_QUESTION: they are asking about their cover, benefits, premiums, documents or how something works.\n" +
            "BUY_COVER: they want new insurance or a quote.\n" +
            "PERSON: they want to talk to a person.\n" +
            "OTHER: anything else.\n\n" +
            $"Message: <<<{text}>>>";

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(prompt, scope.TenantId, scope.WorkspaceId, correlationId, AgentKey), timeout.Token);
            return ParseModelIntent(result.Content);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return CustomerIntent.Unknown;
        }
    }

    private static CustomerIntent ParseModelIntent(string? content)
    {
        var upper = (content ?? string.Empty).ToUpperInvariant();
        (string Label, CustomerIntent Intent)[] labels =
        [
            ("NEW_CLAIM", CustomerIntent.NewClaim),
            ("CLAIM_STATUS", CustomerIntent.ExistingClaimStatus),
            ("POLICY_QUESTION", CustomerIntent.GeneralQuestion),
            ("BUY_COVER", CustomerIntent.ProductInterest),
            ("PERSON", CustomerIntent.HumanRequested),
        ];

        var found = labels.Where(label => upper.Contains(label.Label, StringComparison.Ordinal)).ToArray();
        return found.Length == 1 ? found[0].Intent : CustomerIntent.Unknown;
    }

    private async Task RecordConsentAsync(TenantScope scope, string waId, string threadId, string correlationId, CancellationToken cancellationToken) =>
        await consentRepository.SaveAsync(new ConsentRecord
        {
            RecordId = Guid.NewGuid(),
            Scope = scope,
            CustomerIdentifier = waId,
            Basis = ConsentBasis.CustomerInitiatedConversation,
            Purpose = WhatsAppMessagingPurpose,
            Channel = "WhatsApp",
            RecordedByUserId = SystemActorId,
            RecordedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            InteractionReference = threadId,
        }, cancellationToken);

    private async Task SendReplyAsync(TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string replyText, Guid? caseId, CancellationToken cancellationToken)
    {
        if (!outboundProvider.IsConfigured)
        {
            return; // Honest no-op, never fabricate a send that didn't happen.
        }

        // WO 6.1 outbound enforcement: the capture point above just wrote this on every inbound
        // message in this same call, so this will pass for a genuinely active conversation, but
        // it is the real gate, not a formality, for any future caller that reaches SendReplyAsync
        // without having just captured consent itself (e.g. a retry path reading a stale message).
        var consent = await consentRepository.GetCurrentAsync(scope, waId, WhatsAppMessagingPurpose, cancellationToken);
        if (!ConsentEnforcementGuard.IsPermitted(consent))
        {
            return; // Honest no-op, no active consent basis to send on.
        }

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var result = await outboundProvider.SendAsync(new WhatsAppProviderSendRequest(
            inbound.BusinessPhoneNumberId, waId, replyText, TemplateName: null, Language: "en",
            ReplyToProviderMessageId: inbound.ProviderMessageId, IdempotencyKey: idempotencyKey), cancellationToken);

        await whatsAppMessageRepository.SaveAsync(new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ThreadId = threadId,
            BusinessPhoneNumberId = inbound.BusinessPhoneNumberId,
            ContactWaId = waId,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = WhatsAppContentType.Text,
            CustomerSafeBody = replyText,
            ProviderMessageId = result.ProviderMessageId,
            ReplyToProviderMessageId = inbound.ProviderMessageId,
            Purpose = "customer_assistant_reply",
            ConsentBasis = "customer_initiated_conversation",
            ApprovedByUserId = SystemActorId,
            ApprovedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey,
            DeliveryState = result.Accepted ? WhatsAppDeliveryState.Queued : WhatsAppDeliveryState.Failed,
            ProviderErrorCode = result.ProviderErrorCode,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N"),
        }, cancellationToken);
    }

    private static string? ExtractPolicyNumber(string text)
    {
        var match = PolicyNumberPattern.Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string NormalizeWaId(string value) => new(value.Where(char.IsDigit).ToArray());

    private static string Welcome(SenderProfile? sender) =>
        sender is null
            ? "Hello, thanks for getting in touch. I can help you report a claim, check on a claim you've already made, or answer questions about your cover. What can I do for you today?"
            : $"Hi {sender.FirstName}, good to hear from you. I can help you report a claim, check on a claim you've already made, or answer questions about your cover. What can I do for you today?";

    private static string HoldingReply(SenderProfile? sender) =>
        $"Thank you for your question{(sender is null ? "" : $", {sender.FirstName}")}. I've passed it to our team to confirm the answer, and someone will reply to you here shortly.";

    private static string DefaultEscalationMessage() =>
        "I'm not able to help with that directly, I've passed this to our team and someone will follow up with you.";

    /// <summary>Who is writing, when their WhatsApp number matches one person on file.</summary>
    private sealed record SenderProfile(string FullName, string FirstName, IReadOnlyList<Policy> Policies);

    private sealed record AssistantOutcome(
        string? Reply,
        CustomerIntent Intent,
        CustomerAssistantOutcome Outcome,
        IReadOnlyCollection<string> Citations,
        Guid? CaseId,
        string? LeadId,
        string? PolicyNumber,
        bool OpenedCase)
    {
        public static AssistantOutcome Of(
            string? reply,
            CustomerIntent intent,
            CustomerAssistantOutcome outcome,
            IReadOnlyCollection<string>? citations = null,
            Guid? caseId = null,
            string? leadId = null,
            string? policyNumber = null,
            bool openedCase = false) =>
            new(reply, intent, outcome, citations ?? [], caseId, leadId, policyNumber, openedCase);
    }
}

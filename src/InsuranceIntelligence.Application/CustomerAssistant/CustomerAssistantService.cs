using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Translation;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Acquisition;
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

public sealed partial class CustomerAssistantService(
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
    IClaimantAccessLinkIssuer? accessLinkIssuer = null,
    // The four below are all optional and all needed together: without them, or with no language
    // enabled, the assistant is exactly what it was, English in and English out.
    ITranslationService? translationService = null,
    ICustomerLanguageResolutionService? languageResolution = null,
    ILanguageDetector? languageDetector = null,
    IAssistantLanguageSettings? languageSettings = null,
    // Optional: the hidden field-test commands, live only in the Demo profile for the demo phone.
    DemoSessionCommands? demoSession = null,
    // Optional: voice notes in, text replies out. Without it a voice note gets a written reply
    // saying Kato cannot listen yet, never silence.
    IVoiceNoteTranscriber? voiceTranscriber = null,
    // Optional: a voice note is answered with a voice note in the same language. Without it, or
    // when speaking fails, the answer goes as text.
    IVoiceNoteSpeaker? voiceSpeaker = null,
    // Optional: Kato as an agent that holds the conversation and acts through tools. Without it,
    // or when the model fails, the rule-based replies below answer instead.
    IToolCallingChatProvider? agentModel = null,
    // Optional: reads the conversation again once it goes quiet and keeps the prospect record current.
    IConversationInsightsQueue? insightsQueue = null) : ICustomerAssistantService
{
    // Per message: the first name the sender put on their WhatsApp profile, for a greeting only.
    private string? contactFirstName;

    // Per batch: whether a voice note would actually be understood, so Kato only invites them then.
    private bool voiceNotesAvailable;

    internal const string SystemActorId = "system:customer-assistant";
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
        try
        {
            voiceNotesAvailable = voiceTranscriber is not null && await voiceTranscriber.IsAvailableAsync(scope, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            voiceNotesAvailable = false;
        }

        foreach (var inbound in messages)
        {
            try
            {
                contactFirstName = FirstNameOf(inbound.ProfileName);

                // Checked before anything else, including the agent kill switch: resetting the
                // shared demo phone must work even when the assistant itself is switched off.
                if (inbound.ContentType == WhatsAppContentType.Text && demoSession is not null)
                {
                    var command = demoSession.Recognise(NormalizeWaId(inbound.SenderWaId), inbound.CustomerSafeBody);
                    if (command != DemoCommand.None)
                    {
                        await HandleDemoCommandAsync(scope, inbound, command, correlationId, cancellationToken);
                        continue;
                    }
                }

                if (inbound.ContentType is WhatsAppContentType.Image or WhatsAppContentType.Document)
                {
                    // The file itself is stored on the claim by WhatsAppInboundDocumentIngestionService
                    // before this runs; this only tells the customer it arrived.
                    await HandleMediaMessageAsync(scope, inbound, correlationId, cancellationToken);
                    continue;
                }

                var message = inbound;
                if (inbound.ContentType == WhatsAppContentType.Audio)
                {
                    var heard = await TranscribeVoiceNoteAsync(scope, inbound, cancellationToken);
                    if (heard is null)
                    {
                        await ReplyCannotListenAsync(scope, inbound, correlationId, cancellationToken);
                        continue;
                    }

                    // From here a voice note is handled exactly as if they had typed those words.
                    message = inbound with { ContentType = WhatsAppContentType.Text, CustomerSafeBody = heard };
                }

                if (message.ContentType != WhatsAppContentType.Text || string.IsNullOrWhiteSpace(message.CustomerSafeBody))
                {
                    continue;
                }

                var openedCaseId = await HandleTextMessageAsync(scope, message, correlationId, autoReplyEnabled, cancellationToken);
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

    private async Task<string?> TranscribeVoiceNoteAsync(TenantScope scope, WhatsAppInboundMessage inbound, CancellationToken cancellationToken)
    {
        if (!voiceNotesAvailable || voiceTranscriber is null || string.IsNullOrWhiteSpace(inbound.MediaId))
        {
            return null;
        }

        // Transcription can take a while on a cold start; the customer sees "typing..." meanwhile.
        using var typing = StartTypingIndicator(inbound, cancellationToken);
        return await voiceTranscriber.TranscribeAsync(scope, inbound.MediaId, preferredLanguage: null, cancellationToken);
    }

    private async Task ReplyCannotListenAsync(TenantScope scope, WhatsAppInboundMessage inbound, string correlationId, CancellationToken cancellationToken)
    {
        var waId = NormalizeWaId(inbound.SenderWaId);
        var threadId = WhatsAppMessagingService.BuildThreadId(inbound.BusinessPhoneNumberId, waId);
        await RecordConsentAsync(scope, waId, threadId, correlationId, cancellationToken);
        var sender = await IdentifySenderAsync(scope, waId, cancellationToken);
        var reply = KatoReplies.CannotListenYet(sender?.FirstName ?? contactFirstName);
        await SendReplyAsync(scope, inbound, threadId, waId, reply, caseId: null, cancellationToken);
        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = inbound.CustomerSafeBody,
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = reply,
            Outcome = CustomerAssistantOutcome.AutoReplied,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);
    }

    // Display names people use instead of a name: never greet anyone as "Customer".
    private static readonly HashSet<string> NotAName = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer", "client", "user", "me", "myself", "test", "tester", "demo", "whatsapp", "unknown", "admin", "phone",
        "mr", "mrs", "ms", "dr", "sir", "madam", "boss", "official", "business", "shop", "store", "ltd", "limited",
    };

    /// <summary>A usable first name from a self-chosen WhatsApp profile name: letters only, so
    /// "~Sarah N. *" becomes "Sarah" and a name made only of symbols or emoji gives nothing.</summary>
    public static string? FirstNameOf(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        foreach (var token in profileName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (NotAName.Contains(token.Trim('.', ',', '~', '*', '!')))
            {
                continue;
            }

            var letters = new string(token.Where(c => char.IsLetter(c) || c is '\'' or '-').ToArray()).Trim('\'', '-');
            if (letters.Length >= 2 && letters.Length <= 30 && char.IsLetter(letters[0]))
            {
                return char.ToUpperInvariant(letters[0]) + letters[1..];
            }
        }

        return null;
    }

    /// <summary>A contact record for a number nobody is on file for. Never added beside an existing
    /// record: two names on one number makes the sender unidentifiable everywhere.</summary>
    private async Task SaveContactAsync(TenantScope scope, string waId, string name, CancellationToken cancellationToken)
    {
        var existing = (await customerRepository.ListAsync(scope, cancellationToken))
            .Any(customer => PhoneNumbers.SameNumber(customer.Phone, waId));
        if (existing)
        {
            return;
        }

        var fullName = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (fullName.Length is 0 or > 80)
        {
            return;
        }

        var customerId = new Guid(System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.WorkspaceId}:whatsapp-contact:{waId}"))).ToString();
        await customerRepository.UpsertAsync(
            new Customer(customerId, scope.TenantId, scope.WorkspaceId, fullName, null, $"+{waId}"), cancellationToken);
    }

    private static readonly string[] NamePrefixes =
        ["my name is ", "i am ", "i'm ", "im ", "call me ", "it's ", "its ", "this is ", "nze ", "erinnya lyange ", "erinnya lyange nze ", "naitwa ", "jina langu ni "];

    private static readonly HashSet<string> NotNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "one", "other", "that", "this", "it", "yes", "no", "ok", "okay", "please", "thanks", "thank", "you",
        "help", "claim", "accident", "person", "what", "why", "how", "who", "where", "when", "hello", "hi", "hey", "me", "my",
        "yee", "nedda", "kale", "ndiyo", "hapana", "sawa", "stop", "nothing", "none", "later", "cover", "insurance", "policy",
    };

    /// <summary>The name in a reply to "What should I call you?", or null when it does not read as one.</summary>
    public static string? GivenName(string reply)
    {
        var text = reply.Trim().TrimEnd('.', '!');
        foreach (var prefix in NamePrefixes.OrderByDescending(p => p.Length))
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].Trim();
                break;
            }
        }

        // Without "my name is", only a word or two, and none of them an ordinary word: "the other one"
        // or "yes please" is not a name.
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var maxWords = text.Length == reply.Trim().TrimEnd('.', '!').Length ? 2 : 3;
        if (words.Length == 0 || words.Length > maxWords
            || words.Any(word => !word.All(c => char.IsLetter(c) || c is '\'' or '-'))
            || words.Any(word => NotNames.Contains(word))
            || ConversationContinuation.IsGreeting(text))
        {
            return null;
        }

        return string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private async Task HandleDemoCommandAsync(
        TenantScope scope, WhatsAppInboundMessage inbound, DemoCommand command, string correlationId, CancellationToken cancellationToken)
    {
        var waId = NormalizeWaId(inbound.SenderWaId);
        var threadId = WhatsAppMessagingService.BuildThreadId(inbound.BusinessPhoneNumberId, waId);
        await RecordConsentAsync(scope, waId, threadId, correlationId, cancellationToken);

        var reply = await demoSession!.RunAsync(command, scope, waId, correlationId, cancellationToken);
        await SendReplyAsync(scope, inbound, threadId, waId, reply, caseId: null, cancellationToken);

        // Both commands start a fresh conversation: everything before this turn is ignored.
        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = inbound.CustomerSafeBody.Trim(),
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = reply,
            Outcome = CustomerAssistantOutcome.SessionReset,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);
    }

    /// <summary>This conversation's turns, oldest first, from after the most recent demo reset.</summary>
    private async Task<CustomerConversationTurn[]> CurrentThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken)
    {
        var turns = (await conversationRepository.ListForThreadAsync(scope, threadId, cancellationToken))
            .OrderBy(turn => turn.OccurredAt)
            .ToArray();
        var lastReset = Array.FindLastIndex(turns, turn => turn.Outcome == CustomerAssistantOutcome.SessionReset);
        return lastReset < 0 ? turns : turns[(lastReset + 1)..];
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

    /// <summary>WhatsApp clears a typing indicator after 25 seconds, and an AI answer that is then
    /// translated can take longer, so it is refreshed shortly before it would lapse, a bounded number
    /// of times so a stuck reply cannot keep a customer watching "typing..." indefinitely.</summary>
    internal static readonly TimeSpan TypingRefreshInterval = TimeSpan.FromSeconds(20);
    internal const int MaxTypingIndicators = 4;

    private TypingIndicator StartTypingIndicator(WhatsAppInboundMessage inbound, CancellationToken cancellationToken)
    {
        var indicator = new TypingIndicator(cancellationToken);
        _ = KeepTypingAsync(inbound, indicator.Token);
        return indicator;
    }

    private async Task KeepTypingAsync(WhatsAppInboundMessage inbound, CancellationToken token)
    {
        for (var shown = 0; shown < MaxTypingIndicators && !token.IsCancellationRequested; shown++)
        {
            try
            {
                if (!await outboundProvider.ShowTypingAsync(inbound.BusinessPhoneNumberId, inbound.ProviderMessageId, token))
                {
                    return;
                }

                await Task.Delay(TypingRefreshInterval, token);
            }
            catch
            {
                // Cosmetic only: a failed or cancelled indicator must never affect the reply itself.
                return;
            }
        }
    }

    private sealed class TypingIndicator(CancellationToken cancellationToken) : IDisposable
    {
        private readonly CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        public CancellationToken Token => stop.Token;

        public void Dispose()
        {
            stop.Cancel();
            stop.Dispose();
        }
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

        // From here on the customer always gets a reply (an answer, a holding message, a language
        // question or a hand-off to a person), so show them "typing..." while it is prepared. It stops
        // when this method returns, whatever path it takes.
        using var typing = StartTypingIndicator(inbound, cancellationToken);

        // WO 6.1 capture point: the customer messaging first is the real basis every hardcoded
        // "customer_initiated_conversation" string in this codebase was standing in for, record
        // it as a real, queryable, auditable grant instead of an ambient assumption. Every inbound
        // message refreshes it (append-only, so the full history survives), which is also what
        // keeps SendReplyAsync's enforcement check below permitted for an active conversation.
        await RecordConsentAsync(scope, waId, threadId, correlationId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var sender = await IdentifySenderAsync(scope, waId, cancellationToken);
        var thread = await CurrentThreadAsync(scope, threadId, cancellationToken);
        var previousTurn = thread.Length == 0 ? null : thread[^1];

        // Someone new: keep a contact record under the name they gave WhatsApp, so staff see who
        // wrote and Kato greets them by name next time. It holds no policy, so it unlocks nothing.
        if (sender is null && contactFirstName is not null)
        {
            await SaveContactAsync(scope, waId, inbound.ProfileName!, cancellationToken);
        }

        // Which language is this customer conversing in? Everything from here on runs on English:
        // the customer's message is translated at the door and the reply is translated on the way
        // out, so no gate, classifier, consent or identity check ever sees anything but English.
        var original = text;
        var language = await ResolveConversationLanguageAsync(scope, waId, original, previousTurn, cancellationToken);
        if (language.Step != LanguageStep.Proceed)
        {
            await HandleLanguageStepAsync(scope, inbound, threadId, waId, original, language, sender, correlationId, cancellationToken,
                firstContact: thread.Length == 0 && sender is null);
            return null;
        }

        var understood = await UnderstandAsync(scope, original, language.Inbound, cancellationToken);
        if (understood.NeedsAPerson)
        {
            await EscalateUntranslatedAsync(scope, inbound, threadId, waId, original, language, sender, understood.Failure, correlationId, cancellationToken);
            return null;
        }

        text = understood.English;

        var classified = intentClassifier.Classify(text);
        if (CustomerMessageSignals.IsBareHumanRequest(text))
        {
            classified = CustomerIntent.HumanRequested;
        }

        var continuation = ConversationContinuation.Resolve(classified, ExtractPolicyNumber(text) is not null, thread, now);
        var answeringOurQuestion = continuation.EarlierMessageText is not null;

        var intent = continuation.Intent;
        // The agent reads the message itself; asking the model for an intent first only cost the
        // customer about nine seconds per message for an answer nobody used.
        if (agentModel is null && intent == CustomerIntent.Unknown && !answeringOurQuestion && ShouldAskModelForIntent(text))
        {
            intent = await ClassifyWithModelAsync(scope, text, correlationId, cancellationToken);
        }

        // When the customer wrote in another language, the claim keeps their own words beside the
        // English description, including the earlier message this one continues.
        CustomerOwnWords? ownWords = null;
        if (understood.Status == TranslationStatus.Translated)
        {
            var earlierOriginal = continuation.EarlierMessageText is not null ? previousTurn?.OriginalInboundText : null;
            var wording = string.IsNullOrWhiteSpace(earlierOriginal) ? original : $"{earlierOriginal}\n{original}";
            ownWords = new CustomerOwnWords(wording, SupportedLanguage.For(language.Inbound).Iso);
        }

        // A claim read back to the customer waits for their answer: yes opens it, no starts it
        // again, anything else is a correction. Nothing binding happens on the first telling.
        AssistantOutcome? outcome = null;
        var turnId = Guid.NewGuid();
        if (agentModel is not null)
        {
            try
            {
                outcome = await RunAgentAsync(
                    scope, original, text, waId, threadId, turnId, thread, language.Reply,
                    understood.Status == TranslationStatus.Translated ? new CustomerOwnWords(original, SupportedLanguage.For(language.Inbound).Iso) : null,
                    correlationId, now, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The model is unavailable or misbehaved: fall back to the rule-based replies
                // rather than leave the customer without an answer.
                outcome = null;
            }
        }

        if (outcome is null && previousTurn is { Outcome: CustomerAssistantOutcome.AwaitingClaimConfirmation }
            && now - previousTurn.OccurredAt <= ConversationContinuation.Window)
        {
            outcome = await HandlePendingClaimAsync(
                scope, original, text, classified, waId, previousTurn, sender, correlationId, now,
                understood.Status == TranslationStatus.Translated ? new CustomerOwnWords(original, SupportedLanguage.For(language.Inbound).Iso) : null,
                cancellationToken);
        }

        outcome ??= await DecideAsync(
            scope, inbound, text, waId, intent, continuation, answeringOurQuestion, previousTurn, sender, correlationId, autoReplyEnabled, now, cancellationToken, ownWords,
            firstContact: thread.Length == 0);

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
        // An unknown number's first message always meets Kato, even when it goes straight to a
        // question or an accident, so the person knows who is answering and what he can do.
        if (reply is not null && !outcome.FromAgent && thread.Length == 0 && sender is null && !reply.StartsWith("Hello", StringComparison.Ordinal))
        {
            reply = $"{KatoReplies.FirstContactPrefix(contactFirstName)} {reply}";
        }

        LocalisedReply? localised = null;
        if (reply is not null)
        {
            var textToSend = outcome.Outcome == CustomerAssistantOutcome.QueuedForHumanApproval ? HoldingReply(sender) : reply;
            localised = await LocaliseAsync(scope, textToSend, language.Reply, sender?.FirstName, cancellationToken,
                noteAlreadyShown: thread.Any(turn => turn.OutboundTranslationStatus == TranslationStatus.Translated));
            await SendReplyAsync(scope, inbound, threadId, waId, localised.Text, outcome.CaseId, cancellationToken, LanguageIsoFor(localised.Status, language.Reply),
                asVoice: inbound.MediaId is not null);
        }

        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = turnId,
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
            CustomerLanguage = language.Reply == SupportedLanguageCode.En ? null : SupportedLanguage.For(language.Reply).Iso,
            OriginalInboundText = string.Equals(original, text, StringComparison.Ordinal) ? null : original,
            OutboundLocalisedText = localised is { Status: TranslationStatus.Translated } ? localised.Text : null,
            InboundTranslationStatus = understood.Status,
            OutboundTranslationStatus = localised?.Status,
            PendingClaimDescription = outcome.PendingClaimDescription,
            PendingClaimOriginalText = outcome.PendingClaimOriginalText,
            PendingClaimOriginalLanguage = outcome.PendingClaimOriginalLanguage,
            OfferedNewIncidentForCaseId = outcome.OfferedNewIncidentForCaseId,
            Occupation = outcome.Occupation,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, outcome.CaseId, null, SystemActorId, ActorType.Integration,
                $"customer_assistant.{outcome.Outcome.ToString().ToLowerInvariant()}", DateTimeOffset.UtcNow,
                null, null, outcome.LeadId is null ? [] : [outcome.LeadId], "CustomerAssistant", null,
                "recorded", null,
                $"WhatsApp customer assistant classified this message as {outcome.Intent} and took action {outcome.Outcome}{(sender is null ? "" : ", sender recognised by phone number")}{LanguageEvidence(language, understood.Status, localised?.Status)}. Message body omitted from evidence.",
                correlationId),
            cancellationToken);

        await NoteProspectActivityAsync(scope, waId, threadId, correlationId, cancellationToken);

        return outcome.OpenedCase ? outcome.CaseId : null;
    }

    /// <summary>After the reply has gone: keep the person's prospect record (if they have one) pointing
    /// at this conversation and its last message, and ask for the conversation to be read again once it
    /// goes quiet. Never fails the customer's turn.</summary>
    private async Task NoteProspectActivityAsync(TenantScope scope, string waId, string threadId, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var lead = await salesLeadWriteRepository.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, waId, cancellationToken);
            if (lead is not null)
            {
                await salesLeadWriteRepository.CreateAsync(scope.TenantId, scope.WorkspaceId,
                    lead with { ThreadId = threadId, LastInteractionAt = DateTimeOffset.UtcNow }, cancellationToken);
            }

            insightsQueue?.Enqueue(scope, waId, threadId, correlationId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The customer already has their answer; the record catches up on the next message.
        }
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
        CancellationToken cancellationToken,
        CustomerOwnWords? ownWords = null,
        bool firstContact = false)
    {
        // Honest about what he is: asked directly, Kato says plainly he is not a person.
        if (CustomerMessageSignals.IsAskingIfAutomated(text))
        {
            return AssistantOutcome.Of(KatoReplies.NotAPerson(sender?.FirstName), CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
        }

        var original = ownWords?.Text ?? text;
        var hasCover = sender is { Policies.Count: > 0 };

        // Kato asked what to call them: a short reply that reads as a name is the answer.
        if (previousTurn is { Outcome: CustomerAssistantOutcome.AwaitingName }
            && sender is null
            && now - previousTurn.OccurredAt <= ConversationContinuation.Window
            && intent == CustomerIntent.Unknown
            && GivenName(inbound.CustomerSafeBody) is { } givenName)
        {
            await SaveContactAsync(scope, waId, givenName, cancellationToken);
            return AssistantOutcome.Of(KatoReplies.NiceToMeetYou(FirstNameOf(givenName)!), CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
        }

        // Kato asked what work they do: this message is the answer, unless it clearly asks for
        // something else (a person, a claim, a claim's status).
        if (previousTurn is { Outcome: CustomerAssistantOutcome.AwaitingOccupation }
            && now - previousTurn.OccurredAt <= ConversationContinuation.Window
            && intent is not (CustomerIntent.HumanRequested or CustomerIntent.NewClaim or CustomerIntent.ExistingClaimStatus))
        {
            return await SuggestCoverAsync(scope, original, text, waId, sender, correlationId, cancellationToken, askIfUnclear: false);
        }

        if (!answeringOurQuestion && intent is CustomerIntent.Unknown or CustomerIntent.GeneralQuestion)
        {
            if (CustomerMessageSignals.IsThanksOrClose(text))
            {
                return AssistantOutcome.Of(KatoReplies.YoureWelcome(sender?.FirstName), CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
            }

            if (CustomerMessageSignals.IsAccessLinkRequest(text))
            {
                return await HandleAccessLinkRequestAsync(scope, waId, sender, previousTurn, correlationId, cancellationToken);
            }

            // "I sell tomatoes in Nakasero" from someone without cover is the start of finding cover
            // that fits their work, not an unclear message.
            if (!hasCover && intent == CustomerIntent.Unknown && MicroinsuranceCatalog.OccupationFrom(original, text) is not null)
            {
                return await SuggestCoverAsync(scope, original, text, waId, sender, correlationId, cancellationToken, askIfUnclear: true);
            }
        }

        return intent switch
        {
            CustomerIntent.NewClaim => await HandleNewClaimAsync(scope, text, waId, continuation, previousTurn, sender, correlationId, now, cancellationToken, ownWords),
            CustomerIntent.ExistingClaimStatus => await HandleExistingClaimStatusAsync(scope, text, waId, sender, cancellationToken),
            CustomerIntent.GeneralQuestion => await HandleGeneralQuestionAsync(scope, text, sender, correlationId, autoReplyEnabled, cancellationToken),
            CustomerIntent.ProductInterest when !hasCover => await SuggestCoverAsync(scope, original, text, waId, sender, correlationId, cancellationToken, askIfUnclear: true),
            CustomerIntent.ProductInterest => await HandleProductInterestAsync(scope, text, waId, sender, correlationId, cancellationToken),
            CustomerIntent.HumanRequested => await HandleHumanRequestedAsync(scope, text, waId, sender, correlationId, cancellationToken),
            // No name from WhatsApp and none on record: Kato introduces himself and asks for one.
            _ when firstContact && sender is null && contactFirstName is null =>
                AssistantOutcome.Of($"{Welcome(sender)} {KatoReplies.AskName}", CustomerIntent.Unknown, CustomerAssistantOutcome.AwaitingName),
            _ when ConversationContinuation.IsGreeting(text) || firstContact =>
                AssistantOutcome.Of(Welcome(sender), CustomerIntent.Unknown, CustomerAssistantOutcome.AwaitingMoreInformation),
            _ => ClarifyOrEscalate(previousTurn, sender, now),
        };
    }

    /// <summary>For someone without cover: the products that fit the work they do, explained in
    /// plain words, and a lead so a licensed person can follow up. Kato never completes a sale and
    /// never compares insurers.</summary>
    private async Task<AssistantOutcome> SuggestCoverAsync(
        TenantScope scope, string original, string text, string waId, SenderProfile? sender, string correlationId,
        CancellationToken cancellationToken, bool askIfUnclear)
    {
        var occupation = MicroinsuranceCatalog.OccupationFrom(original, text);
        if (occupation is null && askIfUnclear)
        {
            return AssistantOutcome.Of(KatoReplies.AskOccupation(sender?.FirstName), CustomerIntent.ProductInterest, CustomerAssistantOutcome.AwaitingOccupation);
        }

        var kind = occupation ?? MicroinsuranceCatalog.OccupationKind.Other;
        var products = MicroinsuranceCatalog.ProductsFor(kind);
        var leadId = await CreateLeadAsync(
            scope, original, waId, sender, "WhatsApp - Kato cover suggestion", correlationId, cancellationToken,
            productFit: string.Join(" + ", products.Select(p => p.Name)),
            occupation: original);
        return AssistantOutcome.Of(
            KatoReplies.CoverSuggestion(sender?.FirstName, MicroinsuranceCatalog.Explain(kind)),
            CustomerIntent.ProductInterest, CustomerAssistantOutcome.LeadCreated, leadId: leadId) with
        {
            Occupation = original,
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
            return AssistantOutcome.Of(KatoReplies.HandingToPerson(sender?.FirstName), CustomerIntent.Unknown, CustomerAssistantOutcome.Escalated);
        }

        return AssistantOutcome.Of(KatoReplies.Clarify(), CustomerIntent.Unknown, CustomerAssistantOutcome.Clarifying);
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
        CancellationToken cancellationToken,
        CustomerOwnWords? ownWords = null)
    {
        var earlier = continuation.EarlierMessageText;
        var everything = string.IsNullOrWhiteSpace(earlier) ? text : $"{earlier}\n{text}";

        // A claim can only be reported from the phone number on the policy. A typed policy number is
        // printed on paperwork anyone can hold, so on its own it proves nothing about who is writing.
        if (sender is not { Policies.Count: > 0 })
        {
            return AssistantOutcome.Of(
                KatoReplies.NoCoverOnThisNumber(sender?.FirstName),
                CustomerIntent.NewClaim, CustomerAssistantOutcome.Declined);
        }

        var typedPolicyNumber = ExtractPolicyNumber(text) ?? (earlier is null ? null : ExtractPolicyNumber(earlier));
        Policy? policy = null;
        if (typedPolicyNumber is not null)
        {
            policy = sender.Policies.FirstOrDefault(p => string.Equals(p.PolicyNumber, typedPolicyNumber, StringComparison.OrdinalIgnoreCase));
            if (policy is null)
            {
                return AssistantOutcome.Of(
                    KatoReplies.PolicyNotOnThisNumber(sender.FirstName),
                    CustomerIntent.NewClaim, CustomerAssistantOutcome.Declined);
            }
        }
        else if (continuation.PolicyNumber is not null)
        {
            policy = sender.Policies.FirstOrDefault(p => string.Equals(p.PolicyNumber, continuation.PolicyNumber, StringComparison.OrdinalIgnoreCase));
        }

        if (policy is null)
        {
            var choosing = previousTurn?.Outcome == CustomerAssistantOutcome.AwaitingPolicyChoice;
            policy = ChoosePolicy(sender.Policies, choosing ? text : everything, choosing);
            if (policy is null)
            {
                return AssistantOutcome.Of(
                    KatoReplies.WhichCover(sender.FirstName, sender.Policies),
                    CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingPolicyChoice);
            }
        }

        var description = IncidentDescription(everything);
        if (!CustomerMessageSignals.HasEnoughIncidentDetail(description))
        {
            return AssistantOutcome.Of(
                KatoReplies.AskWhatHappened(sender.FirstName, policy.ProductLine),
                CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingIncidentDetails, policyNumber: policy.PolicyNumber);
        }

        // The same accident told twice (or retold hours later) must never become two claims. The
        // new words are added to the open claim, and the customer is asked before a second is started.
        var recent = await RecentOpenClaimAsync(scope, policy.PolicyNumber, now, cancellationToken);
        if (recent is not null)
        {
            await AddDetailToClaimAsync(recent, description, now, correlationId, cancellationToken);
            return AssistantOutcome.Of(
                KatoReplies.AlreadyReported(sender.FirstName, ClaimReference.For(recent.CaseId), recent.CreatedAt),
                CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingClaimConfirmation,
                caseId: recent.CaseId, policyNumber: policy.PolicyNumber) with
            {
                PendingClaimDescription = description,
                PendingClaimOriginalText = ownWords?.Text,
                PendingClaimOriginalLanguage = ownWords?.LanguageIso,
                OfferedNewIncidentForCaseId = recent.CaseId,
            };
        }

        return ReadBack(sender.FirstName, policy, description, ownWords?.Text, ownWords?.LanguageIso);
    }

    private static AssistantOutcome ReadBack(string? firstName, Policy policy, string description, string? originalText, string? originalLanguage) =>
        AssistantOutcome.Of(
            KatoReplies.ReadBack(firstName, policy.ProductLine, description),
            CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingClaimConfirmation, policyNumber: policy.PolicyNumber) with
        {
            PendingClaimDescription = description,
            PendingClaimOriginalText = originalText,
            PendingClaimOriginalLanguage = originalLanguage,
        };

    private static readonly TimeSpan DuplicateClaimWindow = TimeSpan.FromHours(72);

    private async Task<ClaimCase?> RecentOpenClaimAsync(TenantScope scope, string policyNumber, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await caseRepository.ListAsync(scope, cancellationToken))
            .Where(c => string.Equals(c.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase))
            .Where(c => ClaimStatusLabels.IsOpen(c.Status) && now - c.CreatedAt <= DuplicateClaimWindow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

    private async Task AddDetailToClaimAsync(ClaimCase claimCase, string detail, DateTimeOffset now, string correlationId, CancellationToken cancellationToken)
    {
        if (claimCase.IncidentDescription.Contains(detail, StringComparison.OrdinalIgnoreCase))
        {
            return; // Told exactly the same thing again: nothing new to add.
        }

        await caseRepository.SaveAsync(claimCase with
        {
            IncidentDescription = $"{claimCase.IncidentDescription}\n\nMore from the customer on WhatsApp ({now:d MMM, HH:mm}): {detail}",
            UpdatedAt = now,
        }, cancellationToken);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), claimCase.Scope, claimCase.CaseId, null, SystemActorId, ActorType.Integration,
                "case.detail_added", now, null, null, [], "CustomerAssistant", null, "recorded", null,
                "The customer described this incident again on WhatsApp; the new detail was added to the open claim instead of opening a second one. Message body omitted from evidence.",
                correlationId),
            cancellationToken);
    }

    /// <summary>The customer answered a read-back (or the "is this a new accident?" question).
    /// Null means the message is about something else and takes the normal route.</summary>
    private async Task<AssistantOutcome?> HandlePendingClaimAsync(
        TenantScope scope, string original, string text, CustomerIntent classified, string waId,
        CustomerConversationTurn pending, SenderProfile? sender, string correlationId, DateTimeOffset now,
        CustomerOwnWords? ownWords, CancellationToken cancellationToken)
    {
        if (classified is CustomerIntent.HumanRequested or CustomerIntent.ExistingClaimStatus or CustomerIntent.ProductInterest
            || CustomerMessageSignals.IsAskingIfAutomated(text))
        {
            return null;
        }

        var policy = sender?.Policies.FirstOrDefault(p => string.Equals(p.PolicyNumber, pending.PolicyNumber, StringComparison.OrdinalIgnoreCase));
        if (policy is null || string.IsNullOrWhiteSpace(pending.PendingClaimDescription))
        {
            return null;
        }

        if (pending.OfferedNewIncidentForCaseId is not null)
        {
            return KatoWords.IsNewIncident(original, text)
                ? ReadBack(sender!.FirstName, policy, pending.PendingClaimDescription, pending.PendingClaimOriginalText, pending.PendingClaimOriginalLanguage)
                : null;
        }

        if (KatoWords.IsYes(original, text))
        {
            return await OpenConfirmedClaimAsync(scope, waId, sender!, policy, pending, correlationId, now, cancellationToken);
        }

        if (KatoWords.IsNo(original, text))
        {
            return AssistantOutcome.Of(
                KatoReplies.TellMeAgain(sender!.FirstName),
                CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingIncidentDetails, policyNumber: policy.PolicyNumber);
        }

        // Anything else is a correction or more detail: add it and read the whole thing back again.
        var corrected = $"{pending.PendingClaimDescription}\n{text}";
        var correctedOriginal = ownWords is null ? pending.PendingClaimOriginalText : $"{pending.PendingClaimOriginalText}\n{ownWords.Text}".Trim();
        return ReadBack(sender!.FirstName, policy, corrected, correctedOriginal, ownWords?.LanguageIso ?? pending.PendingClaimOriginalLanguage);
    }

    private async Task<AssistantOutcome> OpenConfirmedClaimAsync(
        TenantScope scope, string waId, SenderProfile sender, Policy policy, CustomerConversationTurn pending,
        string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var description = pending.PendingClaimDescription!;

        // Confirmed twice in quick succession (a double tap, a retried webhook): one claim only.
        var recent = await RecentOpenClaimAsync(scope, policy.PolicyNumber, now, cancellationToken);
        if (recent is not null && now - recent.CreatedAt <= TimeSpan.FromMinutes(10))
        {
            return AssistantOutcome.Of(
                KatoReplies.AlreadyReported(sender.FirstName, ClaimReference.For(recent.CaseId), recent.CreatedAt),
                CustomerIntent.NewClaim, CustomerAssistantOutcome.AutoReplied, caseId: recent.CaseId, policyNumber: policy.PolicyNumber);
        }

        var customer = string.IsNullOrWhiteSpace(policy.CustomerId) ? null : await customerRepository.GetAsync(policy.CustomerId, scope, cancellationToken);
        var caseIntake = new CaseIntakeService(caseRepository, auditRepository, productRepository);
        var claimCase = await caseIntake.CreateCaseAsync(new CreateCaseCommand(
            scope,
            CustomerName: customer?.FullName ?? sender.FullName,
            CustomerEmail: customer?.Email,
            CustomerPhone: customer?.Phone ?? $"+{waId}",
            PolicyNumber: policy.PolicyNumber,
            ProductLine: policy.ProductLine,
            ClaimType: $"{policy.ProductLine} claim reported on WhatsApp",
            Channel: "WhatsApp",
            IncidentDate: CustomerMessageSignals.IncidentDate(description, DateOnly.FromDateTime(now.UtcDateTime)),
            IncidentDescription: description,
            CreatedByUserId: SystemActorId,
            CorrelationId: correlationId,
            RequiresConfirmation: true,
            OriginalIncidentText: pending.PendingClaimOriginalText,
            OriginalIncidentLanguage: pending.PendingClaimOriginalLanguage), cancellationToken);

        if (!string.IsNullOrWhiteSpace(claimCase.AssignedUserId))
        {
            try
            {
                await notificationEmitter.NotifyAsync(
                    claimCase.Scope,
                    claimCase.AssignedUserId,
                    NotificationType.ClaimAssigned,
                    $"New WhatsApp claim assigned to you on the {claimCase.AssignedTeam} team, confirmed by the customer, awaiting your confirmation.",
                    claimCase.CaseId,
                    cancellationToken);
            }
            catch
            {
                // Best effort, the claim itself has already been created; a notification failure
                // must never surface to the customer or block their WhatsApp reply.
            }
        }

        var documents = await DocumentsToRequestAsync(claimCase, cancellationToken);
        var link = await IssueAccessLinkAsync(scope, claimCase, waId, correlationId, cancellationToken);
        return AssistantOutcome.Of(
            KatoReplies.ClaimSent(sender.FirstName, ClaimReference.For(claimCase.CaseId), ClaimDocumentChecklist.AsSentenceList(documents), link),
            CustomerIntent.NewClaim, CustomerAssistantOutcome.CaseOpened,
            caseId: claimCase.CaseId, policyNumber: policy.PolicyNumber, openedCase: true);
    }

    private async Task<AssistantOutcome> HandleExistingClaimStatusAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, CancellationToken cancellationToken)
    {
        // Claim news goes only to the phone number on the policy. WhatsApp verified that number when
        // the customer registered it; a typed policy number proves nothing about who is writing.
        if (sender is null)
        {
            return AssistantOutcome.Of(KatoReplies.OnlyForThePolicyNumber(), CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.Declined);
        }

        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var theirs = CasesFor(cases, sender, waId).OrderByDescending(c => c.CreatedAt).ToArray();
        var open = theirs.Where(c => ClaimStatusLabels.IsOpen(c.Status)).Take(3).ToArray();
        if (open.Length > 0)
        {
            var sentences = open.Select(c => KatoReplies.ClaimStatusSentence(
                ClaimReference.For(c.CaseId), c.ProductLine, c.CreatedAt, ClaimStatusLabels.For(c.Status).ToLowerInvariant()));
            return AssistantOutcome.Of(
                KatoReplies.ClaimStatusList(sender.FirstName, sentences),
                CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied, caseId: open[0].CaseId);
        }

        if (theirs.Length > 0)
        {
            var latest = theirs[0];
            return AssistantOutcome.Of(
                KatoReplies.NoOpenClaims(sender.FirstName, ClaimReference.For(latest.CaseId), latest.ProductLine, ClaimStatusLabels.For(latest.Status).ToLowerInvariant()),
                CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied, caseId: latest.CaseId);
        }

        return AssistantOutcome.Of(KatoReplies.NoClaimsYet(sender.FirstName), CustomerIntent.ExistingClaimStatus, CustomerAssistantOutcome.AutoReplied);
    }

    private async Task<AssistantOutcome> HandleAccessLinkRequestAsync(
        TenantScope scope, string waId, SenderProfile? sender, CustomerConversationTurn? previousTurn, string correlationId, CancellationToken cancellationToken)
    {
        // A link opens the claim portal, so it goes only to the phone number on the policy.
        if (sender is null)
        {
            return AssistantOutcome.Of(KatoReplies.OnlyForThePolicyNumber(), CustomerIntent.Unknown, CustomerAssistantOutcome.Declined);
        }

        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var claimCase = CasesFor(cases, sender, waId).Where(c => ClaimStatusLabels.IsOpen(c.Status)).OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        if (claimCase is null)
        {
            return AssistantOutcome.Of(KatoReplies.NoOpenClaimForLink(sender.FirstName), CustomerIntent.Unknown, CustomerAssistantOutcome.AutoReplied);
        }

        var link = await IssueAccessLinkAsync(scope, claimCase, waId, correlationId, cancellationToken);
        var reference = ClaimReference.For(claimCase.CaseId);
        return AssistantOutcome.Of(
            link is null ? KatoReplies.LinkUnavailable(reference) : KatoReplies.SecureLink(reference, link),
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
        var thread = await CurrentThreadAsync(scope, threadId, cancellationToken);
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
            reply = claimCase is null
                ? KatoReplies.PhotoHeld(sender?.FirstName)
                : KatoReplies.PhotoAdded(sender?.FirstName, ClaimReference.For(claimCase.CaseId));

            // A photo has no words to detect a language from, so use the one this customer already
            // chose. Nothing remembered means English, as it always was.
            var replyLanguage = SupportedLanguageCode.En;
            if (LanguagesEnabled)
            {
                var remembered = await languageResolution!.ResolveAsync(scope, waId, string.Empty, false, cancellationToken);
                if (remembered.Source == LanguageResolutionSource.RememberedPreference && languageSettings!.EnabledLanguages.Contains(remembered.Language))
                {
                    replyLanguage = remembered.Language;
                }
            }

            var localisedAck = await LocaliseAsync(scope, reply, replyLanguage, sender?.FirstName, cancellationToken);
            await SendReplyAsync(scope, inbound, threadId, waId, localisedAck.Text, claimCase?.CaseId, cancellationToken,
                LanguageIsoFor(localisedAck.Status, replyLanguage));
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
        TenantScope scope, string text, SenderProfile? sender, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken)
    {
        // Search only the product the question is about, so a motor question is answered from the
        // motor wording and not from whatever else shares its words.
        var productLine = QuestionProductLine(text, sender);
        var question = new AssistantQuestion(scope, null, text, productLine, "Customer", SystemActorId, correlationId);
        var retrieval = await retrievalProvider.RetrieveAsync(question, cancellationToken);
        if (retrieval.Chunks.Count == 0 && productLine is not null)
        {
            question = question with { ProductLine = null };
            retrieval = await retrievalProvider.RetrieveAsync(question, cancellationToken);
        }

        if (retrieval.Chunks.Count == 0)
        {
            // Nothing approved to ground an answer in, never guess from model memory for a customer.
            return AssistantOutcome.Of(KatoReplies.NoTrustedAnswer(), CustomerIntent.GeneralQuestion, CustomerAssistantOutcome.QueuedForHumanApproval);
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
            KatoReplies.PersonRequested(sender?.FirstName),
            CustomerIntent.ProductInterest, CustomerAssistantOutcome.LeadCreated, leadId: leadId);
    }

    /// <summary>The product line a customer's question is about: one they name, preferring their own
    /// covers, otherwise their only cover. Null searches every approved product.</summary>
    private static string? QuestionProductLine(string text, SenderProfile? sender)
    {
        var own = sender?.Policies.Select(p => p.ProductLine).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (own.Length > 0 && CustomerMessageSignals.ProductLineHint(text, own) is { } ownHint)
        {
            return ownHint;
        }

        return CustomerMessageSignals.ProductLineHint(text) ?? (own.Length == 1 ? own[0] : null);
    }

    private async Task<AssistantOutcome> HandleHumanRequestedAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, string correlationId, CancellationToken cancellationToken)
    {
        var leadId = await CreateLeadAsync(scope, text, waId, sender, "WhatsApp - human requested", correlationId, cancellationToken);
        return AssistantOutcome.Of(
            KatoReplies.PersonRequested(sender?.FirstName),
            CustomerIntent.HumanRequested, CustomerAssistantOutcome.Escalated, leadId: leadId);
    }

    /// <summary>
    /// The person's prospect record: updated when this phone number already has one, created when it
    /// does not. One record per person, however many times they write; each call adds a line to its
    /// activity so the record shows what happened and when.
    /// </summary>
    private async Task<string?> CreateLeadAsync(
        TenantScope scope, string text, string waId, SenderProfile? sender, string leadSource, string correlationId, CancellationToken cancellationToken,
        string? productFit = null, string? occupation = null, Func<SalesLeadDetailReadModel, SalesLeadDetailReadModel>? change = null,
        bool createIfMissing = true)
    {
        var now = DateTimeOffset.UtcNow;
        var digits = NormalizeWaId(waId);
        var activity = new ProspectActivity(now, LeadActivityText(leadSource, productFit), "Kato");
        var existing = await salesLeadWriteRepository.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, digits, cancellationToken);

        if (existing is null && !createIfMissing)
        {
            return null;
        }

        SalesLeadDetailReadModel lead;
        if (existing is not null)
        {
            var assessed = productFit is not null && !string.Equals(productFit, "To be chosen", StringComparison.OrdinalIgnoreCase);
            lead = existing with
            {
                CustomerName = sender?.FullName is { Length: > 0 } known && existing.CustomerName is "WhatsApp customer" or "" ? known : existing.CustomerName,
                ProductFit = assessed ? productFit! : existing.ProductFit,
                Phone = existing.Phone ?? digits,
                PrimaryChannel = existing.PrimaryChannel ?? "WhatsApp",
                LastInteractionAt = now,
                Activity = [.. existing.Activity, activity],
            };
        }
        else
        {
            lead = new SalesLeadDetailReadModel(
                LeadId: $"LEAD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                LeadSource: leadSource,
                CustomerNeed: text,
                ConversationSummary: occupation is null
                    ? $"Captured automatically from an inbound WhatsApp message on {now:yyyy-MM-dd}."
                    : $"Captured by Kato on WhatsApp on {now:yyyy-MM-dd}. Work described by the customer: \"{occupation}\". Products explained (not sold): {productFit}.",
                ProductFit: productFit ?? "To be assessed",
                QuoteStatus: "New",
                RequiredInformation: ["Confirm contact details", "Confirm insurable interest", "Capture risk details for quote"],
                FollowUpActions: ["Contact customer to confirm details", "Prepare indicative quote for human review"],
                AssignedSalesOfficer: null,
                ConversionProbability: null,
                CustomerName: sender?.FullName ?? "WhatsApp customer",
                ContactInfo: $"+{digits}",
                CreatedAt: now,
                CreatedBy: SystemActorId)
            {
                Phone = digits,
                PrimaryChannel = "WhatsApp",
                LastInteractionAt = now,
                Activity = [activity],
            };
        }

        if (change is not null)
        {
            lead = change(lead);
        }

        await salesLeadWriteRepository.CreateAsync(scope.TenantId, scope.WorkspaceId, lead, cancellationToken);
        var leadId = lead.LeadId;
        if (existing is not null)
        {
            return leadId;
        }

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

    private static string LeadActivityText(string leadSource, string? productFit) => leadSource switch
    {
        "WhatsApp - human requested" => "Asked to speak to a person",
        "WhatsApp - cover application" => productFit is null ? "Applied for cover" : $"Applied for {productFit}",
        "WhatsApp - Kato registration" => "Registered on WhatsApp",
        "WhatsApp - Kato cover suggestion" => "Kato suggested covers that fit their work",
        "WhatsApp" => "Asked about cover",
        _ => leadSource,
    };

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

        var hint = CustomerMessageSignals.ProductLineHint(text, policies.Select(policy => policy.ProductLine).ToArray());
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

    private async Task SendReplyAsync(TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string replyText, Guid? caseId, CancellationToken cancellationToken, string languageIso = "en", bool asVoice = false)
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

        // Spoken to, Kato speaks back, in the language of the reply. A reply carrying a link also
        // goes as text, because a link cannot be tapped in a voice note.
        if (asVoice && voiceSpeaker is not null && SpokenText(replyText) is { } spoken)
        {
            var audio = await voiceSpeaker.SpeakAsync(scope, spoken, LanguageFromIso(languageIso), cancellationToken);
            if (audio is not null)
            {
                var voiceKey = Guid.NewGuid().ToString("N");
                var voice = await outboundProvider.SendVoiceNoteAsync(inbound.BusinessPhoneNumberId, waId, audio, inbound.ProviderMessageId, cancellationToken);
                await SaveOutboundAsync(scope, inbound, threadId, waId, replyText, caseId, voice, voiceKey, WhatsAppContentType.Audio, cancellationToken);
                if (voice.Accepted && !replyText.Contains("http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var result = await outboundProvider.SendAsync(new WhatsAppProviderSendRequest(
            inbound.BusinessPhoneNumberId, waId, replyText, TemplateName: null, Language: languageIso,
            ReplyToProviderMessageId: inbound.ProviderMessageId, IdempotencyKey: idempotencyKey), cancellationToken);

        await SaveOutboundAsync(scope, inbound, threadId, waId, replyText, caseId, result, idempotencyKey, WhatsAppContentType.Text, cancellationToken);
    }

    private static string? ExtractPolicyNumber(string text)
    {
        var match = PolicyNumberPattern.Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static string NormalizeWaId(string value) => new(value.Where(char.IsDigit).ToArray());

    private string Welcome(SenderProfile? sender) =>
        sender is { Policies.Count: > 0 }
            ? KatoReplies.WelcomeBack(sender.FirstName)
            : KatoReplies.Introduction(sender?.FirstName ?? contactFirstName, VoiceNotesSupported);

    private static string HoldingReply(SenderProfile? sender) => KatoReplies.CheckingAnswer(sender?.FirstName);

    private static string DefaultEscalationMessage() => KatoReplies.CannotHelpDirectly();

    /// <summary>Kato mentions voice notes only when a voice note actually gets understood.</summary>
    private bool VoiceNotesSupported => voiceNotesAvailable;


    // ---- Language: understand the customer at the door, answer them in their own language ----

    private enum LanguageStep
    {
        Proceed,
        AskChoice,
        ChoiceAnswered,
    }

    /// <summary>Reply is the language to answer in. Inbound is the language to translate this one
    /// message from. They differ when someone who prefers Luganda types "yes" in English.</summary>
    private sealed record ConversationLanguage(SupportedLanguageCode Reply, SupportedLanguageCode Inbound, LanguageStep Step);

    private sealed record Understood(string English, TranslationStatus Status, bool NeedsAPerson, TranslationFailureReason? Failure);

    private sealed record LocalisedReply(string Text, TranslationStatus Status);

    /// <summary>The customer's own words and their language, kept on a claim opened from a translation.</summary>
    private sealed record CustomerOwnWords(string Text, string LanguageIso);

    private static readonly ConversationLanguage English = new(SupportedLanguageCode.En, SupportedLanguageCode.En, LanguageStep.Proceed);

    private const string MachineTranslatedNote = "This message was translated by a computer.";

    private bool LanguagesEnabled =>
        translationService is not null
        && languageResolution is not null
        && languageDetector is not null
        && languageSettings is { EnabledLanguages.Count: > 0 };

    private async Task<ConversationLanguage> ResolveConversationLanguageAsync(
        TenantScope scope, string waId, string original, CustomerConversationTurn? previousTurn, CancellationToken cancellationToken)
    {
        if (!LanguagesEnabled)
        {
            return English;
        }

        var previousWasChoice = previousTurn?.Outcome == CustomerAssistantOutcome.AwaitingLanguageChoice;
        var resolution = await languageResolution!.ResolveAsync(scope, waId, original, previousWasChoice, cancellationToken);
        var enabled = languageSettings!.EnabledLanguages;
        var detected = languageDetector!.Detect(original);

        if (previousWasChoice && LanguageChoicePrompt.ParseAnswer(original) is not null)
        {
            var chosen = enabled.Contains(resolution.Language) ? resolution.Language : SupportedLanguageCode.En;
            return new ConversationLanguage(chosen, chosen, LanguageStep.ChoiceAnswered);
        }

        if (resolution.Source == LanguageResolutionSource.ExplicitRequest)
        {
            var requested = enabled.Contains(resolution.Language) ? resolution.Language : SupportedLanguageCode.En;
            var onlyAskedForALanguage = original.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 6
                && intentClassifier.Classify(original) == CustomerIntent.Unknown;
            return onlyAskedForALanguage
                ? new ConversationLanguage(requested, requested, LanguageStep.ChoiceAnswered)
                : new ConversationLanguage(requested, InboundLanguageFor(original, detected, requested, enabled), LanguageStep.Proceed);
        }

        // Answer in the language the customer just used, the way a person would. Someone who wrote
        // "let's speak English" yesterday and sends a Luganda voice note today wants Luganda back.
        var wroteInLocalLanguage = detected.Language != SupportedLanguageCode.En
            && detected.Confidence > 0
            && enabled.Contains(detected.Language);
        if (wroteInLocalLanguage)
        {
            if (resolution.Language != detected.Language || resolution.Source != LanguageResolutionSource.RememberedPreference)
            {
                await languageResolution.RememberAsync(scope, waId, detected.Language, cancellationToken);
            }

            return new ConversationLanguage(detected.Language, detected.Language, LanguageStep.Proceed);
        }

        if (resolution.Source == LanguageResolutionSource.PromptedChoice)
        {
            return English;
        }

        var reply = enabled.Contains(resolution.Language) ? resolution.Language : SupportedLanguageCode.En;
        // A full English sentence gets English back, whatever was preferred before.
        if (reply != SupportedLanguageCode.En
            && detected.Confidence > 0
            && original.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
        {
            reply = SupportedLanguageCode.En;
            await languageResolution.RememberAsync(scope, waId, reply, cancellationToken);
        }

        return new ConversationLanguage(reply, InboundLanguageFor(original, detected, reply, enabled), LanguageStep.Proceed);
    }

    /// <summary>The language to translate one message from. The message itself decides, not the
    /// customer's preference: someone who prefers Luganda still types "yes", or a policy number, in
    /// English. A message that shows no sign of either is assumed to be in the preferred language.</summary>
    private static SupportedLanguageCode InboundLanguageFor(
        string original, LanguageResolution detected, SupportedLanguageCode preferred, IReadOnlyCollection<SupportedLanguageCode> enabled)
    {
        // Only a positive sign of a local language sends a message to translation. A message with
        // none is English, even when the customer prefers another language: treating "what does
        // Motor insurance cover" as Luganda sent plain English through a translator and refused it.
        if (detected.Language != SupportedLanguageCode.En && detected.Confidence > 0 && enabled.Contains(detected.Language))
        {
            return detected.Language;
        }

        return SupportedLanguageCode.En;
    }

    private async Task<Understood> UnderstandAsync(TenantScope scope, string original, SupportedLanguageCode inbound, CancellationToken cancellationToken)
    {
        if (inbound == SupportedLanguageCode.En || !HasWordsToTranslate(original))
        {
            return new Understood(original, TranslationStatus.NotNeeded, false, null);
        }

        // A bare greeting is a greeting in any language; the engine once turned "Gyebale ko" into
        // "Thank you", and Kato answered "you're welcome".
        if (ConversationContinuation.IsGreeting(original))
        {
            return new Understood("Hello (a greeting)", TranslationStatus.Translated, false, null);
        }

        var outcome = await translationService!.TranslateAsync(original, inbound, SupportedLanguageCode.En, scope, cancellationToken);
        if (outcome.Translated)
        {
            return new Understood(outcome.Text, TranslationStatus.Translated, false, null);
        }

        // The model handed the text back unchanged: it was already English, or there was nothing in
        // it to translate. Carry on with it as it is. Any other failure means the message could not
        // be understood, and guessing an intent from it is exactly what must never happen.
        return outcome.Failure == TranslationFailureReason.Unchanged
            ? new Understood(original, TranslationStatus.NotNeeded, false, null)
            : new Understood(original, TranslationStatus.FellBackToEnglish, true, outcome.Failure);
    }

    /// <summary>A message that is only a policy number, a digit or punctuation has nothing to translate,
    /// and sending it to the model returns it unchanged, which the checks would then reject.</summary>
    private static bool HasWordsToTranslate(string text)
    {
        var (masked, _) = ProtectedTokenMasker.Mask(text);
        return System.Text.RegularExpressions.Regex.Replace(masked, @"\[TKN\d+\]", string.Empty).Any(char.IsLetter);
    }

    /// <summary>Puts an English reply into the customer's language, or leaves it in English and says
    /// why. Only ever a machine translation, so it is only ever done where the workspace allows
    /// unreviewed translations, it carries a note saying a computer did it, and a failure of any kind
    /// sends the English rather than something unchecked.</summary>
    private async Task<LocalisedReply> LocaliseAsync(TenantScope scope, string english, SupportedLanguageCode reply, string? firstName, CancellationToken cancellationToken, bool noteAlreadyShown = false)
    {
        if (reply == SupportedLanguageCode.En)
        {
            return new LocalisedReply(english, TranslationStatus.NotNeeded);
        }

        if (!LanguagesEnabled || languageSettings is not { AllowUnreviewedTranslations: true })
        {
            return new LocalisedReply(english, TranslationStatus.FellBackToEnglish);
        }

        // The customer's name must come through untouched, and so must the insurance words that are
        // better left in English for this language (see AssistantGlossary).
        var literals = new List<string>();
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            literals.Add(firstName);
        }

        literals.AddRange(AssistantGlossary.KeptInEnglish(reply));
        // Product names are names: "Small Business Shop Cover" once came back as "picture of the shop".
        literals.AddRange(MicroinsuranceCatalog.All.SelectMany(p => new[] { p.Name, p.Name.Replace("Nile Shield ", string.Empty, StringComparison.Ordinal), p.ProductLine }));
        literals.Add("Nile Shield");
        var outcome = await translationService!.TranslateAsync(english, SupportedLanguageCode.En, reply, scope, cancellationToken, literals);
        if (!outcome.Translated)
        {
            return new LocalisedReply(english, TranslationStatus.FellBackToEnglish);
        }

        // Said once per conversation, not on every reply: the customer has been told.
        if (noteAlreadyShown)
        {
            return new LocalisedReply(outcome.Text, TranslationStatus.Translated);
        }

        var note = await translationService.TranslateAsync(MachineTranslatedNote, SupportedLanguageCode.En, reply, scope, cancellationToken);
        return new LocalisedReply($"{outcome.Text}\n\n({(note.Translated ? note.Text : MachineTranslatedNote)})", TranslationStatus.Translated);
    }

    /// <summary>The language code a reply is labelled with: the customer's language only when the
    /// text really is in it. English that went out because a translation could not be shown is
    /// labelled English.</summary>
    private Task SaveOutboundAsync(
        TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string replyText, Guid? caseId,
        WhatsAppProviderSendResult result, string idempotencyKey, WhatsAppContentType contentType, CancellationToken cancellationToken) =>
        whatsAppMessageRepository.SaveAsync(new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            ThreadId = threadId,
            BusinessPhoneNumberId = inbound.BusinessPhoneNumberId,
            ContactWaId = waId,
            Direction = WhatsAppMessageDirection.Outbound,
            ContentType = contentType,
            // For a voice note, the words that were spoken, so staff can read what the customer heard.
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

    /// <summary>What to say aloud: the reply without links and without the bracketed
    /// machine-translation note, which the voice itself makes plain enough.</summary>
    public static string? SpokenText(string replyText)
    {
        var paragraphs = replyText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(paragraph => !(paragraph.StartsWith('(') && paragraph.EndsWith(')')))
            .Select(paragraph => string.Join(' ', paragraph
                .Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !word.Contains("http", StringComparison.OrdinalIgnoreCase))).TrimEnd(':', ' '))
            .Where(paragraph => paragraph.Length > 0)
            .Select(paragraph => paragraph[^1] is '.' or '?' or '!' ? paragraph : paragraph + ".");
        var spoken = string.Join(" ", paragraphs).Trim();
        return spoken.Length == 0 ? null : spoken;
    }

    private static SupportedLanguageCode LanguageFromIso(string iso) => iso switch
    {
        "lg" => SupportedLanguageCode.Lg,
        "sw" => SupportedLanguageCode.Sw,
        _ => SupportedLanguageCode.En,
    };

    private static string LanguageIsoFor(TranslationStatus status, SupportedLanguageCode language) =>
        status == TranslationStatus.Translated ? SupportedLanguage.For(language).Iso : "en";

    private static string LanguageEvidence(ConversationLanguage language, TranslationStatus inbound, TranslationStatus? outbound) =>
        language.Reply == SupportedLanguageCode.En && inbound == TranslationStatus.NotNeeded
            ? string.Empty
            : $", language {SupportedLanguage.For(language.Reply).Iso}, inbound {inbound}, outbound {outbound?.ToString() ?? "none"}";

    /// <summary>Asks which language to use, or greets the customer in the one they just chose.</summary>
    private async Task HandleLanguageStepAsync(
        TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string original,
        ConversationLanguage language, SenderProfile? sender, string correlationId, CancellationToken cancellationToken,
        bool firstContact = false)
    {
        var asking = language.Step == LanguageStep.AskChoice;
        var english = asking
            ? $"{(firstContact ? Welcome(sender) + " " : "")}I can reply in English, Luganda or Kiswahili. {LanguageChoicePrompt.Text}"
            : Welcome(sender);
        var localised = asking
            ? new LocalisedReply(english, TranslationStatus.NotNeeded)
            : await LocaliseAsync(scope, english, language.Reply, sender?.FirstName, cancellationToken);

        await SendReplyAsync(scope, inbound, threadId, waId, localised.Text, null, cancellationToken, asking ? "en" : LanguageIsoFor(localised.Status, language.Reply));
        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = original,
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = english,
            Outcome = asking ? CustomerAssistantOutcome.AwaitingLanguageChoice : CustomerAssistantOutcome.AwaitingMoreInformation,
            CustomerLanguage = language.Reply == SupportedLanguageCode.En ? null : SupportedLanguage.For(language.Reply).Iso,
            OutboundLocalisedText = localised.Status == TranslationStatus.Translated ? localised.Text : null,
            OutboundTranslationStatus = localised.Status,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, SystemActorId, ActorType.Integration,
                asking ? "customer_assistant.language_asked" : "customer_assistant.language_chosen", DateTimeOffset.UtcNow,
                null, null, [], "CustomerAssistant", null,
                "recorded", null,
                $"WhatsApp customer assistant {(asking ? "asked which language to use" : $"switched to {SupportedLanguage.For(language.Reply).Iso}")}. Message body omitted from evidence.",
                correlationId),
            cancellationToken);
    }

    /// <summary>The customer wrote in a language we could not turn into English. A person gets the
    /// original words; nothing is guessed from a failed translation.</summary>
    private async Task EscalateUntranslatedAsync(
        TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string original,
        ConversationLanguage language, SenderProfile? sender, TranslationFailureReason? failure, string correlationId, CancellationToken cancellationToken)
    {
        var english = DefaultEscalationMessage();
        var localised = await LocaliseAsync(scope, english, language.Reply, sender?.FirstName, cancellationToken);
        await SendReplyAsync(scope, inbound, threadId, waId, localised.Text, null, cancellationToken, LanguageIsoFor(localised.Status, language.Reply));

        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = original,
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = english,
            Outcome = CustomerAssistantOutcome.Escalated,
            CustomerLanguage = language.Reply == SupportedLanguageCode.En ? null : SupportedLanguage.For(language.Reply).Iso,
            OutboundLocalisedText = localised.Status == TranslationStatus.Translated ? localised.Text : null,
            InboundTranslationStatus = TranslationStatus.FellBackToEnglish,
            OutboundTranslationStatus = localised.Status,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, SystemActorId, ActorType.Integration,
                "customer_assistant.translation_failed", DateTimeOffset.UtcNow,
                null, null, [], "CustomerAssistant", null,
                "failed", null,
                $"WhatsApp customer assistant could not translate a {SupportedLanguage.For(language.Inbound).Iso} message into English ({failure}) and escalated it to a person with the original words. Message body omitted from evidence.",
                correlationId),
            cancellationToken);
    }

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

        public string? PendingClaimDescription { get; init; }
        public string? PendingClaimOriginalText { get; init; }
        public string? PendingClaimOriginalLanguage { get; init; }
        public Guid? OfferedNewIncidentForCaseId { get; init; }
        public string? Occupation { get; init; }
        public bool FromAgent { get; init; }
    }
}

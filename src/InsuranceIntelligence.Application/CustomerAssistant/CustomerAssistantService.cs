using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Routes an inbound WhatsApp text message to a real outcome: a grounded, cited answer (only when
/// the five-condition gate permits auto-send — otherwise drafted and logged, never sent), a new
/// provisional claim, a sales lead, or a human escalation. Blueprint §8.3.
///
/// This is a system-actor service, not a staff-facing one: it is invoked from the unauthenticated
/// WhatsApp webhook, where there is no signed-in user and no ITenantContextAccessor/
/// IPermissionService context to resolve — Meta calls the webhook directly, verified by HMAC
/// signature, not a bearer token. Every write this service makes goes through the same
/// system-actor pattern already established by WhatsAppMessagingService.WriteIntegrationAuditAsync
/// ("system:whatsapp-webhook", ActorType.Integration) and ClaimantAuthenticationEndpoints
/// ("system:claimant-magic-link") — not through the permission-gated CaseIntakeService callers or
/// SalesLeadCommandService, both of which assume a real staff user and would throw here.
///
/// Outbound sends go through IWhatsAppOutboundProvider directly, not
/// IWhatsAppMessagingService.SendAsync — that higher-level service requires a case-linked,
/// previously-verified thread and two staff permissions, which is exactly right for an officer
/// replying to an existing case, and exactly wrong for the platform replying to a first inbound
/// message with no case yet. See docs/01-strategy-requirements-roadmap/
/// IIP_HACKATHON_READINESS_BLUEPRINT.md §14 Phase 4 for this finding in full.
/// </summary>
public interface ICustomerAssistantService
{
    /// <summary>autoReplyEnabled is the tenant/deployment opt-in for auto-sending grounded,
    /// cited GeneralQuestion answers (blueprint §8.3 gate condition 5 — AssistantAutoReplyEnabled
    /// in InfrastructureOptions). Passed in by the caller rather than read from InfrastructureOptions
    /// directly: InsuranceIntelligence.Application has no reference to InsuranceIntelligence.Infrastructure
    /// (correctly — Infrastructure depends on Application, not the reverse), so this setting has to
    /// cross that boundary as a parameter, not a dependency.</summary>
    Task HandleInboundMessagesAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> messages,
        string correlationId,
        bool autoReplyEnabled,
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
    IAuditRepository auditRepository) : ICustomerAssistantService
{
    private const string SystemActorId = "system:customer-assistant";
    private const string GroundingAgentKey = "policy-intelligence"; // the same grounded-answer capability, reused for customers

    // The demo-seed policy number format (NSA/{product}/{year}/{seq}) plus a looser generic
    // fallback — a real customer will not always type the format perfectly, and this must not be
    // the only path into the flow, but it is the primary one this pass implements.
    private static readonly Regex PolicyNumberPattern = new(@"[A-Z]{2,4}/[A-Z]{2}/\d{4}/\d{4,6}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task HandleInboundMessagesAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> messages,
        string correlationId,
        bool autoReplyEnabled,
        CancellationToken cancellationToken = default)
    {
        foreach (var inbound in messages)
        {
            // Media-only messages (documents, images) are handled by the existing
            // WhatsAppInboundDocumentIngestionService path — this service only reasons about text.
            if (inbound.ContentType != WhatsAppContentType.Text || string.IsNullOrWhiteSpace(inbound.CustomerSafeBody))
            {
                continue;
            }

            await HandleOneMessageAsync(scope, inbound, correlationId, autoReplyEnabled, cancellationToken);
        }
    }

    private async Task HandleOneMessageAsync(TenantScope scope, WhatsAppInboundMessage inbound, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken)
    {
        var text = inbound.CustomerSafeBody.Trim();
        var waId = NormalizeWaId(inbound.SenderWaId);
        var threadId = WhatsAppMessagingService.BuildThreadId(inbound.BusinessPhoneNumberId, waId);
        var intent = intentClassifier.Classify(text);

        var (reply, outcome, citations, caseId, leadId) = intent switch
        {
            CustomerIntent.GeneralQuestion => await HandleGeneralQuestionAsync(scope, text, waId, correlationId, autoReplyEnabled, cancellationToken),
            CustomerIntent.NewClaim => await HandleNewClaimAsync(scope, text, waId, inbound.BusinessPhoneNumberId, correlationId, cancellationToken),
            CustomerIntent.ExistingClaimStatus => await HandleExistingClaimStatusAsync(scope, text, waId, cancellationToken),
            CustomerIntent.ProductInterest => await HandleProductInterestAsync(scope, text, waId, cancellationToken),
            CustomerIntent.HumanRequested => await HandleHumanRequestedAsync(scope, text, waId, cancellationToken),
            _ => (DefaultEscalationMessage(), CustomerAssistantOutcome.Escalated, Array.Empty<string>(), (Guid?)null, (string?)null),
        };

        // QueuedForHumanApproval means the five-condition gate (CustomerAutoReplyGate) declined to
        // auto-send an AI-composed answer — it must be logged for a human to review, never sent to
        // the customer on our behalf. Every other non-null reply here is a fixed, non-disclosing
        // template acknowledgement (a claim reference, "please share your policy number", a lead
        // confirmation) — a structurally different, lower-risk category the gate does not govern.
        if (reply is not null && outcome != CustomerAssistantOutcome.QueuedForHumanApproval)
        {
            await SendReplyAsync(scope, inbound, threadId, waId, reply, caseId, cancellationToken);
        }

        await conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = text,
            ClassifiedIntent = intent,
            OutboundReplyText = reply,
            Outcome = outcome,
            CitationTitles = citations,
            CaseId = caseId,
            LeadId = leadId,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        }, cancellationToken);

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, caseId, null, SystemActorId, ActorType.Integration,
                $"customer_assistant.{outcome.ToString().ToLowerInvariant()}", DateTimeOffset.UtcNow,
                null, null, leadId is null ? [] : [leadId], "CustomerAssistant", null,
                "recorded", null,
                $"WhatsApp customer assistant classified this message as {intent} and took action {outcome}. Message body omitted from evidence.",
                correlationId),
            cancellationToken);
    }

    private async Task<(string? Reply, CustomerAssistantOutcome Outcome, IReadOnlyCollection<string> Citations, Guid? CaseId, string? LeadId)> HandleGeneralQuestionAsync(
        TenantScope scope, string text, string waId, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken)
    {
        var question = new AssistantQuestion(scope, null, text, null, "Customer", SystemActorId, correlationId);
        var retrieval = await retrievalProvider.RetrieveAsync(question, cancellationToken);

        if (retrieval.Chunks.Count == 0)
        {
            // Nothing approved to ground an answer in — never guess from model memory for a customer.
            var fallback = "I don't have an approved answer for that yet — I've passed this to our team and someone will follow up with you.";
            return (fallback, CustomerAssistantOutcome.QueuedForHumanApproval, [], null, null);
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

        return permitted
            ? (answer.AnswerText, CustomerAssistantOutcome.AutoReplied, citationTitles, (Guid?)null, (string?)null)
            : (answer.AnswerText, CustomerAssistantOutcome.QueuedForHumanApproval, citationTitles, (Guid?)null, (string?)null);
    }

    private async Task<(string? Reply, CustomerAssistantOutcome Outcome, IReadOnlyCollection<string> Citations, Guid? CaseId, string? LeadId)> HandleNewClaimAsync(
        TenantScope scope, string text, string waId, string businessPhoneNumberId, string correlationId, CancellationToken cancellationToken)
    {
        var policyNumber = ExtractPolicyNumber(text);
        if (policyNumber is null)
        {
            return ("I'm sorry to hear that. To start your claim, please reply with your policy number.",
                CustomerAssistantOutcome.AwaitingMoreInformation, [], null, null);
        }

        var policy = await policyRepository.GetByPolicyNumberAsync(policyNumber, scope, cancellationToken);
        if (policy is null)
        {
            return ($"I couldn't find a policy matching {policyNumber}. Please double-check the number, or reply \"human\" to speak with our team.",
                CustomerAssistantOutcome.Declined, [], null, null);
        }

        var customer = string.IsNullOrWhiteSpace(policy.CustomerId) ? null : await customerRepository.GetAsync(policy.CustomerId, scope, cancellationToken);
        var caseIntake = new CaseIntakeService(caseRepository, auditRepository);
        var claimCase = await caseIntake.CreateCaseAsync(new CreateCaseCommand(
            scope,
            CustomerName: customer?.FullName ?? "WhatsApp customer",
            CustomerEmail: customer?.Email,
            CustomerPhone: customer?.Phone ?? $"+{waId}",
            PolicyNumber: policy.PolicyNumber,
            ProductLine: policy.ProductLine,
            ClaimType: "Unclassified — reported via WhatsApp",
            Channel: "WhatsApp",
            IncidentDate: DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date),
            IncidentDescription: text,
            CreatedByUserId: SystemActorId,
            CorrelationId: correlationId,
            RequiresConfirmation: true), cancellationToken);

        var reference = claimCase.CaseId.ToString()[..8].ToUpperInvariant();
        return ($"Thank you — we've logged your claim. Your reference is {reference}. A claims officer will confirm the details and follow up with you shortly.",
            CustomerAssistantOutcome.CaseOpened, [], claimCase.CaseId, null);
    }

    private async Task<(string? Reply, CustomerAssistantOutcome Outcome, IReadOnlyCollection<string> Citations, Guid? CaseId, string? LeadId)> HandleExistingClaimStatusAsync(
        TenantScope scope, string text, string waId, CancellationToken cancellationToken)
    {
        var policyNumber = ExtractPolicyNumber(text);
        if (policyNumber is null)
        {
            return ("To check your claim status, please reply with your policy number so I can verify it's you.",
                CustomerAssistantOutcome.AwaitingMoreInformation, [], null, null);
        }

        // The policy-number match IS the identity verification (blueprint §8.3 WO 4.4) — a bare
        // WhatsApp number is never, on its own, enough to disclose anything about a case.
        var policy = await policyRepository.GetByPolicyNumberAsync(policyNumber, scope, cancellationToken);
        if (policy is null)
        {
            return ($"I couldn't verify a policy matching {policyNumber}.", CustomerAssistantOutcome.Declined, [], null, null);
        }

        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var matchingCase = cases
            .Where(c => string.Equals(c.PolicyNumber, policy.PolicyNumber, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        if (matchingCase is null)
        {
            return ($"I verified policy {policyNumber}, but there's no claim on file for it yet. Reply with details of your incident to start one.",
                CustomerAssistantOutcome.Declined, [], null, null);
        }

        return ($"Your claim on policy {policyNumber} is currently: {matchingCase.Status}. For full details, a claims officer can follow up with you.",
            CustomerAssistantOutcome.AutoReplied, [], matchingCase.CaseId, null);
    }

    private async Task<(string? Reply, CustomerAssistantOutcome Outcome, IReadOnlyCollection<string> Citations, Guid? CaseId, string? LeadId)> HandleProductInterestAsync(
        TenantScope scope, string text, string waId, CancellationToken cancellationToken)
    {
        var leadId = await CreateLeadAsync(scope, text, waId, "WhatsApp", cancellationToken);
        return ("Thanks for your interest! I've passed your details to our sales team — someone will reach out with options that fit your needs.",
            CustomerAssistantOutcome.LeadCreated, [], null, leadId);
    }

    private async Task<(string? Reply, CustomerAssistantOutcome Outcome, IReadOnlyCollection<string> Citations, Guid? CaseId, string? LeadId)> HandleHumanRequestedAsync(
        TenantScope scope, string text, string waId, CancellationToken cancellationToken)
    {
        var leadId = await CreateLeadAsync(scope, text, waId, "WhatsApp - human requested", cancellationToken);
        return ("Of course — I've flagged this for a member of our team to follow up with you directly.",
            CustomerAssistantOutcome.Escalated, [], null, leadId);
    }

    private async Task<string> CreateLeadAsync(TenantScope scope, string text, string waId, string leadSource, CancellationToken cancellationToken)
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
            CustomerName: "WhatsApp customer",
            ContactInfo: $"+{waId}",
            CreatedAt: now,
            CreatedBy: SystemActorId);

        await salesLeadWriteRepository.CreateAsync(scope.TenantId, scope.WorkspaceId, lead, cancellationToken);
        return leadId;
    }

    private async Task SendReplyAsync(TenantScope scope, WhatsAppInboundMessage inbound, string threadId, string waId, string replyText, Guid? caseId, CancellationToken cancellationToken)
    {
        if (!outboundProvider.IsConfigured)
        {
            return; // Honest no-op — never fabricate a send that didn't happen.
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

    private static string DefaultEscalationMessage() =>
        "I'm not able to help with that directly — I've passed this to our team and someone will follow up with you.";
}

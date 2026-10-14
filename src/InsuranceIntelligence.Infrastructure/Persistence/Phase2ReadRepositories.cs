using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using DomainAuditEvent = InsuranceIntelligence.Domain.Audit.AuditEvent;
using CustomerAuditRow = InsuranceIntelligence.Application.Customers.AuditEventRowDto;

namespace InsuranceIntelligence.Infrastructure.Persistence;

// Read-side adapters that back the Phase 2 query services. Follows the same
// "materialize tenant-scoped rows, then deserialize the JSON payload in memory"
// approach used by EfClaimsReadRepository, the persisted aggregates are stored as
// scoped JSON, so filtering happens after deserialization.

/// <summary>
/// Real audit read model over the AuditEvents table.
/// </summary>
public sealed class EfAuditReadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAuditReadRepository
{
    public async Task<IReadOnlyList<AuditEventReadModel>> ListEventsAsync(
        string tenantId,
        string workspaceId,
        AuditEventListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var scope = TenantScope.Create(tenantId, workspaceId);

        var payloads = await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
            .Select(record => record.PayloadJson)
            .ToArrayAsync(cancellationToken);

        var events = payloads
            .Select(Deserialize<DomainAuditEvent>)
            .Select(ToReadModel)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            events = events.Where(item => string.Equals(item.EventType, query.EventType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
        {
            events = events.Where(item => string.Equals(item.ActorUserId, query.ActorUserId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            events = events.Where(item => string.Equals(item.TargetType, query.TargetType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TargetId))
        {
            events = events.Where(item => string.Equals(item.TargetId, query.TargetId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FromUtc is not null)
        {
            events = events.Where(item => item.OccurredAt >= query.FromUtc.Value);
        }

        if (query.ToUtc is not null)
        {
            events = events.Where(item => item.OccurredAt <= query.ToUtc.Value);
        }

        return events
            .OrderByDescending(item => item.OccurredAt)
            .Skip((Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize))
            .Take(Math.Max(1, query.PageSize))
            .ToArray();
    }

    private static AuditEventReadModel ToReadModel(DomainAuditEvent auditEvent)
    {
        var (targetType, targetId) = ResolveTarget(auditEvent);
        return new AuditEventReadModel(
            auditEvent.EventId.ToString(),
            auditEvent.ActionName,
            auditEvent.UserId,
            targetType,
            targetId,
            auditEvent.AuditReason ?? auditEvent.ActionName,
            auditEvent.Timestamp,
            auditEvent.CorrelationId);
    }

    private static (string? TargetType, string? TargetId) ResolveTarget(DomainAuditEvent auditEvent)
    {
        if (!string.IsNullOrWhiteSpace(auditEvent.DocumentId))
        {
            return ("document", auditEvent.DocumentId);
        }

        return auditEvent.CaseId is { } caseId
            ? ("case", caseId.ToString())
            : (null, null);
    }
}

/// <summary>
/// Real Customer 360 aggregation across the customer's identity, policies, claims,
/// communications, documents, and audit trail. Sources that do not yet exist in the
/// data model (consent, payments, sales opportunities) return empty rather than
/// simulated data.
/// </summary>
public sealed class EfCustomerReadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICustomerReadRepository
{
    public async Task<CustomerProfileReadModel?> GetCustomerProfileAsync(
        string tenantId,
        string workspaceId,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);

        var customer = (await Scoped(DbContext.Customers.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Customer>)
            .FirstOrDefault(item => string.Equals(item.CustomerId, customerId, StringComparison.OrdinalIgnoreCase));

        if (customer is null)
        {
            return null;
        }

        var policies = (await Scoped(DbContext.Policies.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Policy>)
            .Where(policy => string.Equals(policy.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var claims = (await Scoped(DbContext.Cases.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<ClaimCase>)
            .Where(claim => MatchesCustomer(claim, customer))
            .ToArray();

        var caseIds = claims.Select(claim => claim.CaseId).ToHashSet();

        var interactions = (await Scoped(DbContext.Communications.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Communication>)
            .Where(message =>
                string.Equals(message.CustomerId, customerId, StringComparison.OrdinalIgnoreCase)
                || caseIds.Contains(message.CaseId))
            .OrderByDescending(message => message.CreatedAt)
            .ToArray();

        var documents = caseIds.Count == 0
            ? Array.Empty<CaseDocument>()
            : (await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
                    .Where(record => caseIds.Contains(record.CaseId))
                    .Select(record => record.PayloadJson)
                    .ToArrayAsync(cancellationToken))
                .Select(Deserialize<CaseDocument>)
                .OrderByDescending(document => document.UploadedAt)
                .ToArray();

        var auditHistory = caseIds.Count == 0
            ? Array.Empty<CustomerAuditRow>()
            : (await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
                    .Where(record => record.CaseId != null && caseIds.Contains(record.CaseId.Value))
                    .Select(record => record.PayloadJson)
                    .ToArrayAsync(cancellationToken))
                .Select(Deserialize<DomainAuditEvent>)
                .OrderByDescending(auditEvent => auditEvent.Timestamp)
                .Select(ToCustomerAuditRow)
                .ToArray();

        return new CustomerProfileReadModel(
            customer.CustomerId,
            BuildIdentity(customer),
            policies.Select(BuildPolicy).ToArray(),
            claims.Select(BuildClaim).ToArray(),
            interactions.Select(BuildInteraction).ToArray(),
            documents.Select(BuildDocument).ToArray(),
            new Dictionary<string, string>(),
            Array.Empty<IReadOnlyDictionary<string, string>>(),
            Array.Empty<IReadOnlyDictionary<string, string>>(),
            auditHistory);
    }

    public async Task<IReadOnlyList<CustomerListItemDto>> ListCustomersAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);

        var customers = (await Scoped(DbContext.Customers.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Customer>)
            .ToArray();

        if (customers.Length == 0)
        {
            return Array.Empty<CustomerListItemDto>();
        }

        var claims = (await Scoped(DbContext.Cases.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<ClaimCase>)
            .ToArray();

        // Open claims indexed once by email, phone and name, so each customer's count is a lookup and not a pass over
        // every claim (customers times claims gets slow quickly on a small plan). The same matching rules as
        // MatchesCustomer, and a claim that matches by both email and phone is still counted once.
        var open = claims.Where(IsOpenClaim).ToArray();
        var openByEmail = open.Where(claim => !string.IsNullOrWhiteSpace(claim.CustomerEmail))
            .ToLookup(claim => claim.CustomerEmail!, StringComparer.OrdinalIgnoreCase);
        var openByPhone = open.Where(claim => !string.IsNullOrWhiteSpace(claim.CustomerPhone))
            .ToLookup(claim => NormalizePhone(claim.CustomerPhone!), StringComparer.Ordinal);
        var openByName = open.Where(claim => claim.CustomerName is not null)
            .ToLookup(claim => claim.CustomerName, StringComparer.OrdinalIgnoreCase);

        int OpenClaimCount(Customer customer)
        {
            var hasEmail = !string.IsNullOrWhiteSpace(customer.Email);
            var hasPhone = !string.IsNullOrWhiteSpace(customer.Phone);
            var matched = new HashSet<Guid>();
            if (hasEmail) matched.UnionWith(openByEmail[customer.Email!].Select(claim => claim.CaseId));
            if (hasPhone) matched.UnionWith(openByPhone[NormalizePhone(customer.Phone!)].Select(claim => claim.CaseId));
            if (!hasEmail && !hasPhone && customer.FullName is not null) matched.UnionWith(openByName[customer.FullName].Select(claim => claim.CaseId));
            return matched.Count;
        }

        // The same person can be on file more than once (a demonstration list loaded again, or a customer
        // added by hand and later by WhatsApp). One row per person: the email if there is one, else the
        // phone number, else the record itself. The earliest-created record is kept so links stay stable.
        return customers
            .GroupBy(customer => IdentityKey(customer), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(customer => customer.CustomerId, StringComparer.Ordinal).First())
            .OrderBy(customer => customer.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(customer => new CustomerListItemDto(
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone,
                OpenClaimCount(customer)))
            .ToArray();
    }

    private static string IdentityKey(Customer customer)
    {
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            return $"email:{customer.Email.Trim()}";
        }

        var phone = NormalizePhone(customer.Phone ?? string.Empty);
        return phone.Length >= 9 ? $"phone:{phone}" : $"id:{customer.CustomerId}";
    }

    private static bool IsOpenClaim(ClaimCase claim) =>
        claim.Status is not (CaseStatus.Settled or CaseStatus.Closed);

    // ClaimCase stores denormalized customer contact fields (no CustomerId FK). Correlate on
    // email/phone when the customer has them; only fall back to name when no contact info exists,
    // so one customer's claims never leak onto another with the same name.
    private static bool MatchesCustomer(ClaimCase claim, Customer customer)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(customer.Email);
        var hasPhone = !string.IsNullOrWhiteSpace(customer.Phone);

        if (hasEmail
            && !string.IsNullOrWhiteSpace(claim.CustomerEmail)
            && string.Equals(customer.Email, claim.CustomerEmail, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (hasPhone
            && !string.IsNullOrWhiteSpace(claim.CustomerPhone)
            && string.Equals(NormalizePhone(customer.Phone!), NormalizePhone(claim.CustomerPhone!), StringComparison.Ordinal))
        {
            return true;
        }

        return !hasEmail
            && !hasPhone
            && string.Equals(customer.FullName, claim.CustomerName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static IReadOnlyDictionary<string, string> BuildIdentity(Customer customer)
    {
        var identity = new Dictionary<string, string>
        {
            ["customerId"] = customer.CustomerId,
            ["fullName"] = customer.FullName
        };

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            identity["email"] = customer.Email;
        }

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            identity["phone"] = customer.Phone;
        }

        return identity;
    }

    private static IReadOnlyDictionary<string, string> BuildPolicy(Policy policy)
    {
        var row = new Dictionary<string, string>
        {
            ["policyId"] = policy.PolicyId,
            ["policyNumber"] = policy.PolicyNumber,
            ["productLine"] = policy.ProductLine
        };

        if (!string.IsNullOrWhiteSpace(policy.CoverType))
        {
            row["coverType"] = policy.CoverType;
        }

        if (policy.EffectiveFrom is { } from)
        {
            row["effectiveFrom"] = from.ToString("O");
        }

        if (policy.EffectiveTo is { } to)
        {
            row["effectiveTo"] = to.ToString("O");
        }

        return row;
    }

    private static IReadOnlyDictionary<string, string> BuildClaim(ClaimCase claim) =>
        new Dictionary<string, string>
        {
            ["caseId"] = claim.CaseId.ToString(),
            ["claimType"] = claim.ClaimType,
            ["productLine"] = claim.ProductLine,
            ["policyNumber"] = claim.PolicyNumber,
            ["status"] = claim.Status.ToString(),
            ["completenessScore"] = claim.CompletenessScore.ToString("0.##"),
            ["riskLevel"] = claim.RiskLevel,
            ["channel"] = claim.Channel,
            ["incidentDate"] = claim.IncidentDate.ToString("O"),
            ["createdAt"] = claim.CreatedAt.ToString("O")
        };

    private static IReadOnlyDictionary<string, string> BuildInteraction(Communication message) =>
        new Dictionary<string, string>
        {
            ["communicationId"] = message.CommunicationId.ToString(),
            ["caseId"] = message.CaseId.ToString(),
            ["channel"] = message.Channel.ToString(),
            ["direction"] = message.Direction,
            ["deliveryStatus"] = message.DeliveryStatus,
            ["messageSnippet"] = Snippet(message.MessageBody),
            ["createdAt"] = message.CreatedAt.ToString("O")
        };

    private static IReadOnlyDictionary<string, string> BuildDocument(CaseDocument document) =>
        new Dictionary<string, string>
        {
            ["documentId"] = document.DocumentId.ToString(),
            ["caseId"] = document.CaseId.ToString(),
            ["fileName"] = document.OriginalFileName,
            ["documentType"] = document.DocumentType.ToString(),
            ["processingStatus"] = document.ProcessingStatus.ToString(),
            ["uploadedAt"] = document.UploadedAt.ToString("O")
        };

    private static CustomerAuditRow ToCustomerAuditRow(DomainAuditEvent auditEvent) =>
        new(
            auditEvent.ActionName,
            auditEvent.ActorType.ToString(),
            auditEvent.UserId,
            "case",
            auditEvent.CaseId?.ToString() ?? string.Empty,
            auditEvent.Timestamp.ToString("O"),
            auditEvent.CorrelationId);

    private static string Snippet(string body) =>
        body.Length <= 160 ? body : body[..160];
}

/// <summary>
/// Tenant-scoped channel overview projected from durable WhatsApp messages and inbound email,
/// every channel that can land a claim, so an officer working the inbox sees all of them in one
/// place rather than needing a separate admin screen per channel.
/// </summary>
public sealed class EfChannelReadRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IChannelReadRepository
{
    public async Task<IReadOnlyList<ChannelConversationReadModel>> GetOverviewAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = TenantScope.Create(tenantId, workspaceId);
        var whatsAppMessages = (await Scoped(DbContext.WhatsAppMessages.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<WhatsAppMessage>)
            .ToArray();
        var emailMessages = (await Scoped(DbContext.InboundEmails.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<InboundEmailMessage>)
            .ToArray();
        if (whatsAppMessages.Length == 0 && emailMessages.Length == 0) return [];

        // The customer assistant already classified every inbound WhatsApp message and recorded
        // what it did about it (CustomerConversationTurn). Without reading that back, this screen
        // fell through to "general_service" for every thread and showed WhatsApp's delivery state
        // as though it were a work state, so an officer could not see which conversations were
        // new claims, and a reply held for human approval looked identical to one already sent.
        var turnsByThread = (await Scoped(DbContext.CustomerConversationTurns.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<CustomerConversationTurn>)
            .GroupBy(turn => turn.ThreadId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        // Who is answering is a conversation-level state, so it reads every turn; the customer-facing
        // classification below uses the latest one that is not just a control event.
        var latestTurnByThread = turnsByThread.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Where(turn => turn.Outcome is not (CustomerAssistantOutcome.HumanTookOver or CustomerAssistantOutcome.HandedBackToKato or CustomerAssistantOutcome.HumanReplied or CustomerAssistantOutcome.HeldForPerson))
                .OrderByDescending(turn => turn.OccurredAt)
                .FirstOrDefault(),
            StringComparer.Ordinal);

        var cases = (await Scoped(DbContext.Cases.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<ClaimCase>)
            .ToDictionary(claimCase => claimCase.CaseId);

        // A conversation with no linked claim yet (a fresh lead, or a policyholder asking a
        // question before ever filing one) previously always showed a masked WhatsApp contact id
        // instead of a name, even when the number already belongs to a known customer. Matching
        // by phone here (same normalization PhoneNumbers.SameNumber uses) resolves the name
        // whenever we already know who this is.
        var customersByPhoneDigits = (await Scoped(DbContext.Customers.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<Customer>)
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Phone))
            .GroupBy(customer => PhoneNumbers.Digits(customer.Phone))
            .ToDictionary(group => group.Key, group => group.First());

        // A prospect who has not become a customer still has a name worth showing: the one a person
        // typed, an imported list gave, or the customer told Kato. Generic placeholders are skipped.
        var prospectNamesByPhoneDigits = (await Scoped(DbContext.SalesLeads.AsNoTracking(), scope)
                .Select(record => record.PayloadJson)
                .ToArrayAsync(cancellationToken))
            .Select(Deserialize<InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel>)
            .Where(lead => !string.IsNullOrWhiteSpace(lead.CustomerName)
                && !string.Equals(lead.CustomerName, "WhatsApp customer", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(lead.CustomerName, "WhatsApp contact", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(lead.Phone ?? lead.ContactInfo))
            .GroupBy(lead => PhoneNumbers.Digits(lead.Phone ?? lead.ContactInfo))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(lead => lead.CreatedAt).First().CustomerName);

        var whatsAppRows = whatsAppMessages
            .GroupBy(message => message.ThreadId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(message => message.CreatedAt).First())
            .Select(message =>
            {
                var claimCase = message.CaseId is { } caseId && cases.TryGetValue(caseId, out var found) ? found : null;
                var matchedCustomer = claimCase is null
                    ? customersByPhoneDigits.GetValueOrDefault(PhoneNumbers.Digits(message.ContactWaId))
                    : null;
                var turn = latestTurnByThread.GetValueOrDefault(message.ThreadId);
                var language = ConversationLanguageNote.For(turn);
                var controlMode = ConversationControl.ModeOf(turnsByThread.GetValueOrDefault(message.ThreadId) ?? [], DateTimeOffset.UtcNow);
                return new ChannelConversationReadModel(
                    message.ThreadId,
                    claimCase?.CustomerName ?? matchedCustomer?.FullName ?? prospectNamesByPhoneDigits.GetValueOrDefault(PhoneNumbers.Digits(message.ContactWaId)) ?? $"WhatsApp contact …{message.ContactWaId[^Math.Min(4, message.ContactWaId.Length)..]}",
                    ChannelValues.WhatsApp,
                    DetectedIntentFor(turn),
                    RoutingFor(turn, claimCase is not null),
                    0m,
                    StatusFor(turn, message.DeliveryState),
                    claimCase?.AssignedUserId,
                    message.CustomerSafeBody.Length <= 160 ? message.CustomerSafeBody : message.CustomerSafeBody[..160],
                    message.CreatedAt,
                    claimCase?.CustomerPhone ?? message.ContactWaId,
                    language.Language,
                    language.Note)
                {
                    ControlMode = controlMode.ToString(),
                };
            });

        var emailRows = emailMessages.Select(message =>
        {
            var claimCase = message.LinkedCaseId is { } caseId && cases.TryGetValue(caseId, out var found) ? found : null;
            var snippet = $"{message.Subject}. {message.BodyPreview}";
            return new ChannelConversationReadModel(
                message.MessageId.ToString(),
                claimCase?.CustomerName ?? message.Sender,
                ChannelValues.Email,
                EmailIntentFor(message.ProcessingStatus),
                message.LinkedCaseId is not null ? "Open linked claim" : "Process into a claim",
                0m,
                message.ProcessingStatus.ToString(),
                claimCase?.AssignedUserId,
                snippet.Length <= 160 ? snippet : snippet[..160],
                message.ReceivedAt,
                claimCase?.CustomerEmail ?? message.Sender);
        });

        return whatsAppRows
            .Concat(emailRows)
            .OrderByDescending(row => row.LastActivityAt)
            .ToArray();
    }

    /// <summary>There is no intent classifier on the email channel, so the only honest signal is
    /// what processing actually did with the message. Previously any case-linked email was labelled
    /// "new claim", which mislabelled every document reply on an existing claim as a fresh FNOL.
    /// An unprocessed email claims no intent at all.</summary>
    private static string EmailIntentFor(EmailProcessingStatus status) => status switch
    {
        EmailProcessingStatus.CaseCreated => ChannelDetectedIntentValues.ClaimFnol,
        EmailProcessingStatus.LinkedToCase => ChannelDetectedIntentValues.DocumentUpload,
        _ => ChannelDetectedIntentValues.GeneralService,
    };

    /// <summary>The assistant's own six-intent classification, in the channel vocabulary. A thread
    /// with no turn yet (outbound-only, or one that arrived while the agent was gated off) stays
    /// "general_service" rather than claiming a classification that never happened.</summary>
    private static string DetectedIntentFor(CustomerConversationTurn? turn) => turn?.ClassifiedIntent switch
    {
        CustomerIntent.NewClaim => ChannelDetectedIntentValues.ClaimFnol,
        CustomerIntent.ProductInterest => ChannelDetectedIntentValues.QuoteRequest,
        CustomerIntent.ExistingClaimStatus => ChannelDetectedIntentValues.ClaimStatus,
        CustomerIntent.HumanRequested => ChannelDetectedIntentValues.HumanRequested,
        _ => ChannelDetectedIntentValues.GeneralService,
    };

    /// <summary>What the officer should do next, driven by what the assistant actually did rather
    /// than by case-linkage alone.</summary>
    private static string RoutingFor(CustomerConversationTurn? turn, bool hasLinkedCase) => turn?.Outcome switch
    {
        CustomerAssistantOutcome.QueuedForHumanApproval => "Approve or edit the drafted reply",
        CustomerAssistantOutcome.Escalated => "Escalated, needs a person",
        CustomerAssistantOutcome.CaseOpened => "Open linked claim",
        CustomerAssistantOutcome.LeadCreated => "Follow up on the new enquiry",
        CustomerAssistantOutcome.AwaitingMoreInformation => "Waiting on the customer",
        _ => hasLinkedCase ? "Open linked claim" : "Manual case-link review",
    };

    /// <summary>Work state, not transport state. WhatsApp's DeliveryState says whether the network
    /// carried the message; it says nothing about whether a human still owes this conversation
    /// something, which is the only thing the inbox is for. Delivery state remains the fallback for
    /// threads the assistant never handled.</summary>
    private static string StatusFor(CustomerConversationTurn? turn, WhatsAppDeliveryState deliveryState) =>
        turn?.Outcome switch
        {
            CustomerAssistantOutcome.QueuedForHumanApproval => "AwaitingApproval",
            CustomerAssistantOutcome.Escalated => "NeedsReview",
            CustomerAssistantOutcome.AwaitingMoreInformation => "AwaitingCustomer",
            CustomerAssistantOutcome.CaseOpened => "ClaimOpened",
            CustomerAssistantOutcome.LeadCreated => "LeadCaptured",
            CustomerAssistantOutcome.AutoReplied => "Answered",
            CustomerAssistantOutcome.Declined => "NeedsReview",
            _ => deliveryState.ToString(),
        };
}

// Sales-lead persistence moved to EfSalesLeadRepository (real scoped-JSON store), the manual
// create path (Gate 0) writes real leads; channel ingestion / fabric import feed the same store later.

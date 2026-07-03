# IIP Email Ingestion and Dispatch — Provider-Agnostic Port

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready architecture and Application-layer port specification  
**Area:** Email ingestion, outbound email dispatch, FNOL/claim intake routing  
**Status:** New-repository implementation specification  
**Date:** July 2026

---

## Shared rules used by this implementation output

This output follows the same implementation pattern used by the existing IIP backend service-output documents for Command Center, Claims, Documents, Approvals, Notifications, Integrations, Reporting, and Core Ports.

```text
1. Resolve tenant/workspace from server-side trusted context or a validated integration mapping.
2. Never trust an inbound email sender, mailbox address, header, subject, or attachment as tenant/workspace authority.
3. Enforce backend permissions before creating FNOL records, documents, channel conversations, tasks, or customer communications.
4. Write durable audit events for every inbound email processing attempt, success, failure, attachment handoff, and dispatch decision.
5. Keep Application-layer ports provider-agnostic. No Microsoft Graph, Gmail, Zoho, IMAP, SMTP, MIME library, or provider SDK types may appear in Application contracts.
6. Treat provider push/webhook notification as a latency optimization only. Polling remains the baseline correctness model.
7. Reuse existing document and claim services. Do not create a parallel upload or FNOL path for email.
8. Provider-not-configured and provider-unavailable states must surface explicitly. Do not fake mailbox success.
9. Staging, Production, and ManagedApplicationReviewer must never resolve Mock, InMemory, Simulated, Placeholder, Demo, DevelopmentAuth, SeededDemoBusinessData, or FallbackToMock provider behavior.
10. AI is never the final actor for a consequential insurance decision. Email intake may create candidate records and work items only.
11. Do not expose raw provider payloads, message bodies, attachment bytes, mailbox credentials, OAuth tokens, refresh tokens, SMTP passwords, or provider exception payloads in audit summaries or client-facing errors.
12. Domain remains free of EF Core, Azure SDKs, ASP.NET Core, Microsoft Graph SDK, Gmail SDK, Zoho SDK, IMAP/SMTP libraries, and provider-specific dependencies.
```

Fixed inputs consumed by this document and not redesigned here:

```text
ITenantContextAccessor
ITenantContextSetter, where asynchronous worker execution must set a validated TenantContext before calling Application services
IPermissionService
IAuditWriter
IProviderHealthCheck
IClaimIntakeService
IDocumentUploadService
INotificationDraftService
INotificationSendService
ICommunicationSendingProvider
Permissions
AuditEventTypeValues
ProviderCodes
AzureProviderStartupValidator
InfrastructureOptions
```

This document defines provider-agnostic email ports and the dispatcher that converts inbound mailbox events into claim/FNOL intake candidates. It does not replace the existing notification draft/send pipeline.

---

## 1. Scope of this pass

This pass introduces a provider-agnostic email boundary for IIP.

Implemented capabilities:

```text
- Define IEmailInboundProvider for fetching inbound mailbox messages, marking processed messages, and retrieving attachments.
- Define IEmailOutboundProvider as a generic provider adapter boundary for outbound email delivery.
- Clarify the relationship between IEmailOutboundProvider and the existing ICommunicationSendingProvider.
- Extend provider selection beyond Graph-only email.
- Define configuration shapes for Graph, Gmail, Zoho, and IMAP/SMTP providers.
- Define mailbox-to-tenant/workspace mapping for asynchronous inbound email processing.
- Define the email-to-claim intake dispatcher.
- Define polling as the baseline delivery model, with push/webhook as an optimization.
- Route claim-document attachments through IDocumentUploadService.
- Route new FNOL candidates through IClaimIntakeService.CreateFnolAsync.
- Route existing-claim or customer conversation updates through existing claim/channel services, not a separate email-only path.
- Write safe audit events for inbound email processing.
```

Not implemented in this pass:

```text
- Concrete Gmail API adapter.
- Concrete Zoho Mail API adapter.
- Concrete generic IMAP/SMTP adapter.
- Replacement of the existing Graph adapter.
- Email body AI classification prompts.
- OCR or attachment extraction logic.
- Retry/backoff orchestration beyond repository checkpoint semantics.
- End-user admin UI.
- A public inbound email route.
- Direct final claim approval, rejection, settlement, payment release, policy binding, or fraud determination.
```

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create a parallel architecture if equivalent files already exist.

### 2.1 Application files

```text
src/InsuranceIntelligence.Application/Email/IEmailInboundProvider.cs
src/InsuranceIntelligence.Application/Email/IEmailOutboundProvider.cs
src/InsuranceIntelligence.Application/Email/EmailProviderModels.cs
src/InsuranceIntelligence.Application/Email/EmailProviderValues.cs
src/InsuranceIntelligence.Application/Email/IEmailInboundCheckpointRepository.cs
src/InsuranceIntelligence.Application/Email/IEmailMailboxMappingRepository.cs
src/InsuranceIntelligence.Application/Email/IEmailInboundDispatchRepository.cs
src/InsuranceIntelligence.Application/Email/IEmailToClaimIntakeDispatcher.cs
src/InsuranceIntelligence.Application/Email/EmailToClaimIntakeDispatcher.cs
src/InsuranceIntelligence.Application/Email/EmailIntakeModels.cs
src/InsuranceIntelligence.Application/Email/EmailIntakeAuditActions.cs
src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
src/InsuranceIntelligence.Application/Common/Providers/ProviderCodes.cs
src/InsuranceIntelligence.Application/Common/Audit/AuditEventTypeValues.cs
```

### 2.2 Infrastructure files

```text
src/InsuranceIntelligence.Infrastructure/Configuration/InfrastructureOptions.cs
src/InsuranceIntelligence.Infrastructure/Configuration/GraphMailOptions.cs
src/InsuranceIntelligence.Infrastructure/Configuration/GmailMailOptions.cs
src/InsuranceIntelligence.Infrastructure/Configuration/ZohoMailOptions.cs
src/InsuranceIntelligence.Infrastructure/Configuration/ImapSmtpMailOptions.cs
src/InsuranceIntelligence.Infrastructure/Email/EfEmailInboundCheckpointRepository.cs
src/InsuranceIntelligence.Infrastructure/Email/EfEmailMailboxMappingRepository.cs
src/InsuranceIntelligence.Infrastructure/Email/EfEmailInboundDispatchRepository.cs
src/InsuranceIntelligence.Infrastructure/Email/GraphEmailInboundProvider.cs // existing adapter may be refactored to implement IEmailInboundProvider
src/InsuranceIntelligence.Infrastructure/Email/GraphEmailOutboundProvider.cs // existing Graph outbound adapter may be refactored to implement IEmailOutboundProvider
src/InsuranceIntelligence.Infrastructure/Email/GmailEmailInboundProvider.cs // deferred adapter boundary only
src/InsuranceIntelligence.Infrastructure/Email/GmailEmailOutboundProvider.cs // deferred adapter boundary only
src/InsuranceIntelligence.Infrastructure/Email/ZohoEmailInboundProvider.cs // deferred adapter boundary only
src/InsuranceIntelligence.Infrastructure/Email/ZohoEmailOutboundProvider.cs // deferred adapter boundary only
src/InsuranceIntelligence.Infrastructure/Email/ImapSmtpEmailInboundProvider.cs // deferred adapter boundary only
src/InsuranceIntelligence.Infrastructure/Email/ImapSmtpEmailOutboundProvider.cs // deferred adapter boundary only
src/InsuranceIntelligence.Infrastructure/DependencyInjection/EmailProviderRegistration.cs
```

### 2.3 Worker / function files

```text
src/InsuranceIntelligence.Functions/EmailIngestion/EmailInboxPollingFunction.cs
src/InsuranceIntelligence.Functions/EmailIngestion/EmailPushNotificationFunction.cs
src/InsuranceIntelligence.Functions/EmailIngestion/EmailIngestionQueueMessage.cs
```

### 2.4 Test files

```text
src/InsuranceIntelligence.Tests/Application/Email/EmailToClaimIntakeDispatcherTests.cs
src/InsuranceIntelligence.Tests/Application/Email/EmailProviderContractTests.cs
src/InsuranceIntelligence.Tests/Infrastructure/Email/EmailProviderRegistrationTests.cs
src/InsuranceIntelligence.Tests/Security/EmailInboundTenantIsolationTests.cs
```

---

## 3. Permissions and audit constants to add

### 3.1 Permissions

Add these constants to the canonical `Permissions` class.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static partial class Permissions
{
    public const string EmailInboundProcess = "email.inbound.process";
    public const string EmailMailboxManage = "email.mailbox.manage";
}
```

Usage rules:

```text
- EmailInboundProcess is required before processing inbound mailbox messages into claims, documents, conversations, or tasks.
- Existing ClaimsCreate, DocumentsUpload, ChannelsRead, and relevant workflow permissions are still required for the actual downstream operation.
- EmailMailboxManage is for future admin configuration of mailbox-to-tenant mappings; it is not required by the dispatcher at runtime.
```

### 3.2 Audit event type values

Add email-specific audit event types if the audit taxonomy allows new constants. If not, use `AuditEventTypeValues.DataChanged` with the action strings below.

```csharp
namespace InsuranceIntelligence.Application.Common.Audit;

public static partial class AuditEventTypeValues
{
    public const string EmailInboundProcessed = "email_inbound_processed";
    public const string EmailInboundFailed = "email_inbound_failed";
}
```

### 3.3 Audit action values

```csharp
namespace InsuranceIntelligence.Application.Email;

public static class EmailIntakeAuditActions
{
    public const string InboundProcessingAttempted = "email.inbound.processing.attempted";
    public const string InboundProcessingSucceeded = "email.inbound.processing.succeeded";
    public const string InboundProcessingFailed = "email.inbound.processing.failed";
    public const string AttachmentRoutedToDocumentUpload = "email.attachment.routed_to_document_upload";
    public const string RoutedToFnolCandidate = "email.inbound.routed_to_fnol_candidate";
    public const string RoutedToExistingClaim = "email.inbound.routed_to_existing_claim";
    public const string RoutedToManualReview = "email.inbound.routed_to_manual_review";
}
```

Audit rules:

```text
- Audit summaries must include mailbox mapping ID, provider code, safe message reference, dispatch decision, and correlation ID.
- Audit summaries must not include raw email body, full attachment content, OAuth tokens, SMTP passwords, raw MIME payload, provider webhook payload, or provider exception payload.
```

---

## 4. Provider codes and configuration

### 4.1 Provider codes

Extend provider constants without removing the existing Graph code.

```csharp
namespace InsuranceIntelligence.Application.Common.Providers;

public static partial class ProviderCodes
{
    public const string GmailMail = "gmail_mail";
    public const string ZohoMail = "zoho_mail";
    public const string ImapSmtpMail = "imap_smtp_mail";
}
```

Existing code remains valid:

```csharp
ProviderCodes.GraphMail // "graph_mail"
```

### 4.2 Email provider values

```csharp
namespace InsuranceIntelligence.Application.Email;

public static class EmailProviderValues
{
    public const string Graph = "Graph";
    public const string Gmail = "Gmail";
    public const string Zoho = "Zoho";
    public const string ImapSmtp = "ImapSmtp";

    // LocalTest only. Rejected by AzureProviderStartupValidator in protected runtimes.
    public const string Mock = "Mock";
}
```

Protected-runtime rule:

```text
- Staging, Production, and ManagedApplicationReviewer may use Graph, Gmail, Zoho, or ImapSmtp only when fully configured.
- Mock is LocalTest/test-only and must be rejected by AzureProviderStartupValidator in protected runtimes.
```

### 4.3 InfrastructureOptions extension

The current repository has Graph-centric email configuration. Extend the shape so Graph is one adapter among several.

```csharp
namespace InsuranceIntelligence.Infrastructure.Configuration;

public sealed record InfrastructureOptions
{
    public string EmailProvider { get; init; } = EmailProviderValues.Graph;

    public GraphMailOptions GraphMail { get; init; } = new();
    public GmailMailOptions GmailMail { get; init; } = new();
    public ZohoMailOptions ZohoMail { get; init; } = new();
    public ImapSmtpMailOptions ImapSmtpMail { get; init; } = new();
}

public sealed record GmailMailOptions
{
    public bool Enabled { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecretSecretName { get; init; }
    public string? OAuthTenantOrDomainHint { get; init; }
    public string? WebhookVerificationSecretName { get; init; }
    public int PollingIntervalSeconds { get; init; } = 300;
}

public sealed record ZohoMailOptions
{
    public bool Enabled { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecretSecretName { get; init; }
    public string? Region { get; init; }
    public string? WebhookVerificationSecretName { get; init; }
    public int PollingIntervalSeconds { get; init; } = 300;
}

public sealed record ImapSmtpMailOptions
{
    public bool Enabled { get; init; }
    public string? ImapHost { get; init; }
    public int ImapPort { get; init; } = 993;
    public bool ImapUseSsl { get; init; } = true;
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public bool SmtpUseStartTls { get; init; } = true;
    public string? UsernameSecretName { get; init; }
    public string? PasswordSecretName { get; init; }
    public int PollingIntervalSeconds { get; init; } = 300;
}
```

Configuration rules:

```text
- Provider-specific secrets are referenced by Key Vault secret names, not stored inline.
- Gmail and Zoho OAuth flow details are TODOs and must be confirmed against provider documentation before adapter implementation.
- IMAP/SMTP credentials must use Key Vault and may not be stored in appsettings for protected runtimes.
- Push/webhook configuration may be present for providers that support it, but polling remains the baseline correctness mechanism.
```

---

## 5. Delivery model per provider

IIP must not assume all mail providers support the same delivery mode.

| Provider | Baseline model | Optional optimization | Design rule |
|---|---|---|---|
| Microsoft Graph | Polling checkpoint and/or provider delta handled inside adapter | Change notifications/webhooks where configured | Application contract sees only opaque checkpoints and safe message refs. |
| Gmail API | Polling checkpoint handled inside adapter | Push notifications where configured | Push triggers a poll/dispatch cycle; it is not the source of truth. |
| Zoho Mail API | Polling checkpoint handled inside adapter | Provider-specific webhook/push if available/configured | Exact API/auth model is adapter-specific and deferred. |
| Generic IMAP/SMTP | Polling | IMAP IDLE may be used only as a hint where reliable | Dispatcher correctness must not depend on IMAP IDLE. |

Standing rule:

```text
Polling is the baseline guarantee. Push/webhook notifications may reduce latency but may not be required for correctness. A missed push event must be recovered by the next polling cycle.
```

---

## 6. Inbound provider port

`IEmailInboundProvider` is an Application-layer provider port. It exposes safe, provider-neutral email concepts only.

```csharp
namespace InsuranceIntelligence.Application.Email;

public interface IEmailInboundProvider
{
    string ProviderCode { get; }

    Task<EmailFetchBatch> FetchNewMessagesAsync(
        EmailFetchRequest request,
        CancellationToken cancellationToken);

    Task<EmailAttachmentContent> GetAttachmentAsync(
        EmailAttachmentRequest request,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        EmailMarkProcessedRequest request,
        CancellationToken cancellationToken);
}

public sealed record EmailFetchRequest(
    string MailboxMappingId,
    string ProviderMailboxKey,
    string? OpaqueCheckpoint,
    int MaxMessages);

public sealed record EmailFetchBatch(
    string ProviderCode,
    string MailboxMappingId,
    string? NextOpaqueCheckpoint,
    IReadOnlyList<EmailInboundMessage> Messages);

public sealed record EmailInboundMessage(
    string ProviderCode,
    string ProviderMessageRef,
    string? ConversationRef,
    string FromAddress,
    string? FromDisplayName,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    string? Subject,
    string TextPreview,
    string? NormalizedTextBody,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<EmailInboundAttachmentRef> Attachments,
    IReadOnlyDictionary<string, string> SafeHeaders);

public sealed record EmailInboundAttachmentRef(
    string ProviderAttachmentRef,
    string FileName,
    string? ContentType,
    long? SizeBytes,
    string? ContentHash);

public sealed record EmailAttachmentRequest(
    string MailboxMappingId,
    string ProviderMailboxKey,
    string ProviderMessageRef,
    string ProviderAttachmentRef);

public sealed record EmailAttachmentContent(
    string ProviderAttachmentRef,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record EmailMarkProcessedRequest(
    string MailboxMappingId,
    string ProviderMailboxKey,
    string ProviderMessageRef,
    string? OpaqueCheckpoint,
    string ProcessingResult,
    string CorrelationId);
```

Contract rules:

```text
- ProviderMessageRef is an opaque stable reference, not a Graph message ID, Gmail ID, Zoho ID, or IMAP UID in public contract semantics.
- OpaqueCheckpoint is adapter-owned. It may represent a delta token, history ID, UID validity/highest UID, page cursor, timestamp, or another provider-specific checkpoint.
- Application services must never parse OpaqueCheckpoint.
- SafeHeaders may include non-sensitive routing hints only. Do not include Authorization, authentication tokens, DKIM private material, or full raw MIME headers.
- NormalizedTextBody may be used only for intake classification and claim notes according to policy; it must not be written into audit summary.
```

---

## 7. Outbound provider port and relationship to ICommunicationSendingProvider

### 7.1 Decision

`ICommunicationSendingProvider` remains the higher-level Application port consumed by `INotificationSendService`.

`IEmailOutboundProvider` is a lower-level provider adapter boundary for email-specific sending. Concrete Graph/Gmail/Zoho/SMTP adapters may implement `IEmailOutboundProvider`, and the existing `ICommunicationSendingProvider` implementation may delegate to it when the channel resolves to email.

Do not silently duplicate send logic.

### 7.2 IEmailOutboundProvider contract

```csharp
namespace InsuranceIntelligence.Application.Email;

public interface IEmailOutboundProvider
{
    string ProviderCode { get; }

    Task<EmailOutboundProviderResult> SendEmailAsync(
        EmailOutboundProviderRequest request,
        CancellationToken cancellationToken);
}

public sealed record EmailOutboundProviderRequest(
    string TenantId,
    string? WorkspaceId,
    string FromMailboxKey,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    IReadOnlyList<string> BccAddresses,
    string? Subject,
    string Body,
    string? HtmlBody,
    IReadOnlyList<EmailOutboundAttachment> Attachments,
    string CorrelationId);

public sealed record EmailOutboundAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record EmailOutboundProviderResult(
    bool Confirmed,
    string? ProviderMessageRef,
    DateTimeOffset? SentAt,
    string SafeProviderSummary);
```

Relationship rules:

```text
- NotificationSendService continues to call ICommunicationSendingProvider.
- ICommunicationSendingProvider chooses the communication channel/provider and may delegate email sends to IEmailOutboundProvider.
- IEmailOutboundProvider must not bypass the existing human-approved draft/send pipeline.
- IEmailOutboundProvider must not be injected directly into controllers for sending customer communications.
```

---

## 8. Mailbox-to-tenant mapping

Inbound email arrives without an authenticated user principal. IIP must resolve tenant/workspace through a configured, durable, server-side mailbox mapping.

```csharp
namespace InsuranceIntelligence.Application.Email;

public interface IEmailMailboxMappingRepository
{
    Task<EmailMailboxMappingReadModel?> GetActiveMappingAsync(
        string mailboxMappingId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailMailboxMappingReadModel>> ListActiveMappingsForProviderAsync(
        string providerCode,
        CancellationToken cancellationToken);
}

public sealed record EmailMailboxMappingReadModel(
    string MailboxMappingId,
    string TenantId,
    string? WorkspaceId,
    string ProviderCode,
    string ProviderMailboxKey,
    string DisplayAddress,
    string IntakeMode,
    string IntegrationUserId,
    bool IsActive,
    DateTimeOffset CreatedAt);

public static class EmailIntakeModeValues
{
    public const string Fnol = "fnol";
    public const string ClaimDocuments = "claim_documents";
    public const string CustomerConversation = "customer_conversation";
    public const string ManualReview = "manual_review";
}
```

Mapping rules:

```text
- MailboxMappingId is configured by an admin and stored durably.
- ProviderMailboxKey is adapter-specific but not secret. It may be an address, provider mailbox identifier, or configured mailbox alias.
- IntegrationUserId is a real service/integration identity in the IIP permission store.
- The dispatcher must set TenantContext using TenantId, WorkspaceId, IntegrationUserId, and real effective permissions before calling downstream Application services.
- If the mapping is missing or inactive, processing fails closed and writes a safe audit/operations log if a tenant can be resolved; otherwise it writes only platform telemetry.
```

---

## 9. Checkpoint repository

```csharp
namespace InsuranceIntelligence.Application.Email;

public interface IEmailInboundCheckpointRepository
{
    Task<EmailInboundCheckpointReadModel?> GetCheckpointAsync(
        string tenantId,
        string? workspaceId,
        string mailboxMappingId,
        CancellationToken cancellationToken);

    Task SaveCheckpointAsync(
        EmailInboundCheckpointWriteModel checkpoint,
        CancellationToken cancellationToken);
}

public sealed record EmailInboundCheckpointReadModel(
    string MailboxMappingId,
    string TenantId,
    string? WorkspaceId,
    string? OpaqueCheckpoint,
    DateTimeOffset UpdatedAt);

public sealed record EmailInboundCheckpointWriteModel(
    string MailboxMappingId,
    string TenantId,
    string? WorkspaceId,
    string? OpaqueCheckpoint,
    DateTimeOffset UpdatedAt,
    string CorrelationId);
```

Checkpoint rules:

```text
- The checkpoint value is opaque to Application services.
- Checkpoints are tenant/workspace/mailbox scoped.
- A failed message must not advance the checkpoint past unprocessed messages unless the provider semantics guarantee replay-safe behavior and the failure has been persisted for manual review.
```

---

## 10. Email dispatch decision contract

```csharp
namespace InsuranceIntelligence.Application.Email;

public interface IEmailInboundDispatchRepository
{
    Task<EmailDispatchDecision> ClassifyDispatchAsync(
        string tenantId,
        string? workspaceId,
        EmailInboundMessage message,
        CancellationToken cancellationToken);
}

public sealed record EmailDispatchDecision(
    string DecisionType,
    string? ClaimId,
    string? CustomerId,
    string? ChannelConversationId,
    string Reason);

public static class EmailDispatchDecisionTypeValues
{
    public const string CreateFnolCandidate = "create_fnol_candidate";
    public const string AttachToExistingClaim = "attach_to_existing_claim";
    public const string RouteToCustomerConversation = "route_to_customer_conversation";
    public const string ManualReview = "manual_review";
}
```

Decision rules:

```text
- Dispatch classification may use deterministic rules such as claim number in subject, sender mapping, mailbox intake mode, and safe headers.
- If AI classification is used later, it must be deterministic-first, grounded in retrieved records, validated by IAiOutputValidator, and auditable.
- Unclear messages go to ManualReview. Do not fabricate claim/customer matches.
```

---

## 11. Email-to-claim intake dispatcher contract

```csharp
namespace InsuranceIntelligence.Application.Email;

public interface IEmailToClaimIntakeDispatcher
{
    Task<EmailInboundDispatchResult> ProcessMailboxAsync(
        string mailboxMappingId,
        CancellationToken cancellationToken);

    Task<EmailInboundDispatchResult> ProcessPushNotificationAsync(
        EmailPushNotificationReceived notification,
        CancellationToken cancellationToken);
}

public sealed record EmailPushNotificationReceived(
    string ProviderCode,
    string MailboxMappingId,
    string? ProviderNotificationRef,
    DateTimeOffset ReceivedAt);

public sealed record EmailInboundDispatchResult(
    string MailboxMappingId,
    string ProviderCode,
    int MessagesFetched,
    int MessagesProcessed,
    int MessagesFailed,
    string Status,
    string CorrelationId);

public static class EmailInboundDispatchStatusValues
{
    public const string Succeeded = "succeeded";
    public const string PartialFailure = "partial_failure";
    public const string Failed = "failed";
}
```

Push rule:

```text
ProcessPushNotificationAsync must enqueue or run the same mailbox processing path used by polling. It must not trust provider notification payloads as the source of message content.
```

---

## 12. Full C# implementation sketch

The implementation below shows the Application-layer dispatcher logic. It intentionally delegates provider-specific behavior to `IEmailInboundProvider` and downstream claim/document behavior to existing services.

```csharp
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;
using InsuranceIntelligence.Application.Documents;

namespace InsuranceIntelligence.Application.Email;

internal sealed class EmailToClaimIntakeDispatcher : IEmailToClaimIntakeDispatcher
{
    private const int DefaultMaxMessagesPerCycle = 50;

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantContextSetter _tenantContextSetter;
    private readonly IPermissionService _permissionService;
    private readonly IAuditWriter _auditWriter;
    private readonly IProviderHealthCheck _providerHealthCheck;
    private readonly IEmailMailboxMappingRepository _mailboxMappingRepository;
    private readonly IEmailInboundCheckpointRepository _checkpointRepository;
    private readonly IEmailInboundDispatchRepository _dispatchRepository;
    private readonly IReadOnlyDictionary<string, IEmailInboundProvider> _inboundProviders;
    private readonly IClaimIntakeService _claimIntakeService;
    private readonly IDocumentUploadService _documentUploadService;

    public EmailToClaimIntakeDispatcher(
        ITenantContextAccessor tenantContextAccessor,
        ITenantContextSetter tenantContextSetter,
        IPermissionService permissionService,
        IAuditWriter auditWriter,
        IProviderHealthCheck providerHealthCheck,
        IEmailMailboxMappingRepository mailboxMappingRepository,
        IEmailInboundCheckpointRepository checkpointRepository,
        IEmailInboundDispatchRepository dispatchRepository,
        IEnumerable<IEmailInboundProvider> inboundProviders,
        IClaimIntakeService claimIntakeService,
        IDocumentUploadService documentUploadService)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _tenantContextSetter = tenantContextSetter;
        _permissionService = permissionService;
        _auditWriter = auditWriter;
        _providerHealthCheck = providerHealthCheck;
        _mailboxMappingRepository = mailboxMappingRepository;
        _checkpointRepository = checkpointRepository;
        _dispatchRepository = dispatchRepository;
        _inboundProviders = inboundProviders.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _claimIntakeService = claimIntakeService;
        _documentUploadService = documentUploadService;
    }

    public async Task<EmailInboundDispatchResult> ProcessMailboxAsync(
        string mailboxMappingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mailboxMappingId))
        {
            throw new ValidationException("mailboxMappingId is required.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var mapping = await _mailboxMappingRepository.GetActiveMappingAsync(mailboxMappingId, cancellationToken);
        if (mapping is null)
        {
            throw new NotFoundOrNotVisibleException("Email mailbox mapping was not found or is not active.");
        }

        if (!_inboundProviders.TryGetValue(mapping.ProviderCode, out var provider))
        {
            throw new ProviderNotConfiguredException("Email inbound provider is not configured.");
        }

        var providerHealth = await _providerHealthCheck.CheckAsync(mapping.ProviderCode, cancellationToken);
        if (providerHealth.Status == ProviderStatus.NotConfigured)
        {
            throw new ProviderNotConfiguredException("Email inbound provider is not configured.");
        }

        if (providerHealth.Status == ProviderStatus.Unavailable)
        {
            throw new ProviderUnavailableException("Email inbound provider is unavailable.");
        }

        var context = await BuildIntegrationTenantContextAsync(mapping, correlationId, cancellationToken);
        _tenantContextSetter.SetCurrent(context);

        try
        {
            await _permissionService.EnsureAsync(Permissions.EmailInboundProcess, cancellationToken);

            var checkpoint = await _checkpointRepository.GetCheckpointAsync(
                context.TenantId,
                context.WorkspaceId,
                mapping.MailboxMappingId,
                cancellationToken);

            await WriteAuditAsync(
                EmailIntakeAuditActions.InboundProcessingAttempted,
                mapping,
                "Inbound email processing attempted.",
                entityId: mapping.MailboxMappingId,
                cancellationToken);

            var batch = await provider.FetchNewMessagesAsync(
                new EmailFetchRequest(
                    MailboxMappingId: mapping.MailboxMappingId,
                    ProviderMailboxKey: mapping.ProviderMailboxKey,
                    OpaqueCheckpoint: checkpoint?.OpaqueCheckpoint,
                    MaxMessages: DefaultMaxMessagesPerCycle),
                cancellationToken);

            var processed = 0;
            var failed = 0;

            foreach (var message in batch.Messages)
            {
                try
                {
                    await ProcessMessageAsync(mapping, provider, message, cancellationToken);
                    await provider.MarkProcessedAsync(
                        new EmailMarkProcessedRequest(
                            MailboxMappingId: mapping.MailboxMappingId,
                            ProviderMailboxKey: mapping.ProviderMailboxKey,
                            ProviderMessageRef: message.ProviderMessageRef,
                            OpaqueCheckpoint: batch.NextOpaqueCheckpoint,
                            ProcessingResult: "processed",
                            CorrelationId: context.CorrelationId),
                        cancellationToken);
                    processed++;
                }
                catch (Exception)
                {
                    failed++;
                    await WriteAuditAsync(
                        EmailIntakeAuditActions.InboundProcessingFailed,
                        mapping,
                        $"Inbound email processing failed for messageRef={SafeRef(message.ProviderMessageRef)}.",
                        entityId: SafeRef(message.ProviderMessageRef),
                        cancellationToken);
                }
            }

            if (failed == 0)
            {
                await _checkpointRepository.SaveCheckpointAsync(
                    new EmailInboundCheckpointWriteModel(
                        MailboxMappingId: mapping.MailboxMappingId,
                        TenantId: context.TenantId,
                        WorkspaceId: context.WorkspaceId,
                        OpaqueCheckpoint: batch.NextOpaqueCheckpoint,
                        UpdatedAt: DateTimeOffset.UtcNow,
                        CorrelationId: context.CorrelationId),
                    cancellationToken);
            }

            await WriteAuditAsync(
                EmailIntakeAuditActions.InboundProcessingSucceeded,
                mapping,
                $"Inbound email processing completed. fetched={batch.Messages.Count}; processed={processed}; failed={failed}.",
                entityId: mapping.MailboxMappingId,
                cancellationToken);

            return new EmailInboundDispatchResult(
                MailboxMappingId: mapping.MailboxMappingId,
                ProviderCode: mapping.ProviderCode,
                MessagesFetched: batch.Messages.Count,
                MessagesProcessed: processed,
                MessagesFailed: failed,
                Status: failed == 0 ? EmailInboundDispatchStatusValues.Succeeded : EmailInboundDispatchStatusValues.PartialFailure,
                CorrelationId: context.CorrelationId);
        }
        finally
        {
            _tenantContextSetter.Clear();
        }
    }

    public Task<EmailInboundDispatchResult> ProcessPushNotificationAsync(
        EmailPushNotificationReceived notification,
        CancellationToken cancellationToken)
    {
        if (notification is null || string.IsNullOrWhiteSpace(notification.MailboxMappingId))
        {
            throw new ValidationException("mailboxMappingId is required.");
        }

        // Push is only a latency optimization. The provider notification ref is not trusted as content.
        return ProcessMailboxAsync(notification.MailboxMappingId, cancellationToken);
    }

    private async Task ProcessMessageAsync(
        EmailMailboxMappingReadModel mapping,
        IEmailInboundProvider provider,
        EmailInboundMessage message,
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;

        var decision = await _dispatchRepository.ClassifyDispatchAsync(
            context.TenantId,
            context.WorkspaceId,
            message,
            cancellationToken);

        switch (decision.DecisionType)
        {
            case EmailDispatchDecisionTypeValues.CreateFnolCandidate:
                await _permissionService.EnsureAsync(Permissions.ClaimsCreate, cancellationToken);
                await CreateFnolCandidateAsync(mapping, provider, message, decision, cancellationToken);
                await WriteAuditAsync(
                    EmailIntakeAuditActions.RoutedToFnolCandidate,
                    mapping,
                    $"Inbound email routed to FNOL candidate. messageRef={SafeRef(message.ProviderMessageRef)}.",
                    entityId: SafeRef(message.ProviderMessageRef),
                    cancellationToken);
                break;

            case EmailDispatchDecisionTypeValues.AttachToExistingClaim:
                await _permissionService.EnsureAsync(Permissions.DocumentsUpload, cancellationToken);
                await AttachDocumentsToExistingClaimAsync(mapping, provider, message, decision, cancellationToken);
                await WriteAuditAsync(
                    EmailIntakeAuditActions.RoutedToExistingClaim,
                    mapping,
                    $"Inbound email routed to existing claim. claimId={decision.ClaimId}; messageRef={SafeRef(message.ProviderMessageRef)}.",
                    entityId: decision.ClaimId ?? SafeRef(message.ProviderMessageRef),
                    cancellationToken);
                break;

            case EmailDispatchDecisionTypeValues.RouteToCustomerConversation:
                await _permissionService.EnsureAsync(Permissions.ChannelsRead, cancellationToken);
                // TODO: call existing channel/conversation append service when its write port is present.
                await WriteAuditAsync(
                    EmailIntakeAuditActions.RoutedToManualReview,
                    mapping,
                    $"Inbound email conversation routing requires channel append implementation. messageRef={SafeRef(message.ProviderMessageRef)}.",
                    entityId: SafeRef(message.ProviderMessageRef),
                    cancellationToken);
                break;

            default:
                await WriteAuditAsync(
                    EmailIntakeAuditActions.RoutedToManualReview,
                    mapping,
                    $"Inbound email routed to manual review. messageRef={SafeRef(message.ProviderMessageRef)}.",
                    entityId: SafeRef(message.ProviderMessageRef),
                    cancellationToken);
                break;
        }
    }

    private async Task CreateFnolCandidateAsync(
        EmailMailboxMappingReadModel mapping,
        IEmailInboundProvider provider,
        EmailInboundMessage message,
        EmailDispatchDecision decision,
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;

        var fnol = await _claimIntakeService.CreateFnolAsync(
            new ClaimFnolCreateRequest(
                SourceChannel: "email",
                SourceReference: message.ProviderMessageRef,
                CustomerId: decision.CustomerId,
                Summary: BuildSafeFnolSummary(message),
                ReceivedAtUtc: message.ReceivedAt),
            cancellationToken);

        foreach (var attachment in message.Attachments)
        {
            await UploadAttachmentAsClaimDocumentAsync(mapping, provider, message, attachment, fnol.ClaimId, cancellationToken);
        }
    }

    private async Task AttachDocumentsToExistingClaimAsync(
        EmailMailboxMappingReadModel mapping,
        IEmailInboundProvider provider,
        EmailInboundMessage message,
        EmailDispatchDecision decision,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(decision.ClaimId))
        {
            throw new WorkflowConflictException("Existing claim dispatch requires claimId.");
        }

        foreach (var attachment in message.Attachments)
        {
            await UploadAttachmentAsClaimDocumentAsync(mapping, provider, message, attachment, decision.ClaimId, cancellationToken);
        }
    }

    private async Task UploadAttachmentAsClaimDocumentAsync(
        EmailMailboxMappingReadModel mapping,
        IEmailInboundProvider provider,
        EmailInboundMessage message,
        EmailInboundAttachmentRef attachment,
        string claimId,
        CancellationToken cancellationToken)
    {
        var content = await provider.GetAttachmentAsync(
            new EmailAttachmentRequest(
                MailboxMappingId: mapping.MailboxMappingId,
                ProviderMailboxKey: mapping.ProviderMailboxKey,
                ProviderMessageRef: message.ProviderMessageRef,
                ProviderAttachmentRef: attachment.ProviderAttachmentRef),
            cancellationToken);

        await _documentUploadService.UploadAsync(
            new DocumentUploadRequest(
                RelatedEntityType: "claim",
                RelatedEntityId: claimId,
                FileName: content.FileName,
                ContentType: content.ContentType,
                Content: content.Content,
                SourceChannel: "email",
                SourceReference: message.ProviderMessageRef),
            cancellationToken);

        await WriteAuditAsync(
            EmailIntakeAuditActions.AttachmentRoutedToDocumentUpload,
            mapping,
            $"Email attachment routed through IDocumentUploadService. claimId={claimId}; fileName={SafeRef(content.FileName)}.",
            entityId: claimId,
            cancellationToken);
    }

    private static Task<TenantContext> BuildIntegrationTenantContextAsync(
        EmailMailboxMappingReadModel mapping,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // The concrete implementation should load real effective roles/permissions for IntegrationUserId
        // from IPermissionReadRepository or the authenticated tenant resolver equivalent.
        var context = new TenantContext(
            TenantId: mapping.TenantId,
            WorkspaceId: mapping.WorkspaceId,
            UserId: mapping.IntegrationUserId,
            Roles: new HashSet<string>(StringComparer.Ordinal),
            Permissions: new HashSet<string>(StringComparer.Ordinal),
            CorrelationId: correlationId);

        return Task.FromResult(context);
    }

    private Task WriteAuditAsync(
        string action,
        EmailMailboxMappingReadModel mapping,
        string summary,
        string entityId,
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;

        var eventType = action == EmailIntakeAuditActions.InboundProcessingFailed
            ? AuditEventTypeValues.EmailInboundFailed
            : AuditEventTypeValues.EmailInboundProcessed;

        return _auditWriter.WriteAsync(
            new AuditEventCreateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                EventType: eventType,
                ActorType: "integration",
                ActorUserId: context.UserId,
                Action: action,
                EntityType: "email_inbound",
                EntityId: entityId,
                Summary: RedactAndTruncate(summary, 800),
                Reason: $"mailboxMappingId={mapping.MailboxMappingId}; provider={mapping.ProviderCode}; mailbox={mapping.DisplayAddress}",
                CorrelationId: context.CorrelationId,
                OccurredAt: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private static string BuildSafeFnolSummary(EmailInboundMessage message)
    {
        var subject = string.IsNullOrWhiteSpace(message.Subject) ? "No subject" : message.Subject.Trim();
        return RedactAndTruncate($"Email FNOL candidate. Subject: {subject}. Preview: {message.TextPreview}", 1000);
    }

    private static string SafeRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return RedactAndTruncate(value.Replace("\r", " ").Replace("\n", " ").Trim(), 128);
    }

    private static string RedactAndTruncate(string value, int maxLength)
    {
        var sanitized = value
            .Replace("Bearer ", "Bearer [redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("SharedAccessSignature", "[redacted-sas]", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }
}
```

Implementation note:

```text
BuildIntegrationTenantContextAsync is shown inline to make the dispatcher flow visible. In production, this should be implemented through a small resolver that loads the integration user's real effective permissions from the existing permission store. It must not grant permissions from the mailbox mapping row itself.
```

---

## 13. Provider registration guidance

```csharp
using InsuranceIntelligence.Application.Email;
using InsuranceIntelligence.Application.Common.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Infrastructure.DependencyInjection;

public static class EmailProviderRegistration
{
    public static IServiceCollection AddEmailProviders(
        this IServiceCollection services,
        InfrastructureOptions options,
        RuntimeProfile runtimeProfile)
    {
        if (runtimeProfile.IsProtected() && string.Equals(options.EmailProvider, EmailProviderValues.Mock, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mock email provider is not permitted in protected runtime profiles.");
        }

        switch (options.EmailProvider)
        {
            case EmailProviderValues.Graph:
                services.AddScoped<IEmailInboundProvider, GraphEmailInboundProvider>();
                services.AddScoped<IEmailOutboundProvider, GraphEmailOutboundProvider>();
                break;

            case EmailProviderValues.Gmail:
                services.AddScoped<IEmailInboundProvider, GmailEmailInboundProvider>();
                services.AddScoped<IEmailOutboundProvider, GmailEmailOutboundProvider>();
                break;

            case EmailProviderValues.Zoho:
                services.AddScoped<IEmailInboundProvider, ZohoEmailInboundProvider>();
                services.AddScoped<IEmailOutboundProvider, ZohoEmailOutboundProvider>();
                break;

            case EmailProviderValues.ImapSmtp:
                services.AddScoped<IEmailInboundProvider, ImapSmtpEmailInboundProvider>();
                services.AddScoped<IEmailOutboundProvider, ImapSmtpEmailOutboundProvider>();
                break;

            default:
                throw new InvalidOperationException($"Unsupported EmailProvider '{options.EmailProvider}'.");
        }

        return services;
    }
}
```

Deferred adapter behavior:

```text
Until Gmail, Zoho, and IMAP/SMTP adapters are implemented and configured, registration for those providers must fail closed or each deferred adapter must throw ProviderNotConfiguredException. It must not return fake messages, fake sends, or optimistic success.
```

---

## 14. Tests to add

```text
EmailToClaimIntakeDispatcherTests
- ProcessMailboxAsync_ThrowsProviderNotConfiguredWhenProviderNotRegistered
- ProcessMailboxAsync_ResolvesTenantWorkspaceFromMailboxMapping
- ProcessMailboxAsync_UsesIntegrationUserAndPermissionService
- ProcessMailboxAsync_PollingBaselineWorksWithoutPush
- ProcessPushNotificationAsync_DelegatesToPollingPath
- ProcessMailboxAsync_AttachmentRoutesThroughIDocumentUploadService
- ProcessMailboxAsync_NewFnolRoutesThroughIClaimIntakeService
- ProcessMailboxAsync_WritesAuditForInboundEmailProcessing
- ProcessMailboxAsync_DoesNotAdvanceCheckpointWhenMessageFails
- ProcessMailboxAsync_DoesNotIncludeRawBodyOrAttachmentBytesInAudit

EmailProviderRegistrationTests
- ProtectedRuntime_RejectsMockEmailProvider
- UnsupportedEmailProvider_FailsStartup
- GraphProvider_RegistersGraphInboundAndOutboundPorts
- GmailProvider_RequiresGmailOptionsBeforeRegistrationOrFailsClosed
- ZohoProvider_RequiresZohoOptionsBeforeRegistrationOrFailsClosed
- ImapSmtpProvider_RequiresImapSmtpOptionsBeforeRegistrationOrFailsClosed

EmailProviderContractTests
- InboundProviderContract_DoesNotExposeGraphTypes
- InboundProviderContract_DoesNotExposeGmailTypes
- InboundProviderContract_UsesOpaqueCheckpoint
- OutboundProviderContract_ReturnsRealConfirmationOnly
```

### 14.1 Provider-not-configured test

```csharp
[Fact]
public async Task ProcessMailboxAsync_ThrowsProviderNotConfiguredWhenProviderNotRegistered()
{
    var mapping = TestEmailMappings.GraphMailbox("tenant-a", "workspace-a");
    var service = BuildDispatcher(
        mappings: new[] { mapping },
        inboundProviders: Array.Empty<IEmailInboundProvider>());

    await Assert.ThrowsAsync<ProviderNotConfiguredException>(() =>
        service.ProcessMailboxAsync(mapping.MailboxMappingId, CancellationToken.None));
}
```

### 14.2 Tenant resolution from mailbox mapping test

```csharp
[Fact]
public async Task ProcessMailboxAsync_ResolvesTenantWorkspaceFromMailboxMapping()
{
    var mapping = TestEmailMappings.GraphMailbox("tenant-a", "workspace-a", integrationUserId: "email-worker-1");
    var repository = new Mock<IEmailInboundDispatchRepository>();

    var service = BuildDispatcher(
        mappings: new[] { mapping },
        dispatchRepository: repository.Object);

    await service.ProcessMailboxAsync(mapping.MailboxMappingId, CancellationToken.None);

    repository.Verify(x => x.ClassifyDispatchAsync(
        "tenant-a",
        "workspace-a",
        It.IsAny<EmailInboundMessage>(),
        It.IsAny<CancellationToken>()), Times.AtLeastOnce);
}
```

### 14.3 Polling-baseline test

```csharp
[Fact]
public async Task ProcessPushNotificationAsync_DelegatesToPollingPath()
{
    var provider = new Mock<IEmailInboundProvider>();
    provider.SetupGet(x => x.ProviderCode).Returns(ProviderCodes.GraphMail);
    provider.Setup(x => x.FetchNewMessagesAsync(It.IsAny<EmailFetchRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new EmailFetchBatch(ProviderCodes.GraphMail, "mailbox-1", "cursor-2", Array.Empty<EmailInboundMessage>()));

    var service = BuildDispatcher(inboundProviders: new[] { provider.Object });

    await service.ProcessPushNotificationAsync(
        new EmailPushNotificationReceived(ProviderCodes.GraphMail, "mailbox-1", "push-1", DateTimeOffset.UtcNow),
        CancellationToken.None);

    provider.Verify(x => x.FetchNewMessagesAsync(It.IsAny<EmailFetchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

### 14.4 Attachment routes through IDocumentUploadService test

```csharp
[Fact]
public async Task ProcessMailboxAsync_AttachmentRoutesThroughIDocumentUploadService()
{
    var documentUploadService = new Mock<IDocumentUploadService>();
    var service = BuildDispatcher(documentUploadService: documentUploadService.Object);

    await service.ProcessMailboxAsync("mailbox-1", CancellationToken.None);

    documentUploadService.Verify(x => x.UploadAsync(
        It.Is<DocumentUploadRequest>(r =>
            r.RelatedEntityType == "claim" &&
            r.SourceChannel == "email"),
        It.IsAny<CancellationToken>()), Times.AtLeastOnce);
}
```

### 14.5 Audit write test

```csharp
[Fact]
public async Task ProcessMailboxAsync_WritesAuditForInboundEmailProcessing()
{
    var auditWriter = new Mock<IAuditWriter>();
    var service = BuildDispatcher(auditWriter: auditWriter.Object);

    await service.ProcessMailboxAsync("mailbox-1", CancellationToken.None);

    auditWriter.Verify(x => x.WriteAsync(
        It.Is<AuditEventCreateRequest>(evt =>
            evt.EventType == AuditEventTypeValues.EmailInboundProcessed &&
            evt.ActorType == "integration" &&
            evt.Action == EmailIntakeAuditActions.InboundProcessingSucceeded &&
            !evt.Summary.Contains("Authorization", StringComparison.OrdinalIgnoreCase) &&
            !evt.Summary.Contains("attachment bytes", StringComparison.OrdinalIgnoreCase)),
        It.IsAny<CancellationToken>()), Times.AtLeastOnce);
}
```

---

## 15. TODOs and confirmation points

| Area | Current decision | TODO / confirmation needed |
|---|---|---|
| Gmail adapter | Port/config shape only | Confirm exact Gmail API OAuth model, webhook model, rate limits, and mailbox delegation approach. |
| Zoho adapter | Port/config shape only | Confirm exact Zoho Mail API OAuth model, regional endpoints, webhook/push availability, and limits. |
| IMAP/SMTP adapter | Polling baseline | Confirm whether IMAP IDLE is reliable enough for each insurer host; treat only as polling-interval hint until proven. |
| Attachment limits | Route through IDocumentUploadService | Confirm provider-specific attachment size/type limits and IIP upload constraints. |
| Mailbox mapping admin | Repository shape defined | Confirm admin UX and approval workflow for adding/changing inbound mailboxes. |
| Channel conversation append | Deferred to existing channel write service | Confirm the canonical service method for appending inbound email to existing customer/claim conversation. |
| Integration user | Mapping includes IntegrationUserId | Confirm permission model and audit actor naming for asynchronous email workers. |
| Provider health | Uses IProviderHealthCheck | Add provider connectivity checks for Gmail, Zoho, and IMAP/SMTP when adapters are implemented. |

---

## 16. Completion report template

```text
Implementation document: IIP Email Ingestion and Dispatch — Provider-Agnostic Port.

Files to create or modify:
- src/InsuranceIntelligence.Application/Email/IEmailInboundProvider.cs
- src/InsuranceIntelligence.Application/Email/IEmailOutboundProvider.cs
- src/InsuranceIntelligence.Application/Email/EmailProviderModels.cs
- src/InsuranceIntelligence.Application/Email/EmailProviderValues.cs
- src/InsuranceIntelligence.Application/Email/IEmailInboundCheckpointRepository.cs
- src/InsuranceIntelligence.Application/Email/IEmailMailboxMappingRepository.cs
- src/InsuranceIntelligence.Application/Email/IEmailInboundDispatchRepository.cs
- src/InsuranceIntelligence.Application/Email/IEmailToClaimIntakeDispatcher.cs
- src/InsuranceIntelligence.Application/Email/EmailToClaimIntakeDispatcher.cs
- src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
- src/InsuranceIntelligence.Application/Common/Providers/ProviderCodes.cs
- src/InsuranceIntelligence.Application/Common/Audit/AuditEventTypeValues.cs
- src/InsuranceIntelligence.Infrastructure/Configuration/InfrastructureOptions.cs
- src/InsuranceIntelligence.Infrastructure/Configuration/GmailMailOptions.cs
- src/InsuranceIntelligence.Infrastructure/Configuration/ZohoMailOptions.cs
- src/InsuranceIntelligence.Infrastructure/Configuration/ImapSmtpMailOptions.cs
- src/InsuranceIntelligence.Infrastructure/Email/* provider adapters as deferred implementation boundaries
- src/InsuranceIntelligence.Functions/EmailIngestion/EmailInboxPollingFunction.cs
- src/InsuranceIntelligence.Functions/EmailIngestion/EmailPushNotificationFunction.cs
- src/InsuranceIntelligence.Tests/Application/Email/EmailToClaimIntakeDispatcherTests.cs
- src/InsuranceIntelligence.Tests/Infrastructure/Email/EmailProviderRegistrationTests.cs

Ports added:
- IEmailInboundProvider
- IEmailOutboundProvider
- IEmailMailboxMappingRepository
- IEmailInboundCheckpointRepository
- IEmailInboundDispatchRepository
- IEmailToClaimIntakeDispatcher

Provider impact:
- Graph becomes one email provider adapter, not the only real implementation.
- Gmail, Zoho, and IMAP/SMTP configuration shapes are defined.
- Concrete Gmail, Zoho, and IMAP/SMTP adapters are deferred and must fail closed until implemented.

Security impact:
- Inbound email does not bypass tenant resolution, permission checks, or audit writing.
- Mailbox-to-tenant mapping is server-side and durable.
- OAuth tokens, SMTP credentials, provider payloads, raw email bodies, and attachment bytes are not written to audit summaries.
- Mock email providers are rejected in protected runtimes.

Tenant/workspace isolation impact:
- Every inbound mailbox maps to exactly one tenant/workspace context.
- Dispatcher sets TenantContext from validated mailbox mapping and integration user before calling downstream services.
- Checkpoints and processed-message state are tenant/workspace/mailbox scoped.

Authentication/authorization impact:
- Asynchronous inbound email uses a real integration user, not an authenticated browser principal.
- IPermissionService still gates email processing and downstream claim/document operations.
- EmailInboundProcess permission is introduced for inbound dispatch execution.

AI/human-review impact:
- Email intake can create FNOL candidates and route documents.
- Email intake does not approve claims, settle claims, release payment, bind policies, set premiums, or make fraud determinations.
- Unclear messages route to manual review.

Data model or migration impact:
- Requires mailbox mapping table/entity.
- Requires inbound checkpoint table/entity.
- May require inbound email processing history table/entity.

Tests added or required:
- Provider-not-configured test.
- Tenant resolution from mailbox mapping test.
- Polling-baseline test.
- Attachment routes through IDocumentUploadService test.
- Audit-write test for inbound email processing.
- Protected runtime rejects Mock provider test.

TODOs:
- Confirm Gmail API auth and webhook model.
- Confirm Zoho Mail API auth and webhook model.
- Confirm attachment size/type limits per provider.
- Confirm whether IMAP IDLE is reliable enough for any configured host.
- Confirm canonical channel-conversation append service.

No fake data confirmation:
- No simulated inbox behavior is introduced into protected runtimes.
- Provider-specific adapters must return ProviderNotConfiguredException or ProviderUnavailableException rather than fake messages or fake sends.

Recommended next step:
- Implement the mailbox mapping and checkpoint persistence first, then refactor the existing Graph adapter to implement IEmailInboundProvider/IEmailOutboundProvider before adding Gmail, Zoho, or IMAP/SMTP adapters.
```

# IIP Notification Send Execution Service

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Status:** Implementation-ready service output for repository application layer  
**Date:** July 2026  

## Changelog

```text
- Fix 2: Replaced the literal approval status string "approved" with ApprovalStatusValues.Approved and added the Application.Approvals namespace import, because the approval decision service document is the canonical source for approval status constants.
```

---

## Shared rules used by this service output

This output follows the same implementation pattern as the existing IIP Application-layer service-output documents for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales & Leads, Audit, Claim Intake/Workflow Transitions, and Integration Write-Back.

```text
1. Resolve tenant/workspace from ITenantContextAccessor.
2. Enforce the relevant backend permission before any read/write.
3. Apply tenantId and workspaceId filters in the repository/provider boundary.
4. Return explicit DTOs and status values; do not invent frontend fields.
5. Write durable audit events for every state-changing operation and sensitive read.
6. Never fabricate records when no data exists.
7. Never let an AI/system actor be the final actor for a consequential insurance decision.
8. Surface missing providers as ProviderNotConfiguredException and unavailable providers as ProviderUnavailableException.
9. Use ISO 8601 round-trip timestamps for frontend wire strings.
10. Keep Domain free of EF Core, Azure SDKs, ASP.NET Core, and provider SDK dependencies.
11. Do not expose raw customer communication payloads, provider payloads, message bodies, secrets, tokens, prompts, or credentials in audit summaries or client-facing errors.
12. A notification draft must never be sent unless a real human approval decision is recorded and verified.
```

Fixed inputs used unchanged:

```text
INotificationDraftService
NotificationDraftCreateRequest
NotificationDraftResult
ApprovalDecisionService
ApprovalDecisionGuard.EnsureHumanForConsequentialDecision
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
AuditEventTypeValues.CustomerCommunicationSent
```

This document is additive. It does not redesign the corrected `CommandCenterQueryService`, `INotificationDraftService`, or the approval decision service.

---

## 1. Scope of this pass

This pass implements **approved notification send execution**:

```text
INotificationSendService.SendApprovedDraftAsync(draftId, ct)
```

Implemented capabilities:

```text
- Load a persisted notification draft by draftId using tenant/workspace scope.
- Enforce communications.send permission.
- Verify that the draft has a linked, recorded, approved human approval decision.
- Reuse ApprovalDecisionGuard.EnsureHumanForConsequentialDecision rather than inventing a separate human-boundary check.
- Check the configured communication provider health before attempting a send.
- Call a real communication sending provider port.
- Require real provider confirmation before marking a draft as sent.
- Mark draft status as sent on success.
- Mark draft status as send_failed on provider-not-configured, provider-unavailable, or provider-confirmation failure.
- Write audit events for send attempted, send succeeded, and send failed.
- Return a send result with provider confirmation metadata safe for the application layer.
```

Not implemented in this pass:

```text
- The real Microsoft Graph send adapter implementation.
- SMS or WhatsApp provider adapters.
- Retry/backoff orchestration.
- Idempotency-key generation and duplicate-send prevention beyond repository hooks.
- Controllers/API routes.
- Frontend code.
- Changes to notification draft creation shape.
- Changes to ApprovalDecisionService or ApprovalDecisionGuard.
```

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create a parallel architecture if equivalent files already exist.

```text
src/InsuranceIntelligence.Application/Communications/INotificationSendService.cs
src/InsuranceIntelligence.Application/Communications/NotificationSendService.cs
src/InsuranceIntelligence.Application/Communications/NotificationSendModels.cs
src/InsuranceIntelligence.Application/Communications/INotificationDraftSendRepository.cs
src/InsuranceIntelligence.Application/Communications/ICommunicationSendingProvider.cs
src/InsuranceIntelligence.Application/Communications/CommunicationSendingModels.cs
src/InsuranceIntelligence.Application/Communications/NotificationProviderCodeResolver.cs
src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
src/InsuranceIntelligence.Tests/Application/Communications/NotificationSendServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Communications/NotificationSendContractTests.cs
```

Infrastructure adapter placeholder for a future pass:

```text
src/InsuranceIntelligence.Infrastructure/Providers/Graph/GraphCommunicationSendingProvider.cs
```

This pass defines the `ICommunicationSendingProvider` port only. The concrete real adapter is insurer/provider-specific and is intentionally out of scope.

---

## 3. Permission constant

Add this permission constant if it does not already exist:

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static partial class Permissions
{
    public const string CommunicationsSend = "communications.send";
}
```

Rules:

```text
- communications.send is required before attempting to send any approved notification draft.
- communications.send does not replace the human approval requirement.
- A caller with communications.send still cannot send a draft unless the linked approval decision was made by a human.
```

---

## 4. Service contract

```csharp
namespace InsuranceIntelligence.Application.Communications;

public interface INotificationSendService
{
    Task<NotificationSendResult> SendApprovedDraftAsync(
        string draftId,
        CancellationToken cancellationToken);
}
```

---

## 5. Notification send models

```csharp
namespace InsuranceIntelligence.Application.Communications;

public sealed record NotificationSendResult(
    string DraftId,
    string Status,
    string Channel,
    string? ProviderMessageId,
    string SentAt,
    bool RequiredHumanApproval,
    string ApprovedByUserId,
    string ApprovalId);

public static class NotificationDraftStatusValues
{
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Sent = "sent";
    public const string SendFailed = "send_failed";
    public const string Rejected = "rejected";
}
```

`// TODO: confirm` final draft/notification status enum:

```text
pending_approval | approved | sent | send_failed | rejected
```

The implementation below uses only this subset because it is the minimum required to connect persisted drafts, human approval, and send execution.

---

## 6. Draft send repository port

This repository is an Application-layer port. The concrete EF Core implementation belongs in Infrastructure and must persist to Azure SQL in protected runtimes.

```csharp
using InsuranceIntelligence.Domain.Approvals;

namespace InsuranceIntelligence.Application.Communications;

public interface INotificationDraftSendRepository
{
    Task<NotificationDraftForSendReadModel?> GetForSendAsync(
        string tenantId,
        string? workspaceId,
        string draftId,
        CancellationToken cancellationToken);

    Task<LinkedApprovalDecisionReadModel?> GetLinkedApprovalDecisionAsync(
        string tenantId,
        string? workspaceId,
        string approvalId,
        string draftId,
        CancellationToken cancellationToken);

    Task MarkSentAsync(
        NotificationDraftSentWriteModel model,
        CancellationToken cancellationToken);

    Task MarkSendFailedAsync(
        NotificationDraftSendFailedWriteModel model,
        CancellationToken cancellationToken);
}

public sealed record NotificationDraftForSendReadModel(
    string DraftId,
    string TenantId,
    string? WorkspaceId,
    string Status,
    string Channel,
    string? RecipientAddress,
    string? RecipientDisplayName,
    string? Subject,
    string Preview,
    string Body,
    string RelatedEntityType,
    string RelatedEntityId,
    bool RequiresApproval,
    string LinkedApprovalId,
    string RequestedActionType,
    DateTimeOffset CreatedAt);

public sealed record LinkedApprovalDecisionReadModel(
    string ApprovalId,
    string DraftId,
    string Status,
    string DecidedByUserId,
    DecisionActorType ActorType,
    DateTimeOffset DecidedAt,
    bool RequiresHumanDecision,
    string RequestedActionType);

public sealed record NotificationDraftSentWriteModel(
    string DraftId,
    string TenantId,
    string? WorkspaceId,
    string ProviderCode,
    string ProviderMessageId,
    DateTimeOffset SentAt,
    string SentByUserId,
    string CorrelationId);

public sealed record NotificationDraftSendFailedWriteModel(
    string DraftId,
    string TenantId,
    string? WorkspaceId,
    string ProviderCode,
    string SafeFailureReason,
    DateTimeOffset FailedAt,
    string FailedByUserId,
    string CorrelationId);
```

Repository rules:

```text
- GetForSendAsync must filter by tenantId and workspaceId.
- GetLinkedApprovalDecisionAsync must verify the approval belongs to the same tenant/workspace and draft.
- MarkSentAsync must update the stored draft/notification status to sent.
- MarkSendFailedAsync must update the stored draft/notification status to send_failed.
- The repository may store full message body where needed for sending, but audit summaries and client errors must never include the full body or provider payload.
```

---

## 7. Communication sending provider port

The port name follows the current codebase's Infrastructure registration convention:

```text
ICommunicationSendingProvider
GraphCommunicationSendingProvider
```

The concrete `GraphCommunicationSendingProvider` is out of scope for this pass.

```csharp
namespace InsuranceIntelligence.Application.Communications;

public interface ICommunicationSendingProvider
{
    Task<CommunicationSendProviderResult> SendAsync(
        CommunicationSendProviderRequest request,
        CancellationToken cancellationToken);
}

public sealed record CommunicationSendProviderRequest(
    string TenantId,
    string? WorkspaceId,
    string DraftId,
    string Channel,
    string? RecipientAddress,
    string? RecipientDisplayName,
    string? Subject,
    string Body,
    string RelatedEntityType,
    string RelatedEntityId,
    string CorrelationId);

public sealed record CommunicationSendProviderResult(
    bool Confirmed,
    string? ProviderMessageId,
    DateTimeOffset? SentAt,
    string SafeProviderSummary);
```

Provider rules:

```text
- Confirmed must be true only when the provider has accepted or sent the message and returned a real confirmation.
- ProviderMessageId must come from the real provider where available.
- SafeProviderSummary must not contain raw request payload, full customer message body, credentials, tokens, or provider exception payloads.
- The service must not treat absence of confirmation as success.
```

---

## 8. Provider code resolver

```csharp
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Providers;

namespace InsuranceIntelligence.Application.Communications;

public static class NotificationProviderCodeResolver
{
    public static string ResolveProviderCode(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ProviderNotConfiguredException("Notification channel is not configured for this draft.");
        }

        return channel.Trim().ToLowerInvariant() switch
        {
            "email" => ProviderCodes.GraphMail,
            "graph_mail" => ProviderCodes.GraphMail,
            "graph_email" => ProviderCodes.GraphMail,
            "customer_update" => ProviderCodes.GraphMail,
            "sms" => "sms_provider",
            "whatsapp" => "whatsapp_business",
            _ => throw new ProviderNotConfiguredException($"No communication provider is configured for channel '{channel}'.")
        };
    }
}
```

`// TODO: confirm` channel strategy:

```text
- The current Command Center draft flow uses Channel = customer_update.
- This implementation maps customer_update to graph_mail as a conservative first pass for email-based customer updates.
- Confirm whether SMS/WhatsApp sends should use the same INotificationSendService or separate channel-specific services.
```

---

## 9. Implementation

```csharp
using InsuranceIntelligence.Application.Approvals;
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;
using InsuranceIntelligence.Domain.Approvals;

namespace InsuranceIntelligence.Application.Communications;

internal sealed class NotificationSendService : INotificationSendService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IProviderHealthCheck _providerHealthCheck;
    private readonly ICommunicationSendingProvider _sendingProvider;
    private readonly INotificationDraftSendRepository _repository;
    private readonly IAuditWriter _auditWriter;

    public NotificationSendService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IProviderHealthCheck providerHealthCheck,
        ICommunicationSendingProvider sendingProvider,
        INotificationDraftSendRepository repository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _providerHealthCheck = providerHealthCheck;
        _sendingProvider = sendingProvider;
        _repository = repository;
        _auditWriter = auditWriter;
    }

    public async Task<NotificationSendResult> SendApprovedDraftAsync(
        string draftId,
        CancellationToken cancellationToken)
    {
        ValidateDraftId(draftId);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.CommunicationsSend, cancellationToken);

        var draft = await _repository.GetForSendAsync(
            context.TenantId,
            context.WorkspaceId,
            draftId,
            cancellationToken);

        if (draft is null)
        {
            throw new NotFoundOrNotVisibleException("Notification draft was not found or is not visible.");
        }

        EnsureDraftIsApproved(draft);

        var approval = await _repository.GetLinkedApprovalDecisionAsync(
            context.TenantId,
            context.WorkspaceId,
            draft.LinkedApprovalId,
            draft.DraftId,
            cancellationToken);

        EnsureHumanApprovalDecisionExists(draft, approval);

        var providerCode = NotificationProviderCodeResolver.ResolveProviderCode(draft.Channel);

        await WriteAuditAsync(
            context,
            draft,
            action: "customer_communication_send_attempted",
            summary: BuildSafeSummary(draft, "Customer communication send attempted."),
            reason: null,
            cancellationToken);

        var providerHealth = await _providerHealthCheck.CheckAsync(providerCode, cancellationToken);
        if (providerHealth.Status == ProviderStatus.NotConfigured)
        {
            await MarkFailedAndAuditAsync(
                context,
                draft,
                providerCode,
                "Communication provider is not configured.",
                cancellationToken);

            throw new ProviderNotConfiguredException("Communication provider is not configured for this notification channel.");
        }

        if (providerHealth.Status == ProviderStatus.Unavailable)
        {
            await MarkFailedAndAuditAsync(
                context,
                draft,
                providerCode,
                "Communication provider is unavailable.",
                cancellationToken);

            throw new ProviderUnavailableException("Communication provider is unavailable.");
        }

        CommunicationSendProviderResult providerResult;
        try
        {
            providerResult = await _sendingProvider.SendAsync(
                new CommunicationSendProviderRequest(
                    TenantId: context.TenantId,
                    WorkspaceId: context.WorkspaceId,
                    DraftId: draft.DraftId,
                    Channel: draft.Channel,
                    RecipientAddress: draft.RecipientAddress,
                    RecipientDisplayName: draft.RecipientDisplayName,
                    Subject: draft.Subject,
                    Body: draft.Body,
                    RelatedEntityType: draft.RelatedEntityType,
                    RelatedEntityId: draft.RelatedEntityId,
                    CorrelationId: context.CorrelationId),
                cancellationToken);
        }
        catch (ProviderUnavailableException ex)
        {
            await MarkFailedAndAuditAsync(
                context,
                draft,
                providerCode,
                SafeFailure(ex.Message),
                cancellationToken);

            throw;
        }
        catch (Exception)
        {
            await MarkFailedAndAuditAsync(
                context,
                draft,
                providerCode,
                "Communication provider send failed.",
                cancellationToken);

            throw new ProviderUnavailableException("Communication provider send failed. Use the correlation ID when contacting support.");
        }

        if (!providerResult.Confirmed || string.IsNullOrWhiteSpace(providerResult.ProviderMessageId))
        {
            await MarkFailedAndAuditAsync(
                context,
                draft,
                providerCode,
                "Communication provider did not return a send confirmation.",
                cancellationToken);

            throw new ProviderUnavailableException("Communication provider did not confirm the send.");
        }

        var sentAt = providerResult.SentAt ?? DateTimeOffset.UtcNow;

        await _repository.MarkSentAsync(
            new NotificationDraftSentWriteModel(
                DraftId: draft.DraftId,
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                ProviderCode: providerCode,
                ProviderMessageId: providerResult.ProviderMessageId,
                SentAt: sentAt,
                SentByUserId: context.UserId,
                CorrelationId: context.CorrelationId),
            cancellationToken);

        await WriteAuditAsync(
            context,
            draft,
            action: "customer_communication_sent",
            summary: BuildSafeSummary(draft, "Customer communication sent after human approval."),
            reason: null,
            cancellationToken);

        return new NotificationSendResult(
            DraftId: draft.DraftId,
            Status: NotificationDraftStatusValues.Sent,
            Channel: draft.Channel,
            ProviderMessageId: providerResult.ProviderMessageId,
            SentAt: sentAt.ToString("O"),
            RequiredHumanApproval: draft.RequiresApproval,
            ApprovedByUserId: approval.DecidedByUserId,
            ApprovalId: approval.ApprovalId);
    }

    private static void ValidateDraftId(string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
        {
            throw new ValidationException("draftId is required.");
        }

        if (draftId.Length > 128)
        {
            throw new ValidationException("draftId exceeds maximum length.");
        }
    }

    private static void EnsureDraftIsApproved(NotificationDraftForSendReadModel draft)
    {
        if (!string.Equals(draft.Status, NotificationDraftStatusValues.Approved, StringComparison.Ordinal))
        {
            throw new WorkflowConflictException("Notification draft must be approved before it can be sent.");
        }

        if (string.IsNullOrWhiteSpace(draft.LinkedApprovalId))
        {
            throw new WorkflowConflictException("Notification draft has no linked approval decision.");
        }
    }

    private static void EnsureHumanApprovalDecisionExists(
        NotificationDraftForSendReadModel draft,
        LinkedApprovalDecisionReadModel? approval)
    {
        if (approval is null)
        {
            throw new WorkflowConflictException("Notification draft has not been approved by a human approver.");
        }

        if (!string.Equals(approval.Status, ApprovalStatusValues.Approved, StringComparison.Ordinal))
        {
            throw new WorkflowConflictException("Linked approval decision is not approved.");
        }

        ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
            requiresHumanDecision: draft.RequiresApproval || approval.RequiresHumanDecision,
            actorType: approval.ActorType,
            requestedActionType: string.IsNullOrWhiteSpace(approval.RequestedActionType)
                ? draft.RequestedActionType
                : approval.RequestedActionType);
    }

    private async Task MarkFailedAndAuditAsync(
        TenantContext context,
        NotificationDraftForSendReadModel draft,
        string providerCode,
        string safeFailureReason,
        CancellationToken cancellationToken)
    {
        await _repository.MarkSendFailedAsync(
            new NotificationDraftSendFailedWriteModel(
                DraftId: draft.DraftId,
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                ProviderCode: providerCode,
                SafeFailureReason: SafeFailure(safeFailureReason),
                FailedAt: DateTimeOffset.UtcNow,
                FailedByUserId: context.UserId,
                CorrelationId: context.CorrelationId),
            cancellationToken);

        await WriteAuditAsync(
            context,
            draft,
            action: "customer_communication_send_failed",
            summary: BuildSafeSummary(draft, "Customer communication send failed."),
            reason: SafeFailure(safeFailureReason),
            cancellationToken);
    }

    private Task WriteAuditAsync(
        TenantContext context,
        NotificationDraftForSendReadModel draft,
        string action,
        string summary,
        string? reason,
        CancellationToken cancellationToken)
    {
        return _auditWriter.WriteAsync(
            new AuditEventCreateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                EventType: AuditEventTypeValues.CustomerCommunicationSent,
                ActorType: "user",
                ActorUserId: context.UserId,
                Action: action,
                EntityType: "notification_draft",
                EntityId: draft.DraftId,
                Summary: summary,
                Reason: reason,
                CorrelationId: context.CorrelationId,
                OccurredAt: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private static string BuildSafeSummary(NotificationDraftForSendReadModel draft, string prefix)
    {
        return RedactAndTruncate(
            $"{prefix} Draft {draft.DraftId}; related {draft.RelatedEntityType} {draft.RelatedEntityId}; channel {draft.Channel}.",
            500);
    }

    private static string SafeFailure(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Communication send failed.";
        }

        return RedactAndTruncate(value, 500);
    }

    private static string RedactAndTruncate(string value, int maxLength)
    {
        var sanitized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("Bearer ", "Bearer [redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("SharedAccessSignature", "[redacted-sas]", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }
}
```

---

## 10. Tests to add

```text
NotificationSendServiceTests
- SendApprovedDraftAsync_RequiresCommunicationsSendPermission
- SendApprovedDraftAsync_RejectsWhenDraftNotApproved
- SendApprovedDraftAsync_RejectsWhenLinkedApprovalIsMissing
- SendApprovedDraftAsync_RejectsWhenLinkedApprovalWasNotHuman
- SendApprovedDraftAsync_RejectsWhenProviderNotConfigured
- SendApprovedDraftAsync_RejectsWhenProviderUnavailable
- SendApprovedDraftAsync_SucceedsWritesAuditAndUpdatesStatus
- SendApprovedDraftAsync_SendFailureMarksDraftSendFailed
- SendApprovedDraftAsync_SendFailureDoesNotSilentlyRetry
- SendApprovedDraftAsync_DoesNotMarkSentWithoutProviderConfirmation
- SendApprovedDraftAsync_DoesNotExposeBodyOrProviderPayloadInAudit
```

Contract test:

```text
NotificationSendContractTests
- NotificationSendResult serializes to draftId, status, channel, providerMessageId, sentAt, requiredHumanApproval, approvedByUserId, approvalId
- status is sent on successful execution
- sentAt uses ISO 8601 round-trip format
```

Provider failure tests:

```text
- ProviderStatus.NotConfigured causes MarkSendFailedAsync, failed audit, then ProviderNotConfiguredException.
- ProviderStatus.Unavailable causes MarkSendFailedAsync, failed audit, then ProviderUnavailableException.
- Provider SendAsync throwing ProviderUnavailableException causes MarkSendFailedAsync, failed audit, and no retry.
```

Human approval boundary tests:

```text
- Linked approval with ActorType.Agent is rejected through ApprovalDecisionGuard.
- Linked approval with ActorType.System is rejected through ApprovalDecisionGuard.
- Linked approval with ActorType.Integration is rejected through ApprovalDecisionGuard.
- Linked approval with ActorType.Human and Status = approved is allowed.
```

No silent retry test requirement:

```text
The test must verify ICommunicationSendingProvider.SendAsync is called exactly once for a single SendApprovedDraftAsync call when the provider throws.
```

Example test skeleton:

```csharp
[Fact]
public async Task SendApprovedDraftAsync_SendFailureDoesNotSilentlyRetry()
{
    var provider = new Mock<ICommunicationSendingProvider>();
    provider.Setup(x => x.SendAsync(It.IsAny<CommunicationSendProviderRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ProviderUnavailableException("Graph send unavailable."));

    var service = BuildService(provider: provider.Object);

    await Assert.ThrowsAsync<ProviderUnavailableException>(() =>
        service.SendApprovedDraftAsync("draft-1", CancellationToken.None));

    provider.Verify(x => x.SendAsync(
        It.IsAny<CommunicationSendProviderRequest>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

Example failure-persistence test:

```csharp
[Fact]
public async Task SendApprovedDraftAsync_SendFailureMarksDraftSendFailed()
{
    var repository = new Mock<INotificationDraftSendRepository>();
    repository.SetupApprovedDraft("tenant-a", "workspace-a", "draft-1");

    var provider = new Mock<ICommunicationSendingProvider>();
    provider.Setup(x => x.SendAsync(It.IsAny<CommunicationSendProviderRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ProviderUnavailableException("Graph unavailable."));

    var service = BuildService(repository: repository.Object, provider: provider.Object);

    await Assert.ThrowsAsync<ProviderUnavailableException>(() =>
        service.SendApprovedDraftAsync("draft-1", CancellationToken.None));

    repository.Verify(x => x.MarkSendFailedAsync(
        It.Is<NotificationDraftSendFailedWriteModel>(m =>
            m.DraftId == "draft-1" &&
            m.TenantId == "tenant-a" &&
            m.WorkspaceId == "workspace-a"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

Example success test:

```csharp
[Fact]
public async Task SendApprovedDraftAsync_SucceedsWritesAuditAndUpdatesStatus()
{
    var repository = new Mock<INotificationDraftSendRepository>();
    repository.SetupApprovedDraft("tenant-a", "workspace-a", "draft-1");

    var auditWriter = new Mock<IAuditWriter>();
    var provider = new Mock<ICommunicationSendingProvider>();
    provider.Setup(x => x.SendAsync(It.IsAny<CommunicationSendProviderRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new CommunicationSendProviderResult(
            Confirmed: true,
            ProviderMessageId: "graph-msg-123",
            SentAt: DateTimeOffset.UtcNow,
            SafeProviderSummary: "Graph accepted message."));

    var service = BuildService(repository: repository.Object, provider: provider.Object, auditWriter: auditWriter.Object);

    var result = await service.SendApprovedDraftAsync("draft-1", CancellationToken.None);

    Assert.Equal(NotificationDraftStatusValues.Sent, result.Status);
    Assert.Equal("graph-msg-123", result.ProviderMessageId);

    repository.Verify(x => x.MarkSentAsync(
        It.Is<NotificationDraftSentWriteModel>(m =>
            m.DraftId == "draft-1" &&
            m.ProviderMessageId == "graph-msg-123"),
        It.IsAny<CancellationToken>()), Times.Once);

    auditWriter.Verify(x => x.WriteAsync(
        It.Is<AuditEventCreateRequest>(e =>
            e.EventType == AuditEventTypeValues.CustomerCommunicationSent &&
            e.Action == "customer_communication_sent" &&
            e.EntityId == "draft-1"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

---

## 11. TODOs and confirmation points

| Area | Current implementation decision | TODO / confirmation needed |
|---|---|---|
| Draft status enum | Uses `pending_approval`, `approved`, `sent`, `send_failed`, `rejected` | Confirm exact persisted draft/notification status values. |
| Approval linkage | Requires a linked approval decision with `Status = approved` and human actor | Confirm exact database relationship between notification drafts and approval tasks. |
| Channel mapping | Maps `email`, `graph_mail`, `graph_email`, and `customer_update` to `graph_mail` | Confirm whether `customer_update` should be a category separate from delivery channel. |
| SMS/WhatsApp | Uses future provider codes `sms_provider` and `whatsapp_business` | Confirm whether SMS/WhatsApp sends use this service or channel-specific variants. |
| Provider adapter | Defines `ICommunicationSendingProvider`; concrete Graph adapter out of scope | Confirm real Microsoft Graph send method, mailbox scoping, and tenant mailbox configuration. |
| Retry/backoff | No retry in this pass | Design retry policy separately with idempotency keys and duplicate-send protection. |
| Idempotency | No idempotency key strategy in this pass | Define idempotency key and duplicate-send prevention before automatic retries. |
| Audit detail | Safe summaries only; no raw message body | Confirm whether a separate privileged audit detail view should show redacted message preview. |
```

---

## 12. Completion report template

```text
Service implemented: INotificationSendService.

Files to create or modify:
- src/InsuranceIntelligence.Application/Communications/INotificationSendService.cs
- src/InsuranceIntelligence.Application/Communications/NotificationSendService.cs
- src/InsuranceIntelligence.Application/Communications/NotificationSendModels.cs
- src/InsuranceIntelligence.Application/Communications/INotificationDraftSendRepository.cs
- src/InsuranceIntelligence.Application/Communications/ICommunicationSendingProvider.cs
- src/InsuranceIntelligence.Application/Communications/CommunicationSendingModels.cs
- src/InsuranceIntelligence.Application/Communications/NotificationProviderCodeResolver.cs
- src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
- src/InsuranceIntelligence.Tests/Application/Communications/NotificationSendServiceTests.cs
- src/InsuranceIntelligence.Tests/Application/Communications/NotificationSendContractTests.cs

Actual logic implemented:
- Enforces communications.send.
- Loads draft by tenant/workspace scope.
- Requires draft status approved.
- Requires a linked approved human approval decision.
- Reuses ApprovalDecisionGuard.EnsureHumanForConsequentialDecision.
- Checks provider health before send.
- Calls ICommunicationSendingProvider.
- Requires real provider confirmation before marking sent.
- Marks send_failed on not configured, unavailable, provider exception, or missing confirmation.
- Writes customer communication audit events for attempted, sent, and failed outcomes.

Security impact:
- Prevents sending customer communications without backend permission.
- Prevents AI/system/integration actors from being treated as final approvers for consequential communications.
- Prevents raw communication body, provider payload, credentials, tokens, and prompts from leaking into audit summaries or client-facing errors.

Tenant/workspace isolation impact:
- Draft load, approval decision lookup, sent update, and failed update are all tenant/workspace scoped.
- Client-supplied tenant/workspace values are not accepted.

Authentication/authorization impact:
- Adds communications.send permission requirement.
- Human approval decision remains separate from send permission.

Provider impact:
- Adds ICommunicationSendingProvider port.
- Uses IProviderHealthCheck before send.
- Does not implement concrete GraphCommunicationSendingProvider in this pass.

Audit impact:
- Writes AuditEventTypeValues.CustomerCommunicationSent with action customer_communication_send_attempted.
- Writes AuditEventTypeValues.CustomerCommunicationSent with action customer_communication_sent.
- Writes AuditEventTypeValues.CustomerCommunicationSent with action customer_communication_send_failed.

Data model or migration impact:
- Requires notification draft table/status fields.
- Requires linked approval ID on notification draft.
- Requires provider message ID and sent/send_failed timestamps if not already present.

AI/human approval impact:
- Completes the human-approved draft execution path.
- Does not allow AI-generated drafts to be sent unless a real human approval decision exists.

Tests added or required:
- SendApprovedDraftAsync_RequiresCommunicationsSendPermission
- SendApprovedDraftAsync_RejectsWhenDraftNotApproved
- SendApprovedDraftAsync_RejectsWhenLinkedApprovalIsMissing
- SendApprovedDraftAsync_RejectsWhenLinkedApprovalWasNotHuman
- SendApprovedDraftAsync_RejectsWhenProviderNotConfigured
- SendApprovedDraftAsync_RejectsWhenProviderUnavailable
- SendApprovedDraftAsync_SucceedsWritesAuditAndUpdatesStatus
- SendApprovedDraftAsync_SendFailureMarksDraftSendFailed
- SendApprovedDraftAsync_SendFailureDoesNotSilentlyRetry
- SendApprovedDraftAsync_DoesNotMarkSentWithoutProviderConfirmation
- NotificationSendContractTests

TODOs:
- Confirm draft/notification status enum.
- Confirm exact draft-to-approval database relationship.
- Confirm channel mapping and SMS/WhatsApp strategy.
- Implement real GraphCommunicationSendingProvider.
- Design retry/backoff and idempotency before automatic retry support.

Missing dependencies:
- Concrete EF repository for INotificationDraftSendRepository.
- Concrete GraphCommunicationSendingProvider.
- Confirmed mailbox/provider configuration model.

No fake data confirmation:
- No fake send path is introduced.
- No optimistic success path is introduced.
- The service never marks sent unless ICommunicationSendingProvider returns real confirmation.
- ProviderNotConfiguredException and ProviderUnavailableException are surfaced instead of fake success.

Recommended next service:
- Implement the Infrastructure adapter pass for GraphCommunicationSendingProvider and the EF notification draft send repository.
```

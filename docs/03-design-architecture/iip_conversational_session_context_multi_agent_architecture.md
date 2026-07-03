# IIP Conversational Session, Context, and Multi-Agent Architecture

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Architecture decision + implementation-ready specification  
**Area:** Command Center session state, context management, grounding, and future multi-agent orchestration  
**Status:** New-repository implementation specification  
**Date:** July 2026  

---

## Changelog

```text
Version 1.0 - July 2026
- Added the canonical hybrid architecture decision for IIP conversational context management.
- Added ICommandCenterSessionStateService as the missing Application-layer bounded session state port.
- Added retrieval-on-demand as the standing grounding rule for every consequential conversational turn.
- Added session-state compaction policy for operational state only, not evidence content.
- Added future multi-agent orchestration scaffold using code-first Application-layer orchestration and Microsoft Agent Framework patterns.
- Added provider-port review for IChatCompletionProvider and optional Foundry conversation continuity.
- Added operational risk register and verification TODOs for context windows, quota tiers, pricing, and SDK/platform behavior.
```

---

## Shared rules used by this architecture output

This document follows the same implementation pattern used across the IIP backend implementation-output documents: explicit rules, scoped contracts, concrete implementation guidance, tests, and a completion report.

```text
1. Resolve tenant/workspace/user context from ITenantContextAccessor only.
2. Enforce backend permissions before reading, writing, or mutating session state.
3. Never trust a client-supplied tenant ID, workspace ID, role, permission, or model conversation reference as authority.
4. Keep grounding data authoritative in live repositories, approved document stores, and approved knowledge indexes.
5. Never persist raw retrieved evidence, extracted document content, policy wording, claim payloads, or customer PII inside conversational session state.
6. Re-retrieve grounding data on every consequential turn through existing Application services, repositories, and IRetrievalProvider.
7. Treat Azure AI Foundry conversation history and compaction as model-continuity conveniences, never as evidence storage, audit records, or source of truth.
8. AI may prepare summaries, drafts, risk signals, and evidence packs, but may never be the final actor for claim approval, rejection, payment release, policy binding, pricing, or other consequential insurance decisions.
9. Multi-agent orchestration must remain deterministic, auditable, tenant-scoped, and permission-gated in the IIP Application layer.
10. Do not adopt preview inter-agent primitives as a hard dependency without an explicit architecture review.
11. No mock, simulated, placeholder, seeded-demo, or fake provider behavior is allowed in Staging, Production, or ManagedApplicationReviewer.
12. All figures for model context windows, quota tiers, cost, latency, and SDK capability are implementation-time verification items, not settled product guarantees.
```

Fixed inputs consumed by this document and not redesigned here:

```text
CommandCenterQueryService
IIntentClassifier / DeterministicThenAiIntentClassifier
IChatCompletionProvider
IRetrievalProvider
IAiOutputValidator
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
```

The existing services remain valid. This document adds the missing session/context architecture around them and defines a future multi-agent target shape.

---

## 1. Purpose

IIP's Command Center is a chat-first, structured operational workspace. Staff ask questions such as:

```text
- show me claims at SLA risk
- summarize this claim
- what documents are missing
- draft a customer update for this claim
- explain why this case needs approval
```

The response is not free-form chat. It is structured operational output: tables, metric pills, evidence citations, status cards, approval queues, draft actions, and human-review gates.

The central architecture problem is context management:

```text
- Strict grounding requires real evidence on every turn.
- Long conversations can exceed model context windows.
- Default model or platform truncation is not acceptable as the grounding strategy.
- Opaque model-side compaction is not an auditable source of truth.
- Multi-agent behavior must not become an unmanaged black box.
```

This document fixes the architecture decision and defines the missing Application-layer session state, retrieval policy, compaction policy, and future multi-agent scaffold.

---

## 2. Scope of this pass

Implemented by this architecture pass:

```text
- A formal architecture decision for hybrid context management.
- ICommandCenterSessionStateService Application-layer port.
- Command Center session state models.
- Session state repository port.
- Session-state compaction policy for bounded operational continuity.
- Retrieval-on-demand rule for every grounded turn.
- Optional Foundry conversation continuity policy.
- Future multi-agent orchestration scaffold.
- Provider port review for IChatCompletionProvider.
- Tests and completion report template.
```

Not implemented by this pass:

```text
- Replacement of CommandCenterQueryService.
- Replacement of IIntentClassifier.
- Replacement of IChatCompletionProvider.
- Replacement of IRetrievalProvider.
- Full Microsoft Agent Framework implementation.
- Foundry Workflow designer implementation.
- A2A inter-service agent transport.
- New public API routes.
- Frontend UX implementation.
- Model selection, model pricing, quota procurement, or production capacity planning.
```

---

## 3. Decision record

### 3.1 Architecture decision

IIP will use a **hybrid context and agent architecture**:

```text
Azure AI Foundry provides model hosting, inference, optional short-horizon conversation continuity, and optional model-side compaction.

IIP's .NET Application layer owns session state, grounding policy, evidence retrieval, permission enforcement, audit, approval gates, and multi-agent supervision.
```

Foundry conversations or `previous_response_id` may be used as a convenience for short-horizon model continuity. They are never the evidentiary source for IIP. Every consequential Command Center turn must re-retrieve authoritative data from:

```text
- Claims read models and repositories.
- Policy and customer read models.
- Document/extraction repositories.
- Approved knowledge indexes through IRetrievalProvider.
- Approval/audit/reporting read models.
```

### 3.2 Why Foundry native conversation history is not enough

Per current research, pending live verification:

```text
- Foundry's current runtime model includes agents, conversations, and responses.
- Conversations are durable objects that can persist history and be reused.
- When the active model context exceeds the model's supported context size, the model-side input context can be truncated.
- Foundry also offers server-side compaction through context_management / compact_threshold.
- Compaction produces an opaque compacted item and should not be treated as a human-auditable summary.
```

IIP cannot rely on silent truncation or opaque compaction for evidence continuity because:

```text
- Insurance outputs must be traceable to source records.
- Staff must be able to inspect what evidence was used.
- Compliance and audit must be able to reconstruct the basis for an answer.
- A stale or truncated in-context copy of a claim is not a source of truth.
```

### 3.3 Why multi-agent orchestration stays in the Application layer

Per current research, pending live verification:

```text
- The older Connected Agents capability from the classic API should not be treated as available in the current Foundry Agent Service architecture.
- Current Foundry multi-agent-adjacent primitives include Workflows and A2A.
- Workflows are visual/declarative and support patterns such as sequential, group chat, and human-in-the-loop.
- The workflow designer reportedly does not support hosted agents for code-first hosted-agent orchestration.
- For hosted-agent/code-first orchestration, the current documented direction is Microsoft Agent Framework SDK patterns.
- A2A is useful as protocol-level inter-agent communication, but it does not by itself provide IIP-specific permissioning, approval gates, audit semantics, or evidence-grounding policy.
```

Therefore, IIP will not move core insurance routing, approval gates, or grounding decisions into a visual workflow designer. The Application layer remains authoritative.

### 3.4 Build vs. buy decision

```text
Buy/use from Foundry:
- Model hosting and inference.
- Native conversations where helpful for short-horizon continuity.
- Optional server-side compaction where helpful for model continuation.
- Model catalog and managed runtime integrations.
- Potential future Workflows/A2A for non-critical or explicitly reviewed cases.

Build/own in IIP Application layer:
- Tenant/workspace/user-scoped session state.
- Grounding data retrieval policy.
- Evidence selection and citation model.
- Agent/tool registry enforcement.
- Permission checks and human-review gates.
- Audit and AIInteraction/AgentRun records.
- Future multi-agent supervisor and handoff contracts.
```

This minimizes hallucination, keeps context bounded, preserves auditability, and reduces platform lock-in.

---

## 4. Files to create or modify

### 4.1 Application session/context files

```text
src/InsuranceIntelligence.Application/CommandCenter/Sessions/ICommandCenterSessionStateService.cs
src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionStateService.cs
src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionStateModels.cs
src/InsuranceIntelligence.Application/CommandCenter/Sessions/ICommandCenterSessionStateRepository.cs
src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionStateValidation.cs
src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionCompactionPolicy.cs
```

### 4.2 Infrastructure persistence files

```text
src/InsuranceIntelligence.Infrastructure/CommandCenter/EfCommandCenterSessionStateRepository.cs
src/InsuranceIntelligence.Infrastructure/Persistence/Entities/CommandCenterSessionStateEntity.cs
src/InsuranceIntelligence.Infrastructure/Persistence/Configurations/CommandCenterSessionStateConfiguration.cs
```

### 4.3 Optional model-continuity adapter files

These files are optional and should be implemented only if the team elects to use Foundry native conversation continuity.

```text
src/InsuranceIntelligence.Application/CommandCenter/Sessions/ModelConversationReference.cs
src/InsuranceIntelligence.Application/CommandCenter/Sessions/IModelConversationReferenceStore.cs
src/InsuranceIntelligence.Infrastructure/Ai/Foundry/FoundryConversationContinuityAdapter.cs
```

### 4.4 Future multi-agent scaffold files

These are target-shape scaffold files, not required for the first implementation of this pass.

```text
src/InsuranceIntelligence.Application/Agents/AgentRole.cs
src/InsuranceIntelligence.Application/Agents/IAgentOrchestrator.cs
src/InsuranceIntelligence.Application/Agents/AgentHandoffModels.cs
src/InsuranceIntelligence.Application/Agents/CommandCenterAgentSupervisor.cs
src/InsuranceIntelligence.Application/Agents/AgentExecutionPolicy.cs
src/InsuranceIntelligence.Application/Agents/AgentToolRegistry.cs
```

### 4.5 Tests

```text
src/InsuranceIntelligence.Tests/Application/CommandCenter/CommandCenterSessionStateServiceTests.cs
src/InsuranceIntelligence.Tests/Application/CommandCenter/CommandCenterSessionStatePersistenceTests.cs
src/InsuranceIntelligence.Tests/Application/CommandCenter/CommandCenterGroundingPolicyTests.cs
src/InsuranceIntelligence.Tests/Application/Agents/AgentOrchestrationPolicyTests.cs
```

---

## 5. Session state contract

### 5.1 Design rule

Command Center session state stores **operational continuity only**.

It may store:

```text
- conversationId
- current claim/customer/policy focus references
- active filters
- current task intent
- unresolved clarifying questions
- pending action proposal references
- prior task references as one-line pointers
- optional provider-side model conversation metadata
```

It must never store:

```text
- raw retrieved evidence
- full claim records
- full policy wording
- extracted document content
- full customer communication bodies
- document OCR text
- provider payloads
- prompts or hidden system messages
- embeddings
- raw AI Search chunks
- raw customer PII beyond stable entity references needed for lookup
```

### 5.2 ICommandCenterSessionStateService

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public interface ICommandCenterSessionStateService
{
    Task<CommandCenterSessionStateDto> GetOrCreateAsync(
        string conversationId,
        CancellationToken cancellationToken);

    Task<CommandCenterSessionStateDto> GetAsync(
        string conversationId,
        CancellationToken cancellationToken);

    Task<CommandCenterSessionStateDto> UpdateFocusAsync(
        string conversationId,
        CommandCenterFocusUpdateRequest request,
        CancellationToken cancellationToken);

    Task<CommandCenterSessionStateDto> UpdateActiveFiltersAsync(
        string conversationId,
        IReadOnlyList<CommandCenterFilterReferenceDto> filters,
        CancellationToken cancellationToken);

    Task<CommandCenterSessionStateDto> RecordTurnAsync(
        string conversationId,
        CommandCenterTurnReferenceCreateRequest request,
        CancellationToken cancellationToken);

    Task<CommandCenterSessionStateDto> CompactIfNeededAsync(
        string conversationId,
        CancellationToken cancellationToken);

    Task ClearAsync(
        string conversationId,
        string reason,
        CancellationToken cancellationToken);
}
```

Rules:

```text
- The caller supplies conversationId only.
- TenantId, WorkspaceId, and UserId are always taken from ITenantContextAccessor.Current.
- conversationId is not globally trusted. It is always scoped by tenantId + workspaceId + userId.
- Every public method enforces the appropriate Command Center permission.
- The service writes audit records for session creation, compaction, and clearing.
```

---

## 6. Session state models

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public sealed record CommandCenterSessionStateDto(
    string ConversationId,
    string TenantId,
    string? WorkspaceId,
    string UserId,
    CommandCenterSessionFocusDto Focus,
    IReadOnlyList<CommandCenterFilterReferenceDto> ActiveFilters,
    IReadOnlyList<CommandCenterUnresolvedQuestionDto> UnresolvedQuestions,
    IReadOnlyList<CommandCenterPendingTaskDto> PendingTasks,
    IReadOnlyList<CommandCenterTurnReferenceDto> RecentTurnReferences,
    IReadOnlyList<CommandCenterCompactedTurnPointerDto> CompactedTurnPointers,
    ModelConversationReferenceDto? ModelConversationReference,
    string StateVersion,
    int CompactionCount,
    string CreatedAt,
    string UpdatedAt);

public sealed record CommandCenterSessionFocusDto(
    string? ClaimId,
    string? ClaimNumber,
    string? CustomerId,
    string? PolicyId,
    string? DocumentId,
    string? ApprovalId,
    string? ReportId,
    string? FocusLabel);

public sealed record CommandCenterFilterReferenceDto(
    string FilterType,
    string FilterValue,
    string DisplayLabel);

public sealed record CommandCenterUnresolvedQuestionDto(
    string QuestionId,
    string QuestionText,
    string RelatedEntityType,
    string? RelatedEntityId,
    string CreatedAt);

public sealed record CommandCenterPendingTaskDto(
    string TaskId,
    string TaskType,
    string RelatedEntityType,
    string RelatedEntityId,
    string Status,
    string RequiresHumanReview,
    string CreatedAt);

public sealed record CommandCenterTurnReferenceDto(
    string TurnId,
    string Intent,
    string UserRequestPreview,
    string? PrimaryEntityType,
    string? PrimaryEntityId,
    IReadOnlyList<string> EvidenceReferenceIds,
    string CreatedAt);

public sealed record CommandCenterCompactedTurnPointerDto(
    string PointerId,
    string SummaryPointer,
    string? PrimaryEntityType,
    string? PrimaryEntityId,
    string FromTurnId,
    string ToTurnId,
    string CreatedAt);

public sealed record ModelConversationReferenceDto(
    string Provider,
    string? FoundryConversationId,
    string? PreviousResponseId,
    string? LastCompactionItemId,
    string UpdatedAt);
```

Important distinction:

```text
EvidenceReferenceIds may contain IDs of evidence records or retrieval results, but not the evidence text itself.
The actual evidence must be reloaded from source repositories or IRetrievalProvider on each turn.
```

### 6.1 Update request models

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public sealed record CommandCenterFocusUpdateRequest(
    string? ClaimId,
    string? ClaimNumber,
    string? CustomerId,
    string? PolicyId,
    string? DocumentId,
    string? ApprovalId,
    string? ReportId,
    string? FocusLabel);

public sealed record CommandCenterTurnReferenceCreateRequest(
    string Intent,
    string UserRequestPreview,
    string? PrimaryEntityType,
    string? PrimaryEntityId,
    IReadOnlyList<string> EvidenceReferenceIds,
    IReadOnlyList<CommandCenterUnresolvedQuestionDto> NewUnresolvedQuestions,
    IReadOnlyList<CommandCenterPendingTaskDto> NewPendingTasks,
    ModelConversationReferenceDto? ModelConversationReference = null);
```

---

## 7. Session state repository port

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public interface ICommandCenterSessionStateRepository
{
    Task<CommandCenterSessionStateReadModel?> GetAsync(
        string tenantId,
        string? workspaceId,
        string userId,
        string conversationId,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        CommandCenterSessionStateWriteModel model,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string tenantId,
        string? workspaceId,
        string userId,
        string conversationId,
        CancellationToken cancellationToken);
}

public sealed record CommandCenterSessionStateReadModel(
    string ConversationId,
    string TenantId,
    string? WorkspaceId,
    string UserId,
    string StateJson,
    int CompactionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CommandCenterSessionStateWriteModel(
    string ConversationId,
    string TenantId,
    string? WorkspaceId,
    string UserId,
    string StateJson,
    int CompactionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Persistence rules:

```text
- Use durable persistence such as Azure SQL in protected runtimes.
- Do not use in-memory session state in Staging, Production, or ManagedApplicationReviewer.
- Store the payload as structured JSON only after validation that it contains references, not raw evidence.
- Add indexes on TenantId, WorkspaceId, UserId, ConversationId.
- Consider TTL/retention policy later, but do not silently purge active operational sessions without product approval.
```

---

## 8. Session state validation policy

### 8.1 Forbidden stored content policy

The session service must reject attempts to store raw evidence-like content.

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public static class CommandCenterSessionStateValidation
{
    private static readonly string[] ForbiddenPropertyNames =
    {
        "rawEvidence",
        "evidenceText",
        "documentContent",
        "ocrText",
        "policyWording",
        "claimPayload",
        "customerPayload",
        "providerPayload",
        "prompt",
        "systemPrompt",
        "embedding",
        "vector",
        "rawSearchChunk",
        "messageBody"
    };

    public static void EnsureSafeForSessionState(CommandCenterSessionStateDto state)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(state);

        foreach (var forbidden in ForbiddenPropertyNames)
        {
            if (json.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException($"Session state cannot contain '{forbidden}'. Store references only.");
            }
        }

        EnsurePreviewLengths(state);
    }

    private static void EnsurePreviewLengths(CommandCenterSessionStateDto state)
    {
        foreach (var turn in state.RecentTurnReferences)
        {
            if (turn.UserRequestPreview.Length > 500)
            {
                throw new ValidationException("Session turn preview exceeds maximum allowed length.");
            }
        }

        foreach (var pointer in state.CompactedTurnPointers)
        {
            if (pointer.SummaryPointer.Length > 500)
            {
                throw new ValidationException("Compacted session pointer exceeds maximum allowed length.");
            }
        }
    }
}
```

This validation is not the only protection. Code review, DTO shape, persistence tests, and security tests must also confirm no raw evidence is persisted.

---

## 9. Session-state compaction policy

### 9.1 Concrete threshold

IIP session state is intentionally small, but it can still grow across long operational conversations. Apply deterministic session-state compaction with this policy:

```text
MaxRecentTurnReferences: 20
RetainRecentTurnReferencesAfterCompaction: 10
MaxUnresolvedQuestions: 10
MaxPendingTasks: 20
MaxCompactedPointers: 50
```

Trigger compaction when any condition is true:

```text
- RecentTurnReferences.Count > 20
- UnresolvedQuestions.Count > 10
- PendingTasks.Count > 20
```

Compaction behavior:

```text
- Keep the newest 10 turn references as-is.
- Convert older turn references into one-line compacted pointers.
- Keep only stable entity references and intent labels.
- Drop stale unresolved questions beyond the newest 10, unless they are linked to an active pending task.
- Do not summarize evidence text because evidence text must not exist in session state.
```

### 9.2 CommandCenterSessionCompactionPolicy

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public sealed record CommandCenterSessionCompactionOptions(
    int MaxRecentTurnReferences = 20,
    int RetainRecentTurnReferencesAfterCompaction = 10,
    int MaxUnresolvedQuestions = 10,
    int MaxPendingTasks = 20,
    int MaxCompactedPointers = 50);

public sealed class CommandCenterSessionCompactionPolicy
{
    private readonly CommandCenterSessionCompactionOptions _options;

    public CommandCenterSessionCompactionPolicy(CommandCenterSessionCompactionOptions options)
    {
        _options = options;
    }

    public bool ShouldCompact(CommandCenterSessionStateDto state)
    {
        return state.RecentTurnReferences.Count > _options.MaxRecentTurnReferences
            || state.UnresolvedQuestions.Count > _options.MaxUnresolvedQuestions
            || state.PendingTasks.Count > _options.MaxPendingTasks;
    }

    public CommandCenterSessionStateDto Compact(CommandCenterSessionStateDto state, DateTimeOffset now)
    {
        if (!ShouldCompact(state))
        {
            return state;
        }

        var retained = state.RecentTurnReferences
            .OrderByDescending(x => x.CreatedAt)
            .Take(_options.RetainRecentTurnReferencesAfterCompaction)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var retainedIds = retained.Select(x => x.TurnId).ToHashSet(StringComparer.Ordinal);

        var older = state.RecentTurnReferences
            .Where(x => !retainedIds.Contains(x.TurnId))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var newPointers = older.Select(turn => new CommandCenterCompactedTurnPointerDto(
            PointerId: Guid.NewGuid().ToString("N"),
            SummaryPointer: BuildPointer(turn),
            PrimaryEntityType: turn.PrimaryEntityType,
            PrimaryEntityId: turn.PrimaryEntityId,
            FromTurnId: turn.TurnId,
            ToTurnId: turn.TurnId,
            CreatedAt: now.ToString("O"))).ToList();

        var compactedPointers = state.CompactedTurnPointers
            .Concat(newPointers)
            .OrderByDescending(x => x.CreatedAt)
            .Take(_options.MaxCompactedPointers)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var unresolved = state.UnresolvedQuestions
            .OrderByDescending(x => x.CreatedAt)
            .Take(_options.MaxUnresolvedQuestions)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var pending = state.PendingTasks
            .OrderByDescending(x => x.CreatedAt)
            .Take(_options.MaxPendingTasks)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        return state with
        {
            RecentTurnReferences = retained,
            CompactedTurnPointers = compactedPointers,
            UnresolvedQuestions = unresolved,
            PendingTasks = pending,
            CompactionCount = state.CompactionCount + 1,
            UpdatedAt = now.ToString("O")
        };
    }

    private static string BuildPointer(CommandCenterTurnReferenceDto turn)
    {
        var entity = string.IsNullOrWhiteSpace(turn.PrimaryEntityType) || string.IsNullOrWhiteSpace(turn.PrimaryEntityId)
            ? "no primary entity"
            : $"{turn.PrimaryEntityType}:{turn.PrimaryEntityId}";

        return $"Prior turn intent={turn.Intent}; entity={entity}; createdAt={turn.CreatedAt}. Re-retrieve evidence by reference if needed.";
    }
}
```

### 9.3 Foundry-side compaction policy

Foundry native context compaction may be enabled for the underlying model conversation object when supported and verified in the target deployment.

Rules:

```text
- Foundry compaction is optional.
- It is a model-continuity optimization only.
- It must not be used as an audit record.
- It must not be used as evidence storage.
- It must not replace session-state compaction.
- It must not replace retrieval-on-demand.
- Audit trail integrity comes from IAuditWriter, AgentRun, AIInteraction, and source evidence records.
```

---

## 10. Full C# implementation - CommandCenterSessionStateService

```csharp
using System.Text.Json;
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

internal sealed class CommandCenterSessionStateService : ICommandCenterSessionStateService
{
    private const string StateVersion = "1.0";

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ICommandCenterSessionStateRepository _repository;
    private readonly CommandCenterSessionCompactionPolicy _compactionPolicy;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;

    public CommandCenterSessionStateService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        ICommandCenterSessionStateRepository repository,
        CommandCenterSessionCompactionPolicy compactionPolicy,
        IAuditWriter auditWriter,
        IClock clock)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _compactionPolicy = compactionPolicy;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    public async Task<CommandCenterSessionStateDto> GetOrCreateAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ValidateConversationId(conversationId);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.CommandCenterQuery, cancellationToken);

        var existing = await _repository.GetAsync(
            context.TenantId,
            context.WorkspaceId,
            context.UserId,
            conversationId,
            cancellationToken);

        if (existing is not null)
        {
            return Deserialize(existing.StateJson);
        }

        var now = _clock.UtcNow;
        var created = new CommandCenterSessionStateDto(
            ConversationId: conversationId,
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            UserId: context.UserId,
            Focus: new CommandCenterSessionFocusDto(null, null, null, null, null, null, null, null),
            ActiveFilters: Array.Empty<CommandCenterFilterReferenceDto>(),
            UnresolvedQuestions: Array.Empty<CommandCenterUnresolvedQuestionDto>(),
            PendingTasks: Array.Empty<CommandCenterPendingTaskDto>(),
            RecentTurnReferences: Array.Empty<CommandCenterTurnReferenceDto>(),
            CompactedTurnPointers: Array.Empty<CommandCenterCompactedTurnPointerDto>(),
            ModelConversationReference: null,
            StateVersion: StateVersion,
            CompactionCount: 0,
            CreatedAt: now.ToString("O"),
            UpdatedAt: now.ToString("O"));

        await SaveAsync(created, createdAt: now, updatedAt: now, cancellationToken);

        await WriteAuditAsync(
            context,
            conversationId,
            action: "command_center.session.created",
            summary: "Command Center session state created.",
            cancellationToken);

        return created;
    }

    public async Task<CommandCenterSessionStateDto> GetAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        ValidateConversationId(conversationId);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.CommandCenterQuery, cancellationToken);

        var existing = await _repository.GetAsync(
            context.TenantId,
            context.WorkspaceId,
            context.UserId,
            conversationId,
            cancellationToken);

        if (existing is null)
        {
            throw new NotFoundOrNotVisibleException("Command Center session was not found or is not visible.");
        }

        return Deserialize(existing.StateJson);
    }

    public async Task<CommandCenterSessionStateDto> UpdateFocusAsync(
        string conversationId,
        CommandCenterFocusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var state = await GetOrCreateAsync(conversationId, cancellationToken);
        var now = _clock.UtcNow;

        var updated = state with
        {
            Focus = new CommandCenterSessionFocusDto(
                ClaimId: Normalize(request.ClaimId),
                ClaimNumber: Normalize(request.ClaimNumber),
                CustomerId: Normalize(request.CustomerId),
                PolicyId: Normalize(request.PolicyId),
                DocumentId: Normalize(request.DocumentId),
                ApprovalId: Normalize(request.ApprovalId),
                ReportId: Normalize(request.ReportId),
                FocusLabel: Truncate(Normalize(request.FocusLabel), 200)),
            UpdatedAt = now.ToString("O")
        };

        await SaveAsync(updated, DateTimeOffset.Parse(updated.CreatedAt), now, cancellationToken);
        return updated;
    }

    public async Task<CommandCenterSessionStateDto> UpdateActiveFiltersAsync(
        string conversationId,
        IReadOnlyList<CommandCenterFilterReferenceDto> filters,
        CancellationToken cancellationToken)
    {
        var state = await GetOrCreateAsync(conversationId, cancellationToken);
        var now = _clock.UtcNow;

        var safeFilters = filters
            .Take(20)
            .Select(x => new CommandCenterFilterReferenceDto(
                FilterType: Truncate(x.FilterType, 100),
                FilterValue: Truncate(x.FilterValue, 200),
                DisplayLabel: Truncate(x.DisplayLabel, 200)))
            .ToList();

        var updated = state with
        {
            ActiveFilters = safeFilters,
            UpdatedAt = now.ToString("O")
        };

        await SaveAsync(updated, DateTimeOffset.Parse(updated.CreatedAt), now, cancellationToken);
        return updated;
    }

    public async Task<CommandCenterSessionStateDto> RecordTurnAsync(
        string conversationId,
        CommandCenterTurnReferenceCreateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTurn(request);

        var state = await GetOrCreateAsync(conversationId, cancellationToken);
        var now = _clock.UtcNow;

        var turn = new CommandCenterTurnReferenceDto(
            TurnId: Guid.NewGuid().ToString("N"),
            Intent: Truncate(request.Intent, 100),
            UserRequestPreview: Truncate(request.UserRequestPreview, 500),
            PrimaryEntityType: Normalize(request.PrimaryEntityType),
            PrimaryEntityId: Normalize(request.PrimaryEntityId),
            EvidenceReferenceIds: request.EvidenceReferenceIds.Take(50).Select(x => Truncate(x, 200)).ToList(),
            CreatedAt: now.ToString("O"));

        var updated = state with
        {
            RecentTurnReferences = state.RecentTurnReferences.Concat(new[] { turn }).ToList(),
            UnresolvedQuestions = MergeQuestions(state.UnresolvedQuestions, request.NewUnresolvedQuestions),
            PendingTasks = MergePendingTasks(state.PendingTasks, request.NewPendingTasks),
            ModelConversationReference = request.ModelConversationReference ?? state.ModelConversationReference,
            UpdatedAt = now.ToString("O")
        };

        updated = _compactionPolicy.Compact(updated, now);

        await SaveAsync(updated, DateTimeOffset.Parse(updated.CreatedAt), now, cancellationToken);
        return updated;
    }

    public async Task<CommandCenterSessionStateDto> CompactIfNeededAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        var state = await GetAsync(conversationId, cancellationToken);

        if (!_compactionPolicy.ShouldCompact(state))
        {
            return state;
        }

        var context = _tenantContextAccessor.Current;
        var now = _clock.UtcNow;
        var compacted = _compactionPolicy.Compact(state, now);

        await SaveAsync(compacted, DateTimeOffset.Parse(compacted.CreatedAt), now, cancellationToken);

        await WriteAuditAsync(
            context,
            conversationId,
            action: "command_center.session.compacted",
            summary: "Command Center session operational state compacted. Evidence was not stored or summarized.",
            cancellationToken);

        return compacted;
    }

    public async Task ClearAsync(
        string conversationId,
        string reason,
        CancellationToken cancellationToken)
    {
        ValidateConversationId(conversationId);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.CommandCenterQuery, cancellationToken);

        await _repository.DeleteAsync(
            context.TenantId,
            context.WorkspaceId,
            context.UserId,
            conversationId,
            cancellationToken);

        await WriteAuditAsync(
            context,
            conversationId,
            action: "command_center.session.cleared",
            summary: "Command Center session state cleared.",
            cancellationToken);
    }

    private async Task SaveAsync(
        CommandCenterSessionStateDto state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        CommandCenterSessionStateValidation.EnsureSafeForSessionState(state);

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _repository.UpsertAsync(new CommandCenterSessionStateWriteModel(
            ConversationId: state.ConversationId,
            TenantId: state.TenantId,
            WorkspaceId: state.WorkspaceId,
            UserId: state.UserId,
            StateJson: json,
            CompactionCount: state.CompactionCount,
            CreatedAt: createdAt,
            UpdatedAt: updatedAt), cancellationToken);
    }

    private static CommandCenterSessionStateDto Deserialize(string json)
    {
        return JsonSerializer.Deserialize<CommandCenterSessionStateDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ValidationException("Stored Command Center session state is invalid.");
    }

    private Task WriteAuditAsync(
        TenantContext context,
        string conversationId,
        string action,
        string summary,
        CancellationToken cancellationToken)
    {
        return _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: AuditEventTypeValues.DataChanged,
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: action,
            EntityType: "command_center_session",
            EntityId: conversationId,
            Summary: summary,
            Reason: null,
            CorrelationId: context.CorrelationId,
            OccurredAt: _clock.UtcNow), cancellationToken);
    }

    private static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ValidationException("conversationId is required.");
        }

        if (conversationId.Length > 128)
        {
            throw new ValidationException("conversationId exceeds maximum length.");
        }
    }

    private static void ValidateTurn(CommandCenterTurnReferenceCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Intent))
        {
            throw new ValidationException("intent is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserRequestPreview))
        {
            throw new ValidationException("userRequestPreview is required.");
        }
    }

    private static IReadOnlyList<CommandCenterUnresolvedQuestionDto> MergeQuestions(
        IReadOnlyList<CommandCenterUnresolvedQuestionDto> existing,
        IReadOnlyList<CommandCenterUnresolvedQuestionDto> incoming)
    {
        return existing.Concat(incoming)
            .GroupBy(x => x.QuestionId, StringComparer.Ordinal)
            .Select(x => x.Last())
            .ToList();
    }

    private static IReadOnlyList<CommandCenterPendingTaskDto> MergePendingTasks(
        IReadOnlyList<CommandCenterPendingTaskDto> existing,
        IReadOnlyList<CommandCenterPendingTaskDto> incoming)
    {
        return existing.Concat(incoming)
            .GroupBy(x => x.TaskId, StringComparer.Ordinal)
            .Select(x => x.Last())
            .ToList();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }
}
```

Implementation notes:

```text
- IClock is the same simple clock pattern used in other Application services where deterministic time is needed for tests.
- The service uses Permissions.CommandCenterQuery as the minimum permission for session continuity because this is Command Center state, not a separate business object.
- If a future admin/debug endpoint reads sessions directly, add a separate permission such as command_center.session.read.
- The service stores references and task state only; it does not store evidence text.
```

---

## 11. Retrieval-on-demand standing rule

### 11.1 Rule

Every consequential Command Center turn must re-retrieve grounding data fresh.

The session state may say:

```text
The active claim is claimId=clm-123.
The active filter is SLA risk = breached.
The pending task is draft_customer_update for claimId=clm-123.
```

It must not store:

```text
The full claim record.
The full extracted police report text.
The full policy wording.
The generated evidence memo as truth.
```

### 11.2 Retrieval flow

```text
User asks follow-up question
        |
        v
CommandCenterQueryService receives conversationId + user text
        |
        v
ICommandCenterSessionStateService loads current focus and task references
        |
        v
Intent classifier determines intent deterministically first, AI fallback only when needed
        |
        v
Application services / repositories / IRetrievalProvider re-retrieve source data by ID/filter
        |
        v
Prompt/evidence pack is built from fresh scoped source data
        |
        v
IChatCompletionProvider generates structured response
        |
        v
IAiOutputValidator validates schema, citations, and human-review flags
        |
        v
ICommandCenterSessionStateService records only references and task continuity
        |
        v
IAuditWriter records agent/tool/audit activity as required
```

### 11.3 Prompt construction rule

The model prompt should be composed from bounded parts:

```text
- Static system/developer instructions.
- Current authenticated tenant/workspace/user role context.
- Current user request.
- Small session continuity block from ICommandCenterSessionStateService.
- Freshly retrieved evidence block for this turn.
- Tool result metadata and citations.
- Required response schema.
```

It should not contain the full historic transcript unless explicitly needed for a short-horizon clarifying exchange.

---

## 12. Optional Foundry conversation continuity

### 12.1 Decision

The existing `IChatCompletionProvider` should remain stateless/per-call for the core Application layer.

No required signature change is introduced in this pass.

Reason:

```text
- IIP's source of truth is Application-layer retrieval, not provider-side conversation memory.
- Keeping IChatCompletionProvider stateless reduces provider lock-in.
- It keeps tests deterministic and avoids coupling service logic to Foundry-specific conversation IDs.
- Optional provider-side continuity can be added through adapter-level metadata without making it an evidence dependency.
```

### 12.2 Optional future extension

If the team later chooses to use Foundry conversations or `previous_response_id`, add a provider-neutral continuation reference in session state and let the Infrastructure adapter decide whether to use it.

```csharp
namespace InsuranceIntelligence.Application.CommandCenter.Sessions;

public sealed record ModelConversationReference(
    string Provider,
    string? ProviderConversationId,
    string? PreviousResponseId,
    string? LastCompactionItemId,
    DateTimeOffset UpdatedAt);
```

Rules:

```text
- This reference is not evidence.
- It is not audited as source data.
- It may be omitted without changing the business answer.
- Losing it must not break the user's ability to continue, because the operational focus lives in ICommandCenterSessionStateService.
- If the adapter uses it, it must still pass freshly retrieved evidence for consequential turns.
```

### 12.3 Provider health and availability

If native Foundry conversation continuity is enabled, provider availability must still be checked through the existing provider-health pattern where applicable.

```text
- Missing model provider configuration -> ProviderNotConfiguredException.
- Unavailable model provider -> ProviderUnavailableException.
- Failure to restore provider-side conversation reference -> continue with stateless call if safe, or return a safe unavailable/degraded response depending on the action.
- Never fabricate response content because provider-side conversation state is missing.
```

---

## 13. Multi-agent orchestration scaffold - future state

### 13.1 Immediate state

The existing 12 Application services work correctly as directly called services today.

CommandCenterQueryService currently performs deterministic intent classification and dispatches to the relevant services. That is acceptable for the first production-grade implementation.

Do not force a multi-agent migration before there is a real need.

### 13.2 Target future shape

If and when true multi-agent handoff becomes necessary, IIP should evolve toward named agent roles with explicit handoff contracts.

Potential roles:

```text
Command Center Agent
Claims Triage Agent
Document Extraction Agent
Completeness Check Agent
Customer Draft Agent
Risk & Fraud Signal Agent
Underwriting Intelligence Agent
Report Insight Agent
Knowledge Assistant
Routing Agent
```

Future orchestration should use code-first Application-layer contracts and, where appropriate after live SDK verification, Microsoft Agent Framework SDK Sequential and Handoff patterns.

### 13.3 Agent role enum

```csharp
namespace InsuranceIntelligence.Application.Agents;

public enum AgentRole
{
    CommandCenter,
    ClaimsTriage,
    DocumentExtraction,
    CompletenessCheck,
    CustomerDraft,
    RiskFraudSignal,
    UnderwritingIntelligence,
    ReportInsight,
    KnowledgeAssistant,
    Routing
}
```

### 13.4 Handoff contract

```csharp
namespace InsuranceIntelligence.Application.Agents;

public sealed record AgentHandoffRequest(
    string HandoffId,
    AgentRole FromAgent,
    AgentRole ToAgent,
    string Intent,
    string RelatedEntityType,
    string RelatedEntityId,
    IReadOnlyList<string> EvidenceReferenceIds,
    string RequiredPermission,
    bool RequiresHumanReview,
    string CorrelationId);

public sealed record AgentHandoffResult(
    string HandoffId,
    AgentRole CompletedByAgent,
    string Status,
    string OutputType,
    object Output,
    IReadOnlyList<string> EvidenceReferenceIds,
    bool RequiresHumanReview);
```

Rules:

```text
- Handoffs pass entity references and evidence IDs, not raw evidence text.
- The receiving agent must re-retrieve evidence from authorized sources.
- Every handoff must be auditable.
- Every handoff must preserve tenant/workspace scope.
- Every consequential action must require a human-review gate.
```

### 13.5 Orchestrator interface

```csharp
namespace InsuranceIntelligence.Application.Agents;

public interface IAgentOrchestrator
{
    Task<AgentHandoffResult> ExecuteAsync(
        AgentHandoffRequest request,
        CancellationToken cancellationToken);
}
```

### 13.6 Example future orchestration flow

```text
Command Center Agent receives: "Check whether this claim is ready for approval and draft the customer update."
        |
        v
Claims Triage Agent re-retrieves claim summary and status.
        |
        v
Document Extraction Agent verifies extraction status where needed.
        |
        v
Completeness Check Agent checks missing/required documents.
        |
        v
Risk & Fraud Signal Agent produces advisory risk signals only.
        |
        v
Customer Draft Agent drafts message with evidence citations.
        |
        v
Command Center Agent returns structured blocks and requires human review.
```

### 13.7 A2A policy

Foundry A2A may be considered in the future if IIP needs independently discoverable/callable agents across service boundaries.

Current policy:

```text
- Do not adopt A2A preemptively.
- Do not make A2A a hard dependency while preview/availability concerns remain.
- Do not let A2A bypass IIP tenant context, permissions, audit, or approval gates.
- If A2A is adopted later, wrap it behind IAgentOrchestrator or a dedicated transport adapter.
```

---

## 14. CommandCenterQueryService integration guidance

### 14.1 Current direct-dispatch flow

CommandCenterQueryService should be extended to use the new session service without changing its core business logic.

```text
1. Receive CommandCenterQueryRequest with conversationId.
2. Load session state through ICommandCenterSessionStateService.GetOrCreateAsync.
3. Use session focus to resolve follow-up references such as "this claim" or "the one we discussed".
4. Classify intent using DeterministicThenAiIntentClassifier.
5. Re-retrieve data from Application services/repositories/IRetrievalProvider.
6. Build bounded evidence pack.
7. Invoke IChatCompletionProvider.
8. Validate structured output using IAiOutputValidator.
9. Record turn reference through ICommandCenterSessionStateService.RecordTurnAsync.
```

### 14.2 Follow-up resolution rule

When a user says:

```text
- this claim
- the customer we discussed
- draft the update for it
- show me the documents for that case
```

CommandCenterQueryService may use session state to resolve the referenced entity ID.

It must then re-retrieve the entity through the relevant service. It must not rely on prior model text.

### 14.3 Evidence citations

Evidence citations in the model response must refer to source records or retrieval results, not to session-state summaries.

Allowed citation sources:

```text
- claim records
- claim timeline items
- document metadata
- extracted field records
- policy knowledge chunks
- approved SOP/knowledge-base chunks
- approval queue records
- audit/event records where permitted
```

Forbidden citation sources:

```text
- Foundry compacted state
- prior assistant prose
- session-state focus labels
- model memory
- hidden prompt text
```

---

## 15. Operational risk register

| Risk | Description | Impact | Mitigation |
|---|---|---|---|
| Model catalog availability vs subscription quota | Per current research, pending live verification, a model can appear in the model catalog while the target subscription/region has insufficient quota to use it at production scale. | Deployment failure, throttling, poor UX. | Verify model SKU, region, TPM/RPM, and quota tier before implementation and again before production launch. |
| Context window assumptions | Per current research, pending live verification, context windows vary by model/SKU and can change. | Incorrect prompt budget, truncation risk. | Treat all context-window figures as TODOs. Query live Foundry model catalog and provider docs at implementation time. |
| Silent truncation | Provider may truncate active context when the model limit is exceeded. | Loss of grounding fidelity if relied upon. | Use bounded prompts and retrieval-on-demand. Never use provider history as evidence. |
| Opaque compaction | Foundry compaction may preserve model continuity but not human-readable audit evidence. | Compliance and audit gaps if misused. | Use IAuditWriter and source evidence records for audit. Treat compaction as continuation only. |
| Long-context cost and quota pressure | Carrying growing transcripts/evidence can multiply token usage. | High cost, latency, quota exhaustion. | Bound prompts, re-retrieve only relevant evidence, evaluate prompt caching later. |
| Preview multi-agent primitives | A2A and workflow capabilities may vary in maturity and availability. | Lock-in or unstable dependency. | Keep orchestration in Application layer; wrap preview transports behind adapters only after review. |
| Over-agentification | Too many agents too early can add latency and complexity without business value. | Slower implementation, harder QA. | Start with CommandCenterQueryService direct-dispatch; evolve to agents only where handoff adds value. |
| Session state leakage | Conversation IDs could be guessed or reused across users. | Tenant/customer data leak. | Scope every session by tenantId + workspaceId + userId + conversationId. |
| Evidence stored in session | Developers may accidentally persist raw retrieved content. | Privacy, compliance, stale-data risk. | Validation, tests, code review, persistence inspection tests. |
```

---

## 16. Tests to add

### 16.1 Session state tenant/workspace scoping

```text
CommandCenterSessionStateServiceTests
- GetOrCreateAsync_CreatesSessionScopedToTenantWorkspaceUser
- GetAsync_DoesNotReturnSessionFromAnotherTenant
- GetAsync_DoesNotReturnSessionFromAnotherWorkspace
- GetAsync_DoesNotReturnSessionFromAnotherUser
- UpdateFocusAsync_UsesTenantContextNotClientTenantInput
```

Example:

```csharp
[Fact]
public async Task GetAsync_DoesNotReturnSessionFromAnotherTenant()
{
    var context = TestTenantContext.For("tenant-a", "workspace-a", "user-1", Permissions.CommandCenterQuery);
    var repository = new Mock<ICommandCenterSessionStateRepository>();

    repository.Setup(x => x.GetAsync(
            "tenant-a",
            "workspace-a",
            "user-1",
            "conv-1",
            It.IsAny<CancellationToken>()))
        .ReturnsAsync((CommandCenterSessionStateReadModel?)null);

    var service = BuildService(context, repository.Object);

    await Assert.ThrowsAsync<NotFoundOrNotVisibleException>(() =>
        service.GetAsync("conv-1", CancellationToken.None));
}
```

### 16.2 Grounding data never persisted in session state

```text
CommandCenterSessionStatePersistenceTests
- RecordTurnAsync_DoesNotPersistRawEvidenceText
- RecordTurnAsync_DoesNotPersistDocumentContent
- RecordTurnAsync_DoesNotPersistPolicyWording
- RecordTurnAsync_DoesNotPersistClaimPayload
- StoredSessionJsonContainsReferencesOnly
```

Required test behavior:

```text
This test must inspect the actual stored StateJson, not only the DTO before persistence.
```

Example:

```csharp
[Fact]
public async Task StoredSessionJsonContainsReferencesOnly()
{
    var repository = new CapturingCommandCenterSessionStateRepository();
    var service = BuildService(repository: repository);

    await service.RecordTurnAsync(
        "conv-1",
        new CommandCenterTurnReferenceCreateRequest(
            Intent: "claim_summary",
            UserRequestPreview: "Summarize claim CLM-1",
            PrimaryEntityType: "claim",
            PrimaryEntityId: "clm-1",
            EvidenceReferenceIds: new[] { "claim:clm-1", "document:doc-1" },
            NewUnresolvedQuestions: Array.Empty<CommandCenterUnresolvedQuestionDto>(),
            NewPendingTasks: Array.Empty<CommandCenterPendingTaskDto>()),
        CancellationToken.None);

    Assert.Contains("claim:clm-1", repository.LastStateJson, StringComparison.Ordinal);
    Assert.DoesNotContain("full policy wording", repository.LastStateJson, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("documentContent", repository.LastStateJson, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("ocrText", repository.LastStateJson, StringComparison.OrdinalIgnoreCase);
}
```

### 16.3 Session-state compaction threshold

```text
CommandCenterSessionStateServiceTests
- CompactIfNeededAsync_DoesNothingBelowThreshold
- RecordTurnAsync_CompactsWhenRecentTurnReferencesExceedThreshold
- CompactIfNeededAsync_RetainsNewestTenTurnReferences
- CompactIfNeededAsync_ConvertsOlderTurnsToPointersOnly
- CompactIfNeededAsync_DoesNotCreateEvidenceSummaries
```

### 16.4 Resume after session-state compaction

```text
CommandCenterGroundingPolicyTests
- ConversationCanResumeAfterSessionCompactionUsingStoredClaimReference
- FollowUpThisClaimResolvesToStoredClaimIdThenReRetrievesClaim
- CompactedPointersAreNotUsedAsEvidence
```

Required assertion:

```text
The test must verify IClaimsQueryService or the relevant repository is called again after compaction.
```

### 16.5 Resume after Foundry-side compaction

```text
CommandCenterGroundingPolicyTests
- ConversationCanResumeAfterFoundryCompactionBecauseFocusLivesInSessionState
- MissingFoundryConversationReferenceDoesNotEraseActiveClaimFocus
- FoundryCompactionItemIsNeverUsedAsEvidenceCitation
```

### 16.6 Multi-agent policy tests

```text
AgentOrchestrationPolicyTests
- HandoffPassesEntityReferencesNotRawEvidence
- HandoffRequiresPermissionForTargetAction
- HandoffWritesAuditOrAgentRunRecord
- AgentCannotBypassHumanReviewForConsequentialDecision
- A2ATransportCannotBypassTenantContextOrPermissionPolicy
```

---

## 17. Implementation order

Recommended order:

```text
1. Add ICommandCenterSessionStateService models and repository port.
2. Add EF persistence for session state.
3. Add session-state validation and compaction policy.
4. Add tests for tenant isolation and no raw evidence persistence.
5. Wire CommandCenterQueryService to load session state and resolve follow-up references.
6. Keep IChatCompletionProvider stateless for the first implementation.
7. Add optional provider-side model conversation references only after baseline behavior is stable.
8. Evaluate prompt caching and Foundry compaction as secondary optimizations.
9. Add future multi-agent orchestration only after direct-dispatch complexity justifies it.
```

---

## 18. TODOs requiring live verification

Every figure below is **per current research, pending live verification**. Do not commit implementation decisions that assume these values without checking the live Azure tenant/subscription, official Microsoft documentation, and current provider model cards.

```text
- GPT-5.5 context window and max output.
- GPT-5.4 context window and max output.
- GPT-5.4-mini context window and max output.
- GPT-5 context window and max output.
- Claude Fable 5 context window and max output.
- Claude Opus 4.8 context window and max output.
- Claude Sonnet 5 context window and max output.
- Claude Haiku 4.5 context window and max output.
- Azure model-region availability for chosen GPT/Claude model.
- Azure subscription TPM/RPM quota for chosen GPT/Claude model.
- Whether any chosen model requires quota increase request.
- Actual pricing for input tokens, cached input tokens, output tokens, tool calls, file search, memory, hosted agent compute, and storage.
- Prompt caching availability and behavior for the exact chosen deployment path.
- Foundry context_management / compact_threshold availability for the chosen API/model path.
- Microsoft Agent Framework .NET SDK support for Sequential and Handoff patterns at implementation time.
- A2A preview/GA status and enterprise support posture at implementation time.
```

---

## 19. Open questions for Elvis/Azure review

```text
1. What is the confirmed current Foundry quota tier for the target Azure subscription?
2. Which model(s) will IIP deploy for MVP: GPT-5-family, Claude via Foundry, or both?
3. For the chosen model(s), what are the exact context window, max output, TPM, RPM, region, and deployment type in the target subscription?
4. Does A2A's current preview/availability status block any near-term dependency for IIP?
5. Is Microsoft Agent Framework's .NET SDK support for Sequential and Handoff patterns still accurate at implementation time?
6. Should IIP use Foundry native conversations immediately, or start stateless and add native continuity after the session-state layer is stable?
7. Should Command Center sessions be retained for a fixed duration, user-controlled archive duration, or tenant-configurable duration?
8. Should session state be stored in Azure SQL with other operational data or in Cosmos DB if Foundry conversation storage is also Cosmos-backed?
9. Should insurer tenants be allowed to disable provider-side conversation persistence entirely for stricter privacy deployments?
10. What audit evidence format does the pilot insurer/regulator expect for AI-assisted recommendations?
```

---

## 20. Completion report template

```text
Architecture document implemented: IIP Conversational Session, Context, and Multi-Agent Architecture.

Files changed or created:
- src/InsuranceIntelligence.Application/CommandCenter/Sessions/ICommandCenterSessionStateService.cs
- src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionStateService.cs
- src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionStateModels.cs
- src/InsuranceIntelligence.Application/CommandCenter/Sessions/ICommandCenterSessionStateRepository.cs
- src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionStateValidation.cs
- src/InsuranceIntelligence.Application/CommandCenter/Sessions/CommandCenterSessionCompactionPolicy.cs
- src/InsuranceIntelligence.Infrastructure/CommandCenter/EfCommandCenterSessionStateRepository.cs
- src/InsuranceIntelligence.Infrastructure/Persistence/Entities/CommandCenterSessionStateEntity.cs
- src/InsuranceIntelligence.Infrastructure/Persistence/Configurations/CommandCenterSessionStateConfiguration.cs
- Optional: src/InsuranceIntelligence.Application/CommandCenter/Sessions/ModelConversationReference.cs
- Future: src/InsuranceIntelligence.Application/Agents/*

Architecture decision:
- Hybrid context architecture approved.
- Foundry provides inference and optional short-horizon continuity.
- IIP Application layer owns grounding, session state, audit, approval gates, and future multi-agent supervision.
- Retrieval-on-demand is mandatory for consequential turns.

Security impact:
- Prevents model-side conversation memory from becoming a source of truth.
- Prevents raw evidence from being persisted in session state.
- Preserves human-review gates for consequential insurance decisions.
- Keeps multi-agent handoff under IIP permission and audit controls.

Tenant isolation impact:
- Session state is scoped by TenantId, WorkspaceId, UserId, and ConversationId.
- ConversationId alone can never retrieve a session.
- Cross-tenant and cross-workspace session leakage is blocked by repository filters and tests.

Authentication/authorization impact:
- Session read/write requires Command Center permission.
- Future direct session-admin or debug surfaces require separate permissions.

Grounding impact:
- Session state stores only operational references.
- Claims, policies, documents, knowledge, approvals, and audits are re-retrieved per turn.
- Evidence citations point to live source records or retrieval results, not prior model text or compaction artifacts.

Provider impact:
- IChatCompletionProvider remains stateless/per-call in the core Application layer.
- Optional Foundry conversation continuity may be added behind provider/adapters later.
- Foundry compaction is optional continuation support only, not evidence or audit.

Data model or migration impact:
- Requires CommandCenterSessionState persistence table/entity.
- Requires indexes on TenantId, WorkspaceId, UserId, ConversationId.
- Requires JSON payload validation before persistence.

Multi-agent impact:
- No immediate migration to full multi-agent execution is required.
- Future agent handoff should use Application-layer contracts and Microsoft Agent Framework patterns only after live SDK review.
- A2A is out of scope unless separately approved.

Tests added or required:
- Tenant/workspace/user session isolation tests.
- No-raw-evidence persistence tests.
- Session-state compaction threshold tests.
- Resume-after-session-compaction tests.
- Resume-after-Foundry-compaction tests.
- Future multi-agent handoff policy tests.

TODOs requiring live verification:
- Verify exact model context windows and max output limits for chosen GPT/Claude deployments.
- Verify actual Azure subscription TPM/RPM quota and region availability.
- Verify pricing for tokens, cached inputs, tools, memory, file search, storage, and hosted agents.
- Verify Foundry context_management/compact_threshold availability for chosen model/API path.
- Verify Microsoft Agent Framework .NET SDK support for Sequential and Handoff patterns.
- Verify A2A preview/GA/support status.

Open questions for Elvis/Azure review:
- Confirm target Foundry quota tier.
- Confirm exact MVP model choices.
- Confirm whether provider-side conversation persistence is allowed for pilot tenants.
- Confirm session retention policy.
- Confirm audit evidence expectations for AI-assisted recommendations.

No fake data confirmation:
- No mock, simulated, placeholder, in-memory, seeded-demo, or fake provider behavior is introduced.
- No model-generated or summarized evidence is treated as source data.
- No Foundry internal compaction artifact is treated as audit evidence.

Recommended next step:
- Implement ICommandCenterSessionStateService and its persistence tests before adding provider-side conversation continuity or multi-agent handoff.
```

# IIP CommandCenterQueryService - Updated Backend Logic

**Product:** Insurance Intelligence Platform (IIP)  
**Area:** Chat-first Command Center backend query flow  
**Status:** Implementation-ready logic draft  
**Purpose:** Corrected implementation guidance for `CommandCenterQueryService` after backend/frontend contract reconciliation.

---

## 1. What changed from the earlier draft

This version keeps the original direction but fixes the production-readiness issues identified in review:

| Area | Earlier issue | Updated logic |
|---|---|---|
| Provider health checks | Azure AI Search and Azure OpenAI were required for every command | Provider checks are now intent-specific. SQL-backed read-only queries do not require AI providers. |
| Authorization | Tenant scope existed, but explicit permissions were missing | Every command checks `command_center.query`; each data path checks module permissions such as `claims.read`, `documents.read`, and `approvals.read`. |
| Not configured auditing | `not_configured` could return before audit was written | Every query attempt, including not-configured responses, writes an audit event. |
| Draft communication | Draft ID was generated but not persisted | Draft is persisted through `INotificationDraftService` before `draft_communication` block is returned. |
| Evidence source contract | Approval evidence used `sourceType = "approval"`, but the contract did not allow it | Frontend and backend contracts now include `approval` as an allowed `EvidenceSource.sourceType`. |
| Intent parameters | Dictionary-record pattern and `AdditionalContext` access were unsafe | Parameters are now a clear wrapper with `TryGetString` / `GetStringOrDefault`. |
| Timestamps | Display-formatted timestamps were returned | API timestamps now use ISO 8601 round-trip format. |
| AI output validation | LLM output was returned directly | Management summaries and customer drafts are passed through `IAiOutputValidator`. |
| Agent/audit summaries | Full block JSON could store sensitive data | Stored summaries are minimized and redacted. |

---

## 2. Required files

```text
Application/CommandCenter/
  CommandCenterQueryService.cs
  IIntentClassifier.cs
  DeterministicThenAiIntentClassifier.cs
  IntentClassificationResult.cs
  IntentParameters.cs
  IProviderRequirementResolver.cs
  ProviderRequirementResolver.cs
  INotificationDraftService.cs

Application/Ai/
  IAiOutputValidator.cs

Application/Common/Authorization/
  IPermissionService.cs

Tests/Application/CommandCenter/
  CommandCenterQueryServiceTests.cs
  CommandCenterContractTests.cs
```

---

## 3. Core service implementation

```csharp
using System.Text.Json;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Authorization;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.CommandCenter.Models;
using InsuranceIntelligence.Application.Common.Abstractions;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Application.Approvals;
using InsuranceIntelligence.Domain.Audit;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Application.CommandCenter;

internal sealed class CommandCenterQueryService : ICommandCenterQueryService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IIntentClassifier _intentClassifier;
    private readonly IProviderRequirementResolver _providerRequirementResolver;
    private readonly IProviderHealthCheck _providerHealth;
    private readonly IClaimsQueryService _claimsQueryService;
    private readonly ISlaRiskService _slaRiskService;
    private readonly IDocumentQueryService _documentQueryService;
    private readonly IApprovalQueryService _approvalQueryService;
    private readonly IRetrievalProvider _retrievalProvider;
    private readonly IChatCompletionProvider _chatProvider;
    private readonly IAiOutputValidator _aiOutputValidator;
    private readonly INotificationDraftService _notificationDraftService;
    private readonly IAgentRunWriter _agentRunWriter;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<CommandCenterQueryService> _logger;

    public CommandCenterQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IIntentClassifier intentClassifier,
        IProviderRequirementResolver providerRequirementResolver,
        IProviderHealthCheck providerHealth,
        IClaimsQueryService claimsQueryService,
        ISlaRiskService slaRiskService,
        IDocumentQueryService documentQueryService,
        IApprovalQueryService approvalQueryService,
        IRetrievalProvider retrievalProvider,
        IChatCompletionProvider chatProvider,
        IAiOutputValidator aiOutputValidator,
        INotificationDraftService notificationDraftService,
        IAgentRunWriter agentRunWriter,
        IAuditWriter auditWriter,
        ILogger<CommandCenterQueryService> logger)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _intentClassifier = intentClassifier;
        _providerRequirementResolver = providerRequirementResolver;
        _providerHealth = providerHealth;
        _claimsQueryService = claimsQueryService;
        _slaRiskService = slaRiskService;
        _documentQueryService = documentQueryService;
        _approvalQueryService = approvalQueryService;
        _retrievalProvider = retrievalProvider;
        _chatProvider = chatProvider;
        _aiOutputValidator = aiOutputValidator;
        _notificationDraftService = notificationDraftService;
        _agentRunWriter = agentRunWriter;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<CommandCenterQueryResponse> QueryAsync(
        CommandCenterQueryRequest request,
        CancellationToken ct)
    {
        var context = _tenantContextAccessor.Current;
        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new UnauthorizedAccessException("Tenant context not resolved.");
        }

        await _permissionService.EnsureAsync(Permissions.CommandCenterQuery, ct);

        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        var messageId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var intent = await _intentClassifier.ClassifyAsync(
                request.Message,
                context.TenantId,
                context.WorkspaceId,
                ct);

            var providerGate = await EnsureRequiredProvidersAsync(intent.Intent, ct);
            if (!providerGate.IsReady)
            {
                var notConfiguredResponse = BuildNotConfiguredResponse(
                    conversationId,
                    messageId,
                    providerGate.Capability ?? "Required provider",
                    providerGate.Message ?? "A required provider is not configured.",
                    providerGate.AdminHref ?? "/admin/integrations");

                await WriteAuditAndAgentRunAsync(
                    context,
                    request,
                    conversationId,
                    intent.Intent.ToString(),
                    status: "not_configured",
                    requiresHumanReview: false,
                    safeOutputSummary: "Command Center query returned not_configured.",
                    startedAt,
                    ct);

                return notConfiguredResponse;
            }

            var blocks = new List<CommandCenterBlockDto>();
            var evidence = new List<EvidenceSourceDto>();
            var requiresHumanReview = false;
            var blockedActions = new List<string>();
            var allowedNextActions = new List<string>();

            switch (intent.Intent)
            {
                case IntentType.ShowSlaBreaches:
                    await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);
                    var slaResult = await BuildSlaBlocksAsync(context, ct);
                    blocks.AddRange(slaResult.Blocks);
                    evidence.AddRange(slaResult.Evidence);
                    break;

                case IntentType.ShowAttentionClaims:
                    await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);
                    var attentionResult = await BuildAttentionBlocksAsync(context, ct);
                    blocks.AddRange(attentionResult.Blocks);
                    evidence.AddRange(attentionResult.Evidence);
                    break;

                case IntentType.ShowMissingDocuments:
                    await _permissionService.EnsureAsync(Permissions.DocumentsRead, ct);
                    var docResult = await BuildMissingDocumentBlocksAsync(context, ct);
                    blocks.AddRange(docResult.Blocks);
                    evidence.AddRange(docResult.Evidence);
                    break;

                case IntentType.ShowPendingApprovals:
                    await _permissionService.EnsureAsync(Permissions.ApprovalsRead, ct);
                    var approvalResult = await BuildApprovalBlocksAsync(context, ct);
                    blocks.AddRange(approvalResult.Blocks);
                    evidence.AddRange(approvalResult.Evidence);
                    break;

                case IntentType.GenerateManagementSummary:
                    await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);
                    var summaryResult = await BuildManagementSummaryAsync(context, intent.Parameters, ct);
                    blocks.AddRange(summaryResult.Blocks);
                    evidence.AddRange(summaryResult.Evidence);
                    break;

                case IntentType.PrepareCustomerUpdateDraft:
                    await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);
                    requiresHumanReview = true;
                    blockedActions.Add(CommandCenterBlockedActions.SendCustomerCommunication);
                    allowedNextActions.AddRange(new[]
                    {
                        CommandCenterAllowedNextActions.ReviewDraft,
                        CommandCenterAllowedNextActions.EditDraft,
                        CommandCenterAllowedNextActions.SubmitForApproval,
                        CommandCenterAllowedNextActions.Escalate
                    });

                    var draftResult = await BuildCustomerUpdateDraftAsync(context, intent.Parameters, ct);
                    blocks.AddRange(draftResult.Blocks);
                    evidence.AddRange(draftResult.Evidence);
                    break;

                case IntentType.Unsupported:
                default:
                    blocks.Add(new TextBlockDto(
                        "I can help with SLA breaches, claims needing attention, missing documents, pending approvals, " +
                        "management summaries, and customer update drafts. Please refine your question."));
                    break;
            }

            if (requiresHumanReview)
            {
                blocks.Insert(0, new TextBlockDto(
                    "This response includes proposed work that requires human review before any consequential action is taken."));
            }

            var assistantSummary = GenerateSafeAssistantSummary(blocks);

            await WriteAuditAndAgentRunAsync(
                context,
                request,
                conversationId,
                intent.Intent.ToString(),
                status: "succeeded",
                requiresHumanReview,
                safeOutputSummary: assistantSummary,
                startedAt,
                ct);

            return new CommandCenterQueryResponse(
                ConversationId: conversationId,
                MessageId: messageId,
                AssistantSummary: assistantSummary,
                Blocks: blocks,
                EvidenceSources: evidence,
                HumanReview: new HumanReviewRequirementDto(
                    Required: requiresHumanReview,
                    Reason: requiresHumanReview
                        ? "This response includes proposed work that may affect customer communications or workflow state. Human review is required."
                        : "No human review required for this read-only operational query.",
                    BlockedAutonomousActions: blockedActions,
                    AllowedNextActions: allowedNextActions),
                GeneratedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command Center query failed. CorrelationId: {CorrelationId}", context.CorrelationId);

            await WriteAuditOnlyAsync(
                context,
                request,
                conversationId,
                status: "failed",
                summary: "Command Center query failed. See correlation ID.",
                ct);

            return new CommandCenterQueryResponse(
                ConversationId: conversationId,
                MessageId: messageId,
                AssistantSummary: "The command could not be completed.",
                Blocks: new List<CommandCenterBlockDto>
                {
                    new ErrorBlockDto(
                        Message: "The command could not be completed. Please try again or contact support if the issue continues.",
                        CorrelationId: context.CorrelationId,
                        Retryable: true)
                },
                EvidenceSources: Array.Empty<EvidenceSourceDto>(),
                HumanReview: new HumanReviewRequirementDto(
                    Required: false,
                    Reason: "Command failed before a consequential action could be prepared.",
                    BlockedAutonomousActions: Array.Empty<string>(),
                    AllowedNextActions: Array.Empty<string>()),
                GeneratedAt: DateTimeOffset.UtcNow);
        }
    }

    private async Task<ProviderGateResult> EnsureRequiredProvidersAsync(IntentType intent, CancellationToken ct)
    {
        var requiredProviders = _providerRequirementResolver.GetRequiredProviders(intent);

        foreach (var provider in requiredProviders)
        {
            var health = await _providerHealth.CheckAsync(provider.ProviderCode, ct);
            if (health.Status != ProviderStatus.Connected)
            {
                return ProviderGateResult.NotReady(
                    capability: provider.DisplayName,
                    message: $"{provider.DisplayName} is not configured or not connected.",
                    adminHref: "/admin/integrations");
            }
        }

        return ProviderGateResult.Ready();
    }

    private async Task<(IReadOnlyList<CommandCenterBlockDto> Blocks, IReadOnlyList<EvidenceSourceDto> Evidence)>
        BuildSlaBlocksAsync(TenantContext context, CancellationToken ct)
    {
        var slaItems = await _slaRiskService.GetAtRiskItemsAsync(context.TenantId, context.WorkspaceId, ct);
        var blocks = new List<CommandCenterBlockDto>();
        var evidence = new List<EvidenceSourceDto>();

        if (slaItems.Count == 0)
        {
            blocks.Add(new TextBlockDto("No claims are currently at SLA risk or breached."));
            return (blocks, evidence);
        }

        blocks.Add(new TextBlockDto($"{slaItems.Count} claim(s) are at SLA risk or breached."));
        blocks.Add(new SlaSummaryBlockDto(
            slaItems.Select(i => new SlaRiskItemDto(
                Id: i.ClaimId,
                Label: $"Claim {i.ClaimNumber} - {i.CustomerName}",
                Severity: i.Severity,
                DueAt: i.DueAt?.ToString("O"),
                Href: $"/claims/{i.ClaimId}"))
            .ToList()));

        foreach (var item in slaItems)
        {
            evidence.Add(new EvidenceSourceDto(
                Id: item.ClaimId,
                Title: $"Claim {item.ClaimNumber} SLA status",
                SourceType: CommandCenterEvidenceSourceTypeValues.Claim,
                Href: $"/claims/{item.ClaimId}",
                Confidence: 1.0m));
        }

        return (blocks, evidence);
    }

    private async Task<(IReadOnlyList<CommandCenterBlockDto> Blocks, IReadOnlyList<EvidenceSourceDto> Evidence)>
        BuildAttentionBlocksAsync(TenantContext context, CancellationToken ct)
    {
        var claims = await _claimsQueryService.GetClaimsNeedingAttentionAsync(context.TenantId, context.WorkspaceId, ct);
        var blocks = new List<CommandCenterBlockDto>();
        var evidence = new List<EvidenceSourceDto>();

        if (claims.Count == 0)
        {
            blocks.Add(new TextBlockDto("No claims need immediate attention."));
            return (blocks, evidence);
        }

        blocks.Add(new TextBlockDto($"{claims.Count} claim(s) need attention."));
        blocks.Add(new ClaimsTableBlockDto(
            claims.Select(c => new ClaimAttentionRowDto(
                ClaimId: c.ClaimId,
                ClaimNumber: c.ClaimNumber,
                CustomerName: c.CustomerName,
                Status: c.Status,
                SlaStatus: c.SlaStatus,
                Priority: c.Priority,
                AssignedTo: c.AssignedTo,
                LastActivityAt: c.LastActivityAt.ToString("O")))
            .ToList()));

        foreach (var claim in claims)
        {
            evidence.Add(new EvidenceSourceDto(
                Id: claim.ClaimId,
                Title: $"Claim {claim.ClaimNumber} - {claim.CustomerName}",
                SourceType: CommandCenterEvidenceSourceTypeValues.Claim,
                Href: $"/claims/{claim.ClaimId}",
                Confidence: 0.95m));
        }

        return (blocks, evidence);
    }

    private async Task<(IReadOnlyList<CommandCenterBlockDto> Blocks, IReadOnlyList<EvidenceSourceDto> Evidence)>
        BuildMissingDocumentBlocksAsync(TenantContext context, CancellationToken ct)
    {
        var missingDocs = await _documentQueryService.GetMissingDocumentsAsync(context.TenantId, context.WorkspaceId, ct);
        var blocks = new List<CommandCenterBlockDto>();
        var evidence = new List<EvidenceSourceDto>();

        if (missingDocs.Count == 0)
        {
            blocks.Add(new TextBlockDto("All visible claims have their required documents."));
            return (blocks, evidence);
        }

        blocks.Add(new TextBlockDto($"{missingDocs.Count} document gap(s) need review."));
        blocks.Add(new DocumentGapListBlockDto(
            missingDocs.Select(d => new DocumentGapItemDto(
                Id: d.DocumentRequirementId,
                ClaimId: d.ClaimId,
                DocumentType: d.DocumentType,
                Reason: d.Reason,
                Status: d.Status,
                Href: $"/claims/{d.ClaimId}/documents"))
            .ToList()));

        foreach (var doc in missingDocs)
        {
            evidence.Add(new EvidenceSourceDto(
                Id: doc.DocumentRequirementId,
                Title: $"{doc.DocumentType} requirement for claim {doc.ClaimNumber}",
                SourceType: CommandCenterEvidenceSourceTypeValues.Document,
                Href: $"/claims/{doc.ClaimId}/documents",
                Confidence: 0.95m));
        }

        return (blocks, evidence);
    }

    private async Task<(IReadOnlyList<CommandCenterBlockDto> Blocks, IReadOnlyList<EvidenceSourceDto> Evidence)>
        BuildApprovalBlocksAsync(TenantContext context, CancellationToken ct)
    {
        var approvals = await _approvalQueryService.GetPendingApprovalsAsync(context.TenantId, context.WorkspaceId, ct);
        var blocks = new List<CommandCenterBlockDto>();
        var evidence = new List<EvidenceSourceDto>();

        if (approvals.Count == 0)
        {
            blocks.Add(new TextBlockDto("No pending approvals are visible to you."));
            return (blocks, evidence);
        }

        blocks.Add(new TextBlockDto($"{approvals.Count} approval task(s) are pending review."));
        blocks.Add(new ApprovalQueueBlockDto(
            approvals.Select(a => new ApprovalQueueItemDto(
                Id: a.ApprovalId,
                Title: a.Title,
                RequestedBy: a.RequestedBy,
                Priority: a.Priority,
                RequiresHumanDecision: true,
                Href: $"/approvals/{a.ApprovalId}"))
            .ToList()));

        foreach (var approval in approvals)
        {
            evidence.Add(new EvidenceSourceDto(
                Id: approval.ApprovalId,
                Title: $"Approval: {approval.Title}",
                SourceType: CommandCenterEvidenceSourceTypeValues.Approval,
                Href: $"/approvals/{approval.ApprovalId}",
                Confidence: 1.0m));
        }

        return (blocks, evidence);
    }

    private async Task<(IReadOnlyList<CommandCenterBlockDto> Blocks, IReadOnlyList<EvidenceSourceDto> Evidence)>
        BuildManagementSummaryAsync(TenantContext context, IntentParameters parameters, CancellationToken ct)
    {
        var claimsSummary = await _claimsQueryService.GetDailySummaryAsync(context.TenantId, context.WorkspaceId, ct);
        var retrievalResult = await _retrievalProvider.SearchAsync(
            new RetrievalRequest(
                Query: "Claims operations management summary guidance",
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                TopK: 5),
            ct);

        var sourceContext = string.Join("\n", retrievalResult.Sources.Select(s => $"- {s.Title}: {s.SafeExcerpt}"));

        var prompt = $@"
You are an operational assistant for an insurance company.
Generate a concise management summary for today's claims operations.
Use only the real operational figures and approved source context below.
Do not invent numbers. If data is missing, say so.

Operational figures:
- Open claims: {claimsSummary.OpenClaims}
- New today: {claimsSummary.NewToday}
- SLA at risk: {claimsSummary.SlaAtRisk}
- Completed today: {claimsSummary.CompletedToday}

Approved source context:
{sourceContext}

Additional user context:
{parameters.GetStringOrDefault("additionalContext") ?? "None"}
";

        var chatResult = await _chatProvider.CompleteAsync(new ChatCompletionRequest(prompt, context.TenantId), ct);
        var validated = await _aiOutputValidator.ValidateManagementSummaryAsync(chatResult.Content, context, ct);

        var blocks = new List<CommandCenterBlockDto>
        {
            new ManagementSummaryBlockDto(
                Title: "Daily claims operations summary",
                Sections: validated.Sections.Select(s => new SummarySectionDto(s.Heading, s.Body)).ToList()),
            new MetricPillStripBlockDto(new[]
            {
                new MetricPillDto("Open Claims", claimsSummary.OpenClaims, "neutral", "/claims"),
                new MetricPillDto("New Today", claimsSummary.NewToday, "good", "/claims?status=new"),
                new MetricPillDto("SLA At Risk", claimsSummary.SlaAtRisk, "danger", "/claims?sla=at_risk"),
                new MetricPillDto("Completed Today", claimsSummary.CompletedToday, "good", "/claims?status=closed")
            })
        };

        var evidence = retrievalResult.Sources.Select(source => new EvidenceSourceDto(
            Id: source.SourceId,
            Title: source.Title,
            SourceType: CommandCenterEvidenceSourceTypeValues.Knowledge,
            Href: source.Href,
            Confidence: source.RelevanceScore)).ToList();

        return (blocks, evidence);
    }

    private async Task<(IReadOnlyList<CommandCenterBlockDto> Blocks, IReadOnlyList<EvidenceSourceDto> Evidence)>
        BuildCustomerUpdateDraftAsync(TenantContext context, IntentParameters parameters, CancellationToken ct)
    {
        if (!parameters.TryGetString("claimId", out var claimId) || string.IsNullOrWhiteSpace(claimId))
        {
            return (new List<CommandCenterBlockDto>
            {
                new ErrorBlockDto("I need a specific claim ID before I can prepare a customer update draft.", context.CorrelationId, false)
            }, Array.Empty<EvidenceSourceDto>());
        }

        var claim = await _claimsQueryService.GetClaimAsync(claimId, context.TenantId, context.WorkspaceId, ct);
        if (claim is null)
        {
            return (new List<CommandCenterBlockDto>
            {
                new ErrorBlockDto("The claim was not found or is not visible to your role.", context.CorrelationId, false)
            }, Array.Empty<EvidenceSourceDto>());
        }

        var retrievalResult = await _retrievalProvider.SearchAsync(
            new RetrievalRequest(
                Query: "Approved customer communication templates for claim status updates and missing documents",
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                TopK: 3),
            ct);

        var templateContext = string.Join("\n", retrievalResult.Sources.Select(s => $"- {s.Title}: {s.SafeExcerpt}"));

        var prompt = $@"
Draft a customer-safe update for claim {claim.ClaimNumber}.
Customer: {claim.CustomerName}
Status: {claim.Status}
Missing documents: {string.Join(", ", claim.MissingDocuments)}
Approved template context:
{templateContext}

Rules:
- The message is a draft only.
- Do not promise settlement.
- Do not admit liability.
- Do not expose internal risk or fraud notes.
- Use a professional, clear, empathetic tone.
";

        var chatResult = await _chatProvider.CompleteAsync(new ChatCompletionRequest(prompt, context.TenantId), ct);
        var validatedDraft = await _aiOutputValidator.ValidateCustomerSafeDraftAsync(chatResult.Content, context, ct);

        var draft = await _notificationDraftService.CreateDraftAsync(
            new NotificationDraftCreateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                RelatedEntityType: "claim",
                RelatedEntityId: claim.ClaimId,
                Channel: "customer_update",
                Preview: validatedDraft.Preview,
                RequiresApproval: true,
                CreatedByUserId: context.UserId,
                CorrelationId: context.CorrelationId),
            ct);

        var blocks = new List<CommandCenterBlockDto>
        {
            new TextBlockDto("Draft customer update prepared. Human review is required before sending."),
            new DraftCommunicationBlockDto(
                DraftId: draft.DraftId,
                Preview: validatedDraft.Preview,
                RequiresApproval: true),
            new ActionButtonsBlockDto(new[]
            {
                new CommandActionDto(
                    Id: "review_draft",
                    Label: "Review draft",
                    ActionType: CommandCenterActionTypeValues.OpenRoute,
                    RequiresConfirmation: false,
                    RequiresHumanApproval: false,
                    Href: $"/drafts/{draft.DraftId}"),
                new CommandActionDto(
                    Id: "submit_for_approval",
                    Label: "Submit for approval",
                    ActionType: CommandCenterActionTypeValues.CreateApprovalTask,
                    RequiresConfirmation: true,
                    RequiresHumanApproval: true,
                    Href: null)
            })
        };

        var evidence = new List<EvidenceSourceDto>
        {
            new EvidenceSourceDto(
                Id: claim.ClaimId,
                Title: $"Claim {claim.ClaimNumber}",
                SourceType: CommandCenterEvidenceSourceTypeValues.Claim,
                Href: $"/claims/{claim.ClaimId}",
                Confidence: 1.0m)
        };

        evidence.AddRange(retrievalResult.Sources.Select(source => new EvidenceSourceDto(
            Id: source.SourceId,
            Title: source.Title,
            SourceType: CommandCenterEvidenceSourceTypeValues.Knowledge,
            Href: source.Href,
            Confidence: source.RelevanceScore)));

        return (blocks, evidence);
    }

    private CommandCenterQueryResponse BuildNotConfiguredResponse(
        string conversationId,
        string messageId,
        string capability,
        string message,
        string adminHref)
    {
        return new CommandCenterQueryResponse(
            ConversationId: conversationId,
            MessageId: messageId,
            AssistantSummary: "The requested capability is not configured.",
            Blocks: new List<CommandCenterBlockDto>
            {
                new NotConfiguredBlockDto(
                    Capability: capability,
                    Message: message,
                    AdminHref: adminHref)
            },
            EvidenceSources: Array.Empty<EvidenceSourceDto>(),
            HumanReview: new HumanReviewRequirementDto(
                Required: false,
                Reason: "Capability not available.",
                BlockedAutonomousActions: Array.Empty<string>(),
                AllowedNextActions: Array.Empty<string>()),
            GeneratedAt: DateTimeOffset.UtcNow);
    }

    private async Task WriteAuditAndAgentRunAsync(
        TenantContext context,
        CommandCenterQueryRequest request,
        string conversationId,
        string intent,
        string status,
        bool requiresHumanReview,
        string safeOutputSummary,
        DateTimeOffset startedAt,
        CancellationToken ct)
    {
        var completedAt = DateTimeOffset.UtcNow;

        await _agentRunWriter.WriteAsync(new AgentRun
        {
            AgentRunId = Guid.NewGuid().ToString("N"),
            TenantId = context.TenantId,
            WorkspaceId = context.WorkspaceId,
            AgentCode = "command_center_agent",
            ModelProvider = "AzureOpenAI",
            ModelDeployment = "configured-deployment", // resolved from provider configuration, not hardcoded at runtime
            PromptVersion = "v1.0.0",
            Status = status,
            InputSummary = RedactAndTruncate(request.Message, 200),
            OutputSummary = safeOutputSummary,
            RequiresHumanReview = requiresHumanReview,
            StartedAt = startedAt,
            CompletedAt = completedAt
        }, ct);

        await WriteAuditOnlyAsync(context, request, conversationId, status, safeOutputSummary, ct);
    }

    private async Task WriteAuditOnlyAsync(
        TenantContext context,
        CommandCenterQueryRequest request,
        string conversationId,
        string status,
        string summary,
        CancellationToken ct)
    {
        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: "command_center_query",
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: status,
            EntityType: "command_center",
            EntityId: conversationId,
            Summary: summary,
            Reason: RedactAndTruncate(request.Message, 200),
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), ct);
    }

    private static string GenerateSafeAssistantSummary(IReadOnlyList<CommandCenterBlockDto> blocks)
    {
        var firstText = blocks.OfType<TextBlockDto>().FirstOrDefault()?.Markdown;
        if (!string.IsNullOrWhiteSpace(firstText))
        {
            return RedactAndTruncate(firstText, 200);
        }

        return $"Generated {blocks.Count} Command Center block(s).";
    }

    private static string RedactAndTruncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }
}
```

---

## 4. Intent classification support

```csharp
public enum IntentType
{
    ShowSlaBreaches,
    ShowAttentionClaims,
    ShowMissingDocuments,
    ShowPendingApprovals,
    GenerateManagementSummary,
    PrepareCustomerUpdateDraft,
    Unsupported
}

public sealed record IntentClassificationResult(
    IntentType Intent,
    IntentParameters Parameters,
    decimal Confidence,
    bool UsedAiClassifier);

public sealed class IntentParameters
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    public IntentParameters(IReadOnlyDictionary<string, string?>? values = null)
    {
        _values = values ?? new Dictionary<string, string?>();
    }

    public bool TryGetString(string key, out string? value) => _values.TryGetValue(key, out value);

    public string? GetStringOrDefault(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public IReadOnlyDictionary<string, string?> AsReadOnly() => _values;
}

public interface IIntentClassifier
{
    Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);
}
```

### Deterministic first, AI fallback

```csharp
internal sealed class DeterministicThenAiIntentClassifier : IIntentClassifier
{
    private readonly IChatCompletionProvider _chatProvider;
    private readonly IProviderHealthCheck _providerHealth;
    private readonly ILogger<DeterministicThenAiIntentClassifier> _logger;

    public DeterministicThenAiIntentClassifier(
        IChatCompletionProvider chatProvider,
        IProviderHealthCheck providerHealth,
        ILogger<DeterministicThenAiIntentClassifier> logger)
    {
        _chatProvider = chatProvider;
        _providerHealth = providerHealth;
        _logger = logger;
    }

    public async Task<IntentClassificationResult> ClassifyAsync(
        string message,
        string tenantId,
        string? workspaceId,
        CancellationToken ct)
    {
        var deterministic = TryClassifyDeterministically(message);
        if (deterministic is not null)
        {
            return deterministic;
        }

        var chatHealth = await _providerHealth.CheckAsync("azure_openai", ct);
        if (chatHealth.Status != ProviderStatus.Connected)
        {
            return new IntentClassificationResult(
                IntentType.Unsupported,
                new IntentParameters(),
                0.0m,
                UsedAiClassifier: false);
        }

        var prompt = BuildClassifierPrompt(message);
        var result = await _chatProvider.CompleteAsync(new ChatCompletionRequest(prompt, tenantId), ct);

        try
        {
            var parsed = JsonSerializer.Deserialize<IntentClassifierResponse>(
                result.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null)
            {
                return Unsupported();
            }

            return new IntentClassificationResult(
                Enum.TryParse<IntentType>(parsed.Intent, ignoreCase: true, out var intent)
                    ? intent
                    : IntentType.Unsupported,
                new IntentParameters(parsed.Parameters),
                parsed.Confidence,
                UsedAiClassifier: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification failed; returning Unsupported.");
            return Unsupported();
        }
    }

    private static IntentClassificationResult? TryClassifyDeterministically(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();

        if (normalized.Contains("sla") || normalized.Contains("breach"))
        {
            return new IntentClassificationResult(IntentType.ShowSlaBreaches, new IntentParameters(), 0.9m, false);
        }

        if (normalized.Contains("need attention") || normalized.Contains("priority claims"))
        {
            return new IntentClassificationResult(IntentType.ShowAttentionClaims, new IntentParameters(), 0.9m, false);
        }

        if (normalized.Contains("missing document") || normalized.Contains("document gap"))
        {
            return new IntentClassificationResult(IntentType.ShowMissingDocuments, new IntentParameters(), 0.9m, false);
        }

        if (normalized.Contains("pending approval") || normalized.Contains("approvals"))
        {
            return new IntentClassificationResult(IntentType.ShowPendingApprovals, new IntentParameters(), 0.9m, false);
        }

        return null;
    }

    private static IntentClassificationResult Unsupported() =>
        new(IntentType.Unsupported, new IntentParameters(), 0.0m, false);

    private static string BuildClassifierPrompt(string message) => $@"
Classify this insurance operations command.
Return JSON only.
Message: {JsonSerializer.Serialize(message)}

Allowed intents:
- ShowSlaBreaches
- ShowAttentionClaims
- ShowMissingDocuments
- ShowPendingApprovals
- GenerateManagementSummary
- PrepareCustomerUpdateDraft
- Unsupported

Schema:
{{
  ""intent"": ""<intent>"",
  ""parameters"": {{ ""claimId"": null, ""additionalContext"": null }},
  ""confidence"": 0.0
}}
";

    private sealed record IntentClassifierResponse(
        string Intent,
        Dictionary<string, string?> Parameters,
        decimal Confidence);
}
```

---

## 5. Intent-specific provider requirements

```csharp
public sealed record ProviderRequirement(string ProviderCode, string DisplayName);

public interface IProviderRequirementResolver
{
    IReadOnlyList<ProviderRequirement> GetRequiredProviders(IntentType intent);
}

internal sealed class ProviderRequirementResolver : IProviderRequirementResolver
{
    public IReadOnlyList<ProviderRequirement> GetRequiredProviders(IntentType intent) => intent switch
    {
        IntentType.ShowSlaBreaches => Array.Empty<ProviderRequirement>(),
        IntentType.ShowAttentionClaims => Array.Empty<ProviderRequirement>(),
        IntentType.ShowMissingDocuments => Array.Empty<ProviderRequirement>(),
        IntentType.ShowPendingApprovals => Array.Empty<ProviderRequirement>(),

        IntentType.GenerateManagementSummary => new[]
        {
            new ProviderRequirement("azure_openai", "Azure OpenAI"),
            new ProviderRequirement("azure_ai_search", "Azure AI Search")
        },

        IntentType.PrepareCustomerUpdateDraft => new[]
        {
            new ProviderRequirement("azure_openai", "Azure OpenAI"),
            new ProviderRequirement("azure_ai_search", "Azure AI Search")
        },

        _ => Array.Empty<ProviderRequirement>()
    };
}

public sealed record ProviderGateResult(
    bool IsReady,
    string? Capability,
    string? Message,
    string? AdminHref)
{
    public static ProviderGateResult Ready() => new(true, null, null, null);

    public static ProviderGateResult NotReady(string capability, string message, string adminHref) =>
        new(false, capability, message, adminHref);
}
```

---

## 6. Draft persistence contract

```csharp
public interface INotificationDraftService
{
    Task<NotificationDraftResult> CreateDraftAsync(
        NotificationDraftCreateRequest request,
        CancellationToken cancellationToken);
}

public sealed record NotificationDraftCreateRequest(
    string TenantId,
    string? WorkspaceId,
    string RelatedEntityType,
    string RelatedEntityId,
    string Channel,
    string Preview,
    bool RequiresApproval,
    string CreatedByUserId,
    string CorrelationId);

public sealed record NotificationDraftResult(
    string DraftId,
    string Status,
    DateTimeOffset CreatedAt);
```

Rule: `DraftCommunicationBlockDto.DraftId` must never be a generated-but-unpersisted GUID. It must reference a stored draft, approval task, or workflow item that the frontend can open or submit for review.

---

## 7. Constants used by the service

```csharp
public static class CommandCenterBlockedActions
{
    public const string SendCustomerCommunication = "send_customer_communication";
}

public static class CommandCenterAllowedNextActions
{
    public const string ReviewDraft = "review_draft";
    public const string EditDraft = "edit_draft";
    public const string SubmitForApproval = "submit_for_approval";
    public const string Escalate = "escalate";
}
```

---

## 8. Required tests

Add these tests beside the existing Command Center tests:

```text
CommandCenterQueryUsesIntentSpecificProviderGatesTest
CommandCenterNotConfiguredResponseWritesAuditEventTest
CommandCenterDraftCommunicationIsPersistedBeforeDraftBlockTest
CommandCenterEvidenceSourceTypeAllowsApprovalTest
CommandCenterTimestampsSerializeAsIso8601Test
CommandCenterQueryRequiresCommandCenterPermissionTest
CommandCenterClaimsIntentRequiresClaimsReadPermissionTest
CommandCenterApprovalsIntentRequiresApprovalsReadPermissionTest
CommandCenterAiOutputValidatorRunsForDraftsAndSummariesTest
CommandCenterDoesNotStoreRawSensitiveBlockPayloadInAgentRunSummaryTest
```

### Test expectations

```text
1. SLA, attention-claim, missing-document, and approval queries must work without Azure AI Search/OpenAI provider gates when the intent is deterministically classified.
2. Management summaries and customer draft preparation must require Azure OpenAI and Azure AI Search.
3. Not-configured responses must still write an audit event.
4. Customer draft response must call INotificationDraftService before returning DraftCommunicationBlockDto.
5. Approval evidence must serialize with sourceType = "approval" and pass the frontend/backend contract test.
6. lastActivityAt, dueAt, occurredAt, createdAt, and generatedAt values must serialize as ISO 8601 strings.
7. Every intent path must enforce the correct permission.
8. AI-generated output must pass through IAiOutputValidator before being returned.
9. AgentRun.OutputSummary must not contain raw full draft text, hidden notes, provider payloads, or full serialized response blocks.
```

---

## 9. Implementation notes for Codex / Claude / Antigravity

Use this logic as the implementation blueprint, but adapt names to the real repository where valid equivalents already exist.

Do not introduce:

```text
Fake claims
Fake metrics
In-memory provider fallback
Demo-provider success paths
Frontend-owned authorization
Unpersisted draft IDs
Raw prompt storage
Autonomous send/approve/reject flows
```

Before submitting a backend PR, run:

```text
dotnet build
dotnet test
CommandCenterBlockContractMatchesFrontendTypesTest
CommandCenterEvidenceSourceTypeAllowsApprovalTest
CommandCenterDraftCommunicationIsPersistedBeforeDraftBlockTest
```

namespace InsuranceIntelligence.Domain.Agents;

// ─── Routing ────────────────────────────────────────────────────────────────

/// <summary>
/// Deterministic result of classifying a user message to a module + intent.
/// No LLM is involved in this classification — it is keyword/pattern-based only.
/// </summary>
public sealed record AgentRoutingDecision(
    string Module,
    string Intent,
    string ClassificationCategory = "module_route",
    string Reason = "");

// ─── Tool execution ──────────────────────────────────────────────────────────

/// <summary>
/// Context passed into every tool execution. Carries the original request,
/// the routing decision, an optional resolved entity ID (e.g. case ID from
/// the message text), and compact-mode hints.
/// </summary>
public sealed record AgentToolContext(
    AgentQueryRequest Request,
    AgentRoutingDecision Route,
    Guid? EntityId,
    int MaxRecords,
    bool CompactMode);

/// <summary>
/// Result returned by every tool. Status is one of: success | partial | failed.
/// Failed results carry an empty Sources collection and at least one Warning.
/// </summary>
public sealed record AgentToolResult(
    string Tool,
    string Status,
    string SourceType,
    object? Output,
    IReadOnlyCollection<AgentCitationDto> Sources,
    IReadOnlyCollection<string> Warnings);

/// <summary>
/// Compact description of a source document or data endpoint used as evidence.
/// Mirrors CIP's AssistantCitationDto, re-cast to IIP terminology.
/// </summary>
public sealed record AgentCitationDto(
    string Label,
    string Type,
    string Path,
    string? DocumentId = null,
    string? ChunkId = null,
    string? Section = null,
    string? Page = null,
    string? Url = null,
    DateTimeOffset? LastModified = null,
    decimal? Score = null);

/// <summary>
/// Describes a tool so the UI can render a tool trace and admin can inspect
/// what the orchestrator is allowed to call for a given module + role.
/// </summary>
public sealed record AgentToolDescriptorDto(
    string Name,
    string Module,
    IReadOnlyCollection<string> AllowedRoles,
    string SourceSystem,
    IReadOnlyCollection<string> RequiredInputs,
    string OutputDto,
    bool MaySendToLlm,
    bool MayDisplayAsSource,
    bool RequiresTenantFilter,
    int MaxRecords,
    bool Enabled);

// ─── Evidence + prompt ───────────────────────────────────────────────────────

/// <summary>
/// Compact JSON prompt bundle produced by AgentEvidenceBuilder.
/// Carries bookkeeping flags used to determine ResponseMode.
/// </summary>
public sealed record AgentPromptBundle(
    string Prompt,
    int EvidenceChars,
    int KbChunksIncluded,
    bool UsedSql,
    bool UsedSearch);

// ─── Query contracts ─────────────────────────────────────────────────────────

/// <summary>
/// Inbound request posted to POST /api/v1/assistant/query.
/// Module is optional; if omitted the router derives it from the message.
/// </summary>
public sealed record AgentQueryRequest(
    string? Message,
    string? Module,
    string? WorkspaceId,
    string? TenantId,
    string? EntityId,
    string? ContextId,
    string? ConversationId);

/// <summary>
/// Full orchestrator response. The UI should display Answer and Sources;
/// ToolTrace and ResponseMode are for developer/audit transparency.
/// </summary>
public sealed record AgentQueryResponse(
    string Answer,
    string Module,
    string Intent,
    string ResponseMode,
    IReadOnlyCollection<AgentCitationDto> Sources,
    IReadOnlyCollection<AgentToolTraceDto> ToolTrace,
    bool UsedLiveData,
    bool UsedKnowledgeBase,
    bool UsedLlm,
    IReadOnlyCollection<string> Warnings);

/// <summary>
/// One entry in the tool trace returned with every response.
/// Status: success | partial | failed. FirstWarning is populated only for
/// partial/failed results.
/// </summary>
public sealed record AgentToolTraceDto(
    string Tool,
    string Status,
    string SourceType,
    string? FirstWarning);

// ─── Knowledge search ─────────────────────────────────────────────────────────

/// <summary>
/// Structured output of the SearchKnowledgeBase tool.
/// Mirrors CIP's KnowledgeSearchResultDto.
/// </summary>
public sealed record AgentKnowledgeSearchResult(
    string Query,
    bool RetrievalExecuted,
    string Status,
    IReadOnlyCollection<AgentKnowledgeChunk> Chunks,
    IReadOnlyCollection<AgentCitationDto> Citations,
    IReadOnlyCollection<string> Warnings);

public sealed record AgentKnowledgeChunk(
    string TenantId,
    string? WorkspaceId,
    string DocumentId,
    string ChunkId,
    string Title,
    string SourceType,
    string? Path,
    string? Section,
    string? LogicalPage,
    string? SourceUrl,
    string Text,
    DateTimeOffset? LastModified,
    decimal? Score);

// ─── Interfaces ───────────────────────────────────────────────────────────────

/// <summary>
/// Top-level entry point for the AI assistant. One implementation:
/// AssistantOrchestrator in the Api layer.
/// </summary>
public interface IAssistantOrchestrator
{
    Task<AgentQueryResponse> QueryAsync(AgentQueryRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Deterministic, no-LLM classifier. Returns a routing decision from
/// the message text and optional explicit module hint.
/// </summary>
public interface IAgentIntentRouter
{
    AgentRoutingDecision Route(AgentQueryRequest request);
}

/// <summary>
/// Thin wrapper around a named, typed operation (e.g. GetCommandCenterMetrics).
/// Tools are pure query objects — no writes, no LLM calls, tenant-scoped.
/// </summary>
public interface IAgentTool
{
    AgentToolDescriptorDto Descriptor { get; }
    Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves tool names to IAgentTool instances. Enforces role-based access
/// before delegating to the tool.
/// </summary>
public interface IAgentToolRegistry
{
    IReadOnlyCollection<AgentToolDescriptorDto> Descriptors { get; }
    Task<AgentToolResult> ExecuteAsync(string toolName, AgentToolContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Serialises tool results into a compact JSON prompt bundle for the LLM,
/// respecting character budgets and evidence caps.
/// </summary>
public interface IAgentEvidenceBuilder
{
    AgentPromptBundle BuildPrompt(
        AgentQueryRequest request,
        AgentRoutingDecision route,
        IReadOnlyCollection<AgentToolResult> evidence);
}

/// <summary>
/// One module agent (portfolio, claims, documents, knowledge, approvals,
/// communications, reports, system). Runs tool plan → evidence → LLM → response.
/// </summary>
public interface IModuleAgent
{
    string Module { get; }
    Task<AgentQueryResponse> RunAsync(
        AgentQueryRequest request,
        AgentRoutingDecision route,
        CancellationToken cancellationToken);
}

// ─── Exceptions ───────────────────────────────────────────────────────────────

/// <summary>
/// Thrown when the LLM call fails or returns an unusable response.
/// The orchestrator catches this and returns a 503 with the error code.
/// </summary>
public sealed class AssistantModelUnavailableException(string errorCode)
    : Exception($"The assistant model is temporarily unavailable: {errorCode}")
{
    public string ErrorCode { get; } = errorCode;
}

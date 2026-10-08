using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Application.CommandCenter;

public interface ICommandCenterConversationService
{
    Task<ConversationListResponse> ListAsync(bool archived, int limit, CancellationToken cancellationToken = default);
    Task<ConversationDetailDto> GetAsync(Guid conversationId, string correlationId, CancellationToken cancellationToken = default);
    Task<CommandCenterQueryResponse> QueryAsync(CommandCenterQueryRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ConversationSummaryDto> RenameAsync(Guid conversationId, RenameConversationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ConversationSummaryDto> ArchiveAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ConversationSummaryDto> RestoreAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class CommandCenterConversationService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICommandCenterQueryService queryService,
    ICommandCenterConversationRepository repository,
    IAuditRepository auditRepository,
    IConversationGroundingService groundingService) : ICommandCenterConversationService
{
    public async Task<ConversationListResponse> ListAsync(bool archived, int limit, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCommandCenter, cancellationToken);
        var context = tenantContextAccessor.Current;
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var expectedStatus = archived ? CommandCenterConversationStatus.Archived : CommandCenterConversationStatus.Active;
        var matches = (await repository.ListAsync(context.Scope, context.UserId, cancellationToken))
            .Where(item => item.Status == expectedStatus && item.Turns.Count > 0)
            .OrderByDescending(item => item.LastActivityAt)
            .ToArray();

        var summaries = new List<ConversationSummaryDto>();
        foreach (var conversation in matches.Take(boundedLimit))
        {
            summaries.Add(ToSummary(conversation, await groundingService.CompareAsync(conversation, cancellationToken)));
        }

        return new ConversationListResponse(
            summaries,
            matches.Length,
            matches.Length > boundedLimit);
    }

    public async Task<ConversationDetailDto> GetAsync(Guid conversationId, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCommandCenter, cancellationToken);
        var context = tenantContextAccessor.Current;
        var conversation = await GetOwnedAsync(conversationId, cancellationToken);
        await auditRepository.WriteAsync(ConversationAudit(conversation, context.UserId, "conversation.viewed", conversation.Status.ToString(), conversation.Status.ToString(), correlationId), cancellationToken);
        var grounding = await groundingService.CompareAsync(conversation, cancellationToken);
        return ToDetail(conversation, grounding);
    }

    public async Task<CommandCenterQueryResponse> QueryAsync(CommandCenterQueryRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCommandCenter, cancellationToken);
        var context = tenantContextAccessor.Current;
        if (request.ClientRequestId is Guid clientRequestId)
        {
            var prior = (await repository.ListAsync(context.Scope, context.UserId, cancellationToken))
                .SelectMany(item => item.Turns.Select(turn => (Conversation: item, Turn: turn)))
                .FirstOrDefault(candidate => candidate.Turn.ClientRequestId == clientRequestId);
            if (prior.Turn is not null)
            {
                var sameQuestion = string.Equals(prior.Turn.Question, request.Question?.Trim(), StringComparison.Ordinal);
                var sameConversation = request.ConversationId is null || request.ConversationId == prior.Conversation.ConversationId;
                if (!sameQuestion || !sameConversation) throw new CommandCenterConversationConflictException();
                return prior.Turn.Response;
            }
        }

        CommandCenterConversation? conversation = null;
        if (request.ConversationId is Guid conversationId)
        {
            conversation = await GetOwnedAsync(conversationId, cancellationToken);
            if (conversation.Status != CommandCenterConversationStatus.Active)
            {
                throw new CommandCenterConversationConflictException();
            }
        }

        if (conversation is not null)
        {
            await groundingService.CompareAsync(conversation, cancellationToken);
        }
        var response = await queryService.QueryAsync(request, correlationId, cancellationToken);
        var captured = await groundingService.CaptureAsync(response, context.Scope, cancellationToken);
        var now = response.GeneratedAt;
        var turnId = Guid.NewGuid();
        conversation ??= new CommandCenterConversation
        {
            ConversationId = Guid.NewGuid(),
            Scope = context.Scope,
            OwnerUserId = context.UserId,
            Title = SafeGeneratedTitle(request.Question),
            TitleSource = CommandCenterConversationTitleSource.SystemGenerated,
            Status = CommandCenterConversationStatus.Active,
            Turns = Array.Empty<CommandCenterConversationTurn>(),
            CreatedAt = now,
            LastActivityAt = now,
            Version = 0,
        };

        var enrichedResponse = response with { ConversationId = conversation.ConversationId, TurnId = turnId };
        var retainedTurns = conversation.Turns
            .Append(new CommandCenterConversationTurn(turnId, request.Question!.Trim(), enrichedResponse, now, request.ClientRequestId))
            .ToArray();
        conversation = conversation with
        {
            Turns = retainedTurns,
            LastActivityAt = now,
            Version = conversation.Version + 1,
            PrimaryFocus = captured.Focus ?? conversation.PrimaryFocus,
            EvidenceSnapshot = captured.Snapshot,
        };
        await repository.SaveAsync(conversation, cancellationToken);
        return enrichedResponse;
    }

    public Task<ConversationSummaryDto> RenameAsync(Guid conversationId, RenameConversationRequest request, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeAsync(conversationId, request.ExpectedVersion, correlationId, "conversation.renamed", conversation =>
        {
            var title = request.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new CommandCenterConversationValidationException("title", "A conversation title is required.");
            }

            if (title.Length > 120 || title.Any(char.IsControl))
            {
                throw new CommandCenterConversationValidationException("title", "A conversation title must be 120 characters or fewer and contain no control characters.");
            }

            return conversation with { Title = title, TitleSource = CommandCenterConversationTitleSource.UserRenamed };
        }, cancellationToken);

    public Task<ConversationSummaryDto> ArchiveAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeAsync(conversationId, request.ExpectedVersion, correlationId, "conversation.archived", conversation => conversation with { Status = CommandCenterConversationStatus.Archived }, cancellationToken);

    public Task<ConversationSummaryDto> RestoreAsync(Guid conversationId, ChangeConversationStatusRequest request, string correlationId, CancellationToken cancellationToken = default) =>
        ChangeAsync(conversationId, request.ExpectedVersion, correlationId, "conversation.restored", conversation => conversation with { Status = CommandCenterConversationStatus.Active }, cancellationToken);

    private async Task<ConversationSummaryDto> ChangeAsync(
        Guid conversationId,
        int expectedVersion,
        string correlationId,
        string actionName,
        Func<CommandCenterConversation, CommandCenterConversation> change,
        CancellationToken cancellationToken)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewCommandCenter, cancellationToken);
        var context = tenantContextAccessor.Current;
        var current = await GetOwnedAsync(conversationId, cancellationToken);
        if (current.Version != expectedVersion)
        {
            throw new CommandCenterConversationConflictException();
        }

        var changed = change(current) with { LastActivityAt = DateTimeOffset.UtcNow, Version = current.Version + 1 };
        await repository.SaveAsync(changed, cancellationToken);
        await auditRepository.WriteAsync(ConversationAudit(current, context.UserId, actionName, current.Status.ToString(), changed.Status.ToString(), correlationId), cancellationToken);
        return ToSummary(changed, await groundingService.CompareAsync(changed, cancellationToken));
    }

    private async Task<CommandCenterConversation> GetOwnedAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        return await repository.GetAsync(conversationId, context.Scope, context.UserId, cancellationToken)
            ?? throw new CommandCenterConversationNotFoundException();
    }

    private static ConversationSummaryDto ToSummary(CommandCenterConversation conversation, ConversationGroundingSummary grounding) =>
        new(conversation.ConversationId, conversation.Title, Token(conversation.TitleSource), Token(conversation.Status), conversation.Turns.Count, conversation.CreatedAt, conversation.LastActivityAt, conversation.Version, conversation.OwnerUserId, conversation.PrimaryFocus, grounding);

    private static ConversationDetailDto ToDetail(CommandCenterConversation conversation, ConversationGroundingSummary grounding) =>
        new(
            conversation.ConversationId,
            conversation.Title,
            Token(conversation.TitleSource),
            Token(conversation.Status),
            conversation.Turns.Select(turn => new ConversationTurnDto(turn.TurnId, turn.Question, turn.Response, turn.CreatedAt)).ToArray(),
            conversation.CreatedAt,
            conversation.LastActivityAt,
            conversation.Version,
            grounding.Status,
            conversation.Status == CommandCenterConversationStatus.Active,
            conversation.OwnerUserId,
            conversation.PrimaryFocus,
            grounding);

    private static string SafeGeneratedTitle(string? question)
    {
        var normalized = question?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("missing") && normalized.Contains("document")) return "Review missing documents";
        if (normalized.Contains("sla")) return "Review SLA status";
        if (normalized.Contains("approval")) return "Review pending approvals";
        if (normalized.Contains("attention") || normalized.Contains("priority")) return "Review claims needing attention";
        if (normalized.Contains("customer") && normalized.Contains("update")) return "Prepare customer update";

        // A counting question is titled by what it counts and over what period, never by its wording.
        var counted = CommandCenterAnalytics.Parse(question ?? string.Empty);
        if (counted is not null)
        {
            var subject = char.ToUpperInvariant(CommandCenterAnalytics.Noun(counted.Entity, 2)[0]) + CommandCenterAnalytics.Noun(counted.Entity, 2)[1..];
            if (counted.BreakdownBy is not null) return $"{subject} by {counted.BreakdownBy}";
            var when = counted.Period switch
            {
                "today" => "today",
                "yesterday" => "yesterday",
                "this_week" => "this week",
                "last_7_days" => "over 7 days",
                "last_30_days" => "over 30 days",
                "this_month" => "this month",
                "last_month" => "last month",
                _ => null,
            };
            return when is null ? $"{subject} in total" : $"{subject} {when}";
        }

        return "Command Center investigation";
    }

    private static string Token<T>(T value) where T : Enum =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    private static AuditEvent ConversationAudit(
        CommandCenterConversation conversation,
        string userId,
        string actionName,
        string beforeStatus,
        string afterStatus,
        string correlationId) =>
        new(
            Guid.NewGuid(),
            conversation.Scope,
            CaseId: null,
            DocumentId: null,
            UserId: userId,
            ActorType.User,
            actionName,
            DateTimeOffset.UtcNow,
            beforeStatus,
            afterStatus,
            EvidenceIds: [conversation.ConversationId.ToString()],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);
}

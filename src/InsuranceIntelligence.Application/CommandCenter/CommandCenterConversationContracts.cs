using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.CommandCenter;

public enum CommandCenterConversationStatus
{
    Active,
    Archived,
}

public enum CommandCenterConversationTitleSource
{
    SystemGenerated,
    UserRenamed,
}

public sealed record CommandCenterConversationTurn(
    Guid TurnId,
    string Question,
    CommandCenterQueryResponse Response,
    DateTimeOffset CreatedAt);

public sealed record CommandCenterConversation
{
    public required Guid ConversationId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string OwnerUserId { get; init; }
    public required string Title { get; init; }
    public required CommandCenterConversationTitleSource TitleSource { get; init; }
    public required CommandCenterConversationStatus Status { get; init; }
    public required IReadOnlyList<CommandCenterConversationTurn> Turns { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastActivityAt { get; init; }
    public required int Version { get; init; }
}

public interface ICommandCenterConversationRepository
{
    Task SaveAsync(CommandCenterConversation conversation, CancellationToken cancellationToken = default);
    Task<CommandCenterConversation?> GetAsync(Guid conversationId, TenantScope scope, string ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommandCenterConversation>> ListAsync(TenantScope scope, string ownerUserId, CancellationToken cancellationToken = default);
}

public sealed record ConversationSummaryDto(
    Guid ConversationId,
    string Title,
    string TitleSource,
    string Status,
    int TurnCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    int Version);

public sealed record ConversationTurnDto(
    Guid TurnId,
    string Question,
    CommandCenterQueryResponse Response,
    DateTimeOffset CreatedAt);

public sealed record ConversationDetailDto(
    Guid ConversationId,
    string Title,
    string TitleSource,
    string Status,
    IReadOnlyList<ConversationTurnDto> Turns,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    int Version,
    string GroundingStatus,
    bool CanContinue);

public sealed record ConversationListResponse(
    IReadOnlyList<ConversationSummaryDto> Items,
    int Total,
    bool HasMore);

public sealed record RenameConversationRequest(string? Title, int ExpectedVersion);
public sealed record ChangeConversationStatusRequest(int ExpectedVersion);

public sealed class CommandCenterConversationNotFoundException : Exception;
public sealed class CommandCenterConversationConflictException : Exception;
public sealed class CommandCenterConversationValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

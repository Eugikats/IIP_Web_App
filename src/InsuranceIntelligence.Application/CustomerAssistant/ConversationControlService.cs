using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.CustomerAssistant;

public sealed record ConversationControlDto(string Mode, string ModeLabel, DateTimeOffset? Since, string? ByName, TimeSpan HandBackAfterIdle);

public interface IConversationControlService
{
    Task<ConversationControlDto> GetAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>A person takes the conversation, or hands it back to Kato.</summary>
    Task<ConversationControlDto> SetAsync(string threadId, ConversationMode mode, CancellationToken cancellationToken = default);

    /// <summary>Records that a named person has just replied on this thread, which is what keeps Kato
    /// quiet. Called after the reply has gone out; never throws for the caller.</summary>
    Task RecordReplyAsync(string threadId, string customerSafeBody, CancellationToken cancellationToken = default);
}

/// <summary>
/// Take-over and hand-back for WhatsApp conversations. The state lives in the conversation's own turns
/// (see <see cref="ConversationControl"/>), so it is auditable, needs no new table, and a conversation
/// can never be both Kato's and a person's.
/// </summary>
public sealed class ConversationControlService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ICustomerConversationRepository conversationRepository,
    IWhatsAppMessageRepository messageRepository,
    IAuditRepository auditRepository,
    IUserAccountRepository? userAccountRepository = null) : IConversationControlService
{
    public async Task<ConversationControlDto> GetAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var scope = tenantContextAccessor.Current.Scope;
        var turns = await TurnsAsync(threadId, cancellationToken);
        return await ToDtoAsync(turns, cancellationToken);
    }

    public async Task<ConversationControlDto> SetAsync(string threadId, ConversationMode mode, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageCustomerCommunications, cancellationToken);
        var context = tenantContextAccessor.Current;
        var waId = await ContactOfAsync(threadId, cancellationToken)
            ?? throw new ArgumentException("This conversation was not found in this workspace.", nameof(threadId));

        var turns = await TurnsAsync(threadId, cancellationToken);
        var current = ConversationControl.ModeOf(turns, DateTimeOffset.UtcNow);
        if (current != mode)
        {
            await SaveAsync(
                threadId, waId, context.UserId,
                mode == ConversationMode.Person ? CustomerAssistantOutcome.HumanTookOver : CustomerAssistantOutcome.HandedBackToKato,
                null, cancellationToken);
            await AuditAsync(
                mode == ConversationMode.Person ? "whatsapp.conversation.taken_over" : "whatsapp.conversation.handed_back",
                mode.ToString(), "Control of a WhatsApp conversation changed. Message text omitted.", cancellationToken);
            turns = await TurnsAsync(threadId, cancellationToken);
        }

        return await ToDtoAsync(turns, cancellationToken);
    }

    public async Task RecordReplyAsync(string threadId, string customerSafeBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = tenantContextAccessor.Current;
            var waId = await ContactOfAsync(threadId, cancellationToken);
            if (waId is null)
            {
                return;
            }

            await SaveAsync(threadId, waId, context.UserId, CustomerAssistantOutcome.HumanReplied, customerSafeBody, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The reply has already gone to the customer; losing this record only means Kato may
            // answer sooner than intended, which the officer can correct with Take over.
        }
    }

    private async Task<IReadOnlyList<CustomerConversationTurn>> TurnsAsync(string threadId, CancellationToken cancellationToken)
    {
        var turns = (await conversationRepository.ListForThreadAsync(tenantContextAccessor.Current.Scope, threadId.Trim(), cancellationToken))
            .OrderBy(turn => turn.OccurredAt)
            .ToArray();
        var lastReset = Array.FindLastIndex(turns, turn => turn.Outcome == CustomerAssistantOutcome.SessionReset);
        return lastReset < 0 ? turns : turns[(lastReset + 1)..];
    }

    private async Task<string?> ContactOfAsync(string threadId, CancellationToken cancellationToken) =>
        (await messageRepository.ListThreadAsync(tenantContextAccessor.Current.Scope, threadId.Trim(), cancellationToken))
            .Select(message => message.ContactWaId)
            .FirstOrDefault(waId => !string.IsNullOrWhiteSpace(waId));

    private Task SaveAsync(string threadId, string waId, string userId, CustomerAssistantOutcome outcome, string? text, CancellationToken cancellationToken) =>
        conversationRepository.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = tenantContextAccessor.Current.Scope,
            ThreadId = threadId.Trim(),
            ContactWaId = waId,
            InboundMessageText = string.Empty,
            ClassifiedIntent = CustomerIntent.Unknown,
            OutboundReplyText = text,
            Outcome = outcome,
            HumanUserId = userId,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N"),
        }, cancellationToken);

    private Task AuditAsync(string action, string after, string reason, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        return auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, null, null, context.UserId, ActorType.User, action, DateTimeOffset.UtcNow,
                null, after, [], "WhatsApp", null, "recorded", null, reason, Guid.NewGuid().ToString("N")),
            cancellationToken);
    }

    private async Task<ConversationControlDto> ToDtoAsync(IReadOnlyList<CustomerConversationTurn> turns, CancellationToken cancellationToken)
    {
        var mode = ConversationControl.ModeOf(turns, DateTimeOffset.UtcNow);
        var latest = ConversationControl.LastControlTurn(turns);
        string? name = null;
        if (mode == ConversationMode.Person && latest?.HumanUserId is { } userId && userAccountRepository is not null)
        {
            name = (await userAccountRepository.GetAsync(userId, tenantContextAccessor.Current.Scope, cancellationToken))?.Name;
        }

        return new ConversationControlDto(
            mode.ToString(),
            mode == ConversationMode.Person ? "A person is answering" : "Kato is answering",
            mode == ConversationMode.Person ? latest?.OccurredAt : null,
            name,
            ConversationControl.IdleHandBack);
    }
}

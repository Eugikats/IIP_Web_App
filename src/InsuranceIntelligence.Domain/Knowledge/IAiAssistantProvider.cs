namespace InsuranceIntelligence.Domain.Knowledge;

public interface IAiAssistantProvider
{
    Task<AssistantAnswer> AnswerPolicyQuestionAsync(
        AssistantQuestion question,
        RetrievalResult retrievalResult,
        CancellationToken cancellationToken = default);
}

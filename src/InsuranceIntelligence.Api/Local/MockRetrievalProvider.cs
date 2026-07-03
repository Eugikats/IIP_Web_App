using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Local;

public sealed class MockRetrievalProvider(
    IKnowledgeIndexProvider indexProvider,
    ISourceCitationFormatter citationFormatter) : IRetrievalProvider
{
    public async Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default)
    {
        var chunks = await indexProvider.SearchAsync(question.Scope, question.Question, question.ProductLine, cancellationToken);
        var citations = chunks.Select(citationFormatter.Format).ToArray();

        return new RetrievalResult(question.Question, question.ProductLine, chunks, citations);
    }
}

namespace InsuranceIntelligence.Domain.Knowledge;

public interface ISourceCitationFormatter
{
    SourceCitation Format(KnowledgeChunk chunk);
}

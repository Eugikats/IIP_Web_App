using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Local;

public sealed class SourceCitationFormatter : ISourceCitationFormatter
{
    public SourceCitation Format(KnowledgeChunk chunk)
    {
        var excerpt = chunk.Content.Length <= 220
            ? chunk.Content
            : $"{chunk.Content[..220]}...";

        var sectionSuffix = string.IsNullOrWhiteSpace(chunk.Section) ? string.Empty : $" § {chunk.Section}";
        var label = $"{chunk.DocumentName} v{chunk.Version}{sectionSuffix}, chunk {chunk.ChunkIndex + 1}";

        return new SourceCitation(
            chunk.KnowledgeDocumentId,
            chunk.ChunkId,
            chunk.DocumentName,
            chunk.ProductLine,
            chunk.Version,
            excerpt,
            label);
    }
}

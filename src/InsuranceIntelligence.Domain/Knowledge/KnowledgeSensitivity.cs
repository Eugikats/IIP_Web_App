namespace InsuranceIntelligence.Domain.Knowledge;

// WO-17: mirrors the sensitivity vocabulary the portal already anticipated
// (lib/lovable-parity/data/knowledge.ts's KnowledgeSensitivity) before the API captured it at all.
// Unknown is the only value a document can have before a human explicitly classifies it at
// approval time, and is never retrieval-eligible (see KnowledgeDocument.IsActiveOn) — fail closed.
public enum KnowledgeSensitivity
{
    Unknown,
    Approved,
    StaffOnly,
    CustomerConfidential,
    ContainsUnapprovedPii,
    ContainsSecrets,
}

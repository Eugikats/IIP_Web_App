namespace InsuranceIntelligence.Domain.Knowledge;

public enum KnowledgeDocumentType
{
    PolicyWording,
    ClaimsChecklist,
    CustomerFaq,
    ClaimsProcedure,
    RegulatoryGuidance,
    Other,

    // Acquisition-facing types (blueprint section 8.4, WO 4.7), content meant for a prospect,
    // not an existing policyholder. Distinct from CustomerFaq above, which is claims/servicing FAQ
    // content for people who already hold a policy. Only documents classified `approved` are
    // eligible for customer-channel retrieval regardless of DocumentType, this enum only
    // categorizes content, it grants no retrieval eligibility on its own.
    CompanyProfile,
    ProductOverview,
    Faq,
}

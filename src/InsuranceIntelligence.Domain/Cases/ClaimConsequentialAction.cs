namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Consequential insurance actions that AI/system actors must never finalize.
/// </summary>
public enum ClaimConsequentialAction
{
    ApproveClaim,
    RejectClaim,
    Repudiate,
    ApproveSettlement,
    ApprovePayment,
    BindOrPricePolicy,
    AdverseUnderwriting,
    FinalComplaintResponse,
    RegulatoryFiling,
    EvidencePackExport,
    CustomerCommunicationSend
}

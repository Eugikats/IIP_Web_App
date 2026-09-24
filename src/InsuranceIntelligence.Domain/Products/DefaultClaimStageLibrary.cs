namespace InsuranceIntelligence.Domain.Products;

/// <summary>
/// The default claim process a new product starts from: the eight stages Ugandan claims actually
/// go through, pre-filled with the IRA's own indicative service standards in working days
/// (IIP_NEXT_BUILD_PROMPT.md section 5, outcomes A and B). An administrator edits or replaces these
/// per product; nothing here is enforced code-side beyond being the starting point for a new draft.
/// </summary>
public static class DefaultClaimStageLibrary
{
    public const string Intimation = "intimation_and_reference";
    public const string AcknowledgementAndDocumentRequest = "acknowledgement_and_document_request";
    public const string AssessorOrSiteVisit = "assessor_or_site_visit";
    public const string AdjustmentOrInvestigation = "adjustment_or_investigation_report";
    public const string OfferOrRepudiation = "offer_or_repudiation";
    public const string DischargeVoucher = "discharge_voucher";
    public const string Payment = "payment";
    public const string ComplaintEscalation = "complaint_escalation";

    public static IReadOnlyList<ClaimStageDefinition> Default() =>
    [
        new ClaimStageDefinition(
            Intimation,
            "Claim intimation and reference",
            OwnerRole: "Claims Officer",
            IsAutomatic: true,
            SortOrder: 1,
            // IRA: "as per policy conditions", not a fixed day count.
            ServiceStandardWorkingDays: null),

        new ClaimStageDefinition(
            AcknowledgementAndDocumentRequest,
            "Acknowledgement and document request",
            OwnerRole: "Claims Officer",
            IsAutomatic: true,
            SortOrder: 2,
            // IRA: within 3 working days of intimation.
            ServiceStandardWorkingDays: 3),

        new ClaimStageDefinition(
            AssessorOrSiteVisit,
            "Assessor or site visit appointment",
            OwnerRole: "Assessor",
            IsAutomatic: false,
            SortOrder: 3,
            // IRA: within 3 working days (5 for complex claims needing IRA approval). The complex-
            // claim variant is a per-claim exception an officer records, not a second stage.
            ServiceStandardWorkingDays: 3),

        new ClaimStageDefinition(
            AdjustmentOrInvestigation,
            "Adjustment or investigation report",
            OwnerRole: "Adjuster",
            IsAutomatic: false,
            SortOrder: 4,
            // No standalone IRA figure; this stage's output starts the offer/repudiation clock.
            ServiceStandardWorkingDays: null),

        new ClaimStageDefinition(
            OfferOrRepudiation,
            "Settlement offer or repudiation",
            OwnerRole: "Claims Officer",
            IsAutomatic: false,
            SortOrder: 5,
            // IRA: 5 working days from the final adjustment/investigation report or final documents.
            ServiceStandardWorkingDays: 5),

        new ClaimStageDefinition(
            DischargeVoucher,
            "Discharge voucher",
            OwnerRole: "Claims Officer",
            IsAutomatic: false,
            SortOrder: 6,
            ServiceStandardWorkingDays: null),

        new ClaimStageDefinition(
            Payment,
            "Payment",
            OwnerRole: "Finance",
            IsAutomatic: false,
            SortOrder: 7,
            ServiceStandardWorkingDays: null,
            // IRA settlement clock, keyed by claim value: up to UGX 10M within 10 working days of
            // the discharge voucher; above 10M to 50M within 15; above 50M within 20.
            ValueTierStandards:
            [
                new ValueTierServiceStandard(10_000_000m, 10),
                new ValueTierServiceStandard(50_000_000m, 15),
                new ValueTierServiceStandard(null, 20),
            ]),

        new ClaimStageDefinition(
            ComplaintEscalation,
            "Complaint escalation",
            OwnerRole: "Claims Supervisor",
            IsAutomatic: false,
            SortOrder: 8,
            ServiceStandardWorkingDays: null),
    ];
}

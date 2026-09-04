namespace InsuranceIntelligence.Domain.Cases;

// WO-15: the second structured FNOL detail record, added alongside MotorFnolDetails to prove the
// product-line FNOL schema actually generalises rather than remaining Motor-only.
public sealed record MedicalFnolDetails(
    string? PatientName,
    string? RelationshipToPolicyholder,
    string? TreatmentDate,
    string? ProviderName,
    string? AdmissionType,
    string? TreatingDoctor,
    string? DiagnosisSummary,
    string? PreferredCommunicationChannel,
    string? UploadedDocuments)
{
    public MedicalFnolDetails Normalize() =>
        this with
        {
            PatientName = NormalizeOptional(PatientName),
            RelationshipToPolicyholder = NormalizeOptional(RelationshipToPolicyholder),
            TreatmentDate = NormalizeOptional(TreatmentDate),
            ProviderName = NormalizeOptional(ProviderName),
            AdmissionType = NormalizeOptional(AdmissionType),
            TreatingDoctor = NormalizeOptional(TreatingDoctor),
            DiagnosisSummary = NormalizeOptional(DiagnosisSummary),
            PreferredCommunicationChannel = NormalizeOptional(PreferredCommunicationChannel),
            UploadedDocuments = NormalizeOptional(UploadedDocuments)
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

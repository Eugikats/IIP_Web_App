namespace InsuranceIntelligence.Domain.Cases;

public sealed record MotorFnolDetails(
    string? VehicleRegistration,
    string? AccidentTime,
    string? AccidentLocation,
    string? DriverName,
    string? DrivingPermitNumber,
    string? ThirdPartyInvolved,
    string? InjuryIndicator,
    string? PoliceReportStatus,
    string? PreferredCommunicationChannel,
    string? UploadedDocuments)
{
    public MotorFnolDetails Normalize() =>
        this with
        {
            VehicleRegistration = NormalizeOptional(VehicleRegistration),
            AccidentTime = NormalizeOptional(AccidentTime),
            AccidentLocation = NormalizeOptional(AccidentLocation),
            DriverName = NormalizeOptional(DriverName),
            DrivingPermitNumber = NormalizeOptional(DrivingPermitNumber),
            ThirdPartyInvolved = NormalizeOptional(ThirdPartyInvolved),
            InjuryIndicator = NormalizeOptional(InjuryIndicator),
            PoliceReportStatus = NormalizeOptional(PoliceReportStatus),
            PreferredCommunicationChannel = NormalizeOptional(PreferredCommunicationChannel),
            UploadedDocuments = NormalizeOptional(UploadedDocuments)
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

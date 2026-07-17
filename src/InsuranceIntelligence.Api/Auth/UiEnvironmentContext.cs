namespace InsuranceIntelligence.Api.Auth;

public sealed record UiEnvironmentContext(
    string Name,
    string Mode,
    string Source,
    string ReviewMode,
    bool SyntheticData,
    string DataClassification);

namespace InsuranceIntelligence.Api.Common;

public sealed record ActionStateResponse(
    string Action,
    string Label,
    string RequiredPermission,
    string? Reason);

public sealed record BlockedActionResponse(
    string Action,
    string ReasonCode,
    string Message);

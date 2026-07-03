namespace InsuranceIntelligence.Api.Common;

public static class AuditReasonReader
{
    public const string HeaderName = "X-Audit-Reason";
    private const int MaxAuditReasonLength = 500;

    public static string? Read(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var header)
            || string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var reason = header.ToString().Trim();
        return reason.Length <= MaxAuditReasonLength
            ? reason
            : reason[..MaxAuditReasonLength];
    }
}

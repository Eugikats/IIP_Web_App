using System.Globalization;
using System.Text;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Exports;

public sealed record ClaimsRegisterFilter(DateOnly? ReportedFrom, DateOnly? ReportedTo, string? Status);

/// <summary>
/// The claims register as a spreadsheet: one row per claim with the columns an insurer's finance,
/// actuarial and regulatory-return work starts from. It is deliberately plain CSV so it opens in
/// Excel, loads into a core system's import tool, or feeds a script, with no connector needed. The
/// column set is a stable, documented contract (see <see cref="Columns"/>); columns are only ever
/// added at the end.
/// </summary>
public static class ClaimsRegisterExporter
{
    public static readonly IReadOnlyList<string> Columns =
    [
        "claim_reference", "claim_id", "policy_number", "product_line", "claim_type", "status", "channel",
        "date_of_loss", "date_reported", "date_last_updated", "claimed_amount", "risk_level", "priority",
        "assigned_team", "customer_name", "customer_phone", "customer_email", "sla_due",
    ];

    public static string BuildCsv(IEnumerable<ClaimCase> cases, ClaimsRegisterFilter filter)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Columns));

        foreach (var claim in Select(cases, filter))
        {
            var cells = new[]
            {
                ClaimReference.For(claim.CaseId),
                claim.CaseId.ToString(),
                claim.PolicyNumber,
                claim.ProductLine,
                claim.ClaimType,
                ClaimStatusLabels.For(claim.Status),
                claim.Channel,
                claim.IncidentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                claim.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                claim.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                claim.ClaimedAmount?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
                claim.RiskLevel,
                claim.Priority.ToString(),
                claim.AssignedTeam ?? string.Empty,
                claim.CustomerName,
                claim.CustomerPhone ?? string.Empty,
                claim.CustomerEmail ?? string.Empty,
                claim.SlaDueAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
            };
            builder.AppendLine(string.Join(',', cells.Select(Cell)));
        }

        return builder.ToString();
    }

    public static IReadOnlyList<ClaimCase> Select(IEnumerable<ClaimCase> cases, ClaimsRegisterFilter filter) =>
        cases
            .Where(c => filter.ReportedFrom is null || DateOnly.FromDateTime(c.CreatedAt.UtcDateTime) >= filter.ReportedFrom)
            .Where(c => filter.ReportedTo is null || DateOnly.FromDateTime(c.CreatedAt.UtcDateTime) <= filter.ReportedTo)
            .Where(c => string.IsNullOrWhiteSpace(filter.Status)
                || string.Equals(c.Status.ToString(), filter.Status.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(ClaimStatusLabels.For(c.Status), filter.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.CreatedAt)
            .ToArray();

    /// <summary>One CSV cell. Quoted when it needs to be, and a value a spreadsheet would run as a
    /// formula (a customer who types =HYPERLINK(...) as their name) is made inert with a leading quote.</summary>
    public static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

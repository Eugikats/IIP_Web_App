namespace InsuranceIntelligence.Domain.Billing;

/// <summary>
/// The module entitlements + seat limit a tenant is licensed for. For v1 this is
/// a code-defined default package; a persisted per-tenant package can replace it
/// later. No external billing/payment integration (ADR-0003).
/// </summary>
public sealed record EntitlementPackage(
    string Name,
    IReadOnlyList<string> Modules,
    int SeatLimit);

/// <summary>
/// A usage meter. <see cref="Metered"/> is false when no real counter exists for
/// the dimension yet — the UI shows "Not configured" rather than a fabricated
/// number (ADR-0003: usage is computed from real counters, never invented).
/// </summary>
public sealed record UsageMeter(
    string Key,
    string Label,
    int Used,
    int Limit,
    string Unit,
    bool Metered)
{
    public bool OverLimit => Metered && Limit > 0 && Used > Limit;
    public int PercentUsed => Metered && Limit > 0 ? (int)Math.Min(100, Math.Round(100.0 * Used / Limit)) : 0;
}

public static class DefaultEntitlements
{
    public static readonly EntitlementPackage Package = new(
        Name: "IIP Operations — Standard",
        Modules:
        [
            "Command Center", "Channels", "Customer 360", "Claims Intelligence",
            "Document Intelligence", "Policy Intelligence", "Fraud Intelligence",
            "Underwriting Intelligence", "Workflow & Approvals", "Knowledge Hub",
            "Executive Intelligence", "Integrations", "Compliance & Audit",
            "AI & Agent Management", "Support & Health",
        ],
        SeatLimit: 50);
}

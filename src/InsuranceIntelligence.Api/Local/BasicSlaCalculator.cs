using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class BasicSlaCalculator : ISlaCalculator
{
    public SlaStatus Calculate(ClaimCase claimCase, DateTimeOffset now)
    {
        if (claimCase.SlaDueAt is null)
        {
            return SlaStatus.OnTrack;
        }

        if (claimCase.SlaDueAt <= now)
        {
            return SlaStatus.Breached;
        }

        return claimCase.SlaDueAt <= now.AddHours(24)
            ? SlaStatus.AtRisk
            : SlaStatus.OnTrack;
    }
}

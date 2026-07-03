using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Domain.Workflow;

public interface ISlaCalculator
{
    SlaStatus Calculate(ClaimCase claimCase, DateTimeOffset now);
}

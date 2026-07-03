using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Domain.Customers;

public sealed record Customer360(
    Customer Customer,
    IReadOnlyCollection<Policy> Policies,
    IReadOnlyCollection<ClaimCase> ClaimHistory,
    IReadOnlyCollection<Communication> Communications);

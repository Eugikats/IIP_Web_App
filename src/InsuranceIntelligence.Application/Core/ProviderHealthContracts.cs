namespace InsuranceIntelligence.Application.Core;

public sealed record ProviderStatus(
    string ProviderCode,
    string Status,
    string Detail,
    DateTimeOffset CheckedAt);

public interface IProviderHealthCheck
{
    Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default);
}

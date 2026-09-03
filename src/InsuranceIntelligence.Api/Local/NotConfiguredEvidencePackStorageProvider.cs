using InsuranceIntelligence.Domain.Storage;

namespace InsuranceIntelligence.Api.Local;

public sealed class NotConfiguredEvidencePackStorageProvider : IEvidencePackStorageProvider
{
    public Task<EvidencePackStorageResult> StoreAsync(
        EvidencePackStorageRequest request,
        CancellationToken cancellationToken = default) =>
        throw new ProviderNotConfiguredException("EvidencePackStorage", "Evidence pack storage provider is not configured.");
}

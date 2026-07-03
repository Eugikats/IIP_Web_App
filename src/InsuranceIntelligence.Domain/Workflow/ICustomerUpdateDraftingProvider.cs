using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Workflow;

public interface ICustomerUpdateDraftingProvider
{
    Task<CustomerUpdateDraft> DraftAsync(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        CustomerUpdateReasonCategory reasonCategory,
        CommunicationChannel channel,
        string createdByUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}

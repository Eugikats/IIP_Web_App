using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Local;

public sealed class MockCustomerUpdateDraftingProvider(IWorkflowRuleProvider workflowRuleProvider) : ICustomerUpdateDraftingProvider
{
    public Task<CustomerUpdateDraft> DraftAsync(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        CustomerUpdateReasonCategory reasonCategory,
        CommunicationChannel channel,
        string createdByUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var message = BuildMessage(claimCase, checklist, reasonCategory);
        var sensitive = workflowRuleProvider.CustomerUpdateRequiresApproval(message);
        var requiresApproval = sensitive || reasonCategory is CustomerUpdateReasonCategory.ReadyForReview or CustomerUpdateReasonCategory.EscalationReceived;

        return Task.FromResult(new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            ReasonCategory = reasonCategory,
            Channel = channel,
            Status = requiresApproval ? CustomerUpdateStatus.PendingApproval : CustomerUpdateStatus.Draft,
            MessageBody = message,
            RequiresApproval = requiresApproval,
            Sensitive = sensitive,
            DeliveryStatus = null,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        });
    }

    private static string BuildMessage(
        ClaimCase claimCase,
        DocumentCompletenessChecklist checklist,
        CustomerUpdateReasonCategory reasonCategory)
    {
        return reasonCategory switch
        {
            CustomerUpdateReasonCategory.ClaimReceived =>
                $"Hello {claimCase.CustomerName}, we have received your claim for policy {claimCase.PolicyNumber}. Our team will review the information and contact you if anything else is needed.",
            CustomerUpdateReasonCategory.MissingDocuments =>
                BuildMissingDocumentsMessage(claimCase, checklist),
            CustomerUpdateReasonCategory.ReadyForReview =>
                $"Hello {claimCase.CustomerName}, your claim documents have been received and your case is ready for review by our claims team. We will keep you updated as the review progresses.",
            CustomerUpdateReasonCategory.EscalationReceived =>
                $"Hello {claimCase.CustomerName}, your case has been escalated for additional human review. A claims team member will review the file and provide a further update.",
            _ =>
                $"Hello {claimCase.CustomerName}, your claim is currently being reviewed by our team. We will share the next update through your selected communication channel."
        };
    }

    private static string BuildMissingDocumentsMessage(ClaimCase claimCase, DocumentCompletenessChecklist checklist)
    {
        var missing = checklist.MissingRequiredDocuments.Count == 0
            ? "the required supporting documents"
            : string.Join(", ", checklist.MissingRequiredDocuments.Select(document => document.Label));

        return $"Hello {claimCase.CustomerName}, we are reviewing your claim for policy {claimCase.PolicyNumber}. Please provide the following required documents so the claims team can continue: {missing}.";
    }
}

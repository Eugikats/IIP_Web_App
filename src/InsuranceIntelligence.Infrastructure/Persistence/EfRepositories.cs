using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class EfCaseRepository(InsuranceIntelligenceDbContext dbContext, InsuranceIntelligence.Application.Core.ITenantContextAccessor? tenantContext = null)
    : JsonRepositoryBase(dbContext), ICaseRepository
{
    public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Cases, claimCase.CaseId, claimCase.Scope, Serialize(claimCase), cancellationToken: cancellationToken);

    public async Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.Cases.FindAsync([caseId], cancellationToken);
        if (record is null)
        {
            return null;
        }

        // Looked up by id alone, so enforce the caller's organisation here: when a request is running
        // inside one, a claim that belongs to another organisation does not exist as far as it is concerned.
        if (tenantContext is { HasCurrent: true }
            && (!string.Equals(record.TenantId, tenantContext.Current.TenantId, StringComparison.Ordinal)
                || !string.Equals(record.WorkspaceId, tenantContext.Current.WorkspaceId, StringComparison.Ordinal)))
        {
            return null;
        }

        return Deserialize<ClaimCase>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Cases.AsNoTracking(), scope)
            .Select(record => Deserialize<ClaimCase>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfCaseDocumentRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICaseDocumentRepository
{
    public Task SaveAsync(CaseDocument document, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.CaseDocuments,
            document.DocumentId,
            document.Scope,
            Serialize(document),
            record => record.CaseId = document.CaseId,
            cancellationToken);

    public async Task<CaseDocument?> GetAsync(Guid caseId, Guid documentId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == documentId && item.CaseId == caseId, cancellationToken);
        return record is null ? null : Deserialize<CaseDocument>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<CaseDocument>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<CaseDocument>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfCaseReviewRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.CaseReview.ICaseReviewRepository
{
    public Task SaveAsync(InsuranceIntelligence.Domain.CaseReview.CaseReviewSummary review, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.CaseReviews,
            review.CaseReviewId,
            review.Scope,
            Serialize(review),
            record => record.CaseId = review.CaseId,
            cancellationToken);

    public async Task<InsuranceIntelligence.Domain.CaseReview.CaseReviewSummary?> GetLatestForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.CaseReviews.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .OrderByDescending(record => record.UpdatedAt)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.CaseReview.CaseReviewSummary>(record.PayloadJson))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.CaseReview.CaseReviewSummary>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.CaseReviews.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .OrderByDescending(record => record.UpdatedAt)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.CaseReview.CaseReviewSummary>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfKnowledgeDocumentRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IKnowledgeDocumentRepository
{
    public Task SaveAsync(KnowledgeDocument document, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.KnowledgeDocuments, document.KnowledgeDocumentId, document.Scope, Serialize(document), cancellationToken: cancellationToken);

    public async Task<KnowledgeDocument?> GetAsync(Guid knowledgeDocumentId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.KnowledgeDocuments.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == knowledgeDocumentId, cancellationToken);
        return record is null ? null : Deserialize<KnowledgeDocument>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<KnowledgeDocument>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.KnowledgeDocuments.AsNoTracking(), scope)
            .Select(record => Deserialize<KnowledgeDocument>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfAuditRepository(
    InsuranceIntelligenceDbContext dbContext,
    InsuranceIntelligence.Application.Core.ITenantContextAccessor? tenantContext = null,
    InsuranceIntelligence.Application.Webhooks.OutboundEventPublisher? events = null)
    : JsonRepositoryBase(dbContext), IAuditRepository
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await UpsertAsync(
            DbContext.AuditEvents,
            auditEvent.EventId,
            auditEvent.Scope,
            Serialize(auditEvent),
            record => record.CaseId = auditEvent.CaseId,
            cancellationToken);

        // Events for the insurer's own systems come from the audit trail. The audit entry is
        // already safely written; a problem queuing an event must never undo or hide it.
        if (events is not null)
        {
            try
            {
                await events.PublishFromAuditAsync(auditEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The event can be replayed from the audit trail; the audit entry itself stands.
            }
        }
    }

    public async Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var query = DbContext.AuditEvents.AsNoTracking().Where(record => record.CaseId == caseId);
        // Looked up by case id alone, so inside a request it only returns the caller's own organisation's trail.
        if (tenantContext is { HasCurrent: true })
        {
            var current = tenantContext.Current;
            query = query.Where(record => record.TenantId == current.TenantId && record.WorkspaceId == current.WorkspaceId);
        }

        return await query.Select(record => Deserialize<AuditEvent>(record.PayloadJson)).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AuditEvents.AsNoTracking(), scope)
            .Select(record => Deserialize<AuditEvent>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfAgentActionLogRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentActionLogRepository
{
    public Task SaveAsync(AgentActionLog actionLog, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AgentActionLogs,
            actionLog.AgentActionLogId,
            actionLog.Scope,
            Serialize(actionLog),
            record => record.CaseId = actionLog.CaseId,
            cancellationToken);

    public async Task<IReadOnlyCollection<AgentActionLog>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentActionLogs.AsNoTracking(), scope)
            .Select(record => Deserialize<AgentActionLog>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AgentActionLog>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentActionLogs.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<AgentActionLog>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfAgentRunRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IAgentRunRepository
{
    public Task SaveAsync(AgentRun run, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AgentRuns,
            run.AgentRunId,
            run.Scope,
            Serialize(run),
            record =>
            {
                record.AgentKey = run.AgentKey;
                record.CaseId = run.CaseId;
            },
            cancellationToken);

    public async Task<AgentRun?> GetAsync(Guid agentRunId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.AgentRuns.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == agentRunId, cancellationToken);
        return record is null ? null : Deserialize<AgentRun>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<AgentRun>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentRuns.AsNoTracking(), scope)
            .Select(record => Deserialize<AgentRun>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AgentRun>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.AgentRuns.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<AgentRun>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfWorkflowTaskRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IWorkflowTaskRepository
{
    public Task SaveAsync(WorkflowTask task, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.WorkflowTasks, task.TaskId, task.Scope, Serialize(task), record => record.CaseId = task.CaseId, cancellationToken);

    public async Task<WorkflowTask?> GetAsync(Guid taskId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.WorkflowTasks.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        return record is null ? null : Deserialize<WorkflowTask>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<WorkflowTask>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.WorkflowTasks.AsNoTracking(), scope)
            .Select(record => Deserialize<WorkflowTask>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<WorkflowTask>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.WorkflowTasks.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<WorkflowTask>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfApprovalRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IApprovalRepository
{
    public Task SaveAsync(ApprovalRequest approval, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Approvals, approval.ApprovalId, approval.Scope, Serialize(approval), record => record.CaseId = approval.CaseId, cancellationToken);

    public async Task<ApprovalRequest?> GetAsync(Guid approvalId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.Approvals.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == approvalId, cancellationToken);
        return record is null ? null : Deserialize<ApprovalRequest>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<ApprovalRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Approvals.AsNoTracking(), scope)
            .Select(record => Deserialize<ApprovalRequest>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ApprovalRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Approvals.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<ApprovalRequest>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfCustomerUpdateRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICustomerUpdateRepository
{
    public Task SaveAsync(CustomerUpdateDraft update, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.CustomerUpdates, update.UpdateId, update.Scope, Serialize(update), record => record.CaseId = update.CaseId, cancellationToken);

    public async Task<CustomerUpdateDraft?> GetAsync(Guid updateId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.CustomerUpdates.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == updateId, cancellationToken);
        return record is null ? null : Deserialize<CustomerUpdateDraft>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<CustomerUpdateDraft>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.CustomerUpdates.AsNoTracking(), scope)
            .Select(record => Deserialize<CustomerUpdateDraft>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<CustomerUpdateDraft>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.CustomerUpdates.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<CustomerUpdateDraft>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfClaimReviewRequestRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IClaimReviewRequestRepository
{
    public Task SaveAsync(ClaimReviewRequest request, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.ClaimReviewRequests, request.RequestId, request.Scope, Serialize(request), record => record.CaseId = request.CaseId, cancellationToken);

    public async Task<ClaimReviewRequest?> GetAsync(Guid requestId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.ClaimReviewRequests.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        return record is null ? null : Deserialize<ClaimReviewRequest>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<ClaimReviewRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.ClaimReviewRequests.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<ClaimReviewRequest>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfContactDetailChangeRequestRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.Customers.IContactDetailChangeRequestRepository
{
    public Task SaveAsync(InsuranceIntelligence.Domain.Customers.ContactDetailChangeRequest request, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.ContactDetailChangeRequests, request.RequestId, request.Scope, Serialize(request), record => record.CaseId = request.CaseId, cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Customers.ContactDetailChangeRequest?> GetAsync(Guid requestId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.ContactDetailChangeRequests.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        return record is null ? null : Deserialize<InsuranceIntelligence.Domain.Customers.ContactDetailChangeRequest>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.ContactDetailChangeRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.ContactDetailChangeRequests.AsNoTracking(), scope)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.Customers.ContactDetailChangeRequest>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfCommunicationRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICommunicationRepository
{
    public Task SaveAsync(Communication communication, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.Communications,
            communication.CommunicationId,
            communication.Scope,
            Serialize(communication),
            record => record.CaseId = communication.CaseId,
            cancellationToken);

    public async Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Communications.AsNoTracking(), scope)
            .Select(record => Deserialize<Communication>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Communications.AsNoTracking(), scope)
            .Where(record => record.CaseId == caseId)
            .Select(record => Deserialize<Communication>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    // ProviderMessageId lives inside the JSON payload, not a dedicated column, so this filters
    // in memory after a scoped load rather than in SQL, acceptable at today's per-tenant
    // communication volume; revisit with a real column/index if that changes.
    public async Task<Communication?> FindByProviderMessageIdAcrossTenantsAsync(string providerMessageId, CancellationToken cancellationToken = default)
    {
        // A delivery receipt names only the gateway's message id. Recent messages first, and only
        // an unambiguous single match is accepted.
        var records = await DbContext.Communications.AsNoTracking()
            .OrderByDescending(record => record.UpdatedAt)
            .Take(5000)
            .ToArrayAsync(cancellationToken);
        var matches = records
            .Select(record => Deserialize<Communication>(record.PayloadJson))
            .Where(communication => string.Equals(communication.ProviderMessageId, providerMessageId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public async Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.Communications.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<Communication>(record.PayloadJson))
            .FirstOrDefault(communication => string.Equals(communication.ProviderMessageId, providerMessageId, StringComparison.Ordinal));
    }
}

public sealed class EfTenantConfigurationRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ITenantConfigurationRepository
{
    public async Task<TenantConfiguration> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.TenantConfigurations.AsNoTracking(), scope)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null ? TenantConfiguration.Unconfigured(scope) : Deserialize<TenantConfiguration>(record.PayloadJson);
    }

    public Task SaveAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.TenantConfigurations, DeterministicId(configuration.Scope.TenantId, configuration.Scope.WorkspaceId), configuration.Scope, Serialize(configuration), cancellationToken: cancellationToken);

    private static Guid DeterministicId(string tenantId, string workspaceId)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{tenantId}:{workspaceId}"));
        return new Guid(bytes);
    }
}

public sealed class EfUserAccountRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IUserAccountRepository
{
    public async Task<IReadOnlyCollection<InsuranceUserAccount>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.UserAccounts.AsNoTracking(), scope)
            .OrderBy(record => record.UserId)
            .Select(record => Deserialize<InsuranceUserAccount>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<InsuranceUserAccount?> GetAsync(string userId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.UserAccounts.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return record is null ? null : Deserialize<InsuranceUserAccount>(record.PayloadJson);
    }

    public Task SaveAsync(InsuranceUserAccount user, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.UserAccounts,
            DeterministicId(user.Scope.TenantId, user.Scope.WorkspaceId, user.UserId),
            user.Scope,
            Serialize(user),
            record => record.UserId = user.UserId,
            cancellationToken);

    private static Guid DeterministicId(string tenantId, string workspaceId, string userId)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"{tenantId}:{workspaceId}:{userId}"));
        return new Guid(bytes);
    }
}

public sealed class EfMailboxConnectionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IMailboxConnectionRepository
{
    public Task SaveAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.MailboxConnections, mailbox.MailboxConnectionId, mailbox.Scope, Serialize(mailbox), cancellationToken: cancellationToken);

    public async Task<IReadOnlyCollection<MailboxConnection>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.MailboxConnections.AsNoTracking(), scope)
            .Select(record => Deserialize<MailboxConnection>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<MailboxConnection?> GetAsync(Guid mailboxConnectionId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.MailboxConnections.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == mailboxConnectionId, cancellationToken);
        return record is null ? null : Deserialize<MailboxConnection>(record.PayloadJson);
    }
}

public sealed class EfInboundEmailRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IInboundEmailRepository
{
    public Task SaveAsync(InboundEmailMessage message, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.InboundEmails, message.MessageId, message.Scope, Serialize(message), cancellationToken: cancellationToken);

    public async Task<InboundEmailMessage?> GetAsync(Guid messageId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.InboundEmails.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        return record is null ? null : Deserialize<InboundEmailMessage>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InboundEmailMessage>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.InboundEmails.AsNoTracking(), scope)
            .Select(record => Deserialize<InboundEmailMessage>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfCustomerRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.Customers.ICustomerRepository
{
    public Task UpsertAsync(InsuranceIntelligence.Domain.Customers.Customer customer, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Customers, Guid.Parse(customer.CustomerId), TenantScope.Create(customer.TenantId, customer.WorkspaceId), Serialize(customer), cancellationToken: cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Customers.Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(customerId);
        var record = await Scoped(DbContext.Customers.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<InsuranceIntelligence.Domain.Customers.Customer>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Customers.AsNoTracking(), scope)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.Customers.Customer>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfPolicyRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.Policies.IPolicyRepository
{
    public Task UpsertAsync(InsuranceIntelligence.Domain.Policies.Policy policy, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Policies, Guid.Parse(policy.PolicyId), TenantScope.Create(policy.TenantId, policy.WorkspaceId), Serialize(policy), cancellationToken: cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Policies.Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(policyId);
        var record = await Scoped(DbContext.Policies.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<InsuranceIntelligence.Domain.Policies.Policy>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Policies.AsNoTracking(), scope)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.Policies.Policy>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Policies.Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default) =>
        (await ListAsync(scope, cancellationToken))
            .FirstOrDefault(p => string.Equals(p.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default) =>
        (await ListAsync(scope, cancellationToken)).Where(p => p.CustomerId == customerId).ToList();
}

public sealed class EfCoverageRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.Policies.ICoverageRepository
{
    public Task UpsertAsync(InsuranceIntelligence.Domain.Policies.Coverage coverage, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Coverages, Guid.Parse(coverage.CoverageId), TenantScope.Create(coverage.TenantId, coverage.WorkspaceId), Serialize(coverage), cancellationToken: cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Policies.Coverage?> GetAsync(string coverageId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(coverageId);
        var record = await Scoped(DbContext.Coverages.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<InsuranceIntelligence.Domain.Policies.Coverage>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Coverage>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default) =>
        (await Scoped(DbContext.Coverages.AsNoTracking(), scope)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.Policies.Coverage>(record.PayloadJson))
            .ToArrayAsync(cancellationToken)).Where(c => c.PolicyId == policyId).ToList();
}

public sealed class EfBranchRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.Distribution.IBranchRepository
{
    public Task UpsertAsync(InsuranceIntelligence.Domain.Distribution.Branch branch, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Branches, Guid.Parse(branch.BranchId), TenantScope.Create(branch.TenantId, branch.WorkspaceId), Serialize(branch), cancellationToken: cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Distribution.Branch?> GetAsync(string branchId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(branchId);
        var record = await Scoped(DbContext.Branches.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<InsuranceIntelligence.Domain.Distribution.Branch>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Branch>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Branches.AsNoTracking(), scope)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.Distribution.Branch>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

public sealed class EfProducerRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), InsuranceIntelligence.Domain.Distribution.IProducerRepository
{
    public Task UpsertAsync(InsuranceIntelligence.Domain.Distribution.Producer producer, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.Producers, Guid.Parse(producer.ProducerId), TenantScope.Create(producer.TenantId, producer.WorkspaceId), Serialize(producer), cancellationToken: cancellationToken);

    public async Task<InsuranceIntelligence.Domain.Distribution.Producer?> GetAsync(string producerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(producerId);
        var record = await Scoped(DbContext.Producers.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<InsuranceIntelligence.Domain.Distribution.Producer>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Producer>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.Producers.AsNoTracking(), scope)
            .Select(record => Deserialize<InsuranceIntelligence.Domain.Distribution.Producer>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Producer>> ListByBranchAsync(string branchId, TenantScope scope, CancellationToken cancellationToken = default) =>
        (await ListAsync(scope, cancellationToken)).Where(p => p.BranchId == branchId).ToList();
}

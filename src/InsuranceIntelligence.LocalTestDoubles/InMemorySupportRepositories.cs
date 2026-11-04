using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Retention;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryTenantConfigurationRepository : ITenantConfigurationRepository
{
    private readonly ConcurrentDictionary<string, TenantConfiguration> configurations = new(StringComparer.OrdinalIgnoreCase);

    public Task<TenantConfiguration> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var key = Key(scope);
        return Task.FromResult(configurations.TryGetValue(key, out var configuration)
            ? configuration
            : TenantConfiguration.Demo() with { Scope = scope });
    }

    public Task SaveAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configurations[Key(configuration.Scope)] = configuration;
        return Task.CompletedTask;
    }

    private static string Key(TenantScope scope) => $"{scope.TenantId}:{scope.WorkspaceId}";
}

public sealed class InMemoryUserAccountRepository : IUserAccountRepository
{
    private readonly ConcurrentDictionary<string, InsuranceUserAccount> users = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyCollection<InsuranceUserAccount>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = users.Values.Where(user => user.Scope.Matches(scope)).OrderBy(user => user.UserId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceUserAccount>>(matches);
    }

    public Task<InsuranceUserAccount?> GetAsync(string userId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        users.TryGetValue(Key(scope, userId), out var user);
        return Task.FromResult(user);
    }

    public Task SaveAsync(InsuranceUserAccount user, CancellationToken cancellationToken = default)
    {
        users[Key(user.Scope, user.UserId)] = user;
        return Task.CompletedTask;
    }

    private static string Key(TenantScope scope, string userId) => $"{scope.TenantId}:{scope.WorkspaceId}:{userId}";
}

public sealed class InMemoryCommunicationRepository : ICommunicationRepository
{
    private readonly ConcurrentDictionary<Guid, Communication> communications = new();

    public Task SaveAsync(Communication communication, CancellationToken cancellationToken = default)
    {
        communications[communication.CommunicationId] = communication;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = communications.Values.Where(item => item.Scope.Matches(scope)).OrderByDescending(item => item.CreatedAt).ToArray();
        return Task.FromResult<IReadOnlyCollection<Communication>>(matches);
    }

    public Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = communications.Values.Where(item => item.CaseId == caseId && item.Scope.Matches(scope)).OrderByDescending(item => item.CreatedAt).ToArray();
        return Task.FromResult<IReadOnlyCollection<Communication>>(matches);
    }

    public Task<Communication?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default)
    {
        var match = communications.Values.FirstOrDefault(item =>
            item.Scope.Matches(scope) && string.Equals(item.ProviderMessageId, providerMessageId, StringComparison.Ordinal));
        return Task.FromResult(match);
    }

    public Task<Communication?> FindByProviderMessageIdAcrossTenantsAsync(string providerMessageId, CancellationToken cancellationToken = default)
    {
        var matches = communications.Values
            .Where(item => string.Equals(item.ProviderMessageId, providerMessageId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return Task.FromResult(matches.Length == 1 ? matches[0] : null);
    }
}

public sealed class InMemoryClaimReviewRequestRepository : IClaimReviewRequestRepository
{
    private readonly ConcurrentDictionary<Guid, ClaimReviewRequest> requests = new();

    public Task SaveAsync(ClaimReviewRequest request, CancellationToken cancellationToken = default)
    {
        requests[request.RequestId] = request;
        return Task.CompletedTask;
    }

    public Task<ClaimReviewRequest?> GetAsync(Guid requestId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        requests.TryGetValue(requestId, out var request);
        return Task.FromResult(request is not null && request.Scope.Matches(scope) ? request : null);
    }

    public Task<IReadOnlyCollection<ClaimReviewRequest>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = requests.Values
            .Where(item => item.CaseId == caseId && item.Scope.Matches(scope))
            .OrderByDescending(item => item.RequestedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<ClaimReviewRequest>>(matches);
    }
}

public sealed class InMemoryConsentRepository : IConsentRepository
{
    private readonly ConcurrentBag<ConsentRecord> records = [];

    public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default)
    {
        records.Add(record);
        return Task.CompletedTask;
    }

    public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default)
    {
        var current = records
            .Where(record => record.Scope.Matches(scope) && record.CustomerIdentifier == customerIdentifier && record.Purpose == purpose)
            .OrderByDescending(record => record.RecordedAt)
            .FirstOrDefault();
        return Task.FromResult(current);
    }

    public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default)
    {
        var matches = records
            .Where(record => record.Scope.Matches(scope) && record.CustomerIdentifier == customerIdentifier)
            .OrderByDescending(record => record.RecordedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<ConsentRecord>>(matches);
    }
}

public sealed class InMemoryRetentionReminderRepository : IRetentionReminderRepository
{
    private readonly ConcurrentDictionary<Guid, RetentionReminderDraft> reminders = new();

    public Task SaveAsync(RetentionReminderDraft reminder, CancellationToken cancellationToken = default)
    {
        reminders[reminder.ReminderId] = reminder;
        return Task.CompletedTask;
    }

    public Task<RetentionReminderDraft?> GetAsync(Guid reminderId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var found = reminders.TryGetValue(reminderId, out var reminder) && reminder.Scope.Matches(scope) ? reminder : null;
        return Task.FromResult(found);
    }

    public Task<IReadOnlyCollection<RetentionReminderDraft>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = reminders.Values
            .Where(reminder => reminder.Scope.Matches(scope) && reminder.PolicyId == policyId)
            .OrderByDescending(reminder => reminder.CreatedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<RetentionReminderDraft>>(matches);
    }

    public Task<IReadOnlyCollection<RetentionReminderDraft>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = reminders.Values.Where(reminder => reminder.Scope.Matches(scope)).ToArray();
        return Task.FromResult<IReadOnlyCollection<RetentionReminderDraft>>(matches);
    }
}

public sealed class InMemorySatisfactionResponseRepository : ISatisfactionResponseRepository
{
    private readonly ConcurrentBag<SatisfactionResponse> responses = [];

    public Task SaveAsync(SatisfactionResponse response, CancellationToken cancellationToken = default)
    {
        responses.Add(response);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<SatisfactionResponse>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = responses
            .Where(response => response.Scope.Matches(scope) && response.CaseId == caseId)
            .OrderByDescending(response => response.RecordedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<SatisfactionResponse>>(matches);
    }
}

public sealed class InMemoryContactDetailChangeRequestRepository : IContactDetailChangeRequestRepository
{
    private readonly ConcurrentDictionary<Guid, ContactDetailChangeRequest> requests = new();

    public Task SaveAsync(ContactDetailChangeRequest request, CancellationToken cancellationToken = default)
    {
        requests[request.RequestId] = request;
        return Task.CompletedTask;
    }

    public Task<ContactDetailChangeRequest?> GetAsync(Guid requestId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        requests.TryGetValue(requestId, out var request);
        return Task.FromResult(request is not null && request.Scope.Matches(scope) ? request : null);
    }

    public Task<IReadOnlyCollection<ContactDetailChangeRequest>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = requests.Values
            .Where(item => item.Scope.Matches(scope))
            .OrderByDescending(item => item.RequestedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<ContactDetailChangeRequest>>(matches);
    }
}

public sealed class InMemoryMailboxConnectionRepository : IMailboxConnectionRepository
{
    private readonly ConcurrentDictionary<Guid, MailboxConnection> mailboxes = new();

    public Task SaveAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default)
    {
        mailboxes[mailbox.MailboxConnectionId] = mailbox;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<MailboxConnection>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = mailboxes.Values.Where(item => item.Scope.Matches(scope)).OrderByDescending(item => item.CreatedAt).ToArray();
        return Task.FromResult<IReadOnlyCollection<MailboxConnection>>(matches);
    }

    public Task<MailboxConnection?> GetAsync(Guid mailboxConnectionId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        mailboxes.TryGetValue(mailboxConnectionId, out var mailbox);
        return Task.FromResult(mailbox is not null && mailbox.Scope.Matches(scope) ? mailbox : null);
    }

    public Task<IReadOnlyList<TenantScope>> ListScopesWithIngestionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantScope>>(mailboxes.Values.Where(item => item.IngestionEnabled).Select(item => item.Scope).Distinct().ToArray());
}

public sealed class InMemoryInboundEmailRepository : IInboundEmailRepository
{
    private readonly ConcurrentDictionary<Guid, InboundEmailMessage> messages = new();

    public Task SaveAsync(InboundEmailMessage message, CancellationToken cancellationToken = default)
    {
        messages[message.MessageId] = message;
        return Task.CompletedTask;
    }

    public Task<InboundEmailMessage?> GetAsync(Guid messageId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        messages.TryGetValue(messageId, out var message);
        return Task.FromResult(message is not null && message.Scope.Matches(scope) ? message : null);
    }

    public Task<IReadOnlyCollection<InboundEmailMessage>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = messages.Values.Where(item => item.Scope.Matches(scope)).OrderByDescending(item => item.ReceivedAt).ToArray();
        return Task.FromResult<IReadOnlyCollection<InboundEmailMessage>>(matches);
    }
}

public sealed class InMemoryCustomerRepository : InsuranceIntelligence.Domain.Customers.ICustomerRepository
{
    private readonly ConcurrentDictionary<string, InsuranceIntelligence.Domain.Customers.Customer> items = new();

    public Task<InsuranceIntelligence.Domain.Customers.Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        items.TryGetValue(customerId, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>>(matches);
    }

    public Task UpsertAsync(InsuranceIntelligence.Domain.Customers.Customer customer, CancellationToken cancellationToken = default)
    {
        items[customer.CustomerId] = customer;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPolicyRepository : InsuranceIntelligence.Domain.Policies.IPolicyRepository
{
    private readonly ConcurrentDictionary<string, InsuranceIntelligence.Domain.Policies.Policy> items = new();

    public Task<InsuranceIntelligence.Domain.Policies.Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        items.TryGetValue(policyId, out var item);
        return Task.FromResult(item);
    }

    public Task<InsuranceIntelligence.Domain.Policies.Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var match = items.Values.FirstOrDefault(i =>
            i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId
            && string.Equals(i.PolicyNumber, policyNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId && i.CustomerId == customerId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Policy>>(matches);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Policy>>(matches);
    }

    public Task UpsertAsync(InsuranceIntelligence.Domain.Policies.Policy policy, CancellationToken cancellationToken = default)
    {
        items[policy.PolicyId] = policy;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCoverageRepository : InsuranceIntelligence.Domain.Policies.ICoverageRepository
{
    private readonly ConcurrentDictionary<string, InsuranceIntelligence.Domain.Policies.Coverage> items = new();

    public Task<InsuranceIntelligence.Domain.Policies.Coverage?> GetAsync(string coverageId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        items.TryGetValue(coverageId, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Coverage>> ListForPolicyAsync(string policyId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId && i.PolicyId == policyId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Policies.Coverage>>(matches);
    }

    public Task UpsertAsync(InsuranceIntelligence.Domain.Policies.Coverage coverage, CancellationToken cancellationToken = default)
    {
        items[coverage.CoverageId] = coverage;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryBranchRepository : InsuranceIntelligence.Domain.Distribution.IBranchRepository
{
    private readonly ConcurrentDictionary<string, InsuranceIntelligence.Domain.Distribution.Branch> items = new();

    public Task<InsuranceIntelligence.Domain.Distribution.Branch?> GetAsync(string branchId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        items.TryGetValue(branchId, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Branch>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Branch>>(matches);
    }

    public Task UpsertAsync(InsuranceIntelligence.Domain.Distribution.Branch branch, CancellationToken cancellationToken = default)
    {
        items[branch.BranchId] = branch;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryProducerRepository : InsuranceIntelligence.Domain.Distribution.IProducerRepository
{
    private readonly ConcurrentDictionary<string, InsuranceIntelligence.Domain.Distribution.Producer> items = new();

    public Task<InsuranceIntelligence.Domain.Distribution.Producer?> GetAsync(string producerId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        items.TryGetValue(producerId, out var item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Producer>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Producer>>(matches);
    }

    public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Producer>> ListByBranchAsync(string branchId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = items.Values.Where(i => i.TenantId == scope.TenantId && i.WorkspaceId == scope.WorkspaceId && i.BranchId == branchId).ToArray();
        return Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Distribution.Producer>>(matches);
    }

    public Task UpsertAsync(InsuranceIntelligence.Domain.Distribution.Producer producer, CancellationToken cancellationToken = default)
    {
        items[producer.ProducerId] = producer;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPortalIdentityRepository : InsuranceIntelligence.Domain.ClientPortal.IPortalIdentityRepository
{
    private readonly ConcurrentDictionary<Guid, InsuranceIntelligence.Domain.ClientPortal.PortalIdentity> identities = new();

    public Task<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        identities.TryGetValue(InsuranceIntelligence.Domain.ClientPortal.PortalIdentity.StorageKey(scope), out var identity);
        return Task.FromResult(identity);
    }

    public Task SaveAsync(InsuranceIntelligence.Domain.ClientPortal.PortalIdentity identity, CancellationToken cancellationToken = default)
    {
        identities[InsuranceIntelligence.Domain.ClientPortal.PortalIdentity.StorageKey(identity.Scope)] = identity;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InsuranceIntelligence.Domain.ClientPortal.PortalIdentity>>(identities.Values.ToArray());
}

using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryDocumentFieldValidationRuleRepository : IDocumentFieldValidationRuleRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentFieldValidationRule> rules = new();

    public Task SaveAsync(DocumentFieldValidationRule rule, CancellationToken cancellationToken = default)
    {
        rules[DocumentFieldValidationRule.StorageKey(rule.RuleId, rule.Scope)] = rule;
        return Task.CompletedTask;
    }

    public Task<DocumentFieldValidationRule?> GetAsync(string ruleId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        rules.TryGetValue(DocumentFieldValidationRule.StorageKey(ruleId, scope), out var rule);
        return Task.FromResult(rule is not null && rule.Scope.Matches(scope) ? rule : null);
    }

    public Task<IReadOnlyCollection<DocumentFieldValidationRule>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<DocumentFieldValidationRule>>(
            rules.Values.Where(rule => rule.Scope.Matches(scope)).ToArray());
}

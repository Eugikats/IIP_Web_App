using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Documents;

public sealed class EfDocumentFieldValidationRuleRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IDocumentFieldValidationRuleRepository
{
    public Task SaveAsync(DocumentFieldValidationRule rule, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.DocumentFieldValidationRules,
            DocumentFieldValidationRule.StorageKey(rule.RuleId, rule.Scope),
            rule.Scope,
            Serialize(rule),
            record => record.DocumentType = rule.DocumentType.ToString(),
            cancellationToken);

    public async Task<DocumentFieldValidationRule?> GetAsync(string ruleId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = DocumentFieldValidationRule.StorageKey(ruleId, scope);
        var record = await Scoped(DbContext.DocumentFieldValidationRules.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<DocumentFieldValidationRule>(record.PayloadJson);
    }

    public async Task<IReadOnlyCollection<DocumentFieldValidationRule>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.DocumentFieldValidationRules.AsNoTracking(), scope)
            .Select(record => Deserialize<DocumentFieldValidationRule>(record.PayloadJson))
            .ToArrayAsync(cancellationToken);
}

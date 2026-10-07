using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Academy;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.Academy;

public interface IAcademyPricingService
{
    /// <summary>Every course with its price. Anyone signed in to the workspace can read it.</summary>
    Task<AcademyPricing> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the prices a person set. Needs permission to manage workspace settings.</summary>
    Task<AcademyPricing> SaveAsync(IReadOnlyList<CoursePrice> prices, CancellationToken cancellationToken = default);
}

public sealed class AcademyPriceValidationException(string message) : InvalidOperationException(message);

public sealed class AcademyPricingService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAcademyPricingRepository repository,
    IAuditWriter auditWriter) : IAcademyPricingService
{
    public async Task<AcademyPricing> GetAsync(CancellationToken cancellationToken = default)
    {
        var scope = tenantContextAccessor.Current.Scope;
        return (await repository.GetAsync(scope, cancellationToken) ?? AcademyPricing.Seed(scope)).WithDefaults();
    }

    public async Task<AcademyPricing> SaveAsync(IReadOnlyList<CoursePrice> prices, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantSettings, cancellationToken);
        var context = tenantContextAccessor.Current;

        foreach (var price in prices)
        {
            if (AcademyPricing.Problem(price) is { } problem)
            {
                throw new AcademyPriceValidationException(problem);
            }
        }

        var current = await GetAsync(cancellationToken);
        var merged = current.Prices
            .Select(existing => prices.FirstOrDefault(price => string.Equals(price.CourseId, existing.CourseId, StringComparison.OrdinalIgnoreCase)) is { } changed
                ? new CoursePrice(existing.CourseId, decimal.Round(changed.Amount, 2))
                : existing)
            .ToArray();

        var now = DateTimeOffset.UtcNow;
        var saved = current with { Prices = merged, UpdatedAt = now, UpdatedBy = context.UserId };
        await repository.SaveAsync(saved, cancellationToken);

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, "academy.pricing_changed",
                now, BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: "Academy", Confidence: null,
                ValidationResult: "recorded", ApprovalId: null,
                AuditReason: $"IIP Academy course prices were changed: {string.Join(", ", merged.Select(price => $"{price.CourseId} {price.Amount:0.##} {price.Currency}"))}.",
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);
        return saved;
    }
}

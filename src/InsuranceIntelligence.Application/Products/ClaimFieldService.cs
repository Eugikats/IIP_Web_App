using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Products;

/// <summary>A field as asked: its definition plus the claim's current answer, if any.</summary>
public sealed record ClaimFieldValueDto(
    string Key,
    string Label,
    string Type,
    bool Required,
    IReadOnlyList<string> Options,
    string? HelpText,
    int SortOrder,
    bool CustomerVisible,
    string? Value);

public sealed record ClaimFieldFormDto(string? ClaimTypeKey, bool Configured, IReadOnlyList<ClaimFieldValueDto> Fields);

public interface IClaimFieldService
{
    /// <summary>The questions to ask before a claim exists (the new-claim forms).</summary>
    Task<ClaimFieldFormDto> GetFormAsync(TenantScope scope, string? productLine, string? claimType, bool customerAudience, CancellationToken cancellationToken = default);

    /// <summary>The questions for an existing claim, from its pinned product version, with the
    /// claim's answers.</summary>
    Task<ClaimFieldFormDto> GetForClaimAsync(ClaimCase claimCase, bool customerAudience, CancellationToken cancellationToken = default);

    /// <summary>Saves some answers. Validated against the claim's own definitions (a customer can
    /// only answer customer-visible ones); required fields are not enforced when saving part of the
    /// details. Records which fields changed, never the values.</summary>
    Task<ClaimFieldSaveResult> SaveValuesAsync(
        ClaimCase claimCase,
        IReadOnlyDictionary<string, string> values,
        bool customerAudience,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed record ClaimFieldSaveResult(ClaimCase? Claim, IReadOnlyDictionary<string, string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class ClaimFieldService(
    IProductRepository productRepository,
    ICaseRepository caseRepository,
    IAuditRepository auditRepository) : IClaimFieldService
{
    public async Task<ClaimFieldFormDto> GetFormAsync(
        TenantScope scope, string? productLine, string? claimType, bool customerAudience, CancellationToken cancellationToken = default)
    {
        Product? product = null;
        if (!string.IsNullOrWhiteSpace(productLine))
        {
            product = (await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken))
                .FirstOrDefault(candidate => candidate.Status == ProductStatus.Published
                    && string.Equals(candidate.ProductClass, productLine.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var claimTypeKey = (product?.FindClaimType(claimType) ?? product?.SuggestClaimType(claimType))?.Key;
        var definitions = ClaimFieldResolver.ForProduct(product, productLine ?? string.Empty, claimTypeKey);
        return ToForm(claimTypeKey, product is { ClaimFields.Count: > 0 }, definitions, new Dictionary<string, string>(), customerAudience);
    }

    public async Task<ClaimFieldFormDto> GetForClaimAsync(ClaimCase claimCase, bool customerAudience, CancellationToken cancellationToken = default)
    {
        var (product, claimTypeKey) = await ResolveAsync(claimCase, cancellationToken);
        var definitions = ClaimFieldResolver.ForProduct(product, claimCase.ProductLine, claimTypeKey);
        return ToForm(claimTypeKey, product is { ClaimFields.Count: > 0 }, definitions, ClaimFieldValues.For(claimCase), customerAudience);
    }

    public async Task<ClaimFieldSaveResult> SaveValuesAsync(
        ClaimCase claimCase,
        IReadOnlyDictionary<string, string> values,
        bool customerAudience,
        string actorId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var (product, claimTypeKey) = await ResolveAsync(claimCase, cancellationToken);
        var definitions = ClaimFieldResolver.ForProduct(product, claimCase.ProductLine, claimTypeKey);
        var result = ClaimFieldValidator.Validate(definitions, values, requireRequired: false, customerAudience);
        if (!result.IsValid)
        {
            return new ClaimFieldSaveResult(null, result.Errors);
        }

        var merged = new Dictionary<string, string>(claimCase.FieldValues ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in result.Values)
        {
            merged[key] = value;
        }

        var updated = claimCase with
        {
            FieldValues = merged,
            MotorFnolDetails = ClaimFieldValues.ToMotorFnol(result.Values, claimCase.MotorFnolDetails),
            MedicalFnolDetails = ClaimFieldValues.ToMedicalFnol(result.Values, claimCase.MedicalFnolDetails),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await caseRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseFieldsUpdated(
                updated.ToAuditSnapshot(),
                actorId,
                ActorType.User,
                result.Values.Keys.ToArray(),
                correlationId),
            cancellationToken);
        return new ClaimFieldSaveResult(updated, new Dictionary<string, string>());
    }

    private async Task<(Product? Product, string? ClaimTypeKey)> ResolveAsync(ClaimCase claimCase, CancellationToken cancellationToken)
    {
        if (claimCase.ProductId is not { } productId || claimCase.ProductVersion is not { } version)
        {
            return (null, claimCase.ClaimTypeKey);
        }

        var product = await productRepository.GetVersionAsync(claimCase.Scope, productId, version, cancellationToken);
        return (product, claimCase.ClaimTypeKey ?? product?.FindClaimType(claimCase.ClaimType)?.Key);
    }

    /// <summary>The questions a customer would be asked for a product (draft or published) and
    /// claim type, with no answers. Used by "Preview as customer"; reads nothing from storage.</summary>
    public static ClaimFieldFormDto FormFor(Product product, string? claimTypeKey) =>
        ToForm(
            claimTypeKey,
            product.ClaimFields.Count > 0,
            ClaimFieldResolver.ForProduct(product, product.ProductClass, claimTypeKey),
            new Dictionary<string, string>(),
            customerAudience: true);

    private static ClaimFieldFormDto ToForm(
        string? claimTypeKey,
        bool configured,
        IReadOnlyList<ClaimFieldDefinition> definitions,
        IReadOnlyDictionary<string, string> values,
        bool customerAudience) =>
        new(
            claimTypeKey,
            configured,
            definitions
                .Where(definition => !customerAudience || definition.CustomerVisible)
                .OrderBy(definition => definition.SortOrder)
                .Select(definition => new ClaimFieldValueDto(
                    definition.Key,
                    definition.Label,
                    definition.Type.ToString(),
                    definition.Required,
                    definition.Options ?? [],
                    definition.HelpText,
                    definition.SortOrder,
                    definition.CustomerVisible,
                    values.TryGetValue(definition.Key, out var value) ? value : null))
                .ToArray());
}

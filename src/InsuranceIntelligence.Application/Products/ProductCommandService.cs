using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Products;

/// <summary>
/// Product configuration is the spine everything else in claim handling reads from
/// (IIP_NEXT_BUILD_PROMPT.md outcome A). Every save appends a new version, never overwrites one
/// (non-negotiable 12), so a claim can keep the exact process/document-matrix version in force
/// when it was filed even after the product is edited again (outcome C). Publishing requires an
/// IRA approval reference and effective date, and requires a second person, distinct from whoever
/// submitted it, the same maker/checker split as ApprovalRequest.
/// </summary>
public sealed class ProductCommandService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IProductRepository repository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IAuditWriter auditWriter,
    IKnowledgeDocumentRepository? knowledgeDocuments = null) : IProductCommandService
{
    public async Task<ProductDto> CreateDraftAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProductValidationException(nameof(request.Name), "Product name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProductClass))
        {
            throw new ProductValidationException(nameof(request.ProductClass), "Product class is required.");
        }

        var context = tenantContextAccessor.Current;
        var wordingFacts = request.WordingFacts is null
            ? Array.Empty<ProductWordingFact>()
            : await ValidateWordingFacts(context.Scope, request.WordingFacts, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request.ChangeReason) ? "Product created." : request.ChangeReason.Trim();

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = context.Scope,
            Version = 1,
            Name = request.Name.Trim(),
            ProductClass = request.ProductClass.Trim(),
            IraApprovalReference = null,
            IraApprovalEffectiveDate = null,
            ApprovedWordingKnowledgeDocumentId = null,
            Status = ProductStatus.Draft,
            Stages = request.UseDefaultStageLibrary ? DefaultClaimStageLibrary.Default() : [],
            DocumentMatrix = [],
            WordingFacts = wordingFacts,
            UpdatedByUserId = context.UserId,
            UpdatedAt = now,
            ChangeReason = reason,
        };

        await repository.SaveAsync(product, cancellationToken);
        await Audit(product, "product.created", reason, cancellationToken);

        return ToDto(product);
    }

    public async Task<ProductDto> UpdateDraftAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var current = await RequireCurrent(productId, cancellationToken);

        if (current.Status == ProductStatus.Retired)
        {
            throw new ProductValidationException("status", "A retired product cannot be edited.");
        }

        if (string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            throw new ProductValidationException(nameof(request.ChangeReason), "A reason is required to change a product's configuration.");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? current.Name : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProductValidationException(nameof(request.Name), "Product name is required.");
        }

        var productClass = string.IsNullOrWhiteSpace(request.ProductClass) ? current.ProductClass : request.ProductClass.Trim();
        var stages = request.Stages is null ? current.Stages : ValidateStages(request.Stages);
        var documentMatrix = request.DocumentMatrix is null
            ? current.DocumentMatrix
            : await ValidateDocumentMatrix(current.Scope, request.DocumentMatrix, stages, cancellationToken);
        var wordingFacts = request.WordingFacts is null
            ? current.WordingFacts
            : await ValidateWordingFacts(current.Scope, request.WordingFacts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var updated = current with
        {
            VersionId = Guid.NewGuid(),
            Version = current.Version + 1,
            Name = name,
            ProductClass = productClass,
            IraApprovalReference = request.IraApprovalReference is null ? current.IraApprovalReference : NormalizeOrNull(request.IraApprovalReference),
            IraApprovalEffectiveDate = request.IraApprovalEffectiveDate ?? current.IraApprovalEffectiveDate,
            ApprovedWordingKnowledgeDocumentId = request.ApprovedWordingKnowledgeDocumentId is null
                ? current.ApprovedWordingKnowledgeDocumentId
                : NormalizeOrNull(request.ApprovedWordingKnowledgeDocumentId),
            Stages = stages,
            DocumentMatrix = documentMatrix,
            WordingFacts = wordingFacts,
            // Editing a published or pending-publish product returns it to Draft: the edited
            // content has not itself gone through publish approval, and the previous Published
            // version stays intact in history for any claim already filed against it (outcome C).
            Status = current.Status is ProductStatus.Published or ProductStatus.PendingPublishApproval
                ? ProductStatus.Draft
                : current.Status,
            UpdatedByUserId = context.UserId,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = request.ChangeReason.Trim(),
            PublishSubmittedByUserId = null,
            PublishSubmittedAt = null,
            PublishApprovedByUserId = null,
            PublishApprovedAt = null,
            PublishRejectedByUserId = null,
            PublishRejectedAt = null,
            PublishRejectionReason = null,
        };

        await repository.SaveAsync(updated, cancellationToken);
        await Audit(updated, "product.updated", updated.ChangeReason, cancellationToken);

        return ToDto(updated);
    }

    public async Task<ProductDto> SubmitForPublishApprovalAsync(Guid productId, SubmitProductRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var current = await RequireCurrent(productId, cancellationToken);

        if (current.Status != ProductStatus.Draft)
        {
            throw new ProductValidationException("status", "Only a draft product can be submitted for publish approval.");
        }

        if (!current.CanPublish)
        {
            throw new ProductValidationException(
                "iraApprovalReference",
                "A product cannot be activated without an IRA approval reference, its effective date, and at least one claim stage.");
        }

        var context = tenantContextAccessor.Current;
        var reason = string.IsNullOrWhiteSpace(request.ChangeReason) ? "Submitted for publish approval." : request.ChangeReason.Trim();
        var updated = current with
        {
            VersionId = Guid.NewGuid(),
            Version = current.Version + 1,
            Status = ProductStatus.PendingPublishApproval,
            PublishSubmittedByUserId = context.UserId,
            PublishSubmittedAt = DateTimeOffset.UtcNow,
            PublishApprovedByUserId = null,
            PublishApprovedAt = null,
            PublishRejectedByUserId = null,
            PublishRejectedAt = null,
            PublishRejectionReason = null,
            UpdatedByUserId = context.UserId,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = reason,
        };

        await repository.SaveAsync(updated, cancellationToken);
        await Audit(updated, "product.publish_submitted", reason, cancellationToken);

        return ToDto(updated);
    }

    public async Task<ProductDto> ApprovePublishAsync(Guid productId, ApproveProductPublishRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var current = await RequireCurrent(productId, cancellationToken);

        if (current.Status != ProductStatus.PendingPublishApproval)
        {
            throw new ProductValidationException("status", "Only a product pending publish approval can be approved.");
        }

        var context = tenantContextAccessor.Current;
        if (string.Equals(context.UserId, current.PublishSubmittedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProductValidationException(
                "reviewerUserId",
                "The person who submitted this product for publishing cannot also approve it. A second person must approve.");
        }

        var reason = string.IsNullOrWhiteSpace(request.ChangeReason) ? "Publish approved." : request.ChangeReason.Trim();
        var updated = current with
        {
            VersionId = Guid.NewGuid(),
            Version = current.Version + 1,
            Status = ProductStatus.Published,
            PublishApprovedByUserId = context.UserId,
            PublishApprovedAt = DateTimeOffset.UtcNow,
            UpdatedByUserId = context.UserId,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = reason,
        };

        await repository.SaveAsync(updated, cancellationToken);
        await Audit(updated, "product.published", reason, cancellationToken);

        return ToDto(updated);
    }

    public async Task<ProductDto> RejectPublishAsync(Guid productId, RejectProductPublishRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var current = await RequireCurrent(productId, cancellationToken);

        if (current.Status != ProductStatus.PendingPublishApproval)
        {
            throw new ProductValidationException("status", "Only a product pending publish approval can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ProductValidationException(nameof(request.Reason), "A reason is required to reject a product's publish request.");
        }

        var context = tenantContextAccessor.Current;
        var reason = request.Reason.Trim();
        var updated = current with
        {
            VersionId = Guid.NewGuid(),
            Version = current.Version + 1,
            Status = ProductStatus.Draft,
            PublishRejectedByUserId = context.UserId,
            PublishRejectedAt = DateTimeOffset.UtcNow,
            PublishRejectionReason = reason,
            UpdatedByUserId = context.UserId,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = reason,
        };

        await repository.SaveAsync(updated, cancellationToken);
        await Audit(updated, "product.publish_rejected", reason, cancellationToken);

        return ToDto(updated);
    }

    public async Task<ProductDto> RetireAsync(Guid productId, RetireProductRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var current = await RequireCurrent(productId, cancellationToken);

        if (current.Status != ProductStatus.Published)
        {
            throw new ProductValidationException("status", "Only a published product can be retired.");
        }

        if (string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            throw new ProductValidationException(nameof(request.ChangeReason), "A reason is required to retire a product.");
        }

        var context = tenantContextAccessor.Current;
        var reason = request.ChangeReason.Trim();
        var updated = current with
        {
            VersionId = Guid.NewGuid(),
            Version = current.Version + 1,
            Status = ProductStatus.Retired,
            UpdatedByUserId = context.UserId,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = reason,
        };

        await repository.SaveAsync(updated, cancellationToken);
        await Audit(updated, "product.retired", reason, cancellationToken);

        return ToDto(updated);
    }

    private async Task<Product> RequireCurrent(Guid productId, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        var current = await repository.GetCurrentAsync(context.Scope, productId, cancellationToken);
        return current ?? throw new ProductValidationException("productId", "Product was not found for the current tenant workspace.");
    }

    private async Task Audit(Product product, string actionName, string reason, CancellationToken cancellationToken) =>
        await auditWriter.WriteAsync(
            AuditEvent.ProductVersionSaved(
                product.Scope,
                product.UpdatedByUserId,
                product.ProductId,
                product.Version,
                product.Status.ToString(),
                actionName,
                reason,
                Guid.NewGuid().ToString("N")),
            cancellationToken);

    private static string? NormalizeOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<ClaimStageDefinition> ValidateStages(IReadOnlyList<ClaimStageDto> stages)
    {
        if (stages.Count == 0)
        {
            throw new ProductValidationException("stages", "A claim process needs at least one stage.");
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ClaimStageDefinition>(stages.Count);
        foreach (var stage in stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageKey))
            {
                throw new ProductValidationException("stages", "Every stage needs a stable key.");
            }

            if (!keys.Add(stage.StageKey.Trim()))
            {
                throw new ProductValidationException("stages", $"Stage key '{stage.StageKey}' is used more than once.");
            }

            if (string.IsNullOrWhiteSpace(stage.Name))
            {
                throw new ProductValidationException("stages", $"Stage '{stage.StageKey}' needs a name.");
            }

            if (string.IsNullOrWhiteSpace(stage.OwnerRole))
            {
                throw new ProductValidationException("stages", $"Stage '{stage.Name}' needs an owner role.");
            }

            if (stage.ServiceStandardWorkingDays is < 0)
            {
                throw new ProductValidationException("stages", $"Stage '{stage.Name}' cannot have a negative service standard.");
            }

            result.Add(new ClaimStageDefinition(
                stage.StageKey.Trim(),
                stage.Name.Trim(),
                stage.OwnerRole.Trim(),
                stage.IsAutomatic,
                stage.SortOrder,
                stage.ServiceStandardWorkingDays,
                stage.ValueTierStandards?.Select(tier => new ValueTierServiceStandard(tier.UpToValueUgx, tier.WorkingDays)).ToArray()));
        }

        return result.OrderBy(stage => stage.SortOrder).ToArray();
    }

    private async Task<IReadOnlyList<ProductDocumentRequirement>> ValidateDocumentMatrix(
        TenantScope scope,
        IReadOnlyList<DocumentMatrixEntryDto> entries,
        IReadOnlyList<ClaimStageDefinition> stages,
        CancellationToken cancellationToken)
    {
        var stageKeys = new HashSet<string>(stages.Select(stage => stage.StageKey), StringComparer.OrdinalIgnoreCase);
        var documentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProductDocumentRequirement>(entries.Count);

        // Claim intake build prompt outcome A: a document key used to silently vanish from
        // completeness tracking if it did not match one of the eight DocumentType names, with no
        // error anywhere. It is refused here instead, before it can ever reach a claim.
        var activeCatalogKeys = entries.Count == 0
            ? []
            : new HashSet<string>(
                (await documentTypeCatalogRepository.ListCurrentAsync(scope, cancellationToken))
                    .Where(definition => definition.Active)
                    .Select(definition => definition.Key),
                StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.DocumentKey))
            {
                throw new ProductValidationException("documentMatrix", "Every document needs a stable key.");
            }

            var documentKey = entry.DocumentKey.Trim();

            if (!documentKeys.Add(documentKey))
            {
                throw new ProductValidationException("documentMatrix", $"Document key '{documentKey}' is used more than once.");
            }

            if (string.IsNullOrWhiteSpace(entry.CustomerLabel))
            {
                throw new ProductValidationException("documentMatrix", $"Document '{documentKey}' needs the words the customer sees.");
            }

            if (string.IsNullOrWhiteSpace(entry.Reason))
            {
                throw new ProductValidationException("documentMatrix", $"Document '{documentKey}' needs a reason it is required.");
            }

            if (!stageKeys.Contains(entry.RequiredAtStageKey))
            {
                throw new ProductValidationException("documentMatrix", $"Document '{documentKey}' refers to a stage that does not exist in this process.");
            }

            var isBuiltInDocumentType = Enum.TryParse<DocumentType>(documentKey, ignoreCase: true, out _);
            if (!isBuiltInDocumentType && !activeCatalogKeys.Contains(documentKey))
            {
                throw new ProductValidationException(
                    "documentMatrix",
                    $"'{documentKey}' is not a recognized document type. Add it to the document type catalog first, or fix the spelling.");
            }

            result.Add(new ProductDocumentRequirement(
                documentKey,
                entry.CustomerLabel.Trim(),
                entry.Reason.Trim(),
                entry.IsRequired,
                entry.RequiredAtStageKey.Trim(),
                entry.IsRegulatoryOrWordingMandated));
        }

        return result;
    }

    private async Task<IReadOnlyList<ProductWordingFact>> ValidateWordingFacts(
        TenantScope scope, IReadOnlyList<ProductWordingFactDto> facts, CancellationToken cancellationToken)
    {
        if (facts.Count > 250)
        {
            throw new ProductValidationException("wordingFacts", "A product can keep up to 250 wording facts.");
        }

        if (facts.Count > 0 && knowledgeDocuments is null)
        {
            throw new ProductValidationException("wordingFacts", "Source validation is not available for these wording facts.");
        }

        var result = new List<ProductWordingFact>(facts.Count);
        foreach (var fact in facts)
        {
            if (string.IsNullOrWhiteSpace(fact.Category) || fact.Category.Length > 40
                || string.IsNullOrWhiteSpace(fact.Label) || fact.Label.Length > 120
                || string.IsNullOrWhiteSpace(fact.Value) || fact.Value.Length > 1000
                || string.IsNullOrWhiteSpace(fact.SourceChunkId) || fact.SourceChunkId.Length > 160
                || string.IsNullOrWhiteSpace(fact.SourceQuote) || fact.SourceQuote.Length > 2000
                || fact.Confidence is < 0m or > 1m)
            {
                throw new ProductValidationException("wordingFacts", "Each wording fact needs a value, source passage and confidence from 0 to 1.");
            }

            var document = await knowledgeDocuments!.GetAsync(fact.SourceKnowledgeDocumentId, scope, cancellationToken);
            var chunk = document?.DocumentType == KnowledgeDocumentType.PolicyWording
                ? document.Chunks.FirstOrDefault(candidate => candidate.ChunkId == fact.SourceChunkId)
                : null;
            if (chunk is null || !chunk.Content.Contains(fact.SourceQuote, StringComparison.Ordinal))
            {
                throw new ProductValidationException("wordingFacts", "A wording fact's source passage could not be verified in this workspace.");
            }

            result.Add(new ProductWordingFact(fact.Category.Trim(), fact.Label.Trim(), fact.Value.Trim(),
                fact.SourceKnowledgeDocumentId, fact.SourceChunkId, fact.SourceQuote.Trim(), fact.Confidence));
        }

        return result;
    }

    internal static ProductDto ToDto(Product product) =>
        new(
            product.ProductId,
            product.Version,
            product.Name,
            product.ProductClass,
            product.IraApprovalReference,
            product.IraApprovalEffectiveDate,
            product.ApprovedWordingKnowledgeDocumentId,
            product.Status.ToString(),
            product.CanPublish,
            product.Stages
                .OrderBy(stage => stage.SortOrder)
                .Select(stage => new ClaimStageDto(
                    stage.StageKey,
                    stage.Name,
                    stage.OwnerRole,
                    stage.IsAutomatic,
                    stage.SortOrder,
                    stage.ServiceStandardWorkingDays,
                    stage.ValueTierStandards?.Select(tier => new ValueTierServiceStandardDto(tier.UpToValueUgx, tier.WorkingDays)).ToArray()))
                .ToArray(),
            product.DocumentMatrix
                .Select(entry => new DocumentMatrixEntryDto(
                    entry.DocumentKey,
                    entry.CustomerLabel,
                    entry.Reason,
                    entry.IsRequired,
                    entry.RequiredAtStageKey,
                    entry.IsRegulatoryOrWordingMandated))
                .ToArray(),
            product.UpdatedByUserId,
            product.UpdatedAt,
            product.ChangeReason,
            product.PublishSubmittedByUserId,
            product.PublishSubmittedAt,
            product.PublishApprovedByUserId,
            product.PublishApprovedAt,
            product.PublishRejectedByUserId,
            product.PublishRejectedAt,
            product.PublishRejectionReason,
            product.WordingFacts.Select(fact => new ProductWordingFactDto(
                fact.Category, fact.Label, fact.Value, fact.SourceKnowledgeDocumentId, fact.SourceChunkId, fact.SourceQuote, fact.Confidence)).ToArray());
}

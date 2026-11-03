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
        // Claim types are validated first: a document requirement may name only claim types that
        // exist in the version being saved, so the matrix is checked against the new list.
        var claimTypes = request.ClaimTypes is null ? current.ClaimTypes : ValidateClaimTypes(request.ClaimTypes);
        var documentMatrix = request.DocumentMatrix is null
            ? current.DocumentMatrix
            : await ValidateDocumentMatrix(current.Scope, request.DocumentMatrix, stages, claimTypes, cancellationToken);
        if (request.DocumentMatrix is null && request.ClaimTypes is not null)
        {
            // Claim types were edited but the matrix was not: re-check the existing matrix against
            // them so a removed claim type cannot be left referenced by a document requirement.
            EnsureMatrixClaimTypesExist(documentMatrix, claimTypes);
        }
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
            ClaimTypes = claimTypes,
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

    private const int MaxClaimTypes = 60;

    /// <summary>Keys are lower-case slugs. A blank key is derived from the name. Order in the
    /// list is the order shown, so reordering is just sending the list in a new order.</summary>
    internal static IReadOnlyList<ClaimTypeDefinition> ValidateClaimTypes(IReadOnlyList<ClaimTypeDto> types)
    {
        if (types.Count > MaxClaimTypes)
        {
            throw new ProductValidationException("claimTypes", $"A product can have at most {MaxClaimTypes} claim types.");
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ClaimTypeDefinition>(types.Count);
        foreach (var type in types)
        {
            var name = type.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > 80)
            {
                throw new ProductValidationException("claimTypes", "Every claim type needs a name of up to 80 characters.");
            }

            var key = string.IsNullOrWhiteSpace(type.Key) ? Slug(name) : type.Key.Trim().ToLowerInvariant();
            if (!IsValidKey(key))
            {
                throw new ProductValidationException("claimTypes", $"'{key}' is not a valid key. Use lower-case letters, digits, hyphen or underscore, starting with a letter or digit.");
            }

            if (!keys.Add(key))
            {
                throw new ProductValidationException("claimTypes", $"Claim type key '{key}' is used more than once.");
            }

            if (!names.Add(ClaimTypeMatching.Normalize(name)))
            {
                throw new ProductValidationException("claimTypes", $"A claim type named '{name}' already exists.");
            }

            var description = type.Description?.Trim() ?? string.Empty;
            if (description.Length > 300)
            {
                throw new ProductValidationException("claimTypes", $"The description of '{name}' is longer than 300 characters.");
            }

            var aliases = (type.Aliases ?? [])
                .Select(alias => alias?.Trim() ?? string.Empty)
                .Where(alias => alias.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (aliases.Length > 10 || aliases.Any(alias => alias.Length > 60))
            {
                throw new ProductValidationException("claimTypes", $"'{name}' can have at most 10 aliases of up to 60 characters.");
            }

            result.Add(new ClaimTypeDefinition(key, name, description, type.Active, result.Count, aliases));
        }

        return result;
    }

    internal static string Slug(string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) && character < 128)
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length > 40 ? slug[..40].Trim('-') : slug;
    }

    private static bool IsValidKey(string key) =>
        key.Length is >= 1 and <= 40
        && char.IsAsciiLetterOrDigit(key[0])
        && key.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '-' or '_');

    private async Task<IReadOnlyList<ProductDocumentRequirement>> ValidateDocumentMatrix(
        TenantScope scope,
        IReadOnlyList<DocumentMatrixEntryDto> entries,
        IReadOnlyList<ClaimStageDefinition> stages,
        IReadOnlyList<ClaimTypeDefinition> claimTypes,
        CancellationToken cancellationToken)
    {
        var stageKeys = new HashSet<string>(stages.Select(stage => stage.StageKey), StringComparer.OrdinalIgnoreCase);
        var claimTypeKeys = new HashSet<string>(claimTypes.Select(type => type.Key), StringComparer.OrdinalIgnoreCase);
        // The same document key may appear more than once when each entry names different claim
        // types (for example a stricter police report for theft). What is refused is two entries
        // for one key that would both apply to the same claim type at the same level.
        var allClaimTypeKeysSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var specificSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            var entryClaimTypes = (entry.ClaimTypeKeys ?? [])
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray();
            foreach (var claimTypeKey in entryClaimTypes)
            {
                if (!claimTypeKeys.Contains(claimTypeKey))
                {
                    throw new ProductValidationException(
                        "documentMatrix",
                        $"Document '{documentKey}' refers to claim type '{claimTypeKey}', which this product does not have.");
                }
            }

            if (entryClaimTypes.Length == 0)
            {
                if (!allClaimTypeKeysSeen.Add(documentKey))
                {
                    throw new ProductValidationException("documentMatrix", $"Document key '{documentKey}' is used more than once for every claim type.");
                }
            }
            else
            {
                foreach (var claimTypeKey in entryClaimTypes)
                {
                    if (!specificSeen.Add($"{documentKey}|{claimTypeKey}"))
                    {
                        throw new ProductValidationException("documentMatrix", $"Document '{documentKey}' is set up twice for claim type '{claimTypeKey}'.");
                    }
                }
            }

            DocumentKind kind;
            if (string.IsNullOrWhiteSpace(entry.Kind))
            {
                kind = Enum.TryParse<DocumentType>(documentKey, ignoreCase: true, out var builtIn)
                    ? DocumentSlotDefaults.KindFor(builtIn)
                    : DocumentKind.Document;
            }
            else if (!Enum.TryParse(entry.Kind.Trim(), ignoreCase: true, out kind) || !Enum.IsDefined(kind))
            {
                throw new ProductValidationException("documentMatrix", $"Document '{documentKey}' has an unknown kind '{entry.Kind}'. Use Document or Evidence.");
            }

            var allowsMultiple = kind == DocumentKind.Evidence || entry.AllowsMultiplePages;
            var maxFiles = entry.MaxFiles is null or 0 ? DocumentSlotDefaults.MaxFilesFor(kind, allowsMultiple) : entry.MaxFiles.Value;
            if (maxFiles < 1 || maxFiles > DocumentSlotDefaults.AbsoluteMaxFiles)
            {
                throw new ProductValidationException("documentMatrix", $"Document '{documentKey}' can hold between 1 and {DocumentSlotDefaults.AbsoluteMaxFiles} files.");
            }

            if (!allowsMultiple)
            {
                maxFiles = 1;
            }

            var formats = (entry.AcceptedFormats ?? [])
                .Select(format => format.Trim().TrimStart('.').ToLowerInvariant())
                .Where(format => format.Length > 0)
                .Distinct()
                .ToArray();
            var unsupported = formats.FirstOrDefault(format => !DocumentSlotDefaults.AllSlotFormats.Contains(format));
            if (unsupported is not null)
            {
                throw new ProductValidationException(
                    "documentMatrix",
                    $"Document '{documentKey}' accepts '{unsupported}', which is not a supported file type. Supported: {string.Join(", ", DocumentSlotDefaults.AllSlotFormats)}.");
            }

            if (formats.Length == 0)
            {
                formats = DocumentSlotDefaults.FormatsFor(kind).ToArray();
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
                entry.IsRegulatoryOrWordingMandated,
                entryClaimTypes,
                kind,
                allowsMultiple,
                maxFiles,
                formats));
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

    private static void EnsureMatrixClaimTypesExist(IReadOnlyList<ProductDocumentRequirement> matrix, IReadOnlyList<ClaimTypeDefinition> claimTypes)
    {
        var keys = new HashSet<string>(claimTypes.Select(type => type.Key), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in matrix)
        {
            var missing = entry.ClaimTypeKeys?.FirstOrDefault(key => !keys.Contains(key));
            if (missing is not null)
            {
                throw new ProductValidationException(
                    "claimTypes",
                    $"Claim type '{missing}' is still used by the document '{entry.DocumentKey}'. Remove it from that document first, or keep the claim type and switch it off.");
            }
        }
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
                    entry.IsRegulatoryOrWordingMandated,
                    entry.ClaimTypeKeys ?? [],
                    entry.ToSlot().Kind.ToString(),
                    entry.ToSlot().AllowsMultiplePages,
                    entry.ToSlot().MaxFiles,
                    entry.ToSlot().AcceptedFormats))
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
                fact.Category, fact.Label, fact.Value, fact.SourceKnowledgeDocumentId, fact.SourceChunkId, fact.SourceQuote, fact.Confidence)).ToArray(),
            product.ClaimTypes
                .OrderBy(type => type.SortOrder)
                .Select(type => new ClaimTypeDto(type.Key, type.Name, type.Description, type.Active, type.SortOrder, type.Aliases ?? []))
                .ToArray());
}

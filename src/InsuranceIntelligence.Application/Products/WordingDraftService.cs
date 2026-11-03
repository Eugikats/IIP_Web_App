using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Products;

/// <summary>
/// Policy wording to drafted product setup (build-loop item 7). The model reads the wording that is
/// already stored for the product and proposes covered items, exclusions, claim types, required
/// documents and questions, each quoting its clause. A person accepts, edits or rejects every item;
/// applying writes only the accepted ones into a new draft version of the product, through the normal
/// product update, so it is validated, versioned and audited and still needs a second approver to
/// publish. The wording itself is never copied into the draft or the audit trail.
/// </summary>
public sealed class WordingDraftService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IWordingDraftRepository draftRepository,
    IProductQueryService productQueryService,
    IProductCommandService productCommandService,
    IKnowledgeDocumentRepository knowledgeRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    IChatCompletionProvider chatProvider,
    IAuditWriter auditWriter) : IWordingDraftService
{
    public async Task<WordingDraftDto> CreateAsync(Guid productId, CreateWordingDraftRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);
        var context = tenantContextAccessor.Current;
        var product = await RequireProductAsync(productId, cancellationToken);

        var documentId = request.KnowledgeDocumentId
            ?? (Guid.TryParse(product.ApprovedWordingKnowledgeDocumentId, out var attached) ? attached : (Guid?)null);
        if (documentId is null)
        {
            throw new ProductValidationException("knowledgeDocumentId", "Attach a policy wording to the product first, or choose the wording to read.");
        }

        var wording = await knowledgeRepository.GetAsync(documentId.Value, context.Scope, cancellationToken)
            ?? throw new ProductValidationException("knowledgeDocumentId", "That wording was not found in this workspace.");
        var chunks = wording.Chunks.OrderBy(chunk => chunk.ChunkIndex).ToArray();
        var fullText = string.Join("\n", chunks.Select(chunk => chunk.Content));
        if (string.IsNullOrWhiteSpace(fullText))
        {
            throw new ProductValidationException("knowledgeDocumentId", "No readable text was found in that wording. Upload a text, PDF or Word version that can be read.");
        }

        // Knowledge ingestion stores headings as chunk metadata rather than body text. Restore that
        // context for the reader, but verify citations against body text only.
        var contextualText = string.Join("\n", chunks.Select(chunk =>
            string.IsNullOrWhiteSpace(chunk.Section) ? chunk.Content : $"{chunk.Section}\n{chunk.Content}"));
        var (text, truncated) = WordingExtraction.Prepare(contextualText);
        var documentKeys = await DocumentTypeKeysAsync(context.Scope, cancellationToken);
        var prompt = WordingExtraction.BuildPrompt(wording.DocumentName, text, documentKeys);
        ChatCompletionResult completion;
        try
        {
            completion = await chatProvider.CompleteAsync(
                new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, Guid.NewGuid().ToString("N")),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new WordingExtractionException("The wording could not be read just now. Try again in a few minutes.");
        }

        var items = WordingExtraction.Parse(
            completion.Content,
            fullText,
            new WordingExtractionContext(
                documentKeys,
                (product.ClaimTypes ?? []).Select(type => type.Name).ToArray(),
                (product.DocumentMatrix ?? []).Select(entry => entry.DocumentKey).ToArray(),
                (product.ClaimFields ?? []).Select(field => field.Label).ToArray()));

        var draft = new WordingDraft
        {
            DraftId = Guid.NewGuid(),
            Scope = context.Scope,
            ProductId = productId,
            KnowledgeDocumentId = wording.KnowledgeDocumentId,
            DocumentName = wording.DocumentName,
            Status = WordingDraftStatus.InReview,
            Items = items,
            Provider = completion.Provider,
            ModelVersion = completion.ModelVersion,
            TextTruncated = truncated,
            CharactersRead = text.Length,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await draftRepository.SaveAsync(draft, cancellationToken);
        await AuditAsync(draft, "product.wording.drafted", $"items:{items.Count}", completion.Provider, null, cancellationToken);
        return ToDto(draft, documentKeys);
    }

    public async Task<IReadOnlyList<WordingDraftDto>> ListAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);
        var scope = tenantContextAccessor.Current.Scope;
        await RequireProductAsync(productId, cancellationToken);
        var keys = await DocumentTypeKeysAsync(scope, cancellationToken);
        return (await draftRepository.ListForProductAsync(scope, productId, cancellationToken))
            .OrderByDescending(draft => draft.CreatedAt)
            .Select(draft => ToDto(draft, keys))
            .ToArray();
    }

    public async Task<WordingDraftDto> GetAsync(Guid productId, Guid draftId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);
        var scope = tenantContextAccessor.Current.Scope;
        var draft = await RequireDraftAsync(scope, productId, draftId, cancellationToken);
        return ToDto(draft, await DocumentTypeKeysAsync(scope, cancellationToken));
    }

    public async Task<WordingDraftDto> UpdateItemAsync(Guid productId, Guid draftId, string itemId, UpdateWordingItemRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);
        var scope = tenantContextAccessor.Current.Scope;
        var draft = await RequireDraftAsync(scope, productId, draftId, cancellationToken);
        if (draft.Status != WordingDraftStatus.InReview)
        {
            throw new ProductValidationException("draft", "This reading has already been applied or discarded.");
        }

        var item = draft.Items.FirstOrDefault(candidate => candidate.ItemId == itemId)
            ?? throw new WordingDraftNotFoundException("That item is not in this reading.");
        var keys = await DocumentTypeKeysAsync(scope, cancellationToken);
        var updated = ApplyChanges(item, request, keys);
        var items = draft.Items.Select(candidate => candidate.ItemId == itemId ? updated : candidate).ToArray();
        var saved = draft with { Items = items };
        await draftRepository.SaveAsync(saved, cancellationToken);
        await AuditAsync(saved, "product.wording.item.decided", $"{updated.ItemId}:{updated.Decision}", null, null, cancellationToken);
        return ToDto(saved, keys);
    }

    public async Task<WordingApplyResultDto> ApplyAsync(Guid productId, Guid draftId, ApplyWordingDraftRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);
        var context = tenantContextAccessor.Current;
        var draft = await RequireDraftAsync(context.Scope, productId, draftId, cancellationToken);
        if (draft.Status != WordingDraftStatus.InReview)
        {
            throw new ProductValidationException("draft", "This reading has already been applied or discarded.");
        }

        if (draft.AcceptedCount == 0)
        {
            throw new ProductValidationException("items", "Accept at least one item before applying.");
        }

        var current = await RequireProductAsync(productId, cancellationToken);
        var reason = string.IsNullOrWhiteSpace(request.ChangeReason)
            ? $"Applied items read from the wording '{draft.DocumentName}'"
            : request.ChangeReason.Trim();
        var (update, report) = WordingApplication.Merge(current, draft, reason);
        if (report.TotalAdded == 0)
        {
            throw new ProductValidationException(
                "items",
                "None of the accepted items could be added: " + string.Join(" ", report.Skipped.Select(skipped => $"{skipped.Title}: {skipped.Reason}")));
        }

        var product = await productCommandService.UpdateDraftAsync(productId, update, cancellationToken);
        var applied = draft with
        {
            Status = WordingDraftStatus.Applied,
            AppliedByUserId = context.UserId,
            AppliedAt = DateTimeOffset.UtcNow,
            AppliedProductVersion = product.Version,
        };
        await draftRepository.SaveAsync(applied, cancellationToken);
        await AuditAsync(applied, "product.wording.applied", $"v{product.Version}:+{report.TotalAdded}", null, reason, cancellationToken);
        return new WordingApplyResultDto(
            ToDto(applied, await DocumentTypeKeysAsync(context.Scope, cancellationToken)),
            product.Version,
            report.CoverageAdded,
            report.ClaimTypesAdded,
            report.DocumentsAdded,
            report.FieldsAdded,
            report.Skipped);
    }

    public async Task<WordingDraftDto> DiscardAsync(Guid productId, Guid draftId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);
        var scope = tenantContextAccessor.Current.Scope;
        var draft = await RequireDraftAsync(scope, productId, draftId, cancellationToken);
        if (draft.Status != WordingDraftStatus.InReview)
        {
            throw new ProductValidationException("draft", "This reading has already been applied or discarded.");
        }

        var discarded = draft with { Status = WordingDraftStatus.Discarded };
        await draftRepository.SaveAsync(discarded, cancellationToken);
        await AuditAsync(discarded, "product.wording.discarded", "Discarded", null, null, cancellationToken);
        return ToDto(discarded, await DocumentTypeKeysAsync(scope, cancellationToken));
    }

    // ---- helpers ----

    internal static WordingDraftItem ApplyChanges(WordingDraftItem item, UpdateWordingItemRequest request, IReadOnlyList<string> documentTypeKeys)
    {
        var changed = item;
        var edited = false;

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length is 0 or > 120)
            {
                throw new ProductValidationException("title", "The title must be between 1 and 120 characters.");
            }

            edited |= title != item.Title;
            changed = changed with { Title = title };
        }

        if (request.Detail is not null)
        {
            var detail = request.Detail.Trim();
            if (detail.Length > 500)
            {
                throw new ProductValidationException("detail", "The detail must be at most 500 characters.");
            }

            edited |= detail != item.Detail;
            changed = changed with { Detail = detail };
        }

        if (request.Aliases is not null)
        {
            changed = changed with { Aliases = Clean(request.Aliases, 12, 60) };
            edited = true;
        }

        if (request.ClaimTypeNames is not null)
        {
            changed = changed with { ClaimTypeNames = Clean(request.ClaimTypeNames, 20, 60) };
            edited = true;
        }

        if (request.Required is not null)
        {
            edited |= request.Required != item.Required;
            changed = changed with { Required = request.Required.Value };
        }

        if (request.FieldType is not null && item.Kind == WordingItemKind.Field)
        {
            if (!Enum.TryParse<ClaimFieldType>(request.FieldType.Trim(), ignoreCase: true, out var type) || !Enum.IsDefined(type))
            {
                throw new ProductValidationException("fieldType", "Choose Text, Number, Date, Select, Phone, Currency or YesNo.");
            }

            edited |= type.ToString() != item.FieldType;
            changed = changed with { FieldType = type.ToString() };
        }

        if (request.Options is not null)
        {
            changed = changed with { Options = Clean(request.Options, 30, 80) };
            edited = true;
        }

        if (changed.Kind == WordingItemKind.Field && changed.FieldType == nameof(ClaimFieldType.Select) && changed.Options.Count < 2)
        {
            throw new ProductValidationException("options", "A question with a list of answers needs at least two choices.");
        }

        if (request.DocumentKey is not null && item.Kind == WordingItemKind.Document)
        {
            var requested = request.DocumentKey.Trim();
            var canonical = documentTypeKeys.FirstOrDefault(key => string.Equals(key, requested, StringComparison.OrdinalIgnoreCase))
                ?? throw new ProductValidationException("documentKey", $"There is no document type called '{requested}'.");
            edited |= !string.Equals(canonical, item.DocumentKey, StringComparison.Ordinal);
            changed = changed with { DocumentKey = canonical };
            // A document type that now exists clears the problem "no such type", but never the
            // problem of a clause that is not in the wording.
            if (changed.Citation.Verified)
            {
                changed = changed with { Problem = null };
            }
        }

        if (edited)
        {
            changed = changed with { Edited = true };
        }

        if (request.Decision is not null)
        {
            if (!Enum.TryParse<WordingItemDecision>(request.Decision.Trim(), ignoreCase: true, out var decision) || !Enum.IsDefined(decision))
            {
                throw new ProductValidationException("decision", "Choose Accepted, Rejected or Pending.");
            }

            if (decision == WordingItemDecision.Accepted && changed.Problem is not null)
            {
                throw new ProductValidationException("decision", changed.Problem);
            }

            changed = changed with { Decision = decision };
        }
        else if (edited && changed.Decision == WordingItemDecision.Accepted)
        {
            // An accepted item that is then edited has to be accepted again: the person approved
            // the earlier wording of it, not this one.
            changed = changed with { Decision = WordingItemDecision.Pending };
        }

        return changed;
    }

    private static IReadOnlyList<string> Clean(IReadOnlyList<string> values, int max, int maxLength) =>
        values
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Select(value => value.Length > maxLength ? value[..maxLength] : value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray();

    private async Task<ProductDto> RequireProductAsync(Guid productId, CancellationToken cancellationToken) =>
        await productQueryService.GetCurrentAsync(productId, cancellationToken)
        ?? throw new ProductValidationException("productId", "Product was not found for the current tenant workspace.");

    private async Task<WordingDraft> RequireDraftAsync(TenantScope scope, Guid productId, Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await draftRepository.GetAsync(scope, draftId, cancellationToken);
        if (draft is null || draft.ProductId != productId)
        {
            throw new WordingDraftNotFoundException("That reading was not found for this product.");
        }

        return draft;
    }

    /// <summary>The document types a requirement may name: the built-in ones and any the workspace
    /// has added and left active.</summary>
    private async Task<IReadOnlyList<string>> DocumentTypeKeysAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var keys = Enum.GetNames<DocumentType>().Where(name => name != nameof(DocumentType.Unknown)).ToList();
        foreach (var definition in await documentTypeCatalogRepository.ListCurrentAsync(scope, cancellationToken))
        {
            if (definition.Active && !keys.Contains(definition.Key, StringComparer.OrdinalIgnoreCase))
            {
                keys.Add(definition.Key);
            }
        }

        return keys;
    }

    private Task AuditAsync(WordingDraft draft, string action, string after, string? provider, string? reason, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            AuditEvent.WordingDraftEvent(
                draft.Scope,
                tenantContextAccessor.Current.UserId,
                draft.ProductId,
                draft.DraftId,
                draft.KnowledgeDocumentId,
                action,
                after,
                provider,
                reason,
                Guid.NewGuid().ToString("N")),
            cancellationToken);

    internal static WordingDraftDto ToDto(WordingDraft draft, IReadOnlyList<string> documentTypeKeys) =>
        new(
            draft.DraftId,
            draft.ProductId,
            draft.KnowledgeDocumentId,
            draft.DocumentName,
            draft.Status.ToString(),
            draft.Items.Select(ToDto).ToArray(),
            draft.Provider,
            draft.ModelVersion,
            draft.TextTruncated,
            draft.CharactersRead,
            draft.CreatedByUserId,
            draft.CreatedAt,
            draft.AppliedByUserId,
            draft.AppliedAt,
            draft.AppliedProductVersion,
            draft.AcceptedCount,
            documentTypeKeys);

    private static WordingDraftItemDto ToDto(WordingDraftItem item) =>
        new(
            item.ItemId,
            item.Kind.ToString(),
            item.Title,
            item.Detail,
            new WordingCitationDto(item.Citation.Quote, item.Citation.Clause, item.Citation.Page, item.Citation.Verified),
            item.Decision.ToString(),
            item.Edited,
            item.Problem,
            item.Note,
            item.Aliases,
            item.ClaimTypeNames,
            item.Required,
            item.DocumentKey,
            item.FieldType,
            item.Options);
}

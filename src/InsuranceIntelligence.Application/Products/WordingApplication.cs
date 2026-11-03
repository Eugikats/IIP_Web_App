using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Products;

/// <summary>What applying a reviewed draft did: how many items went into the product and which
/// accepted items were left out, each with the reason.</summary>
public sealed record WordingApplyReport(
    int CoverageAdded,
    int ClaimTypesAdded,
    int DocumentsAdded,
    int FieldsAdded,
    IReadOnlyList<WordingSkippedItem> Skipped)
{
    public int TotalAdded => CoverageAdded + ClaimTypesAdded + DocumentsAdded + FieldsAdded;
}

public sealed record WordingSkippedItem(string ItemId, string Title, string Reason);

/// <summary>
/// Turns the accepted items of a draft into one product update (build-loop item 7). Additive only:
/// it never removes or rewrites what the product already has, and an accepted item that duplicates
/// something already there, or refers to a claim type the product does not have, is left out with a
/// reason rather than guessed at. The result goes through the normal product update, so it is
/// validated, versioned and audited like any edit, and lands as a draft that still needs the usual
/// second approver to publish.
/// </summary>
public static class WordingApplication
{
    public static (UpdateProductRequest Request, WordingApplyReport Report) Merge(ProductDto current, WordingDraft draft, string changeReason)
    {
        var skipped = new List<WordingSkippedItem>();
        var accepted = draft.Items.Where(item => item.Decision == WordingItemDecision.Accepted && item.Problem is null).ToArray();

        // Claim types first, so a document or question can refer to one accepted in the same pass.
        var claimTypes = current.ClaimTypes?.ToList() ?? [];
        var claimTypesAdded = 0;
        foreach (var item in accepted.Where(item => item.Kind == WordingItemKind.ClaimType))
        {
            var existing = claimTypes.Select(ToDefinition).ToArray();
            if (ClaimTypeMatching.Find(existing, item.Title) is not null)
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, "The product already has this claim type."));
                continue;
            }

            claimTypes.Add(new ClaimTypeDto(null, item.Title, item.Detail, true, claimTypes.Count == 0 ? 1 : claimTypes.Max(type => type.SortOrder) + 1, item.Aliases));
            claimTypesAdded++;
        }

        var resolvable = claimTypes.Select(ToDefinition).ToArray();

        var coverage = current.CoverageItems?.ToList() ?? [];
        var coverageAdded = 0;
        foreach (var item in accepted.Where(item => item.Kind is WordingItemKind.Covered or WordingItemKind.Exclusion))
        {
            var kind = item.Kind == WordingItemKind.Covered ? "Covered" : "Excluded";
            var key = WordingExtraction.Normalize(item.Title);
            if (coverage.Any(existing => existing.Kind == kind && WordingExtraction.Normalize(existing.Key ?? existing.Title) == key))
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, $"The product already lists this as {kind.ToLowerInvariant()}."));
                continue;
            }

            coverage.Add(new CoverageItemDto(null, kind, item.Title, item.Detail, item.Citation.Clause, item.Citation.Page, item.Citation.Quote));
            coverageAdded++;
        }

        var stageKey = ChooseStage(current);
        var matrix = current.DocumentMatrix?.ToList() ?? [];
        var documentsAdded = 0;
        foreach (var item in accepted.Where(item => item.Kind == WordingItemKind.Document))
        {
            if (string.IsNullOrWhiteSpace(item.DocumentKey))
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, "It has no document type."));
                continue;
            }

            if (stageKey is null)
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, "The product has no claim process stage to ask for it at."));
                continue;
            }

            if (!TryResolve(item.ClaimTypeNames, resolvable, out var keys, out var missing))
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, $"The product has no claim type called '{missing}'. Accept that claim type too, or edit this item."));
                continue;
            }

            if (matrix.Any(entry => string.Equals(entry.DocumentKey, item.DocumentKey, StringComparison.OrdinalIgnoreCase) && Overlaps(entry.ClaimTypeKeys, keys)))
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, "The product already asks for this document for the same claim types."));
                continue;
            }

            var reason = string.IsNullOrWhiteSpace(item.Detail)
                ? $"Required by the policy wording{ClauseSuffix(item)}."
                : item.Detail;
            matrix.Add(new DocumentMatrixEntryDto(
                item.DocumentKey!,
                item.Title,
                reason,
                item.Required,
                stageKey,
                IsRegulatoryOrWordingMandated: true,
                ClaimTypeKeys: keys));
            documentsAdded++;
        }

        var fields = current.ClaimFields?.ToList() ?? [];
        var fieldsAdded = 0;
        foreach (var item in accepted.Where(item => item.Kind == WordingItemKind.Field))
        {
            if (fields.Any(field => WordingExtraction.Normalize(field.Label) == WordingExtraction.Normalize(item.Title)))
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, "The product already asks this question."));
                continue;
            }

            if (!TryResolve(item.ClaimTypeNames, resolvable, out var keys, out var missing))
            {
                skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, $"The product has no claim type called '{missing}'. Accept that claim type too, or edit this item."));
                continue;
            }

            if (fields.Count == 0)
            {
                // A product with no questions of its own uses the built-in set. Keep it, so adding
                // one question does not silently replace all the others.
                fields.AddRange(BuiltInClaimFields.For(current.ProductClass).Select(ToDto));
                if (fields.Any(field => WordingExtraction.Normalize(field.Label) == WordingExtraction.Normalize(item.Title)))
                {
                    skipped.Add(new WordingSkippedItem(item.ItemId, item.Title, "The product already asks this question."));
                    continue;
                }
            }

            fields.Add(new ClaimFieldDto(
                null,
                item.Title,
                item.FieldType ?? "Text",
                item.Required,
                item.Options,
                string.IsNullOrWhiteSpace(item.Detail) ? null : item.Detail,
                fields.Count == 0 ? 1 : fields.Max(field => field.SortOrder) + 1,
                CustomerVisible: true,
                keys));
            fieldsAdded++;
        }

        var report = new WordingApplyReport(coverageAdded, claimTypesAdded, documentsAdded, fieldsAdded, skipped);
        var request = new UpdateProductRequest(
            null, null, null, null, null, null,
            documentsAdded > 0 ? matrix : null,
            changeReason,
            WordingFacts: null,
            ClaimTypes: claimTypesAdded > 0 ? claimTypes : null,
            ClaimFields: fieldsAdded > 0 ? fields : null,
            CoverageItems: coverageAdded > 0 ? coverage : null);
        return (request, report);
    }

    private static string? ChooseStage(ProductDto product)
    {
        var stages = product.Stages ?? [];
        return stages.FirstOrDefault(stage => stage.StageKey == DefaultClaimStageLibrary.AcknowledgementAndDocumentRequest)?.StageKey
            ?? stages.OrderBy(stage => stage.SortOrder).FirstOrDefault()?.StageKey;
    }

    private static string ClauseSuffix(WordingDraftItem item) =>
        string.IsNullOrWhiteSpace(item.Citation.Clause) ? string.Empty : $" (clause {item.Citation.Clause})";

    /// <summary>Names to claim type keys. An empty list means every claim type, which is a real
    /// choice and stays empty; a name that matches nothing is reported, never dropped.</summary>
    private static bool TryResolve(IReadOnlyList<string> names, IReadOnlyList<ClaimTypeDefinition> types, out List<string> keys, out string missing)
    {
        keys = [];
        missing = string.Empty;
        foreach (var name in names)
        {
            var match = ClaimTypeMatching.Find(types, name);
            if (match is null)
            {
                missing = name;
                return false;
            }

            if (!keys.Contains(match.Key, StringComparer.OrdinalIgnoreCase))
            {
                keys.Add(match.Key);
            }
        }

        return true;
    }

    private static bool Overlaps(IReadOnlyList<string>? existing, IReadOnlyList<string> added) =>
        (existing is null or { Count: 0 } && added.Count == 0)
        || (existing is { Count: > 0 } && added.Any(key => existing.Contains(key, StringComparer.OrdinalIgnoreCase)));

    private static ClaimTypeDefinition ToDefinition(ClaimTypeDto type) =>
        new(string.IsNullOrWhiteSpace(type.Key) ? ProductCommandService.Slug(type.Name) : type.Key, type.Name, type.Description ?? string.Empty, type.Active, type.SortOrder, type.Aliases);

    private static ClaimFieldDto ToDto(ClaimFieldDefinition field) =>
        new(field.Key, field.Label, field.Type.ToString(), field.Required, field.Options ?? [], field.HelpText, field.SortOrder, field.CustomerVisible, field.ClaimTypeKeys ?? []);
}

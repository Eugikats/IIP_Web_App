using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.Documents;

/// <summary>The document types a workspace recognises: the built-in ones, with the insurer's own
/// words where it has set them, plus every type it added.</summary>
public static class DocumentCatalogue
{
    public static IReadOnlyList<DocumentCatalogueEntry> Build(IEnumerable<DocumentTypeDefinition> workspaceDefinitions)
    {
        var entries = new Dictionary<string, DocumentCatalogueEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in Enum.GetValues<DocumentType>().Where(type => type != DocumentType.Unknown))
        {
            entries[type.ToString()] = new DocumentCatalogueEntry(type.ToString(), BuiltInDocumentWording.Label(type), BuiltInDocumentWording.Description(type));
        }

        foreach (var definition in workspaceDefinitions)
        {
            if (definition.Active)
            {
                entries[definition.Key] = new DocumentCatalogueEntry(definition.Key, definition.CustomerLabel, definition.Description);
            }
            else
            {
                entries.Remove(definition.Key);
            }
        }

        return entries.Values.OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public interface IDocumentRecognitionService
{
    /// <summary>Looks at a stored document, names it, and decides where it belongs. A document the
    /// customer put in a slot stays there (and is flagged for staff when the recogniser confidently
    /// disagrees); one with no slot is filed into the matching slot when the recogniser is confident
    /// and otherwise sent to staff review. Returns the document with the outcome; the caller saves it.</summary>
    Task<CaseDocument> ApplyAsync(CaseDocument document, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class DocumentRecognitionService(
    IDocumentRecognizer recognizer,
    IDocumentTypeCatalogRepository catalogueRepository,
    ICaseDocumentRepository documentRepository,
    IDocumentChecklistProvider checklistProvider,
    IDocumentStorageProvider storageProvider,
    IAuditRepository auditRepository) : IDocumentRecognitionService
{
    public async Task<CaseDocument> ApplyAsync(CaseDocument document, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default)
    {
        var scope = claimCase.Scope;
        var catalogue = DocumentCatalogue.Build(await catalogueRepository.ListCurrentAsync(scope, cancellationToken));

        DocumentRecognition recognition;
        try
        {
            recognition = await recognizer.RecognizeAsync(
                new DocumentRecognitionRequest(
                    document.OriginalFileName,
                    document.ContentType,
                    ct => storageProvider.ReadAsync(scope, document.ObjectKey, ct),
                    catalogue),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A recogniser that is down never blocks a claim: the file is simply sent to a person.
            recognition = new DocumentRecognition(null, 0m, "The file could not be looked at just now.", "none", null, DateTimeOffset.UtcNow);
        }

        // Only a type the workspace actually has counts: a model that invents a key is not trusted.
        if (recognition.TypeKey is not null)
        {
            var known = catalogue.FirstOrDefault(entry => string.Equals(entry.Key, recognition.TypeKey, StringComparison.OrdinalIgnoreCase));
            recognition = known is null
                ? recognition with { TypeKey = null, Confidence = 0m, Reason = "It named a document type this workspace does not have." }
                : recognition with { TypeKey = known.Key };
        }

        var others = (await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken))
            .Where(other => other.DocumentId != document.DocumentId && !other.IsSuperseded)
            .ToArray();
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, others, cancellationToken);
        var slots = checklist.Slots;

        var result = document with { Recognition = recognition };
        string outcome;
        string label;

        if (document.DocumentTypeKey is not null)
        {
            // The customer's slot wins. The recogniser only adds a flag for staff when it disagrees.
            label = slots.FirstOrDefault(slot => string.Equals(slot.Key, document.DocumentTypeKey, StringComparison.OrdinalIgnoreCase))?.Label
                ?? catalogue.FirstOrDefault(entry => string.Equals(entry.Key, document.DocumentTypeKey, StringComparison.OrdinalIgnoreCase))?.Label
                ?? document.DocumentTypeKey;
            outcome = result.DisagreesWithClassifier ? "disagrees" : "kept";
        }
        else if (recognition.IsConfident
                 && slots.FirstOrDefault(slot => string.Equals(slot.Key, recognition.TypeKey, StringComparison.OrdinalIgnoreCase)) is { } slot
                 && (slot.AllowsMultipleFiles || !others.Any(other => string.Equals(other.EffectiveKey, slot.Key, StringComparison.OrdinalIgnoreCase))))
        {
            label = slot.Label;
            outcome = "filed";
            result = result with
            {
                DocumentTypeKey = slot.Key,
                SlotKind = slot.Kind,
                FiledByRecognition = true,
                PageOrder = slot.Kind == DocumentKind.Document && slot.AllowsMultipleFiles
                    ? SlotPdfService.PagesInOrder(others, slot.Key).Count + 1
                    : null,
            };
        }
        else if (recognition.IsConfident)
        {
            var entry = catalogue.First(entry => string.Equals(entry.Key, recognition.TypeKey, StringComparison.OrdinalIgnoreCase));
            label = entry.Label;
            outcome = "review";
            var alreadyThere = slots.Any(slot => string.Equals(slot.Key, entry.Key, StringComparison.OrdinalIgnoreCase));
            result = result with
            {
                NeedsReview = true,
                ReviewReason = alreadyThere
                    ? $"It reads as {entry.Label}, but that document is already in place. Check which one to keep."
                    : $"It reads as {entry.Label}, which this claim does not ask for.",
            };
        }
        else
        {
            label = DocumentNaming.UnsortedLabel;
            outcome = "review";
            result = result with { NeedsReview = true, ReviewReason = "We could not tell what this document is. Please look at it and put it in the right place." };
        }

        result = result with { DisplayName = NextName(label, claimCase.CaseId, document.OriginalFileName, others) };
        await auditRepository.WriteAsync(AuditEvent.DocumentRecognised(result, outcome, correlationId), cancellationToken);
        return result;
    }

    /// <summary>The first of a kind on the claim has no number; the second is _2, and so on.</summary>
    public static string NextName(string label, Guid caseId, string originalFileName, IReadOnlyCollection<CaseDocument> others)
    {
        var baseName = Path.GetFileNameWithoutExtension(DocumentNaming.DisplayName(label, ClaimReference.For(caseId), 1, originalFileName));
        var pattern = new Regex($"^{Regex.Escape(baseName)}(_\\d+)?$");
        var taken = others.Count(other => other.DisplayName is not null && pattern.IsMatch(Path.GetFileNameWithoutExtension(other.DisplayName)));
        return DocumentNaming.DisplayName(label, ClaimReference.For(caseId), taken + 1, originalFileName);
    }
}

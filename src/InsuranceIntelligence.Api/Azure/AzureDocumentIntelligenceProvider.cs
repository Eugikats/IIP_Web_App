using Azure;
using Azure.AI.DocumentIntelligence;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public sealed class AzureDocumentIntelligenceProvider(InfrastructureOptions options, ILogger<AzureDocumentIntelligenceProvider> logger) : IDocumentProcessingProvider
{
    public async Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
    {
        if (AzureProviderSupport.Missing(options.DocumentIntelligence.Endpoint))
        {
            return SafeMissingConfiguration(request, "Document Intelligence endpoint is not configured.");
        }

        try
        {
            if (request.OpenContentStreamAsync is null)
            {
                return SafeMissingConfiguration(request, "Document Intelligence requires a stream-capable document storage provider.");
            }

            // Use API key auth when managed identity is disabled and a key is provided.
            var client = !options.DocumentIntelligence.UseManagedIdentity && !string.IsNullOrWhiteSpace(options.DocumentIntelligence.ApiKey)
                ? new DocumentIntelligenceClient(new Uri(options.DocumentIntelligence.Endpoint!), new AzureKeyCredential(options.DocumentIntelligence.ApiKey))
                : new DocumentIntelligenceClient(new Uri(options.DocumentIntelligence.Endpoint!), AzureProviderSupport.Credential());
            await using var content = await request.OpenContentStreamAsync(cancellationToken);
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            var modelId = string.IsNullOrWhiteSpace(options.DocumentIntelligence.DefaultModelId)
                ? "prebuilt-document"
                : options.DocumentIntelligence.DefaultModelId;
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                modelId,
                BinaryData.FromStream(content),
                cancellationToken: cancellationToken);
            var analyzeResult = operation.Value;
            var modelVersion = analyzeResult.ModelId ?? modelId;
            var (fields, confidences, boundingBoxes) = BuildExtraction(analyzeResult);
            var warnings = BuildWarnings(analyzeResult);
            // The printed words decide first; the file name is only a weak hint and says so in its low confidence.
            var guess = DocumentContentClassifier.Classify(
                request.OriginalFileName, analyzeResult.Content, analyzeResult.Documents.FirstOrDefault()?.DocumentType);

            logger.LogInformation("Azure Document Intelligence processed document {DocumentId} with model {ModelId} version {ModelVersion}.", request.DocumentId, modelId, modelVersion);
            return new DocumentProcessingResult(
                new DocumentClassificationResult(guess.Type, guess.Confidence, "AzureDocumentIntelligence", DateTimeOffset.UtcNow, modelVersion),
                new DocumentExtractionResult(fields, confidences, "AzureDocumentIntelligence", DateTimeOffset.UtcNow, false, null, null, null, boundingBoxes, modelVersion),
                warnings);
        }
        catch (Exception ex) when (ex is RequestFailedException or InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(ex, "Azure Document Intelligence processing failed safely for document {DocumentId}.", request.DocumentId);
            return SafeMissingConfiguration(request, AzureProviderSupport.SafeError("Azure Document Intelligence", ex));
        }
    }

    private static (Dictionary<string, string> Fields, Dictionary<string, decimal> Confidences, Dictionary<string, BoundingBox> BoundingBoxes) BuildExtraction(
        AnalyzeResult analyzeResult)
    {
        var boundingBoxes = new Dictionary<string, BoundingBox>();
        var pageSizes = new Dictionary<int, (double Width, double Height)>();
        foreach (var page in analyzeResult.Pages)
        {
            if (page.Width is > 0 && page.Height is > 0)
            {
                pageSizes[page.PageNumber] = (page.Width.Value, page.Height.Value);
            }
        }

        var fields = new Dictionary<string, string>();
        var confidences = new Dictionary<string, decimal>();

        foreach (var document in analyzeResult.Documents)
        {
            if (!string.IsNullOrWhiteSpace(document.DocumentType))
            {
                fields.TryAdd("azureDocumentType", document.DocumentType);
                confidences["azureDocumentType"] = AzureDocumentIntelligenceMapping.NormalizeConfidence(document.Confidence);
            }

            foreach (var field in document.Fields)
            {
                var value = field.Value.Content;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var fieldKey = $"field.{field.Key}";
                fields[fieldKey] = value;
                confidences[fieldKey] = AzureDocumentIntelligenceMapping.NormalizeConfidence(field.Value.Confidence);

                // Where the value sits on the page, scaled to the page, with every region it covers.
                if (BoxFor(field.Value.BoundingRegions, pageSizes) is { } fieldBox)
                {
                    boundingBoxes[fieldKey] = fieldBox;
                }
            }
        }

        foreach (var pair in analyzeResult.KeyValuePairs)
        {
            if (string.IsNullOrWhiteSpace(pair.Key?.Content) || string.IsNullOrWhiteSpace(pair.Value?.Content))
            {
                continue;
            }

            var key = $"keyValue.{NormalizeKey(pair.Key.Content)}";
            fields[key] = pair.Value.Content;
            confidences[key] = AzureDocumentIntelligenceMapping.NormalizeConfidence(pair.Confidence);

            // Most fields of a general document arrive as key-value pairs; the box is the value's own words.
            if (BoxFor(pair.Value.BoundingRegions, pageSizes) is { } pairBox)
            {
                boundingBoxes[key] = pairBox;
            }
        }

        return (fields, confidences, boundingBoxes);
    }

    // Azure reports polygons in the page's own unit (inches for a PDF, pixels for an image), so each one is
    // divided by that page's size. A region on a page with no reported size is dropped, never guessed.
    private static BoundingBox? BoxFor(IReadOnlyList<BoundingRegion>? regions, IReadOnlyDictionary<int, (double Width, double Height)> pageSizes)
    {
        if (regions is not { Count: > 0 })
        {
            return null;
        }

        var boxes = new List<BoundingBox>();
        foreach (var region in regions)
        {
            if (pageSizes.TryGetValue(region.PageNumber, out var size)
                && DocumentGeometry.FromPolygon(region.PageNumber, region.Polygon, size.Width, size.Height) is { } box)
            {
                boxes.Add(box);
            }
        }

        return DocumentGeometry.Combine(boxes);
    }

    private static IReadOnlyCollection<DocumentValidationWarning> BuildWarnings(AnalyzeResult analyzeResult)
    {
        var warnings = new List<DocumentValidationWarning>();
        if (analyzeResult.Pages.Count == 0)
        {
            warnings.Add(new DocumentValidationWarning("document.pages.empty", "Document Intelligence did not return any pages for this document.", "warning"));
        }

        if (analyzeResult.Documents.Count == 0 && analyzeResult.KeyValuePairs.Count == 0)
        {
            warnings.Add(new DocumentValidationWarning("document.extraction.low_signal", "Document Intelligence returned no structured document fields or key-value pairs.", "warning"));
        }

        return warnings;
    }

    private static DocumentProcessingResult SafeMissingConfiguration(DocumentProcessingRequest request, string message)
    {
        var fields = new Dictionary<string, string>();
        var warnings = new[]
        {
            new DocumentValidationWarning("document.provider.configuration_required", message, "error")
        };
        return new DocumentProcessingResult(
            new DocumentClassificationResult(DocumentType.Unknown, 0m, "AzureDocumentIntelligence", DateTimeOffset.UtcNow),
            new DocumentExtractionResult(fields, fields.Keys.ToDictionary(key => key, _ => 0m), "AzureDocumentIntelligence", DateTimeOffset.UtcNow, false, null, null),
            warnings);
        // Note: no ModelVersion or BoundingBoxes on the not-configured fallback path, those require a real extraction.
    }

    private static DocumentType Classify(string fileName, AnalyzeResult? analyzeResult = null)
    {
        var azureType = analyzeResult?.Documents.FirstOrDefault()?.DocumentType;
        if (!string.IsNullOrWhiteSpace(azureType))
        {
            if (azureType.Contains("invoice", StringComparison.OrdinalIgnoreCase)) return DocumentType.Invoice;
            if (azureType.Contains("receipt", StringComparison.OrdinalIgnoreCase)) return DocumentType.Invoice;
            if (azureType.Contains("medical", StringComparison.OrdinalIgnoreCase)) return DocumentType.MedicalReport;
            if (azureType.Contains("identity", StringComparison.OrdinalIgnoreCase)) return DocumentType.CustomerId;
        }

        // Whole-token file name match (see DocumentFileNameClassifier); "accident_report.pdf" is
        // not a customer ID just because it contains the letters "id".
        return DocumentFileNameClassifier.Classify(fileName);
    }

    private static decimal ClassificationConfidence(AnalyzeResult analyzeResult)
    {
        var confidence = analyzeResult.Documents.FirstOrDefault()?.Confidence;
        return confidence is null or <= 0 ? 0.70m : DecimalConfidence(confidence);
    }

    private static decimal DecimalConfidence(float? confidence) =>
        confidence is null or <= 0 ? 0.70m : Math.Clamp((decimal)confidence.Value, 0m, 1m);

    private static string NormalizeKey(string value)
    {
        var cleaned = string.Join('_', value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        cleaned = cleaned.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }
}

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

            var client = new DocumentIntelligenceClient(new Uri(options.DocumentIntelligence.Endpoint!), AzureProviderSupport.Credential());
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
            var (fields, confidences) = BuildExtraction(request, analyzeResult, modelId);
            var warnings = BuildWarnings(analyzeResult);

            logger.LogInformation("Azure Document Intelligence processed document {DocumentId} with model {ModelId}.", request.DocumentId, modelId);
            return new DocumentProcessingResult(
                new DocumentClassificationResult(Classify(request.OriginalFileName, analyzeResult), ClassificationConfidence(analyzeResult), "AzureDocumentIntelligence", DateTimeOffset.UtcNow),
                new DocumentExtractionResult(fields, confidences, "AzureDocumentIntelligence", DateTimeOffset.UtcNow, false, null, null),
                warnings);
        }
        catch (Exception ex) when (ex is RequestFailedException or InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(ex, "Azure Document Intelligence processing failed safely for document {DocumentId}.", request.DocumentId);
            return SafeMissingConfiguration(request, AzureProviderSupport.SafeError("Azure Document Intelligence", ex));
        }
    }

    private static (Dictionary<string, string> Fields, Dictionary<string, decimal> Confidences) BuildExtraction(
        DocumentProcessingRequest request,
        AnalyzeResult analyzeResult,
        string modelId)
    {
        var fields = new Dictionary<string, string>
        {
            ["sourceFileName"] = request.OriginalFileName,
            ["objectKey"] = request.ObjectKey,
            ["modelId"] = modelId,
            ["contentType"] = request.ContentType,
            ["pageCount"] = analyzeResult.Pages.Count.ToString()
        };
        var confidences = fields.Keys.ToDictionary(key => key, _ => 0.70m);

        foreach (var document in analyzeResult.Documents)
        {
            if (!string.IsNullOrWhiteSpace(document.DocumentType))
            {
                fields.TryAdd("azureDocumentType", document.DocumentType);
                confidences["azureDocumentType"] = DecimalConfidence(document.Confidence);
            }

            foreach (var field in document.Fields)
            {
                var value = field.Value.Content;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                fields[$"field.{field.Key}"] = value;
                confidences[$"field.{field.Key}"] = DecimalConfidence(field.Value.Confidence);
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
            confidences[key] = DecimalConfidence(pair.Confidence);
        }

        return (fields, confidences);
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
        var fields = new Dictionary<string, string>
        {
            ["sourceFileName"] = request.OriginalFileName,
            ["objectKey"] = request.ObjectKey
        };
        var warnings = new[]
        {
            new DocumentValidationWarning("document.provider.configuration_required", message, "error")
        };
        return new DocumentProcessingResult(
            new DocumentClassificationResult(DocumentType.Unknown, 0.05m, "AzureDocumentIntelligence", DateTimeOffset.UtcNow),
            new DocumentExtractionResult(fields, fields.Keys.ToDictionary(key => key, _ => 0.05m), "AzureDocumentIntelligence", DateTimeOffset.UtcNow, false, null, null),
            warnings);
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

        var normalized = fileName.ToLowerInvariant();
        if (normalized.Contains("claim", StringComparison.Ordinal)) return DocumentType.ClaimForm;
        if (normalized.Contains("police", StringComparison.Ordinal)) return DocumentType.PoliceAbstract;
        if (normalized.Contains("invoice", StringComparison.Ordinal)) return DocumentType.Invoice;
        if (normalized.Contains("medical", StringComparison.Ordinal)) return DocumentType.MedicalReport;
        if (normalized.Contains("photo", StringComparison.Ordinal)) return DocumentType.PhotoEvidence;
        if (normalized.Contains("id", StringComparison.Ordinal)) return DocumentType.CustomerId;
        return DocumentType.Unknown;
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

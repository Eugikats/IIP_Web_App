using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Azure;

/// <summary>
/// Maps only provider-reported document labels and confidence. Filenames and missing provider
/// confidence are not evidence of a document type or confidence score.
/// </summary>
public static class AzureDocumentIntelligenceMapping
{
    public static (DocumentType DocumentType, decimal Confidence) MapClassification(string? providerType, float? confidence)
    {
        var documentType = string.IsNullOrWhiteSpace(providerType)
            ? DocumentType.Unknown
            : providerType.Contains("invoice", StringComparison.OrdinalIgnoreCase)
                || providerType.Contains("receipt", StringComparison.OrdinalIgnoreCase)
                ? DocumentType.Invoice
                : providerType.Contains("medical", StringComparison.OrdinalIgnoreCase)
                    ? DocumentType.MedicalReport
                    : providerType.Contains("identity", StringComparison.OrdinalIgnoreCase)
                        ? DocumentType.CustomerId
                        : DocumentType.Unknown;

        return documentType == DocumentType.Unknown
            ? (DocumentType.Unknown, 0m)
            : (documentType, NormalizeConfidence(confidence));
    }

    public static decimal NormalizeConfidence(float? confidence) =>
        confidence is null or <= 0 ? 0m : Math.Clamp((decimal)confidence.Value, 0m, 1m);
}

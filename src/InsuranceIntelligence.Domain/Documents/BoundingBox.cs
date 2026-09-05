namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// Page-relative bounding box coordinates returned by Azure AI Document Intelligence.
/// Values are normalised fractions of the page dimensions (0–1).
/// </summary>
public sealed record BoundingBox(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height);

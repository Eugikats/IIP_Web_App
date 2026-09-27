namespace InsuranceIntelligence.Api.Documents;

/// <summary>
/// Shared upload limits for staff and client-portal document endpoints. Sized for
/// compressed field photos on low-bandwidth links; resumable/chunked upload remains deferred
/// (see docs/research/iip-pending-work-register.md).
/// </summary>
public static class DocumentUploadValidation
{
    public const long MaxUploadFileSizeBytes = 25 * 1024 * 1024;

    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff",
        ".txt",
        // A backstop for an iPhone camera photo Safari did not convert to JPEG (claim intake
        // build prompt outcome B). Stored as received like any other file; extraction is not
        // promised on these until verified to actually work against a real HEIC/HEIF sample.
        ".heic",
        ".heif",
    };

    public static string? Validate(long lengthBytes, string fileName)
    {
        if (lengthBytes <= 0)
        {
            return "Uploaded documents cannot be empty.";
        }

        if (lengthBytes > MaxUploadFileSizeBytes)
        {
            return "Uploaded documents cannot exceed 25 MB.";
        }

        var extension = Path.GetExtension(fileName);
        if (!SupportedExtensions.Contains(extension))
        {
            return "Unsupported file type. Supported file types are PDF, PNG, JPG, JPEG, TIF, TIFF, HEIC, HEIF, and TXT.";
        }

        return null;
    }
}

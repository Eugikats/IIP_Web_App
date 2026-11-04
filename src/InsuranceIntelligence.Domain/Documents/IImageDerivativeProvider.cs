namespace InsuranceIntelligence.Domain.Documents;

/// <summary>A smaller copy of a picture made for looking at: always a JPEG, upright, with the
/// location and camera details of the original removed.</summary>
public sealed record ImageDerivative(byte[] Content, string ContentType, int Width, int Height);

/// <summary>
/// Makes the derived copies of an uploaded image (build-loop item 11). The original is never
/// touched: it is stored byte for byte, and everything made here is a separate object. A derivative
/// is for people to look at (a thumbnail, a preview of a HEIC photo a browser cannot show), never
/// the evidence itself.
/// </summary>
public interface IImageDerivativeProvider
{
    /// <summary>A preview no larger than <paramref name="maxEdgePixels"/> on its longest side, or
    /// null when the bytes are not a picture this provider can read (a PDF, a damaged file).</summary>
    Task<ImageDerivative?> CreatePreviewAsync(Stream image, string fileName, int maxEdgePixels, CancellationToken cancellationToken = default);
}

using ImageMagick;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>
/// Makes previews with Magick.NET (Apache-2.0, runs in process, no network): reads JPEG, PNG, TIFF
/// and HEIC, turns the picture upright, limits its size, drops every profile and tag (so a preview
/// never carries the location or device of the original) and writes a JPEG. The input stream is
/// only read; the original bytes are stored elsewhere, untouched.
/// </summary>
public sealed class MagickImageDerivativeProvider(ILogger<MagickImageDerivativeProvider> logger) : IImageDerivativeProvider
{
    private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".tif", ".tiff", ".webp",
    };

    static MagickImageDerivativeProvider()
    {
        // A hostile or broken file must not exhaust memory or time while it is decoded.
        ResourceLimits.Width = 16_000;
        ResourceLimits.Height = 16_000;
        ResourceLimits.Area = 120_000_000;
        ResourceLimits.Memory = 512UL * 1024 * 1024;
        ResourceLimits.Time = 20;
    }

    public async Task<ImageDerivative?> CreatePreviewAsync(Stream image, string fileName, int maxEdgePixels, CancellationToken cancellationToken = default)
    {
        if (!PictureExtensions.Contains(Path.GetExtension(fileName)))
        {
            return null;
        }

        try
        {
            using var memory = new MemoryStream();
            await image.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;
            using var picture = new MagickImage(memory);
            picture.AutoOrient();
            if (picture.Width > maxEdgePixels || picture.Height > maxEdgePixels)
            {
                picture.Resize(new MagickGeometry((uint)maxEdgePixels, (uint)maxEdgePixels) { IgnoreAspectRatio = false });
            }

            picture.Strip();
            picture.Format = MagickFormat.Jpeg;
            picture.Quality = 80;
            var bytes = picture.ToByteArray();
            return new ImageDerivative(bytes, "image/jpeg", (int)picture.Width, (int)picture.Height);
        }
        catch (MagickException exception)
        {
            // Not a picture we can read: there is simply no preview. The original is unaffected.
            logger.LogInformation("No preview could be made for an uploaded image: {Reason}", exception.GetType().Name);
            return null;
        }
    }
}

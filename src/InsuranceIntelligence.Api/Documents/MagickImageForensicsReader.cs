using ImageMagick;
using ImageMagick.Formats;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>
/// Reads the facts for the authenticity checks with Magick.NET (Apache-2.0, in process): the EXIF
/// camera details, the software that last saved the file, the picture's real size, and a 64-bit
/// difference hash. The stream is only read.
/// </summary>
public sealed class MagickImageForensicsReader(ILogger<MagickImageForensicsReader> logger) : IImageForensicsReader
{
    private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".tif", ".tiff", ".webp",
    };

    public async Task<ImageForensics?> ReadAsync(Stream image, string fileName, CancellationToken cancellationToken = default)
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
            var exif = picture.GetExifProfile();

            string? Text(ExifTag<string> tag) => exif?.GetValue(tag)?.Value is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

            var captured = ParseExifDate(Text(ExifTag.DateTimeOriginal) ?? Text(ExifTag.DateTimeDigitized));
            var modified = ParseExifDate(Text(ExifTag.DateTime));
            int? exifWidth = exif?.GetValue(ExifTag.PixelXDimension)?.Value is { } x ? (int)x : null;
            int? exifHeight = exif?.GetValue(ExifTag.PixelYDimension)?.Value is { } y ? (int)y : null;

            return new ImageForensics(
                HasExif: exif is not null && exif.Values.Any(),
                CapturedAt: captured,
                ModifiedAt: modified,
                Make: Text(ExifTag.Make),
                Model: Text(ExifTag.Model),
                Software: Text(ExifTag.Software),
                GpsLatitude: Coordinate(exif, ExifTag.GPSLatitude, ExifTag.GPSLatitudeRef, "S"),
                GpsLongitude: Coordinate(exif, ExifTag.GPSLongitude, ExifTag.GPSLongitudeRef, "W"),
                Width: (int)picture.Width,
                Height: (int)picture.Height,
                ExifWidth: exifWidth,
                ExifHeight: exifHeight,
                PerceptualHash: DifferenceHash(picture));
        }
        catch (MagickException exception)
        {
            logger.LogInformation("The authenticity checks could not read a picture: {Reason}", exception.GetType().Name);
            return null;
        }
    }

    /// <summary>64-bit difference hash: shrink to 9 by 8 grey, one bit per pair of neighbours that gets
    /// brighter. Resizing or recompressing a picture barely changes it. A picture with no detail
    /// returns null, because every flat picture would otherwise "match" every other.</summary>
    public static string? DifferenceHash(MagickImage source)
    {
        using var small = (MagickImage)source.Clone();
        small.AutoOrient();
        small.Resize(new MagickGeometry(9, 8) { IgnoreAspectRatio = true });
        small.ColorSpace = ColorSpace.Gray;
        using var pixels = small.GetPixels();
        var rgb = pixels.ToByteArray(PixelMapping.RGB);
        if (rgb is null || rgb.Length < 72 * 3)
        {
            return null;
        }

        var grey = new byte[72];
        for (var i = 0; i < 72; i++)
        {
            grey[i] = (byte)((rgb[i * 3] * 299 + rgb[i * 3 + 1] * 587 + rgb[i * 3 + 2] * 114) / 1000);
        }

        ulong hash = 0;
        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                hash <<= 1;
                if (grey[row * 9 + column] < grey[row * 9 + column + 1])
                {
                    hash |= 1;
                }
            }
        }

        return InsuranceIntelligence.Domain.Documents.PerceptualHash.HasDetail(hash)
            ? InsuranceIntelligence.Domain.Documents.PerceptualHash.ToHex(hash)
            : null;
    }

    private static DateTimeOffset? ParseExifDate(string? text) =>
        text is not null && DateTime.TryParseExact(text, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var value)
            ? new DateTimeOffset(value, TimeSpan.Zero)
            : null;

    private static double? Coordinate(IExifProfile? exif, ExifTag<Rational[]> tag, ExifTag<string> reference, string negativeReference)
    {
        var parts = exif?.GetValue(tag)?.Value;
        if (parts is null || parts.Length < 3)
        {
            return null;
        }

        var degrees = parts[0].ToDouble() + (parts[1].ToDouble() / 60) + (parts[2].ToDouble() / 3600);
        var hemisphere = exif!.GetValue(reference)?.Value;
        return string.Equals(hemisphere?.Trim(), negativeReference, StringComparison.OrdinalIgnoreCase) ? -degrees : degrees;
    }
}

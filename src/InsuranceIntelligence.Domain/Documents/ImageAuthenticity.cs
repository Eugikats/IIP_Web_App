using System.Numerics;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>What can be read from a picture file without judging it: the camera details recorded in
/// it, whether it was saved by an editor, and a perceptual hash that stays almost the same when a
/// picture is resized or recompressed.</summary>
public sealed record ImageForensics(
    bool HasExif,
    DateTimeOffset? CapturedAt,
    DateTimeOffset? ModifiedAt,
    string? Make,
    string? Model,
    string? Software,
    double? GpsLatitude,
    double? GpsLongitude,
    int Width,
    int Height,
    int? ExifWidth,
    int? ExifHeight,
    string? PerceptualHash)
{
    public bool HasGps => GpsLatitude is not null && GpsLongitude is not null;
    public bool HasCameraDetails => !string.IsNullOrWhiteSpace(Make) || !string.IsNullOrWhiteSpace(Model);
}

public enum AuthenticitySeverity
{
    /// <summary>Worth knowing, not a concern by itself.</summary>
    Info,
    Low,
    Medium,
    High,
}

/// <summary>One thing noticed about a picture, with the reason in words a claims officer can check.
/// Never a verdict: a finding is a prompt for a person to look.</summary>
public sealed record AuthenticityFinding(
    string Code,
    AuthenticitySeverity Severity,
    string Reason,
    Guid? RelatedCaseId = null,
    Guid? RelatedDocumentId = null);

public enum AuthenticityDisposition
{
    NotReviewed,

    /// <summary>A person looked at the findings and is satisfied.</summary>
    NoConcern,

    /// <summary>A person wants something checked or asked before relying on the picture.</summary>
    NeedsFollowUp,

    /// <summary>A person believes the picture is not what it claims to be. Still not an accusation:
    /// it is the officer's reading, recorded and audited, and handled through the normal route.</summary>
    ConcernRaised,
}

/// <summary>
/// What the authenticity checks found for one stored picture (build-loop item 16), kept with the
/// person's disposition. Advisory only: it never blocks a claim or decides anything, and the
/// disposition is a human's. It holds findings and a few recorded facts, never the picture.
/// </summary>
public sealed record AuthenticityReport
{
    public required Guid DocumentId { get; init; }
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required IReadOnlyList<AuthenticityFinding> Findings { get; init; }
    public required DateTimeOffset AnalysedAt { get; init; }

    public bool HasExif { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? Software { get; init; }
    public bool HasGps { get; init; }

    /// <summary>64-bit difference hash as 16 hex characters; null when the picture carries too
    /// little detail for a hash to mean anything (a flat colour, a blank page).</summary>
    public string? PerceptualHash { get; init; }

    public AuthenticityDisposition Disposition { get; init; } = AuthenticityDisposition.NotReviewed;
    public string? DispositionNote { get; init; }
    public string? DispositionByUserId { get; init; }
    public DateTimeOffset? DispositionAt { get; init; }

    public AuthenticitySeverity? HighestSeverity => Findings.Count == 0 ? null : Findings.Max(finding => finding.Severity);

    /// <summary>Findings that a person should look at (Medium or High) and has not yet.</summary>
    public bool NeedsLook =>
        Disposition == AuthenticityDisposition.NotReviewed && Findings.Any(finding => finding.Severity >= AuthenticitySeverity.Medium);
}

public interface IAuthenticityReportRepository
{
    Task SaveAsync(AuthenticityReport report, CancellationToken cancellationToken = default);

    Task<AuthenticityReport?> GetAsync(TenantScope scope, Guid caseId, Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthenticityReport>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Every report in the workspace that has a perceptual hash, for finding a picture
    /// that has been used before. Never crosses a workspace.</summary>
    Task<IReadOnlyList<AuthenticityReport>> ListWithHashAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

/// <summary>Reads the facts from a picture's bytes. Never changes them.</summary>
public interface IImageForensicsReader
{
    /// <summary>Null when the bytes are not a picture this reader understands.</summary>
    Task<ImageForensics?> ReadAsync(Stream image, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>How alike two pictures are, from their perceptual hashes.</summary>
public static class PerceptualHash
{
    /// <summary>A hash this close to another (out of 64 bits) is treated as the same picture.</summary>
    public const int SamePictureDistance = 4;

    /// <summary>Fewer than this many bits set (or unset) means the picture has no detail to hash.</summary>
    public const int MinimumDetailBits = 6;

    public static int Distance(string a, string b) =>
        BitOperations.PopCount(Convert.ToUInt64(a, 16) ^ Convert.ToUInt64(b, 16));

    public static bool HasDetail(ulong hash)
    {
        var ones = BitOperations.PopCount(hash);
        return ones >= MinimumDetailBits && ones <= 64 - MinimumDetailBits;
    }

    public static string ToHex(ulong hash) => hash.ToString("x16");
}

/// <summary>
/// The authenticity rules (build-loop item 16): pure functions from what was read from a picture, the
/// claim's incident date and the pictures already on file to a list of findings. Each finding says
/// why. Nothing here decides; a person does.
/// </summary>
public static class AuthenticityRules
{
    private static readonly string[] EditingSoftware =
    [
        "photoshop", "gimp", "lightroom", "snapseed", "picsart", "facetune", "pixlr", "canva", "paint.net", "affinity", "photopea", "fotor", "stable diffusion", "midjourney", "dall-e", "firefly",
    ];

    /// <summary>A picture that is not a phone or camera photograph (a screenshot, a scan) is not
    /// expected to have camera details, so this only applies to evidence photos.</summary>
    public static IReadOnlyList<AuthenticityFinding> Evaluate(
        ImageForensics forensics,
        DocumentKind? slotKind,
        DateOnly incidentDate,
        DateTimeOffset claimReportedAt,
        IEnumerable<KnownPicture> otherPictures)
    {
        var findings = new List<AuthenticityFinding>();
        var evidence = slotKind == DocumentKind.Evidence;

        if (!forensics.HasExif)
        {
            if (evidence)
            {
                findings.Add(new AuthenticityFinding(
                    "no_metadata",
                    AuthenticitySeverity.Low,
                    "The file has no camera details (no time taken, no device). Messaging apps and editing tools remove them, so ask for the original photo if it matters."));
            }
        }
        else
        {
            if (forensics.CapturedAt is { } captured)
            {
                var taken = DateOnly.FromDateTime(captured.UtcDateTime);
                if (taken < incidentDate.AddDays(-1))
                {
                    var days = incidentDate.DayNumber - taken.DayNumber;
                    findings.Add(new AuthenticityFinding(
                        "captured_before_incident",
                        AuthenticitySeverity.High,
                        $"The photo says it was taken on {taken:d MMM yyyy}, {days} days before the incident on {incidentDate:d MMM yyyy}."));
                }
                else if (captured > claimReportedAt.AddDays(1))
                {
                    findings.Add(new AuthenticityFinding(
                        "captured_in_future",
                        AuthenticitySeverity.Medium,
                        $"The photo says it was taken on {taken:d MMM yyyy}, after the claim was reported. Check the date set on the camera."));
                }
            }
            else if (evidence)
            {
                findings.Add(new AuthenticityFinding("no_capture_time", AuthenticitySeverity.Low, "The file has camera details but no time the photo was taken."));
            }

            if (!string.IsNullOrWhiteSpace(forensics.Software)
                && EditingSoftware.FirstOrDefault(name => forensics.Software.Contains(name, StringComparison.OrdinalIgnoreCase)) is { } editor)
            {
                findings.Add(new AuthenticityFinding(
                    "edited_with_software",
                    AuthenticitySeverity.Medium,
                    $"The file says it was last saved by \"{forensics.Software.Trim()}\", which is picture-editing software."));
            }

            if (forensics.CapturedAt is { } original && forensics.ModifiedAt is { } modified && modified - original > TimeSpan.FromDays(1))
            {
                findings.Add(new AuthenticityFinding(
                    "modified_after_capture",
                    AuthenticitySeverity.Medium,
                    $"The file was changed on {modified:d MMM yyyy}, {(int)(modified - original).TotalDays} days after the photo was taken."));
            }

            if (forensics.ExifWidth is { } exifWidth && forensics.ExifHeight is { } exifHeight
                && (exifWidth != forensics.Width || exifHeight != forensics.Height))
            {
                findings.Add(new AuthenticityFinding(
                    "size_differs_from_camera",
                    AuthenticitySeverity.Medium,
                    $"The picture is {forensics.Width} by {forensics.Height}, but the camera recorded {exifWidth} by {exifHeight}, so it was resized or cropped after it was taken."));
            }

            if (evidence && !forensics.HasCameraDetails && string.IsNullOrWhiteSpace(forensics.Software))
            {
                findings.Add(new AuthenticityFinding("no_device", AuthenticitySeverity.Low, "The file does not say which phone or camera took it."));
            }

            if (forensics.HasGps)
            {
                findings.Add(new AuthenticityFinding(
                    "location_recorded",
                    AuthenticitySeverity.Info,
                    $"The photo records where it was taken ({forensics.GpsLatitude:0.####}, {forensics.GpsLongitude:0.####}). Compare it with where the customer says it happened."));
            }
        }

        if (forensics.PerceptualHash is { } hash)
        {
            foreach (var other in otherPictures)
            {
                if (other.PerceptualHash is null || PerceptualHash.Distance(hash, other.PerceptualHash) > PerceptualHash.SamePictureDistance)
                {
                    continue;
                }

                if (other.IsSameClaim)
                {
                    if (other.IsSameSlot)
                    {
                        continue;
                    }

                    findings.Add(new AuthenticityFinding(
                        "same_picture_in_claim",
                        AuthenticitySeverity.Medium,
                        "This looks like the same picture as another document on this claim.",
                        other.CaseId,
                        other.DocumentId));
                }
                else
                {
                    findings.Add(new AuthenticityFinding(
                        "picture_used_on_another_claim",
                        AuthenticitySeverity.High,
                        "This looks like the same picture as one sent for another claim.",
                        other.CaseId,
                        other.DocumentId));
                }
            }
        }

        return findings
            .GroupBy(finding => (finding.Code, finding.RelatedDocumentId))
            .Select(group => group.First())
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>A picture already on file that a new one is compared with.</summary>
public sealed record KnownPicture(Guid CaseId, Guid DocumentId, string? PerceptualHash, bool IsSameClaim, bool IsSameSlot);

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Application.Fraud;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.CaseReview;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Compliance;

public sealed record ClaimsPackFile(string Path, string Kind, string OriginalName, string NewName, string Type, string Sha256, DateTimeOffset? UploadedAt, long SizeBytes, int? Pages);

public sealed record ClaimsPackManifest(string ClaimReference, DateTimeOffset GeneratedAt, string GeneratedBy, IReadOnlyList<ClaimsPackFile> Files, IReadOnlyList<string> Notes);

public sealed record ClaimsPack(string FileName, byte[] Content, ClaimsPackManifest Manifest);

public interface IClaimsPackBuilder
{
    /// <summary>Null when the claim is not in this workspace. The caller has already checked the person may export.</summary>
    Task<ClaimsPack?> BuildAsync(TenantScope scope, Guid caseId, string actorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Puts a claim and everything held for it into one zip (build-loop item 24): a summary PDF that reads like a
/// file note, the customer's documents renamed after what they are, the untouched evidence, and a manifest that
/// ties every file to its SHA-256 so it can be shown that nothing changed. What the platform worked out (the
/// summary of the claim, the review score, the picture checks, the recommendation) is included but every such
/// part is labelled advisory: it is there to help a person and decides nothing. The JSON audit pack remains a
/// separate export.
/// </summary>
public sealed class ClaimsPackBuilder(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IDocumentChecklistProvider checklistProvider,
    IDocumentStorageProvider storageProvider,
    ISlotPdfRepository slotPdfRepository,
    IPolicyRepository policyRepository,
    IProductRepository productRepository,
    IClaimFieldService claimFieldService,
    ICaseReviewRepository caseReviewRepository,
    IClaimAssessmentService assessmentService,
    IAuthenticityService authenticityService,
    IAuditRepository auditRepository) : IClaimsPackBuilder
{
    public const string AdvisoryNotice = "ADVISORY: prepared by the platform to help a person. It does not decide, approve, reject or settle anything.";

    public async Task<ClaimsPack?> BuildAsync(TenantScope scope, Guid caseId, string actorId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return null;
        }

        var reference = ClaimReference.For(caseId);
        var now = DateTimeOffset.UtcNow;
        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).Where(document => !document.IsSuperseded).OrderBy(document => document.UploadedAt).ToArray();
        var checklist = await checklistProvider.BuildChecklistAsync(claim, documents, cancellationToken);

        var files = new List<(ClaimsPackFile Entry, byte[] Content)>();
        await AddDocumentsAsync(scope, reference, documents, checklist, files, cancellationToken);

        var summary = await BuildSummaryAsync(claim, reference, now, actorId, documents, checklist, files.Select(file => file.Entry).ToArray(), cancellationToken);
        var summaryName = $"00_ClaimSummary_{reference}.pdf";
        files.Insert(0, (new ClaimsPackFile(summaryName, "Summary", summaryName, summaryName, "Claim summary", Sha(summary), now, summary.Length, null), summary));

        var manifest = new ClaimsPackManifest(
            reference,
            now,
            actorId,
            files.Select(file => file.Entry).ToArray(),
            [
                "Documents are renamed after what they are. Evidence is exactly as the customer sent it.",
                "The SHA-256 of every file is listed so any file can be checked against what was received.",
                "Parts of the summary prepared by the platform are labelled advisory and decide nothing.",
            ]);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entry, content) in files)
            {
                var zipEntry = archive.CreateEntry(entry.Path, CompressionLevel.Optimal);
                zipEntry.LastWriteTime = entry.UploadedAt ?? now;
                await using var stream = zipEntry.Open();
                await stream.WriteAsync(content, cancellationToken);
            }

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }, cancellationToken);
        }

        return new ClaimsPack($"ClaimsPack_{reference}.zip", zipStream.ToArray(), manifest);
    }

    private async Task AddDocumentsAsync(
        TenantScope scope,
        string reference,
        IReadOnlyList<CaseDocument> documents,
        DocumentCompletenessChecklist checklist,
        List<(ClaimsPackFile Entry, byte[] Content)> files,
        CancellationToken cancellationToken)
    {
        var labels = checklist.Slots.ToDictionary(slot => slot.Key, slot => slot.Label, StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string Unique(string folder, string name)
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var extension = Path.GetExtension(name);
            var candidate = $"{folder}/{name}";
            for (var number = 2; !used.Add(candidate); number++)
            {
                candidate = $"{folder}/{stem}_{number}{extension}";
            }

            return candidate;
        }

        foreach (var slotGroup in documents.Where(document => document.SlotKind != DocumentKind.Evidence).GroupBy(document => document.EffectiveKey, StringComparer.OrdinalIgnoreCase))
        {
            var label = labels.TryGetValue(slotGroup.Key, out var known) ? known : slotGroup.Key;
            var pages = SlotPdfService.PagesInOrder(slotGroup.ToArray(), slotGroup.Key);
            var slotPdf = pages.Count > 0 ? await slotPdfRepository.GetAsync(scope, slotGroup.First().CaseId, slotGroup.Key, cancellationToken) : null;
            if (slotPdf is not null && pages.Count > 0)
            {
                byte[] bytes;
                await using (var stream = await storageProvider.ReadAsync(scope, slotPdf.ObjectKey, cancellationToken))
                {
                    bytes = await ReadAllAsync(stream, cancellationToken);
                }

                var name = $"{DocumentNaming.Compact(label)}_{reference}.pdf";
                var path = Unique("01_Documents", name);
                files.Add((new ClaimsPackFile(path, "Document", string.Join("; ", pages.Select(page => page.OriginalFileName)), Path.GetFileName(path), label, Sha(bytes), pages.Max(page => page.UploadedAt), bytes.Length, slotPdf.PageCount), bytes));
                continue;
            }

            var ordinal = 0;
            foreach (var document in slotGroup)
            {
                ordinal++;
                var name = document.DisplayName ?? DocumentNaming.DisplayName(label, reference, ordinal, document.OriginalFileName);
                await AddOriginalAsync(scope, document, Unique("01_Documents", name), label, "Document", files, cancellationToken);
            }
        }

        foreach (var document in documents.Where(document => document.SlotKind == DocumentKind.Evidence))
        {
            var label = labels.TryGetValue(document.EffectiveKey, out var known) ? known : "Evidence";
            var name = document.DisplayName ?? document.OriginalFileName;
            await AddOriginalAsync(scope, document, Unique("02_Evidence", name), label, "Evidence", files, cancellationToken);
        }
    }

    private async Task AddOriginalAsync(
        TenantScope scope, CaseDocument document, string path, string label, string kind,
        List<(ClaimsPackFile Entry, byte[] Content)> files, CancellationToken cancellationToken)
    {
        byte[] bytes;
        await using (var stream = await storageProvider.ReadAsync(scope, document.ObjectKey, cancellationToken))
        {
            bytes = await ReadAllAsync(stream, cancellationToken);
        }

        files.Add((new ClaimsPackFile(path, kind, document.OriginalFileName, Path.GetFileName(path), label, document.Sha256 ?? Sha(bytes), document.UploadedAt, bytes.Length, null), bytes));
    }

    private async Task<byte[]> BuildSummaryAsync(
        ClaimCase claim,
        string reference,
        DateTimeOffset now,
        string actorId,
        IReadOnlyList<CaseDocument> documents,
        DocumentCompletenessChecklist checklist,
        IReadOnlyList<ClaimsPackFile> packedFiles,
        CancellationToken cancellationToken)
    {
        var pdf = new SimplePdfDocument($"Claim {reference}  |  Contains personal information, handle with care");
        pdf.Title($"Claim summary {reference}");
        pdf.Paragraph($"Prepared {now:d MMMM yyyy, HH:mm} UTC by {actorId}. This is a copy of what the platform holds for the claim.", small: true);

        pdf.Heading("The claim");
        pdf.Row("Status", ClaimStatusLabels.For(claim.Status));
        pdf.Row("Reported", $"{claim.CreatedAt:d MMMM yyyy} through {claim.Channel}");
        pdf.Row("Date of incident", claim.IncidentDate.ToString("d MMMM yyyy"));
        pdf.Row("What happened", string.IsNullOrWhiteSpace(claim.IncidentDescription) ? "Not given" : claim.IncidentDescription);
        pdf.Row("Handled by", string.IsNullOrWhiteSpace(claim.AssignedUserId) ? "Nobody yet" : claim.AssignedUserId);
        pdf.Row("Completeness", $"{claim.CompletenessScore:0}% of the documents asked for");

        pdf.Heading("The customer");
        pdf.Row("Name", claim.CustomerName);
        pdf.Row("Phone", string.IsNullOrWhiteSpace(claim.CustomerPhone) ? "Not given" : claim.CustomerPhone);
        pdf.Row("Email", string.IsNullOrWhiteSpace(claim.CustomerEmail) ? "Not given" : claim.CustomerEmail);

        var policy = await policyRepository.GetByPolicyNumberAsync(claim.PolicyNumber, claim.Scope, cancellationToken);
        pdf.Heading("The policy");
        pdf.Row("Policy number", claim.PolicyNumber);
        if (policy is null)
        {
            pdf.Paragraph("No policy record for this number is held in this workspace.");
        }
        else
        {
            pdf.Row("Product line", policy.ProductLine);
            if (!string.IsNullOrWhiteSpace(policy.CoverType)) pdf.Row("Cover", policy.CoverType!);
            if (policy.EffectiveFrom is { } from) pdf.Row("Cover from", from.ToString("d MMMM yyyy"));
            if (policy.EffectiveTo is { } to) pdf.Row("Cover to", to.ToString("d MMMM yyyy"));
            if (policy.SumInsured is { } insured) pdf.Row("Sum insured", $"{policy.PremiumCurrency ?? "UGX"} {insured:N0}");
        }

        pdf.Heading("Product and kind of claim");
        string? productName = null;
        if (claim.ProductId is { } productId && claim.ProductVersion is { } version)
        {
            var product = await productRepository.GetVersionAsync(claim.Scope, productId, version, cancellationToken);
            productName = product is null ? null : $"{product.Name} (version {product.Version})";
        }

        pdf.Row("Product", productName ?? claim.ProductLine);
        pdf.Row("Kind of claim", claim.ClaimType);

        var fields = await claimFieldService.GetForClaimAsync(claim, customerAudience: false, cancellationToken);
        pdf.Heading("Details given");
        if (fields.Fields.Count == 0)
        {
            pdf.Paragraph("No questions are set up for this kind of claim.");
        }
        else
        {
            foreach (var field in fields.Fields.OrderBy(field => field.SortOrder))
            {
                var value = string.IsNullOrWhiteSpace(field.Value) ? "Not given" : field.Value!;
                var source = field.Source switch
                {
                    "Document" => " (read from a document" + (field.SourcePage is { } page ? $", page {page}" : string.Empty) + (field.ConfirmedBy is { } by ? $", confirmed by {by}" : string.Empty) + ")",
                    "Staff" => field.ConfirmedBy is { } staff ? $" (entered by {staff})" : " (entered by staff)",
                    "Customer" => " (given by the customer)",
                    _ => string.Empty,
                };
                pdf.Row(field.Label, value + source);
            }
        }

        pdf.Heading("Documents asked for");
        foreach (var slot in checklist.Slots)
        {
            var held = documents.Where(document => string.Equals(document.EffectiveKey, slot.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            var status = held.Length == 0
                ? "Missing"
                : held.Any(document => document.ProcessingStatus == DocumentProcessingStatus.Failed) ? "Received, could not be read"
                : held.Any(document => document.ProcessingStatus is DocumentProcessingStatus.Uploaded or DocumentProcessingStatus.Processing) ? "Received, being checked"
                : "Received";
            var names = held.Length == 0 ? string.Empty : "  Files: " + string.Join(", ", held.Select(document => document.OriginalFileName));
            pdf.Bullet($"{slot.Label} ({(slot.Required ? "required" : "optional")}): {status}.{names}");
        }

        pdf.Heading("Files in this pack");
        foreach (var file in packedFiles)
        {
            pdf.Bullet($"{file.Path}  (SHA-256 {file.Sha256[..16]}...)");
        }

        pdf.Heading("What happened, in order");
        var events = (await auditRepository.ListForCaseAsync(claim.CaseId, cancellationToken))
            .Where(item => item.Scope == claim.Scope)
            .OrderBy(item => item.Timestamp)
            .TakeLast(80)
            .ToArray();
        if (events.Length == 0)
        {
            pdf.Paragraph("Nothing has been recorded yet.");
        }
        else
        {
            foreach (var item in events)
            {
                var change = item.BeforeStatus is not null && item.AfterStatus is not null && item.ActionName.Contains("status", StringComparison.OrdinalIgnoreCase)
                    ? $": {ClaimStatusLabels.For(item.BeforeStatus)} to {ClaimStatusLabels.For(item.AfterStatus)}"
                    : string.Empty;
                pdf.Bullet($"{item.Timestamp:d MMM yyyy HH:mm}  {Humanise(item.ActionName)}{change}  ({item.UserId ?? "system"})");
            }
        }

        await AddPreparedByPlatformAsync(pdf, claim, cancellationToken);
        return pdf.ToBytes();
    }

    private async Task AddPreparedByPlatformAsync(SimplePdfDocument pdf, ClaimCase claim, CancellationToken cancellationToken)
    {
        var review = await caseReviewRepository.GetLatestForCaseAsync(claim.CaseId, claim.Scope, cancellationToken);
        pdf.Heading("Summary of the claim");
        pdf.Strong(AdvisoryNotice);
        if (review is null)
        {
            pdf.Paragraph("No summary has been prepared for this claim.");
        }
        else
        {
            pdf.Paragraph(review.Overview);
            if (review.OpenQuestions.Count > 0)
            {
                pdf.Strong("Open questions");
                foreach (var question in review.OpenQuestions) pdf.Bullet(question);
            }

            if (review.RiskSignals.Count > 0)
            {
                pdf.Strong("Things the model suggested a person may want to check");
                foreach (var signal in review.RiskSignals) pdf.Bullet($"{(signal.Category == CaseReviewSignalCategory.Fraud ? "Possible concern" : "Underwriting point")}: {signal.Note}");
            }

            pdf.Paragraph($"Prepared {review.GeneratedAt:d MMM yyyy HH:mm} UTC by {review.GeneratedByProvider}.", small: true);
        }

        var assessment = await assessmentService.GetAsync(claim.Scope, claim.CaseId, cancellationToken);
        pdf.Heading("Review score and recommendation");
        pdf.Strong(AdvisoryNotice);
        if (assessment is null)
        {
            pdf.Paragraph("No review score is available for this claim.");
        }
        else
        {
            pdf.Row("Review score", $"{assessment.Score.Value} out of 100 ({assessment.Score.BandLabel}). How much a person should look at the claim; not a chance that anyone has done wrong");
            if (assessment.Score.Factors.Count == 0)
            {
                pdf.Paragraph("Nothing was flagged.");
            }
            else
            {
                foreach (var factor in assessment.Score.Factors)
                {
                    pdf.Bullet($"{factor.SourceLabel}, {factor.Strength.ToLowerInvariant()}: {factor.Reason} ({factor.ReadingLabel}{(factor.Counts ? string.Empty : ", no longer counted")})");
                }
            }

            pdf.Row("Recommended next step", $"{assessment.Recommendation.Label}. {assessment.Recommendation.Headline}");
            foreach (var reason in assessment.Recommendation.Reasons) pdf.Bullet($"{reason.Text} (source: {reason.Source.Label})");
            foreach (var request in assessment.Recommendation.SuggestedRequests) pdf.Bullet(request);
            if (!string.IsNullOrWhiteSpace(assessment.Narrative)) pdf.Paragraph(assessment.Narrative);
            pdf.Row("Response by a person", assessment.Response is null ? "None recorded" : $"{assessment.Response.Kind} by {assessment.Response.ByUserId}{(string.IsNullOrWhiteSpace(assessment.Response.Note) ? string.Empty : ": " + assessment.Response.Note)}");
        }

        var pictures = await authenticityService.ListAsync(claim.Scope, claim.CaseId, cancellationToken);
        pdf.Heading("Checks on the pictures");
        pdf.Strong(AdvisoryNotice);
        if (pictures.Count == 0)
        {
            pdf.Paragraph("No pictures were checked.");
        }
        else
        {
            foreach (var picture in pictures)
            {
                pdf.Strong(picture.DocumentName ?? picture.OriginalFileName);
                if (picture.Findings.Count == 0) pdf.Bullet("Nothing unusual was noticed.");
                foreach (var finding in picture.Findings) pdf.Bullet($"{finding.Severity}: {finding.Reason}");
                pdf.Bullet(picture.Disposition == "NotReviewed" ? "Not looked at by a person yet." : $"A person's reading: {picture.Disposition}{(string.IsNullOrWhiteSpace(picture.DispositionNote) ? string.Empty : " (" + picture.DispositionNote + ")")}.");
            }
        }
    }

    private static string Humanise(string action)
    {
        var text = action.Replace('.', ' ').Replace('_', ' ').Trim();
        return text.Length == 0 ? "Event" : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Sha(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}

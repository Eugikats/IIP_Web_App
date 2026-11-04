using System.Security.Cryptography;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.Documents;

public interface ISlotPdfService
{
    /// <summary>Rebuilds the PDF of a document slot from the pictures it holds now, or removes it
    /// when the slot is empty or holds anything that is not a picture (a PDF, a spreadsheet). Never
    /// throws for a picture it cannot read: the originals are what matter, the PDF is a convenience.</summary>
    Task<SlotPdf?> RebuildAsync(TenantScope scope, Guid caseId, string slotKey, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps one PDF per document slot (build-loop item 12). A slot's pictures, in the customer's page
/// order, become the pages. Originals are read and never written. A slot that already holds a PDF,
/// or any file that is not a picture, gets no combined PDF: the files stay as they were sent.
/// </summary>
public sealed class SlotPdfService(
    ICaseDocumentRepository documentRepository,
    ISlotPdfRepository slotPdfRepository,
    IDocumentStorageProvider storageProvider,
    IDocumentPdfBuilder pdfBuilder,
    IAuditRepository auditRepository) : ISlotPdfService
{
    private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".tif", ".tiff", ".webp",
    };

    /// <summary>Pages of a slot in the order they are shown: the order the customer arranged, and
    /// where none was set, the order they were sent.</summary>
    public static IReadOnlyList<CaseDocument> PagesInOrder(IEnumerable<CaseDocument> documents, string slotKey) =>
        documents
            .Where(document => !document.IsSuperseded && string.Equals(document.EffectiveKey, slotKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(document => document.PageOrder ?? int.MaxValue)
            .ThenBy(document => document.UploadedAt)
            .ToArray();

    public async Task<SlotPdf?> RebuildAsync(TenantScope scope, Guid caseId, string slotKey, string correlationId, CancellationToken cancellationToken = default)
    {
        var pages = PagesInOrder(await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken), slotKey);
        var onlyPictures = pages.Count > 0 && pages.All(page => PictureExtensions.Contains(Path.GetExtension(page.OriginalFileName)));
        if (!onlyPictures)
        {
            await slotPdfRepository.RemoveAsync(scope, caseId, slotKey, cancellationToken);
            return null;
        }

        var streams = new List<(Stream Picture, string FileName)>();
        try
        {
            foreach (var page in pages)
            {
                streams.Add((await storageProvider.ReadAsync(scope, page.ObjectKey, cancellationToken), page.OriginalFileName));
            }

            var built = await pdfBuilder.BuildAsync(streams, cancellationToken);
            if (built is null)
            {
                await slotPdfRepository.RemoveAsync(scope, caseId, slotKey, cancellationToken);
                return null;
            }

            string objectKey;
            await using (var content = new MemoryStream(built.Content))
            {
                objectKey = await storageProvider.StoreDerivativeAsync(
                    scope, caseId, Guid.NewGuid(), $"{SafeName(slotKey)}.pdf", "application/pdf", content, cancellationToken);
            }

            var pdf = new SlotPdf
            {
                CaseId = caseId,
                Scope = scope,
                SlotKey = slotKey,
                ObjectKey = objectKey,
                Sha256 = Convert.ToHexString(SHA256.HashData(built.Content)).ToLowerInvariant(),
                SizeBytes = built.Content.Length,
                PageDocumentIds = pages.Select(page => page.DocumentId).ToArray(),
                BuiltAt = DateTimeOffset.UtcNow,
            };
            await slotPdfRepository.SaveAsync(pdf, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.SlotPdfBuilt(scope, caseId, slotKey, pdf.PageCount, correlationId), cancellationToken);
            return pdf;
        }
        catch (NotSupportedException)
        {
            // This storage keeps no derived copies; there is simply no combined PDF.
            return null;
        }
        finally
        {
            foreach (var (picture, _) in streams)
            {
                await picture.DisposeAsync();
            }
        }
    }

    private static string SafeName(string value) =>
        new string(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
}

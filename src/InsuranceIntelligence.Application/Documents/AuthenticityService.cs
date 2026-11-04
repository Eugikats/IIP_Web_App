using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.Documents;

public sealed record AuthenticityFindingDto(
    string Code,
    string Severity,
    string Reason,
    string? RelatedClaimReference,
    bool RelatedIsThisClaim);

public sealed record AuthenticityReportDto(
    Guid DocumentId,
    string? DocumentName,
    string OriginalFileName,
    string? SlotKind,
    IReadOnlyList<AuthenticityFindingDto> Findings,
    bool HasExif,
    DateTimeOffset? CapturedAt,
    string? Make,
    string? Model,
    string? Software,
    bool HasGps,
    string Disposition,
    string? DispositionNote,
    string? DispositionByUserId,
    DateTimeOffset? DispositionAt,
    bool NeedsLook,
    string? HighestSeverity,
    DateTimeOffset AnalysedAt);

public enum AuthenticityOutcome
{
    Done,
    NotFound,
    Invalid,
}

public sealed record AuthenticityResult(AuthenticityOutcome Outcome, string? Message = null, AuthenticityReportDto? Report = null)
{
    public bool Ok => Outcome == AuthenticityOutcome.Done;
}

public interface IAuthenticityService
{
    /// <summary>Looks at a stored picture and records what was noticed. Returns null for anything
    /// that is not a picture. Never changes the picture and never blocks the claim.</summary>
    Task<AuthenticityReport?> AnalyseAsync(CaseDocument document, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthenticityReportDto>> ListAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>A person's reading of the findings. Anything other than "no concern" needs a note.</summary>
    Task<AuthenticityResult> SetDispositionAsync(TenantScope scope, Guid caseId, Guid documentId, string? disposition, string? note, string actorId, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Authenticity checks (build-loop item 16): for each picture, the camera details it carries, signs
/// it was edited, and whether the same picture appears elsewhere in the workspace. The result is a
/// list of reasons for a person to look, kept with that person's disposition and an audit trail. It
/// is advisory: it never blocks a claim, scores a customer or accuses anyone, and it never leaves
/// the workspace (duplicates are searched only among the same workspace's own pictures).
/// </summary>
public sealed class AuthenticityService(
    IImageForensicsReader forensicsReader,
    IAuthenticityReportRepository reportRepository,
    ICaseDocumentRepository documentRepository,
    ICaseRepository caseRepository,
    IDocumentStorageProvider storageProvider,
    IAuditRepository auditRepository) : IAuthenticityService
{
    public async Task<AuthenticityReport?> AnalyseAsync(CaseDocument document, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken = default)
    {
        if (document.IsSuperseded)
        {
            return null;
        }

        var scope = claimCase.Scope;
        ImageForensics? forensics;
        await using (var stream = await storageProvider.ReadAsync(scope, document.ObjectKey, cancellationToken))
        {
            forensics = await forensicsReader.ReadAsync(stream, document.OriginalFileName, cancellationToken);
        }

        if (forensics is null)
        {
            return null;
        }

        var sameClaimDocuments = (await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken))
            .Where(other => other.DocumentId != document.DocumentId && !other.IsSuperseded)
            .ToDictionary(other => other.DocumentId);
        var known = new List<KnownPicture>();
        if (forensics.PerceptualHash is not null)
        {
            foreach (var other in await reportRepository.ListWithHashAsync(scope, cancellationToken))
            {
                if (other.DocumentId == document.DocumentId)
                {
                    continue;
                }

                var sameClaim = other.CaseId == claimCase.CaseId;
                if (sameClaim && !sameClaimDocuments.ContainsKey(other.DocumentId))
                {
                    continue;
                }

                known.Add(new KnownPicture(
                    other.CaseId,
                    other.DocumentId,
                    other.PerceptualHash,
                    sameClaim,
                    sameClaim && string.Equals(sameClaimDocuments[other.DocumentId].EffectiveKey, document.EffectiveKey, StringComparison.OrdinalIgnoreCase)));
            }
        }

        var findings = AuthenticityRules.Evaluate(forensics, document.SlotKind, claimCase.IncidentDate, claimCase.CreatedAt, known);
        var existing = await reportRepository.GetAsync(scope, claimCase.CaseId, document.DocumentId, cancellationToken);
        var report = new AuthenticityReport
        {
            DocumentId = document.DocumentId,
            CaseId = claimCase.CaseId,
            Scope = scope,
            Findings = findings,
            AnalysedAt = DateTimeOffset.UtcNow,
            HasExif = forensics.HasExif,
            CapturedAt = forensics.CapturedAt,
            Make = forensics.Make,
            Model = forensics.Model,
            Software = forensics.Software,
            HasGps = forensics.HasGps,
            PerceptualHash = forensics.PerceptualHash,
            // A person's earlier reading is never wiped by looking at the picture again.
            Disposition = existing?.Disposition ?? AuthenticityDisposition.NotReviewed,
            DispositionNote = existing?.DispositionNote,
            DispositionByUserId = existing?.DispositionByUserId,
            DispositionAt = existing?.DispositionAt,
        };
        await reportRepository.SaveAsync(report, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.AuthenticityAnalysed(document, findings.Select(finding => finding.Code).ToArray(), correlationId),
            cancellationToken);
        return report;
    }

    public async Task<IReadOnlyList<AuthenticityReportDto>> ListAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return [];
        }

        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).ToDictionary(document => document.DocumentId);
        return (await reportRepository.ListForCaseAsync(scope, caseId, cancellationToken))
            .Where(report => documents.TryGetValue(report.DocumentId, out var document) && !document.IsSuperseded)
            .OrderByDescending(report => report.HighestSeverity ?? AuthenticitySeverity.Info)
            .ThenBy(report => documents[report.DocumentId].UploadedAt)
            .Select(report => ToDto(report, documents[report.DocumentId], caseId))
            .ToArray();
    }

    public async Task<AuthenticityResult> SetDispositionAsync(TenantScope scope, Guid caseId, Guid documentId, string? disposition, string? note, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        var report = claim is null || claim.Scope != scope ? null : await reportRepository.GetAsync(scope, caseId, documentId, cancellationToken);
        var document = report is null ? null : await documentRepository.GetAsync(caseId, documentId, scope, cancellationToken);
        if (report is null || document is null)
        {
            return new AuthenticityResult(AuthenticityOutcome.NotFound, "No authenticity check was found for that document.");
        }

        if (!Enum.TryParse<AuthenticityDisposition>(disposition?.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || parsed == AuthenticityDisposition.NotReviewed)
        {
            return new AuthenticityResult(AuthenticityOutcome.Invalid, "Choose No concern, Needs follow-up or Concern raised.");
        }

        var trimmed = note?.Trim();
        if (parsed != AuthenticityDisposition.NoConcern && string.IsNullOrWhiteSpace(trimmed))
        {
            return new AuthenticityResult(AuthenticityOutcome.Invalid, "Say in a sentence what you saw or what needs checking.");
        }

        if (trimmed is { Length: > 1000 })
        {
            return new AuthenticityResult(AuthenticityOutcome.Invalid, "The note can be at most 1,000 characters.");
        }

        var updated = report with
        {
            Disposition = parsed,
            DispositionNote = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            DispositionByUserId = actorId,
            DispositionAt = DateTimeOffset.UtcNow,
        };
        await reportRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.AuthenticityDisposition(document, actorId, parsed.ToString(), correlationId), cancellationToken);
        return new AuthenticityResult(AuthenticityOutcome.Done, null, ToDto(updated, document, caseId));
    }

    private static AuthenticityReportDto ToDto(AuthenticityReport report, CaseDocument document, Guid caseId) =>
        new(
            report.DocumentId,
            document.DisplayName,
            document.OriginalFileName,
            document.SlotKind?.ToString(),
            report.Findings
                .Select(finding => new AuthenticityFindingDto(
                    finding.Code,
                    finding.Severity.ToString(),
                    finding.Reason,
                    finding.RelatedCaseId is { } related ? ClaimReference.For(related) : null,
                    finding.RelatedCaseId == caseId))
                .ToArray(),
            report.HasExif,
            report.CapturedAt,
            report.Make,
            report.Model,
            report.Software,
            report.HasGps,
            report.Disposition.ToString(),
            report.DispositionNote,
            report.DispositionByUserId,
            report.DispositionAt,
            report.NeedsLook,
            report.HighestSeverity?.ToString(),
            report.AnalysedAt);
}

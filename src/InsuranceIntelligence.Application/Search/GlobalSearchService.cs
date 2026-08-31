using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Search;

public sealed class GlobalSearchService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IWorkflowTaskRepository taskRepository,
    IApprovalRepository approvalRepository,
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAuditWriter auditWriter) : IGlobalSearchService
{
    public async Task<GlobalSearchResponse> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2)
        {
            throw new ArgumentException("Search query must contain at least two characters.", nameof(query));
        }

        if (limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Search limit must be between 1 and 50.");
        }

        var canViewAllClaims = await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken);
        var canViewAssignedClaims = await permissionService.HasPermissionAsync(Permission.ViewAssignedClaims, cancellationToken);
        if (!canViewAllClaims && !canViewAssignedClaims)
        {
            throw new PermissionDeniedException(Permission.ViewAssignedClaims);
        }

        var context = tenantContextAccessor.Current;
        var scopedCases = await caseRepository.ListAsync(context.Scope, cancellationToken);
        var visibleCases = scopedCases
            .Where(claimCase => canViewAllClaims || IsAssignedToUser(claimCase, context.UserId))
            .ToArray();
        var visibleCaseIds = visibleCases.Select(claimCase => claimCase.CaseId).ToHashSet();
        var candidates = new List<SearchCandidate>();

        foreach (var claimCase in visibleCases)
        {
            AddCaseCandidate(candidates, claimCase, normalizedQuery);
        }

        // Repositories share the scoped EF DbContext in protected runtimes, so these reads
        // must not run concurrently. Replace this bounded baseline with one scoped query
        // before production volume makes per-case reads material.
        foreach (var claimCase in visibleCases)
        {
            var documents = await documentRepository.ListForCaseAsync(
                claimCase.CaseId,
                context.Scope,
                cancellationToken);
            foreach (var document in documents)
            {
                AddDocumentCandidate(candidates, document, normalizedQuery);
            }
        }

        var tasks = await taskRepository.ListAsync(context.Scope, cancellationToken);
        foreach (var task in tasks.Where(task => visibleCaseIds.Contains(task.CaseId)))
        {
            AddTaskCandidate(candidates, task, normalizedQuery);
        }

        if (await permissionService.HasPermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken))
        {
            var approvals = await approvalRepository.ListAsync(context.Scope, cancellationToken);
            foreach (var approval in approvals.Where(approval => visibleCaseIds.Contains(approval.CaseId)))
            {
                AddApprovalCandidate(candidates, approval, normalizedQuery);
            }
        }

        var ordered = candidates
            .OrderByDescending(candidate => candidate.MatchTier)
            .ThenByDescending(candidate => candidate.Urgency)
            .ThenByDescending(candidate => candidate.Result.UpdatedAt)
            .ThenBy(candidate => candidate.Result.Type)
            .ThenBy(candidate => candidate.Result.Id, StringComparer.Ordinal)
            .ToArray();
        var results = ordered.Take(limit).Select(candidate => candidate.Result).ToArray();
        var hasMore = ordered.Length > limit;

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                context.Scope,
                CaseId: null,
                DocumentId: null,
                UserId: context.UserId,
                ActorType.User,
                "global-search.executed",
                DateTimeOffset.UtcNow,
                BeforeStatus: null,
                AfterStatus: null,
                EvidenceIds: results.Select(result => $"{result.Type}:{result.Id}").ToArray(),
                ModelProvider: "AuthoritativeRepositorySearch",
                Confidence: null,
                ValidationResult: $"authorized-results:{results.Length};has-more:{hasMore}",
                ApprovalId: null,
                AuditReason: "Permission-filtered global search executed; raw query omitted.",
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

        return new GlobalSearchResponse(normalizedQuery, results.Length, hasMore, results);
    }

    private static bool IsAssignedToUser(ClaimCase claimCase, string userId) =>
        MatchesIdentity(claimCase.AssignedUserId, userId)
        || MatchesIdentity(claimCase.CreatedByUserId, userId);

    private static bool MatchesIdentity(string? candidate, string userId) =>
        !string.IsNullOrWhiteSpace(candidate)
        && string.Equals(candidate.Trim(), userId.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void AddCaseCandidate(List<SearchCandidate> candidates, ClaimCase claimCase, string query)
    {
        var match = BestMatch(query,
            (claimCase.CaseId.ToString(), "Exact case reference", true),
            (claimCase.PolicyNumber, "Policy reference", true),
            (claimCase.MotorFnolDetails?.VehicleRegistration, "Vehicle reference", true),
            (claimCase.CustomerName, "Customer name", false),
            (claimCase.ProductLine, "Product line", false),
            (claimCase.ClaimType, "Claim type", false),
            (claimCase.Status.ToString(), "Case status", false));
        if (match is null)
        {
            return;
        }

        candidates.Add(new SearchCandidate(
            match.Value.Tier,
            (int)claimCase.Priority,
            new GlobalSearchResultDto(
                GlobalSearchResultType.Case,
                claimCase.CaseId.ToString(),
                claimCase.CaseId.ToString(),
                $"Case {ShortReference(claimCase.CaseId)}",
                $"{claimCase.CustomerName} · {claimCase.PolicyNumber} · {claimCase.Status}",
                match.Value.Reason,
                $"/app/claims/{claimCase.CaseId}",
                claimCase.UpdatedAt)));
    }

    private static void AddDocumentCandidate(List<SearchCandidate> candidates, CaseDocument document, string query)
    {
        var match = BestMatch(query,
            (document.DocumentId.ToString(), "Exact document reference", true),
            (document.OriginalFileName, "File name", false),
            (document.DocumentType.ToString(), "Document type", false),
            (document.ProcessingStatus.ToString(), "Processing status", false));
        if (match is null)
        {
            return;
        }

        candidates.Add(new SearchCandidate(
            match.Value.Tier,
            document.ValidationWarnings.Count > 0 ? 2 : 1,
            new GlobalSearchResultDto(
                GlobalSearchResultType.Document,
                document.DocumentId.ToString(),
                document.CaseId.ToString(),
                document.OriginalFileName,
                $"{document.DocumentType} · {document.ProcessingStatus}",
                match.Value.Reason,
                $"/app/claims/{document.CaseId}/extraction?documentId={document.DocumentId}",
                document.ProcessedAt ?? document.UploadedAt)));
    }

    private static void AddTaskCandidate(List<SearchCandidate> candidates, WorkflowTask task, string query)
    {
        var match = BestMatch(query,
            (task.TaskId.ToString(), "Exact task reference", true),
            (task.Title, "Task title", false),
            (task.TaskType.ToString(), "Task type", false),
            (task.Status.ToString(), "Task status", false));
        if (match is null)
        {
            return;
        }

        var urgency = task.Status switch
        {
            WorkflowTaskStatus.Open => 3,
            WorkflowTaskStatus.InProgress => 2,
            _ => 0
        };
        candidates.Add(new SearchCandidate(
            match.Value.Tier,
            urgency,
            new GlobalSearchResultDto(
                GlobalSearchResultType.WorkflowTask,
                task.TaskId.ToString(),
                task.CaseId.ToString(),
                task.Title,
                $"{task.TaskType} · {task.Status}",
                match.Value.Reason,
                $"/app/work-items?taskId={task.TaskId}",
                task.UpdatedAt)));
    }

    private static void AddApprovalCandidate(List<SearchCandidate> candidates, ApprovalRequest approval, string query)
    {
        var match = BestMatch(query,
            (approval.ApprovalId.ToString(), "Exact approval reference", true),
            (approval.ApprovalType, "Approval type", false),
            (approval.Status.ToString(), "Approval status", false));
        if (match is null)
        {
            return;
        }

        var urgency = approval.Status switch
        {
            ApprovalStatus.Escalated => 4,
            ApprovalStatus.Pending => 3,
            _ => 0
        };
        candidates.Add(new SearchCandidate(
            match.Value.Tier,
            urgency,
            new GlobalSearchResultDto(
                GlobalSearchResultType.Approval,
                approval.ApprovalId.ToString(),
                approval.CaseId.ToString(),
                approval.ApprovalType,
                $"Approval · {approval.Status}",
                match.Value.Reason,
                $"/app/work-items?approvalId={approval.ApprovalId}",
                approval.UpdatedAt)));
    }

    private static SearchMatch? BestMatch(
        string query,
        params (string? Value, string Reason, bool Identity)[] fields)
    {
        SearchMatch? best = null;
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            var tier = string.Equals(field.Value.Trim(), query, StringComparison.OrdinalIgnoreCase)
                ? field.Identity ? 4 : 3
                : field.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
                    ? field.Identity ? 2 : 1
                    : 0;
            if (tier > (best?.Tier ?? 0))
            {
                best = new SearchMatch(tier, field.Reason);
            }
        }

        return best;
    }

    private static string ShortReference(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private sealed record SearchCandidate(int MatchTier, int Urgency, GlobalSearchResultDto Result);

    private readonly record struct SearchMatch(int Tier, string Reason);
}

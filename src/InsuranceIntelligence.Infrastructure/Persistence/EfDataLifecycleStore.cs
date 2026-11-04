using System.Text.Json;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.DataLifecycle;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Counts and deletes a workspace's data for retention and erasure (build-loop item 34). Every query starts from one scope. Data tied to a
/// claim is found by the claim's id; data that is not (chats, consents, mail, profiles) is found by looking for the person's phone number or
/// email in what is stored, because it is kept as documents without a column for either.
/// </summary>
public sealed class EfDataLifecycleStore(InsuranceIntelligenceDbContext dbContext) : JsonRepositoryBase(dbContext), IDataLifecycleStore
{
    public async Task<CaseDataFootprint> FootprintAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>
        {
            [DataCategories.Documents] = await Scoped(DbContext.CaseDocuments, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
            [DataCategories.Communications] = await Scoped(DbContext.Communications, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
            [DataCategories.CustomerUpdates] = await Scoped(DbContext.CustomerUpdates, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
            [DataCategories.Reviews] =
                await Scoped(DbContext.ClaimReviewRequests, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.WorkflowTasks, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.Approvals, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.CaseReviews, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.SatisfactionResponses, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
            [DataCategories.AiRecords] =
                await Scoped(DbContext.AgentActionLogs, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.ClaimAssessments, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.AuthenticityReports, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.FieldProposals, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.SlotPdfs, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.AgentRuns, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
            [DataCategories.Packs] =
                await Scoped(DbContext.ClaimsPackDispatches, scope).CountAsync(item => item.CaseId == caseId, cancellationToken)
                + await Scoped(DbContext.ClaimsPackShareLinks, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
            [DataCategories.Uploads] = await Scoped(DbContext.UploadSessions, scope).CountAsync(item => item.CaseId == caseId, cancellationToken),
        };

        var links = await Scoped(DbContext.ClaimsPackShareLinks.AsNoTracking(), scope).Where(item => item.CaseId == caseId).Select(item => item.PayloadJson).ToListAsync(cancellationToken);
        var keys = links.Select(json => Deserialize<ClaimsPackShareLink>(json).ObjectKey).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct().ToArray();
        return new CaseDataFootprint(counts, keys);
    }

    public async Task<IReadOnlyDictionary<string, int>> DeleteCaseDataAsync(TenantScope scope, Guid caseId, bool includeClaim, CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>();

        async Task<int> Remove<T>(IQueryable<T> query, DbSet<T> set) where T : class
        {
            var rows = await query.ToListAsync(cancellationToken);
            set.RemoveRange(rows);
            return rows.Count;
        }

        counts[DataCategories.Documents] = await Remove(Scoped(DbContext.CaseDocuments, scope).Where(item => item.CaseId == caseId), DbContext.CaseDocuments);
        counts[DataCategories.Communications] = await Remove(Scoped(DbContext.Communications, scope).Where(item => item.CaseId == caseId), DbContext.Communications);
        counts[DataCategories.CustomerUpdates] = await Remove(Scoped(DbContext.CustomerUpdates, scope).Where(item => item.CaseId == caseId), DbContext.CustomerUpdates);
        counts[DataCategories.Reviews] =
            await Remove(Scoped(DbContext.ClaimReviewRequests, scope).Where(item => item.CaseId == caseId), DbContext.ClaimReviewRequests)
            + await Remove(Scoped(DbContext.WorkflowTasks, scope).Where(item => item.CaseId == caseId), DbContext.WorkflowTasks)
            + await Remove(Scoped(DbContext.Approvals, scope).Where(item => item.CaseId == caseId), DbContext.Approvals)
            + await Remove(Scoped(DbContext.CaseReviews, scope).Where(item => item.CaseId == caseId), DbContext.CaseReviews)
            + await Remove(Scoped(DbContext.SatisfactionResponses, scope).Where(item => item.CaseId == caseId), DbContext.SatisfactionResponses);
        counts[DataCategories.AiRecords] =
            await Remove(Scoped(DbContext.AgentActionLogs, scope).Where(item => item.CaseId == caseId), DbContext.AgentActionLogs)
            + await Remove(Scoped(DbContext.ClaimAssessments, scope).Where(item => item.CaseId == caseId), DbContext.ClaimAssessments)
            + await Remove(Scoped(DbContext.AuthenticityReports, scope).Where(item => item.CaseId == caseId), DbContext.AuthenticityReports)
            + await Remove(Scoped(DbContext.FieldProposals, scope).Where(item => item.CaseId == caseId), DbContext.FieldProposals)
            + await Remove(Scoped(DbContext.SlotPdfs, scope).Where(item => item.CaseId == caseId), DbContext.SlotPdfs)
            + await Remove(Scoped(DbContext.AgentRuns, scope).Where(item => item.CaseId == caseId), DbContext.AgentRuns);
        counts[DataCategories.Packs] =
            await Remove(Scoped(DbContext.ClaimsPackDispatches, scope).Where(item => item.CaseId == caseId), DbContext.ClaimsPackDispatches)
            + await Remove(Scoped(DbContext.ClaimsPackShareLinks, scope).Where(item => item.CaseId == caseId), DbContext.ClaimsPackShareLinks);

        var sessions = await Scoped(DbContext.UploadSessions, scope).Where(item => item.CaseId == caseId).ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(item => item.Id).ToArray();
        if (sessionIds.Length > 0)
        {
            DbContext.UploadParts.RemoveRange(await Scoped(DbContext.UploadParts, scope).Where(item => sessionIds.Contains(item.UploadId)).ToListAsync(cancellationToken));
        }

        DbContext.UploadSessions.RemoveRange(sessions);
        counts[DataCategories.Uploads] = sessions.Count;

        var runs = await Scoped(DbContext.AgentWorkflowRuns, scope).Where(item => item.BusinessReferenceId == caseId.ToString()).ToListAsync(cancellationToken);
        if (runs.Count > 0)
        {
            var runIds = runs.Select(item => item.Id).ToArray();
            DbContext.AgentWorkItems.RemoveRange(await DbContext.AgentWorkItems.Where(item => runIds.Contains(item.RunId)).ToListAsync(cancellationToken));
            DbContext.AgentWorkflowRuns.RemoveRange(runs);
            counts[DataCategories.Reviews] += runs.Count;
        }

        if (includeClaim)
        {
            var claims = await Scoped(DbContext.Cases, scope).Where(item => item.Id == caseId).ToListAsync(cancellationToken);
            DbContext.Cases.RemoveRange(claims);
            counts[DataCategories.Claims] = claims.Count;
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return counts;
    }

    public async Task<IReadOnlyDictionary<string, int>> SubjectDataAsync(TenantScope scope, ErasureSubject subject, bool delete, CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>();
        if (subject.PhoneTail is null && subject.NormalisedEmail is null)
        {
            return counts;
        }

        async Task<int> Sweep<T>(IQueryable<T> query, DbSet<T> set) where T : PersistedScopedJson
        {
            var rows = (await query.ToListAsync(cancellationToken)).Where(row => Mentions(row.PayloadJson, subject)).ToList();
            if (delete)
            {
                set.RemoveRange(rows);
            }

            return rows.Count;
        }

        counts[DataCategories.Conversations] = await Sweep(Scoped(DbContext.CustomerConversationTurns, scope), DbContext.CustomerConversationTurns);
        counts[DataCategories.Messages] = await Sweep(Scoped(DbContext.WhatsAppMessages, scope), DbContext.WhatsAppMessages);
        counts[DataCategories.Consents] = await Sweep(Scoped(DbContext.ConsentRecords, scope), DbContext.ConsentRecords)
            + await Sweep(Scoped(DbContext.CustomerLanguagePreferences, scope), DbContext.CustomerLanguagePreferences);
        counts[DataCategories.PortalAccess] = await Sweep(Scoped(DbContext.ClaimantMagicLinkTokens, scope), DbContext.ClaimantMagicLinkTokens)
            + await Sweep(Scoped(DbContext.ClaimantPortalSessions, scope), DbContext.ClaimantPortalSessions);
        counts[DataCategories.Profiles] = await Sweep(Scoped(DbContext.Customers, scope), DbContext.Customers);
        counts[DataCategories.Leads] = await Sweep(Scoped(DbContext.SalesLeads, scope), DbContext.SalesLeads);
        counts[DataCategories.Emails] = await Sweep(Scoped(DbContext.InboundEmails, scope), DbContext.InboundEmails);

        if (delete)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return counts;
    }

    /// <summary>Whether a stored record mentions the person: their email as written, or their phone number however it was formatted (compared by its last nine digits).</summary>
    internal static bool Mentions(string payloadJson, ErasureSubject subject)
    {
        if (subject.NormalisedEmail is { } email && payloadJson.Contains(email, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (subject.PhoneTail is { } tail)
        {
            // Every digit in order, with the spaces, dashes and plus signs they were written with left out.
            var digits = new System.Text.StringBuilder(payloadJson.Length / 2);
            foreach (var character in payloadJson)
            {
                if (char.IsDigit(character))
                {
                    digits.Append(character);
                }
            }

            return digits.ToString().Contains(tail, StringComparison.Ordinal);
        }

        return false;
    }

    public async Task<IReadOnlyList<DocumentRef>> DocumentsBeforeAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> excludeCases, int take, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(DbContext.CaseDocuments.AsNoTracking(), scope)
            .Where(item => item.UpdatedAt < cutoff && !excludeCases.Contains(item.CaseId))
            .OrderBy(item => item.UpdatedAt)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var document = Deserialize<InsuranceIntelligence.Domain.Documents.CaseDocument>(row.PayloadJson);
            return new DocumentRef(row.CaseId, row.Id, document.ObjectKey, document.PreviewObjectKey);
        }).ToArray();
    }

    public async Task<int> DeleteDocumentRowsAsync(TenantScope scope, IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(DbContext.CaseDocuments, scope).Where(item => documentIds.Contains(item.Id)).ToListAsync(cancellationToken);
        DbContext.CaseDocuments.RemoveRange(rows);
        await DbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    public async Task<int> DeleteCommunicationsBeforeAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> excludeCases, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(DbContext.Communications, scope).Where(item => item.UpdatedAt < cutoff && !excludeCases.Contains(item.CaseId)).ToListAsync(cancellationToken);
        DbContext.Communications.RemoveRange(rows);
        await DbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    public async Task<IReadOnlyDictionary<string, int>> DeleteConversationsBeforeAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> excludeCases, CancellationToken cancellationToken = default)
    {
        var held = excludeCases.Select(id => id.ToString()).ToArray();
        bool Kept(string json) => held.Any(id => json.Contains(id, StringComparison.OrdinalIgnoreCase));

        var turns = (await Scoped(DbContext.CustomerConversationTurns, scope).Where(item => item.UpdatedAt < cutoff).ToListAsync(cancellationToken)).Where(row => !Kept(row.PayloadJson)).ToList();
        var messages = (await Scoped(DbContext.WhatsAppMessages, scope).Where(item => item.UpdatedAt < cutoff).ToListAsync(cancellationToken))
            .Where(row => (row.CaseId is null || !excludeCases.Contains(row.CaseId.Value)) && !Kept(row.PayloadJson)).ToList();
        DbContext.CustomerConversationTurns.RemoveRange(turns);
        DbContext.WhatsAppMessages.RemoveRange(messages);
        await DbContext.SaveChangesAsync(cancellationToken);
        return new Dictionary<string, int> { [DataCategories.Conversations] = turns.Count, [DataCategories.Messages] = messages.Count };
    }

    public async Task<IReadOnlyList<TenantScope>> ListScopesWithRetentionAsync(CancellationToken cancellationToken = default)
    {
        var all = await DbContext.TenantSettings.AsNoTracking().Select(item => item.PayloadJson).ToListAsync(cancellationToken);
        return all.Select(Deserialize<TenantSettings>)
            .Where(settings => settings.RetentionMonths is { } months && months.Values.Any(value => value > 0))
            .Select(settings => settings.Scope)
            .ToArray();
    }

    public Task SaveDeletionAsync(DeletionRecord record, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.DeletionRecords,
            record.DeletionId,
            record.Scope,
            Serialize(record),
            row =>
            {
                row.Kind = record.Kind;
                row.AtUtc = record.At.UtcDateTime;
            },
            cancellationToken);

    public async Task<IReadOnlyList<DeletionRecord>> ListDeletionsAsync(TenantScope scope, int take, CancellationToken cancellationToken = default)
    {
        var rows = await Scoped(DbContext.DeletionRecords.AsNoTracking(), scope)
            .OrderByDescending(item => item.AtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(item => item.PayloadJson)
            .ToListAsync(cancellationToken);
        return rows.Select(Deserialize<DeletionRecord>).ToArray();
    }
}

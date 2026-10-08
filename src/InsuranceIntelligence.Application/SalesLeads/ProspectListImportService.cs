using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>One person from an uploaded customer list, after the columns were matched on screen.</summary>
public sealed record ImportListRow(
    string? Name,
    string? Phone,
    string? Occupation = null,
    string? Area = null,
    string? Branch = null,
    string? BankProducts = null,
    string? Consent = null);

public sealed record ImportListRequest(string? ListName, IReadOnlyList<ImportListRow>? Rows, bool? BankCustomers = null);

public sealed record ImportListProblem(int Row, string Reason);

public sealed record ImportListResult(
    string ListName,
    int Received,
    int Created,
    int WithConsent,
    int AlreadyKnown,
    int Invalid,
    IReadOnlyList<ImportListProblem> Problems);

public interface IProspectListImportService
{
    Task<ImportListResult> ImportAsync(ImportListRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads a customer list the staff uploaded into the workspace as prospects. Each person becomes one
/// prospect marked as coming from the list. A person already on record (same phone number) is left
/// untouched. Consent is taken only from what the list says: a blank or unclear answer is "not recorded",
/// and only people with a recorded agreement can be messaged. Nothing is sent and nothing is scored here.
/// </summary>
public sealed class ProspectListImportService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadWriteRepository repository,
    IAuditWriter auditWriter) : IProspectListImportService
{
    public const int MaxRows = 5000;
    private const int MaxProblemsReported = 50;

    public async Task<ImportListResult> ImportAsync(ImportListRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);

        var rows = request.Rows ?? [];
        if (rows.Count == 0)
        {
            throw new SalesLeadQueryValidationException(nameof(request.Rows), "The list has no people in it.");
        }

        if (rows.Count > MaxRows)
        {
            throw new SalesLeadQueryValidationException(nameof(request.Rows), $"A list can hold up to {MaxRows:N0} people at a time. Split it and upload it in parts.");
        }

        var listName = string.IsNullOrWhiteSpace(request.ListName) ? "Uploaded list" : request.ListName.Trim();
        if (listName.Length > 120)
        {
            listName = listName[..120];
        }

        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;
        var problems = new List<ImportListProblem>();
        var seenPhones = new HashSet<string>(StringComparer.Ordinal);
        int created = 0, withConsent = 0, alreadyKnown = 0, invalid = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowNumber = index + 2; // the file's first line is the headings

            var name = CleanText(row.Name, 120);
            if (name is null || name.Length < 2)
            {
                invalid++;
                AddProblem(problems, rowNumber, "No name.");
                continue;
            }

            var phone = NormalisePhone(row.Phone);
            if (phone is null)
            {
                invalid++;
                AddProblem(problems, rowNumber, "No usable phone number.");
                continue;
            }

            if (!seenPhones.Add(phone))
            {
                alreadyKnown++;
                AddProblem(problems, rowNumber, "The same phone number appears earlier in this file.");
                continue;
            }

            if (await repository.FindByPhoneAsync(context.TenantId, context.WorkspaceId, phone, cancellationToken) is not null)
            {
                alreadyKnown++;
                AddProblem(problems, rowNumber, "Already a prospect. Left as it is.");
                continue;
            }

            var consentStatus = ParseConsent(row.Consent);
            var facts = new List<ProspectFact>();
            AddFact(facts, "occupation", row.Occupation, 120, now, context.UserId, listName);
            AddFact(facts, "area", row.Area, 120, now, context.UserId, listName);
            AddFact(facts, "branch", row.Branch, 120, now, context.UserId, listName);
            AddFact(facts, "bank_products", row.BankProducts, 200, now, context.UserId, listName);

            var consent = consentStatus == ContactConsentStatus.NotRecorded
                ? null
                : new ContactConsent(
                    consentStatus,
                    now,
                    consentStatus == ContactConsentStatus.Agreed
                        ? $"The uploaded list \"{listName}\" records that this person agreed to be contacted."
                        : $"The uploaded list \"{listName}\" records that this person did not agree to be contacted.",
                    "List upload");
            if (consentStatus == ContactConsentStatus.Agreed)
            {
                withConsent++;
            }

            var lead = new SalesLeadDetailReadModel(
                LeadId: $"LEAD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                LeadSource: "Imported list",
                CustomerNeed: "On an uploaded customer list",
                ConversationSummary: $"Imported from {listName}.",
                ProductFit: "To be assessed",
                QuoteStatus: "New",
                RequiredInformation: [],
                FollowUpActions: [],
                AssignedSalesOfficer: null,
                ConversionProbability: null,
                CustomerName: name,
                ContactInfo: $"+{phone}",
                CreatedAt: now,
                CreatedBy: context.UserId)
            {
                Phone = phone,
                PrimaryChannel = "List",
                LastInteractionAt = now,
                BankCustomer = request.BankCustomers,
                Facts = ProspectFacts.Merge([], facts),
                Consent = consent,
                Activity = [new ProspectActivity(now, $"Imported from {listName}", "List upload")],
            };

            await repository.CreateAsync(context.TenantId, context.WorkspaceId, lead, cancellationToken);
            created++;
        }

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                context.Scope,
                CaseId: null,
                DocumentId: null,
                UserId: context.UserId,
                ActorType.User,
                "sales_lead.list_imported",
                now,
                BeforeStatus: null,
                AfterStatus: null,
                EvidenceIds: [],
                ModelProvider: "SalesLeads",
                Confidence: null,
                ValidationResult: "recorded",
                ApprovalId: null,
                // Counts only: the names and numbers in the list are not copied into the audit trail.
                AuditReason: $"Customer list \"{listName}\" imported: {created} added, {alreadyKnown} already known, {invalid} rejected, of {rows.Count}.",
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

        return new ImportListResult(listName, rows.Count, created, withConsent, alreadyKnown, invalid, problems);
    }

    private static void AddProblem(List<ImportListProblem> problems, int row, string reason)
    {
        if (problems.Count < MaxProblemsReported)
        {
            problems.Add(new ImportListProblem(row, reason));
        }
    }

    private static void AddFact(List<ProspectFact> facts, string key, string? value, int max, DateTimeOffset now, string userId, string listName)
    {
        var clean = CleanText(value, max);
        if (clean is not null)
        {
            facts.Add(new ProspectFact(key, clean, ProspectFactSource.ImportedList, now, null, listName));
        }
    }

    private static string? CleanText(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // Spreadsheet formulas can run when a value is opened again in a spreadsheet: keep the text, drop the trigger.
        if (text[0] is '=' or '+' or '@' || (text[0] == '-' && text.Length > 1 && !char.IsDigit(text[1])))
        {
            text = text.TrimStart('=', '+', '-', '@', ' ');
        }

        text = new string(text.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length > max ? text[..max] : text;
    }

    /// <summary>International digits only, Uganda assumed for a number written with a leading 0 or without a
    /// country code. Returns null when too short or too long to be a phone number.</summary>
    public static string? NormalisePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith('0') && digits.Length == 10)
        {
            digits = "256" + digits[1..];
        }
        else if (digits.Length == 9)
        {
            digits = "256" + digits;
        }

        return digits.Length is >= 11 and <= 15 ? digits : null;
    }

    public static ContactConsentStatus ParseConsent(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant();
        return value switch
        {
            "yes" or "y" or "true" or "1" or "agreed" or "agree" or "opted in" or "opt in" or "consented" or "consent" => ContactConsentStatus.Agreed,
            "no" or "n" or "false" or "0" or "declined" or "refused" or "opted out" or "opt out" => ContactConsentStatus.Declined,
            "withdrawn" or "revoked" or "unsubscribed" => ContactConsentStatus.Withdrawn,
            _ => ContactConsentStatus.NotRecorded,
        };
    }
}

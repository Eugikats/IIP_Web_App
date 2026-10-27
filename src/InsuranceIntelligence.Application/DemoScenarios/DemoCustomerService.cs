using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.DemoScenarios;

public enum DemoConsent
{
    /// <summary>Agreed to be contacted by the bank about insurance. The rules may message them.</summary>
    Agreed,

    /// <summary>Never asked. The rules must skip them.</summary>
    NotAsked,

    /// <summary>Said no to marketing. The rules must skip them.</summary>
    Declined,
}

public sealed record DemoCustomerRequest(
    string? Name,
    string? Phone,
    string? Branch,
    IReadOnlyList<string>? BankProducts,
    string? IncomeBand,
    string? Dependants,
    decimal? LoanAmount,
    int? LoanTermMonths,
    int? LoanDaysAgo,
    DemoConsent Consent);

public sealed class DemoCustomerException(string message) : Exception(message);

public sealed record DemoCustomerResult(string LeadId, string Phone, bool EventRecorded);

/// <summary>
/// Adds one person to a demonstration workspace, for a teammate who has agreed to take part, with what the bank would know about
/// them and what they agreed to. Only a workspace marked as a demonstration accepts it, so a real organisation can never get an
/// invented customer through this door. The consent is the thing being demonstrated, so it is stated, not assumed.
/// </summary>
public sealed class DemoCustomerService(ISalesLeadWriteRepository leads, EventService events, WorkspaceBrandService brands, TimeProvider clock)
{
    public async Task<DemoCustomerResult> AddAsync(TenantScope scope, DemoCustomerRequest request, string actorId, CancellationToken cancellationToken = default)
    {
        var settings = await brands.GetAsync(scope, cancellationToken);
        if (settings is not { IsDemonstration: true })
        {
            throw new DemoCustomerException("Demonstration customers can only be added to a demonstration workspace.");
        }

        var name = string.Join(' ', (request.Name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (name.Length is < 2 or > 80)
        {
            throw new DemoCustomerException("Give the person's name, up to 80 characters.");
        }

        string phone;
        try
        {
            phone = EventService.NormalisePhone(request.Phone);
        }
        catch (EventException problem)
        {
            throw new DemoCustomerException(problem.Message);
        }

        var now = clock.GetUtcNow();
        var products = (request.BankProducts ?? []).Select(item => item.Trim()).Where(item => item.Length > 0).Take(8).ToArray();
        var facts = new List<ProspectFact>();
        void Fact(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                facts.Add(new ProspectFact(key, value.Trim(), ProspectFactSource.ImportedList, now, null, "Demonstration customer"));
            }
        }

        Fact("bank_products", string.Join(", ", products));
        Fact("income_band", request.IncomeBand);
        Fact("dependants", request.Dependants);
        Fact("branch", request.Branch);

        var consent = request.Consent switch
        {
            // Given when the account was opened, before anything the bank later tells us about.
            DemoConsent.Agreed => new ContactConsent(ContactConsentStatus.Agreed, now.AddDays(-30), "Agreed to be contacted by the bank about insurance.", "Bank list"),
            DemoConsent.Declined => new ContactConsent(ContactConsentStatus.Declined, now.AddDays(-30), "Said no to marketing messages.", "Bank list"),
            _ => null,
        };

        var existing = await leads.FindByPhoneAsync(scope.TenantId, scope.WorkspaceId, phone, cancellationToken);
        var lead = new SalesLeadDetailReadModel(
            existing?.LeadId ?? $"LEAD-DEMO-{phone}", "Bank customer list", "Customer of the bank", "Demonstration customer, added for a rehearsal.", "To be assessed", "New", [], [], null, null,
            name, $"+{phone}", existing?.CreatedAt ?? now.AddDays(-7), actorId)
        {
            Phone = phone,
            PrimaryChannel = "Bank list",
            BankCustomer = true,
            Facts = ProspectFacts.Merge(existing?.Facts ?? [], facts),
            Consent = consent,
            Activity = [.. existing?.Activity ?? [], new ProspectActivity(now, "Added as a demonstration customer", "Demo setup")],
        };
        await leads.CreateAsync(scope.TenantId, scope.WorkspaceId, lead, cancellationToken);

        var recorded = false;
        if (request.LoanAmount is > 0)
        {
            var daysAgo = Math.Clamp(request.LoanDaysAgo ?? 2, 0, 60);
            var when = now.AddDays(-daysAgo).AddMinutes(-1);
            var made = await events.RecordAsync(
                scope,
                new EventInput("loan_disbursed", phone, when, $"demo-loan-{phone}-{when:yyyyMMdd}", request.LoanAmount, request.LoanTermMonths, null, request.Branch),
                EventSource.Recorded, "Demonstration customer", actorId, cancellationToken);
            recorded = made.Created;
        }

        return new DemoCustomerResult(lead.LeadId, phone, recorded);
    }
}

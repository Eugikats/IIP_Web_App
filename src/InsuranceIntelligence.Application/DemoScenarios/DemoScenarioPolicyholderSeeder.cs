using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.DemoScenarios;

/// <summary>
/// Invented policyholders for the two Nile demonstrations. Every date is worked out from today, so a policy that "ends in
/// 30 days" is always 30 days away however long the demonstration has been set up. Running it again restores the same
/// records and moves their dates to match today.
/// </summary>
public sealed class DemoScenarioPolicyholderSeeder(
    ICustomerRepository customers, IPolicyRepository policies, InsuranceIntelligence.Application.SalesLeads.ISalesLeadWriteRepository? leads = null)
{
    public const string Marker = "demonstration";

    public sealed record Result(int Customers, int Policies);

    private sealed record Holder(string Name, string Line, int DaysUntilEnd);

    private static readonly Holder[] Life =
    [
        new("Sarah Nakirya", MicroinsuranceCatalog.FuneralLine, 3),
        new("Peter Otieno", MicroinsuranceCatalog.SchoolFeesLine, 7),
        new("Rose Namatovu", MicroinsuranceCatalog.LoanProtectorLine, 14),
        new("Isaac Mukiibi", MicroinsuranceCatalog.FuneralLine, 21),
        new("Beatrice Adong", MicroinsuranceCatalog.SchoolFeesLine, 30),
        new("Charles Wanyama", MicroinsuranceCatalog.LoanProtectorLine, 30),
        new("Dorothy Kyalimpa", MicroinsuranceCatalog.FuneralLine, 45),
        new("Emmanuel Kizza", MicroinsuranceCatalog.SchoolFeesLine, 60),
        new("Florence Ayo", MicroinsuranceCatalog.FuneralLine, 90),
        new("George Lutaaya", MicroinsuranceCatalog.LoanProtectorLine, 150),
        new("Harriet Nabwire", MicroinsuranceCatalog.SchoolFeesLine, 240),
        new("Ibrahim Kasule", MicroinsuranceCatalog.FuneralLine, -6),
    ];

    private static readonly Holder[] General =
    [
        new("Joan Namuddu", MicroinsuranceCatalog.BodaAccidentLine, 3),
        new("Kenneth Opolot", MicroinsuranceCatalog.ShopLine, 7),
        new("Lillian Tendo", MicroinsuranceCatalog.StallStockLine, 14),
        new("Martin Ssentongo", MicroinsuranceCatalog.TransitLine, 21),
        new("Nora Atuhaire", MicroinsuranceCatalog.HospitalCashLine, 30),
        new("Oscar Mwanje", MicroinsuranceCatalog.BodaAccidentLine, 30),
        new("Pauline Akot", MicroinsuranceCatalog.ShopLine, 45),
        new("Quincy Bwambale", MicroinsuranceCatalog.StallStockLine, 60),
        new("Racheal Nabbosa", MicroinsuranceCatalog.MaternityLine, 90),
        new("Simon Okwera", MicroinsuranceCatalog.HospitalCashLine, 150),
        new("Teddy Kobusingye", MicroinsuranceCatalog.TransitLine, 240),
        new("Umar Ssebugwawo", MicroinsuranceCatalog.BodaAccidentLine, -6),
    ];

    public async Task<Result> SeedAsync(TenantScope scope, DemoScenario scenario, CancellationToken cancellationToken = default)
    {
        if (scenario.IsBank)
        {
            return new Result(0, 0);
        }

        var holders = scenario.LineFocus == LineFocus.Life ? Life : General;
        var brand = new KatoBrand(scenario.BrandName, scenario.ShortName, [], scenario.LineFocus);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = 0;
        for (var index = 0; index < holders.Length; index++)
        {
            var holder = holders[index];
            var product = brand.Products.FirstOrDefault(item => item.ProductLine == holder.Line);
            if (product is null)
            {
                continue;
            }

            var phone = $"2567{(scenario.LineFocus == LineFocus.Life ? "901" : "902")}{index + 1:D5}";
            var customer = new Customer(IdOf(scope, $"customer-{phone}"), scope.TenantId, scope.WorkspaceId, holder.Name, null, $"+{phone}");
            await customers.UpsertAsync(customer, cancellationToken);

            var end = today.AddDays(holder.DaysUntilEnd);
            var start = end.AddYears(-1).AddDays(1);
            var policy = new Policy(
                IdOf(scope, $"policy-{phone}"), scope.TenantId, scope.WorkspaceId,
                $"{product.PolicyPrefix}/{product.Code}/{start.Year}/{index + 1:D5}", product.ProductLine,
                $"{product.Name}, UGX {product.MonthlyPremium:N0} a month",
                customer.CustomerId, null, start, end, product.MonthlyPremium * 12, "UGX", product.SumInsured);
            await policies.UpsertAsync(policy, cancellationToken);

            // The person is also on the contact list, with the agreement to receive reminders about their own policy that they gave
            // when they bought it, so the rules have something to check.
            if (leads is not null)
            {
                var joined = DateTimeOffset.UtcNow.AddDays(-200);
                await leads.CreateAsync(scope.TenantId, scope.WorkspaceId, new InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel(
                    $"LEAD-POLICY-{phone}", "Policyholder", "Existing customer", "Demonstration policyholder", product.Name, "Customer", [], [], null, null,
                    holder.Name, $"+{phone}", joined, "system:demo-seed")
                {
                    Phone = phone,
                    PrimaryChannel = "Policy records",
                    Consent = new InsuranceIntelligence.Domain.Acquisition.ContactConsent(
                        InsuranceIntelligence.Domain.Acquisition.ContactConsentStatus.Agreed, joined, "Agreed to receive reminders about their own policy on WhatsApp when the policy was bought.", "Policy purchase"),
                    Activity = [new InsuranceIntelligence.Domain.Acquisition.ProspectActivity(joined, "Bought " + product.Name, "Demo data")],
                }, cancellationToken);
            }

            created++;
        }

        return new Result(created, created);
    }

    private static string IdOf(TenantScope scope, string key) =>
        new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"scenario:{scope.TenantId}:{scope.WorkspaceId}:{key}"))).ToString();
}

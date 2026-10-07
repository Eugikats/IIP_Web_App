using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.DemoSeeding;

/// <summary>
/// An invented customer list for a bank selling insurance (bancassurance): 39 made-up people with the
/// kind of facts a bank holds (work, area, branch, products held, income band), a mix of marketing
/// consent, and different distances along the way to buying. Phone numbers are in a range that does not
/// belong to real subscribers, so nothing here can reach a real person. Re-running replaces the same
/// records rather than adding more. Demo profiles only (the route is blocked in protected runtimes).
/// </summary>
public sealed class BankCustomerDemoSeeder(ISalesLeadWriteRepository leads, IAuditRepository auditRepository)
{
    public const string SeedActor = "system:demo-seed";
    public const string ListName = "Mavuno Bank customer list (demonstration)";

    public sealed record Result(int Created, int WithConsent, string ListName);

    private sealed record Person(
        string Name, string Occupation, string Area, string Branch, string BankProducts, string Income, string Dependants,
        string? Interest, InterestStrength Strength, ContactConsentStatus Consent, int DaysAgo, string? Cover = null);

    private static readonly Person[] People =
    [
        new("Aisha Namutebi", "Salon owner", "Ntinda", "Ntinda branch", "Current account, business loan", "UGX 1.5m to 3m a month", "2 children", MicroinsuranceCatalog.ShopLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 1),
        new("Brian Okello", "Boda boda rider", "Kawempe", "Kawempe branch", "Savings account, boda loan", "UGX 600,000 a month", "3 children", MicroinsuranceCatalog.BodaAccidentLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 1),
        new("Christine Auma", "Teacher", "Gulu", "Gulu branch", "Salary account, school fees loan", "UGX 1.2m a month", "4 children", MicroinsuranceCatalog.SchoolFeesLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 2),
        new("David Mugisha", "Hardware shop owner", "Mbarara", "Mbarara branch", "Business account, working capital loan", "UGX 4m a month", "3 children", MicroinsuranceCatalog.ShopLine, InterestStrength.Mentioned, ContactConsentStatus.Agreed, 2),
        new("Esther Nakato", "Market vendor", "Owino", "Kampala Road branch", "Savings account", "UGX 500,000 a month", "2 children", MicroinsuranceCatalog.StallStockLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 3),
        new("Francis Ssemakula", "Taxi driver", "Najjera", "Najjera branch", "Current account, vehicle loan", "UGX 1.8m a month", "5 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 3),
        new("Grace Atim", "Nurse", "Lira", "Lira branch", "Salary account, mortgage", "UGX 2.4m a month", "1 child", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 4),
        new("Hassan Kiggundu", "Wholesale trader", "Kikuubo", "Kampala Road branch", "Business account, trade loan", "UGX 6m a month", "4 children", MicroinsuranceCatalog.TransitLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 4),
        new("Irene Nabirye", "Tailor", "Jinja", "Jinja branch", "Savings account, group loan", "UGX 700,000 a month", "3 children", MicroinsuranceCatalog.FuneralLine, InterestStrength.Mentioned, ContactConsentStatus.NotRecorded, 5),
        new("Joseph Byaruhanga", "Farmer", "Masaka", "Masaka branch", "Savings account, farm input loan", "UGX 900,000 a month", "6 children", MicroinsuranceCatalog.FuneralLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 5),
        new("Kevin Opio", "Mechanic", "Nakawa", "Nakawa branch", "Current account, equipment loan", "UGX 1.4m a month", "2 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 5),
        new("Lydia Nansubuga", "Boutique owner", "Entebbe", "Entebbe branch", "Business account, overdraft", "UGX 3m a month", "1 child", MicroinsuranceCatalog.ShopLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 6),
        new("Moses Tumusiime", "Civil servant", "Mbale", "Mbale branch", "Salary account, personal loan", "UGX 2.1m a month", "3 children", MicroinsuranceCatalog.SchoolFeesLine, InterestStrength.Mentioned, ContactConsentStatus.Declined, 6),
        new("Naomi Achan", "Caterer", "Kololo", "Kololo branch", "Current account", "UGX 2m a month", "2 children", MicroinsuranceCatalog.HomeContentsLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 6),
        new("Patrick Mwesigwa", "Boda boda rider", "Kasubi", "Kawempe branch", "Savings account, boda loan", "UGX 550,000 a month", "2 children", MicroinsuranceCatalog.BodaAccidentLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 7),
        new("Queen Birungi", "Pharmacy owner", "Kabalagala", "Kabalagala branch", "Business account, equipment loan", "UGX 5m a month", "2 children", MicroinsuranceCatalog.ShopLine, InterestStrength.Mentioned, ContactConsentStatus.Agreed, 7),
        new("Robert Ssekandi", "Electrician", "Bweyogerere", "Najjera branch", "Current account", "UGX 1.3m a month", "3 children", MicroinsuranceCatalog.HomeContentsLine, InterestStrength.Mentioned, ContactConsentStatus.NotRecorded, 8),
        new("Sarah Nalubega", "Primary school teacher", "Kira", "Najjera branch", "Salary account, school fees loan", "UGX 1.1m a month", "3 children", MicroinsuranceCatalog.SchoolFeesLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 8),
        new("Timothy Ochieng", "Fish trader", "Kasenyi", "Entebbe branch", "Savings account", "UGX 800,000 a month", "4 children", MicroinsuranceCatalog.TransitLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 8),
        new("Ursula Kyomuhendo", "Accountant", "Muyenga", "Kabalagala branch", "Salary account, mortgage", "UGX 3.5m a month", "2 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.Mentioned, ContactConsentStatus.Agreed, 9),
        new("Vincent Lubega", "Welder", "Katwe", "Kampala Road branch", "Current account, equipment loan", "UGX 1.6m a month", "3 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 9),
        new("Winnie Akello", "Hairdresser", "Lira", "Lira branch", "Savings account, group loan", "UGX 650,000 a month", "2 children", MicroinsuranceCatalog.MaternityLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 9),
        new("Xavier Muhumuza", "Lecturer", "Mbarara", "Mbarara branch", "Salary account", "UGX 3m a month", "2 children", MicroinsuranceCatalog.FuneralLine, InterestStrength.Mentioned, ContactConsentStatus.Agreed, 10),
        new("Yvonne Namukasa", "Restaurant owner", "Wandegeya", "Kampala Road branch", "Business account, working capital loan", "UGX 4.5m a month", "3 children", MicroinsuranceCatalog.HomeContentsLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 10),
        new("Zachary Wasswa", "Boda boda rider", "Ggaba", "Kabalagala branch", "Savings account", "UGX 500,000 a month", "1 child", MicroinsuranceCatalog.HospitalCashLine, InterestStrength.NotNow, ContactConsentStatus.Agreed, 10),
        new("Agnes Tusiime", "Shop owner", "Fort Portal", "Fort Portal branch", "Business account, stock loan", "UGX 2.5m a month", "4 children", MicroinsuranceCatalog.ShopLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 11),
        new("Bosco Kasozi", "Driver", "Mukono", "Mukono branch", "Current account, vehicle loan", "UGX 1.5m a month", "3 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.Mentioned, ContactConsentStatus.NotRecorded, 11),
        new("Carol Akullo", "Midwife", "Gulu", "Gulu branch", "Salary account", "UGX 1.9m a month", "expecting her second child", MicroinsuranceCatalog.MaternityLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 11),
        new("Denis Katende", "Carpenter", "Kasangati", "Najjera branch", "Savings account, group loan", "UGX 950,000 a month", "5 children", MicroinsuranceCatalog.FuneralLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 12),
        new("Eva Nankya", "Boutique owner", "Ntinda", "Ntinda branch", "Business account", "UGX 2.2m a month", "2 children", MicroinsuranceCatalog.ShopLine, InterestStrength.Mentioned, ContactConsentStatus.Agreed, 12),
        new("Felix Odongo", "Security guard", "Kawempe", "Kawempe branch", "Salary account", "UGX 700,000 a month", "4 children", MicroinsuranceCatalog.HospitalCashLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 12),
        new("Gloria Namuli", "Market vendor", "Nakasero", "Kololo branch", "Savings account", "UGX 600,000 a month", "3 children", MicroinsuranceCatalog.StallStockLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 13),
        new("Henry Mukasa", "Contractor", "Munyonyo", "Kabalagala branch", "Business account, asset loan", "UGX 7m a month", "3 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 13),
        new("Imelda Apio", "Tailor", "Lira", "Lira branch", "Savings account, group loan", "UGX 600,000 a month", "3 children", MicroinsuranceCatalog.SchoolFeesLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 13),
        new("Julius Kato", "Boda boda rider", "Kitintale", "Nakawa branch", "Savings account, boda loan", "UGX 580,000 a month", "2 children", MicroinsuranceCatalog.BodaAccidentLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 13),
        new("Kate Nabukenya", "Cosmetics seller", "Jinja", "Jinja branch", "Savings account", "UGX 850,000 a month", "1 child", MicroinsuranceCatalog.StallStockLine, InterestStrength.Mentioned, ContactConsentStatus.Withdrawn, 13),
        new("Lawrence Oloya", "Accountant", "Kampala", "Kampala Road branch", "Salary account, car loan", "UGX 3.2m a month", "2 children", MicroinsuranceCatalog.LoanProtectorLine, InterestStrength.Interested, ContactConsentStatus.Agreed, 13),
        new("Margaret Nakimuli", "Retired teacher", "Masaka", "Masaka branch", "Savings account", "UGX 900,000 a month", "grandchildren in school", MicroinsuranceCatalog.FuneralLine, InterestStrength.WantsIt, ContactConsentStatus.Agreed, 13),
        new("Nelson Bagonza", "Trader", "Kisoro", "Mbarara branch", "Business account", "UGX 2.8m a month", "4 children", MicroinsuranceCatalog.TransitLine, InterestStrength.Mentioned, ContactConsentStatus.NotRecorded, 13),
    ];

    public async Task<Result> SeedAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var withConsent = 0;
        for (var index = 0; index < People.Length; index++)
        {
            var person = People[index];
            var phone = $"2567900{index + 1:D5}";
            var created = now.AddDays(-person.DaysAgo).AddHours(-(index % 7));
            var when = created.AddHours(2);
            var product = MicroinsuranceCatalog.FindByLineOrName(person.Interest);

            var facts = ProspectFacts.Merge([], new[]
            {
                new ProspectFact("occupation", person.Occupation, ProspectFactSource.ImportedList, created, null, ListName),
                new ProspectFact("area", person.Area, ProspectFactSource.ImportedList, created, null, ListName),
                new ProspectFact("income_band", person.Income, ProspectFactSource.ImportedList, created, null, ListName),
                new ProspectFact("dependants", person.Dependants, ProspectFactSource.ImportedList, created, null, ListName),
                new ProspectFact("bank_products", person.BankProducts, ProspectFactSource.ImportedList, created, null, ListName),
                new ProspectFact("branch", person.Branch, ProspectFactSource.ImportedList, created, null, ListName),
            });

            var consent = person.Consent == ContactConsentStatus.NotRecorded
                ? null
                : new ContactConsent(
                    person.Consent, created, "Agreed at account opening to receive offers from the bank and its insurance partners.", "Bank list");
            if (person.Consent == ContactConsentStatus.Agreed)
            {
                withConsent++;
            }

            var lead = new SalesLeadDetailReadModel(
                LeadId: $"LEAD-BANK-{index + 1:D3}",
                LeadSource: "Bank customer list",
                CustomerNeed: product is null ? "Customer of the bank" : $"Customer of the bank, a fit for {product.Name}",
                ConversationSummary: $"Imported from {ListName}.",
                ProductFit: product?.Name ?? "To be assessed",
                QuoteStatus: "New",
                RequiredInformation: [],
                FollowUpActions: [],
                AssignedSalesOfficer: null,
                ConversionProbability: null,
                CustomerName: person.Name,
                ContactInfo: $"+{phone}",
                CreatedAt: created,
                CreatedBy: SeedActor)
            {
                Phone = phone,
                PrimaryChannel = "Bank list",
                LastInteractionAt = when,
                BankCustomer = true,
                Facts = facts,
                Interests = product is null
                    ? []
                    : [new ProspectInterest(product.Name, person.Strength, "Suggested from the customer's bank profile.", when)],
                Consent = consent,
                Activity = [new ProspectActivity(created, $"Imported from {ListName}", "Demo data")],
            };

            await leads.CreateAsync(scope.TenantId, scope.WorkspaceId, lead, cancellationToken);
        }

        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, SeedActor, ActorType.Integration, "demo_seed.bank_customers", now,
                null, null, [], "DemoSeed", null, "recorded", null,
                $"Loaded {People.Length} invented bank customers for the bancassurance demonstration. No real customer data.",
                Guid.NewGuid().ToString("N")),
            cancellationToken);
        return new Result(People.Length, withConsent, ListName);
    }
}

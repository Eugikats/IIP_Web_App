using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;

namespace InsuranceIntelligence.Application.DemoScenarios;

/// <summary>Where one demonstration world stands for the signed-in person.</summary>
public sealed record DemoScenarioState(DemoScenario Scenario, string? OrganisationId, string? WorkspaceId, bool Ready);

/// <summary>
/// Sets up and finds the demonstration worlds. Each one is an organisation of its own that the signed-in person
/// administers, marked as a demonstration in its brand settings; switching between them is choosing which organisation
/// the portal acts in. Setting one up creates the organisation, names the assistant after it, publishes its covers and
/// wordings, and loads its invented people. Doing it again finds the same organisation and refreshes its data.
/// </summary>
public sealed class DemoScenarioService(
    OrganisationService organisations,
    WorkspaceBrandService brands,
    MicroinsuranceDemoSeeder catalogue,
    BankCustomerDemoSeeder bankCustomers,
    DemoScenarioPolicyholderSeeder policyholders,
    IProspectTriageService triage,
    InsuranceIntelligence.Application.Rules.RuleService rules)
{
    public async Task<IReadOnlyList<DemoScenarioState>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var mine = await organisations.ListMineAsync(userId, cancellationToken);
        var found = new Dictionary<string, MyOrganisation>(StringComparer.OrdinalIgnoreCase);
        foreach (var organisation in mine.Where(item => item.Roles.Contains(nameof(Domain.Administration.PlatformRole.Admin), StringComparer.OrdinalIgnoreCase)))
        {
            var settings = await brands.GetAsync(TenantScope.Create(organisation.OrganisationId, organisation.WorkspaceId), cancellationToken);
            if (settings is { IsDemonstration: true, ScenarioId: { } id } && !found.ContainsKey(id))
            {
                found[id] = organisation;
            }
        }

        return DemoScenarioCatalog.All
            .Select(scenario => found.TryGetValue(scenario.Id, out var organisation)
                ? new DemoScenarioState(scenario, organisation.OrganisationId, organisation.WorkspaceId, Ready: true)
                : new DemoScenarioState(scenario, null, null, Ready: false))
            .ToArray();
    }

    public async Task<DemoScenarioState> ActivateAsync(string userId, string scenarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        var scenario = DemoScenarioCatalog.Find(scenarioId)
            ?? throw OrganisationException.NotFound("That demonstration does not exist.");
        var existing = (await ListAsync(userId, cancellationToken)).First(item => item.Scenario.Id == scenario.Id);

        TenantScope scope;
        string organisationId;
        if (existing.Ready)
        {
            organisationId = existing.OrganisationId!;
            scope = TenantScope.Create(organisationId, existing.WorkspaceId!);
        }
        else
        {
            var organisation = await organisations.CreateAsync(
                userId, scenario.OrganisationName, OrganisationType.Insurer, correlationId, cancellationToken, scenario.Product);
            organisationId = organisation.OrganisationId;
            scope = organisation.Scope;
        }

        var brand = new WorkspaceBrandSettings(
            scenario.BrandName, scenario.ShortName, scenario.PartnerInsurers, scenario.LineFocus, scenario.Id, IsDemonstration: true, About: scenario.About);
        await brands.SaveAsync(scope, brand, cancellationToken);

        await catalogue.SeedCatalogueForAsync(scope, brand.ToBrand().Products, cancellationToken);
        if (scenario.IsBank)
        {
            await bankCustomers.SeedAsync(scope, cancellationToken);
            await ScoreAsync(scope, userId, cancellationToken);
        }
        else
        {
            await policyholders.SeedAsync(scope, scenario, cancellationToken);
        }

        // Starting rules, only when the workspace has none, so a person who changed or approved them keeps their work.
        if ((await rules.ListAsync(scope, cancellationToken)).Count == 0)
        {
            foreach (var rule in DemoScenarioRules.For(scenario))
            {
                await rules.SaveAsync(scope, rule, "demo-setup", cancellationToken);
            }
        }

        return new DemoScenarioState(scenario, organisationId, scope.WorkspaceId, Ready: true);
    }

    private async Task ScoreAsync(TenantScope scope, string userId, CancellationToken cancellationToken)
    {
        try
        {
            await triage.ScoreAsync(scope, null, userId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Loading succeeded; scoring can be run from the Scores tab.
        }
    }
}

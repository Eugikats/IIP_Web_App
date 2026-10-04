using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Organisations;

public sealed record OnboardingStep(string Key, string Title, string Hint, bool Done, string Route);

public sealed record OnboardingChecklist(string OrganisationId, int Completed, int Total, IReadOnlyList<OnboardingStep> Steps);

/// <summary>
/// What a new insurer still has to do before the assistant can serve its customers, worked out from
/// what actually exists rather than from ticks someone remembered to make. Also offers a sandbox: a
/// separate workspace of clearly synthetic claims to explore, so nobody has to learn the product on
/// live data, and nothing in it can ever reach a real customer.
/// </summary>
public sealed class OrganisationOnboardingService(
    IOrganisationRepository organisations,
    IWhatsAppChannelConfigurationRepository whatsAppConfigurations,
    IProductRepository products,
    IKnowledgeDocumentRepository knowledge,
    ICaseRepository cases,
    ITenantMembershipWriter membershipWriter,
    DemoSeedService demoSeed)
{
    /// <summary>The workspace for synthetic data. Scheduled jobs and customer messaging never visit it.</summary>
    public const string SandboxWorkspace = "sandbox";

    public async Task<OnboardingChecklist> ChecklistAsync(string organisationId, CancellationToken cancellationToken = default)
    {
        var organisation = await organisations.GetAsync(organisationId, cancellationToken)
            ?? throw OrganisationException.NotFound("No such organisation.");
        var scope = organisation.Scope;

        var whatsApp = await whatsAppConfigurations.GetAsync(scope, cancellationToken);
        var anyPublishedProduct = (await products.ListCurrentForAllProductsAsync(scope, cancellationToken))
            .Any(p => p.Status == ProductStatus.Published);
        var anyApprovedWording = (await knowledge.ListAsync(scope, cancellationToken))
            .Any(d => d.ApprovalStatus == KnowledgeDocumentStatus.Approved);
        var members = await organisations.ListMemberUserIdsAsync(organisationId, cancellationToken);
        var sandboxLoaded = (await cases.ListAsync(SandboxScope(organisation), cancellationToken)).Count > 0;

        var steps = new List<OnboardingStep>
        {
            new("organisation", "Your organisation is set up", "Done when you signed up.", true, "/app/admin"),
            new("whatsapp", "Connect your WhatsApp Business number",
                "Customers write to this number and Kato answers. Each organisation uses its own number.",
                whatsApp is { IsActive: true }, "/app/admin/connections"),
            new("products", "Publish your products",
                "Kato can only explain and sell products you have published, with the documents each claim needs.",
                anyPublishedProduct, "/app/admin/products"),
            new("wordings", "Upload your approved policy wordings",
                "Kato answers policy questions only from wordings you have approved, and never from memory.",
                anyApprovedWording, "/app/knowledge"),
            new("team", "Invite your team",
                "Claims officers, supervisors and compliance; brokers join through a link instead.",
                members.Count > 1, "/app/admin/users"),
            new("sandbox", "Look around the sandbox",
                "A separate workspace of made-up claims to explore. Nothing in it reaches a real customer.",
                sandboxLoaded, "/app/admin/sandbox"),
        };

        return new OnboardingChecklist(organisationId, steps.Count(s => s.Done), steps.Count, steps);
    }

    /// <summary>Opens the sandbox for the person asking and fills it once. Safe to repeat.</summary>
    public async Task<TenantScope> EnableSandboxAsync(string organisationId, string userId, CancellationToken cancellationToken = default)
    {
        var organisation = await organisations.GetAsync(organisationId, cancellationToken)
            ?? throw OrganisationException.NotFound("No such organisation.");
        if (!organisation.IsActive)
        {
            throw OrganisationException.Forbidden("This organisation is not active.");
        }

        var scope = SandboxScope(organisation);
        await membershipWriter.GrantMembershipAsync(scope.TenantId, scope.WorkspaceId, userId, nameof(PlatformRole.Admin), cancellationToken);
        if ((await cases.ListAsync(scope, cancellationToken)).Count == 0)
        {
            await demoSeed.SeedAsync(scope, "system:sandbox-seed", userId, cancellationToken);
        }

        return scope;
    }

    private static TenantScope SandboxScope(Organisation organisation) => TenantScope.Create(organisation.OrganisationId, SandboxWorkspace);
}

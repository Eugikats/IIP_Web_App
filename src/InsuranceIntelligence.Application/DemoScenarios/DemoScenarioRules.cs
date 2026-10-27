using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Rules;

namespace InsuranceIntelligence.Application.DemoScenarios;

/// <summary>
/// The rules a demonstration starts with, written but not approved: the person showing the demonstration approves one in front
/// of the audience, which is the point. They wait no time after the event so a rehearsal does not have to wait days; a real bank
/// would set the waits in the plan (two to three days after a loan, a week after a salary account).
/// </summary>
public static class DemoScenarioRules
{
    private static OutreachRule Draft(string name, string eventType, int wait, RuleConditions conditions, params string[] lines) =>
        Draft(name, eventType, wait, "bank_cover_intro", conditions, lines);

    private static OutreachRule Draft(string name, string eventType, int wait, string template, RuleConditions conditions, params string[] lines) => new(
        string.Empty, name, 0, eventType, conditions, wait, lines, template, "en", new RuleHours(9, 18), new RuleLimits(),
        null, null, RuleStatus.Draft, "demo-setup", DateTimeOffset.UnixEpoch);

    private static string[] LinesOf(DemoScenario scenario) =>
        new KatoBrand(scenario.BrandName, scenario.ShortName, scenario.PartnerInsurers, scenario.LineFocus).Products.Select(product => product.ProductLine).ToArray();

    public static IReadOnlyList<OutreachRule> For(DemoScenario scenario) => scenario.Id switch
    {
        DemoScenarioCatalog.MavunoBank =>
        [
            Draft("Loan Protector after a salary loan", "loan_disbursed", 0, new RuleConditions(2_000_000m, 12),
                MicroinsuranceCatalog.LoanProtectorLine, MicroinsuranceCatalog.FuneralLine, MicroinsuranceCatalog.SchoolFeesLine),
            Draft("Funeral cover after a new salary account", "salary_account_opened", 0, new RuleConditions(),
                MicroinsuranceCatalog.FuneralLine, MicroinsuranceCatalog.SchoolFeesLine),
            Draft("School fees protector around the fees", "school_fees_paid", 0, new RuleConditions(),
                MicroinsuranceCatalog.SchoolFeesLine, MicroinsuranceCatalog.FuneralLine),
        ],
        DemoScenarioCatalog.NileLife =>
        [
            Draft("Renewal reminder, 30 days before", "policy_ending_30", 0, "renewal_30_days", new RuleConditions(), LinesOf(scenario)),
            Draft("Premium due reminder", "premium_due", 0, "premium_due", new RuleConditions(), LinesOf(scenario)),
            Draft("Win-back after a lapse", "policy_lapsed", 0, "policy_lapsed", new RuleConditions(), LinesOf(scenario)),
        ],
        DemoScenarioCatalog.NileGeneral =>
        [
            Draft("Renewal reminder, 30 days before", "policy_ending_30", 0, "renewal_30_days", new RuleConditions(), LinesOf(scenario)),
            Draft("Renewal reminder, 7 days before", "policy_ending_7", 0, "renewal_7_days", new RuleConditions(), LinesOf(scenario)),
            Draft("Win-back after a lapse", "policy_lapsed", 0, "policy_lapsed", new RuleConditions(), LinesOf(scenario)),
        ],
        _ => [],
    };
}

using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Organisations;

namespace InsuranceIntelligence.Application.DemoScenarios;

/// <summary>
/// A ready-made demonstration world: who Kato is, what the organisation sells, and the people and policies in it. Each
/// scenario lives in an organisation of its own, so switching between them changes nothing in the others and never mixes
/// their data. All the people are invented.
/// </summary>
public sealed record DemoScenario(
    string Id,
    string Title,
    string Summary,
    string Emphasis,
    string OrganisationName,
    string BrandName,
    string ShortName,
    IReadOnlyList<string> PartnerInsurers,
    LineFocus LineFocus,
    OrganisationProduct Product,
    string? About = null)
{
    /// <summary>A bank sells its partners' covers and shows who is worth offering cover to.</summary>
    public bool IsBank => PartnerInsurers.Count > 0;
}

public static class DemoScenarioCatalog
{
    public const string MavunoBank = "mavuno-bank";
    public const string NileLife = "nile-life";
    public const string NileGeneral = "nile-general";

    public static readonly IReadOnlyList<DemoScenario> All =
    [
        new(MavunoBank,
            "Mavuno Bank",
            "A bank that sells life and general covers from its partner insurers, Mpingo Life Assurance and Kifaru General Insurance, and earns a commission from them.",
            "Acquisition: who to offer cover to, and when.",
            "Mavuno Bank (demonstration)", "Mavuno Bank", "Mavuno Bank",
            [MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru],
            LineFocus.Both,
            OrganisationProduct.Bancassurance,
            "Mavuno Bank is a fictional demonstration bank. It offers savings, current and salary accounts, personal and business loans, and school fees loans. It also distributes life and general insurance from Mpingo Life Assurance and Kifaru General Insurance. Ask bank staff for current banking terms and eligibility."),
        new(NileLife,
            "Nile Life",
            "A life insurer with policyholders on monthly premiums: funeral, school fees and loan cover.",
            "Retention: premiums due, lapses and win-backs.",
            "Nile Shield Life Assurance (demonstration)", MicroinsuranceCatalog.NileLife, "Nile Shield Life",
            [],
            LineFocus.Life,
            OrganisationProduct.Insurance),
        new(NileGeneral,
            "Nile General",
            "A general insurer with annual policies, renewals falling due and claims waiting for documents.",
            "Retention and claims: renewals due, claims waiting.",
            "Nile Shield Assurance (demonstration)", MicroinsuranceCatalog.NileShield, "Nile Shield",
            [],
            LineFocus.General,
            OrganisationProduct.Insurance),
    ];

    public static DemoScenario? Find(string? id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));
}

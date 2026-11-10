using System.Text;

namespace InsuranceIntelligence.Domain.Fraud;

/// <summary>
/// One pattern the fraud agent knows to look for. An indicator is a reason for a person to look
/// closer, never proof: every one carries the ordinary explanations that look the same from outside,
/// so the agent (and the person reading it) weighs both.
/// </summary>
public sealed record FraudIndicator(
    string Code,
    string Line,
    string Label,
    string WhatItLooksLike,
    string OrdinaryExplanations,
    string WhatToCheck);

/// <summary>
/// What fraud looks like in insurance, as it tends to show up in Uganda, written down once so the
/// fraud agent is briefed the same way every time and so a person can read exactly what it was told.
/// This is the agent's training material: it goes into every fraud review prompt, the agent may only
/// raise a finding that cites facts from the claim in front of it, and a workspace can add its own
/// guidance through the agent's behaviour profile.
///
/// It is a starting playbook, not a statute. It is written from the common patterns in East African
/// motor, medical, life, property and agriculture claims and from how the Ugandan market works
/// (informal documentation, shared phones, mobile money, motorcycle taxis), and an insurer's
/// special investigations team should read it and extend it. Change <see cref="Version"/> whenever
/// the text changes, so a review records which briefing it was written under.
/// </summary>
public static class FraudPlaybook
{
    public const string Version = "uganda-fraud-playbook-v1";

    /// <summary>The code the agent uses for a concern the list below does not cover.</summary>
    public const string OtherCode = "OTHER";

    public static IReadOnlyList<FraudIndicator> Indicators { get; } =
    [
        // Motor
        new("MOT-EARLY-LOSS", "Motor", "Loss soon after cover began",
            "The accident, theft or fire is dated within days or weeks of the policy start, a renewal after a lapse, or the date premium was paid.",
            "A genuine early accident; a vehicle bought and insured the same week; a renewal paid late but in force.",
            "Compare the premium receipt time with the incident time, and check the policy history for a lapse."),
        new("MOT-STAGED", "Motor", "Accident that may have been arranged",
            "No independent witnesses, a quiet road at night, a collision with a vehicle that cannot be traced, the same people or garage appearing in earlier claims, or a description that does not fit the damage.",
            "Many real accidents on rural roads have no witnesses; hit-and-run is common.",
            "Ask for the police report and its date, a scene visit, and whether the other driver or vehicle appears in earlier claims."),
        new("MOT-REPAIR-INFLATION", "Motor", "Repair estimate that looks high for the damage",
            "An estimate far above what the photos show, parts priced above the usual market, items on the estimate that the photos do not show damaged, or a garage the claimant insisted on.",
            "Genuine hidden damage found on strip-down; imported parts that really cost more.",
            "Ask for an independent assessor's view or a second quote, and compare each estimate line with the photos."),
        new("MOT-PRE-EXISTING", "Motor", "Damage that may be older than the incident",
            "Rust or dirt in the scratches and dents, damage that matches an earlier claim on the same vehicle, or photos taken before the stated incident date.",
            "A vehicle that was already repaired badly after a genuine earlier claim.",
            "Check earlier claims on the same registration and the photo dates."),
        new("MOT-THEFT-ARSON", "Motor", "Total loss with weak circumstances",
            "Theft or fire where keys cannot be accounted for, there is no tracker or lock record, the owner is behind on a loan, or the police were told late.",
            "Genuine theft happens in the open; people also panic and delay.",
            "Ask for the key accounting, the finance status and the time between the loss and the police report."),
        new("MOT-TP-INJURY", "Motor", "Third-party injury claim that looks inflated",
            "More claimants than people in the vehicles, medical bills without treatment records, injuries not mentioned at the scene, or several claimants using the same lawyer or clinic.",
            "Passengers who were not first recorded; delayed symptoms.",
            "Ask for hospital records dated near the accident and the count of occupants."),
        new("MOT-BODA", "Motor", "Motorcycle taxi claim where owner, rider and insured differ",
            "The insured, the registered owner and the rider are three different people, or the same plate appears in several small claims.",
            "Motorcycles are commonly owned by one person and ridden by another.",
            "Check the registration book against the policy and the earlier claims on the plate."),
        new("MOT-CERT-AUTH", "Motor", "Certificate or sticker that does not match the policy record",
            "A motor certificate or sticker number, plate, chassis or dates that differ from the insurer's own policy register.",
            "A typing error at issue; a replacement certificate.",
            "Check the certificate against the insurer's own record, not the copy the claimant sent."),

        // Medical
        new("MED-INFLATED", "Medical", "Bill that looks high for the diagnosis",
            "Charges well above the usual for the diagnosis, treatment that does not match the diagnosis, several providers on the same day, or round-number bills.",
            "Complications; a referral to a private facility; emergency pricing.",
            "Compare against the provider's usual charges and ask for itemised bills with the treatment notes."),
        new("MED-PHANTOM", "Medical", "Treatment that may not have taken place",
            "Records that are identical across patients, treatment dated when the member was elsewhere, or no treatment notes behind the bill.",
            "A clerk copying a template; a late record.",
            "Ask the provider for the visit record and confirm with the member."),
        new("MED-WAITING", "Medical", "Claim inside a waiting period or for an undisclosed condition",
            "A major claim shortly after cover began, for a condition that is usually long-standing.",
            "A genuine sudden illness; a condition the member did not know about.",
            "Check the waiting period in the policy and what was disclosed on the application."),
        new("MED-IDENTITY", "Medical", "Member card that may have been used by someone else",
            "Different age, sex or appearance from the member on file, or a card used at the same time in two places.",
            "A dependant using the card with permission where the scheme allows it.",
            "Check identity details against the member record."),

        // Life and funeral
        new("LIFE-EARLY-DEATH", "Life", "Death soon after cover began or after cover was raised",
            "A death within a short period of inception, a reinstatement, or a sharp rise in the sum assured, especially after a long illness.",
            "A genuine sudden death; an illness the person did not know about.",
            "Compare the date of death with the application and the medical history the insured gave."),
        new("LIFE-DOCS", "Life", "Death documents that do not agree",
            "Names, dates or places that differ between the death certificate, the burial permit and the policy; signs of alteration; a change of beneficiary shortly before the death.",
            "Spelling variants of a name; clan and baptismal names used in different documents.",
            "Check each document against the policy and the issuing office."),
        new("LIFE-MULTI", "Life", "Same person covered under many funeral or credit-life covers",
            "Several small covers on one person with different insurers or different agents.",
            "People often take cover for funeral costs from more than one place.",
            "Ask for the other covers and check the amounts against the likely cost."),

        // Property
        new("PROP-FIRE", "Property", "Fire or loss where the circumstances look unusual",
            "A fire soon after cover began or was increased, a business in trouble, stock that does not fit the premises, or a contents list that grew between the first call and the claim form.",
            "Electrical faults and fires in crowded markets are common and genuine.",
            "Ask for the fire brigade or police report, the stock records and the earlier value on the policy."),
        new("PROP-BURGLARY", "Property", "Burglary without the usual signs",
            "No forced entry, expensive items without receipts or photos, or a long delay before reporting.",
            "Many households have no receipts; informal purchases are normal.",
            "Ask for the police report, other proof of ownership such as photos, and the timeline."),
        new("PROP-VALUE", "Property", "Claimed value that does not fit the documents",
            "A claim above the sum insured or above documented or market values, or the same receipt or valuation used on more than one claim.",
            "Replacement cost against depreciated value; a valuation done at a different time.",
            "Compare each claimed item with the policy schedule and independent values."),

        // Agriculture
        new("AGRI-AREA", "Agriculture", "Loss area or cause that does not fit the records",
            "More land claimed than was insured or registered, a loss when nearby farms report none, or a cause that the weather records do not support.",
            "Localised hail or pests; a farmer measuring land loosely.",
            "Check the insured acreage, the location and the weather data for the date."),
        new("AGRI-LIVESTOCK", "Agriculture", "Livestock loss with weak identification",
            "No veterinary report, tags or descriptions reused across claims, or an animal not on the insured list.",
            "Remote areas where a vet could not attend.",
            "Ask for the vet or local council confirmation and compare the animal's identification with earlier claims."),

        // Any line
        new("ANY-NARRATIVE", "Any", "Account that changes or cannot be true",
            "A story that differs between the first call, the form and the documents, a date that is after the claim or before the policy, or details that cannot all be true together.",
            "A stressed claimant; a translation difference.",
            "Put the versions side by side and ask the claimant to explain the difference."),
        new("ANY-IDENTITY", "Any", "Identity details that do not agree",
            "Different dates of birth or national ID details across documents, or a name that differs from the policyholder's beyond ordinary spelling.",
            "A recorded error; the same person known by different names.",
            "Check the details against the policy record and a document that was seen in person."),
        new("ANY-PAYEE", "Any", "Payment asked for someone other than the policyholder",
            "A request to pay a third party, or a mobile money wallet or bank account not in the claimant's name, or a sudden change of payment details.",
            "A relative collecting for an unwell person; a garage or clinic paid directly.",
            "Confirm the payee with the policyholder using a number already on file."),
        new("ANY-PRESSURE", "Any", "Pressure or reluctance",
            "Unusual urgency, refusal to allow an inspection, a refusal to provide a document that should be easy to get, or a demand for cash.",
            "Real financial hardship; distrust of insurers.",
            "Note what was asked and refused, and ask again in writing."),
        new("ANY-DOC-TAMPER", "Any", "Document or photo that may have been edited or reused",
            "The same photo or receipt on more than one claim, fonts or alignment that do not match the rest of the document, or file details that disagree with the stated date.",
            "A scanned copy that was re-saved; a template used by a small shop.",
            "Look at the picture checks on the claim and compare the document with the issuer."),
        new("ANY-INSIDER", "Any", "Pattern that may involve an intermediary",
            "The same agent, broker, garage or clinic on many unusual claims, or policy paperwork that is missing for claims arriving through one source.",
            "A busy intermediary who handles many claims.",
            "Compare the source of this claim with the source of the other flagged claims."),
        new("ANY-AML", "Any", "Money-handling pattern that deserves a second look",
            "A large claim or premium with an unclear source, unusually quick payment of a large premium followed by a claim, or a request to move proceeds to unrelated accounts.",
            "A genuine high-value policy; a business paying premium in advance.",
            "Follow the insurer's own anti-money-laundering review steps; this is advisory only."),
    ];

    /// <summary>The indicator with this code, or null (including for <see cref="OtherCode"/>).</summary>
    public static FraudIndicator? Find(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Indicators.FirstOrDefault(indicator => string.Equals(indicator.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownCode(string? code) =>
        string.Equals(code?.Trim(), OtherCode, StringComparison.OrdinalIgnoreCase) || Find(code) is not null;

    /// <summary>How the Ugandan setting changes what counts as unusual. Kept short and concrete so the agent applies it.</summary>
    public const string LocalContext =
        "How things work in Uganda, so you do not over-flag ordinary life: many small businesses and households keep no receipts, so a missing receipt alone is weak. " +
        "Family members often share one phone, so a shared phone number is only a weak sign unless it links claims with different surnames and unrelated incidents. " +
        "Mobile money wallets (MTN MoMo, Airtel Money) are common, and a wallet in a relative's name is often innocent; a changed payee late in the claim is worth a question. " +
        "Motorcycle taxis (boda boda) are common and the owner, rider and insured are often different people. " +
        "Names are written in several ways, with clan, baptismal and local-language variants, so small spelling differences are not a mismatch. " +
        "Rural roads and market fires often have no witnesses or paperwork. " +
        "The regulator is the Insurance Regulatory Authority, and money-laundering concerns are handled under the insurer's own anti-money-laundering process.";

    /// <summary>The playbook as it goes into a prompt: the codes the agent may use, what each looks like, the ordinary explanation, and what to check.</summary>
    public static string RenderForPrompt()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"INDICATORS (use these codes; playbook {Version}):");
        foreach (var indicator in Indicators)
        {
            builder.Append('[').Append(indicator.Code).Append("] ").Append(indicator.Line).Append(": ").Append(indicator.Label).AppendLine(".");
            builder.Append("  Looks like: ").AppendLine(indicator.WhatItLooksLike);
            builder.Append("  Often innocent: ").AppendLine(indicator.OrdinaryExplanations);
            builder.Append("  Check: ").AppendLine(indicator.WhatToCheck);
        }

        builder.Append('[').Append(OtherCode).AppendLine("] Use only when the facts show a real concern that none of the above describes.");
        return builder.ToString().TrimEnd();
    }
}

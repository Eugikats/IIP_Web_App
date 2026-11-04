namespace InsuranceIntelligence.Domain.Products;

/// <summary>
/// What the customer assistant may say when it explains a cover on WhatsApp (build-loop item 27): who provides it,
/// what it pays in plain words, who it is for, how long before it starts, which papers a claim needs and what it
/// costs. It is part of the product's version snapshot, so changing what the assistant says about a cover goes through
/// the same history and publish approval as changing the cover. A product with no offer is not sold on WhatsApp; the
/// assistant still handles claims on it. CustomerName is the name the customer hears; the product's own name is used when
/// it is empty.
/// </summary>
public sealed record ProductOffer(
    string Insurer,
    string Summary,
    string WhoItIsFor,
    string WaitingPeriod,
    string PapersForAClaim,
    IReadOnlyList<ProductOfferLevel> Levels,
    string? PolicyPrefix = null,
    string? Code = null,
    string? CustomerName = null);

/// <summary>One level of cover a customer can choose: the most it pays and its monthly price.</summary>
public sealed record ProductOfferLevel(decimal SumInsured, decimal MonthlyPremium);

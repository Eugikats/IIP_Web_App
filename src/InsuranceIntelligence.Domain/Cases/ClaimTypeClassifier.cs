namespace InsuranceIntelligence.Domain.Cases;

public static class ClaimTypeClassifier
{
    private static readonly HashSet<string> NegativeInjuryIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "no",
        "none",
        "n",
        "false",
        "0",
        "no injury",
        "no injuries",
        "none reported",
        "no injuries reported",
        "not injured",
        "nil"
    };

    /// <summary>
    /// This taxonomy (own damage / third-party property / theft-total-loss / injury-fatality) is
    /// motor-claim-specific. Applying its keyword matching to another product line's claim type
    /// text — e.g. a "Medical" claim's type routinely mentions "medical" — would misclassify it
    /// as InjuryFatality and force-escalate every medical claim regardless of actual severity.
    /// When <paramref name="productLine"/> is supplied and is not Motor, this returns Unknown
    /// immediately: the honest, safe result until that product line has its own governed routing
    /// (WO-15 deliberately keeps this scope narrow — see DocumentChecklistRules.ForProductLine
    /// and ClaimRoutingPolicy's Unknown-kind default for what "safe" means downstream).
    /// </summary>
    public static ClaimTypeKind Classify(string? claimType, MotorFnolDetails? motorFnolDetails = null, string? productLine = null)
    {
        if (!string.IsNullOrWhiteSpace(productLine) && !string.Equals(productLine, "Motor", StringComparison.OrdinalIgnoreCase))
        {
            return ClaimTypeKind.Unknown;
        }

        if (IndicatesInjuryOrFatality(claimType, motorFnolDetails))
        {
            return ClaimTypeKind.InjuryFatality;
        }

        var normalized = Normalize(claimType);
        if (string.IsNullOrEmpty(normalized))
        {
            return ClaimTypeKind.Unknown;
        }

        if (ContainsAny(normalized, "injury", "bodily", "fatality", "fatal", "death", "comesa", "medical"))
        {
            return ClaimTypeKind.InjuryFatality;
        }

        if (ContainsAny(normalized, "theft", "stolen", "total loss", "totalloss", "write off", "writeoff"))
        {
            return ClaimTypeKind.TheftTotalLoss;
        }

        if (ContainsAny(normalized, "third party", "third-party", "thirdparty", "tppd", "property damage"))
        {
            return ClaimTypeKind.ThirdPartyProperty;
        }

        if (ContainsAny(normalized, "own damage", "owndamage", "own-damage", "comprehensive", "accident", "windscreen", "glass", "collision"))
        {
            return ClaimTypeKind.OwnDamage;
        }

        return ClaimTypeKind.Unknown;
    }

    public static bool IndicatesInjuryOrFatality(string? claimType, MotorFnolDetails? motorFnolDetails)
    {
        var injury = motorFnolDetails?.InjuryIndicator?.Trim();
        if (!string.IsNullOrWhiteSpace(injury) && !NegativeInjuryIndicators.Contains(injury))
        {
            return true;
        }

        var normalized = Normalize(claimType);
        return ContainsAny(normalized, "injury", "bodily", "fatality", "fatal", "death", "comesa");
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));
}

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

    public static ClaimTypeKind Classify(string? claimType, MotorFnolDetails? motorFnolDetails = null)
    {
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

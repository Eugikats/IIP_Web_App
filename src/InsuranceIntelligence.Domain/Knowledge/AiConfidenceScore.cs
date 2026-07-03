namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record AiConfidenceScore(decimal Value)
{
    public static AiConfidenceScore Create(decimal value) =>
        new(Math.Clamp(Math.Round(value, 2), 0m, 1m));
}

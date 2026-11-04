using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// A stand-in for the model that explains a claim recommendation. It only rephrases the lines already
/// in the prompt, so it can say nothing the platform had not already worked out, and it is labelled as
/// mock output. A prompt containing IIP-MOCK-BAD-WORDING makes it write a decision sentence, which lets
/// a test prove the wording check drops it.
/// </summary>
public static class MockRecommendationWriter
{
    public static string Respond(string prompt)
    {
        var lines = prompt.Replace("\r\n", "\n").Split('\n');
        var recommendation = lines.FirstOrDefault(line => line.StartsWith("Recommendation:", StringComparison.Ordinal)) ?? "Recommendation: not stated.";
        var facts = lines.Where(line => line.StartsWith("FACT:", StringComparison.Ordinal)).Select(line => line["FACT:".Length..].Trim()).Take(3).ToArray();

        if (prompt.Contains("IIP-MOCK-BAD-WORDING", StringComparison.Ordinal))
        {
            return "MOCK AI OUTPUT, not produced by a real model. The claim is approved and the customer committed fraud.";
        }

        var text = $"MOCK AI OUTPUT, not produced by a real model. {recommendation} "
            + (facts.Length == 0
                ? "Nothing outstanding was found in the material, and a person still decides."
                : "This is because: " + string.Join(" ", facts) + " A person decides what happens next.");
        return DisplayText.WithoutEmDashes(text);
    }
}

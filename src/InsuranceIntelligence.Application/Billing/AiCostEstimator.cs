namespace InsuranceIntelligence.Application.Billing;

/// <summary>
/// Estimates Azure OpenAI cost in USD from token counts using published list prices.
/// All figures are approximations — actual billing comes from the Azure Cost Management portal.
/// Prices are per 1 000 tokens (as of the gpt-4o family, 2024-08 vintage).
/// </summary>
public static class AiCostEstimator
{
    // gpt-4o (2024-08-06) list prices per 1 000 tokens
    private const decimal PromptPricePer1K = 0.0025m;
    private const decimal CompletionPricePer1K = 0.010m;

    /// <summary>
    /// Estimates cost in USD. Returns null when either token count is unavailable.
    /// </summary>
    public static decimal? Estimate(int? promptTokens, int? completionTokens)
    {
        if (promptTokens is null || completionTokens is null)
        {
            return null;
        }

        var promptCost = promptTokens.Value / 1000m * PromptPricePer1K;
        var completionCost = completionTokens.Value / 1000m * CompletionPricePer1K;
        return Math.Round(promptCost + completionCost, 8);
    }
}

using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class ProtectedTokenMaskerTests
{
    [Fact]
    public void A_policy_number_is_masked_and_restored_unchanged()
    {
        const string text = "My policy is NSA/MO/2026/00033, please help.";

        var (masked, tokens) = ProtectedTokenMasker.Mask(text);
        var restored = ProtectedTokenMasker.Unmask(masked, tokens);

        Assert.DoesNotContain("NSA/MO/2026/00033", masked);
        Assert.Equal(text, restored);
    }

    [Fact]
    public void An_amount_a_date_and_a_phone_number_all_survive_a_round_trip()
    {
        const string text = "We owe UGX 450,000 for the claim opened on 2026-09-20, call +256772123456.";

        var (masked, tokens) = ProtectedTokenMasker.Mask(text);
        var restored = ProtectedTokenMasker.Unmask(masked, tokens);

        Assert.DoesNotContain("450,000", masked);
        Assert.DoesNotContain("2026-09-20", masked);
        Assert.DoesNotContain("256772123456", masked);
        Assert.Equal(text, restored);
        Assert.Equal(3, tokens.Count);
    }

    [Fact]
    public void Text_with_nothing_to_protect_is_unchanged()
    {
        const string text = "Please share more details about the accident.";

        var (masked, tokens) = ProtectedTokenMasker.Mask(text);

        Assert.Equal(text, masked);
        Assert.Empty(tokens);
    }
}

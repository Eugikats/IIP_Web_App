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

    [Fact]
    public void A_secure_link_and_a_claim_reference_survive_a_round_trip()
    {
        const string text = "Your reference is CLM-4E1A92C7. Upload documents at https://portal.example.com/access/verify?token=abc123-XYZ.";

        var (masked, tokens) = ProtectedTokenMasker.Mask(text);

        Assert.DoesNotContain("CLM-4E1A92C7", masked);
        Assert.DoesNotContain("portal.example.com", masked);
        Assert.Equal(text, ProtectedTokenMasker.Unmask(masked, tokens));
        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public void A_link_is_not_split_by_the_phone_number_pattern()
    {
        const string text = "Open https://portal.example.com/access/verify?token=0123456789012345 now";

        var (masked, tokens) = ProtectedTokenMasker.Mask(text);

        Assert.Single(tokens);
        Assert.Equal(text, ProtectedTokenMasker.Unmask(masked, tokens));
    }

    [Fact]
    public void Punctuation_after_a_link_is_not_swallowed_into_it()
    {
        var (masked, tokens) = ProtectedTokenMasker.Mask("See https://portal.example.com/access.");

        Assert.EndsWith(".", masked);
        Assert.Equal("https://portal.example.com/access", Assert.Single(tokens).Value);
    }

    [Fact]
    public void A_protected_literal_such_as_a_first_name_is_masked_only_as_a_whole_word()
    {
        var (masked, tokens) = ProtectedTokenMasker.Mask("Thank you, Aisha. Aishatu is someone else.", ["Aisha"]);

        Assert.Contains("Aishatu", masked);
        Assert.DoesNotContain("Thank you, Aisha.", masked);
        Assert.Equal("Aisha", Assert.Single(tokens).Value);
    }

    [Fact]
    public void Placeholders_are_plain_ASCII_so_the_model_cannot_strip_them()
    {
        var (masked, tokens) = ProtectedTokenMasker.Mask("Policy NSA/MO/2026/00033");

        Assert.All(masked, character => Assert.True(character < 128));
        Assert.Equal("[TKN0]", Assert.Single(tokens).Key);
    }
}

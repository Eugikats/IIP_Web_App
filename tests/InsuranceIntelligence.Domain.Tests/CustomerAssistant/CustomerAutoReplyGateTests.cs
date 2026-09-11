using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests.CustomerAssistant;

public sealed class CustomerAutoReplyGateTests
{
    [Fact]
    public void IsAutoReplyPermitted_WhenEveryConditionMet_ReturnsTrue() =>
        Assert.True(CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion, hasApprovedCitation: true, carriesCustomerSpecificAssertion: false,
            replyLength: 100, maxResponseCharacters: 320, tenantOptedIntoAutoReply: true));

    [Theory]
    [InlineData(CustomerIntent.ProductInterest)]
    [InlineData(CustomerIntent.NewClaim)]
    [InlineData(CustomerIntent.ExistingClaimStatus)]
    [InlineData(CustomerIntent.HumanRequested)]
    [InlineData(CustomerIntent.Unknown)]
    public void IsAutoReplyPermitted_ForAnyIntentOtherThanGeneralQuestion_ReturnsFalse(CustomerIntent intent) =>
        Assert.False(CustomerAutoReplyGate.IsAutoReplyPermitted(
            intent, hasApprovedCitation: true, carriesCustomerSpecificAssertion: false,
            replyLength: 100, maxResponseCharacters: null, tenantOptedIntoAutoReply: true));

    [Fact]
    public void IsAutoReplyPermitted_WithoutAnApprovedCitation_ReturnsFalse() =>
        Assert.False(CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion, hasApprovedCitation: false, carriesCustomerSpecificAssertion: false,
            replyLength: 100, maxResponseCharacters: null, tenantOptedIntoAutoReply: true));

    [Fact]
    public void IsAutoReplyPermitted_WithACustomerSpecificAssertion_ReturnsFalse() =>
        Assert.False(CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion, hasApprovedCitation: true, carriesCustomerSpecificAssertion: true,
            replyLength: 100, maxResponseCharacters: null, tenantOptedIntoAutoReply: true));

    [Fact]
    public void IsAutoReplyPermitted_WhenReplyExceedsMaxResponseCharacters_ReturnsFalse() =>
        Assert.False(CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion, hasApprovedCitation: true, carriesCustomerSpecificAssertion: false,
            replyLength: 400, maxResponseCharacters: 320, tenantOptedIntoAutoReply: true));

    [Fact]
    public void IsAutoReplyPermitted_WithNoMaxResponseCharactersConfigured_LengthNeverBlocksIt() =>
        Assert.True(CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion, hasApprovedCitation: true, carriesCustomerSpecificAssertion: false,
            replyLength: 10_000, maxResponseCharacters: null, tenantOptedIntoAutoReply: true));

    [Fact]
    public void IsAutoReplyPermitted_WhenTenantHasNotOptedIn_ReturnsFalseEvenIfEveryOtherConditionIsMet() =>
        Assert.False(CustomerAutoReplyGate.IsAutoReplyPermitted(
            CustomerIntent.GeneralQuestion, hasApprovedCitation: true, carriesCustomerSpecificAssertion: false,
            replyLength: 50, maxResponseCharacters: 320, tenantOptedIntoAutoReply: false));
}

using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests.CustomerAssistant;

public sealed class KeywordCustomerIntentClassifierTests
{
    private readonly KeywordCustomerIntentClassifier classifier = new();

    [Theory]
    [InlineData("I was in an accident yesterday and my car is damaged")]
    [InlineData("I want to make a claim for the fire damage at my shop")]
    [InlineData("Someone hit my car in the parking lot")]
    [InlineData("My motorbike was stolen last night")]
    public void Classify_AccidentAndDamageMessages_ReturnsNewClaim(string message) =>
        Assert.Equal(CustomerIntent.NewClaim, classifier.Classify(message));

    [Theory]
    [InlineData("What is the status of my claim?")]
    [InlineData("Can you give me an update on my claim please")]
    [InlineData("I want to track my claim reference NSA/MO/2026/00009")]
    public void Classify_StatusInquiries_ReturnsExistingClaimStatus(string message) =>
        Assert.Equal(CustomerIntent.ExistingClaimStatus, classifier.Classify(message));

    [Theory]
    [InlineData("I'm interested in getting motor insurance")]
    [InlineData("How much is a comprehensive policy?")]
    [InlineData("I want to buy travel insurance for next month")]
    public void Classify_PurchaseInterest_ReturnsProductInterest(string message) =>
        Assert.Equal(CustomerIntent.ProductInterest, classifier.Classify(message));

    [Theory]
    [InlineData("Can I speak to a human please")]
    [InlineData("I want to talk to an agent")]
    [InlineData("Please have customer service call me")]
    public void Classify_EscalationRequests_ReturnsHumanRequested(string message) =>
        Assert.Equal(CustomerIntent.HumanRequested, classifier.Classify(message));

    [Theory]
    [InlineData("What does my comprehensive motor policy cover?")]
    [InlineData("How do I renew my policy?")]
    [InlineData("Does my policy cover windscreen replacement?")]
    public void Classify_GenericCoverageQuestions_ReturnsGeneralQuestion(string message) =>
        Assert.Equal(CustomerIntent.GeneralQuestion, classifier.Classify(message));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("hi")]
    [InlineData("ok thanks")]
    public void Classify_EmptyOrUnclassifiableMessages_ReturnsUnknown(string? message) =>
        Assert.Equal(CustomerIntent.Unknown, classifier.Classify(message!));

    [Fact]
    public void Classify_ClaimIntentTakesPrecedenceOverHumanRequestedInTheSameMessage()
    {
        // Precedence matters: a claim-related message that also asks for a human should still
        // route as NewClaim, not get swallowed by the more generic escalation intent.
        var result = classifier.Classify("I had an accident, can I speak to a human about my claim?");
        Assert.Equal(CustomerIntent.NewClaim, result);
    }
}

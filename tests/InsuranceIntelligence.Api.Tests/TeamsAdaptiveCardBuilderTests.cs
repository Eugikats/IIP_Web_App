using System;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Notifications;
using Xunit;
using System.Text.Json;

namespace InsuranceIntelligence.Api.Tests;

public class TeamsAdaptiveCardBuilderTests
{
    [Fact]
    public void BuildCard_ProducesValidAdaptiveCard_WithFactSetAndDeepLink()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var scope = new TenantScope("tenant1", "workspace1");
        
        var payloadJson = JsonSerializer.Serialize(new
        {
            ApprovalId = Guid.NewGuid(),
            ApprovalType = "Settlement",
            RiskLevel = "High",
            EvidenceSummary = "Large payout requested",
            RequestedByUserId = "nic.kings@example.com"
        });

        var notification = new TeamsNotification
        {
            Id = notificationId,
            Scope = scope,
            CaseId = caseId,
            NotificationType = TeamsNotificationType.ApprovalRequired,
            PayloadJson = payloadJson,
            DeepLinkUrl = "https://iip.example.com/app/work-items"
        };

        // Act
        var cardJson = TeamsAdaptiveCardBuilder.BuildCard(notification);

        // Assert
        Assert.NotNull(cardJson);
        Assert.Contains("AdaptiveCard", cardJson);
        Assert.Contains(caseId.ToString(), cardJson);
        Assert.Contains("Approval Required", cardJson);
        Assert.Contains("https://iip.example.com/app/work-items", cardJson);
        Assert.Contains("Settlement", cardJson);
        Assert.Contains("High", cardJson);
        Assert.Contains("nic.kings@example.com", cardJson);
    }

    // No production caller sets DeepLinkUrl today and the property defaults to string.Empty, so the
    // builder's old `?? "https://localhost:3000/approvals"` fallback never fired and every real card
    // shipped a "View in Portal" button whose url was "". A button that opens nothing is a dead
    // control (I2) — the card must simply omit the action until a real deep link exists.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // Not http(s) -- a Teams client can't open it. Also a regression pin: Uri.TryCreate(...,
    // UriKind.Absolute, ...) alone accepts this as a valid absolute file: URI on Linux/macOS
    // (POSIX absolute path) while rejecting it on Windows, so the scheme check is what must
    // reject it here, identically on every platform.
    [InlineData("/app/work-items")]
    public void BuildCard_OmitsTheViewAction_WhenThereIsNoAbsoluteDeepLink(string deepLinkUrl)
    {
        var notification = new TeamsNotification
        {
            Id = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = Guid.NewGuid(),
            NotificationType = TeamsNotificationType.ApprovalRequired,
            PayloadJson = JsonSerializer.Serialize(new { ApprovalType = "Settlement" }),
            DeepLinkUrl = deepLinkUrl
        };

        var cardJson = TeamsAdaptiveCardBuilder.BuildCard(notification);

        Assert.DoesNotContain("Action.OpenUrl", cardJson);
        Assert.DoesNotContain("View in Portal", cardJson);
        Assert.DoesNotContain("localhost", cardJson);
        // The card itself still renders — only the dead button is withheld.
        Assert.Contains("Approval Required", cardJson);
    }

    [Theory]
    [InlineData("https://iip.example.com/app/claims/00000000-0000-0000-0000-000000000001")]
    [InlineData("http://localhost:3000/app/work-items")]
    public void BuildCard_IncludesTheViewAction_ForAGenuineHttpOrHttpsDeepLink(string deepLinkUrl)
    {
        var notification = new TeamsNotification
        {
            Id = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = Guid.NewGuid(),
            NotificationType = TeamsNotificationType.ApprovalRequired,
            PayloadJson = JsonSerializer.Serialize(new { ApprovalType = "Settlement" }),
            DeepLinkUrl = deepLinkUrl
        };

        var cardJson = TeamsAdaptiveCardBuilder.BuildCard(notification);

        Assert.Contains("Action.OpenUrl", cardJson);
        Assert.Contains("View in Portal", cardJson);
        Assert.Contains(deepLinkUrl, cardJson);
    }
}

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
            DeepLinkUrl = "http://localhost:3000/approvals"
        };

        // Act
        var cardJson = TeamsAdaptiveCardBuilder.BuildCard(notification);

        // Assert
        Assert.NotNull(cardJson);
        Assert.Contains("AdaptiveCard", cardJson);
        Assert.Contains(caseId.ToString(), cardJson);
        Assert.Contains("Approval Required", cardJson);
        Assert.Contains("http://localhost:3000/approvals", cardJson);
        Assert.Contains("Settlement", cardJson);
        Assert.Contains("High", cardJson);
        Assert.Contains("nic.kings@example.com", cardJson);
    }
}

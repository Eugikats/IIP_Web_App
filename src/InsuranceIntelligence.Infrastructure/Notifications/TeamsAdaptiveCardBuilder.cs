using System.Text.Json;
using System.Text.Json.Nodes;
using InsuranceIntelligence.Domain.Notifications;

namespace InsuranceIntelligence.Infrastructure.Notifications;

public static class TeamsAdaptiveCardBuilder
{
    public static string BuildCard(TeamsNotification notification)
    {
        var facts = new JsonArray();

        // Extract some base details from payload if possible
        var parsed = JsonNode.Parse(notification.PayloadJson);
        if (parsed != null)
        {
            if (parsed["RiskLevel"]?.ToString() is string risk)
                facts.Add(new JsonObject { ["title"] = "Risk Level", ["value"] = risk });
            
            if (parsed["ApprovalType"]?.ToString() is string type)
                facts.Add(new JsonObject { ["title"] = "Type", ["value"] = type });
            
            if (parsed["RequestedByUserId"]?.ToString() is string user)
                facts.Add(new JsonObject { ["title"] = "Requested By", ["value"] = user });
        }

        var card = new JsonObject
        {
            ["type"] = "AdaptiveCard",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["version"] = "1.5",
            ["body"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = GetTitle(notification.NotificationType),
                    ["weight"] = "Bolder",
                    ["size"] = "Medium"
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = $"Case ID: {notification.CaseId}",
                    ["isSubtle"] = true,
                    ["wrap"] = true
                },
                new JsonObject
                {
                    ["type"] = "FactSet",
                    ["facts"] = facts
                }
            },
        };

        // No caller currently supplies a deep link, and TeamsNotification.DeepLinkUrl defaults to
        // string.Empty, so the previous `?? "https://localhost:3000/approvals"` fallback never
        // fired and every card shipped a "View in Portal" button with an empty url. Render the
        // action only when there is a real absolute link to open: a button that goes nowhere is a
        // dead control, and a localhost default would be worse than no button in a real tenant.
        //
        // UriKind.Absolute alone is not enough: on Linux/macOS, Uri.TryCreate accepts a POSIX
        // absolute path (e.g. "/app/work-items") as a valid absolute file: URI, while the same
        // call returns false on Windows -- a platform-dependent result for the same input. A Teams
        // client can only open http(s) links, so the scheme must be checked explicitly to get
        // identical behavior on every platform.
        if (!string.IsNullOrWhiteSpace(notification.DeepLinkUrl)
            && Uri.TryCreate(notification.DeepLinkUrl, UriKind.Absolute, out var deepLinkUri)
            && deepLinkUri.Scheme is "http" or "https")
        {
            card["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Action.OpenUrl",
                    ["title"] = "View in Portal",
                    ["url"] = notification.DeepLinkUrl
                }
            };
        }

        return card.ToJsonString();
    }

    private static string GetTitle(TeamsNotificationType type) => type switch
    {
        TeamsNotificationType.ApprovalRequired => "Approval Required",
        TeamsNotificationType.SlaAtRisk => "SLA at Risk",
        TeamsNotificationType.SlaBreached => "SLA Breached",
        TeamsNotificationType.CustomerUpdatePending => "Customer Update Pending",
        TeamsNotificationType.ClaimEscalated => "Claim Escalated",
        TeamsNotificationType.DocumentReviewNeeded => "Document Review Needed",
        TeamsNotificationType.ReviewRequested => "Review Requested",
        TeamsNotificationType.ClaimAssigned => "Claim Assigned",
        _ => "Notification"
    };
}

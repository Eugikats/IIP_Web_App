using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Tests;

// Drafts are persisted as JSON. Rows written before outcome D carry neither shortMessageBody nor
// creationMethod; when both were `required`, every such row threw on read and took down My Work,
// the customer update queue, the operations summary and the command center on Azure dev.
public sealed class CustomerUpdateDraftStoredJsonTests
{
    // Mirrors Infrastructure.Persistence.JsonSerializerSettings.Options (which is internal).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Draft_saved_before_outcome_D_reads_back_with_safe_defaults()
    {
        var node = JsonSerializer.SerializeToNode(NewDraft(shortBody: "short", method: CustomerUpdateCreationMethod.Template), Json)!.AsObject();
        node.Remove("shortMessageBody");
        node.Remove("creationMethod");

        var draft = JsonSerializer.Deserialize<CustomerUpdateDraft>(node.ToJsonString(), Json)!;

        Assert.Equal("Your documents were received.", draft.ShortMessageBody);
        Assert.Equal(CustomerUpdateCreationMethod.AiDrafted, draft.CreationMethod);
    }

    [Fact]
    public void Draft_with_a_distinct_short_form_round_trips_it()
    {
        var json = JsonSerializer.Serialize(NewDraft(shortBody: "Docs received.", method: CustomerUpdateCreationMethod.ManuallyWritten), Json);

        var draft = JsonSerializer.Deserialize<CustomerUpdateDraft>(json, Json)!;

        Assert.Equal("Docs received.", draft.ShortMessageBody);
        Assert.Equal(CustomerUpdateCreationMethod.ManuallyWritten, draft.CreationMethod);
    }

    private static CustomerUpdateDraft NewDraft(string shortBody, CustomerUpdateCreationMethod method) => new()
    {
        UpdateId = Guid.NewGuid(),
        Scope = TenantScope.Create("tenant-a", "workspace-a"),
        CaseId = Guid.NewGuid(),
        ReasonCategory = default,
        Channel = default,
        Status = default,
        MessageBody = "Your documents were received.",
        ShortMessageBody = shortBody,
        CreationMethod = method,
        RequiresApproval = true,
        Sensitive = false,
        CreatedByUserId = "user-a",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        CorrelationId = "corr-a",
    };
}

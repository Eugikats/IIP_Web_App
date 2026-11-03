using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ChannelTimelineRepositoryTests
{
    [Fact]
    public async Task GenericMessagesAreQueriedByVerifiedRecipientWithinTenantAndWorkspace()
    {
        var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"channel-timeline-{Guid.NewGuid():N}")
            .Options;
        var scope = TenantScope.Create("insurer-a", "claims");
        var otherScope = TenantScope.Create("insurer-b", "claims");
        var accountId = "social-page-1";
        var recipientId = "opaque-recipient-1";
        await using var db = new InsuranceIntelligenceDbContext(options);
        var repository = new EfChannelMessageRepository(db);
        await repository.SaveAsync(Message(scope, accountId, recipientId, "Private message A"));
        await repository.SaveAsync(Message(scope, accountId, "different-recipient", "Private message B"));
        await repository.SaveAsync(Message(otherScope, accountId, recipientId, "Other tenant message"));

        var visible = await repository.ListForExternalRecipientAsync(scope, "x", accountId, recipientId);

        var message = Assert.Single(visible);
        Assert.Equal("Private message A", message.CustomerSafeBody);
        Assert.Empty(await repository.ListForExternalRecipientAsync(TenantScope.Create("insurer-a", "sales"), "x", accountId, recipientId));
    }

    [Fact]
    public async Task WhatsAppMessagesAreQueriedByVerifiedRecipientWithinTenantAndWorkspace()
    {
        var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"whatsapp-timeline-{Guid.NewGuid():N}")
            .Options;
        var scope = TenantScope.Create("insurer-a", "claims");
        var otherScope = TenantScope.Create("insurer-b", "claims");
        var accountId = "business-phone-1";
        var recipientId = "opaque-wa-recipient-1";
        await using var db = new InsuranceIntelligenceDbContext(options);
        var repository = new EfWhatsAppMessageRepository(db);
        await repository.SaveAsync(WhatsAppMessage(scope, accountId, recipientId, "Private WhatsApp A"));
        await repository.SaveAsync(WhatsAppMessage(scope, accountId, "different-recipient", "Private WhatsApp B"));
        await repository.SaveAsync(WhatsAppMessage(otherScope, accountId, recipientId, "Other tenant WhatsApp"));

        var visible = await repository.ListForRecipientAsync(scope, accountId, recipientId);

        var message = Assert.Single(visible);
        Assert.Equal("Private WhatsApp A", message.CustomerSafeBody);
        Assert.Empty(await repository.ListForRecipientAsync(TenantScope.Create("insurer-a", "sales"), accountId, recipientId));
    }

    private static ChannelMessage Message(TenantScope scope, string accountId, string recipientId, string body) => new()
    {
        MessageId = Guid.NewGuid(), Scope = scope, Channel = "x", AccountId = accountId,
        ThreadId = $"thread-{Guid.NewGuid():N}", ExternalRecipientId = recipientId,
        Direction = ChannelMessageDirection.Inbound, ContentType = "text", CustomerSafeBody = body,
        ProviderMessageId = $"provider-{Guid.NewGuid():N}", DeliveryState = ChannelMessageDeliveryState.Received,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid().ToString("N"),
    };

    private static InsuranceIntelligence.Domain.Integrations.WhatsAppMessage WhatsAppMessage(
        TenantScope scope, string accountId, string recipientId, string body) => new()
    {
        MessageId = Guid.NewGuid(), Scope = scope, ThreadId = $"thread-{Guid.NewGuid():N}",
        BusinessPhoneNumberId = accountId, ContactWaId = recipientId,
        Direction = InsuranceIntelligence.Domain.Integrations.WhatsAppMessageDirection.Inbound,
        ContentType = InsuranceIntelligence.Domain.Integrations.WhatsAppContentType.Text,
        CustomerSafeBody = body, DeliveryState = InsuranceIntelligence.Domain.Integrations.WhatsAppDeliveryState.Received,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid().ToString("N"),
    };
}

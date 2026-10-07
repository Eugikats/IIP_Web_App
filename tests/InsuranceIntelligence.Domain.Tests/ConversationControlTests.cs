using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ConversationControlTests
{
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static CustomerConversationTurn Turn(CustomerAssistantOutcome outcome, TimeSpan ago) => new()
    {
        TurnId = Guid.NewGuid(),
        Scope = Scope,
        ThreadId = "thread-1",
        ContactWaId = "256776678989",
        InboundMessageText = string.Empty,
        ClassifiedIntent = CustomerIntent.Unknown,
        Outcome = outcome,
        OccurredAt = Now - ago,
        CorrelationId = "c",
    };

    [Fact]
    public void A_conversation_nobody_took_is_Katos() =>
        Assert.Equal(ConversationMode.Kato, ConversationControl.ModeOf([Turn(CustomerAssistantOutcome.AutoReplied, TimeSpan.FromMinutes(5))], Now));

    [Fact]
    public void A_person_who_took_it_keeps_it()
    {
        var turns = new[] { Turn(CustomerAssistantOutcome.AutoReplied, TimeSpan.FromMinutes(30)), Turn(CustomerAssistantOutcome.HumanTookOver, TimeSpan.FromMinutes(10)) };

        Assert.Equal(ConversationMode.Person, ConversationControl.ModeOf(turns, Now));
    }

    [Fact]
    public void Customer_messages_held_for_the_person_do_not_extend_it()
    {
        var turns = new[] { Turn(CustomerAssistantOutcome.HumanTookOver, TimeSpan.FromHours(3)), Turn(CustomerAssistantOutcome.HeldForPerson, TimeSpan.FromMinutes(1)) };

        Assert.Equal(ConversationMode.Kato, ConversationControl.ModeOf(turns, Now));
    }

    [Fact]
    public void A_reply_from_the_person_renews_it()
    {
        var turns = new[] { Turn(CustomerAssistantOutcome.HumanTookOver, TimeSpan.FromHours(3)), Turn(CustomerAssistantOutcome.HumanReplied, TimeSpan.FromMinutes(20)) };

        Assert.Equal(ConversationMode.Person, ConversationControl.ModeOf(turns, Now));
    }

    [Fact]
    public void Handing_back_returns_it_to_Kato_at_once()
    {
        var turns = new[] { Turn(CustomerAssistantOutcome.HumanReplied, TimeSpan.FromMinutes(20)), Turn(CustomerAssistantOutcome.HandedBackToKato, TimeSpan.FromMinutes(1)) };

        Assert.Equal(ConversationMode.Kato, ConversationControl.ModeOf(turns, Now));
    }

    [Fact]
    public void A_person_who_goes_quiet_does_not_leave_the_customer_unanswered()
    {
        var turns = new[] { Turn(CustomerAssistantOutcome.HumanReplied, ConversationControl.IdleHandBack + TimeSpan.FromMinutes(1)) };

        Assert.Equal(ConversationMode.Kato, ConversationControl.ModeOf(turns, Now));
    }
}

public sealed class LeadMergeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static SalesLeadDetailReadModel Lead(string id, string name, DateTimeOffset created) => new(
        id, "WhatsApp", "need", "summary", "To be assessed", "New", [], [], null, null, name, "+256776678989", created, "system:test")
    {
        Phone = "256776678989",
    };

    [Fact]
    public void The_oldest_record_is_kept_and_nothing_recorded_on_the_others_is_lost()
    {
        var first = Lead("LEAD-1", "WhatsApp customer", T0);
        var second = Lead("LEAD-2", "Denis Kabagambi", T0.AddHours(1)) with
        {
            AssignedSalesOfficer = "user-7",
            ProductFit = "Hospital Cash",
            Facts = [new ProspectFact("occupation", "boda rider", ProspectFactSource.Customer, T0.AddHours(1))],
            Interests = [new ProspectInterest("Hospital Cash", InterestStrength.Interested, "asked", T0.AddHours(1))],
            Activity = [new ProspectActivity(T0.AddHours(1), "Asked about cover", "Kato")],
            BankCustomer = true,
        };
        var third = Lead("LEAD-3", "Denis Kabagambi", T0.AddHours(2)) with
        {
            Facts = [new ProspectFact("occupation", "boda rider and mechanic", ProspectFactSource.Customer, T0.AddHours(2)), new ProspectFact("area", "Kawempe", ProspectFactSource.Customer, T0.AddHours(2))],
            Interests = [new ProspectInterest("Hospital Cash", InterestStrength.WantsIt, "decided", T0.AddHours(2))],
            Consent = new ContactConsent(ContactConsentStatus.Agreed, T0.AddHours(2), "May we message you later?", "WhatsApp"),
            Activity = [new ProspectActivity(T0.AddHours(2), "Agreed to follow-up", "Kato")],
            ThreadId = "thread-9",
        };

        var merged = LeadMerge.Merge([first, second, third]);

        Assert.Equal("LEAD-1", merged.LeadId);
        Assert.Equal("Denis Kabagambi", merged.CustomerName);
        Assert.Equal("user-7", merged.AssignedSalesOfficer);
        Assert.Equal("Hospital Cash", merged.ProductFit);
        Assert.Equal("boda rider and mechanic", merged.Facts.Single(fact => fact.Key == "occupation").Value);
        Assert.Equal("Kawempe", merged.Facts.Single(fact => fact.Key == "area").Value);
        Assert.Equal(InterestStrength.WantsIt, Assert.Single(merged.Interests).Strength);
        Assert.Equal(ContactConsentStatus.Agreed, merged.Consent!.Status);
        Assert.Equal(["Asked about cover", "Agreed to follow-up"], merged.Activity.Select(entry => entry.Text));
        Assert.Equal("thread-9", merged.ThreadId);
        Assert.True(merged.BankCustomer);
        Assert.Equal(T0, merged.CreatedAt);
    }
}

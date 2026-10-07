using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.DemoSeeding;

/// <summary>
/// Gives four of the invented bank customers a WhatsApp conversation with Kato, with the assistant's
/// reading of each one, so the chat screen has something to show before anyone texts. Everything is
/// invented: the people, the numbers (a range that belongs to no subscriber) and the words. The
/// conversations live on a demonstration number id, so a reply typed into one can never reach a real
/// phone. Demo profiles only; re-running replaces the same records.
/// </summary>
public sealed class DemoConversationSeeder(
    ISalesLeadReadRepository leadRead,
    ISalesLeadWriteRepository leadWrite,
    IWhatsAppMessageRepository messages,
    ICustomerConversationRepository turns)
{
    public const string DemoNumberId = "demo-number";
    public const string KatoId = "system:customer-assistant";

    public sealed record Result(int Conversations, int Messages);

    private sealed record Line(bool FromCustomer, string Text, int MinutesAfterStart);

    private sealed record Script(
        string LeadId,
        int StartedMinutesAgo,
        Line[] Lines,
        string Summary,
        string Tone,
        (string Product, InterestStrength Strength, string Why, int[] Refs)[] Interests,
        (string Text, int[] Refs)[] Worries,
        (string Text, int[] Refs)[] Doubts,
        (string Text, int[] Refs)? NextStep,
        (string Key, string Value, int Ref)[] Facts);

    private static readonly Script[] Scripts =
    [
        new("LEAD-BANK-001", 95,
        [
            new(true, "Hello, I got a message from the bank about insurance for my salon", 0),
            new(false, "Hello Aisha, I am Kato, Mavuno Bank's insurance assistant. Yes, we can help. Tell me a little about your salon: do you work from your own premises?", 1),
            new(true, "Yes I rent a small shop in Ntinda. Two chairs, a dryer, a fridge and products. Thieves broke into the shop next door last month", 3),
            new(false, "I am sorry to hear that, that must have been worrying. Small Business Shop Cover from Nile Shield Assurance pays for your equipment and stock if they are stolen in a break-in or burnt in a fire. With about UGX 4,000,000 of equipment and stock, the UGX 4,000,000 level costs UGX 9,000 a month, about UGX 300 a day. You pay the first UGX 100,000 of a claim yourself.", 5),
            new(true, "That is okay. But what if they say it was not a real break in", 8),
            new(false, "A break-in has to be reported to the police, and the claims team reviews the police report and your list of what was lost. Cover starts 7 days after you join. Would you like me to set it up for you?", 10),
            new(true, "Let me think about it, I can pay maybe 10,000 a month. Can you message me again on Friday", 14),
        ],
        "Salon owner in Ntinda who rents her premises and holds around UGX 4,000,000 of equipment and stock. A break-in next door last month made her think about cover.",
        "curious, cautious",
        [(MicroinsuranceCatalog.ShopCover.Name, InterestStrength.Interested, "Wants her salon equipment and stock covered after a nearby break-in.", [2, 3])],
        [("A neighbouring shop was broken into last month.", [2])],
        [("Unsure a break-in claim would be accepted.", [3])],
        ("Follow up on Friday as she asked, with how a break-in claim is proven, and confirm the UGX 9,000 level fits her UGX 10,000 budget.", [4]),
        [("occupation", "salon owner", 1), ("area", "Ntinda", 2), ("monthly_budget", "maybe 10,000 a month", 4), ("worries", "break-in next door last month", 2)]),

        new("LEAD-BANK-002", 40,
        [
            new(true, "Boda rider hapa. Naskia mna bima ya ajali", 0),
            new(false, "Hello Brian, I am Kato, Mavuno Bank's insurance assistant. For a boda boda rider, Boda Rider Accident from Nile Shield Assurance pays UGX 50,000 for each day an accident stops you working, up to 15 days, and UGX 2,500,000 for permanent disability or death. It costs UGX 6,000 a month, about UGX 200 a day, and you are covered from the day you join.", 1),
            new(true, "Nafanya kazi kila siku. Mwezi uliopita nilianguka nikapoteza wiki mbili", 3),
            new(false, "That week and a half without income is exactly what this cover pays for: UGX 50,000 a day for up to 15 days. Do you have a riding permit?", 5),
            new(true, "Ndiyo ninayo. Nataka hii, naanza leo", 7),
        ],
        "Boda boda rider who writes in Kiswahili. He lost two weeks of income after a fall last month and wants Boda Rider Accident cover now.",
        "decisive",
        [(MicroinsuranceCatalog.BodaAccident.Name, InterestStrength.WantsIt, "Lost two weeks of income after a fall and said he wants it today.", [2, 3, 4])],
        [("Lost two weeks of income after a fall last month.", [2])],
        [],
        ("He is ready: confirm his riding permit and send the application for a licensed person to complete.", [4]),
        [("occupation", "boda boda rider", 0), ("timing", "wants it today", 4), ("worries", "lost two weeks of income after a fall", 2)]),

        new("LEAD-BANK-006", 200,
        [
            new(true, "Good morning, I have a loan with the bank and my wife says I should protect it", 0),
            new(false, "Good morning Francis, I am Kato, Mavuno Bank's insurance assistant. Your wife has a good point: Loan Protector from Mpingo Life Assurance pays off what you still owe on the loan if you die or are left permanently unable to work, so your family keeps the car. Is it the vehicle loan you are thinking of?", 1),
            new(true, "Yes the taxi loan. Five children, I am the only one working", 3),
            new(false, "With five children relying on you, that matters. The UGX 6,000,000 level costs UGX 9,000 a month and the UGX 10,000,000 level UGX 15,000. Accidents are covered from the day the loan is paid out; illness after 90 days. How much do you still owe?", 5),
            new(true, "About 7 million. I will ask my wife and call the branch", 9),
        ],
        "Taxi driver with a vehicle loan of about UGX 7,000,000. He is the only earner for five children and is discussing it with his wife.",
        "thoughtful",
        [(MicroinsuranceCatalog.LoanProtector.Name, InterestStrength.Interested, "Wants his taxi loan protected for his five children.", [1, 2])],
        [("He is the only earner for five children.", [2])],
        [("Needs to talk to his wife and the branch first.", [4])],
        ("Call after he has spoken to his wife, and quote the UGX 10,000,000 level to cover his UGX 7,000,000 balance.", [4]),
        [("occupation", "taxi driver", 2), ("dependants", "five children, only earner", 2), ("existing_cover", "none mentioned", 2)]),

        new("LEAD-BANK-004", 600,
        [
            new(true, "Hi", 0),
            new(false, "Hello, I am Kato, Mavuno Bank's insurance assistant. How can I help you today?", 1),
            new(true, "Just checking what this is", 5),
            new(false, "I can explain the insurance covers the bank offers with its partner insurers, such as funeral, school fees, shop, home and loan covers. Is there something you would like to protect?", 6),
        ],
        "Wrote in to see what the service is. Has not yet asked about a cover.",
        "neutral",
        [],
        [],
        [],
        null,
        []),
    ];

    public async Task<Result> SeedAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var conversations = 0;
        var messageCount = 0;
        foreach (var script in Scripts)
        {
            var lead = (await leadRead.ListLeadsAsync(scope.TenantId, scope.WorkspaceId, cancellationToken))
                .FirstOrDefault(item => item.LeadId == script.LeadId);
            if (lead is null || string.IsNullOrWhiteSpace(lead.Phone))
            {
                continue;
            }

            var start = now.AddMinutes(-script.StartedMinutesAgo);
            var threadId = WhatsAppMessagingService.BuildThreadId(DemoNumberId, lead.Phone);
            var customerTurnIds = new List<(Guid TurnId, DateTimeOffset At, string Text)>();
            string? pendingCustomerText = null;
            Guid pendingTurnId = default;
            DateTimeOffset pendingAt = default;

            foreach (var line in script.Lines)
            {
                var at = start.AddMinutes(line.MinutesAfterStart);
                var messageId = DeterministicGuid($"{scope.TenantId}:{threadId}:{line.MinutesAfterStart}:{line.FromCustomer}");
                await messages.SaveAsync(new WhatsAppMessage
                {
                    MessageId = messageId,
                    Scope = scope,
                    ThreadId = threadId,
                    BusinessPhoneNumberId = DemoNumberId,
                    ContactWaId = lead.Phone,
                    Direction = line.FromCustomer ? WhatsAppMessageDirection.Inbound : WhatsAppMessageDirection.Outbound,
                    ContentType = WhatsAppContentType.Text,
                    CustomerSafeBody = line.Text,
                    ProviderMessageId = $"demo.{messageId:N}",
                    ApprovedByUserId = line.FromCustomer ? null : KatoId,
                    DeliveryState = line.FromCustomer ? WhatsAppDeliveryState.Received : WhatsAppDeliveryState.Read,
                    CreatedAt = at,
                    UpdatedAt = at,
                    CorrelationId = "demo-conversation",
                }, cancellationToken);
                messageCount++;

                if (line.FromCustomer)
                {
                    if (pendingCustomerText is not null)
                    {
                        await SaveTurnAsync(scope, threadId, lead.Phone, pendingTurnId, pendingAt, pendingCustomerText, null, cancellationToken);
                    }

                    pendingCustomerText = line.Text;
                    pendingTurnId = DeterministicGuid($"{scope.TenantId}:{threadId}:turn:{line.MinutesAfterStart}");
                    pendingAt = at;
                    customerTurnIds.Add((pendingTurnId, at, line.Text));
                }
                else if (pendingCustomerText is not null)
                {
                    await SaveTurnAsync(scope, threadId, lead.Phone, pendingTurnId, pendingAt, pendingCustomerText, line.Text, cancellationToken);
                    pendingCustomerText = null;
                }
            }

            if (pendingCustomerText is not null)
            {
                await SaveTurnAsync(scope, threadId, lead.Phone, pendingTurnId, pendingAt, pendingCustomerText, null, cancellationToken);
            }

            var sources = customerTurnIds
                .Select((turn, index) => new InsightSource($"M{index + 1}", turn.TurnId, turn.At, Excerpt(turn.Text)))
                .ToArray();
            string[] Refs(int[] indexes) => indexes.Where(i => i >= 1 && i <= sources.Length).Select(i => $"M{i}").ToArray();
            string Cite(int index) => $"M{Math.Clamp(index, 1, Math.Max(1, sources.Length))}";

            var insights = new ConversationInsights(
                now,
                sources.Length,
                EnoughConversation: sources.Length >= ConversationInsightsService.MinimumCustomerMessages,
                sources.Length < 2 ? null : new InsightPoint(script.Summary, [Cite(1), Cite(sources.Length)]),
                script.Interests.Select(i => new InsightInterest(i.Product, i.Strength.ToString(), i.Why, Refs(i.Refs))).ToArray(),
                script.Worries.Select(w => new InsightPoint(w.Text, Refs(w.Refs))).ToArray(),
                script.Doubts.Select(d => new InsightPoint(d.Text, Refs(d.Refs))).ToArray(),
                script.Tone,
                [],
                script.NextStep is { } next ? new InsightPoint(next.Text, Refs(next.Refs)) : null,
                sources);

            var facts = ProspectFacts.Merge(
                lead.Facts,
                script.Facts.Select(f => new ProspectFact(f.Key, f.Value, ProspectFactSource.Customer, start.AddMinutes(f.Ref * 2), customerTurnIds.Count >= f.Ref + 1 ? customerTurnIds[f.Ref].TurnId : null, "Kato")));
            var interests = script.Interests
                .Select(i => new ProspectInterest(i.Product, i.Strength, i.Why, start.AddMinutes(8)))
                .ToArray();

            await leadWrite.CreateAsync(scope.TenantId, scope.WorkspaceId, lead with
            {
                ThreadId = threadId,
                PrimaryChannel = "WhatsApp",
                LastInteractionAt = start.AddMinutes(script.Lines[^1].MinutesAfterStart),
                Facts = facts,
                Interests = interests.Length == 0 ? lead.Interests : interests,
                Insights = insights,
                Activity = [.. lead.Activity, new ProspectActivity(start, "Wrote to Kato on WhatsApp", "Demo data")],
            }, cancellationToken);
            conversations++;
        }

        return new Result(conversations, messageCount);
    }

    private Task SaveTurnAsync(TenantScope scope, string threadId, string waId, Guid turnId, DateTimeOffset at, string inbound, string? reply, CancellationToken cancellationToken) =>
        turns.SaveAsync(new CustomerConversationTurn
        {
            TurnId = turnId,
            Scope = scope,
            ThreadId = threadId,
            ContactWaId = waId,
            InboundMessageText = inbound,
            ClassifiedIntent = CustomerIntent.ProductInterest,
            OutboundReplyText = reply,
            Outcome = CustomerAssistantOutcome.AutoReplied,
            OccurredAt = at,
            CorrelationId = "demo-conversation",
        }, cancellationToken);

    private static string Excerpt(string text) => text.Length <= 160 ? text : $"{text[..157]}...";

    private static Guid DeterministicGuid(string value) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}

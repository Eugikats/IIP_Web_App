using System.Text.Json;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Kato as an agent: a language model that holds the conversation and gets things done through a
/// small set of tools. Everything that matters is enforced in the tools, not in the prompt:
/// records are only ever read for, and written against, the phone number the message came from; a
/// claim is filed only when the customer's latest message confirms the read-back; papers go only
/// through the secure link; a policy is issued at once only on the operator's demo phone, and is an
/// application for a person to confirm anywhere else.
/// </summary>
public sealed partial class CustomerAssistantService
{
    private const int MaxAgentSteps = 6;
    private const int AgentHistoryTurns = 12;

    private static readonly JsonSerializerOptions ToolJson = new(JsonSerializerDefaults.Web);

    private async Task<AssistantOutcome?> RunAgentAsync(
        TenantScope scope, string original, string english, string waId, CustomerConversationTurn[] thread,
        CustomerOwnWords? ownWords, string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (agentModel is null)
        {
            return null;
        }

        var run = new AgentRun(scope, waId, original, english, ownWords, correlationId, now);
        var messages = new List<AgentChatMessage> { AgentChatMessage.System(await AgentSystemPromptAsync(run, cancellationToken)) };
        foreach (var turn in thread.TakeLast(AgentHistoryTurns))
        {
            if (!string.IsNullOrWhiteSpace(turn.InboundMessageText))
            {
                messages.Add(AgentChatMessage.User(turn.InboundMessageText));
            }

            if (!string.IsNullOrWhiteSpace(turn.OutboundReplyText))
            {
                messages.Add(AgentChatMessage.Assistant(turn.OutboundReplyText));
            }
        }

        messages.Add(AgentChatMessage.User(english));

        for (var step = 0; step < MaxAgentSteps; step++)
        {
            var next = await agentModel.NextAsync(messages, AgentTools, correlationId, cancellationToken);
            if (next.ToolCalls.Count == 0)
            {
                return string.IsNullOrWhiteSpace(next.Content) ? null : run.Finish(next.Content.Trim());
            }

            messages.Add(AgentChatMessage.Assistant(next.Content, next.ToolCalls));
            foreach (var call in next.ToolCalls)
            {
                string result;
                try
                {
                    result = await RunToolAsync(run, call, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    result = Json(new { error = "tool_failed", detail = "That did not work just now. Apologise briefly and offer to try again or to hand over to a person." });
                }

                messages.Add(AgentChatMessage.Tool(call.Id, result));
            }
        }

        return null;
    }

    private async Task<string> AgentSystemPromptAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        var knownName = sender?.FirstName ?? contactFirstName;
        var demoPhone = demoSession?.IsDemoPhone(run.WaId) == true;
        return $"""
            {AgentBehaviourComposer.PlatformPreamble}

            You are Kato, Nile Shield Assurance's assistant on WhatsApp in Uganda. You talk with ordinary people: boda boda riders, shop and boutique owners, market vendors, salon owners, families. Many write in a second language. Be warm, respectful and practical, like a helpful person at an insurance office counter who actually gets things done.

            How you write:
            - 1 to 4 short sentences. Plain words. No markdown, no headings, no bullet points; when you compare covers, write them as sentences.
            - Write in English. Your reply is translated into the customer's language automatically.
            - Use the customer's first name when you know it{(knownName is null ? "" : $" (you know them as {knownName})")}. Never call anyone "Customer".
            - Never start with "Sorry, I didn't understand". If a message is unclear, make your best guess from the conversation and ask one short question.
            - Answer greetings warmly and ask how you can help. Only say "you're welcome" when someone thanks you. The first time you speak to someone, say you are Kato from Nile Shield.

            What you can do, always through your tools (never invent a price, policy, claim or rule):
            - Explain covers: list_products for prices and what each pays; search_policy_information for detailed terms.
            - Register a new customer: ask for whatever you do not already know of their full name, their work and their area, in one question, then call register_customer. Never ask again for something they already told you.
            - Sell a cover: as soon as they agree to a cover ("yes", "ok", "that one", "the boda one"), call apply_for_cover straight away; do not ask them to confirm again. Register them first if they are not yet registered.{(demoPhone ? " On this test phone, applications are approved immediately as demo policies and no money is collected; tell them so plainly." : " The application is confirmed by a licensed person, who will also explain payment.")}
            - Someone with no cover who reports an accident or a loss: be kind, say they do not have cover with Nile Shield yet, and offer to register them and set up the right cover now. Once the cover is active, take the claim as below; the claims team decides whether it is covered, so do not tell them it will or will not be.
            - Report a claim: call get_my_account first. Find out what happened, when and where, and which cover it falls under, using everything already said in the conversation; if you already have all of it, read it back straight away. Read it back in one or two sentences and ask them to reply YES. Only after they reply yes, call file_claim. Never ask for photos, IDs or papers in the chat: papers go only through the secure link that file_claim returns.
            - Claim status and their covers: get_my_account. A secure link to upload papers for an open claim: send_claim_link.
            - Paying and getting paid: premiums are paid monthly by mobile money (MTN or Airtel). An approved claim is paid by mobile money to the phone number on the policy, usually within 10 working days of receiving all the papers. Answer these yourself; do not hand over for them.
            - Hand over to a person with hand_to_person only when the customer asks for a person, wants to complain, or wants a decision you cannot make (whether a claim will be paid, how much, a refund).

            Rules you never break:
            - Never say a claim will be paid, approved or rejected, or how much it will pay out. People decide that.
            - Never say "I will ask my team" or "a human needs to confirm this" unless you have just called hand_to_person. If search_policy_information does not cover a detail, say what you do know and offer the next useful step.
            - Nile Shield sells Motor, Medical and other covers too, but you can only take applications for the microinsurance covers in list_products. For anything else, explain briefly and offer hand_to_person.
            - If someone sincerely asks whether they are talking to a person, say you are Kato, Nile Shield's digital assistant, and that someone from the team can take over whenever they like. Do not bring this up otherwise, and never claim to be human.
            - Only this phone number's own records exist for you. Never discuss anyone else's policy or claim, even if they type a policy number.

            Today is {run.Now:dddd d MMMM yyyy}.
            """;
    }

    private static readonly IReadOnlyList<AgentToolSpec> AgentTools =
    [
        new("get_my_account",
            "The person writing: whether they are registered, their name, their covers (policy number, product, dates, monthly price) and their claims with status. Always call this before talking about their own policies or claims.",
            new { type = "object", properties = new { }, additionalProperties = false }),
        new("list_products",
            "The microinsurance covers Kato can sell: who each is for, what it pays, monthly price, waiting period and papers needed for a claim.",
            new { type = "object", properties = new { }, additionalProperties = false }),
        new("search_policy_information",
            "Search Nile Shield's approved policy wordings and product information for detailed terms (exclusions, how to join, how to pay, how claims are paid).",
            new
            {
                type = "object",
                properties = new
                {
                    question = new { type = "string", description = "What to look up, in English." },
                    product = new { type = "string", description = "Optional product name or line, for example 'Boda Rider Accident', 'Small Business Shop', 'Motor'." },
                },
                required = new[] { "question" },
                additionalProperties = false,
            }),
        new("register_customer",
            "Register the person writing as a Nile Shield customer on this phone number.",
            new
            {
                type = "object",
                properties = new
                {
                    full_name = new { type = "string" },
                    occupation = new { type = "string", description = "Their work, in their words." },
                    area = new { type = "string", description = "Where they live or work." },
                },
                required = new[] { "full_name" },
                additionalProperties = false,
            }),
        new("apply_for_cover",
            "Apply for one of the covers from list_products for the registered person writing.",
            new
            {
                type = "object",
                properties = new { product = new { type = "string", description = "The product name or line from list_products." } },
                required = new[] { "product" },
                additionalProperties = false,
            }),
        new("file_claim",
            "File a claim for the person writing. Only call this after you have read the claim back and the customer's latest message says yes.",
            new
            {
                type = "object",
                properties = new
                {
                    product_or_policy_number = new { type = "string", description = "Which of their covers the claim is under." },
                    what_happened = new { type = "string", description = "A clear description in English: what happened, where, and what was lost or damaged or what injury." },
                    when_it_happened = new { type = "string", description = "When it happened, for example 'this morning' or '28 September'." },
                },
                required = new[] { "what_happened" },
                additionalProperties = false,
            }),
        new("send_claim_link",
            "A secure link for the person writing to upload papers and follow their latest open claim.",
            new { type = "object", properties = new { }, additionalProperties = false }),
        new("hand_to_person",
            "Hand the conversation to a person on the Nile Shield team.",
            new
            {
                type = "object",
                properties = new { reason = new { type = "string" } },
                required = new[] { "reason" },
                additionalProperties = false,
            }),
    ];

    private async Task<string> RunToolAsync(AgentRun run, AgentToolCall call, CancellationToken cancellationToken)
    {
        using var arguments = ParseArguments(call.ArgumentsJson);
        var args = arguments.RootElement;
        return call.Name switch
        {
            "get_my_account" => await ToolGetMyAccountAsync(run, cancellationToken),
            "list_products" => ToolListProducts(),
            "search_policy_information" => await ToolSearchAsync(run, Arg(args, "question") ?? run.English, Arg(args, "product"), cancellationToken),
            "register_customer" => await ToolRegisterAsync(run, Arg(args, "full_name"), Arg(args, "occupation"), Arg(args, "area"), cancellationToken),
            "apply_for_cover" => await ToolApplyAsync(run, Arg(args, "product"), cancellationToken),
            "file_claim" => await ToolFileClaimAsync(run, Arg(args, "product_or_policy_number"), Arg(args, "what_happened"), Arg(args, "when_it_happened"), cancellationToken),
            "send_claim_link" => await ToolSendLinkAsync(run, cancellationToken),
            "hand_to_person" => await ToolHandToPersonAsync(run, Arg(args, "reason"), cancellationToken),
            _ => Json(new { error = "unknown_tool" }),
        };
    }

    private async Task<string> ToolGetMyAccountAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var customers = await CustomersOnThisPhoneAsync(run, cancellationToken);
        if (customers.Length == 0)
        {
            return Json(new { registered = false });
        }

        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        var claims = sender is null
            ? []
            : CasesFor(await caseRepository.ListAsync(run.Scope, cancellationToken), sender, run.WaId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new
                {
                    reference = ClaimReference.For(c.CaseId),
                    product = c.ProductLine,
                    status = ClaimStatusLabels.For(c.Status),
                    open = ClaimStatusLabels.IsOpen(c.Status),
                    reported = c.CreatedAt.ToString("d MMMM yyyy"),
                })
                .ToArray();
        return Json(new
        {
            registered = true,
            name = customers[0].FullName,
            covers = (sender?.Policies ?? []).Select(p => new
            {
                policy_number = p.PolicyNumber,
                product = p.ProductLine,
                from = p.EffectiveFrom?.ToString("d MMMM yyyy"),
                to = p.EffectiveTo?.ToString("d MMMM yyyy"),
                monthly_price_ugx = p.AnnualPremium is { } annual ? Math.Round(annual / 12m) : (decimal?)null,
            }).ToArray(),
            claims,
        });
    }

    private static string ToolListProducts() =>
        Json(MicroinsuranceCatalog.All.Select(p => new
        {
            name = p.Name,
            product = p.ProductLine,
            who_it_is_for = p.WhoItIsFor,
            what_it_pays = p.Explanation,
            monthly_price_ugx = p.MonthlyPremium,
            most_it_pays_ugx = p.SumInsured,
            waiting_period = p.WaitingPeriod,
            papers_for_a_claim = p.PapersForAClaim,
            how_to_pay = "Monthly by mobile money (MTN or Airtel). The first payment starts the cover.",
            how_claims_are_paid = "Report on WhatsApp, share papers through the secure link, the claims team reviews, and an approved claim is paid by mobile money to the policy's phone number, usually within 10 working days of all papers arriving.",
        }));

    private async Task<string> ToolSearchAsync(AgentRun run, string question, string? product, CancellationToken cancellationToken)
    {
        var micro = MicroinsuranceCatalog.FindByLineOrName(product);
        var lines = micro is not null
            ? [micro.ProductLine]
            : !string.IsNullOrWhiteSpace(product)
                ? [product.Trim()]
                : MicroinsuranceCatalog.All.Select(p => p.ProductLine).ToArray();

        var found = new List<(string Document, string Text)>();
        foreach (var line in lines)
        {
            var retrieval = await retrievalProvider.RetrieveAsync(
                new AssistantQuestion(run.Scope, null, question, line, "Customer", SystemActorId, run.CorrelationId), cancellationToken);
            found.AddRange(retrieval.Chunks.Take(lines.Length == 1 ? 4 : 2).Select(chunk => (chunk.DocumentName, Trim(chunk.Content, 900))));
        }

        return found.Count == 0
            ? Json(new { found = false, note = "Nothing in the approved documents about this. Say what you do know from list_products, or offer hand_to_person if they want more." })
            : Json(new { found = true, passages = found.Take(6).Select(f => new { document = f.Document, text = f.Text }) });
    }

    private async Task<string> ToolRegisterAsync(AgentRun run, string? fullName, string? occupation, string? area, CancellationToken cancellationToken)
    {
        var name = string.Join(' ', (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (name.Length < 2 || name.Length > 80 || FirstNameOf(name) is null)
        {
            return Json(new { error = "name_needed", instruction = "Ask for their full name." });
        }

        var onFile = await CustomersOnThisPhoneAsync(run, cancellationToken);
        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        if (sender is { Policies.Count: > 0 })
        {
            return Json(new { registered = true, already = true, name = sender.FullName });
        }

        // One record per phone: rename the contact Kato keeps for a new number rather than add a
        // second name to it, which would make the number unidentifiable everywhere.
        var customerId = onFile.Length == 1
            ? onFile[0].CustomerId
            : new Guid(System.Security.Cryptography.MD5.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{run.Scope.TenantId}:{run.Scope.WorkspaceId}:whatsapp-contact:{run.WaId}"))).ToString();
        foreach (var other in onFile.Where(c => c.CustomerId != customerId))
        {
            await customerRepository.UpsertAsync(other with { Phone = null }, cancellationToken);
        }

        await customerRepository.UpsertAsync(
            new Customer(customerId, run.Scope.TenantId, run.Scope.WorkspaceId, name, null, $"+{run.WaId}"), cancellationToken);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), run.Scope, null, null, SystemActorId, ActorType.Integration, "customer.registered_on_whatsapp", run.Now,
                null, null, [customerId], "CustomerAssistant", null, "recorded", null,
                "Customer registered themselves on WhatsApp through Kato. Details omitted from evidence.", run.CorrelationId),
            cancellationToken);

        var profile = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        run.LeadId ??= await CreateLeadAsync(
            run.Scope, $"Registered on WhatsApp. Work: {occupation ?? "not given"}. Area: {area ?? "not given"}.", run.WaId, profile,
            "WhatsApp - Kato registration", run.CorrelationId, cancellationToken, productFit: "To be chosen", occupation: occupation);
        return Json(new { registered = true, name, next = "Offer the covers from list_products that fit their work, and ask which they want." });
    }

    private async Task<string> ToolApplyAsync(AgentRun run, string? productName, CancellationToken cancellationToken)
    {
        var product = MicroinsuranceCatalog.FindByLineOrName(productName);
        if (product is null)
        {
            return Json(new { error = "unknown_product", instruction = "Use a product from list_products, or offer hand_to_person for other covers." });
        }

        var customers = await CustomersOnThisPhoneAsync(run, cancellationToken);
        if (customers.Length == 0)
        {
            return Json(new { error = "not_registered", instruction = "Ask for their full name, work and area, then call register_customer." });
        }

        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        if (sender?.Policies.FirstOrDefault(p => string.Equals(p.ProductLine, product.ProductLine, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            return Json(new { status = "already_covered", policy_number = existing.PolicyNumber, product = product.Name });
        }

        if (demoSession?.IsDemoPhone(run.WaId) != true)
        {
            run.LeadId = await CreateLeadAsync(
                run.Scope, $"Applied on WhatsApp for {product.Name}.", run.WaId, sender, "WhatsApp - cover application",
                run.CorrelationId, cancellationToken, productFit: product.Name);
            return Json(new
            {
                status = "application_received",
                product = product.Name,
                next = "A licensed person from Nile Shield confirms the application and explains how to pay by mobile money. Nothing is charged until then.",
            });
        }

        // The operator's standing decision for the field-test phone: a demo application is issued at
        // once so a participant can finish the journey. Marked as a demo policy everywhere.
        var today = DateOnly.FromDateTime(run.Now.UtcDateTime);
        var policyNumber = $"NSA/{product.Code}/{today.Year}/{Random.Shared.Next(10000, 99999)}";
        var policy = new Policy(
            Guid.NewGuid().ToString(), run.Scope.TenantId, run.Scope.WorkspaceId, policyNumber, product.ProductLine,
            $"{product.Name}, UGX {product.MonthlyPremium:N0} a month (demo policy, field test)",
            customers[0].CustomerId, null, today, today.AddYears(1), product.MonthlyPremium * 12m, "UGX", product.SumInsured);
        await policyRepository.UpsertAsync(policy, cancellationToken);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), run.Scope, null, null, SystemActorId, ActorType.Integration, "policy.demo_issued", run.Now,
                null, "Active", [policy.PolicyId], "CustomerAssistant", null, "recorded", null,
                "Demo policy issued on the field-test phone by the operator's standing approval for demo applications. No payment collected.",
                run.CorrelationId),
            cancellationToken);
        run.PolicyNumber = policyNumber;
        return Json(new
        {
            status = "active_demo_policy",
            policy_number = policyNumber,
            product = product.Name,
            starts = today.ToString("d MMMM yyyy"),
            waiting_period = product.WaitingPeriod,
            note = "This is a demo policy for testing. No money is collected.",
        });
    }

    private async Task<string> ToolFileClaimAsync(
        AgentRun run, string? coverHint, string? whatHappened, string? when, CancellationToken cancellationToken)
    {
        if (!KatoWords.StartsWithYes(run.Original, run.English))
        {
            return Json(new
            {
                error = "not_confirmed",
                instruction = "Read the claim back in one or two sentences and ask them to reply YES. Call file_claim only after their next message says yes.",
            });
        }

        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        if (sender is not { Policies.Count: > 0 })
        {
            return Json(new { error = "no_cover", instruction = "They have no cover on this number. Offer to register them and explain the covers; a claim can only be made under a cover they hold." });
        }

        var policy = ChooseAgentPolicy(sender.Policies, coverHint, whatHappened);
        if (policy is null)
        {
            return Json(new
            {
                error = "which_cover",
                covers = sender.Policies.Select(p => new { policy_number = p.PolicyNumber, product = p.ProductLine }),
                instruction = "Ask which of these covers the claim is under.",
            });
        }

        var description = string.IsNullOrWhiteSpace(when) ? whatHappened ?? run.English : $"{whatHappened} (when: {when})";
        var recent = await RecentOpenClaimAsync(run.Scope, policy.PolicyNumber, run.Now, cancellationToken);
        if (recent is not null)
        {
            await AddDetailToClaimAsync(recent, description, run.Now, run.CorrelationId, cancellationToken);
            run.CaseId = recent.CaseId;
            return Json(new
            {
                status = "already_reported",
                claim_reference = ClaimReference.For(recent.CaseId),
                reported = recent.CreatedAt.ToString("d MMMM, HH:mm"),
                note = "Their new detail was added to this open claim rather than opening a second one.",
            });
        }

        var customer = string.IsNullOrWhiteSpace(policy.CustomerId) ? null : await customerRepository.GetAsync(policy.CustomerId, run.Scope, cancellationToken);
        var caseIntake = new CaseIntakeService(caseRepository, auditRepository, productRepository);
        var claimCase = await caseIntake.CreateCaseAsync(new CreateCaseCommand(
            run.Scope,
            CustomerName: customer?.FullName ?? sender.FullName,
            CustomerEmail: customer?.Email,
            CustomerPhone: customer?.Phone ?? $"+{run.WaId}",
            PolicyNumber: policy.PolicyNumber,
            ProductLine: policy.ProductLine,
            ClaimType: $"{policy.ProductLine} claim reported on WhatsApp",
            Channel: "WhatsApp",
            IncidentDate: CustomerMessageSignals.IncidentDate($"{when} {whatHappened}", DateOnly.FromDateTime(run.Now.UtcDateTime)),
            IncidentDescription: description!,
            CreatedByUserId: SystemActorId,
            CorrelationId: run.CorrelationId,
            RequiresConfirmation: true,
            OriginalIncidentText: run.OwnWords?.Text,
            OriginalIncidentLanguage: run.OwnWords?.LanguageIso), cancellationToken);

        if (!string.IsNullOrWhiteSpace(claimCase.AssignedUserId))
        {
            try
            {
                await notificationEmitter.NotifyAsync(
                    claimCase.Scope, claimCase.AssignedUserId, NotificationType.ClaimAssigned,
                    $"New WhatsApp claim assigned to you on the {claimCase.AssignedTeam} team, confirmed by the customer, awaiting your confirmation.",
                    claimCase.CaseId, cancellationToken);
            }
            catch
            {
                // Best effort: the claim exists; a notification failure never reaches the customer.
            }
        }

        var documents = await DocumentsToRequestAsync(claimCase, cancellationToken);
        var link = await IssueAccessLinkAsync(run.Scope, claimCase, run.WaId, run.CorrelationId, cancellationToken);
        run.CaseId = claimCase.CaseId;
        run.OpenedCase = true;
        run.PolicyNumber = policy.PolicyNumber;
        return Json(new
        {
            status = "claim_filed",
            claim_reference = ClaimReference.For(claimCase.CaseId),
            cover = policy.ProductLine,
            papers_needed = documents,
            secure_link = link,
            instruction = link is null
                ? "Tell them the reference, the papers needed, and that someone from the team will tell them how to share the papers safely. Never ask for papers in the chat."
                : "Give the reference, the papers needed and the secure link, and say to share papers only through the link, not in the chat. Say the claims team will review it; never say it will be paid.",
        });
    }

    private static Policy? ChooseAgentPolicy(IReadOnlyList<Policy> policies, string? coverHint, string? whatHappened)
    {
        if (!string.IsNullOrWhiteSpace(coverHint))
        {
            var byNumber = policies.FirstOrDefault(p => coverHint.Contains(p.PolicyNumber, StringComparison.OrdinalIgnoreCase));
            if (byNumber is not null)
            {
                return byNumber;
            }

            var micro = MicroinsuranceCatalog.FindByLineOrName(coverHint);
            var byLine = policies.FirstOrDefault(p =>
                (micro is not null && string.Equals(p.ProductLine, micro.ProductLine, StringComparison.OrdinalIgnoreCase))
                || coverHint.Contains(p.ProductLine, StringComparison.OrdinalIgnoreCase));
            if (byLine is not null)
            {
                return byLine;
            }
        }

        if (policies.Count == 1)
        {
            return policies[0];
        }

        var hint = CustomerMessageSignals.ProductLineHint($"{coverHint} {whatHappened}", policies.Select(p => p.ProductLine).ToArray());
        return hint is null ? null : policies.FirstOrDefault(p => string.Equals(p.ProductLine, hint, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ToolSendLinkAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        if (sender is null)
        {
            return Json(new { error = "not_registered", instruction = "Links are only for customers with a claim on this number." });
        }

        var claimCase = CasesFor(await caseRepository.ListAsync(run.Scope, cancellationToken), sender, run.WaId)
            .Where(c => ClaimStatusLabels.IsOpen(c.Status))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
        if (claimCase is null)
        {
            return Json(new { error = "no_open_claim", instruction = "They have no open claim. If something has happened, offer to report it." });
        }

        var link = await IssueAccessLinkAsync(run.Scope, claimCase, run.WaId, run.CorrelationId, cancellationToken);
        run.CaseId = claimCase.CaseId;
        return Json(new
        {
            claim_reference = ClaimReference.For(claimCase.CaseId),
            secure_link = link,
            valid_for = "24 hours",
            note = link is null ? "The link could not be created right now; say someone from the team will send it." : null,
        });
    }

    private async Task<string> ToolHandToPersonAsync(AgentRun run, string? reason, CancellationToken cancellationToken)
    {
        var sender = await IdentifySenderAsync(run.Scope, run.WaId, cancellationToken);
        run.LeadId = await CreateLeadAsync(
            run.Scope, $"{reason ?? "Asked for a person"}. Customer said: {Trim(run.English, 300)}", run.WaId, sender,
            "WhatsApp - human requested", run.CorrelationId, cancellationToken);
        run.Escalated = true;
        return Json(new { handed_over = true, note = "Tell them someone from the team will reply here soon." });
    }

    private async Task<Customer[]> CustomersOnThisPhoneAsync(AgentRun run, CancellationToken cancellationToken) =>
        (await customerRepository.ListAsync(run.Scope, cancellationToken))
            .Where(customer => PhoneNumbers.SameNumber(customer.Phone, run.WaId))
            .ToArray();

    private static JsonDocument ParseArguments(string json)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static string? Arg(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static string Json(object value) => JsonSerializer.Serialize(value, ToolJson);

    private static string Trim(string text, int max) => text.Length <= max ? text : $"{text[..max]}...";

    private sealed class AgentRun(
        TenantScope scope, string waId, string original, string english, CustomerOwnWords? ownWords, string correlationId, DateTimeOffset now)
    {
        public TenantScope Scope { get; } = scope;
        public string WaId { get; } = waId;
        public string Original { get; } = original;
        public string English { get; } = english;
        public CustomerOwnWords? OwnWords { get; } = ownWords;
        public string CorrelationId { get; } = correlationId;
        public DateTimeOffset Now { get; } = now;
        public Guid? CaseId { get; set; }
        public bool OpenedCase { get; set; }
        public string? LeadId { get; set; }
        public string? PolicyNumber { get; set; }
        public bool Escalated { get; set; }

        public AssistantOutcome Finish(string reply) => AssistantOutcome.Of(
            reply,
            OpenedCase ? CustomerIntent.NewClaim : Escalated ? CustomerIntent.HumanRequested : CustomerIntent.GeneralQuestion,
            OpenedCase ? CustomerAssistantOutcome.CaseOpened : Escalated ? CustomerAssistantOutcome.Escalated : CustomerAssistantOutcome.AutoReplied,
            caseId: CaseId,
            leadId: LeadId,
            policyNumber: PolicyNumber,
            openedCase: OpenedCase) with { FromAgent = true };
    }
}

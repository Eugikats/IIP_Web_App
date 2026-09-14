using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.ClaimsSummary;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Application.CommandCenter;

public interface ICommandCenterActionService
{
    Task<CommandCenterQueryResponse> HandleAsync(
        string question,
        IntentClassificationResult intent,
        TenantContext context,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

/// <summary>
/// Carries out the instructions the assistant accepts from the chat box. There are exactly three,
/// chosen because each is useful on its own and none of them is a regulated insurance decision:
///
///   Draft a message to a customer. The model writes it; it is saved as a draft on the claim and
///   waits for someone with approval rights. The assistant never sends anything to a customer.
///
///   Assign a claim to a person. Work routing, not a decision about the claim. Audited with the
///   words the person typed, so the timeline shows exactly what was asked.
///
///   Run the claims agent on a claim. The same governed review the claim page offers, returned in
///   the conversation.
///
/// Each one resolves its target from what a person would naturally say: a claim reference, or a
/// customer's name. When a name could mean more than one claim, the assistant lists them with links
/// and asks which one was meant. It never picks one silently.
/// </summary>
public sealed class CommandCenterActionService(
    IPermissionService permissionService,
    ICaseRepository caseRepository,
    ICaseDocumentRepository caseDocumentRepository,
    IDocumentChecklistProvider checklistProvider,
    IUserAccountRepository userAccountRepository,
    IChatCompletionProvider chatCompletionProvider,
    IAiOutputValidator aiOutputValidator,
    INotificationDraftService notificationDraftService,
    IClaimsSummaryService claimsSummaryService,
    IAgentGate agentGate,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository) : ICommandCenterActionService
{
    private const string Visibility = "Internal";
    private const string ActionPromptVersion = "command-center-actions-v1";
    private const string NoModel = "None";

    public async Task<CommandCenterQueryResponse> HandleAsync(
        string question,
        IntentClassificationResult intent,
        TenantContext context,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var target = intent.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.TargetKey) ?? string.Empty;

        return intent.Intent switch
        {
            IntentType.DraftCustomerMessage => await DraftMessageAsync(question, target,
                intent.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.InstructionKey), context, correlationId, now, cancellationToken),
            IntentType.AssignClaim => await AssignAsync(question, target,
                intent.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.AssigneeKey) ?? string.Empty, context, correlationId, now, cancellationToken),
            IntentType.SummarizeClaim => await SummarizeAsync(question, target, context, correlationId, now, cancellationToken),
            _ => await RespondAsync(question, context, null, "I did not understand that instruction.", [new TextBlock("I did not understand that instruction.")], correlationId, now, cancellationToken),
        };
    }

    private async Task<CommandCenterQueryResponse> DraftMessageAsync(
        string question, string target, string? instruction, TenantContext context, string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await permissionService.HasPermissionAsync(Permission.ManageCustomerCommunications, cancellationToken))
        {
            return await RespondAsync(question, context, null, "Refused: no permission to prepare customer messages.",
                [new TextBlock("Your role cannot prepare messages to customers, so I have not drafted anything. A claims officer or supervisor can.")],
                correlationId, now, cancellationToken);
        }

        var gate = await agentGate.CheckAsync("customer-update-drafting", cancellationToken);
        if (!gate.Enabled)
        {
            return await RespondAsync(question, context, null, "Refused: customer update drafting agent is switched off.",
                [new TextBlock("Drafting messages to customers is switched off for this workspace, so I have not prepared anything.")],
                correlationId, now, cancellationToken);
        }

        var resolution = await ResolveClaimAsync(target, context, cancellationToken);
        if (resolution.Single is not { } claimCase)
        {
            return await ExplainResolutionAsync(question, target, resolution, "send a message about", context, correlationId, now, cancellationToken);
        }

        var reference = ClaimReference.For(claimCase.CaseId);
        var documents = await caseDocumentRepository.ListForCaseAsync(claimCase.CaseId, context.Scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
        var missing = checklist.MissingRequiredDocuments.Count == 0
            ? "none"
            : string.Join(", ", checklist.MissingRequiredDocuments.Select(document => document.Label));

        var prompt = string.Join('\n', [
            "Draft a short, warm, plain-English message from an insurer to one of its customers about their claim. A staff member will check it before anything is sent.",
            $"Customer: {claimCase.CustomerName}",
            $"Claim reference: {reference}",
            $"Policy number: {claimCase.PolicyNumber}",
            $"Claim: {claimCase.ProductLine} {claimCase.ClaimType}",
            $"Where the claim stands: {ClaimStatusLabels.For(claimCase.Status)}",
            $"Documents still needed: {missing}",
            string.IsNullOrWhiteSpace(instruction)
                ? "What to say: a short update on where the claim stands and, if documents are still needed, which ones."
                : $"What the staff member asked the message to say: {instruction}",
            "Rules: do not approve or deny the claim, promise payment or a timeline, admit liability, mention fraud, reserves or internal notes, or say the message has already been sent.",
            "Refer to the claim by its reference. Do not use em dashes. Sign off as the claims team, without inventing a person's name.",
        ]);

        ChatCompletionResult chatResult;
        try
        {
            chatResult = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(prompt, context.TenantId, context.WorkspaceId, correlationId, "customer-update-drafting"),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await ProviderUnavailableAsync(question, context, claimCase.CaseId, correlationId, now, cancellationToken);
        }

        var validated = await aiOutputValidator.ValidateCustomerSafeDraftAsync(chatResult.Content, context, cancellationToken);
        var channel = string.IsNullOrWhiteSpace(claimCase.CustomerEmail) && !string.IsNullOrWhiteSpace(claimCase.CustomerPhone) ? "SMS" : "Email";
        var draft = await notificationDraftService.CreateDraftAsync(
            new NotificationDraftCreateRequest(
                context.TenantId,
                context.WorkspaceId,
                "claim",
                claimCase.CaseId.ToString(),
                channel,
                validated.Preview,
                RequiresApproval: true,
                context.UserId,
                correlationId),
            cancellationToken);

        var blocks = new CommandCenterResponseBlock[]
        {
            new TextBlock($"I drafted a message to **{claimCase.CustomerName}** about claim {reference}. Nothing has been sent: the draft is waiting on the claim for someone with approval rights to check it and send it by {channel.ToLowerInvariant()}."),
            new DraftCommunicationBlock(draft.DraftId, validated.Preview, RequiresApproval: true),
            new ActionProposalBlock([
                new ProposedAction(
                    "Check and send the message",
                    $"Opens claim {reference}. The draft is under Customer updates.",
                    ClaimHref(claimCase.CaseId)),
            ]),
        };

        return await RespondAsync(question, context, claimCase.CaseId, $"Customer message draft {draft.DraftId} saved for approval on {reference}.",
            blocks, correlationId, now, cancellationToken, chatResult.Provider, AssistantMode.CustomerUpdateDrafting);
    }

    private async Task<CommandCenterQueryResponse> AssignAsync(
        string question, string target, string assigneeText, TenantContext context, string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var resolution = await ResolveClaimAsync(target, context, cancellationToken);
        if (resolution.Single is not { } claimCase)
        {
            return await ExplainResolutionAsync(question, target, resolution, "assign", context, correlationId, now, cancellationToken);
        }

        var reference = ClaimReference.For(claimCase.CaseId);
        string assigneeId;
        string assigneeName;
        if (IsSelf(assigneeText))
        {
            assigneeId = context.UserId;
            assigneeName = "you";
        }
        else
        {
            var people = (await userAccountRepository.ListAsync(context.Scope, cancellationToken))
                .Where(person => string.Equals(person.Status, "Active", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(person.Name))
                .ToArray();
            var matches = people.Where(person => NameMatches(person.Name, assigneeText)).ToArray();

            if (matches.Length != 1)
            {
                var known = people.Length == 0
                    ? "Nobody else has signed in to this workspace yet, and people can only be assigned work once they have."
                    : $"People you can assign to: {string.Join(", ", people.Select(person => person.Name).Order().Take(12))}.";
                var opening = matches.Length == 0
                    ? $"I could not find anyone called \"{assigneeText}\" in this workspace, so claim {reference} is unchanged."
                    : $"More than one person matches \"{assigneeText}\" ({string.Join(", ", matches.Select(person => person.Name))}), so claim {reference} is unchanged. Use their full name.";
                return await RespondAsync(question, context, claimCase.CaseId, "Assignment not made: assignee not resolved.",
                    [new TextBlock($"{opening} {known}")], correlationId, now, cancellationToken);
            }

            assigneeId = matches[0].UserId;
            assigneeName = matches[0].Name;
        }

        var assigningSomeoneElse = !string.Equals(assigneeId, context.UserId, StringComparison.Ordinal);
        var allowed = assigningSomeoneElse
            ? await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken)
            : await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken)
              || await permissionService.HasPermissionAsync(Permission.ViewAssignedClaims, cancellationToken);
        if (!allowed)
        {
            return await RespondAsync(question, context, claimCase.CaseId, "Refused: no permission to assign this claim.",
                [new TextBlock(assigningSomeoneElse
                    ? "Your role can take claims for yourself but cannot assign them to other people. A claims supervisor can."
                    : "Your role cannot take on claims, so the assignment has not been made.")],
                correlationId, now, cancellationToken);
        }

        if (string.Equals(claimCase.AssignedUserId, assigneeId, StringComparison.Ordinal))
        {
            return await RespondAsync(question, context, claimCase.CaseId, "No change: already assigned.",
                [
                    new TextBlock($"Claim {reference} for {claimCase.CustomerName} is already assigned to {assigneeName}. Nothing changed."),
                    OpenClaim(claimCase),
                ],
                correlationId, now, cancellationToken);
        }

        var before = claimCase.ToAuditSnapshot();
        var updated = claimCase with
        {
            Status = claimCase.Status == CaseStatus.New ? CaseStatus.Assigned : claimCase.Status,
            AssignedUserId = assigneeId,
            UpdatedAt = now,
        };
        await caseRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CaseAssigned(before, updated.ToAuditSnapshot(), context.UserId, correlationId, $"Assigned from the assistant: \"{Summarize(question)}\""),
            cancellationToken);

        return await RespondAsync(question, context, claimCase.CaseId, $"Assigned {reference} to {assigneeId}.",
            [
                new TextBlock($"Done. Claim {reference} for **{claimCase.CustomerName}** is now assigned to {assigneeName}. The change is on the claim's timeline."),
                OpenClaim(claimCase),
            ],
            correlationId, now, cancellationToken);
    }

    private async Task<CommandCenterQueryResponse> SummarizeAsync(
        string question, string target, TenantContext context, string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await permissionService.HasPermissionAsync(Permission.ViewAllClaims, cancellationToken)
            && !await permissionService.HasPermissionAsync(Permission.ViewAssignedClaims, cancellationToken))
        {
            return await RespondAsync(question, context, null, "Refused: no permission to read claims.",
                [new TextBlock("Your role cannot read claims, so I have not reviewed anything.")], correlationId, now, cancellationToken);
        }

        var gate = await agentGate.CheckAsync("claims-summary", cancellationToken);
        if (!gate.Enabled)
        {
            return await RespondAsync(question, context, null, "Refused: claims summary agent is switched off.",
                [new TextBlock("The claims agent is switched off for this workspace, so I have not reviewed the claim.")], correlationId, now, cancellationToken);
        }

        var resolution = await ResolveClaimAsync(target, context, cancellationToken);
        if (resolution.Single is not { } claimCase)
        {
            return await ExplainResolutionAsync(question, target, resolution, "summarise claim", context, correlationId, now, cancellationToken);
        }

        ClaimsSummaryResult result;
        try
        {
            result = await claimsSummaryService.SummarizeAsync(claimCase.CaseId, context.Scope, context.UserId, correlationId, cancellationToken);
        }
        catch (CaseNotFoundException)
        {
            return await RespondAsync(question, context, null, "Claim not visible.",
                [new TextBlock("That claim is not visible in this workspace.")], correlationId, now, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await ProviderUnavailableAsync(question, context, claimCase.CaseId, correlationId, now, cancellationToken);
        }

        var blocks = new List<CommandCenterResponseBlock>
        {
            new TextBlock($"Here is the claims agent's review of claim {ClaimReference.For(claimCase.CaseId)} for **{claimCase.CustomerName}**. It prepares the claim for a person to decide; it is not a decision.\n\n{result.Summary}"),
        };

        if (result.RiskSignals.Count > 0)
        {
            blocks.Add(new TextBlock("**Worth checking**\n" + string.Join('\n', result.RiskSignals.Select(signal => $"- {signal.Note}"))));
        }

        if (result.KnowledgeSources.Count > 0)
        {
            blocks.Add(new EvidenceListBlock(result.KnowledgeSources
                .Take(6)
                .Select(source => new EvidenceSource(source.Label, source.DocumentName, source.ChunkId))
                .ToArray()));
        }

        blocks.Add(OpenClaim(claimCase));

        return new CommandCenterQueryResponse(
            blocks,
            result.AgentActionLogId,
            correlationId,
            "claims-summary",
            ActionPromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now);
    }

    private sealed record ClaimResolution(ClaimCase? Single, IReadOnlyList<ClaimCase> Candidates);

    private async Task<ClaimResolution> ResolveClaimAsync(string target, TenantContext context, CancellationToken cancellationToken)
    {
        var cases = (await caseRepository.ListAsync(context.Scope, cancellationToken)).ToArray();

        var references = ClaimReference.FindIn(target);
        if (references.Count > 0)
        {
            var byReference = cases.Where(item => references.Any(reference => ClaimReference.Matches(item.CaseId, reference))).ToArray();
            return byReference.Length == 1 ? new ClaimResolution(byReference[0], byReference) : new ClaimResolution(null, byReference);
        }

        var matches = cases.Where(item => NameMatches(item.CustomerName, target)).ToArray();
        if (matches.Length == 0)
        {
            return new ClaimResolution(null, []);
        }

        var open = matches.Where(item => ClaimStatusLabels.IsOpen(item.Status)).ToArray();
        var pool = open.Length > 0 ? open : matches;
        var customers = pool.Select(item => item.CustomerName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        return customers == 1 && pool.Length == 1
            ? new ClaimResolution(pool[0], pool)
            : new ClaimResolution(null, pool.OrderByDescending(item => item.CreatedAt).ToArray());
    }

    private async Task<CommandCenterQueryResponse> ExplainResolutionAsync(
        string question, string target, ClaimResolution resolution, string verb, TenantContext context, string correlationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (resolution.Candidates.Count == 0)
        {
            return await RespondAsync(question, context, null, "No matching claim.",
                [new TextBlock($"I could not find a claim for \"{target}\" in this workspace, so nothing was changed. Try the customer's full name or the claim reference, for example CLM-1A2B3C4D.")],
                correlationId, now, cancellationToken);
        }

        var candidates = resolution.Candidates.Take(8).ToArray();
        var example = ClaimReference.For(candidates[0].CaseId);
        var exampleInstruction = verb == "assign" ? $"assign {example} to me" : $"{verb} {example}";
        return await RespondAsync(question, context, null, "Ambiguous claim target.",
            [
                new TextBlock($"More than one claim could match \"{target}\", so I have not done anything yet. Which one did you mean? Reply with its reference, for example \"{exampleInstruction}\"."),
                new ActionProposalBlock(candidates.Select(item => new ProposedAction(
                    $"{ClaimReference.For(item.CaseId)} for {item.CustomerName}",
                    $"{item.ProductLine} {item.ClaimType}, {ClaimStatusLabels.For(item.Status).ToLowerInvariant()}, reported {item.CreatedAt:d MMM yyyy}.",
                    ClaimHref(item.CaseId),
                    RequiresHumanApproval: false)).ToArray()),
            ],
            correlationId, now, cancellationToken);
    }

    private async Task<CommandCenterQueryResponse> ProviderUnavailableAsync(
        string question, TenantContext context, Guid? caseId, string correlationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await RespondAsync(question, context, caseId, "Provider unavailable; nothing prepared.",
            [new ErrorBlock("assistant_action_unavailable", "The AI service could not be reached just now, so nothing was prepared and nothing was changed. Try again in a moment.", correlationId)],
            correlationId, now, cancellationToken);

    private async Task<CommandCenterQueryResponse> RespondAsync(
        string question,
        TenantContext context,
        Guid? caseId,
        string modelOutput,
        IReadOnlyList<CommandCenterResponseBlock> blocks,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string modelProvider = NoModel,
        AssistantMode mode = AssistantMode.OperationalSummary)
    {
        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = context.Scope,
            CaseId = caseId,
            UserId = context.UserId,
            AgentName = "CommandCenterAction",
            AssistantMode = mode,
            InputSummary = Summarize(question),
            RetrievedSources = Array.Empty<SourceCitation>(),
            ModelOutput = modelOutput,
            ConfidenceScore = AiConfidenceScore.Create(modelProvider == NoModel ? 1.0m : 0.75m),
            RiskFlag = modelProvider == NoModel ? "none" : "needs_review",
            HumanOverride = false,
            CreatedAt = now,
            CorrelationId = correlationId,
            ModelProvider = modelProvider,
            ModelDeployment = null,
            PromptVersion = ActionPromptVersion,
            Visibility = Visibility,
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);

        return new CommandCenterQueryResponse(
            blocks,
            actionLog.AgentActionLogId,
            correlationId,
            modelProvider,
            ActionPromptVersion,
            Visibility,
            CanTriggerDecision: false,
            now);
    }

    private static ActionProposalBlock OpenClaim(ClaimCase claimCase) =>
        new([
            new ProposedAction(
                $"Open claim {ClaimReference.For(claimCase.CaseId)}",
                $"{claimCase.ProductLine} {claimCase.ClaimType} for {claimCase.CustomerName}.",
                ClaimHref(claimCase.CaseId),
                RequiresHumanApproval: false),
        ]);

    private static string ClaimHref(Guid caseId) => $"/app/claims/{caseId}";

    private static bool IsSelf(string text) =>
        text.Trim().ToLowerInvariant() is "me" or "myself" or "i" or "my queue" or "my name";

    /// <summary>Every word the person typed is the start of some word in the name, so "eugene",
    /// "Eugene K" and "katusiime" all find Eugene Katusiime, but "kato" does not find "Katusiime".</summary>
    private static bool NameMatches(string? name, string typed)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(typed))
        {
            return false;
        }

        var nameWords = name.Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries);
        var typedWords = typed.Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 0)
            .ToArray();

        return typedWords.Length > 0
            && typedWords.All(typedWord => nameWords.Any(nameWord => nameWord.StartsWith(typedWord, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Summarize(string question) =>
        question.Length <= 180 ? question : $"{question[..180]}...";
}

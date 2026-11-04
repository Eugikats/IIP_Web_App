using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Mail;

namespace InsuranceIntelligence.Api.Integrations;

public static class EmailIntegrationEndpoints
{
    public static IResult GetHealth(InfrastructureOptions options, HttpContext httpContext)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        var providerHealth = AdminEndpoints.BuildProviderHealth(options);
        var providers = providerHealth.Providers
            .Where(provider => provider.Name is "graphMail" or "communications" or "notifications")
            .ToArray();
        var graphMail = providers.FirstOrDefault(provider => provider.Name == "graphMail");
        var communications = providers.FirstOrDefault(provider => provider.Name == "communications");

        var allowedActions = new List<ActionStateResponse>
        {
            Action("configureMailbox", "Save mailbox configuration", Permission.ConfigureIntegrations)
        };
        var blockedActions = new List<ActionStateResponse>();

        AddProviderBackedAction(
            graphMail,
            expectedMode: "Graph",
            action: "processInboundEmail",
            label: "Process inbound email",
            permission: Permission.ConfigureIntegrations,
            unavailableReason: "Microsoft Graph mailbox ingestion is not configured.",
            allowedActions,
            blockedActions);

        AddProviderBackedAction(
            communications,
            expectedMode: "GraphEmail",
            action: "sendCustomerEmail",
            label: "Send customer email",
            permission: Permission.SendCustomerUpdate,
            unavailableReason: "GraphEmail customer communication is not configured.",
            allowedActions,
            blockedActions);

        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && options.EmailProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            allowedActions.Add(Action("runTestIngestion", "Run local test ingestion", Permission.ConfigureIntegrations));
        }
        else
        {
            blockedActions.Add(Action(
                "runTestIngestion",
                "Run local test ingestion",
                Permission.ConfigureIntegrations,
                "Local test ingestion is disabled outside isolated development mock mode. Use real Microsoft Graph mailbox ingestion."));
        }

        return Results.Ok(new EmailIntegrationHealthResponse(
            "email",
            "Microsoft Graph mail",
            // Teams notifications are listed for visibility but are not part of email health: an
            // unconfigured Teams webhook does not degrade mail ingestion or sending.
            providers.Where(provider => provider.Name is "graphMail" or "communications").Any(provider => provider.Status is "missing" or "not_configured") ? "degraded" : "healthy",
            providers,
            allowedActions,
            blockedActions));
    }

    public static async Task<IResult> ListMailboxes(
        IMailboxConnectionRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        return Results.Ok(await repository.ListAsync(scope, cancellationToken));
    }

    public static async Task<IResult> CreateMailbox(
        CreateMailboxRequest request,
        IMailboxConnectionRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Validation("displayName", "Mailbox display name is required.");
        }

        var mailboxAddress = request.MailboxAddress?.Trim() ?? string.Empty;
        if (!MailAddress.TryCreate(mailboxAddress, out var parsedAddress)
            || !string.Equals(parsedAddress.Address, mailboxAddress, StringComparison.OrdinalIgnoreCase))
        {
            return Validation("mailboxAddress", "A valid mailbox email address is required.");
        }

        if (!TryReadMutationAuditContext(httpContext, out var actor, out var auditReason, out var auditError))
        {
            return auditError!;
        }

        var existingMailboxes = await repository.ListAsync(scope, cancellationToken);
        if (existingMailboxes.Any(existing =>
            existing.MailboxAddress.Equals(mailboxAddress, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Conflict(new { message = "This mailbox is already configured for the tenant workspace." });
        }

        var mailbox = new MailboxConnection
        {
            MailboxConnectionId = Guid.NewGuid(),
            Scope = scope,
            DisplayName = request.DisplayName.Trim(),
            MailboxAddress = mailboxAddress,
            IngestionEnabled = request.IngestionEnabled,
            DefaultChannel = string.IsNullOrWhiteSpace(request.DefaultChannel) ? "Email" : request.DefaultChannel.Trim(),
            ProductLine = NullIfWhiteSpace(request.ProductLine),
            ProcessedFolderName = NullIfWhiteSpace(request.ProcessedFolderName),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repository.SaveAsync(mailbox, cancellationToken);
        await auditRepository.WriteAsync(
            EmailAudit(
                scope,
                caseId: null,
                "integration.mailbox.configured",
                [mailbox.MailboxConnectionId.ToString()],
                actor,
                httpContext.TraceIdentifier,
                options.EmailProvider,
                auditReason,
                ActorType.User),
            cancellationToken);

        return Results.Created($"/api/v1/integrations/email/mailboxes/{mailbox.MailboxConnectionId}", mailbox);
    }

    /// <summary>
    /// Collect waiting mail now, in any runtime. The scheduled MailboxIngestion timer is what makes
    /// a connected mailbox work unattended; this is how an administrator proves it works without
    /// waiting for the next tick, and it is deliberately not the dev-only TestIngestion below --
    /// that one is gated behind development diagnostics and does not exist in Staging or
    /// Production, which is precisely why connected mailboxes silently delivered nothing there.
    /// Same service the timer calls, so what an admin sees here is what the schedule does.
    /// </summary>
    public static async Task<IResult> Ingest(
        IMailboxIngestionService ingestionService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (!TryReadMutationAuditContext(httpContext, out var actor, out _, out var auditError))
        {
            return auditError!;
        }

        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                message = "Inbound email collection requires a configured Microsoft Graph mailbox connection.",
                provider = options.EmailProvider
            });
        }

        var result = await ingestionService.IngestAsync(scope, httpContext.TraceIdentifier, actor, cancellationToken);

        return Results.Ok(new
        {
            mailboxesPolled = result.MailboxesPolled,
            messagesIngested = result.MessagesIngested,
            alreadyPresent = result.AlreadyPresent,
            mailboxesUnavailable = result.MailboxesUnavailable
        });
    }

    public static async Task<IResult> TestIngestion(
        IMailboxConnectionRepository mailboxRepository,
        IInboundEmailRepository emailRepository,
        IEmailIngestionProvider ingestionProvider,
        IGraphMailProvider graphMailProvider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var graphMode = options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase);
        var isolatedLocalTestMode = options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && options.EmailProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase);
        if (!graphMode && !isolatedLocalTestMode)
        {
            return Results.BadRequest(new
            {
                message = "Email ingestion requires real Microsoft Graph configuration. Test ingestion is available only in isolated Development mock mode."
            });
        }

        if (!TryReadMutationAuditContext(httpContext, out var actor, out var auditReason, out var auditError))
        {
            return auditError!;
        }

        var mailbox = (await mailboxRepository.ListAsync(scope, cancellationToken)).FirstOrDefault();
        if (mailbox is null)
        {
            return Results.BadRequest(new { message = "No mailbox is configured for this tenant workspace." });
        }

        if (graphMode)
        {
            var existingMessages = await emailRepository.ListAsync(scope, cancellationToken);
            var existingExternalIds = existingMessages
                .Where(message => !string.IsNullOrWhiteSpace(message.ExternalMessageId))
                .Select(message => ExternalMessageKey(message.MailboxConnectionId, message.ExternalMessageId!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ingested = new List<InboundEmailMessage>();
            foreach (var configuredMailbox in (await mailboxRepository.ListAsync(scope, cancellationToken)).Where(item => item.IngestionEnabled))
            {
                IReadOnlyCollection<InboundEmailMessage> pulled;
                try
                {
                    pulled = await graphMailProvider.PullMessagesAsync(configuredMailbox, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return Results.BadRequest(new { message = "Graph mail provider is not configured for live ingestion." });
                }

                foreach (var graphMessage in pulled)
                {
                    if (existingMessages.Any(existing => existing.MessageId == graphMessage.MessageId)
                        || (!string.IsNullOrWhiteSpace(graphMessage.ExternalMessageId)
                            && existingExternalIds.Contains(ExternalMessageKey(configuredMailbox.MailboxConnectionId, graphMessage.ExternalMessageId))))
                    {
                        continue;
                    }

                    await emailRepository.SaveAsync(graphMessage, cancellationToken);
                    ingested.Add(graphMessage);
                    if (!string.IsNullOrWhiteSpace(graphMessage.ExternalMessageId))
                    {
                        existingExternalIds.Add(ExternalMessageKey(configuredMailbox.MailboxConnectionId, graphMessage.ExternalMessageId));
                    }

                    await auditRepository.WriteAsync(
                        EmailAudit(
                            scope,
                            null,
                            "email.graph.received",
                            [graphMessage.MessageId.ToString()],
                            actor,
                            httpContext.TraceIdentifier,
                            options.EmailProvider,
                            auditReason),
                        cancellationToken);
                }
            }

            return Results.Ok(new { ingested = ingested.Count, messages = ingested });
        }

        var message = await ingestionProvider.CreateTestMessageAsync(mailbox, cancellationToken);
        await emailRepository.SaveAsync(message, cancellationToken);
        await auditRepository.WriteAsync(
            EmailAudit(
                scope,
                null,
                "email.received",
                [message.MessageId.ToString()],
                actor,
                httpContext.TraceIdentifier,
                options.EmailProvider,
                auditReason),
            cancellationToken);
        return Results.Created($"/api/v1/integrations/email/messages/{message.MessageId}", message);
    }

    public static async Task<IResult> ProcessMessage(
        ProcessEmailMessageRequest request,
        IInboundEmailRepository emailRepository,
        IMailboxConnectionRepository mailboxRepository,
        IGraphMailProvider graphMailProvider,
        IEmailToCaseMatcher matcher,
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IEmailAttachmentProcessor attachmentProcessor,
        IAuditRepository auditRepository,
        InsuranceIntelligence.Application.Cases.IFnolAcknowledgementService acknowledgementService,
        ILogger<EmailFnolAcknowledgementLogMarker> logger,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        InsuranceIntelligence.Api.Agents.IClaimTriageQueue? triageQueue = null)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (!TryReadMutationAuditContext(httpContext, out var actor, out var auditReason, out var auditError))
        {
            return auditError!;
        }

        var message = await emailRepository.GetAsync(request.MessageId, scope, cancellationToken);
        if (message is null)
        {
            return Results.NotFound(new { message = "Inbound email was not found for the requested tenant workspace." });
        }

        if (message.LinkedCaseId is { } linkedCaseId
            && message.ProcessingStatus is EmailProcessingStatus.CaseCreated or EmailProcessingStatus.LinkedToCase)
        {
            return Results.Ok(new { message, caseId = linkedCaseId, matchReason = "already_processed" });
        }

        var sourceMailbox = await mailboxRepository.GetAsync(message.MailboxConnectionId, scope, cancellationToken);
        if (sourceMailbox is null)
        {
            return Results.Conflict(new { message = "The inbound email source mailbox is no longer configured for this tenant workspace." });
        }

        var match = await matcher.MatchAsync(message, scope, cancellationToken);
        ClaimCase claimCase;
        string auditAction;
        if (match.CaseId is { } caseId)
        {
            var existingCase = await caseRepository.GetAsync(caseId, cancellationToken);
            if (existingCase is null || !existingCase.Scope.Matches(scope))
            {
                return Results.NotFound(new { message = "Matched case was not found for the requested tenant workspace." });
            }

            claimCase = existingCase;
            auditAction = "email.case_linked";
        }
        else
        {
            claimCase = NewEmailCase(scope, message, sourceMailbox, actor, httpContext.TraceIdentifier, match.IdentifiedSender);
            auditAction = "email.case_created";
        }

        IReadOnlyCollection<CaseDocument> processedAttachments;
        try
        {
            processedAttachments = await attachmentProcessor.ProcessAsync(claimCase, message, sourceMailbox, actor, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or NotSupportedException)
        {
            var failed = message with { ProcessingStatus = EmailProcessingStatus.Failed };
            await emailRepository.SaveAsync(failed, cancellationToken);
            await auditRepository.WriteAsync(
                EmailAudit(
                    scope,
                    claimCase.CaseId,
                    "email.attachment_processing_failed",
                    [message.MessageId.ToString()],
                    actor,
                    httpContext.TraceIdentifier,
                    options.EmailProvider,
                    auditReason,
                    ActorType.User),
                cancellationToken);
            return Results.BadRequest(new
            {
                message = "Email attachment processing is not configured or failed for the selected provider.",
                provider = options.EmailProvider
            });
        }

        if (auditAction == "email.case_created")
        {
            await caseRepository.SaveAsync(claimCase, cancellationToken);

            // A failed or blocked acknowledgement must never fail email processing, which already
            // succeeded above, see IFnolAcknowledgementService's own doc comment.
            try
            {
                await acknowledgementService.SendAsync(claimCase, httpContext.TraceIdentifier, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FNOL acknowledgement failed for email-derived case {CaseId}", claimCase.CaseId);
            }

            if (triageQueue is not null)
            {
                await triageQueue.EnqueueAsync(scope, claimCase.CaseId, httpContext.TraceIdentifier, "email_intake");
            }
        }

        foreach (var document in processedAttachments)
        {
            await documentRepository.SaveAsync(document, cancellationToken);
            await auditRepository.WriteAsync(
                EmailAudit(
                    scope,
                    claimCase.CaseId,
                    "email.attachment_processed",
                    [document.DocumentId.ToString()],
                    actor,
                    document.CorrelationId,
                    options.EmailProvider,
                    auditReason,
                    ActorType.User),
                cancellationToken);
        }

        var processed = message with
        {
            ProcessingStatus = auditAction == "email.case_created" ? EmailProcessingStatus.CaseCreated : EmailProcessingStatus.LinkedToCase,
            LinkedCaseId = claimCase.CaseId
        };
        await emailRepository.SaveAsync(processed, cancellationToken);
        await auditRepository.WriteAsync(
            EmailAudit(
                scope,
                claimCase.CaseId,
                auditAction,
                [message.MessageId.ToString()],
                actor,
                httpContext.TraceIdentifier,
                options.EmailProvider,
                auditReason,
                ActorType.User),
            cancellationToken);

        if (options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceMailbox is not null)
            {
                await graphMailProvider.CompleteMessageAsync(sourceMailbox, processed, cancellationToken);
            }
        }

        return Results.Ok(new { message = processed, caseId = claimCase.CaseId, match.MatchReason });
    }

    public static async Task<IResult> ListMessages(
        IInboundEmailRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        return Results.Ok(await repository.ListAsync(scope, cancellationToken));
    }

    public static async Task<IResult> GetMessage(
        Guid messageId,
        IInboundEmailRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var message = await repository.GetAsync(messageId, scope, cancellationToken);
        return message is null ? Results.NotFound(new { message = "Inbound email was not found." }) : Results.Ok(message);
    }

    public static async Task<IResult> GraphWebhook(
        IWebhookSubscriptionProvider provider,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!options.EmailProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase)
            && !options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Unsupported email provider mode." });
        }

        using var reader = new StreamReader(httpContext.Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var status = await provider.ValidateAsync(payload, cancellationToken);
        return Results.Ok(new { status });
    }

    /// <summary>
    /// Builds a best-effort case from an inbound email that matched no existing claim. A human
    /// already triggered this via <see cref="ProcessMessage"/>, but the claim details are still
    /// unverified: an unidentified sender's claimed policy number lives only in free-text email
    /// content it fully controls, and even an identified sender's incident details are a guess
    /// from the raw email. Both paths always set <c>RequiresConfirmation = true</c> so the case
    /// carries no SLA clock until a claims officer reviews and confirms it, see
    /// <see cref="ClaimCase.RequiresConfirmation"/>.
    /// </summary>
    private static ClaimCase NewEmailCase(
        TenantScope scope,
        InboundEmailMessage message,
        MailboxConnection? mailbox,
        string actor,
        string correlationId,
        IdentifiedSender? identifiedSender)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = identifiedSender?.CustomerName ?? message.Sender,
            CustomerEmail = identifiedSender?.CustomerEmail ?? message.Sender,
            CustomerPhone = identifiedSender?.CustomerPhone,
            PolicyNumber = identifiedSender?.PolicyNumber ?? "UNKNOWN",
            ProductLine = identifiedSender?.ProductLine
                ?? (string.IsNullOrWhiteSpace(mailbox?.ProductLine) ? "Unassigned" : mailbox.ProductLine),
            ClaimType = "Email Intake",
            Channel = "Email",
            IncidentDate = DateOnly.FromDateTime(message.ReceivedAt.UtcDateTime.Date),
            IncidentDescription = $"{message.Subject}. {message.BodyPreview}",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            RiskLevel = "Low",
            Priority = CasePriority.Normal,
            SlaDueAt = null,
            RequiresConfirmation = true,
            AssignedUserId = actor,
            AssignedTeam = "Claims",
            CreatedByUserId = actor,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId
        };
    }

    private static string ExternalMessageKey(Guid mailboxConnectionId, string externalMessageId) =>
        $"{mailboxConnectionId:N}:{externalMessageId.Trim()}";

    private static AuditEvent EmailAudit(
        TenantScope scope,
        Guid? caseId,
        string action,
        IReadOnlyCollection<string> evidence,
        string userId,
        string correlationId,
        string provider,
        string? auditReason = null,
        ActorType actorType = ActorType.Integration) =>
        new(Guid.NewGuid(), scope, caseId, null, userId, actorType, action, DateTimeOffset.UtcNow, null, null, evidence, provider, null, "accepted", null, auditReason, correlationId);

    private static bool TryReadMutationAuditContext(
        HttpContext httpContext,
        out string actor,
        out string auditReason,
        out IResult? error)
    {
        actor = ResolveActor(httpContext).Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            auditReason = string.Empty;
            error = Validation("userId", "Authenticated user context is required for integration mutations.");
            return false;
        }

        auditReason = AuditReasonReader.Read(httpContext)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            error = Validation("auditReason", "An audit reason is required for integration mutations.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryResolveScope(HttpContext httpContext, out TenantScope scope, out string validationError)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext)
        {
            var current = tenantContext.Current;
            scope = TenantScope.Create(current.TenantId, current.WorkspaceId);
            validationError = string.Empty;
            return true;
        }

        return TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out scope, out validationError);
    }

    private static string ResolveActor(HttpContext httpContext)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext
            && !string.IsNullOrWhiteSpace(tenantContext.Current.UserId))
        {
            return tenantContext.Current.UserId;
        }

        return DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddProviderBackedAction(
        ProviderHealthItem? provider,
        string expectedMode,
        string action,
        string label,
        Permission permission,
        string unavailableReason,
        ICollection<ActionStateResponse> allowedActions,
        ICollection<ActionStateResponse> blockedActions)
    {
        if (IsProviderConfigured(provider, expectedMode))
        {
            allowedActions.Add(Action(action, label, permission));
            return;
        }

        blockedActions.Add(Action(action, label, permission, provider?.Detail ?? unavailableReason));
    }

    private static bool IsProviderConfigured(ProviderHealthItem? provider, string expectedMode)
    {
        if (provider is null || !provider.Mode.Equals(expectedMode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return provider.Status.Equals("configured", StringComparison.OrdinalIgnoreCase)
            || provider.Status.Equals("healthy", StringComparison.OrdinalIgnoreCase);
    }

    private static ActionStateResponse Action(string action, string label, Permission permission, string? reason = null) =>
        new(action, label, permission.ToString(), reason);

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

/// <summary>Empty marker type purely so ILogger&lt;T&gt; has a stable category name for this
/// static-class endpoint (ILogger&lt;EmailIntegrationEndpoints&gt; is not usable, the class is
/// static), matching SmsIntegrationEndpoints' SmsDeliveryStatusLogMarker precedent.</summary>
public sealed class EmailFnolAcknowledgementLogMarker;

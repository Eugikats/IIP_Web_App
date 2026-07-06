using System.Text.Json;
using System.Text;
using System.Security.Claims;
using InsuranceIntelligence.Api.Communications;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Graph;
using InsuranceIntelligence.Infrastructure.Simulated;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class Phase6IntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EmailIntegrationHealth_ReportsConfiguredGraphActionsWithoutSecrets()
    {
        var options = GraphOptions() with { CommunicationProvider = "GraphEmail" };

        var result = EmailIntegrationEndpoints.GetHealth(options, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var allowedActions = body.GetProperty("allowedActions")
            .EnumerateArray()
            .Select(item => item.GetProperty("action").GetString())
            .ToArray();
        var blockedActions = body.GetProperty("blockedActions")
            .EnumerateArray()
            .Select(item => item.GetProperty("action").GetString())
            .ToArray();
        var responseJson = body.GetRawText();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("email", body.GetProperty("integrationId").GetString());
        Assert.Equal("healthy", body.GetProperty("overallStatus").GetString());
        Assert.Contains("configureMailbox", allowedActions);
        Assert.Contains("processInboundEmail", allowedActions);
        Assert.Contains("sendCustomerEmail", allowedActions);
        Assert.Contains("runTestIngestion", blockedActions);
        Assert.DoesNotContain(options.GraphMail.ClientSecret!, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("clientSecret", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailIntegrationHealth_BlocksProviderActionsWhenGraphIsUnavailable()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Entra",
            EmailProvider = "Disabled",
            CommunicationProvider = "Disabled",
            NotificationProvider = "Disabled"
        };

        var result = EmailIntegrationEndpoints.GetHealth(options, AuthenticatedContext("Admin"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var allowedActions = body.GetProperty("allowedActions").EnumerateArray().ToArray();
        var blockedActions = body.GetProperty("blockedActions").EnumerateArray().ToArray();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Single(allowedActions);
        Assert.Equal("configureMailbox", allowedActions[0].GetProperty("action").GetString());
        Assert.Equal(
            ["processInboundEmail", "sendCustomerEmail", "runTestIngestion"],
            blockedActions.Select(item => item.GetProperty("action").GetString() ?? string.Empty).ToArray());
        Assert.All(blockedActions, item => Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("reason").GetString())));
    }

    [Fact]
    public async Task CreateMailboxAndTestIngestion_CreatesInboundEmail()
    {
        var deps = CreateDeps();
        const string auditReason = "Integration admin confirmed the mailbox mapping for this workspace.";
        var create = await EmailIntegrationEndpoints.CreateMailbox(
            new CreateMailboxRequest("Claims", "claims@demo.local", true, "Email"),
            deps.Mailboxes,
            deps.Audit,
            Options(),
            Context(auditReason: auditReason),
            CancellationToken.None);
        var (createStatus, _) = await ExecuteAsync<JsonElement>(create);

        var ingestion = await EmailIntegrationEndpoints.TestIngestion(
            deps.Mailboxes,
            deps.Emails,
            deps.EmailProvider,
            deps.GraphProvider,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var (ingestStatus, body) = await ExecuteAsync<JsonElement>(ingestion);

        Assert.Equal(StatusCodes.Status201Created, createStatus);
        Assert.Equal(StatusCodes.Status201Created, ingestStatus);
        Assert.Equal(0, body.GetProperty("processingStatus").GetInt32());
        var mailboxAudit = Assert.Single(
            await deps.Audit.ListAsync(Scope(), CancellationToken.None),
            audit => audit.ActionName == "integration.mailbox.configured");
        Assert.Equal(auditReason, mailboxAudit.AuditReason);
        Assert.Equal(ActorType.User, mailboxAudit.ActorType);
        Assert.DoesNotContain("claims@demo.local", mailboxAudit.EvidenceIds);
    }

    [Fact]
    public async Task CreateMailbox_RequiresRuntimeActorAndAuditReason()
    {
        var deps = CreateDeps();
        var request = new CreateMailboxRequest("Claims", "claims@demo.local", true, "Email");

        var missingActor = await EmailIntegrationEndpoints.CreateMailbox(
            request,
            deps.Mailboxes,
            deps.Audit,
            Options(),
            Context(userId: null, auditReason: "Approved mailbox configuration."),
            CancellationToken.None);
        var missingReason = await EmailIntegrationEndpoints.CreateMailbox(
            request,
            deps.Mailboxes,
            deps.Audit,
            Options(),
            Context(userId: "local-it-admin", auditReason: null),
            CancellationToken.None);
        var (missingActorStatus, _) = await ExecuteAsync<JsonElement>(missingActor);
        var (missingReasonStatus, _) = await ExecuteAsync<JsonElement>(missingReason);

        Assert.Equal(StatusCodes.Status400BadRequest, missingActorStatus);
        Assert.Equal(StatusCodes.Status400BadRequest, missingReasonStatus);
        Assert.Empty(await deps.Mailboxes.ListAsync(Scope(), CancellationToken.None));
        Assert.Empty(await deps.Audit.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateMailbox_RejectsInvalidAndDuplicateAddresses()
    {
        var deps = CreateDeps();

        var invalid = await EmailIntegrationEndpoints.CreateMailbox(
            new CreateMailboxRequest("Claims", "not-an-email-address", true, "Email"),
            deps.Mailboxes,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var first = await EmailIntegrationEndpoints.CreateMailbox(
            new CreateMailboxRequest("Claims", "claims@demo.local", true, "Email"),
            deps.Mailboxes,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var duplicate = await EmailIntegrationEndpoints.CreateMailbox(
            new CreateMailboxRequest("Claims duplicate", "CLAIMS@demo.local", true, "Email"),
            deps.Mailboxes,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var (invalidStatus, _) = await ExecuteAsync<JsonElement>(invalid);
        var (firstStatus, _) = await ExecuteAsync<JsonElement>(first);
        var (duplicateStatus, _) = await ExecuteAsync<JsonElement>(duplicate);

        Assert.Equal(StatusCodes.Status400BadRequest, invalidStatus);
        Assert.Equal(StatusCodes.Status201Created, firstStatus);
        Assert.Equal(StatusCodes.Status409Conflict, duplicateStatus);
        Assert.Single(await deps.Mailboxes.ListAsync(Scope(), CancellationToken.None));
        Assert.Single(
            await deps.Audit.ListAsync(Scope(), CancellationToken.None),
            audit => audit.ActionName == "integration.mailbox.configured");
    }

    [Fact]
    public async Task ProcessMessage_CreatesCaseAndDocuments()
    {
        var deps = CreateDeps();
        var message = await CreateMessage(deps);

        const string auditReason = "Integration admin manually processed the inbound claim email.";
        var result = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            Options(),
            Context(userId: "runtime-it-admin", auditReason: auditReason),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var cases = await deps.Cases.ListAsync(Scope());
        var documents = await deps.Documents.ListForCaseAsync(body.GetProperty("caseId").GetGuid(), Scope());
        var auditEvents = await deps.Audit.ListAsync(Scope(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(cases);
        Assert.Equal("runtime-it-admin", cases.Single().CreatedByUserId);
        Assert.NotEmpty(documents);
        Assert.Contains(auditEvents, audit =>
            audit.ActionName == "email.case_created"
            && audit.AuditReason == auditReason);
    }

    [Fact]
    public async Task ProcessMessage_RejectsMissingAuditReasonBeforeMutation()
    {
        var deps = CreateDeps();
        var message = await CreateMessage(deps);

        var result = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            Options(),
            Context(auditReason: null),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);
        var storedMessage = await deps.Emails.GetAsync(message.MessageId, Scope(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Empty(await deps.Cases.ListAsync(Scope(), CancellationToken.None));
        Assert.Equal(EmailProcessingStatus.Received, storedMessage?.ProcessingStatus);
        Assert.Empty(await deps.Audit.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task ProcessMessage_LinksExistingCaseByCaseId()
    {
        var deps = CreateDeps();
        var existing = Case(Scope());
        await deps.Cases.SaveAsync(existing);
        var message = (await CreateMessage(deps)) with
        {
            Subject = $"Claim follow-up {existing.CaseId}"
        };
        await deps.Emails.SaveAsync(message);

        var result = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var (_, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(existing.CaseId, body.GetProperty("caseId").GetGuid());
        Assert.Equal("case_id", body.GetProperty("matchReason").GetString());
    }

    [Fact]
    public async Task GraphIngestion_PullsUnreadMessagesAndPreventsDuplicates()
    {
        var graphClient = new FakeGraphMailClient();
        graphClient.Messages.Add(new GraphMailboxMessage(
            "graph-message-1",
            "<graph-message-1@example.test>",
            "customer@example.test",
            "Claim POL-1",
            "Customer sent a claim form. This preview is safe.",
            DateTimeOffset.UtcNow,
            [
                new GraphMailboxAttachment("att-1", "claim-form.pdf", "application/pdf", 2048, IsInline: false),
                new GraphMailboxAttachment("inline-logo", "logo.png", "image/png", 1024, IsInline: true)
            ]));
        var deps = CreateDeps(GraphOptions(), graphClient);
        await deps.Mailboxes.SaveAsync(Mailbox());

        var first = await EmailIntegrationEndpoints.TestIngestion(
            deps.Mailboxes,
            deps.Emails,
            deps.EmailProvider,
            deps.GraphProvider,
            deps.Audit,
            GraphOptions(),
            Context(),
            CancellationToken.None);
        var second = await EmailIntegrationEndpoints.TestIngestion(
            deps.Mailboxes,
            deps.Emails,
            deps.EmailProvider,
            deps.GraphProvider,
            deps.Audit,
            GraphOptions(),
            Context(),
            CancellationToken.None);
        var (firstStatus, firstBody) = await ExecuteAsync<JsonElement>(first);
        var (_, secondBody) = await ExecuteAsync<JsonElement>(second);
        var stored = await deps.Emails.ListAsync(Scope());

        Assert.Equal(StatusCodes.Status200OK, firstStatus);
        Assert.Equal(1, firstBody.GetProperty("ingested").GetInt32());
        Assert.Equal(0, secondBody.GetProperty("ingested").GetInt32());
        Assert.Single(stored);
        Assert.Single(stored.Single().Attachments);
        Assert.Equal("att-1", stored.Single().Attachments.Single().ExternalAttachmentId);
        Assert.Equal("graph-message-1", stored.Single().ExternalMessageId);
    }

    [Fact]
    public async Task GraphProcessMessage_MovesProcessedMessageAndDoesNotDuplicateCase()
    {
        var graphClient = new FakeGraphMailClient();
        var deps = CreateDeps(GraphOptions(), graphClient);
        var mailbox = Mailbox() with { ProductLine = "Medical" };
        await deps.Mailboxes.SaveAsync(mailbox);
        var message = new InboundEmailMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = Scope(),
            MailboxConnectionId = mailbox.MailboxConnectionId,
            ExternalMessageId = "graph-message-2",
            Sender = "customer@example.test",
            Subject = "New claim",
            BodyPreview = "Safe preview.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments = []
        };
        await deps.Emails.SaveAsync(message);

        await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            GraphOptions(),
            Context(),
            CancellationToken.None);
        var repeated = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            GraphOptions(),
            Context(),
            CancellationToken.None);
        var (repeatStatus, repeatBody) = await ExecuteAsync<JsonElement>(repeated);
        var cases = await deps.Cases.ListAsync(Scope());

        Assert.Equal(StatusCodes.Status200OK, repeatStatus);
        Assert.Equal("already_processed", repeatBody.GetProperty("matchReason").GetString());
        Assert.Single(cases);
        Assert.Equal("Medical", cases.Single().ProductLine);
        Assert.Contains(("claims@demo.local", "graph-message-2", "Processed"), graphClient.MovedMessages);
    }

    [Fact]
    public async Task GraphProcessMessage_DownloadsAndProcessesAttachments()
    {
        var graphClient = new FakeGraphMailClient();
        var deps = CreateDeps(GraphOptions(), graphClient);
        var mailbox = Mailbox() with { ProductLine = "Motor" };
        await deps.Mailboxes.SaveAsync(mailbox);
        var messageId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        graphClient.Attachments[("claims@demo.local", "graph-message-attachments", "att-claim")] =
            new GraphAttachmentContent("claim-form.pdf", "application/pdf", 17, Encoding.UTF8.GetBytes("claim form content"));
        var message = new InboundEmailMessage
        {
            MessageId = messageId,
            Scope = Scope(),
            MailboxConnectionId = mailbox.MailboxConnectionId,
            ExternalMessageId = "graph-message-attachments",
            Sender = "customer@example.test",
            Subject = "New claim with attachment",
            BodyPreview = "Safe preview.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments =
            [
                new EmailAttachment(attachmentId, messageId, "claim-form.pdf", "application/pdf", 17, "att-claim")
            ]
        };
        await deps.Emails.SaveAsync(message);

        var result = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            GraphOptions(),
            Context(),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var documents = await deps.Documents.ListForCaseAsync(body.GetProperty("caseId").GetGuid(), Scope());

        Assert.Equal(StatusCodes.Status200OK, status);
        var document = Assert.Single(documents);
        Assert.Equal(DocumentType.ClaimForm, document.DocumentType);
        Assert.Equal(DocumentProcessingStatus.Processed, document.ProcessingStatus);
        Assert.DoesNotContain("mock-email", document.ObjectKey, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(("claims@demo.local", "graph-message-attachments", "Processed"), graphClient.MovedMessages);
    }

    [Fact]
    public async Task GraphProcessMessage_MissingAttachmentContentFailsClosed()
    {
        var graphClient = new FakeGraphMailClient();
        var deps = CreateDeps(GraphOptions(), graphClient);
        var mailbox = Mailbox() with { ProductLine = "Motor" };
        await deps.Mailboxes.SaveAsync(mailbox);
        var messageId = Guid.NewGuid();
        var message = new InboundEmailMessage
        {
            MessageId = messageId,
            Scope = Scope(),
            MailboxConnectionId = mailbox.MailboxConnectionId,
            ExternalMessageId = "graph-message-missing-attachment",
            Sender = "customer@example.test",
            Subject = "New claim with missing attachment payload",
            BodyPreview = "Safe preview.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments =
            [
                new EmailAttachment(Guid.NewGuid(), messageId, "claim-form.pdf", "application/pdf", 17, "att-missing")
            ]
        };
        await deps.Emails.SaveAsync(message);

        var result = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            GraphOptions(),
            Context(),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var cases = await deps.Cases.ListAsync(Scope());
        var storedMessage = await deps.Emails.GetAsync(messageId, Scope());
        var auditEvents = await deps.Audit.ListAsync(Scope(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("attachment processing", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cases);
        Assert.Equal(EmailProcessingStatus.Failed, storedMessage?.ProcessingStatus);
        Assert.Contains(auditEvents, audit => audit.ActionName == "email.attachment_processing_failed");
        Assert.Empty(graphClient.MovedMessages);
    }

    [Fact]
    public async Task ProcessMessage_UsesUnassignedProductLineWhenMailboxHasNoProductLine()
    {
        var deps = CreateDeps();
        var mailbox = Mailbox() with { ProductLine = null };
        await deps.Mailboxes.SaveAsync(mailbox);
        var message = new InboundEmailMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = Scope(),
            MailboxConnectionId = mailbox.MailboxConnectionId,
            ExternalMessageId = "mail-without-product",
            Sender = "customer@example.test",
            Subject = "New claim",
            BodyPreview = "Safe preview.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments = []
        };
        await deps.Emails.SaveAsync(message);

        var result = await EmailIntegrationEndpoints.ProcessMessage(
            new ProcessEmailMessageRequest(message.MessageId),
            deps.Emails,
            deps.Mailboxes,
            deps.GraphProvider,
            deps.Matcher,
            deps.Cases,
            deps.Documents,
            deps.AttachmentProcessor,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);
        var cases = await deps.Cases.ListAsync(Scope());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(cases);
        Assert.Equal("Unassigned", cases.Single().ProductLine);
    }

    [Fact]
    public async Task GraphEmailSend_RequiresApprovalAndUsesConfiguredMailbox()
    {
        var graphClient = new FakeGraphMailClient();
        var options = GraphOptions() with { CommunicationProvider = "GraphEmail" };
        var provider = new GraphCommunicationSendingProvider(options, graphClient);
        var deps = CreateDeps(options, graphClient, provider);
        const string auditReason = "Customer approved Graph email update after human review.";

        var blocked = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(Guid.NewGuid(), "customer@example.test", "Sensitive update", "local-service-agent", Approved: false, LowRisk: false),
            provider,
            deps.Communications,
            deps.Audit,
            options,
            Context(),
            CancellationToken.None);
        var sent = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(Guid.NewGuid(), "customer@example.test", "Approved update", "local-service-agent", Approved: true, LowRisk: false),
            provider,
            deps.Communications,
            deps.Audit,
            options,
            Context(auditReason: auditReason),
            CancellationToken.None);
        var (blockedStatus, blockedBody) = await ExecuteAsync<JsonElement>(blocked);
        var (sentStatus, _) = await ExecuteAsync<JsonElement>(sent);

        Assert.Equal(StatusCodes.Status400BadRequest, blockedStatus);
        Assert.Equal("ApprovalRequired", blockedBody.GetProperty("deliveryStatus").GetString());
        Assert.Equal(StatusCodes.Status200OK, sentStatus);
        Assert.Single(graphClient.SentMessages);
        Assert.Equal("claims@demo.local", graphClient.SentMessages.Single().MailboxAddress);
        var auditEvents = await deps.Audit.ListAsync(Scope(), CancellationToken.None);
        Assert.Contains(auditEvents, audit =>
            audit.ActionName == "notification.email.sent"
            && audit.ModelProvider == "GraphEmail"
            && audit.AuditReason == auditReason);
    }

    [Fact]
    public async Task TestIngestion_RequiresConfiguredMailbox()
    {
        var deps = CreateDeps();

        var ingestion = await EmailIntegrationEndpoints.TestIngestion(
            deps.Mailboxes,
            deps.Emails,
            deps.EmailProvider,
            deps.GraphProvider,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(ingestion);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("No mailbox is configured", body.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(await deps.Mailboxes.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task TestIngestion_RejectsMockProviderOutsideDevelopment()
    {
        var options = new InfrastructureOptions { AuthMode = "Entra", EmailProvider = "Mock" };
        var deps = CreateDeps(options);

        var ingestion = await EmailIntegrationEndpoints.TestIngestion(
            deps.Mailboxes,
            deps.Emails,
            deps.EmailProvider,
            deps.GraphProvider,
            deps.Audit,
            options,
            AuthenticatedContext("Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(ingestion);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("isolated Development mock mode", body.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(await deps.Emails.ListAsync(Scope(), CancellationToken.None));
        Assert.Empty(await deps.Audit.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task CommunicationSend_RequiresApprovalForSensitiveMessage()
    {
        var deps = CreateDeps();
        var result = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(Guid.NewGuid(), "customer@example.test", "Sensitive update", "local-service-agent", Approved: false, LowRisk: false),
            deps.CommunicationProvider,
            deps.Communications,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("ApprovalRequired", body.GetProperty("deliveryStatus").GetString());
    }

    [Fact]
    public async Task MockEmailAndSmsSend_CreateCommunicationRecords()
    {
        var deps = CreateDeps();
        var caseId = Guid.NewGuid();
        await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(caseId, "customer@example.test", "Approved customer update", "local-service-agent", Approved: true, LowRisk: false),
            deps.CommunicationProvider,
            deps.Communications,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);
        await CommunicationEndpoints.SendSms(
            new SendCommunicationRequest(caseId, "+256700000000", "Approved SMS update", "local-service-agent", Approved: true, LowRisk: false),
            deps.CommunicationProvider,
            deps.Communications,
            deps.Audit,
            Options(),
            Context(),
            CancellationToken.None);

        var history = await deps.Communications.ListForCaseAsync(caseId, Scope());
        var auditEvents = await deps.Audit.ListAsync(Scope(), CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.All(history, item => Assert.Equal("SimulatedSent", item.DeliveryStatus));
        Assert.Contains(auditEvents, item => item.ActionName == "notification.email.simulated" && item.ModelProvider == "Mock");
        Assert.Contains(auditEvents, item => item.ActionName == "notification.sms.simulated" && item.ModelProvider == "Mock");
    }

    [Fact]
    public async Task LiveModeMissingConfig_ReturnsSafeError()
    {
        var deps = CreateDeps(new InfrastructureOptions { CommunicationProvider = "GraphEmail" });
        var result = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(Guid.NewGuid(), "customer@example.test", "Approved update", "local-service-agent", Approved: true, LowRisk: false),
            deps.CommunicationProvider,
            deps.Communications,
            deps.Audit,
            new InfrastructureOptions { AuthMode = "Development", CommunicationProvider = "GraphEmail" },
            Context(),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("ConfigurationRequired", body.GetProperty("deliveryStatus").GetString());
        Assert.DoesNotContain("secret", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommunicationSend_RequiresSendCustomerUpdatePermission()
    {
        var options = new InfrastructureOptions { AuthMode = "Entra", CommunicationProvider = "GraphEmail" };
        var deps = CreateDeps(options);

        var result = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(Guid.NewGuid(), "customer@example.test", "Approved update", "broker-user", Approved: true, LowRisk: false),
            deps.CommunicationProvider,
            deps.Communications,
            deps.Audit,
            options,
            AuthenticatedContext("Broker"),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task MockCommunicationProviderOutsideDevelopment_ReturnsConfigurationRequired()
    {
        var options = new InfrastructureOptions { AuthMode = "Entra", CommunicationProvider = "Mock" };
        var deps = CreateDeps(options);
        var result = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(Guid.NewGuid(), "customer@example.test", "Approved update", "entra-user", Approved: true, LowRisk: false),
            deps.CommunicationProvider,
            deps.Communications,
            deps.Audit,
            options,
            AuthenticatedContext("Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("ConfigurationRequired", body.GetProperty("deliveryStatus").GetString());
        Assert.Empty(await deps.Communications.ListAsync(Scope(), CancellationToken.None));
        Assert.Empty(await deps.Audit.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task MockTeamsProviderOutsideDevelopment_ReturnsConfigurationRequired()
    {
        var options = new InfrastructureOptions { AuthMode = "Entra", NotificationProvider = "Mock" };
        var deps = CreateDeps(options);
        var result = await CommunicationEndpoints.SendTeamsNotification(
            new SendTeamsNotificationRequest(Guid.NewGuid(), "SlaAtRisk", "Case escalation", "entra-user"),
            new MockTeamsNotificationProvider(options),
            deps.Audit,
            options,
            AuthenticatedContext("Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("ConfigurationRequired", body.GetProperty("deliveryStatus").GetString());
        Assert.Empty(await deps.Audit.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task TenantWorkspaceMismatchRejectedForEmailMessage()
    {
        var deps = CreateDeps();
        var message = await CreateMessage(deps);

        var result = await EmailIntegrationEndpoints.GetMessage(message.MessageId, deps.Emails, Options(), Context(workspaceId: "wrong"), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task CommunicationHistory_ExcludesInternalContent()
    {
        var deps = CreateDeps();
        var caseId = Guid.NewGuid();
        await deps.Cases.SaveAsync(Case(Scope()) with { CaseId = caseId });
        await deps.Communications.SaveAsync(new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = Scope(),
            CaseId = caseId,
            Channel = CommunicationChannel.Email,
            Direction = "Outbound",
            MessageBody = "internal note raw prompt",
            SentBy = "local-service-agent",
            DeliveryStatus = "SimulatedSent",
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        });

        var result = await CommunicationEndpoints.ListForCase(caseId, deps.Cases, deps.Communications, Options(), Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(body.EnumerateArray());
    }

    private static async Task<InboundEmailMessage> CreateMessage(Deps deps)
    {
        var mailbox = new MailboxConnection
        {
            MailboxConnectionId = Guid.NewGuid(),
            Scope = Scope(),
            DisplayName = "Claims",
            MailboxAddress = "claims@demo.local",
            IngestionEnabled = true,
            DefaultChannel = "Email",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await deps.Mailboxes.SaveAsync(mailbox);
        var message = await deps.EmailProvider.CreateTestMessageAsync(mailbox);
        await deps.Emails.SaveAsync(message);
        return message;
    }

    private static Deps CreateDeps(
        InfrastructureOptions? options = null,
        FakeGraphMailClient? graphClient = null,
        ICommunicationSendingProvider? communicationSendingProvider = null)
    {
        var cases = new InMemoryCaseRepository();
        var effectiveOptions = options ?? Options();
        graphClient ??= new FakeGraphMailClient();
        var communicationProvider = communicationSendingProvider ?? new MockCommunicationSendingProvider(effectiveOptions);
        IEmailAttachmentProcessor attachmentProcessor = effectiveOptions.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase)
            ? new GraphEmailAttachmentProcessor(effectiveOptions, graphClient, new TestDocumentStorageProvider(), new MockDocumentProcessingProvider())
            : new MockEmailAttachmentProcessor();
        return new Deps(
            new InMemoryMailboxConnectionRepository(),
            new InMemoryInboundEmailRepository(),
            new MockEmailIngestionProvider(),
            new GraphMailProvider(effectiveOptions, graphClient),
            cases,
            new InMemoryCaseDocumentRepository(),
            new BasicEmailToCaseMatcher(cases),
            attachmentProcessor,
            new InMemoryAuditRepository(),
            new InMemoryCommunicationRepository(),
            communicationProvider);
    }

    private static InfrastructureOptions Options() => new() { AuthMode = "Development", CommunicationProvider = "Mock" };

    private static InfrastructureOptions GraphOptions() => new()
    {
        AuthMode = "Development",
        EmailProvider = "Graph",
        CommunicationProvider = "Mock",
        GraphMail = new GraphMailOptions
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "configured-outside-source",
            DefaultMailboxAddress = "claims@demo.local",
            MailboxAccessScoped = true,
            ProcessedFolderName = "Processed"
        }
    };

    private static TenantScope Scope() => TenantScope.Create("tenant-demo", "workspace-claims");

    private static DefaultHttpContext Context(
        string role = "Admin",
        string workspaceId = "workspace-claims",
        string? userId = "local-it-admin",
        string? auditReason = "Integration mutation approved for test execution.")
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = workspaceId;
        context.Request.Headers["x-demo-role"] = role;
        if (userId is not null)
        {
            context.Request.Headers["x-demo-user-id"] = userId;
        }
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext AuthenticatedContext(string role)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-authenticated" };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-user"),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", "tenant-demo"),
            new Claim("workspace_id", "workspace-claims")
        ], "Entra"));
        // ADR-0005: deployed-mode authz reads store-resolved permissions the middleware stashes.
        // Simulate that here so these provider tests exercise handler behaviour, not the auth gate.
        context.Items[InsuranceIntelligence.Api.Auth.DemoAuthorization.EffectivePermissionsItemKey] =
            (IReadOnlyCollection<InsuranceIntelligence.Domain.Administration.Permission>)(
                Enum.TryParse<InsuranceIntelligence.Domain.Administration.PlatformRole>(role, true, out var pr)
                    ? InsuranceIntelligence.Domain.Administration.RolePermissionMap.ForRole(pr).ToArray()
                    : Array.Empty<InsuranceIntelligence.Domain.Administration.Permission>());
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static ClaimCase Case(TenantScope scope) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Amina",
            PolicyNumber = "POL-1",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Email",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IncidentDescription = "Demo",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        };

    private static MailboxConnection Mailbox() =>
        new()
        {
            MailboxConnectionId = Guid.NewGuid(),
            Scope = Scope(),
            DisplayName = "Claims",
            MailboxAddress = "claims@demo.local",
            IngestionEnabled = true,
            DefaultChannel = "Email",
            ProductLine = "Motor",
            ProcessedFolderName = "Processed",
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default!
            : (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed record Deps(
        InMemoryMailboxConnectionRepository Mailboxes,
        InMemoryInboundEmailRepository Emails,
        MockEmailIngestionProvider EmailProvider,
        GraphMailProvider GraphProvider,
        InMemoryCaseRepository Cases,
        InMemoryCaseDocumentRepository Documents,
        BasicEmailToCaseMatcher Matcher,
        IEmailAttachmentProcessor AttachmentProcessor,
        InMemoryAuditRepository Audit,
        InMemoryCommunicationRepository Communications,
        ICommunicationSendingProvider CommunicationProvider);

    private sealed class FakeGraphMailClient : IGraphMailClient
    {
        public List<GraphMailboxMessage> Messages { get; } = [];
        public Dictionary<(string MailboxAddress, string ExternalMessageId, string ExternalAttachmentId), GraphAttachmentContent> Attachments { get; } = [];
        public List<(string MailboxAddress, string ExternalMessageId, string FolderName)> MovedMessages { get; } = [];
        public List<GraphMailSendRequest> SentMessages { get; } = [];

        public Task<IReadOnlyCollection<GraphMailboxMessage>> ReadMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<GraphMailboxMessage>>(Messages);

        public Task<GraphAttachmentContent> DownloadAttachmentAsync(
            string mailboxAddress,
            string externalMessageId,
            string externalAttachmentId,
            CancellationToken cancellationToken = default)
        {
            if (Attachments.TryGetValue((mailboxAddress, externalMessageId, externalAttachmentId), out var content))
            {
                return Task.FromResult(content);
            }

            throw new InvalidOperationException("Graph attachment test content was not configured.");
        }

        public Task MoveMessageAsync(string mailboxAddress, string externalMessageId, string folderName, CancellationToken cancellationToken = default)
        {
            MovedMessages.Add((mailboxAddress, externalMessageId, folderName));
            return Task.CompletedTask;
        }

        public Task<GraphMailSendResult> SendMailAsync(GraphMailSendRequest request, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(request);
            return Task.FromResult(new GraphMailSendResult("graph-send-test"));
        }
    }

    private sealed class TestDocumentStorageProvider : IDocumentStorageProvider
    {
        private readonly Dictionary<string, byte[]> payloads = [];

        public async Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            var objectKey = string.Join(
                '/',
                request.Scope.TenantId,
                request.Scope.WorkspaceId,
                request.CaseId,
                $"{request.DocumentId}_{Path.GetFileName(request.OriginalFileName)}");
            payloads[objectKey] = memory.ToArray();
            return new DocumentStorageResult(objectKey, request.ContentType, request.FileSizeBytes);
        }

        public Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(payloads[objectKey], writable: false));
    }
}

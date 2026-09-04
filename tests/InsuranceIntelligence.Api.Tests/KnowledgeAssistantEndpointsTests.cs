using System.Text;
using System.Text.Json;
using System.Security.Claims;
using InsuranceIntelligence.Api.Assistant;
using InsuranceIntelligence.Api.Knowledge;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace InsuranceIntelligence.Api.Tests;

public sealed class KnowledgeAssistantEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UploadKnowledgeDocument_ReturnsDraftDocument()
    {
        var dependencies = CreateDependencies();

        var result = await UploadKnowledgeAsync(dependencies);
        var (statusCode, response) = await ExecuteAsync<KnowledgeDocumentResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("Draft", response!.ApprovalStatus);
        Assert.Equal("NotIndexed", response.IndexedStatus);
        Assert.Contains(response.AllowedActions, action => action.Action == "approveKnowledgeDocument");
        Assert.Contains(response.AllowedActions, action => action.Action == "archiveKnowledgeDocument");
        Assert.DoesNotContain("objectKey", JsonSerializer.Serialize(response, JsonOptions), StringComparison.OrdinalIgnoreCase);
        var auditEvents = await dependencies.AuditRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims"));
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "knowledge.uploaded"
            && auditEvent.UserId == "user-claims-1");
    }

    [Fact]
    public async Task ApproveKnowledgeDocument_IndexesDocument()
    {
        var dependencies = CreateDependencies();
        var document = await UploadAndApproveAsync(dependencies);

        Assert.Equal("Approved", document.ApprovalStatus);
        Assert.Equal("Indexed", document.IndexedStatus);
        Assert.Contains(document.BlockedActions, action => action.Action == "approveKnowledgeDocument");
        Assert.Contains(document.AllowedActions, action => action.Action == "archiveKnowledgeDocument");
    }

    [Fact]
    public async Task ApproveKnowledgeDocument_RejectsMissingGovernanceReason()
    {
        var dependencies = CreateDependencies();
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        var approveResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext(governanceReason: null),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(approveResult);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task ApproveKnowledgeDocument_RejectsUnknownOrMissingSensitivity()
    {
        var dependencies = CreateDependencies();
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        var missingResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext("Approve without a real sensitivity label.", sensitivity: null),
            CancellationToken.None);
        var (missingStatus, missingBody) = await ExecuteAsync<JsonElement>(missingResult);

        var unknownResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext("Approve with explicit Unknown sensitivity.", sensitivity: "Unknown"),
            CancellationToken.None);
        var (unknownStatus, _) = await ExecuteAsync<JsonElement>(unknownResult);

        Assert.Equal(StatusCodes.Status400BadRequest, missingStatus);
        Assert.True(missingBody.GetProperty("errors").TryGetProperty("sensitivity", out _));
        Assert.Equal(StatusCodes.Status400BadRequest, unknownStatus);
    }

    [Theory]
    [InlineData("ContainsSecrets")]
    [InlineData("ContainsUnapprovedPii")]
    public async Task ApproveKnowledgeDocument_NeverApprovesSecretsOrUnapprovedPii(string sensitivity)
    {
        var dependencies = CreateDependencies();
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        var result = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext("An approver mistakenly tries to approve unsafe content.", sensitivity: sensitivity),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Equal("approveKnowledgeDocument", body.GetProperty("blockedAction").GetString());

        var stored = await dependencies.KnowledgeRepository.GetAsync(uploaded.KnowledgeDocumentId, TenantScope.Create("tenant-demo", "workspace-claims"), CancellationToken.None);
        Assert.Equal(KnowledgeDocumentStatus.Draft, stored!.ApprovalStatus);
    }

    [Fact]
    public async Task ApproveKnowledgeDocument_PersistsSensitivityAndGovernanceReason()
    {
        var dependencies = CreateDependencies();
        const string reason = "Legal confirmed this policy wording is safe for staff-only grounding.";
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        var result = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext(reason, sensitivity: "StaffOnly"),
            CancellationToken.None);
        var (statusCode, approved) = await ExecuteAsync<KnowledgeDocumentResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("StaffOnly", approved!.Sensitivity);
        Assert.Equal(reason, approved.GovernanceReason);
    }

    [Fact]
    public async Task ArchiveKnowledgeDocument_RemovesDocumentFromRetrieval()
    {
        var dependencies = CreateDependencies();
        var document = await UploadAndApproveAsync(dependencies);

        var archiveResult = await KnowledgeEndpoints.ArchiveKnowledgeDocument(
            document.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Archive approved wording after retrieval removal test."),
            CancellationToken.None);
        var (archiveStatus, _) = await ExecuteAsync<KnowledgeDocumentResponse>(archiveResult);

        Assert.Equal(StatusCodes.Status200OK, archiveStatus);

        var answer = await AskAsync(dependencies, "Is windscreen damage covered?", productLine: "Motor");

        Assert.True(answer.EscalationRequired);
        Assert.Empty(answer.Sources);
    }

    [Fact]
    public async Task ApproveAndArchiveKnowledgeDocument_PersistAuditReasonHeader()
    {
        var dependencies = CreateDependencies();
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        const string approvalReason = "Knowledge manager approved this policy wording for grounded answers.";
        var approveResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext(approvalReason),
            CancellationToken.None);
        var (approveStatus, approved) = await ExecuteAsync<KnowledgeDocumentResponse>(approveResult);

        const string archiveReason = "Knowledge manager archived the superseded policy wording.";
        var archiveResult = await KnowledgeEndpoints.ArchiveKnowledgeDocument(
            approved!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: archiveReason),
            CancellationToken.None);
        var (archiveStatus, _) = await ExecuteAsync<KnowledgeDocumentResponse>(archiveResult);
        var auditEvents = await dependencies.AuditRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims"));

        Assert.Equal(StatusCodes.Status200OK, approveStatus);
        Assert.Equal(StatusCodes.Status200OK, archiveStatus);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "knowledge.approved"
            && auditEvent.AuditReason == approvalReason);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "knowledge.archived"
            && auditEvent.AuditReason == archiveReason);
    }

    [Fact]
    public async Task KnowledgeLifecycleActions_RequireAuditReason()
    {
        var dependencies = CreateDependencies();
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        var approveWithoutReason = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext(governanceReason: null),
            CancellationToken.None);
        var (approveStatus, approveBody) = await ExecuteAsync<JsonElement>(approveWithoutReason);

        Assert.Equal(StatusCodes.Status400BadRequest, approveStatus);
        Assert.True(approveBody.GetProperty("errors").TryGetProperty("governanceReason", out _));

        var approveWithReason = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext("Approve source before archive reason validation."),
            CancellationToken.None);
        var (_, approved) = await ExecuteAsync<KnowledgeDocumentResponse>(approveWithReason);

        var archiveWithoutReason = await KnowledgeEndpoints.ArchiveKnowledgeDocument(
            approved!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (archiveStatus, archiveBody) = await ExecuteAsync<JsonElement>(archiveWithoutReason);

        Assert.Equal(StatusCodes.Status400BadRequest, archiveStatus);
        Assert.True(archiveBody.GetProperty("errors").TryGetProperty("auditReason", out _));
    }

    [Fact]
    public async Task KnowledgeWithoutExtractedText_CannotBeApprovedOrReceiveFakeChunks()
    {
        var dependencies = CreateDependencies(new EmptyKnowledgeStorageProvider());
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);

        Assert.Equal(0, uploaded!.ChunkCount);
        Assert.DoesNotContain(uploaded.AllowedActions, action => action.Action == "approveKnowledgeDocument");
        Assert.Contains(uploaded.BlockedActions, action =>
            action.Action == "approveKnowledgeDocument"
            && action.Reason!.Contains("extractable source text", StringComparison.OrdinalIgnoreCase));

        var approveResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempt approval of empty extracted source."),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(approveResult);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public async Task ApproveKnowledgeDocument_BlocksRepeatApproval()
    {
        var dependencies = CreateDependencies();
        var document = await UploadAndApproveAsync(dependencies);

        var repeatResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            document.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Repeat approval attempt for an already approved document."),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(repeatResult);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public async Task ArchiveKnowledgeDocument_BlocksRepeatArchive()
    {
        var dependencies = CreateDependencies();
        var document = await UploadAndApproveAsync(dependencies);
        var archiveResult = await KnowledgeEndpoints.ArchiveKnowledgeDocument(
            document.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Archive approved wording before repeat archive validation."),
            CancellationToken.None);
        var (archiveStatus, archived) = await ExecuteAsync<KnowledgeDocumentResponse>(archiveResult);

        Assert.Equal(StatusCodes.Status200OK, archiveStatus);
        Assert.Contains(archived!.BlockedActions, action => action.Action == "archiveKnowledgeDocument");

        var repeatResult = await KnowledgeEndpoints.ArchiveKnowledgeDocument(
            document.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Repeat archive attempt for an already archived document."),
            CancellationToken.None);
        var (repeatStatus, _) = await ExecuteAsync<JsonElement>(repeatResult);

        Assert.Equal(StatusCodes.Status409Conflict, repeatStatus);
    }

    [Fact]
    public async Task PolicyQuestion_ReturnsGroundedAnswerWithSource()
    {
        var dependencies = CreateDependencies();
        await UploadAndApproveAsync(dependencies);

        var answer = await AskAsync(dependencies, "Is windscreen damage covered?", productLine: "Motor");

        Assert.False(answer.EscalationRequired);
        Assert.NotEmpty(answer.Sources);
        Assert.Contains("approved", answer.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("GroundedAnswerAvailable", answer.CoverageState);
    }

    [Fact]
    public async Task PolicyQuestionWithoutSource_Escalates()
    {
        var dependencies = CreateDependencies();

        var answer = await AskAsync(dependencies, "Is windscreen damage covered?", productLine: "Motor");

        Assert.True(answer.EscalationRequired);
        Assert.Empty(answer.Sources);
        Assert.Contains("cannot answer", answer.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("InsufficientApprovedEvidence", answer.CoverageState);
    }

    [Fact]
    public async Task PolicyQuestionWithLimitedEvidence_ReturnsCoverageUnclearState()
    {
        var dependencies = CreateDependencies();
        await UploadAndApproveAsync(dependencies);

        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(null, "Is windscreen damage covered?", "Motor", "InternalUser"),
            dependencies.RetrievalProvider,
            new NeedsReviewAssistantProvider(),
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            dependencies.CaseRepository,
            new LocalTestAgentGate(),
            new LocalTestAgentRunRecorder(),
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, answer) = await ExecuteAsync<PolicyQuestionResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("CoverageUnclear", answer!.CoverageState);
        Assert.True(answer.EscalationRequired);
        Assert.NotEmpty(answer.Sources);
    }

    [Fact]
    public async Task PolicyQuestion_UsesAuthenticatedActorInsteadOfBrowserHeader()
    {
        var dependencies = CreateDependencies();
        var context = ScopedContext();
        context.Request.Headers["X-User-Id"] = "spoofed-browser-user";
        context.Request.Headers["x-demo-user-id"] = "spoofed-demo-user";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-policy-reviewer"),
            new Claim(ClaimTypes.Role, "ClaimsOfficer")
        ], "Entra"));

        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(null, "Is windscreen damage covered?", "Motor", "InternalUser"),
            dependencies.RetrievalProvider,
            dependencies.AssistantProvider,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            dependencies.CaseRepository,
            new LocalTestAgentGate(),
            new LocalTestAgentRunRecorder(),
            context,
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<PolicyQuestionResponse>(result);
        var action = Assert.Single(await dependencies.ActionLogRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims")));

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("entra-policy-reviewer", action.UserId);
    }

    [Fact]
    public async Task PolicyQuestion_DisabledPolicyIntelligenceAgent_RefusesInsteadOfAnswering()
    {
        // WO-06: this is the endpoint entry point AssistantEndpoints.AskPolicyQuestion uses,
        // distinct from — but the same agent key as — the policy-answer path Command Center
        // reaches through BuildPolicyAnswerResponseAsync. Both must be gated.
        var dependencies = CreateDependencies();
        await UploadAndApproveAsync(dependencies);

        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(null, "Is windscreen damage covered?", "Motor", "InternalUser"),
            dependencies.RetrievalProvider,
            new ThrowsIfCalledAssistantProvider(),
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            dependencies.CaseRepository,
            new LocalTestAgentGate("policy-intelligence"),
            new LocalTestAgentRunRecorder(),
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, answer) = await ExecuteAsync<PolicyQuestionResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("agent_disabled", answer!.RiskFlag);
        Assert.True(answer.EscalationRequired);
        Assert.Contains("disabled for this workspace", answer.Answer, StringComparison.OrdinalIgnoreCase);

        var log = Assert.Single(await dependencies.ActionLogRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims")));
        Assert.Equal("agent_disabled", log.RiskFlag);
        Assert.Contains(
            await dependencies.AuditRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims")),
            evt => evt.ActionName == "assistant.policy_question.answered");
    }

    [Fact]
    public async Task PolicyQuestion_RejectsAuthenticatedIdentityWithoutRuntimeUserId()
    {
        var dependencies = CreateDependencies();
        var context = ScopedContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "ClaimsOfficer")
        ], "Entra"));

        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(null, "Is windscreen damage covered?", "Motor", "InternalUser"),
            dependencies.RetrievalProvider,
            dependencies.AssistantProvider,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            dependencies.CaseRepository,
            new LocalTestAgentGate(),
            new LocalTestAgentRunRecorder(),
            context,
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);
        var actions = await dependencies.ActionLogRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Empty(actions);
    }

    [Fact]
    public async Task PolicyQuestion_RejectsTenantWorkspaceMismatchForCase()
    {
        var dependencies = CreateDependencies();
        var caseId = await CreateCaseAsync(dependencies.CaseRepository, dependencies.AuditRepository, TenantScope.Create("tenant-demo", "workspace-claims"));

        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(caseId, "Is windscreen damage covered?", "Motor", "InternalUser"),
            dependencies.RetrievalProvider,
            dependencies.AssistantProvider,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            dependencies.CaseRepository,
            new LocalTestAgentGate(),
            new LocalTestAgentRunRecorder(),
            ScopedContext(workspaceId: "wrong-workspace"),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task PolicyQuestion_WritesAssistantActionLog()
    {
        var dependencies = CreateDependencies();
        await UploadAndApproveAsync(dependencies);

        await AskAsync(dependencies, "Is windscreen damage covered?", productLine: "Motor");
        var logs = await dependencies.ActionLogRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims"));

        var log = Assert.Single(logs);
        Assert.Equal("PolicyQnAAgent", log.AgentName);
        Assert.False(log.HumanOverride);
    }

    [Fact]
    public async Task CustomerAudienceResponseDoesNotExposeInternalFields()
    {
        var dependencies = CreateDependencies();
        await UploadAndApproveAsync(dependencies);

        var answer = await AskAsync(dependencies, "Is windscreen damage covered?", productLine: "Motor", audience: "Customer");

        Assert.DoesNotContain("internal", answer.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", answer.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system", answer.Answer, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<KnowledgeDocumentResponse> UploadAndApproveAsync(Dependencies dependencies)
    {
        var uploadResult = await UploadKnowledgeAsync(dependencies);
        var (_, uploaded) = await ExecuteAsync<KnowledgeDocumentResponse>(uploadResult);
        var approveResult = await KnowledgeEndpoints.ApproveKnowledgeDocument(
            uploaded!.KnowledgeDocumentId,
            dependencies.KnowledgeRepository,
            dependencies.IndexProvider,
            dependencies.AuditRepository,
            KnowledgeGovernanceContext("Approve policy wording for grounded test answers."),
            CancellationToken.None);
        var (statusCode, approved) = await ExecuteAsync<KnowledgeDocumentResponse>(approveResult);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        return approved!;
    }

    private static async Task<IResult> UploadKnowledgeAsync(Dependencies dependencies) =>
        await KnowledgeEndpoints.UploadKnowledgeDocument(
            dependencies.KnowledgeRepository,
            dependencies.StorageProvider,
            dependencies.AuditRepository,
            KnowledgeUploadContext(),
            CancellationToken.None);

    private static async Task<PolicyQuestionResponse> AskAsync(
        Dependencies dependencies,
        string question,
        string? productLine,
        string audience = "InternalUser")
    {
        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(null, question, productLine, audience),
            dependencies.RetrievalProvider,
            dependencies.AssistantProvider,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            dependencies.CaseRepository,
            new LocalTestAgentGate(),
            new LocalTestAgentRunRecorder(),
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<PolicyQuestionResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        return response!;
    }

    private static async Task<Guid> CreateCaseAsync(ICaseRepository caseRepository, IAuditRepository auditRepository, TenantScope scope)
    {
        var service = new CaseIntakeService(caseRepository, auditRepository);
        var claimCase = await service.CreateCaseAsync(
            new CreateCaseCommand(
                scope,
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-1001",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Minor collision reported.",
                "user-claims-1",
                "corr-case-001"));

        return claimCase.CaseId;
    }

    private static Dependencies CreateDependencies(IKnowledgeStorageProvider? storageProvider = null)
    {
        var citationFormatter = new SourceCitationFormatter();
        var indexProvider = new InMemoryKnowledgeIndexProvider();
        var retrievalProvider = new MockRetrievalProvider(indexProvider, citationFormatter);

        return new Dependencies(
            new InMemoryKnowledgeDocumentRepository(),
            storageProvider ?? new StubKnowledgeStorageProvider(),
            indexProvider,
            retrievalProvider,
            new MockAiAssistantProvider(),
            new InMemoryAgentActionLogRepository(),
            new InMemoryAuditRepository(),
            new InMemoryCaseRepository());
    }

    /// <summary>
    /// WO-06 proof: if the kill-switch check in AssistantEndpoints.AskPolicyQuestion is bypassed
    /// and the assistant provider gets called anyway, this fails the test loudly instead of
    /// silently returning a plausible-looking answer.
    /// </summary>
    private sealed class ThrowsIfCalledAssistantProvider : IAiAssistantProvider
    {
        public Task<AssistantAnswer> AnswerPolicyQuestionAsync(
            AssistantQuestion question,
            RetrievalResult retrievalResult,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "The assistant provider was invoked despite the agent being disabled — the kill switch was bypassed.");
    }

    private sealed class NeedsReviewAssistantProvider : IAiAssistantProvider
    {
        public Task<AssistantAnswer> AnswerPolicyQuestionAsync(
            AssistantQuestion question,
            RetrievalResult retrievalResult,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssistantAnswer(
                "Approved wording was found, but the available evidence requires human interpretation.",
                AiConfidenceScore.Create(0.65m),
                true,
                retrievalResult.Citations,
                "A human reviewer must resolve the policy conditions before relying on this answer.",
                "needs_review",
                "TestProvider",
                "test-deployment",
                "policy-answer-test-v1"));
    }

    private static DefaultHttpContext ScopedContext(
        string? tenantId = "tenant-demo",
        string? workspaceId = "workspace-claims",
        string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-knowledge-001" };
        if (tenantId is not null)
        {
            context.Request.Headers["X-Tenant-Id"] = tenantId;
        }

        if (workspaceId is not null)
        {
            context.Request.Headers["X-Workspace-Id"] = workspaceId;
        }

        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-knowledge-001";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        return context;
    }

    private static DefaultHttpContext KnowledgeGovernanceContext(
        string? governanceReason, string actorUserId = "user-claims-1", string? sensitivity = "Approved")
    {
        var context = ScopedContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new KnowledgeGovernanceRequest(actorUserId, governanceReason, sensitivity), JsonOptions));
        return context;
    }

    private static DefaultHttpContext KnowledgeUploadContext()
    {
        var context = ScopedContext();
        var text = "Motor policy wording. Windscreen damage is covered subject to approved policy terms and claim review.";
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "motor-policy.md")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/markdown"
        };
        var files = new FormFileCollection { file };
        var form = new FormCollection(
            new Dictionary<string, StringValues>
            {
                ["documentName"] = "Motor Policy Wording",
                ["productLine"] = "Motor",
                ["documentType"] = "PolicyWording",
                ["version"] = "1.0",
                ["effectiveDate"] = "2026-01-01",
                ["expiryDate"] = "",
                ["uploadedByUserId"] = "spoofed-browser-uploader"
            },
            files);

        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = form;
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }

    private sealed record Dependencies(
        InMemoryKnowledgeDocumentRepository KnowledgeRepository,
        IKnowledgeStorageProvider StorageProvider,
        InMemoryKnowledgeIndexProvider IndexProvider,
        IRetrievalProvider RetrievalProvider,
        IAiAssistantProvider AssistantProvider,
        InMemoryAgentActionLogRepository ActionLogRepository,
        InMemoryAuditRepository AuditRepository,
        InMemoryCaseRepository CaseRepository);

    private sealed class StubKnowledgeStorageProvider : IKnowledgeStorageProvider
    {
        public async Task<KnowledgeStorageResult> StoreAsync(KnowledgeStorageRequest request, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken);

            return new KnowledgeStorageResult(
                $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/knowledge/{request.KnowledgeDocumentId}_{request.OriginalFileName}",
                request.ContentType,
                request.FileSizeBytes,
                text);
        }
    }

    private sealed class EmptyKnowledgeStorageProvider : IKnowledgeStorageProvider
    {
        public Task<KnowledgeStorageResult> StoreAsync(
            KnowledgeStorageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new KnowledgeStorageResult(
                $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/knowledge/{request.KnowledgeDocumentId}_{request.OriginalFileName}",
                request.ContentType,
                request.FileSizeBytes,
                string.Empty));
    }
}

using System.Text.Json;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppConfigurationEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task MessagingMode_DefaultsToReviewAndReportsDeploymentGate()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var result = await WhatsAppMessagingModeEndpoints.Get(repository, Options(), Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<WhatsAppMessagingModeResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Review", body!.Mode);
        Assert.Equal("Review", body.EffectiveMode);
        Assert.False(body.AutopilotAvailable);
    }

    [Fact]
    public async Task MessagingMode_UpdateIsTenantScopedAuditedAndCannotBypassDeploymentGate()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var audit = new InMemoryAuditRepository();
        var options = Options() with { WhatsApp = new WhatsAppOptions { AssistantAutoReplyEnabled = false } };

        var result = await WhatsAppMessagingModeEndpoints.Update(
            new UpdateWhatsAppMessagingModeRequest("Autopilot"), repository, audit, options,
            Context(auditReason: "Enable guarded replies after the pilot review."), CancellationToken.None);
        var (status, body) = await ExecuteAsync<WhatsAppMessagingModeResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Autopilot", body!.Mode);
        Assert.Equal("Review", body.EffectiveMode);
        Assert.False(body.AutopilotAvailable);
        Assert.Equal(CustomerMessagingMode.Autopilot, (await repository.GetAsync(Scope())).CustomerMessagingMode);
        Assert.Contains(await audit.ListAsync(Scope()), e =>
            e.ActionName == "integration.whatsapp.messaging_mode_updated"
            && e.BeforeStatus == "Review"
            && e.AfterStatus == "Autopilot"
            && e.AuditReason == "Enable guarded replies after the pilot review.");
    }

    [Fact]
    public async Task MessagingMode_RejectsMissingAuditReason()
    {
        var result = await WhatsAppMessagingModeEndpoints.Update(
            new UpdateWhatsAppMessagingModeRequest("Autopilot"), new InMemoryTenantConfigurationRepository(),
            new InMemoryAuditRepository(), Options(), Context(), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task GetStatus_WithNoSavedConfiguration_ReportsNotConnected()
    {
        var result = await WhatsAppConfigurationEndpoints.GetStatus(
            new InMemoryWhatsAppChannelConfigurationRepository(), Options(), Context(), CancellationToken.None);

        var (status, body) = await ExecuteAsync<WhatsAppConfigurationStatusResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.False(body!.Connected);
        Assert.Null(body.PhoneNumberId);
    }

    [Fact]
    public async Task SaveConfiguration_WhenMetaValidates_EncryptsAndPersistsAndRefreshesCache()
    {
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null));
        var settingsProvider = new RecordingRuntimeSettingsProvider();
        var audit = new InMemoryAuditRepository();

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            repository, protector, validationClient, settingsProvider, audit, Options(),
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var (status, body) = await ExecuteAsync<WhatsAppConfigurationStatusResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(body!.Connected);
        Assert.Equal("phone-1", body.PhoneNumberId);
        Assert.Equal("Acme Insurer", body.BusinessDisplayName);
        Assert.Equal("Success", body.LastValidationStatus);

        var stored = await repository.GetAsync(Scope(), CancellationToken.None);
        Assert.NotNull(stored);
        // The stored value must never be the plaintext secret, only what the protector produced.
        Assert.NotEqual("secret-token", stored!.EncryptedAccessToken);
        Assert.Equal("secret-token", protector.Unprotect(stored.EncryptedAccessToken));
        Assert.True(settingsProvider.Refreshed);

        var auditEvents = await audit.ListAsync(Scope(), CancellationToken.None);
        Assert.Contains(auditEvents, e => e.ActionName == "integration.whatsapp.configuration_saved" && e.AuditReason == "Connecting our WhatsApp Business number.");
        // The audit trail must never carry the secret itself.
        Assert.DoesNotContain(auditEvents, e => e.EvidenceIds.Any(id => id.Contains("secret-token")));
    }

    [Fact]
    public async Task SaveConfiguration_WithoutWabaId_IsRejectedBeforeCallingMeta()
    {
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null));

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "", null),
            new InMemoryWhatsAppChannelConfigurationRepository(), new FakeWhatsAppCredentialProtector(), validationClient,
            new RecordingRuntimeSettingsProvider(), new InMemoryAuditRepository(), Options(),
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Empty(validationClient.Calls); // never even reaches Meta without a WABA id
    }

    [Fact]
    public async Task SaveConfiguration_NeverAsksTheOperatorForAVerifyToken_GeneratesOneServerSide()
    {
        // The request shape itself is the assertion: WhatsAppSaveConfigurationRequest has no
        // VerifyToken parameter (blueprint §4.3 point 2), this only compiles if that is still true.
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null));

        await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            repository, protector, validationClient, new RecordingRuntimeSettingsProvider(), new InMemoryAuditRepository(), Options(),
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var stored = await repository.GetAsync(Scope(), CancellationToken.None);
        var verifyToken = protector.Unprotect(stored!.EncryptedVerifyToken);
        Assert.False(string.IsNullOrWhiteSpace(verifyToken));
        Assert.True(verifyToken.Length >= 32); // ClaimantTokenGenerator.GenerateRawToken's real entropy, not a placeholder
    }

    [Fact]
    public async Task SaveConfiguration_WithNoPublicBaseUrlConfigured_ReportsManualWebhookRegistrationRequired()
    {
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null));

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            new InMemoryWhatsAppChannelConfigurationRepository(), new FakeWhatsAppCredentialProtector(), validationClient,
            new RecordingRuntimeSettingsProvider(), new InMemoryAuditRepository(), Options(), // Options() sets no ApiPublicBaseUrl
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var (_, body) = await ExecuteAsync<WhatsAppConfigurationStatusResponse>(result);

        Assert.Equal("ManualRequired", body!.WebhookRegistrationStatus);
        Assert.Null(body.WebhookCallbackUrl);
        Assert.Empty(validationClient.WebhookRegistrationCalls); // never attempted without a callback URL to register
    }

    [Fact]
    public async Task SaveConfiguration_WithPublicBaseUrlConfigured_RegistersTheWebhookWithMeta()
    {
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null));
        var options = new InfrastructureOptions { AuthMode = "Development", ChannelProvider = "WhatsAppCloudApi", ApiPublicBaseUrl = "https://app-api-dev.example.net" };

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            new InMemoryWhatsAppChannelConfigurationRepository(), new FakeWhatsAppCredentialProtector(), validationClient,
            new RecordingRuntimeSettingsProvider(), new InMemoryAuditRepository(), options,
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var (_, body) = await ExecuteAsync<WhatsAppConfigurationStatusResponse>(result);

        Assert.Equal("Registered", body!.WebhookRegistrationStatus);
        Assert.Equal("https://app-api-dev.example.net/api/v1/integrations/whatsapp/webhook", body.WebhookCallbackUrl);
        var call = Assert.Single(validationClient.WebhookRegistrationCalls);
        Assert.Equal("waba-1", call.WabaId);
        Assert.Equal("https://app-api-dev.example.net/api/v1/integrations/whatsapp/webhook", call.CallbackUrl);
    }

    [Fact]
    public async Task SaveConfiguration_SavesAndRefreshesTheCacheBeforeAskingMetaToRegisterTheWebhook()
    {
        // Reproduces the live 2026-09-25 failure: Meta calls the new webhook right back as part of
        // accepting a subscribed_apps registration, to confirm the verify token. Asking Meta to
        // register before this endpoint had saved (or the runtime cache had picked up) that verify
        // token meant Meta's own callback always found "not configured" and rejected the
        // registration, every single time, with no way to retry into success.
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var settingsProvider = new RecordingRuntimeSettingsProvider();
        var order = new List<string>();
        settingsProvider.OnRefresh = () => order.Add("refreshed");
        var options = new InfrastructureOptions { AuthMode = "Development", ChannelProvider = "WhatsAppCloudApi", ApiPublicBaseUrl = "https://app-api-dev.example.net" };
        var protector = new FakeWhatsAppCredentialProtector();

        var validationClient = new FakeWhatsAppGraphValidationClient(
            new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null),
            onRegisterWebhook: async () =>
            {
                order.Add("registered");
                // The exact thing Meta's own callback would observe at this instant.
                var saved = await repository.GetAsync(Scope(), CancellationToken.None);
                Assert.NotNull(saved);
                Assert.Equal("Pending", saved!.WebhookRegistrationStatus);
                Assert.True(settingsProvider.Refreshed);
            });

        await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            repository, protector, validationClient, settingsProvider, new InMemoryAuditRepository(), options,
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        Assert.Equal(["refreshed", "registered"], order);
    }

    [Fact]
    public async Task SaveConfiguration_WhenMetaRejectsWebhookRegistration_StillPersistsCredentialsWithFailureRecorded()
    {
        // Valid credentials plus a broken webhook registration must not be treated as a total
        // failure, the connection is real and testable, the webhook just needs manual setup.
        var validationClient = new FakeWhatsAppGraphValidationClient(
            new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null),
            new WhatsAppWebhookRegistrationResult(false, "Meta rejected the webhook registration request."));
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var options = new InfrastructureOptions { AuthMode = "Development", ChannelProvider = "WhatsAppCloudApi", ApiPublicBaseUrl = "https://app-api-dev.example.net" };

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            repository, new FakeWhatsAppCredentialProtector(), validationClient,
            new RecordingRuntimeSettingsProvider(), new InMemoryAuditRepository(), options,
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var (status, body) = await ExecuteAsync<WhatsAppConfigurationStatusResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.True(body!.Connected);
        Assert.Equal("RegistrationFailed", body.WebhookRegistrationStatus);
        Assert.Equal("Meta rejected the webhook registration request.", body.WebhookRegistrationError);
        Assert.NotNull(await repository.GetAsync(Scope(), CancellationToken.None)); // credentials are still saved
    }

    [Fact]
    public async Task SaveConfiguration_WhenMetaRejectsCredentials_DoesNotPersistAnything()
    {
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(false, null, null, "The access token was rejected."));
        var settingsProvider = new RecordingRuntimeSettingsProvider();

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "bad-token", "app-secret-value", "waba-1", null),
            repository, new FakeWhatsAppCredentialProtector(), validationClient, settingsProvider, new InMemoryAuditRepository(), Options(),
            Context(auditReason: "Connecting our WhatsApp Business number."), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Null(await repository.GetAsync(Scope(), CancellationToken.None));
        Assert.False(settingsProvider.Refreshed);
    }

    [Fact]
    public async Task SaveConfiguration_WithoutAnAuditReason_IsRejectedBeforeCallingMeta()
    {
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme Insurer", null));

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-1", "secret-token", "app-secret-value", "waba-1", null),
            new InMemoryWhatsAppChannelConfigurationRepository(), new FakeWhatsAppCredentialProtector(), validationClient,
            new RecordingRuntimeSettingsProvider(), new InMemoryAuditRepository(), Options(),
            Context(), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Empty(validationClient.Calls);
    }

    [Fact]
    public async Task TestConfiguration_ReValidatesStoredCredentialsAndUpdatesStatus()
    {
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        await repository.SaveAsync(new WhatsAppChannelConfiguration
        {
            Scope = Scope(),
            PhoneNumberId = "phone-1",
            EncryptedAccessToken = protector.Protect("real-token"),
            EncryptedAppSecret = protector.Protect("real-secret"),
            EncryptedVerifyToken = protector.Protect("real-verify"),
            ConfiguredByUserId = "admin-1",
            ConfiguredAt = DateTimeOffset.UtcNow.AddDays(-1),
        }, CancellationToken.None);
        var validationClient = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(false, null, null, "The access token was rejected."));

        var result = await WhatsAppConfigurationEndpoints.TestConfiguration(
            repository, protector, validationClient, Options(), Context(), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("real-token", Assert.Single(validationClient.Calls).AccessToken);

        var updated = await repository.GetAsync(Scope(), CancellationToken.None);
        Assert.Equal("Failed", updated!.LastValidationStatus);
        Assert.Equal("The access token was rejected.", updated.LastValidationError);
    }

    [Fact]
    public async Task DeleteConfiguration_RemovesTheRowAndRefreshesCache()
    {
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        await repository.SaveAsync(new WhatsAppChannelConfiguration
        {
            Scope = Scope(),
            PhoneNumberId = "phone-1",
            EncryptedAccessToken = protector.Protect("real-token"),
            EncryptedAppSecret = protector.Protect("real-secret"),
            EncryptedVerifyToken = protector.Protect("real-verify"),
            ConfiguredByUserId = "admin-1",
            ConfiguredAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);
        var settingsProvider = new RecordingRuntimeSettingsProvider();
        var audit = new InMemoryAuditRepository();

        var result = await WhatsAppConfigurationEndpoints.DeleteConfiguration(
            repository, settingsProvider, audit, Options(), Context(auditReason: "Switching to a new WhatsApp number."), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Null(await repository.GetAsync(Scope(), CancellationToken.None));
        Assert.True(settingsProvider.Refreshed);

        var auditEvents = await audit.ListAsync(Scope(), CancellationToken.None);
        Assert.Contains(auditEvents, e => e.ActionName == "integration.whatsapp.configuration_removed");
    }

    private static InsuranceIntelligence.Domain.Common.TenantScope Scope() =>
        InsuranceIntelligence.Domain.Common.TenantScope.Create("tenant-demo", "workspace-claims");

    private static InfrastructureOptions Options() => new() { AuthMode = "Development", ChannelProvider = "WhatsAppCloudApi" };

    private static DefaultHttpContext Context(string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-whatsapp-config" };
        context.Request.Headers["X-Tenant-Id"] = Scope().TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope().WorkspaceId;
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }

    private sealed class RecordingRuntimeSettingsProvider : IWhatsAppRuntimeSettingsProvider
    {
        public bool Refreshed { get; private set; }
        public Action? OnRefresh { get; set; }
        public WhatsAppEffectiveSettings? Current => null;

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            Refreshed = true;
            OnRefresh?.Invoke();
            return Task.CompletedTask;
        }
    }
}

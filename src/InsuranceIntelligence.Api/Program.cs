using Microsoft.Extensions.DependencyInjection.Extensions;
using InsuranceIntelligence.Api.Security;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Assistant;
using InsuranceIntelligence.Api.Audit;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Channels;
using InsuranceIntelligence.Api.Claims;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Customer;
using InsuranceIntelligence.Api.CustomerAssistant;
using InsuranceIntelligence.Api.SalesLeads;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Search;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Communications;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Api.Contestability;
using InsuranceIntelligence.Api.Domain;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Health;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Knowledge;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Notifications;
using InsuranceIntelligence.Api.Reports;
using InsuranceIntelligence.Api.Search;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Notifications;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using InsuranceIntelligence.Infrastructure.Configuration;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInsightsTelemetry();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("InsuranceIntelligence", LogLevel.Information);

// Gate 1 (G1.6): load deployment secrets from Azure Key Vault when configured. The vault URI is a
// non-secret pointer (Azure:KeyVaultUri / Azure__KeyVaultUri); secrets are read via managed identity
// (DefaultAzureCredential), no secrets in config files or env. Only activates when a URI is set, so
// local/dev is unaffected. Key Vault secret names map to config keys ("--" for nested sections).
var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"] ?? builder.Configuration["Azure__KeyVaultUri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new Azure.Identity.DefaultAzureCredential());
}

builder.Services.AddCors();
// The portal origins are the configured ones plus the verified web address of any workspace that has its own (build-loop item 29),
// so the policy is built with the portal directory rather than from configuration alone.
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<InsuranceIntelligence.Infrastructure.ClientPortal.IPortalDirectory>((options, portalDirectory) =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var configuredOrigin = builder.Configuration["Portal:CorsOrigin"];
            var allowedOrigins = string.IsNullOrWhiteSpace(configuredOrigin)
                ? new[] { "http://localhost:3000", "http://127.0.0.1:3000" }
                : configuredOrigin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            policy.AllowAnyHeader()
                .AllowAnyMethod()
                // A browser on another origin can only read these response headers if they are exposed: the
                // name a download was given (claims pack, item 24) and how many files it holds.
                .WithExposedHeaders("Content-Disposition", "X-Pack-Files")
                .SetIsOriginAllowed(origin =>
                    allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) || portalDirectory.IsPortalOrigin(origin))
                .AllowCredentials();
        });
    });

var infrastructureOptions = InfrastructureRegistration.BindOptions(builder.Configuration);
var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(
    infrastructureOptions, builder.Environment.EnvironmentName);
AzureProviderStartupValidator.ThrowIfInvalid(infrastructureOptions);
AzureProviderStartupValidator.ThrowIfProductionInvalid(infrastructureOptions, runtimeProfile);

// Development diagnostics endpoints (debug notification trigger, email test-ingestion)
// are mapped ONLY in local Development/LocalTest. They must never exist as a deployed
// surface in Demo/Staging/Production/Reviewer, per the Gate 0 secure-baseline rule.
var developmentDiagnosticsEnabled = builder.Environment.IsDevelopment()
    && (runtimeProfile.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase));

builder.Services.AddAuthorization();
// Handles ProviderNotConfiguredException with a structured, capability-aware response instead
// of the generic 500 below; must be registered before AddProblemDetails' fallback so it runs
// first (ASP.NET Core tries handlers in registration order).
builder.Services.AddExceptionHandler<InsuranceIntelligence.Api.Local.ProviderNotConfiguredExceptionHandler>();
// A used-up plan allowance is a clear "payment required" answer, not a server error (build-loop item 32).
builder.Services.AddExceptionHandler<InsuranceIntelligence.Api.Admin.Billing.PlanLimitExceptionHandler>();
builder.Services.AddProblemDetails();

// OpenAPI/Swagger. The generated document is the machine-checkable API contract map
// (see docs/api/IIP_API_Contracts_Master.md, ADR-0004). The interactive Swagger UI and
// the JSON document are exposed ONLY in Development/LocalTest (see developmentDiagnosticsEnabled
// below) so no schema/endpoint enumeration surface is published in deployed environments.
// Backs IWhatsAppCredentialProtector (and any future at-rest secret encryption), real, reversible
// encryption keyed by a key ring, not a hand-rolled cipher. Default persistence is the local
// filesystem, which on a single-instance Azure App Service is %HOME%\ASP.NET\DataProtection-Keys,
// durable across app restarts on this deployment's current single-instance B1 plan. Scaling to
// multiple instances would need PersistKeysToAzureBlobStorage + ProtectKeysWithAzureKeyVault so
// every instance shares one key ring; noted here rather than built speculatively.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("InsuranceIntelligence");
if (!string.IsNullOrWhiteSpace(infrastructureOptions.DataProtectionBlobUri) && !string.IsNullOrWhiteSpace(infrastructureOptions.DataProtectionKeyUri))
{
    // One shared key ring in blob storage, wrapped by a Key Vault key: every instance reads the
    // same ring, a redeploy never strands stored credentials, and no key sits on a web server disk.
    var keyRingCredential = new Azure.Identity.DefaultAzureCredential();
    dataProtection
        .PersistKeysToAzureBlobStorage(new Uri(infrastructureOptions.DataProtectionBlobUri), keyRingCredential)
        .ProtectKeysWithAzureKeyVault(new Uri(infrastructureOptions.DataProtectionKeyUri), keyRingCredential);
}

if (infrastructureOptions.MultiTenant && !infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
{
    // People sign in with a token; a company's own systems present an API key. One door, two ways in.
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = "IipAuth";
            options.DefaultChallengeScheme = "IipAuth";
        })
        .AddPolicyScheme("IipAuth", "Bearer token or API key", options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(InsuranceIntelligence.Api.Auth.ApiKeyAuthenticationHandler.HeaderName)
                    ? InsuranceIntelligence.Api.Auth.ApiKeyAuthenticationHandler.Scheme
                    : infrastructureOptions.AcceptsExternalIdAlongsideEntra && InsuranceIntelligence.Api.Auth.TokenIssuer.IsExternalId(context)
                        ? InsuranceIntelligence.Api.Auth.TokenIssuer.ExternalIdScheme
                        : JwtBearerDefaults.AuthenticationScheme;
        })
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InsuranceIntelligence.Api.Auth.ApiKeyAuthenticationHandler>(
            InsuranceIntelligence.Api.Auth.ApiKeyAuthenticationHandler.Scheme, _ => { });
}

builder.Services.AddRateLimiter(limits =>
{
    limits.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limits.AddPolicy("signup", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(context, 10, TimeSpan.FromHours(1)));
    limits.AddPolicy("join", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(context, 20, TimeSpan.FromMinutes(10)));
    limits.AddPolicy("academy-learner", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(
        context, infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? 5000 : 240, TimeSpan.FromMinutes(1)));
    limits.AddPolicy("academy-post", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(
        context, infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? 5000 : 30, TimeSpan.FromHours(1)));
    limits.AddPolicy("academy-zenith", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(
        context, infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? 5000 : 20, TimeSpan.FromMinutes(1)));
    limits.AddPolicy("academy-pay", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(
        context, infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? 5000 : 20, TimeSpan.FromMinutes(10)));
    limits.AddPolicy("academy-public", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(context, 120, TimeSpan.FromMinutes(1)));
    limits.AddPolicy("marketplace", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(context, 30, TimeSpan.FromMinutes(1)));
    limits.AddPolicy("webhook", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(context, 3000, TimeSpan.FromMinutes(1)));
    // Per signed-in person in a deployment. Local development has no signed-in person (a header stands in), so
    // everything shares one bucket there and the limit is relaxed to keep tests and demos from tripping it.
    limits.AddPolicy("mfa", context => InsuranceIntelligence.Api.Common.RateLimitKeys.FixedWindow(
        context, infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? 2000 : 30, TimeSpan.FromMinutes(5)));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IIP Operations API",
        Version = "v1",
        Description = "Insurance Intelligence Platform (IIP), canonical /api/v1 contract. "
            + "Human-reviewed insurance intelligence; AI prepares, humans decide.",
    });
    // Minimal-API route params without a type constraint (e.g. {customerId}) can collide
    // on the same normalized path; pick the first deterministically so the document always
    // generates. Enrichment (operationIds, permission metadata) is layered on in the
    // hand-written contract map (docs/api/IIP_API_Contracts_Master.md).
    swagger.ResolveConflictingActions(descriptions => descriptions.First());
});
// The platform's own sign-in (build-loop item 30) sits beside Microsoft Entra: a token we issued is read by our scheme, any other
// bearer token by Entra's. A deployment with no signing key simply has no platform tokens to accept.
var localAuthOptions = InsuranceIntelligence.Api.Auth.LocalAuthOptions.From(builder.Configuration, IsLocalDeveloperRuntime(runtimeProfile));
builder.Services.AddSingleton(localAuthOptions);
var entraConfigured = infrastructureOptions.AuthMode.Equals("Entra", StringComparison.OrdinalIgnoreCase);
builder.Services
    .AddAuthentication(authentication =>
    {
        authentication.DefaultScheme = InsuranceIntelligence.Api.Auth.LocalAuthOptions.PolicySchemeName;
        authentication.DefaultAuthenticateScheme = InsuranceIntelligence.Api.Auth.LocalAuthOptions.PolicySchemeName;
        authentication.DefaultChallengeScheme = InsuranceIntelligence.Api.Auth.LocalAuthOptions.PolicySchemeName;
    })
    .AddPolicyScheme(InsuranceIntelligence.Api.Auth.LocalAuthOptions.PolicySchemeName, "Platform or Microsoft Entra sign-in", scheme =>
        scheme.ForwardDefaultSelector = context => InsuranceIntelligence.Api.Auth.LocalAuthOptions.SelectScheme(context, entraConfigured))
    .AddJwtBearer(InsuranceIntelligence.Api.Auth.LocalAuthOptions.SchemeName, jwt =>
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = localAuthOptions.ValidationParameters();
    });
builder.Services.AddScoped<InsuranceIntelligence.Application.Identity.ILocalTokenIssuer>(_ => new InsuranceIntelligence.Api.Auth.LocalTokenIssuer(localAuthOptions));
builder.Services.AddScoped<InsuranceIntelligence.Application.Identity.IIdentityMailer, InsuranceIntelligence.Api.Identity.CommunicationIdentityMailer>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Identity.LocalIdentityService>();
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // The pages people use before they have a token: a handful of tries a minute per address is plenty for a person and slow
    // for a program guessing passwords. A developer runtime is not limited, so tests and demos are not slowed.
    var permits = int.TryParse(builder.Configuration["INSURANCEINTELLIGENCE_IDENTITY_RATE_LIMIT_PER_MINUTE"], out var configured)
        ? configured
        : IsLocalDeveloperRuntime(runtimeProfile) ? 100_000 : 20;
    // Payment providers call back from a handful of addresses, sometimes in bursts; the secret in the address is the credential.
    limiter.AddPolicy("payments-callback", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = IsLocalDeveloperRuntime(runtimeProfile) ? 100_000 : 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    limiter.AddPolicy("identity", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
if (entraConfigured)
{
    // Multitenant Entra authentication for IIP's single app registration configured for
    // "Accounts in any organizational directory". Per the reconciled backend-wiring spec:
    // - EntraTenantId is IIP's publisher/home tenant, NOT a customer insurer tenant, and must
    //   NOT restrict the JWT issuer.
    // - Issuer validation is delegated to Microsoft.Identity.Web's multitenant-aware path
    //   (TenantId = "organizations"); do NOT hardcode a single ValidIssuer.
    RequireSetting(infrastructureOptions.EntraTenantId, "INSURANCEINTELLIGENCE_ENTRA_TENANT_ID");
    var clientId = RequireSetting(infrastructureOptions.EntraClientId, "INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID");
    var audience = string.IsNullOrWhiteSpace(infrastructureOptions.EntraAudience)
        ? $"api://{clientId}"
        : infrastructureOptions.EntraAudience;
    var validAudiences = new[] { audience, clientId }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    builder.Services
        .AddAuthentication()
        .AddMicrosoftIdentityWebApi(
            jwt =>
            {
                jwt.Authority = "https://login.microsoftonline.com/organizations/v2.0";
                jwt.Audience = audience;
                jwt.RequireHttpsMetadata = true;

                jwt.TokenValidationParameters ??= new TokenValidationParameters();
                jwt.TokenValidationParameters.ValidateIssuer = true;
                jwt.TokenValidationParameters.ValidateAudience = true;
                jwt.TokenValidationParameters.ValidAudiences = validAudiences;
                jwt.TokenValidationParameters.ValidateLifetime = true;
                jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
                jwt.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                {
                    if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
                        || !string.Equals(issuerUri.Host, "login.microsoftonline.com", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SecurityTokenInvalidIssuerException($"Unsupported token issuer '{issuer}'.");
                    }

                    var segments = issuerUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2
                        && Guid.TryParse(segments[0], out _)
                        && string.Equals(segments[1], "v2.0", StringComparison.OrdinalIgnoreCase))
                    {
                        return issuer;
                    }

                    throw new SecurityTokenInvalidIssuerException($"Unsupported token issuer '{issuer}'.");
                };
                jwt.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                jwt.TokenValidationParameters.RoleClaimType = "roles";
                // Do NOT set ValidIssuer/ValidIssuers: multitenant issuer validation is dynamic.
            },
            identity =>
            {
                identity.Instance = "https://login.microsoftonline.com/";
                identity.TenantId = "organizations";
                identity.ClientId = clientId;
                // Audience is validated on the JwtBearer options above; MicrosoftIdentityOptions
                // (Microsoft.Identity.Web 4.x) exposes no Audience property.
            });
}
else if (infrastructureOptions.AuthMode.Equals("EntraExternalId", StringComparison.OrdinalIgnoreCase))
{
    // SaaS sign-in: people sign up and in through an Entra External ID tenant (Google, Microsoft
    // account or email one-time code). Tokens are checked against that tenant's own metadata, so the
    // issuer is the External ID tenant's and nothing else is accepted. Who a person is comes from
    // the token; which organisation they may act in comes only from IIP's membership store.
    var authority = RequireSetting(infrastructureOptions.ExternalIdAuthority, "INSURANCEINTELLIGENCE_EXTERNAL_ID_AUTHORITY");
    var externalClientId = RequireSetting(infrastructureOptions.EntraClientId, "INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID");
    var externalAudience = string.IsNullOrWhiteSpace(infrastructureOptions.EntraAudience) ? externalClientId : infrastructureOptions.EntraAudience;
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwt =>
        {
            jwt.Authority = authority;
            jwt.Audience = externalAudience;
            jwt.RequireHttpsMetadata = true;
            jwt.TokenValidationParameters.ValidateIssuer = true;
            jwt.TokenValidationParameters.ValidateAudience = true;
            jwt.TokenValidationParameters.ValidateLifetime = true;
            jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
            jwt.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
        });
}
else
{
    builder.Services.AddAuthentication();
}

// Work accounts and External ID (Google, email code) side by side, as CIP does: the External ID tenant's
// tokens are checked against that tenant's own metadata under a second scheme, and the routing above sends
// each token to the scheme of the directory that issued it. Work-account checking is unchanged.
if (infrastructureOptions.AcceptsExternalIdAlongsideEntra)
{
    var externalIdClientId = infrastructureOptions.ExternalIdClientId!;
    var externalIdAudience = string.IsNullOrWhiteSpace(infrastructureOptions.ExternalIdAudience) ? externalIdClientId : infrastructureOptions.ExternalIdAudience;
    builder.Services
        .AddAuthentication()
        .AddJwtBearer(InsuranceIntelligence.Api.Auth.TokenIssuer.ExternalIdScheme, jwt =>
        {
            jwt.Authority = infrastructureOptions.ExternalIdAuthority;
            jwt.Audience = externalIdAudience;
            jwt.RequireHttpsMetadata = true;
            jwt.TokenValidationParameters.ValidateIssuer = true;
            jwt.TokenValidationParameters.ValidateAudience = true;
            jwt.TokenValidationParameters.ValidAudiences = new[] { externalIdAudience, externalIdClientId }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            jwt.TokenValidationParameters.ValidateLifetime = true;
            jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
            jwt.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
        });
}

// In-memory storage is permitted only for developer-machine execution, so the team can run
// the platform with no Azure account. Deny-by-default on the runtime profile: anything that
// is not explicitly a local profile, including an unrecognised value, is refused. This is
// one of three independent blocks; AddInsuranceIntelligenceInfrastructure applies the same
// profile test, and AzureProviderStartupValidator.ValidateProduction separately refuses to
// boot Staging/Production/ManagedApplicationReviewer/Demo on non-SqlServer storage.
var storageProvider = infrastructureOptions.StorageProvider;
if (storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
    && !IsLocalDeveloperRuntime(runtimeProfile))
{
    throw new InvalidOperationException(
        $"InsuranceIntelligence.Api cannot run with INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory under the "
        + $"'{runtimeProfile}' runtime profile. In-memory storage is limited to LocalTest/Development execution; "
        + "deployed profiles require Azure SQL.");
}

static bool IsLocalDeveloperRuntime(string profile) =>
    profile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase)
    || profile.Equals("Development", StringComparison.OrdinalIgnoreCase)
    || profile.Equals("Test", StringComparison.OrdinalIgnoreCase);

builder.Services.AddInsuranceIntelligenceInfrastructure(builder.Configuration, builder.Environment);

if (infrastructureOptions.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStorageProvider, AzureBlobDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, AzureBlobKnowledgeStorageProvider>();
    builder.Services.AddSingleton<IEvidencePackStorageProvider, AzureBlobEvidencePackStorageProvider>();
}
else if (infrastructureOptions.BlobProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStorageProvider, LocalMockDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, LocalMockKnowledgeStorageProvider>();
    if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsLocalDeveloperRuntime(runtimeProfile))
    {
        builder.Services.AddSingleton<IEvidencePackStorageProvider, InsuranceIntelligence.Api.Local.Mock.LocalMockEvidencePackStorageProvider>();
    }
    else
    {
        builder.Services.AddSingleton<IEvidencePackStorageProvider, NotConfiguredEvidencePackStorageProvider>();
    }
}
else
{
    builder.Services.AddSingleton<IDocumentStorageProvider, NotConfiguredDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, NotConfiguredKnowledgeStorageProvider>();
    builder.Services.AddSingleton<IEvidencePackStorageProvider, NotConfiguredEvidencePackStorageProvider>();
}

if (infrastructureOptions.DocumentProvider.Equals("AzureDocumentIntelligence", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, AzureDocumentIntelligenceProvider>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.DocumentProvider, runtimeProfile))
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, InsuranceIntelligence.Api.Local.Mock.MockDocumentProcessingProvider>();
}
else
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, NotConfiguredDocumentProcessingProvider>();
}

builder.Services.AddSingleton<IImageDerivativeProvider, MagickImageDerivativeProvider>();

// ITranslationProvider is always the tenant-aware resolver now, the same three-step order as
// ISmsProvider/IEmailSendingProvider: a tenant's own dashboard-configured connection (e.g. Sunbird
// AI) first, then the deployment's environment-configured engine below, then not configured.
// TranslationProvider/Translation options still govern step 2, the fallback, exactly as before.
// Mock is Development only (AzureProviderStartupValidator refuses it under every deployed profile);
// AfriSlm is the llama.cpp container that hosts the model file. Anything else, including an absent
// setting, fails closed to English rather than guessing. Scoped because it depends on the scoped
// connection repository, mirroring TenantAwareSmsProvider.
builder.Services.AddSingleton<InsuranceIntelligence.Application.Translation.ITranslationProviderFactory, InsuranceIntelligence.Api.Local.TranslationProviderFactory>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionValidator, InsuranceIntelligence.Infrastructure.Translation.SunbirdConnectionValidator>();
builder.Services.AddScoped<InsuranceIntelligence.Infrastructure.Translation.TenantAwareTranslationProvider>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ITranslationProvider>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.TenantAwareTranslationProvider>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ITranslationEngineHealth>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.TenantAwareTranslationProvider>());

builder.Services.AddSingleton<InsuranceIntelligence.Application.CustomerAssistant.IAssistantLanguageSettings>(sp =>

    new InsuranceIntelligence.Api.Local.ConfiguredAssistantLanguageSettings(
        sp.GetRequiredService<InsuranceIntelligence.Domain.Administration.ITenantConfigurationRepository>(), infrastructureOptions.Translation));

builder.Services.AddSingleton<InsuranceIntelligence.Application.Translation.ITranslationCache, InsuranceIntelligence.Infrastructure.Translation.InMemoryTranslationCache>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ITranslationService, InsuranceIntelligence.Application.Translation.TranslationService>();
builder.Services.AddSingleton<InsuranceIntelligence.Application.Translation.ILanguageDetector, InsuranceIntelligence.Application.Translation.WordListLanguageDetector>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ICustomerLanguageResolutionService, InsuranceIntelligence.Application.Translation.CustomerLanguageResolutionService>();

builder.Services.AddSingleton<ICompletenessScoringService, CompletenessScoringService>();
// Scoped, not Singleton: StaticDocumentChecklistProvider now optionally depends on
// IProductRepository (outcome E's product-document-matrix integration), which is itself Scoped
// (it resolves through the per-request DbContext); a Singleton cannot consume a Scoped service.
builder.Services.AddScoped<IDocumentChecklistProvider, StaticDocumentChecklistProvider>();
builder.Services.AddSingleton<ISourceCitationFormatter, SourceCitationFormatter>();
if (infrastructureOptions.RetrievalProvider.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, AzureAiSearchKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, AzureAiSearchRetrievalProvider>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.RetrievalProvider, runtimeProfile))
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, InsuranceIntelligence.Api.Local.Mock.MockKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, InsuranceIntelligence.Api.Local.Mock.MockRetrievalProvider>();
}
else
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, NotConfiguredKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, NotConfiguredRetrievalProvider>();
}

if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    // Scoped, not Singleton: both now depend on the scoped IAgentBehaviourProfileRepository
    // (blueprint §6.3) to compose the tenant's configured system prompt on every call. Embeddings
    // carry no per-call prompt, so that one stays Singleton.
    // Each model call is wrapped so it is checked against the month's AI credits first and counted after.
    builder.Services.AddScoped<AzureOpenAiAssistantProvider>();
    builder.Services.AddScoped<IAiAssistantProvider>(sp => new InsuranceIntelligence.Api.Azure.RecordedAiAssistantProvider(
        sp.GetRequiredService<AzureOpenAiAssistantProvider>(), sp.GetRequiredService<InsuranceIntelligence.Application.Billing.IAiUsageRecorder>()));
    builder.Services.AddSingleton<IEmbeddingProvider, AzureOpenAiEmbeddingProvider>();
    builder.Services.AddScoped<AzureOpenAiChatCompletionProvider>();
    builder.Services.AddScoped<IChatCompletionProvider>(sp => new InsuranceIntelligence.Api.Azure.RecordedChatCompletionProvider(
        sp.GetRequiredService<AzureOpenAiChatCompletionProvider>(), sp.GetRequiredService<InsuranceIntelligence.Application.Billing.IAiUsageRecorder>()));
    builder.Services.AddScoped<AzureOpenAiToolCallingChatProvider>();
    builder.Services.AddScoped<IToolCallingChatProvider>(sp => new InsuranceIntelligence.Api.Azure.RecordedToolCallingChatProvider(
        sp.GetRequiredService<AzureOpenAiToolCallingChatProvider>(), sp.GetRequiredService<InsuranceIntelligence.Application.Billing.IAiUsageRecorder>()));
    builder.Services.AddScoped<IConversationInsightsService, ConversationInsightsService>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.AiProvider, runtimeProfile))
{
    // Deterministic Mock adapters, developer runtimes only (MockProviderPolicy). The conversation
    // insights agent is not registered: it needs a model that returns structured JSON, which a
    // scripted mock cannot honestly provide.
    builder.Services.AddSingleton<IAiAssistantProvider, InsuranceIntelligence.Api.Local.Mock.MockAiAssistantProvider>();
    builder.Services.AddSingleton<IEmbeddingProvider, InsuranceIntelligence.Api.Local.Mock.MockEmbeddingProvider>();
    builder.Services.AddSingleton<IChatCompletionProvider, InsuranceIntelligence.Api.Local.Mock.MockChatCompletionProvider>();
    builder.Services.AddSingleton<IToolCallingChatProvider, InsuranceIntelligence.Api.Local.Mock.MockToolCallingChatProvider>();
}
else
{
    builder.Services.AddSingleton<IAiAssistantProvider, NotConfiguredAiAssistantProvider>();
    builder.Services.AddSingleton<IChatCompletionProvider, NotConfiguredChatCompletionProvider>();
}

// Every call to a model is counted against the workspace that caused it (item 31).
InsuranceIntelligence.Api.Admin.Billing.MeteringRegistration.AddAiUsageMetering(builder.Services, builder.Configuration);

if (infrastructureOptions.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, AzureServiceBusQueueDispatcher>();
}
else if (infrastructureOptions.QueueProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, MockMessageQueueDispatcher>();
}
else
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, NotConfiguredMessageQueueDispatcher>();
}

builder.Services.AddSingleton<IWorkflowRuleProvider, StaticWorkflowRuleProvider>();
if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    // Scoped, not Singleton, see the AzureOpenAiAssistantProvider registration above; same reason.
    builder.Services.AddScoped<ICustomerUpdateDraftingProvider, AzureOpenAiCustomerUpdateDraftingProvider>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.AiProvider, runtimeProfile))
{
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, InsuranceIntelligence.Api.Local.Mock.MockCustomerUpdateDraftingProvider>();
}
else
{
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, NotConfiguredCustomerUpdateDraftingProvider>();
}
if (infrastructureOptions.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ICommunicationProvider, ConfiguredCustomerUpdateCommunicationProvider>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppSenderResolver, ConfiguredWhatsAppSenderResolver>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.CommunicationProvider, runtimeProfile))
{
    // Mock sending records into an in-memory outbox (readable at /api/v1/dev/mock-outbox) and
    // replaces the Disabled sender the infrastructure registered. Nothing leaves the process.
    builder.Services.AddSingleton<InsuranceIntelligence.Api.Local.Mock.MockOutbox>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Communications.ICommunicationSendingProvider, InsuranceIntelligence.Api.Local.Mock.MockCommunicationSendingProvider>();
    builder.Services.AddScoped<ICommunicationProvider, ConfiguredCustomerUpdateCommunicationProvider>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppSenderResolver, MockWhatsAppSenderResolver>();
}
else
{
    builder.Services.AddSingleton<ICommunicationProvider, NotConfiguredCommunicationProvider>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppSenderResolver, ConfiguredWhatsAppSenderResolver>();
}
if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.EmailProvider, runtimeProfile))
{
    // A pretend IMAP host for a developer runtime: mailboxes in memory, filled through /api/v1/dev/mock-mailbox/deliver.
    builder.Services.AddSingleton<InsuranceIntelligence.Api.Local.Mock.MockMailStore>();
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IImapMailClient, InsuranceIntelligence.Api.Local.Mock.MockImapMailClient>();
}
builder.Services.AddSingleton<ISlaCalculator, BasicSlaCalculator>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Integrations.ITelegramWebhookRegistrar,
    InsuranceIntelligence.Infrastructure.Connections.TelegramWebhookRegistrar>();

if (infrastructureOptions.NotificationProvider.Equals("IncomingWebhook", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<IncomingWebhookTeamsProviderOptions>(builder.Configuration.GetSection("TeamsWebhook"));
    builder.Services.AddHttpClient<ITeamsNotificationProvider, IncomingWebhookTeamsProvider>();
}
else
{
    builder.Services.AddSingleton<ITeamsNotificationProvider, InsuranceIntelligence.Infrastructure.Graph.DisabledTeamsNotificationProvider>();
}

builder.Services.AddScoped<ITeamsNotificationQueue, DefaultTeamsNotificationQueue>();
builder.Services.AddScoped<CaseIntakeService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Knowledge.IKnowledgeIngestionSink,
    InsuranceIntelligence.Application.Knowledge.KnowledgeIngestionService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Claims.IClaimGovernedPathService, InsuranceIntelligence.Application.Claims.ClaimGovernedPathService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContextAccessor>();
builder.Services.AddScoped<ITenantContextAccessor>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
builder.Services.AddScoped<ITenantContextSetter>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IClaimsQueryService, ClaimsQueryService>();
builder.Services.AddSingleton<IAiOutputValidator, DeterministicAiOutputValidator>();
builder.Services.AddSingleton<IIntentClassifier, DeterministicThenAiIntentClassifier>();
builder.Services.AddSingleton<IProviderRequirementResolver, ProviderRequirementResolver>();
builder.Services.AddScoped<INotificationDraftService, CustomerUpdateNotificationDraftService>();
builder.Services.AddScoped<ICommandCenterAnalyst, CommandCenterAnalyst>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IAiCreditsSummaryService, InsuranceIntelligence.Application.Billing.AiCreditsSummaryService>();
builder.Services.AddScoped<ICommandCenterQueryService, CommandCenterQueryService>();
builder.Services.AddScoped<ICommandCenterConversationService, CommandCenterConversationService>();
builder.Services.AddScoped<IConversationGroundingService, ConversationGroundingService>();
// W2.5: Phase 2 core-workspace query services.
builder.Services.AddScoped<IChannelQueryService, ChannelQueryService>();
builder.Services.AddScoped<IChannelThreadQueryService, ChannelThreadQueryService>();
builder.Services.AddScoped<IChannelMessageIngestionService, ChannelMessageIngestionService>();
builder.Services.AddScoped<IChannelMessageSendService, ChannelMessageSendService>();
builder.Services.AddScoped<ICustomerFollowUpManagementService, CustomerFollowUpManagementService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Channels.IChannelIdentityService, ChannelIdentityService>();
builder.Services.AddScoped<ICustomerQueryService, CustomerQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Policies.IPolicyImportService, InsuranceIntelligence.Application.Policies.PolicyImportService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Customers.ICustomerConsentService, InsuranceIntelligence.Application.Customers.CustomerConsentService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Retention.IRetentionWatchService, InsuranceIntelligence.Application.Retention.RetentionWatchService>();
builder.Services.AddScoped<ISalesLeadQueryService, SalesLeadQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IConversationControlService, InsuranceIntelligence.Application.CustomerAssistant.ConversationControlService>();
builder.Services.AddScoped<ISalesLeadCommandService, SalesLeadCommandService>();
builder.Services.AddScoped<IProspectListImportService, ProspectListImportService>();
builder.Services.AddScoped<IProspectTriageService, ProspectTriageService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Workspace.IWorkspaceService, InsuranceIntelligence.Application.Workspace.WorkspaceService>();
builder.Services.AddScoped<IProspectListImportService, ProspectListImportService>();
builder.Services.AddSingleton(InsuranceIntelligence.Infrastructure.Connections.FileProviderOptions.From(builder.Configuration));
builder.Services.AddHttpClient(InsuranceIntelligence.Infrastructure.Connections.UserFileService.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<InsuranceIntelligence.Application.Integrations.IUserFileService, InsuranceIntelligence.Infrastructure.Connections.UserFileService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Integrations.IFileAccessTokenProvider>(provider => provider.GetRequiredService<InsuranceIntelligence.Application.Integrations.IUserFileService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Academy.IAcademyPricingService, InsuranceIntelligence.Application.Academy.AcademyPricingService>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Academy.IAcademyContentProvider, InsuranceIntelligence.Api.Academy.AcademyContentProvider>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Academy.AcademyOperator>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.AcademyLearnerService>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.AcademyCommunityService>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.IZenithModel, InsuranceIntelligence.Api.Academy.ChatZenithModel>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.AcademyZenithService>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.AcademySettingsService>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.Payments.IAcademyPriceSource, InsuranceIntelligence.Api.Academy.Payments.AcademyPriceSource>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentService>();
builder.Services.AddHttpClient<InsuranceIntelligence.Api.Academy.Payments.FlutterwaveGateway>();
// Which payment provider is used: the local stand-in only when asked for in development, Flutterwave when its key is set,
// otherwise none (enrolment stays open and nothing is charged, and the pages say so).
builder.Services.AddScoped<InsuranceIntelligence.Api.Academy.Payments.IPaymentGateway>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    if (string.Equals(configuration["Academy:Payments:Provider"], "Fake", StringComparison.OrdinalIgnoreCase)
        && infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
    {
        return new InsuranceIntelligence.Api.Academy.Payments.FakePaymentGateway(configuration);
    }

    var flutterwave = sp.GetRequiredService<InsuranceIntelligence.Api.Academy.Payments.FlutterwaveGateway>();
    return flutterwave.Configured ? flutterwave : new InsuranceIntelligence.Api.Academy.Payments.NotConfiguredPaymentGateway();
});
builder.Services.AddHostedService<InsuranceIntelligence.Api.Academy.Payments.PaymentReconcileWorker>();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new InsuranceIntelligence.Application.Security.PlatformMfaOptions());
builder.Services.AddScoped<InsuranceIntelligence.Application.Security.IPlatformMfaService, InsuranceIntelligence.Application.Security.PlatformMfaService>();
// Passkeys work only on the portal addresses listed here (Security:WebAuthnOrigins, comma separated).
builder.Services.AddSingleton(new InsuranceIntelligence.Application.Security.PasskeyOptions(
    (builder.Configuration["Security:WebAuthnOrigins"]
        ?? "https://app.insuranceintelligenceplatform.com,https://app-portal-dev-h5girbnzrayqe.azurewebsites.net,http://localhost:3000,http://127.0.0.1:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
builder.Services.AddScoped<InsuranceIntelligence.Application.Security.IPasskeyService, InsuranceIntelligence.Infrastructure.Security.PasskeyService>();
// A message that cannot reach a real phone may be recorded as a clearly labelled demonstration send, but
// only where the runtime profile is not protected. In Staging and Production it is blocked, never faked.
builder.Services.AddSingleton(new OutreachOptions(!AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile)));
builder.Services.AddScoped<IOutreachService, OutreachService>();
builder.Services.AddScoped<IAcquisitionReportService, AcquisitionReportService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Brief.IBriefService, InsuranceIntelligence.Application.Brief.BriefService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IProductQueryService, InsuranceIntelligence.Application.Products.ProductQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IProductCommandService, InsuranceIntelligence.Application.Products.ProductCommandService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IProductWordingExtractionService, InsuranceIntelligence.Application.Products.ProductWordingExtractionService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IClaimTypeService, InsuranceIntelligence.Application.Products.ClaimTypeService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IWordingDraftService, InsuranceIntelligence.Application.Products.WordingDraftService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimantConsentService, InsuranceIntelligence.Application.Compliance.ClaimantConsentService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IImageDerivativeProvider, InsuranceIntelligence.Api.Documents.MagickImageDerivativeProvider>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IDocumentPdfBuilder, InsuranceIntelligence.Api.Documents.MagickDocumentPdfBuilder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.ISlotPdfService, InsuranceIntelligence.Application.Documents.SlotPdfService>();
// Recognising documents (build-loop item 14): a vision model against the workspace's whole document
// catalogue. Local runs use the deterministic Mock; where no model is configured every file honestly
// goes to a person instead of being guessed at.
if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Documents.IDocumentRecognizer, InsuranceIntelligence.Api.Azure.AzureOpenAiVisionDocumentRecognizer>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.AiProvider, runtimeProfile))
{
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IDocumentRecognizer, InsuranceIntelligence.Api.Local.Mock.MockDocumentRecognizer>();
}
else
{
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IDocumentRecognizer, InsuranceIntelligence.Api.Azure.NotConfiguredDocumentRecognizer>();
}

builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentRecognitionService, InsuranceIntelligence.Application.Documents.DocumentRecognitionService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IFieldProposalService, InsuranceIntelligence.Application.Documents.FieldProposalService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IImageForensicsReader, InsuranceIntelligence.Api.Documents.MagickImageForensicsReader>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IAuthenticityService, InsuranceIntelligence.Application.Documents.AuthenticityService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IFraudAgentService, InsuranceIntelligence.Application.Fraud.FraudAgentService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IClaimAssessmentService, InsuranceIntelligence.Application.Fraud.ClaimAssessmentService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IClaimantStatusPresenter, InsuranceIntelligence.Application.Cases.ClaimantStatusPresenter>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IClaimStatusChangeService, InsuranceIntelligence.Application.Cases.ClaimStatusChangeService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IClaimStatusNotifier, InsuranceIntelligence.Application.Cases.ClaimStatusNotifier>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimsPackBuilder, InsuranceIntelligence.Application.Compliance.ClaimsPackBuilder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Customers.ICustomerMemoryService, InsuranceIntelligence.Application.Customers.CustomerMemoryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimsPackEmailService, InsuranceIntelligence.Application.Compliance.ClaimsPackEmailService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimsPackTrigger, InsuranceIntelligence.Application.Compliance.ClaimsPackTrigger>();
builder.Services.AddSingleton<InsuranceIntelligence.Application.ClientPortal.IPortalAddress, InsuranceIntelligence.Api.ClientPortal.PortalAddress>();
builder.Services.AddSingleton<InsuranceIntelligence.Application.Compliance.IPortalBaseUrl, InsuranceIntelligence.Api.Compliance.ConfiguredPortalBaseUrl>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Compliance.ClaimsPackJobHandler>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IClaimFieldService, InsuranceIntelligence.Application.Products.ClaimFieldService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Tenants.IWorkspaceCatalogService, InsuranceIntelligence.Application.Tenants.WorkspaceCatalogService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Onboarding.IOnboardingConfiguration, InsuranceIntelligence.Api.Onboarding.OnboardingConfiguration>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Onboarding.ITenantOnboardingService, InsuranceIntelligence.Application.Onboarding.TenantOnboardingService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddSingleton<IComplianceResidencySettings>(_ => new ComplianceResidencySettings(
    infrastructureOptions.DataResidencyCountry,
    infrastructureOptions.DataResidencyGeography,
    infrastructureOptions.AzureRegion,
    infrastructureOptions.DataResidencyMode,
    infrastructureOptions.AzureOpenAI.DeploymentType,
    infrastructureOptions.DataResidencyAllowedAzureRegions,
    infrastructureOptions.DataResidencyApprovalReference,
    infrastructureOptions.AiProvider));
builder.Services.AddScoped<IComplianceQueryService, ComplianceQueryService>();
// Initiative LP: rule-based advisory domains + admin domains (Fraud/Underwriting/Agents/Billing).
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IFraudQueryService, InsuranceIntelligence.Application.Fraud.FraudQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IFraudDispositionService, InsuranceIntelligence.Application.Fraud.FraudDispositionService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Underwriting.IUnderwritingQueryService, InsuranceIntelligence.Application.Underwriting.UnderwritingQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Underwriting.IUnderwritingSubmissionSource, InsuranceIntelligence.Api.Underwriting.NotConfiguredUnderwritingSubmissionSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentRegistryService, InsuranceIntelligence.Application.Agents.AgentRegistryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentGate, InsuranceIntelligence.Application.Agents.AgentGate>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentRunRecorder, InsuranceIntelligence.Application.Agents.AgentRunRecorder>();

// WO-08: read-tier agent tool registry. Each tool is registered as IAgentTool so
// AgentToolRegistry (itself the sole IAgentToolRegistry) can resolve the full set by name.
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.ClaimSnapshotLoader>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ClaimsAttentionReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ClaimsSlaReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.DocumentsGapsReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ApprovalsQueueReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.KnowledgeSearchTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.AuditReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ProviderHealthReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.MetricsReadTool>();
// WO-10: single-entity read tools added for the claims-summary agent, its first real consumer.
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ClaimReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.DocumentReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.PolicyReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentToolRegistry, InsuranceIntelligence.Application.Agents.Tools.AgentToolRegistry>();
builder.Services.AddScoped<InsuranceIntelligence.Application.ClaimsSummary.IClaimsSummaryService, InsuranceIntelligence.Application.ClaimsSummary.ClaimsSummaryService>();
// Customer 360 briefing, chat instructions, and the automatic review every new claim receives.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<InsuranceIntelligence.Application.Knowledge.IKnowledgeIngestionSink, InsuranceIntelligence.Application.Knowledge.KnowledgeIngestionService>();
builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IConnectorFileSource, InsuranceIntelligence.Infrastructure.Connectors.MicrosoftGraphFileSource>();
builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IConnectorFileSource, InsuranceIntelligence.Infrastructure.Connectors.GoogleDriveFileSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Connectors.IConnectorSyncService, InsuranceIntelligence.Application.Connectors.ConnectorSyncService>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Connectors.ConnectorSyncWorker>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Rules.RuleRunWorker>();
// JSON lists are large and the people reading them are far from the data centre; compressing them costs a little CPU
// and saves far more waiting. Brotli at its fastest level is the cheap setting on a small plan.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerSummary.ICustomerSummaryService, InsuranceIntelligence.Application.CustomerSummary.CustomerSummaryService>();
builder.Services.AddScoped<ICommandCenterActionService, CommandCenterActionService>();
// Durable background work (build-loop item 17): document processing and claim review are jobs in the
// database, run by a worker, retried with a back-off and dead-lettered where a person can see them.
builder.Services.AddSingleton(InsuranceIntelligence.Api.Jobs.BackgroundPipelineOptions.From(builder.Configuration));
builder.Services.AddScoped<InsuranceIntelligence.Api.Agents.IClaimTriageQueue, InsuranceIntelligence.Api.Agents.DurableClaimTriageQueue>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Documents.DocumentProcessor>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Documents.IDocumentPipeline, InsuranceIntelligence.Api.Documents.DocumentPipeline>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Documents.DocumentProcessJobHandler>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Agents.ClaimReviewJobHandler>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobProcessor, InsuranceIntelligence.Api.Jobs.BackgroundJobProcessor>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Jobs.BackgroundJobWorker>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Admin.Billing.BillingWorker>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Agents.ConversationInsightsQueue>();
builder.Services.AddSingleton<IConversationInsightsQueue>(sp => sp.GetRequiredService<InsuranceIntelligence.Api.Agents.ConversationInsightsQueue>());
builder.Services.AddHostedService<InsuranceIntelligence.Api.Agents.ConversationInsightsWorker>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Webhooks.OutboundWebhookWorker>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Integrations.WhatsAppAssistantJobHandler>();
// WO-14: one instance backs both the emitter (called from the 5 real event sites) and the
// recipient's own feed (list/read-state), same underlying repository either way.
builder.Services.AddScoped<InsuranceIntelligence.Application.Notifications.NotificationService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Notifications.INotificationEmitter>(
    sp => sp.GetRequiredService<InsuranceIntelligence.Application.Notifications.NotificationService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Notifications.INotificationFeedService>(
    sp => sp.GetRequiredService<InsuranceIntelligence.Application.Notifications.NotificationService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowService, InsuranceIntelligence.Application.AgentWorkflows.AgentWorkflowService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.MyWork.IMyWorkService, InsuranceIntelligence.Application.MyWork.MyWorkService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.WhatsApp.IWhatsAppMessagingService, InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.ICustomerAssistantService, InsuranceIntelligence.Application.CustomerAssistant.CustomerAssistantService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Workspaces.IWorkspaceRecordStore, InsuranceIntelligence.Infrastructure.Persistence.EfWorkspaceRecordStore>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Workspaces.WorkspaceBrandService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Workspaces.IWorkspaceBrandResolver>(services =>
    services.GetRequiredService<InsuranceIntelligence.Application.Workspaces.WorkspaceBrandService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Organisations.CompanyProfileService>();
builder.Services.AddScoped<InsuranceIntelligence.Domain.Organisations.IOrganisationRepository, InsuranceIntelligence.Infrastructure.Persistence.EfOrganisationRepository>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Organisations.OrganisationService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.DemoScenarios.DemoScenarioPolicyholderSeeder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Rules.RuleService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Templates.TemplateCatalogService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.DemoScenarios.DemoScenarioService>();

// Whose assistant Kato is, and which covers he sells, per workspace (build-loop item 27): the organisation from the
// workspace's operating settings and the covers from its published products that carry an offer. Only a developer or
// demonstration runtime may fall back to the built-in demonstration catalogue when a workspace has configured nothing;
// a staging or production workspace with no offers sells nothing rather than another insurer's covers.
var builtInAssistantCatalogue = runtimeProfile.Equals("Development", StringComparison.OrdinalIgnoreCase)
    || runtimeProfile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase)
    || runtimeProfile.Equals("Demo", StringComparison.OrdinalIgnoreCase);
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IAssistantBrandProvider>(services =>
    new InsuranceIntelligence.Application.CustomerAssistant.TenantAssistantBrandProvider(
        services.GetRequiredService<InsuranceIntelligence.Domain.Administration.ITenantSettingsRepository>(),
        services.GetRequiredService<InsuranceIntelligence.Domain.Products.IProductRepository>(),
        builtInAssistantCatalogue,
        services.GetRequiredService<InsuranceIntelligence.Application.Workspaces.WorkspaceBrandService>()));

// Hidden field-test commands (#reset, #demo) for one shared phone: Demo runtime profile only, and
// only for the number in INSURANCEINTELLIGENCE_DEMO_PHONE_NUMBER. Anywhere else they do not exist.
var demoRuntime = InsuranceIntelligence.Api.Azure.AzureProviderStartupValidator
    .ResolveRuntimeProfile(infrastructureOptions, builder.Environment.EnvironmentName)
    .Equals("Demo", StringComparison.OrdinalIgnoreCase);
var demoPhoneNumber = builder.Configuration["INSURANCEINTELLIGENCE_DEMO_PHONE_NUMBER"];
// The SMS possession check before a WhatsApp customer can discuss a claim stays on everywhere except a deliberate,
// Demo-profile-only setting for presenting, so a presenter is not stopped by a code sent to a phone that is not theirs.
// Outside the Demo runtime profile the setting is ignored; set it back to false (or remove it) to restore the check.
var skipClaimIdentityForDemo = demoRuntime
    && string.Equals(builder.Configuration["INSURANCEINTELLIGENCE_DEMO_SKIP_CLAIM_IDENTITY"], "true", StringComparison.OrdinalIgnoreCase);
if (skipClaimIdentityForDemo)
{
    builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IClaimIdentityVerifier, InsuranceIntelligence.Application.CustomerAssistant.DemoSkippedClaimIdentityVerifier>();
}
else
{
    builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IClaimIdentityVerifier, InsuranceIntelligence.Application.CustomerAssistant.ClaimIdentityVerifier>();
}
builder.Services.AddSingleton(demoRuntime && !string.IsNullOrWhiteSpace(demoPhoneNumber)
    ? new InsuranceIntelligence.Application.CustomerAssistant.DemoSessionSettings(true, demoPhoneNumber.Trim())
    : InsuranceIntelligence.Application.CustomerAssistant.DemoSessionSettings.Disabled);
builder.Services.AddSingleton(new InsuranceIntelligence.Application.DemoSeeding.DemoPolicyholderSettings(
    builder.Configuration["INSURANCEINTELLIGENCE_DEMO_POLICYHOLDER_NAME"] ?? string.Empty));
builder.Services.AddScoped<InsuranceIntelligence.Application.DemoSeeding.MicroinsuranceDemoSeeder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.DemoScenarios.DemoScenarioPolicyholderSeeder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.DemoScenarios.DemoScenarioService>();
builder.Services.AddScoped<InsuranceIntelligence.Infrastructure.Translation.SunbirdVoiceNoteTranscriber>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IVoiceNoteTranscriber>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.SunbirdVoiceNoteTranscriber>());
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IVoiceNoteSpeaker>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.SunbirdVoiceNoteTranscriber>());
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IDemoPolicyholderLoader>(
    services => services.GetRequiredService<InsuranceIntelligence.Application.DemoSeeding.MicroinsuranceDemoSeeder>());
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.DemoSessionCommands>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IClaimantAccessLinkIssuer, InsuranceIntelligence.Api.ClientPortal.WhatsAppClaimantAccessLinkIssuer>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IFnolAcknowledgementService, InsuranceIntelligence.Application.Cases.FnolAcknowledgementService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IMissingDocumentRequestService, InsuranceIntelligence.Application.Cases.MissingDocumentRequestService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.ICaseFollowThroughService, InsuranceIntelligence.Application.Cases.CaseFollowThroughService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingQueryService, InsuranceIntelligence.Application.Billing.BillingQueryService>();
// Plans, allowances and payments (build-loop item 32).
builder.Services.AddSingleton(InsuranceIntelligence.Domain.Billing.PlanCatalogue.Parse(builder.Configuration["INSURANCEINTELLIGENCE_PLAN_CATALOGUE"]));
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.PlanService>();
builder.Services.AddScoped<InsuranceIntelligence.Domain.Billing.IPlanAllowance>(sp => sp.GetRequiredService<InsuranceIntelligence.Application.Billing.PlanService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IPaymentLinks, InsuranceIntelligence.Api.Admin.Billing.PaymentLinks>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.PaymentService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IPaymentOutcomeHandler, InsuranceIntelligence.Application.Billing.PlanPaymentOutcomeHandler>();
// Retention, legal holds and erasure (build-loop item 34).
builder.Services.AddScoped<InsuranceIntelligence.Application.DataLifecycle.DataLifecycleService>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.DataLifecycle.RetentionWorker>();
builder.Services.AddScoped<InsuranceIntelligence.Infrastructure.Persistence.LookupKeyBackfill>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Admin.LookupBackfillWorker>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.SubscriptionLifecycleService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.PaymentReconciliationService>();
var paymentProviderSetting = builder.Configuration["INSURANCEINTELLIGENCE_PAYMENT_PROVIDER"] ?? "Disabled";
if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(paymentProviderSetting, runtimeProfile))
{
    builder.Services.AddSingleton<InsuranceIntelligence.Api.Local.Mock.MockPaymentLedger>();
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Billing.IPaymentProvider, InsuranceIntelligence.Api.Local.Mock.MockPaymentProvider>();
}
else if (paymentProviderSetting.Equals("Live", StringComparison.OrdinalIgnoreCase))
{
    // Each live adapter reports itself not configured until its own credentials are present, and is then not offered.
    builder.Services.AddHttpClient("payments");
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Billing.IPaymentProvider>(sp => new InsuranceIntelligence.Infrastructure.Payments.MtnMomoPaymentProvider(
        InsuranceIntelligence.Infrastructure.Payments.MtnMomoOptions.From(builder.Configuration), sp.GetRequiredService<IHttpClientFactory>().CreateClient("payments")));
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Billing.IPaymentProvider>(sp => new InsuranceIntelligence.Infrastructure.Payments.AirtelMoneyPaymentProvider(
        InsuranceIntelligence.Infrastructure.Payments.AirtelMoneyOptions.From(builder.Configuration), sp.GetRequiredService<IHttpClientFactory>().CreateClient("payments")));
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Billing.IPaymentProvider>(sp => new InsuranceIntelligence.Infrastructure.Payments.FlutterwaveCardPaymentProvider(
        InsuranceIntelligence.Infrastructure.Payments.CardGatewayOptions.From(builder.Configuration), sp.GetRequiredService<IHttpClientFactory>().CreateClient("payments")));
}

builder.Services.AddSingleton<InsuranceIntelligence.Domain.Billing.IPaymentProviderSelector, InsuranceIntelligence.Infrastructure.Payments.PaymentProviderSelector>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingUsageSource, InsuranceIntelligence.Api.Admin.Billing.RecordBackedBillingUsageSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentQueryService, InsuranceIntelligence.Application.Documents.DocumentQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentQualityService, InsuranceIntelligence.Application.Documents.DocumentQualityService>();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();
builder.Services.AddScoped<InsuranceIntelligence.Api.CommandCenter.ISystemCapabilityStatusService, InsuranceIntelligence.Api.CommandCenter.SystemCapabilityStatusService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Workflow.ICaseTimelineProvider, InsuranceIntelligence.Api.Local.CaseTimelineProvider>();

builder.Configuration["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_MAX_BYTES"] ??= DocumentEndpoints.MaxLocalMockFileSizeBytes.ToString();
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_KNOWLEDGE_MAX_BYTES"] ??= KnowledgeEndpoints.MaxKnowledgeFileSizeBytes.ToString();

// Whose assistant Kato is: Nile Shield Assurance unless configured for a bank selling its partner
// insurers' covers (Assistant:Organisation, Assistant:ShortName, Assistant:PartnerInsurers).
InsuranceIntelligence.Application.CustomerAssistant.KatoBrand.Current = InsuranceIntelligence.Application.CustomerAssistant.KatoBrand.FromSettings(
    builder.Configuration["Assistant:Organisation"],
    builder.Configuration["Assistant:ShortName"],
    builder.Configuration["Assistant:PartnerInsurers"]);

var app = builder.Build();

await app.Services.InitializeInsuranceIntelligenceInfrastructureAsync(app.Environment);

// Offline developer mode: with in-memory storage the database starts empty on every run, so
var multiTenantProblems = InsuranceIntelligence.Infrastructure.Configuration.MultiTenantStartupGuard.Problems(infrastructureOptions);
if (multiTenantProblems.Count > 0)
{
    throw new InvalidOperationException("Multi-tenant configuration is unsafe: " + string.Join(" ", multiTenantProblems));
}

// seed the demo dataset at boot to give the app something to show without any Azure account.
// Guarded to in-memory storage only, this can never run against SqlServer, and the storage
// provider itself is already restricted to local runtimes.
if (infrastructureOptions.StorageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
    && infrastructureOptions.SeedDemoData
    && !infrastructureOptions.MultiTenant)
{
    using var seedScope = app.Services.CreateScope();
    var seedService = seedScope.ServiceProvider
        .GetRequiredService<InsuranceIntelligence.Application.DemoSeeding.DemoSeedService>();
    var seedScopeIds = InsuranceIntelligence.Domain.Common.TenantScope.Create(
        string.IsNullOrWhiteSpace(infrastructureOptions.TenantId) ? "tenant-demo" : infrastructureOptions.TenantId,
        string.IsNullOrWhiteSpace(infrastructureOptions.WorkspaceId) ? "workspace-operations" : infrastructureOptions.WorkspaceId);
    // Provenance stays with the system seeder; assignment goes to the identity an offline operator
    // actually signs in as (the same default DemoAuthorization.GetUserId falls back to), so "My Work"
    // and the notification feed, both filtered by the current user, are populated on a fresh run
    // instead of showing empty states over a workspace that visibly has 46 claims in it.
    var seedResult = await seedService.SeedAsync(seedScopeIds, "system:local-seed", DemoAuthorization.DefaultLocalUserId);
    app.Logger.LogInformation(
        "Offline mode: seeded {Cases} demo claims, {Documents} documents, {Approvals} approvals into in-memory storage, assigned to {Assignee}.",
        seedResult.Cases, seedResult.Documents, seedResult.Approvals, DemoAuthorization.DefaultLocalUserId);
}

// Always return a generic RFC 7807 response. Exception details and request headers stay server-side.
app.UseExceptionHandler();

app.UseResponseCompression();

app.UseCors();

// When the month's AI credits are used up, an endpoint that needed the model says so plainly (402) instead of failing.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (InsuranceIntelligence.Application.Billing.AiCreditsExhaustedException) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new { code = "credits_exhausted", message = "AI credits for this month are used up. Ask your administrator to add credits." });
    }
});

// Swagger UI + JSON contract document, Development/LocalTest only (secure baseline).
if (developmentDiagnosticsEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "IIP Operations API v1"));
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseRateLimiter();

// Baseline security response headers. The API only returns JSON, so a strict
// content-security-policy and anti-framing headers are safe and reduce
// clickjacking / content-sniffing exposure without affecting the separately
// hosted portal.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

app.Use(async (context, next) =>
{
    var rejection =
        RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, infrastructureOptions)
        ?? RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, infrastructureOptions);
    if (rejection is not null)
    {
        await rejection.ExecuteAsync(context);
        return;
    }

    await next();
});

// Populate the per-request tenant context for query-service endpoints (Channels,
// Customers, Audit, Fraud, Underwriting, Agents, Billing, Documents). These read
// ITenantContextAccessor.Current via their permission/query services; only
// ClaimsController set it explicitly before, so the standalone query endpoints
// otherwise fail with "Tenant context has not been initialized".
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1"))
    {
        if (infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && context.User.Identity?.IsAuthenticated != true)
        {
            // Development ONLY: demo/header scope + role (rejected in deployed profiles by RuntimeHeaderGuard).
            if (TenantScopeReader.TryGetFromHeadersOrQuery(context, out var tenantScope, out _))
            {
                var devUserId = DemoAuthorization.GetUserId(context, fallback: string.Empty);
                if (!string.IsNullOrWhiteSpace(devUserId))
                {
                    var roleCode = DemoAuthorization.GetEffectiveRole(context, infrastructureOptions).ToString();
                    context.RequestServices.GetRequiredService<ITenantContextSetter>()
                        .SetCurrent(new TenantContext(tenantScope.TenantId, tenantScope.WorkspaceId, devUserId, [roleCode]));
                }
            }
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            // Deployed (Entra), ADR-0005: runtime authority is IIP's membership store, keyed by the
            // authenticated user's stable id (oid). The token tid/workspace claims are ONLY
            // disambiguation hints, never authority. Fine-grained permissions come from the store and
            // are stashed for the synchronous permission checks. No membership => fail closed.
            var userId = DemoAuthorization.GetUserId(context, fallback: string.Empty);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var requestedOrganisation = context.Request.Headers.TryGetValue(
                    RuntimeClaimTypes.RequestedOrganisationHeaderName, out var requestedOrganisationValue)
                    ? requestedOrganisationValue.ToString()
                    : null;
                // A key acts only in the organisation it belongs to, whatever the header says.
                if (context.Items[InsuranceIntelligence.Api.Auth.ApiKeyAuthenticationHandler.OrganisationItemKey] is string keyOrganisation)
                {
                    requestedOrganisation = keyOrganisation;
                }

                var tenantHint = !string.IsNullOrWhiteSpace(requestedOrganisation)
                    ? requestedOrganisation.Trim()
                    : infrastructureOptions.MultiTenant
                        ? null // the token's tenant claim is a sign-in directory, never an insurer
                        : RuntimeClaimTypes.TenantScope
                            .Select(t => context.User.FindFirst(t)?.Value)
                            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                // The portal's workspace switcher (WO-13) sends the user's chosen workspace as a
                // request header, not a claim, a new token isn't minted per switch. It is only ever
                // a hint: GetEffectiveMembershipAsync below re-validates it against the user's real
                // active membership and fails closed if they don't belong to it, so this can never
                // grant access to a workspace the user isn't actually a member of (I5).
                var requestedWorkspace = context.Request.Headers.TryGetValue(
                    RuntimeClaimTypes.RequestedWorkspaceHeaderName, out var requestedWorkspaceValue)
                    ? requestedWorkspaceValue.ToString()
                    : null;
                var workspaceHint = !string.IsNullOrWhiteSpace(requestedWorkspace)
                    ? requestedWorkspace
                    : RuntimeClaimTypes.WorkspaceScope
                        .Select(t => context.User.FindFirst(t)?.Value)
                        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

                var membership = await context.RequestServices
                    .GetRequiredService<IPermissionReadRepository>()
                    .GetEffectiveMembershipAsync(userId, tenantHint, workspaceHint, context.RequestAborted);

                if (membership is null && context.Request.Path.StartsWithSegments("/api/v1/me", StringComparison.OrdinalIgnoreCase)
                    && !context.Request.Path.StartsWithSegments("/api/v1/me/organisations", StringComparison.OrdinalIgnoreCase))
                {
                    // The identity lookup is the one that locks a person out, and the refusal said nothing about why.
                    // Record what was asked, never a token: the account id and the two hints.
                    context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MembershipLookup").LogWarning(
                        "No single active membership for user {UserId} (tenant hint {TenantHint}, workspace hint {WorkspaceHint}, organisation header {OrganisationHeader}).",
                        userId, tenantHint ?? "(none)", workspaceHint ?? "(none)", requestedOrganisation ?? "(none)");
                }

                if (membership is not null)
                {
                    context.Items[DemoAuthorization.EffectivePermissionsItemKey] = membership.Permissions;
                    context.RequestServices.GetRequiredService<ITenantContextSetter>()
                        .SetCurrent(new TenantContext(membership.TenantId, membership.WorkspaceId, membership.UserId, membership.Roles));
                }
            }
        }
    }

    await next();
});

// The second step after sign-in. A no-op unless the organisation turned it on (Settings, Security).
app.UseMiddleware<InsuranceIntelligence.Api.Security.PlatformMfaEnforcementMiddleware>();

app.MapGet("/health", (InfrastructureOptions options) => Results.Ok(new
{
    service = "InsuranceIntelligence.Api",
    environment = AzureProviderStartupValidator.ResolveRuntimeProfile(options, app.Environment.EnvironmentName),
    hostEnvironment = app.Environment.EnvironmentName,
    authMode = options.AuthMode,
    status = "healthy"
}));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", HealthEndpoints.Ready);

app.MapGet("/api/v1/me", RuntimeContextEndpoints.GetMe);

// Self-serve sign-up, the platform's own sign-in, password reset and staff invitations (build-loop item 30). The public ones are
// rate limited per address; the administrator's invitation list needs the settings permission.
app.MapGet("/api/v1/auth/capabilities", InsuranceIntelligence.Api.Identity.IdentityEndpoints.Capabilities);
app.MapPost("/api/v1/signup", InsuranceIntelligence.Api.Identity.IdentityEndpoints.SignUp).RequireRateLimiting("identity");
app.MapPost("/api/v1/signup/verify", InsuranceIntelligence.Api.Identity.IdentityEndpoints.VerifySignUp).RequireRateLimiting("identity");
app.MapPost("/api/v1/auth/sign-in", InsuranceIntelligence.Api.Identity.IdentityEndpoints.SignIn).RequireRateLimiting("identity");
app.MapPost("/api/v1/auth/password/forgot", InsuranceIntelligence.Api.Identity.IdentityEndpoints.ForgotPassword).RequireRateLimiting("identity");
app.MapPost("/api/v1/auth/password/reset", InsuranceIntelligence.Api.Identity.IdentityEndpoints.ResetPassword).RequireRateLimiting("identity");
app.MapPost("/api/v1/auth/password/change", InsuranceIntelligence.Api.Identity.IdentityEndpoints.ChangePassword).RequireRateLimiting("identity");
app.MapGet("/api/v1/invitations/preview", InsuranceIntelligence.Api.Identity.IdentityEndpoints.PreviewInvitation).RequireRateLimiting("identity");
app.MapPost("/api/v1/invitations/accept", InsuranceIntelligence.Api.Identity.IdentityEndpoints.AcceptInvitation).RequireRateLimiting("identity");
app.MapGet("/api/v1/admin/invitations", InsuranceIntelligence.Api.Identity.IdentityEndpoints.ListInvitations)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/invitations", InsuranceIntelligence.Api.Identity.IdentityEndpoints.Invite)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapDelete("/api/v1/admin/invitations/{invitationId:guid}", InsuranceIntelligence.Api.Identity.IdentityEndpoints.Revoke)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// SaaS: sign-up, invitations, broker/insurer links. See docs/01-strategy-requirements-roadmap/IIP_SAAS_BUILD_PLAN.md.
app.MapPost("/api/v1/organisations", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Create).RequireRateLimiting("signup");
app.MapPost("/api/v1/admin/organisations/adopt-current-tenant", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.AdoptCurrentTenant)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/me/organisations", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Mine);
// A person's own Google Drive and OneDrive connection: self-scoped, no organisation permission.
app.MapGet("/api/v1/me/file-connections", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Status);
app.MapPost("/api/v1/me/file-connections/{provider}/start", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Start);
app.MapPost("/api/v1/me/file-connections/{provider}/exchange", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Exchange);
app.MapDelete("/api/v1/me/file-connections/{provider}", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Disconnect);
app.MapGet("/api/v1/me/file-connections/{provider}/files", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Browse);
app.MapGet("/api/v1/me/file-connections/{provider}/files/{fileId}/content", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Content);
app.MapGet("/api/v1/me/file-connections/google/picker", InsuranceIntelligence.Api.Integrations.FileConnectionEndpoints.Picker);
app.MapPost("/api/v1/organisation/invitations/accept", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Accept).RequireRateLimiting("join");
app.MapGet("/api/v1/organisation/invitations", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.ListInvitations);
app.MapPost("/api/v1/organisation/invitations", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Invite);
app.MapDelete("/api/v1/organisation/invitations/{invitationId:guid}", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.RevokeInvitation);
app.MapGet("/api/v1/organisation/links", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.ListLinks);
app.MapPost("/api/v1/organisation/links", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.RequestLink);
app.MapPost("/api/v1/organisation/links/{linkId:guid}/approve", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Approve);
app.MapPost("/api/v1/organisation/links/{linkId:guid}/decline", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Decline);
app.MapPost("/api/v1/organisation/links/{linkId:guid}/revoke", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Revoke);
app.MapGet("/api/v1/organisation/broker-dashboard", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.BrokerDashboard);
app.MapPost("/api/v1/marketplace/resolve", InsuranceIntelligence.Api.Marketplace.MarketplaceEndpoints.Resolve).RequireRateLimiting("marketplace");
app.MapPost("/api/v1/marketplace/activate", InsuranceIntelligence.Api.Marketplace.MarketplaceEndpoints.Activate).RequireRateLimiting("marketplace");
app.MapPost("/api/v1/marketplace/webhook", InsuranceIntelligence.Api.Marketplace.MarketplaceEndpoints.Webhook).RequireRateLimiting("webhook");
app.MapGet("/api/v1/organisation/onboarding", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Onboarding);
app.MapGet("/api/v1/organisation/profile", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.GetProfile);
app.MapPut("/api/v1/organisation/profile", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.SaveProfile);
app.MapPost("/api/v1/organisation/terms", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.AcceptTerms);
app.MapGet("/api/v1/organisation/security", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.GetSecurity);
app.MapPost("/api/v1/organisation/security-ack", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.AcknowledgeSecurity);
app.MapPost("/api/v1/organisation/sandbox", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.EnableSandbox);
app.MapGet("/api/v1/events/catalogue", InsuranceIntelligence.Api.Webhooks.WebhookEndpoints.Catalogue);
app.MapPost("/api/v1/organisation/webhooks", InsuranceIntelligence.Api.Webhooks.WebhookEndpoints.Create);
app.MapGet("/api/v1/organisation/webhooks", InsuranceIntelligence.Api.Webhooks.WebhookEndpoints.List);
app.MapDelete("/api/v1/organisation/webhooks/{subscriptionId:guid}", InsuranceIntelligence.Api.Webhooks.WebhookEndpoints.Remove);
app.MapPost("/api/v1/organisation/webhooks/{subscriptionId:guid}/test", InsuranceIntelligence.Api.Webhooks.WebhookEndpoints.Test);
app.MapGet("/api/v1/organisation/webhooks/{subscriptionId:guid}/deliveries", InsuranceIntelligence.Api.Webhooks.WebhookEndpoints.Deliveries);
app.MapPost("/api/v1/organisation/api-keys", InsuranceIntelligence.Api.Organisations.ApiKeyEndpoints.Create);
app.MapGet("/api/v1/organisation/api-keys", InsuranceIntelligence.Api.Organisations.ApiKeyEndpoints.List);
app.MapDelete("/api/v1/organisation/api-keys/{keyId:guid}", InsuranceIntelligence.Api.Organisations.ApiKeyEndpoints.Revoke);
app.MapGet("/api/v1/organisation/usage", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Usage);
app.MapGet("/api/v1/organisation/data-export", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.ExportData);
app.MapPost("/api/v1/organisation/close", InsuranceIntelligence.Api.Organisations.OrganisationEndpoints.Close);

// WO-14: the recipient's own feed, self-scoped like /api/v1/me, no extra permission (any
// authenticated tenant member can see their own notifications).
app.MapGet("/api/v1/notifications", InsuranceIntelligence.Api.Notifications.NotificationEndpoints.GetFeed);
app.MapPost("/api/v1/notifications/{notificationId:guid}/read", InsuranceIntelligence.Api.Notifications.NotificationEndpoints.MarkRead);
app.MapPost("/api/v1/notifications/read-all", InsuranceIntelligence.Api.Notifications.NotificationEndpoints.MarkAllRead);

app.MapGet("/api/v1/search", SearchEndpoints.Search)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));

// A claimant is never a tenant member, so these cannot be gated by PermissionEndpointFilter (which
// requires resolved membership permissions outside Development, no claimant will ever have one).
// ClaimantSessionEndpointFilter is the real gate: a verified magic-link session outside
// Development, or the existing demo-header trust inside it.
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}", ClientPortalEndpoints.GetClaim)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents", ClientPortalEndpoints.UploadDocuments)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/uploads", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Start)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Status)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPut("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}/chunks/{index:int}", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.PutChunk)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}/complete", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Complete)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapDelete("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Cancel)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}/on-file", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.GetOnFile)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/on-file/use-document", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.UseDocument)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/on-file/use-details", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.UseDetails)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/known-details", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.GetKnownDetails)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents/remove", ClientPortalEndpoints.RemoveDocument)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents/reorder", ClientPortalEndpoints.ReorderPages)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents/retry", ClientPortalEndpoints.RetryDocument)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/request-review", ClientPortalEndpoints.RequestReview)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
// Outcome L: "the customer sees their policy," "asks a question about the wording," and "requests
// a change to their contact details." None of these three has a {caseId} route segment (a
// verified policyholder with no claim yet must still reach them), so ClaimantSessionEndpointFilter's
// route-match check never fires for them; each handler requires the stashed session itself.
app.MapGet("/api/v1/client-portal/policy", ClientPortalEndpoints.GetPolicy)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/policy-questions", ClientPortalEndpoints.AskPolicyQuestion)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/contact-detail-changes", ClientPortalEndpoints.RequestContactDetailChange)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/magic-links", InsuranceIntelligence.Api.ClientPortal.ClaimantAuthenticationEndpoints.RequestMagicLink);
app.MapPost("/api/v1/client-portal/magic-links/redeem", InsuranceIntelligence.Api.ClientPortal.ClaimantAuthenticationEndpoints.RedeemMagicLink);
app.MapPost("/api/v1/client-portal/claims", InsuranceIntelligence.Api.ClientPortal.ClaimantAuthenticationEndpoints.CreateClaim)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());

app.MapPost("/api/v1/cases", CaseEndpoints.CreateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/document-checklist/preview", InsuranceIntelligence.Api.Documents.DocumentEndpoints.PreviewChecklist)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/policy-lookup", InsuranceIntelligence.Api.Cases.PolicyLookupEndpoints.LookupByPolicyNumber)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/exports/claims-register", InsuranceIntelligence.Api.Exports.ExportEndpoints.ClaimsRegister)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/exports/claims-register/columns", InsuranceIntelligence.Api.Exports.ExportEndpoints.ClaimsRegisterColumns)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/cases", CaseEndpoints.ListCases)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}", CaseEndpoints.GetCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/product-stages", CaseEndpoints.GetProductStages)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/assign", CaseEndpoints.AssignCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/status-options", InsuranceIntelligence.Api.Cases.ClaimStatusEndpoints.Options)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/status", InsuranceIntelligence.Api.Cases.ClaimStatusEndpoints.Change)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/claim-link", InsuranceIntelligence.Api.ClientPortal.StaffClaimLinkEndpoints.Send)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/cases/{caseId:guid}/escalate", CaseEndpoints.EscalateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/confirm", CaseEndpoints.ConfirmCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/resolve", InsuranceIntelligence.Api.Cases.ClaimResolutionEndpoints.Resolve)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/cases/{caseId:guid}/satisfaction-response", InsuranceIntelligence.Api.Cases.SatisfactionEndpoints.RecordResponse)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/satisfaction-responses", InsuranceIntelligence.Api.Cases.SatisfactionEndpoints.ListResponses)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims", ClaimsController.ListClaims)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims/attention", ClaimsController.GetAttention)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims/daily-summary", ClaimsController.GetDailySummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/claims/{claimId:guid}", ClaimsController.GetClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims/{claimId:guid}/draft-context", ClaimsController.GetDraftContext)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapPost("/api/v1/cases/{caseId:guid}/documents", DocumentEndpoints.UploadDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/v1/cases/{caseId:guid}/documents", DocumentEndpoints.ListDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/document-checklist", DocumentEndpoints.GetChecklist)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/content", DocumentEndpoints.GetDocumentContent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/document-slots/{slotKey}/pdf", DocumentEndpoints.GetSlotPdf)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/image-preview", DocumentEndpoints.GetDocumentImagePreview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/reprocess", DocumentEndpoints.ReprocessDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/file-into", DocumentEndpoints.FileDocumentIntoSlot)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/reclassify", DocumentEndpoints.ReclassifyDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/approve-extraction", DocumentEndpoints.ApproveExtraction)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/correct-field", DocumentEndpoints.CorrectExtractionField)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/v1/document-field-validation-rules", InsuranceIntelligence.Api.Documents.DocumentFieldValidationRuleEndpoints.ListRules)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/document-field-validation-rules", InsuranceIntelligence.Api.Documents.DocumentFieldValidationRuleEndpoints.SaveRule)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/cases/{caseId:guid}/review-requests", ContestabilityEndpoints.ListReviewRequests)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/review-requests/{requestId:guid}/acknowledge", ContestabilityEndpoints.AcknowledgeReviewRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/review-requests/{requestId:guid}/resolve", ContestabilityEndpoints.ResolveReviewRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
// Outcome L: staff side of a policyholder's contact-detail change request.
app.MapGet("/api/v1/contact-detail-changes", InsuranceIntelligence.Api.Customers.ContactDetailChangeEndpoints.ListChangeRequests)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/contact-detail-changes/{requestId:guid}/acknowledge", InsuranceIntelligence.Api.Customers.ContactDetailChangeEndpoints.AcknowledgeChangeRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/contact-detail-changes/{requestId:guid}/resolve", InsuranceIntelligence.Api.Customers.ContactDetailChangeEndpoints.ResolveChangeRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/knowledge-documents", KnowledgeEndpoints.UploadKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/v1/knowledge-documents", KnowledgeEndpoints.ListKnowledgeDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/v1/knowledge-documents/{knowledgeDocId:guid}", KnowledgeEndpoints.GetKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/{knowledgeDocId:guid}/approve", KnowledgeEndpoints.ApproveKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/{knowledgeDocId:guid}/archive", KnowledgeEndpoints.ArchiveKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/{knowledgeDocId:guid}/classify", KnowledgeEndpoints.ClassifyKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/reindex", KnowledgeEndpoints.ReindexKnowledgeDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/assistant/policy-questions", AssistantEndpoints.AskPolicyQuestion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapGet("/api/v1/assistant/actions", AssistantEndpoints.ListActions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapGet("/api/v1/assistant/actions/{caseId:guid}", AssistantEndpoints.ListActionsForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
// The prose POST /command-center/assistant endpoint was dropped (superseded by chat-first /query).
// The /metrics endpoint is retained via the salvaged CommandCenterMetricsLoader (metric_pills).
app.MapGet("/api/v1/command-center/metrics", CommandCenterMetricsLoader.GetMetrics)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/query", CommandCenterQueryEndpoints.RunQuery)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/command-center/conversations", CommandCenterConversationEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/command-center/conversations/{conversationId:guid}", CommandCenterConversationEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPatch("/api/v1/command-center/conversations/{conversationId:guid}", CommandCenterConversationEndpoints.Rename)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/conversations/{conversationId:guid}/archive", CommandCenterConversationEndpoints.Archive)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/conversations/{conversationId:guid}/restore", CommandCenterConversationEndpoints.Restore)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/provider-status", ProviderStatusEndpoints.GetProviderStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/system/capabilities", SystemCapabilityEndpoints.GetCapabilities)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/cases/{caseId:guid}/timeline", WorkflowEndpoints.GetTimeline)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/recommended-actions", WorkflowEndpoints.GetRecommendedActions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/workflow/evaluate", WorkflowEndpoints.EvaluateWorkflow)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapGet("/api/v1/workflow/tasks", WorkflowEndpoints.ListTasks)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/my-work", InsuranceIntelligence.Api.MyWork.MyWorkEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(
        Permission.ViewAssignedClaims,
        Permission.ViewAllClaims,
        Permission.ApproveSettlementWorkflowStep,
        Permission.ManageCustomerCommunications,
        Permission.ReviewAgentWorkItems));
app.MapGet("/api/v1/workflow/tasks/{taskId:guid}", WorkflowEndpoints.GetTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/workflow/tasks/{taskId:guid}/complete", WorkflowEndpoints.CompleteTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/workflow/tasks/{taskId:guid}/assign", WorkflowEndpoints.AssignTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/approvals", ApprovalEndpoints.ListApprovals)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapGet("/api/v1/approvals/{approvalId:guid}", ApprovalEndpoints.GetApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/cases/{caseId:guid}/approvals", ApprovalEndpoints.CreateApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/approvals/{approvalId:guid}/approve", ApprovalEndpoints.ApproveApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/approvals/{approvalId:guid}/reject", ApprovalEndpoints.RejectApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/approvals/{approvalId:guid}/override", ApprovalEndpoints.OverrideApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.OverrideAiRecommendation));
app.MapPost("/api/v1/approvals/{approvalId:guid}/escalate", ApprovalEndpoints.EscalateApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/approvals/{approvalId:guid}/execute-next-step", ApprovalEndpoints.ExecuteApprovedNextStep)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/draft", CustomerUpdateEndpoints.DraftCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/cases/{caseId:guid}/summary", InsuranceIntelligence.Api.Claims.ClaimsSummaryEndpoints.SummarizeCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/case-review", InsuranceIntelligence.Api.Claims.ClaimsSummaryEndpoints.GetLatestCaseReview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/customer-updates", CustomerUpdateEndpoints.ListCustomerUpdates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims));
app.MapGet("/api/v1/customer-updates", CustomerUpdateEndpoints.ListAllCustomerUpdates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/approve", CustomerUpdateEndpoints.ApproveCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/reject", CustomerUpdateEndpoints.RejectCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/send", CustomerUpdateEndpoints.SendCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPut("/api/v1/customer-updates/{updateId:guid}", CustomerUpdateEndpoints.EditCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/write", CustomerUpdateEndpoints.WriteCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/from-template", CustomerUpdateEndpoints.CreateCustomerUpdateFromTemplate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/customer-follow-ups", CustomerFollowUpEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-follow-ups/{taskId:guid}/cancel", CustomerFollowUpEndpoints.Cancel)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));

// Message template library (outcome D), versioned exactly like agent behaviour and products.
app.MapGet("/api/v1/communication-templates", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.ListTemplates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/communication-templates/{templateKey}", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.GetCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/communication-templates/{templateKey}/versions", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/communication-templates/{templateKey}", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.SaveVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/communication-templates/seed-defaults", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.SeedDefaults)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/reports/operations-summary", ReportsEndpoints.GetOperationsSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/admin/tenant-settings", InsuranceIntelligence.Api.Admin.TenantSettingsEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/tenant-settings", InsuranceIntelligence.Api.Admin.TenantSettingsEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/tenant-configuration", AdminEndpoints.GetTenantConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/tenant-configuration", AdminEndpoints.UpdateTenantConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/provider-modes", AdminEndpoints.GetProviderModes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/provider-health", AdminEndpoints.GetProviderHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/roles", AdminEndpoints.GetRoles)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/users", AdminEndpoints.GetUsers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/users", AdminEndpoints.CreateUser)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/users/{userId}", AdminEndpoints.UpdateUserRole)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/users/{userId}/role", AdminEndpoints.UpdateUserRole)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Demo dataset seeding, hard-gated inside the handler to non-protected runtime profiles
// (see DemoSeedEndpoints), never reachable in Staging/Production/ManagedApplicationReviewer.
app.MapPost("/api/v1/admin/demo-seed", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.Run);
app.MapPost("/api/v1/admin/demo-seed/policyholders", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedPolicyholders);
app.MapPost("/api/v1/admin/demo-seed/test-policyholder", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedTestPolicyholder);
app.MapPost("/api/v1/admin/demo-seed/microinsurance", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedMicroinsurance);
app.MapPost("/api/v1/admin/demo-seed/bank-customers", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedBankCustomers);
app.MapPost("/api/v1/admin/demo-seed/conversations", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedConversations);
app.MapPost("/api/v1/admin/db-migrate", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ApplyPending);
app.MapPost("/api/v1/admin/db-migrate/reconcile-history", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ReconcileHistory);
app.MapPost("/api/v1/admin/db-migrate/force-reconcile-schema", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ForceReconcileSchema);
app.MapGet("/api/v1/admin/db-migrate/diagnose", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.Diagnose);

if (developmentDiagnosticsEnabled)
{
    app.MapPost("/api/v1/admin/debug-teams-notification", DebugEndpoints.TriggerTeamsNotification)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    // What the Mock communication adapters "sent" (Mock communication provider only).
    app.MapGet("/api/v1/dev/mock-outbox", InsuranceIntelligence.Api.Local.Mock.MockOutboxEndpoints.List)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    app.MapDelete("/api/v1/dev/mock-outbox", InsuranceIntelligence.Api.Local.Mock.MockOutboxEndpoints.Clear)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    // The pretend payment operator, and the billing pass, for developer runtimes (build-loop item 32).
    if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(paymentProviderSetting, runtimeProfile))
    {
        app.MapGet("/api/v1/dev/mock-payments", (InsuranceIntelligence.Api.Local.Mock.MockPaymentLedger ledger) => InsuranceIntelligence.Api.Local.Mock.MockPaymentEndpoints.List(ledger))
            .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
        app.MapPost("/api/v1/dev/mock-payments/{reference}/settle", InsuranceIntelligence.Api.Local.Mock.MockPaymentEndpoints.Settle)
            .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    }

    app.MapPost("/api/v1/dev/billing/run", async (InsuranceIntelligence.Application.Billing.SubscriptionLifecycleService lifecycle, InsuranceIntelligence.Application.Billing.PaymentReconciliationService reconciliation, CancellationToken cancellationToken) =>
        Results.Ok(new { reconciliation = await reconciliation.RunAsync(null, cancellationToken), cycle = await lifecycle.RunAsync(null, cancellationToken) }))
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    // Puts a message into a pretend IMAP mailbox (Mock email provider only).
    app.MapPost("/api/v1/dev/mock-mailbox/deliver", InsuranceIntelligence.Api.Local.Mock.MockMailboxEndpoints.Deliver)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
}

// W2.5: Phase 2 core-workspace read routes (see docs/02-development-engineering/iip_api_specification.md).
app.MapGet("/api/v1/channels/overview", ChannelEndpoints.GetOverview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/channels/conversations/{channel}/{threadId}", ChannelEndpoints.GetThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapPost("/api/v1/channels/conversations/{channel}/{threadId}/messages", ChannelEndpoints.SendMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications, Permission.SendCustomerUpdate));
app.MapPost("/api/v1/channels/conversations/{channel}/{threadId}/identity-link", ChannelIdentityEndpoints.Link)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications, Permission.ViewCustomers));
app.MapGet("/api/v1/channels/conversations/{channel}/{threadId}/identity-link", ChannelIdentityEndpoints.GetForThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/customers/{customerId}/channel-identities", ChannelIdentityEndpoints.ListForCustomer)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers, Permission.ViewChannels));
app.MapGet("/api/v1/customers/{customerId}/channel-timeline", ChannelIdentityEndpoints.GetCustomerTimeline)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers, Permission.ViewChannels));
app.MapPost("/api/v1/channel-identities/{linkId:guid}/revoke", ChannelIdentityEndpoints.Revoke)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/customers", CustomerProfileEndpoints.ListCustomers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapGet("/api/v1/customers/{customerId}", CustomerProfileEndpoints.GetCustomerProfile)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapPost("/api/v1/customers/{customerId}/summary", InsuranceIntelligence.Api.Customer.CustomerSummaryEndpoints.Summarize)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
// Outcome M: consent as a first class, staff-visible, auditable record.
app.MapGet("/api/v1/customers/{customerId}/consent", InsuranceIntelligence.Api.Customers.ConsentEndpoints.ListForCustomer)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapPost("/api/v1/customers/{customerId}/consent", InsuranceIntelligence.Api.Customers.ConsentEndpoints.RecordExplicitOptIn)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customers/{customerId}/consent/withdraw", InsuranceIntelligence.Api.Customers.ConsentEndpoints.Withdraw)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
// Names for ids, for any staff member who can see claims or customers; also feeds the assignment picker.
app.MapGet("/api/v1/directory/people", InsuranceIntelligence.Api.People.PeopleEndpoints.ListPeople)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ViewCustomers, Permission.ViewSalesLeads));
app.MapGet("/api/v1/brief", InsuranceIntelligence.Api.Brief.BriefEndpoints.GetBrief)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/academy/me", InsuranceIntelligence.Api.Academy.AcademyMeEndpoint.Get).RequireRateLimiting("academy-public");
app.MapGet("/api/v1/academy/public/courses", InsuranceIntelligence.Api.Academy.AcademyPublicEndpoints.Courses).RequireRateLimiting("academy-public");
app.MapGet("/api/v1/academy/public/courses/{courseId}", InsuranceIntelligence.Api.Academy.AcademyPublicEndpoints.Course).RequireRateLimiting("academy-public");
app.MapGet("/api/v1/academy/public/certificates/{code}", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Verify).RequireRateLimiting("academy-public");
app.MapGet("/api/v1/academy/learner", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.State).RequireRateLimiting("academy-learner");
app.MapPut("/api/v1/academy/learner/profile", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.UpdateProfile).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/learner/notifications/read", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.ReadNotifications).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/learner/certificates/{certificateId}", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Certificate).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/enrol", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Enrol).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/courses/{courseId}/lessons/{lessonId}", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Lesson).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/courses/{courseId}/lessons/{lessonId}/media/{fileName}", InsuranceIntelligence.Api.Academy.AcademyMediaEndpoints.Get).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/lessons/{lessonId}/complete", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Complete).RequireRateLimiting("academy-learner");
app.MapPut("/api/v1/academy/courses/{courseId}/lessons/{lessonId}/note", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Note).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/courses/{courseId}/assessments/{assessmentId}", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.StartAssessment).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/assessments/{assessmentId}/submit", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.SubmitAssessment).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/courses/{courseId}/final", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.FinalTask).RequireRateLimiting("academy-learner");
app.MapPut("/api/v1/academy/courses/{courseId}/final", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.SaveFinalDraft).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/final/submit", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.SubmitFinal).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/operator/finals", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.PendingFinals).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/finals/mark", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.MarkFinal).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/operator/learners", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.Learners).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/enrol", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.OperatorEnrol).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/assessments/reset", InsuranceIntelligence.Api.Academy.AcademyLearnerEndpoints.ResetAssessment).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/community", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.State).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/community/consent", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Consent).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/community/groups/{groupId}/join", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Join).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/community/groups/{groupId}/leave", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Leave).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/community/posts", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Post).RequireRateLimiting("academy-post");
app.MapPost("/api/v1/academy/community/posts/{postId}/like", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Like).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/community/posts/{postId}/comments", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Comment).RequireRateLimiting("academy-post");
app.MapPost("/api/v1/academy/community/posts/{postId}/report", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Report).RequireRateLimiting("academy-post");
app.MapDelete("/api/v1/academy/community/posts/{postId}", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Delete).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/community/events/{eventId}/rsvp", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Rsvp).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/community/events", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.CreateEvent).RequireRateLimiting("academy-learner");
app.MapDelete("/api/v1/academy/operator/community/events/{eventId}", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.DeleteEvent).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/community/groups", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.CreateGroup).RequireRateLimiting("academy-learner");
app.MapPut("/api/v1/academy/operator/community/discord", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.SetDiscord).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/operator/community/reports", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Reports).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/community/posts/{postId}/hide", InsuranceIntelligence.Api.Academy.AcademyCommunityEndpoints.Hide).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/zenith/status", InsuranceIntelligence.Api.Academy.AcademyZenithEndpoints.Status).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/zenith/ask", InsuranceIntelligence.Api.Academy.AcademyZenithEndpoints.Ask).RequireRateLimiting("academy-zenith");
app.MapPost("/api/v1/academy/zenith/quiz/answer", InsuranceIntelligence.Api.Academy.AcademyZenithEndpoints.CheckQuiz).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/courses/{courseId}/offer", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Offer).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/checkout", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Checkout).RequireRateLimiting("academy-pay");
app.MapPost("/api/v1/academy/payments/confirm", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Confirm).RequireRateLimiting("academy-pay");
app.MapPost("/api/v1/academy/payments/webhook", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Webhook).RequireRateLimiting("webhook");
app.MapGet("/api/v1/academy/operator/payments", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Orders).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/payments/{reference}/recheck", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Recheck).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/operator/payments/{reference}/refunded", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.Refunded).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/operator/settings/payments", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.GetPaymentSettings).RequireRateLimiting("academy-learner");
app.MapPut("/api/v1/academy/operator/settings/payments", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.SavePaymentSettings).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/operator/settings/zenith", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.GetZenithSettings).RequireRateLimiting("academy-learner");
app.MapPut("/api/v1/academy/operator/settings/zenith", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.SaveZenithSettings).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/final/open", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.FinalOpen).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/final/leave", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.FinalLeave).RequireRateLimiting("academy-learner");
app.MapPost("/api/v1/academy/courses/{courseId}/assessments/{assessmentId}/leave", InsuranceIntelligence.Api.Academy.Payments.AcademyPaymentEndpoints.AssessmentLeave).RequireRateLimiting("academy-learner");
app.MapGet("/api/v1/academy/pricing", InsuranceIntelligence.Api.Academy.AcademyEndpoints.GetPricing);
app.MapPut("/api/v1/academy/pricing", InsuranceIntelligence.Api.Academy.AcademyEndpoints.SavePricing)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));

// Second step after sign-in (two-factor). See IIP_CIP_CHANNELS_AND_INTEGRATIONS_BUILD_PLAN.md, workstream S1.
app.MapGet("/api/v1/security/mfa/status", PlatformMfaEndpoints.GetStatus);
app.MapPost("/api/v1/security/mfa/totp/begin", PlatformMfaEndpoints.BeginTotp).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/totp/confirm", PlatformMfaEndpoints.ConfirmTotp).RequireRateLimiting("mfa");
app.MapDelete("/api/v1/security/mfa/credentials/{method}/{id:guid}", PlatformMfaEndpoints.RemoveCredential);
app.MapPost("/api/v1/security/mfa/backup-codes/rotate", PlatformMfaEndpoints.RotateBackupCodes).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/codes/send", PlatformMfaEndpoints.SendCode).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/codes/confirm", PlatformMfaEndpoints.ConfirmCode).RequireRateLimiting("mfa");
app.MapDelete("/api/v1/security/mfa/channels/{channel}", PlatformMfaEndpoints.DisableChannel);
app.MapPost("/api/v1/security/mfa/verify", PlatformMfaEndpoints.Verify).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/passkey/register/options", PlatformMfaEndpoints.PasskeyRegistrationOptions).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/passkey/register/result", PlatformMfaEndpoints.PasskeyRegistrationResult).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/passkey/assert/options", PlatformMfaEndpoints.PasskeyAssertionOptions).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/passkey/assert/result", PlatformMfaEndpoints.PasskeyAssertionResult).RequireRateLimiting("mfa");
app.MapPost("/api/v1/security/mfa/snooze", PlatformMfaEndpoints.Snooze);
app.MapPost("/api/v1/security/mfa/sign-out", PlatformMfaEndpoints.SignOut);
app.MapGet("/api/v1/security/mfa/policy", PlatformMfaEndpoints.GetPolicy);
app.MapPut("/api/v1/security/mfa/policy", PlatformMfaEndpoints.SetPolicy)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/security/mfa/users/{userId}/reset", PlatformMfaEndpoints.ResetUser)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/security/mfa/metrics", PlatformMfaEndpoints.GetMetrics)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/sales-leads", SalesLeadEndpoints.ListLeads)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/sales-leads/{leadId}", SalesLeadEndpoints.GetLeadDetail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/sales-leads", SalesLeadEndpoints.CreateLead)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/import", SalesLeadEndpoints.ImportList)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/notes", SalesLeadEndpoints.AddNote)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPut("/api/v1/sales-leads/{leadId}/assignee", SalesLeadEndpoints.Assign)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/insights/refresh", SalesLeadEndpoints.RefreshInsights)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/acquisition/escalations", async (IAcquisitionReportService reports, CancellationToken ct) => Results.Ok(await reports.EscalationsAsync(ct)))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/acquisition/agents", async (IAcquisitionReportService reports, CancellationToken ct) => Results.Ok(await reports.AgentOperationsAsync(ct)))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/acquisition/channel-intelligence", async (IAcquisitionReportService reports, CancellationToken ct) => Results.Ok(await reports.ChannelIntelligenceAsync(ct)))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/sales-leads/outreach/draft", SalesLeadEndpoints.DraftOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/outreach/follow-ups", SalesLeadEndpoints.DraftFollowUps)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/outreach/send", SalesLeadEndpoints.SendOutreachMany)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPut("/api/v1/sales-leads/{leadId}/outreach/{draftId:guid}", SalesLeadEndpoints.EditOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/outreach/{draftId:guid}/send", SalesLeadEndpoints.SendOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/sales-leads/{leadId}/outreach/{draftId:guid}/dismiss", SalesLeadEndpoints.DismissOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/triage", SalesLeadEndpoints.RunTriageForAll)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/triage", SalesLeadEndpoints.RunTriageForLead)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));

// Product configuration, the spine everything else in claim handling reads from (blueprint
// outcome A/C). Every save is a new version; publishing needs an IRA approval reference and a
// second person, distinct from whoever submitted it.
// Claim types (build-loop item 3): dropdown options and suggestions for whoever is filing a claim,
// plus the one-off import of the free-text types already used on existing claims.
app.MapGet("/api/v1/claim-types", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ManageProducts));
app.MapPost("/api/v1/claim-types/suggest", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.Suggest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/claim-types/import-existing", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.ImportExisting)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
// Preview as customer (build-loop item 8): read only, nothing stored or sent.
app.MapGet("/api/v1/products/{productId:guid}/customer-preview", InsuranceIntelligence.Api.Products.ProductPreviewEndpoints.CustomerPreview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
// Policy wording to drafted setup (build-loop item 7): the model proposes, a person accepts, edits or
// rejects each item, and applying makes a draft version of the product (publishing still needs a
// second approver).
app.MapPost("/api/v1/products/{productId:guid}/wording-drafts", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Create)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/wording-drafts", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPut("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}/items/{itemId}", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.UpdateItem)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}/apply", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Apply)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}/discard", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Discard)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
// Privacy notice and consent (build-loop item 9): the claimant reads the notice and agrees before the
// first upload, and can withdraw from the claim page; staff see whether the claimant has agreed.
app.MapGet("/api/v1/client-portal/privacy-notice", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.GetNotice)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/consent", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.Grant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/consent/withdraw", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.Withdraw)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/cases/{caseId:guid}/privacy-consent", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.GetForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/client-portal/claim-types", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.ListForClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
// Claim fields (build-loop item 5): the questions a product asks about a claim and a claim's answers.
app.MapGet("/api/v1/claim-fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetForm)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ManageProducts));
// Extracted data into the claim (build-loop item 15): suggestions read from documents, decided by an officer.
// Authenticity checks (build-loop item 16): advice on pictures, with a person's disposition.
// Background work (build-loop item 17): what is waiting, running, done and stuck.
app.MapGet("/api/v1/admin/background-jobs", InsuranceIntelligence.Api.Jobs.BackgroundJobEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/background-jobs/{jobId:guid}/retry", InsuranceIntelligence.Api.Jobs.BackgroundJobEndpoints.Retry)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/cases/{caseId:guid}/background-work", InsuranceIntelligence.Api.Jobs.BackgroundJobEndpoints.ListForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/authenticity", InsuranceIntelligence.Api.Documents.AuthenticityEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/authenticity/disposition", InsuranceIntelligence.Api.Documents.AuthenticityEndpoints.SetDisposition)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/admin/claim-status-wording", InsuranceIntelligence.Api.Admin.ClaimStatusWordingEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/claim-status-wording", InsuranceIntelligence.Api.Admin.ClaimStatusWordingEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/status-notifications", InsuranceIntelligence.Api.Admin.StatusNotificationSettingsEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/status-notifications", InsuranceIntelligence.Api.Admin.StatusNotificationSettingsEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/cases/{caseId:guid}/assessment", InsuranceIntelligence.Api.Fraud.ClaimAssessmentEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/cases/{caseId:guid}/assessment/refresh", InsuranceIntelligence.Api.Fraud.ClaimAssessmentEndpoints.Refresh)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/cases/{caseId:guid}/assessment/response", InsuranceIntelligence.Api.Fraud.ClaimAssessmentEndpoints.Respond)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.OverrideAiRecommendation));
app.MapGet("/api/v1/cases/{caseId:guid}/fraud-review", InsuranceIntelligence.Api.Fraud.FraudAgentEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/cases/{caseId:guid}/fraud-review/run", InsuranceIntelligence.Api.Fraud.FraudAgentEndpoints.Run)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapGet("/api/v1/cases/{caseId:guid}/field-proposals", InsuranceIntelligence.Api.Cases.FieldProposalEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/field-proposals/{proposalId:guid}/accept", InsuranceIntelligence.Api.Cases.FieldProposalEndpoints.Accept)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/field-proposals/{proposalId:guid}/reject", InsuranceIntelligence.Api.Cases.FieldProposalEndpoints.Reject)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetForClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPut("/api/v1/cases/{caseId:guid}/field-values", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.SaveForClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/client-portal/claim-fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetFormForClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}/fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetForClaimByClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPut("/api/v1/client-portal/claims/{caseId:guid}/field-values", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.SaveForClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/products", InsuranceIntelligence.Api.Products.ProductEndpoints.ListProducts)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/default-stage-library", InsuranceIntelligence.Api.Products.ProductEndpoints.GetDefaultStageLibrary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}", InsuranceIntelligence.Api.Products.ProductEndpoints.GetCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/versions", InsuranceIntelligence.Api.Products.ProductEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/versions/{version:int}", InsuranceIntelligence.Api.Products.ProductEndpoints.GetVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products", InsuranceIntelligence.Api.Products.ProductEndpoints.CreateDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPut("/api/v1/products/{productId:guid}", InsuranceIntelligence.Api.Products.ProductEndpoints.UpdateDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/submit", InsuranceIntelligence.Api.Products.ProductEndpoints.SubmitForPublishApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/approve", InsuranceIntelligence.Api.Products.ProductEndpoints.ApprovePublish)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/reject", InsuranceIntelligence.Api.Products.ProductEndpoints.RejectPublish)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/retire", InsuranceIntelligence.Api.Products.ProductEndpoints.Retire)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/wording-extractions", InsuranceIntelligence.Api.Products.ProductEndpoints.ListWordingExtractions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/wording-extractions/{knowledgeDocumentId:guid}", InsuranceIntelligence.Api.Products.ProductEndpoints.ExtractWording)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Document type catalog (claim intake build prompt outcome A): the eight built-in types plus
// whatever an insurer adds, versioned like communication templates. Backs the document matrix
// dropdown and the report of products whose matrix already holds a key the catalog does not know.
app.MapGet("/api/v1/document-types/upload-options", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListUploadOptions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/v1/document-types", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/document-types/{key}/versions", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/document-types/{key}", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.SaveVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/document-types/seed-defaults", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.SeedDefaults)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/document-types/unresolved-product-keys", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListUnresolvedProductDocumentKeys)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Outcome N: file import for insurers whose core system has no API. Parse reads the uploaded file
// only (nothing persisted yet); Commit takes the confirmed column mapping back and imports.
app.MapPost("/api/v1/policy-import/parse", InsuranceIntelligence.Api.Policies.PolicyImportEndpoints.Parse)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/policy-import/commit", InsuranceIntelligence.Api.Policies.PolicyImportEndpoints.Commit)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Kato workspace: analyse a customer workbook, export the analysis, draw a chart from real records.
// None of these sends a message; sending stays behind the named-approval outreach path.
app.MapPost("/api/v1/workspace/prospects/analyse", InsuranceIntelligence.Api.Workspace.WorkspaceEndpoints.AnalyseProspects)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/workspace/prospects/export", InsuranceIntelligence.Api.Workspace.WorkspaceEndpoints.ExportAnalysis)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/workspace/charts", InsuranceIntelligence.Api.Workspace.WorkspaceEndpoints.Chart)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));

// Outcome O: retention watch, a screen of its own for policies nearing cover end.
app.MapGet("/api/v1/retention/watch", InsuranceIntelligence.Api.Retention.RetentionEndpoints.GetWatch)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapPost("/api/v1/retention/policies/{policyId}/reminders", InsuranceIntelligence.Api.Retention.RetentionEndpoints.DraftReminder)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/retention/reminders/{reminderId:guid}/approve-and-send", InsuranceIntelligence.Api.Retention.RetentionEndpoints.ApproveAndSend)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));

// Outcome K acceptance: "accuracy per document type is measured and visible," computed from the
// labelled examples every reclassification/correction/confirmation accumulates.
app.MapGet("/api/v1/documents/quality/accuracy", InsuranceIntelligence.Api.Documents.DocumentQualityEndpoints.GetAccuracyByType)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Gate 1 (G1.7): platform-operator tenant onboarding + Entra admin-consent flow (ADR-0005).
app.MapPost("/api/v1/onboarding", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.Create)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding/{onboardingId}", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding/{onboardingId}/consent-url", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.GetConsentUrl)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
// Admin-consent redirect target, authorized by the opaque state value, not a permission.
// Deliberately OFF /api/v1: the insurer admin returns here from Microsoft UNAUTHENTICATED to IIP,
// so it must bypass the /api/v1 authenticated-request guard (like /health).
app.MapGet("/onboarding/consent-callback", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.ConsentCallback);
app.MapGet("/api/v1/audit/events", AuditEndpoints.ListEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
// Initiative LP: rule-based fraud advisory signals (deterministic, tenant-scoped, advisory-only).
app.MapGet("/api/v1/fraud/signals", InsuranceIntelligence.Api.Fraud.FraudEndpoints.GetSignals)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
// WO-19: fraud signal disposition, triage, assign for investigation, record outcome, close.
app.MapPost("/api/v1/fraud/signals/triage", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.Triage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/fraud/signals/{signalId:guid}/assign", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.Assign)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/fraud/signals/{signalId:guid}/outcome", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.RecordOutcome)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/fraud/signals/{signalId:guid}/close", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.Close)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
// Initiative LP: rule-based underwriting advisory reviews (deterministic, advisory-only).
app.MapGet("/api/v1/underwriting/reviews", InsuranceIntelligence.Api.Underwriting.UnderwritingEndpoints.GetReviews)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewUnderwriting));
// Initiative LP: AI Agent Registry, catalog + tenant-scoped persisted kill switch (ADR-0002).
app.MapGet("/api/v1/admin/agents", InsuranceIntelligence.Api.Admin.Agents.AgentRegistryEndpoints.ListAgents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/admin/agents/{agentKey}/kill-switch", InsuranceIntelligence.Api.Admin.Agents.AgentRegistryEndpoints.SetKillSwitch)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
// Governed agent workflow control plane: immutable Releases 1-5 plus tenant-scoped custom flows.
app.MapGet("/api/v1/agent-workflows", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListDefinitions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflows/{workflowKey}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetDefinition)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflows/{workflowKey}/versions", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflows/{workflowKey}/versions/{version:int}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapPost("/api/v1/agent-workflows", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.SaveDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapPost("/api/v1/agent-workflows/{workflowKey}/draft-new-version", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.DraftNewVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapPost("/api/v1/agent-workflows/{workflowKey}/publish", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.Publish)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapPost("/api/v1/agent-workflows/{workflowKey}/runs", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.StartRun)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.RunAgentWorkflows));
app.MapGet("/api/v1/agent-workflow-runs", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListRuns)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflow-runs/{runId:guid}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetRun)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapPost("/api/v1/agent-workflow-runs/{runId:guid}/nodes/{nodeKey}/complete", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.CompleteNode)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapGet("/api/v1/agent-work-items", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListWorkItems)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ReviewAgentWorkItems));
app.MapGet("/api/v1/agent-work-items/{workItemId:guid}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetWorkItem)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ReviewAgentWorkItems));
app.MapPost("/api/v1/agent-work-items/{workItemId:guid}/decision", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.DecideWorkItem)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ReviewAgentWorkItems));
// Initiative LP: Billing & entitlements, default package + real usage meters (ADR-0003).
// The credit meter: anyone who can use the workspace sees credits used against the plan; cost is for billing viewers.
app.MapGet("/api/v1/billing/credits", InsuranceIntelligence.Api.Admin.Billing.BillingEndpoints.GetCredits)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/admin/billing", InsuranceIntelligence.Api.Admin.Billing.BillingEndpoints.GetSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
// Plans, upgrades and payments (build-loop item 32): reading needs ViewBilling, changing needs ManageTenantSettings.
app.MapGet("/api/v1/admin/billing/plan", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.GetPlan)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
app.MapPost("/api/v1/admin/billing/checkout", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.Checkout)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/billing/payments", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.ListPayments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
app.MapGet("/api/v1/admin/billing/payments/{paymentIntentId:guid}", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.GetPayment)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
app.MapGet("/api/v1/admin/billing/invoices", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.ListInvoices)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
app.MapGet("/api/v1/admin/billing/invoices/{invoiceId:guid}/receipt", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.GetReceipt)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
app.MapPost("/api/v1/admin/billing/cancel", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.Cancel)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/billing/resume", InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.Resume)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Retention, legal holds and erasing one person's data (build-loop item 34).
app.MapGet("/api/v1/admin/data/legal-holds", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.ListHolds)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/data/legal-holds", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.PlaceHold)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapDelete("/api/v1/admin/data/legal-holds/{caseId:guid}", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.ReleaseHold)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/data/erasure/preview", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.PreviewErasure)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/data/erasure", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.Erase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/data/deletions", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.Deletions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/data/retention/run", InsuranceIntelligence.Api.DataLifecycle.DataLifecycleEndpoints.RunRetention)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// The payment provider's own call. The secret in the address is its only credential and the body is never read.
app.MapMethods("/api/v1/payments/callback/{paymentIntentId:guid}/{secret}", ["GET", "POST"], InsuranceIntelligence.Api.Admin.Billing.BillingPlanEndpoints.Callback)
    .RequireRateLimiting("payments-callback");
// Initiative LP: cross-case document library.
app.MapGet("/api/v1/documents", InsuranceIntelligence.Api.Documents.DocumentLibraryEndpoints.ListDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAllClaims));

app.MapGet("/api/v1/compliance/audit-events", ComplianceEndpoints.ListAuditEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/compliance/posture", ComplianceEndpoints.GetCompliancePosture)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/audit-pack", ComplianceEndpoints.GetAuditPack)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapPost("/api/v1/compliance/cases/{caseId:guid}/audit-pack/export", ComplianceEndpoints.ExportAuditPack)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapPost("/api/v1/compliance/cases/{caseId:guid}/audit-pack/export/save", InsuranceIntelligence.Api.Compliance.EvidencePackSaveEndpoints.Save)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapPost("/api/v1/compliance/cases/{caseId:guid}/claims-pack/send", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.SendNow)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/claims-pack/history", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.History)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/admin/claims-pack-delivery", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.GetSettings)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/claims-pack-delivery", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.PutSettings)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Public: the secure link in an email for a pack too large to attach. The long random token is the whole proof.
app.MapGet("/api/v1/shared/claims-packs/{token}", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.DownloadShared);
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/claims-pack", InsuranceIntelligence.Api.Compliance.ClaimsPackEndpoints.Download)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/audit-pack/export/download", ComplianceEndpoints.DownloadAuditPackExport)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/compliance/retention/policies", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.ListPolicies)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/compliance/retention/policies", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.SavePolicy)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/compliance/legal-hold", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.GetLegalHold)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/compliance/legal-hold", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.SetLegalHold)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/compliance/retention/enforce", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.EnforceRetention)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Follow-through (outcome G): missing-document reminders and pre-breach SLA warnings, the same
// service the scheduled CaseFollowThroughTimerFunction calls.
app.MapPost("/api/v1/cases/follow-through/run", InsuranceIntelligence.Api.Cases.CaseFollowThroughEndpoints.Run)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/compliance/data-subjects/{customerId}/export", InsuranceIntelligence.Api.Compliance.DataSubjectRightsEndpoints.Export)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPatch("/api/v1/compliance/data-subjects/{customerId}", InsuranceIntelligence.Api.Compliance.DataSubjectRightsEndpoints.Rectify)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/compliance/data-subjects/{customerId}/erasure", InsuranceIntelligence.Api.Compliance.DataSubjectRightsEndpoints.RequestErasure)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/integrations/email/health", EmailIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.ListMailboxes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.CreateMailbox)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPut("/api/v1/integrations/email/mailboxes/{mailboxId:guid}", EmailIntegrationEndpoints.UpdateMailbox)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
// Available in every runtime, unlike test-ingestion below: an insurer who connects a mailbox in
// Staging or Production needs a way to collect mail on demand and see that the connection works.
// The MailboxIngestion timer function does this on a schedule; this is the same service, on demand.
app.MapPost("/api/v1/integrations/email/ingest", EmailIntegrationEndpoints.Ingest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
if (developmentDiagnosticsEnabled)
{
    app.MapPost("/api/v1/integrations/email/test-ingestion", EmailIntegrationEndpoints.TestIngestion)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
}
app.MapPost("/api/v1/integrations/email/process-message", EmailIntegrationEndpoints.ProcessMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/messages", EmailIntegrationEndpoints.ListMessages)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/messages/{messageId:guid}", EmailIntegrationEndpoints.GetMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/graph/webhook", EmailIntegrationEndpoints.GraphWebhook);
// The customer portal's identity (build-loop item 29): public branding for the claimant pages, and the administrator's controls.
app.MapGet("/api/v1/client-portal/branding", InsuranceIntelligence.Api.ClientPortal.PortalIdentityEndpoints.GetBranding);
app.MapGet("/api/v1/admin/portal-identity", InsuranceIntelligence.Api.ClientPortal.PortalIdentityEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/portal-identity", InsuranceIntelligence.Api.ClientPortal.PortalIdentityEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/portal-identity/verify-hostname", InsuranceIntelligence.Api.ClientPortal.PortalIdentityEndpoints.VerifyHostname)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/portal-identity/sms-webhook", InsuranceIntelligence.Api.ClientPortal.PortalIdentityEndpoints.RotateSmsWebhook)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/integrations/{channel}/test-send", InsuranceIntelligence.Api.Integrations.IntegrationTestSendEndpoints.Send)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/connectors", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/connectors", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.Create)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPut("/api/v1/connectors/{connectorId:guid}/folders", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.ReplaceFolders)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/connectors/{connectorId:guid}/sync", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.SyncNow)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/connectors/{connectorId:guid}/pause", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.Pause)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/connectors/{connectorId:guid}/resume", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.Resume)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapDelete("/api/v1/connectors/{connectorId:guid}", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.Delete)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/v1/connectors/{connectorId:guid}/items", InsuranceIntelligence.Api.Connectors.ConnectorEndpoints.Items)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/v1/integrations/catalogue", InsuranceIntelligence.Api.Integrations.IntegrationCatalogueEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/health", WhatsAppIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations, Permission.ViewSalesLeads, Permission.ViewChannels));
app.MapGet("/api/v1/integrations/whatsapp/configuration", WhatsAppConfigurationEndpoints.GetStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/configuration", WhatsAppConfigurationEndpoints.SaveConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/configuration/test", WhatsAppConfigurationEndpoints.TestConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/configuration/meta-check", WhatsAppConfigurationEndpoints.CheckWithMeta)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/onboarding", WhatsAppOnboardingEndpoints.GetMine)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/onboarding", WhatsAppOnboardingEndpoints.Submit)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapDelete("/api/v1/integrations/whatsapp/onboarding", WhatsAppOnboardingEndpoints.CancelMine)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/admin/whatsapp/onboarding", WhatsAppOnboardingEndpoints.SupportQueue)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapPost("/api/v1/admin/whatsapp/onboarding/{tenantId}/{workspaceId}/assign", WhatsAppOnboardingEndpoints.AssignNumber)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapPost("/api/v1/integrations/whatsapp/onboarding/request-code", WhatsAppOnboardingEndpoints.RequestCode)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations)).RequireRateLimiting("mfa");
app.MapPost("/api/v1/integrations/whatsapp/onboarding/verify-code", WhatsAppOnboardingEndpoints.VerifyCode)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations)).RequireRateLimiting("mfa");
app.MapPost("/api/v1/integrations/whatsapp/onboarding/register", WhatsAppOnboardingEndpoints.RegisterNumber)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations)).RequireRateLimiting("mfa");
app.MapGet("/api/v1/demo/scenarios", InsuranceIntelligence.Api.DemoScenarios.DemoScenarioEndpoints.List);
app.MapPost("/api/v1/demo/scenarios/{scenarioId}/activate", InsuranceIntelligence.Api.DemoScenarios.DemoScenarioEndpoints.Activate);
app.MapGet("/api/v1/templates", InsuranceIntelligence.Api.Templates.TemplateEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPut("/api/v1/templates/{name}/{language}", InsuranceIntelligence.Api.Templates.TemplateEndpoints.SaveDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/templates/{name}/{language}/submit", InsuranceIntelligence.Api.Templates.TemplateEndpoints.Submit)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/templates/{name}/{language}/decision", InsuranceIntelligence.Api.Templates.TemplateEndpoints.Decide)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/templates/import", InsuranceIntelligence.Api.Templates.TemplateEndpoints.Import)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/events/types", InsuranceIntelligence.Api.BankEvents.EventEndpoints.Types)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/events", InsuranceIntelligence.Api.BankEvents.EventEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/events", InsuranceIntelligence.Api.BankEvents.EventEndpoints.Record)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/events/import", InsuranceIntelligence.Api.BankEvents.EventEndpoints.Import)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/rules", InsuranceIntelligence.Api.Rules.RuleEndpoints.Overview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/rules", (InsuranceIntelligence.Api.Rules.RuleRequest request, InsuranceIntelligence.Application.Rules.RuleService rules, InsuranceIntelligence.Application.Core.IAuditWriter audit, InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions options, HttpContext context, CancellationToken token) =>
        InsuranceIntelligence.Api.Rules.RuleEndpoints.Save(null, request, rules, audit, options, context, token))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPut("/api/v1/rules/{ruleId}", (string ruleId, InsuranceIntelligence.Api.Rules.RuleRequest request, InsuranceIntelligence.Application.Rules.RuleService rules, InsuranceIntelligence.Application.Core.IAuditWriter audit, InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions options, HttpContext context, CancellationToken token) =>
        InsuranceIntelligence.Api.Rules.RuleEndpoints.Save(ruleId, request, rules, audit, options, context, token))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/rules/{ruleId}/approve", InsuranceIntelligence.Api.Rules.RuleEndpoints.Approve)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/rules/{ruleId}/pause", InsuranceIntelligence.Api.Rules.RuleEndpoints.Pause)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/rules/{ruleId}/resume", InsuranceIntelligence.Api.Rules.RuleEndpoints.Resume)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPut("/api/v1/rules-approval", InsuranceIntelligence.Api.Rules.RuleEndpoints.SaveApprovalSettings)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/rules-run", InsuranceIntelligence.Api.Rules.RuleEndpoints.Run)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/rule-outcomes", InsuranceIntelligence.Api.Rules.RuleEndpoints.Outcomes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/rule-outcomes/{outcomeId}/done", InsuranceIntelligence.Api.Rules.RuleEndpoints.MarkDone)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/demo/status", InsuranceIntelligence.Api.DemoScenarios.DemoCustomerEndpoints.Status)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/demo/customers", InsuranceIntelligence.Api.DemoScenarios.DemoCustomerEndpoints.Add)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/rule-timeline", InsuranceIntelligence.Api.DemoScenarios.DemoCustomerEndpoints.Timeline)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/rule-campaigns", InsuranceIntelligence.Api.Rules.RuleEndpoints.Campaigns)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/rule-conversions", InsuranceIntelligence.Api.Rules.RuleEndpoints.Conversions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/rule-decisions", InsuranceIntelligence.Api.Rules.RuleEndpoints.Decisions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapGet("/api/v1/workspace/brand", InsuranceIntelligence.Api.Workspaces.WorkspaceBrandEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPut("/api/v1/workspace/brand", InsuranceIntelligence.Api.Workspaces.WorkspaceBrandEndpoints.Save)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/receipts", WhatsAppIntegrationEndpoints.ListReceipts)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapDelete("/api/v1/integrations/whatsapp/configuration", WhatsAppConfigurationEndpoints.DeleteConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/messaging-mode", WhatsAppMessagingModeEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPut("/api/v1/integrations/whatsapp/messaging-mode", WhatsAppMessagingModeEndpoints.Update)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/admin/notification-routing", NotificationRoutingEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPut("/api/v1/admin/notification-routing", NotificationRoutingEndpoints.Update)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/configuration/elsewhere", WhatsAppNumberMoveEndpoints.ListElsewhere)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/configuration/move-here", WhatsAppNumberMoveEndpoints.MoveHere)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/webhook", WhatsAppIntegrationEndpoints.VerifyWebhook);
app.MapPost("/api/v1/integrations/whatsapp/webhook", WhatsAppIntegrationEndpoints.ReceiveWebhook).RequireRateLimiting("webhook");
app.MapPost("/api/v1/integrations/telegram/bots/{botId}/webhook", TelegramIntegrationEndpoints.ReceiveWebhook);
app.MapGet("/api/v1/integrations/meta/{channel}/{accountId}/webhook", MetaMessagingWebhookEndpoints.Verify);
app.MapPost("/api/v1/integrations/meta/{channel}/{accountId}/webhook", MetaMessagingWebhookEndpoints.Receive);
app.MapGet("/api/v1/integrations/x/{accountId}/webhook", XActivityWebhookEndpoints.Challenge);
app.MapPost("/api/v1/integrations/x/{accountId}/webhook", XActivityWebhookEndpoints.Receive);

// Generic connection settings surface (blueprint §3.4), the /app/admin/connections screen's
// backend. {connectionType} is "sms", "email" or "translation"; adding a type is a new
// ConnectionType enum value plus IConnectionValidator implementations, no new routes.
app.MapPost("/api/v1/integrations/connections/translation/try", InsuranceIntelligence.Api.Agents.TranslationStatusEndpoints.TryConnection)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/connections/{connectionType}/providers", ConnectionEndpoints.ListProviders)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/connections/{connectionType}", ConnectionEndpoints.GetStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/connections/{connectionType}", ConnectionEndpoints.SaveConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/connections/{connectionType}/test", ConnectionEndpoints.TestConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapDelete("/api/v1/integrations/connections/{connectionType}", ConnectionEndpoints.DeleteConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));

// Agent behaviour configuration (blueprint §6.3), a tenant's voice/scope customization per
// agent, never its authority. Every save is a new version; nothing is overwritten in place.
app.MapGet("/api/v1/agents/{agentKey}/behaviour", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.GetCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapGet("/api/v1/agents/{agentKey}/behaviour/versions", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/agents/{agentKey}/behaviour", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.SaveVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/agents/{agentKey}/behaviour/test", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.TestBehaviour)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));

// Whether the customer assistant can really answer in Luganda and Kiswahili right now.
app.MapGet("/api/v1/agents/customer-assistant/translation", InsuranceIntelligence.Api.Agents.TranslationStatusEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/agents/customer-assistant/translation/test", InsuranceIntelligence.Api.Agents.TranslationStatusEndpoints.Test)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapGet("/api/v1/admin/assistant-languages", InsuranceIntelligence.Api.Agents.AssistantLanguageEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPut("/api/v1/admin/assistant-languages", InsuranceIntelligence.Api.Agents.AssistantLanguageEndpoints.Update)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));

// Tenant agent policy (blueprint §6.4, WO 3.4), real enforcement-source data, not a hardcoded array.
app.MapGet("/api/v1/admin/tenant-policy", InsuranceIntelligence.Api.Administration.TenantPolicyEndpoints.GetPolicy)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));

// SMS delivery-status webhook (blueprint §5.4), unauthenticated by header (the gateway's payload
// carries no signature), gated by the unguessable path secret instead. No PermissionEndpointFilter:
// the caller is an external SMS gateway, not an authenticated platform user.
app.MapPost("/api/v1/integrations/sms/status/{pathSecret}", SmsIntegrationEndpoints.ReceiveDeliveryStatus);
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}", WhatsAppIntegrationEndpoints.GetThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}/control", ConversationControlEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications, Permission.ViewSalesLeads, Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPut("/api/v1/channels/whatsapp/threads/{threadId}/control", ConversationControlEndpoints.Set)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}/prospect", SalesLeadEndpoints.GetByThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/admin/sales-leads/merge-duplicates", SalesLeadEndpoints.MergeDuplicates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/channels/whatsapp/threads/{threadId}/messages", WhatsAppIntegrationEndpoints.SendMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/communications/send-email", CommunicationEndpoints.SendEmail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/communications/send-sms", CommunicationEndpoints.SendSms)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/communications/send-teams-notification", CommunicationEndpoints.SendTeamsNotification)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/communications/history", CommunicationEndpoints.ListHistory)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/communications", CommunicationEndpoints.ListForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapCustomerEndpoints();
app.MapPolicyEndpoints();
app.MapDistributionEndpoints();

app.Run();

static string RequireSetting(string? value, string settingName)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{settingName} must be configured when INSURANCEINTELLIGENCE_AUTH_MODE=Entra.");
    }

    return value;
}

public partial class Program;

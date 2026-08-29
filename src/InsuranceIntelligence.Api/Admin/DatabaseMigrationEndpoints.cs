using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Admin;

/// <summary>
/// Admin-triggered "apply pending EF Core migrations" operation. Uses the API's own
/// already-authorized SQL connection (managed identity in deployed environments) rather
/// than requiring anyone to be granted direct database access. Purely additive/idempotent
/// (EF Core skips already-applied migrations) — never destructive. Gated the same way as
/// demo data seeding: Admin permission, and blocked in protected runtime profiles as an
/// extra safety margin even though applying migrations is itself safe in Production too —
/// the intent of this endpoint is operational convenience for non-protected environments.
/// </summary>
public static class DatabaseMigrationEndpoints
{
    /// <summary>
    /// Migrations whose target tables were already present in the deployed dev database
    /// before __EFMigrationsHistory tracking began (evidenced by Diagnose's table list) —
    /// reconciled here by recording them as applied WITHOUT re-running their SQL, so
    /// ApplyPending can then correctly apply only the migrations that are genuinely new.
    /// </summary>
    private static readonly string[] AlreadyAppliedButUntracked =
    [
        "202605240001_InitialPersistence",
        "20260624083822_AddTeamsNotifications",
        "20260624164818_Phase4RelationalDomain",
        "202607020001_AddCorePortPermissionTables"
    ];

    private const string EfCoreProductVersion = "8.0.11";

    public static async Task<IResult> ReconcileHistory(
        InsuranceIntelligenceDbContext dbContext,
        InfrastructureOptions options,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        if (AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile))
        {
            return Results.Problem(
                $"Not available under the '{runtimeProfile}' runtime profile.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        var alreadyApplied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        var inserted = new List<string>();

        foreach (var migrationId in AlreadyAppliedButUntracked)
        {
            if (alreadyApplied.Contains(migrationId))
            {
                continue;
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({migrationId}, {EfCoreProductVersion})",
                cancellationToken);
            inserted.Add(migrationId);
        }

        return Results.Ok(new { insertedIntoHistory = inserted });
    }

    /// <summary>
    /// The deployed dev database was found to be schema-drifted at table granularity, not
    /// just migration granularity: some tables from a migration exist while sibling tables
    /// from the SAME migration do not (evidence of a partial manual/EnsureCreated history
    /// predating consistent migration tracking). A blanket Database.Migrate() therefore
    /// fails with "already exists" the moment it hits any already-present table within a
    /// still-pending migration. This runs the exact DDL for the three genuinely pending
    /// migrations (AddAgentWorkflowControlPlane's remaining tables, AddClaimReviewRequests,
    /// AgentWorkflowVersionLineage), each guarded with an existence check so it is safe to
    /// run against any starting state, then records all three as applied in history.
    /// </summary>
    private const string IdempotentReconcileSql = """
        IF OBJECT_ID('AgentWorkflowDefinitions', 'U') IS NULL
        BEGIN
            CREATE TABLE AgentWorkflowDefinitions (
                DefinitionId uniqueidentifier NOT NULL CONSTRAINT PK_AgentWorkflowDefinitions PRIMARY KEY,
                WorkflowKey nvarchar(160) NOT NULL,
                Status nvarchar(40) NOT NULL,
                TenantId nvarchar(120) NOT NULL,
                WorkspaceId nvarchar(120) NOT NULL,
                PayloadJson nvarchar(max) NOT NULL,
                UpdatedAt datetimeoffset NOT NULL
            );
            CREATE INDEX IX_AgentWorkflowDefinitions_TenantId_WorkspaceId ON AgentWorkflowDefinitions (TenantId, WorkspaceId);
        END;

        IF OBJECT_ID('AgentWorkflowRuns', 'U') IS NULL
        BEGIN
            CREATE TABLE AgentWorkflowRuns (
                RunId uniqueidentifier NOT NULL CONSTRAINT PK_AgentWorkflowRuns PRIMARY KEY,
                WorkflowKey nvarchar(160) NOT NULL,
                Status nvarchar(40) NOT NULL,
                BusinessReferenceId nvarchar(240) NOT NULL,
                TenantId nvarchar(120) NOT NULL,
                WorkspaceId nvarchar(120) NOT NULL,
                PayloadJson nvarchar(max) NOT NULL,
                UpdatedAt datetimeoffset NOT NULL
            );
            CREATE INDEX IX_AgentWorkflowRuns_TenantId_WorkspaceId ON AgentWorkflowRuns (TenantId, WorkspaceId);
            CREATE INDEX IX_AgentWorkflowRuns_TenantId_WorkspaceId_WorkflowKey_Status ON AgentWorkflowRuns (TenantId, WorkspaceId, WorkflowKey, Status);
        END;

        IF OBJECT_ID('AgentWorkItems', 'U') IS NULL
        BEGIN
            CREATE TABLE AgentWorkItems (
                WorkItemId uniqueidentifier NOT NULL CONSTRAINT PK_AgentWorkItems PRIMARY KEY,
                RunId uniqueidentifier NOT NULL,
                WorkflowKey nvarchar(160) NOT NULL,
                NodeKey nvarchar(160) NOT NULL,
                Status nvarchar(40) NOT NULL,
                TenantId nvarchar(120) NOT NULL,
                WorkspaceId nvarchar(120) NOT NULL,
                PayloadJson nvarchar(max) NOT NULL,
                UpdatedAt datetimeoffset NOT NULL
            );
            CREATE INDEX IX_AgentWorkItems_TenantId_WorkspaceId ON AgentWorkItems (TenantId, WorkspaceId);
            CREATE INDEX IX_AgentWorkItems_TenantId_WorkspaceId_RunId_NodeKey ON AgentWorkItems (TenantId, WorkspaceId, RunId, NodeKey);
        END;

        IF OBJECT_ID('ClaimReviewRequests', 'U') IS NULL
        BEGIN
            CREATE TABLE ClaimReviewRequests (
                ReviewRequestId uniqueidentifier NOT NULL CONSTRAINT PK_ClaimReviewRequests PRIMARY KEY,
                CaseId uniqueidentifier NOT NULL,
                TenantId nvarchar(120) NOT NULL,
                WorkspaceId nvarchar(120) NOT NULL,
                PayloadJson nvarchar(max) NOT NULL,
                UpdatedAt datetimeoffset NOT NULL
            );
            CREATE INDEX IX_ClaimReviewRequests_TenantId_WorkspaceId ON ClaimReviewRequests (TenantId, WorkspaceId);
            CREATE INDEX IX_ClaimReviewRequests_TenantId_WorkspaceId_CaseId ON ClaimReviewRequests (TenantId, WorkspaceId, CaseId);
        END;

        IF COL_LENGTH('AgentWorkflowDefinitions', 'Version') IS NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey' AND object_id = OBJECT_ID('AgentWorkflowDefinitions'))
                DROP INDEX IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey ON AgentWorkflowDefinitions;

            ALTER TABLE AgentWorkflowDefinitions ADD Version int NOT NULL CONSTRAINT DF_AgentWorkflowDefinitions_Version DEFAULT 1;

            UPDATE AgentWorkflowDefinitions
            SET Version = COALESCE(
                TRY_CAST(JSON_VALUE(PayloadJson, '$.version') AS int),
                TRY_CAST(JSON_VALUE(PayloadJson, '$.Version') AS int),
                1);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey_Version' AND object_id = OBJECT_ID('AgentWorkflowDefinitions'))
                CREATE UNIQUE INDEX IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey_Version ON AgentWorkflowDefinitions (TenantId, WorkspaceId, WorkflowKey, Version);
        END;

        IF COL_LENGTH('AgentWorkflowRuns', 'DefinitionId') IS NULL
        BEGIN
            ALTER TABLE AgentWorkflowRuns ADD DefinitionId uniqueidentifier NOT NULL CONSTRAINT DF_AgentWorkflowRuns_DefinitionId DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE AgentWorkflowRuns ADD WorkflowVersion int NOT NULL CONSTRAINT DF_AgentWorkflowRuns_WorkflowVersion DEFAULT 1;

            UPDATE AgentWorkflowRuns
            SET DefinitionId = COALESCE(
                    TRY_CONVERT(uniqueidentifier, JSON_VALUE(PayloadJson, '$.definitionId')),
                    TRY_CONVERT(uniqueidentifier, JSON_VALUE(PayloadJson, '$.DefinitionId')),
                    DefinitionId),
                WorkflowVersion = COALESCE(
                    TRY_CAST(JSON_VALUE(PayloadJson, '$.workflowVersion') AS int),
                    TRY_CAST(JSON_VALUE(PayloadJson, '$.WorkflowVersion') AS int),
                    1);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AgentWorkflowRuns_TenantId_WorkspaceId_DefinitionId' AND object_id = OBJECT_ID('AgentWorkflowRuns'))
                CREATE INDEX IX_AgentWorkflowRuns_TenantId_WorkspaceId_DefinitionId ON AgentWorkflowRuns (TenantId, WorkspaceId, DefinitionId);
        END;
        """;

    private static readonly string[] RemainingMigrationsCoveredByReconcile =
    [
        "20260716123910_AddAgentWorkflowControlPlane",
        "20260803142731_AddClaimReviewRequests",
        "20260803214500_AgentWorkflowVersionLineage"
    ];

    public static async Task<IResult> ForceReconcileSchema(
        InsuranceIntelligenceDbContext dbContext,
        InfrastructureOptions options,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        if (AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile))
        {
            return Results.Problem(
                $"Not available under the '{runtimeProfile}' runtime profile.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        await dbContext.Database.ExecuteSqlRawAsync(IdempotentReconcileSql, cancellationToken);

        var alreadyApplied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        var inserted = new List<string>();
        foreach (var migrationId in RemainingMigrationsCoveredByReconcile)
        {
            if (alreadyApplied.Contains(migrationId))
            {
                continue;
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({migrationId}, {EfCoreProductVersion})",
                cancellationToken);
            inserted.Add(migrationId);
        }

        var stillPending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

        return Results.Ok(new { insertedIntoHistory = inserted, stillPending });
    }

    public static async Task<IResult> ApplyPending(
        InsuranceIntelligenceDbContext dbContext,
        InfrastructureOptions options,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        if (AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile))
        {
            return Results.Problem(
                $"Migration application via this endpoint is not available under the '{runtimeProfile}' runtime profile.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        var beforeApplied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        await dbContext.Database.MigrateAsync(cancellationToken);

        var afterApplied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();

        return Results.Ok(new
        {
            appliedBefore = beforeApplied,
            wasPending = pending,
            appliedAfter = afterApplied
        });
    }

    /// <summary>
    /// Diagnostic-only: lists every user table currently in the database and the rows in
    /// __EFMigrationsHistory, so drift between actual schema and recorded migrations can be
    /// diagnosed before deciding how to reconcile it. Same gating as ApplyPending.
    /// </summary>
    public static async Task<IResult> Diagnose(
        InsuranceIntelligenceDbContext dbContext,
        InfrastructureOptions options,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        if (AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile))
        {
            return Results.Problem(
                $"Not available under the '{runtimeProfile}' runtime profile.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            var tables = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            var history = new List<string>();
            var historyTableExists = tables.Contains("__EFMigrationsHistory");
            if (historyTableExists)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    history.Add(reader.GetString(0));
                }
            }

            return Results.Ok(new { tables, historyTableExists, appliedMigrationHistory = history });
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}

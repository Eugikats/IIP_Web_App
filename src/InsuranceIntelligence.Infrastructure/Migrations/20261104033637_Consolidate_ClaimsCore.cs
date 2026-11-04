using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Consolidate_ClaimsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                table: "WhatsAppMessages",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRecipientId",
                table: "WhatsAppMessages",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerKey",
                table: "Policies",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LookupVersion",
                table: "Policies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PolicyKey",
                table: "Policies",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionType",
                table: "IntegrationConnections",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountId",
                table: "IntegrationConnections",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailKey",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LookupVersion",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PhoneKey",
                table: "Customers",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailKey",
                table: "Cases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LookupVersion",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PeriodKey",
                table: "Cases",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneKey",
                table: "Cases",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyKey",
                table: "Cases",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusName",
                table: "Cases",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamKey",
                table: "Cases",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodKey",
                table: "CaseDocuments",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "CaseDocuments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "AgentActionLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CostMicroUsd",
                table: "AgentActionLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodKey",
                table: "AgentActionLogs",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "AgentActionLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordKind",
                table: "AgentActionLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Action");

            migrationBuilder.CreateTable(
                name: "AuthenticityReports",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerceptualHash = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticityReports", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Queue = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NotBefore = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeasedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "ChannelIdentityLinks",
                columns: table => new
                {
                    LinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ExternalRecipientId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelIdentityLinks", x => x.LinkId);
                });

            migrationBuilder.CreateTable(
                name: "ChannelMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ExternalRecipientId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAssessments",
                columns: table => new
                {
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAssessments", x => x.AssessmentId);
                });

            migrationBuilder.CreateTable(
                name: "ClaimsPackDispatches",
                columns: table => new
                {
                    DispatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimsPackDispatches", x => x.DispatchId);
                });

            migrationBuilder.CreateTable(
                name: "ClaimsPackShareLinks",
                columns: table => new
                {
                    LinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimsPackShareLinks", x => x.LinkId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerFollowUps",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    RecipientId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    OriginatingMessageId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClaimVersion = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    SafeFailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFollowUps", x => x.TaskId);
                });

            migrationBuilder.CreateTable(
                name: "DeletionRecords",
                columns: table => new
                {
                    DeletionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletionRecords", x => x.DeletionId);
                });

            migrationBuilder.CreateTable(
                name: "FieldProposals",
                columns: table => new
                {
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldProposals", x => x.ProposalId);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "LocalCredentials",
                columns: table => new
                {
                    LocalCredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCredentials", x => x.LocalCredentialId);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResets",
                columns: table => new
                {
                    PasswordResetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResets", x => x.PasswordResetId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentIntents",
                columns: table => new
                {
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntents", x => x.PaymentIntentId);
                });

            migrationBuilder.CreateTable(
                name: "PortalIdentities",
                columns: table => new
                {
                    PortalIdentityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalIdentities", x => x.PortalIdentityId);
                });

            migrationBuilder.CreateTable(
                name: "SignupRequests",
                columns: table => new
                {
                    SignupRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupRequests", x => x.SignupRequestId);
                });

            migrationBuilder.CreateTable(
                name: "SlotPdfs",
                columns: table => new
                {
                    SlotPdfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotPdfs", x => x.SlotPdfId);
                });

            migrationBuilder.CreateTable(
                name: "StaffInvitations",
                columns: table => new
                {
                    StaffInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffInvitations", x => x.StaffInvitationId);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "TenantSettings",
                columns: table => new
                {
                    TenantSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettings", x => x.TenantSettingsId);
                });

            migrationBuilder.CreateTable(
                name: "UploadParts",
                columns: table => new
                {
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartIndex = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadParts", x => x.PartId);
                });

            migrationBuilder.CreateTable(
                name: "UploadSessions",
                columns: table => new
                {
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadSessions", x => x.UploadId);
                });

            migrationBuilder.CreateTable(
                name: "WordingDrafts",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordingDrafts", x => x.DraftId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_WorkspaceId_AccountId_ExternalRecipientId_CreatedAt",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "WorkspaceId", "AccountId", "ExternalRecipientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_LookupVersion",
                table: "Policies",
                column: "LookupVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_TenantId_WorkspaceId_CustomerKey",
                table: "Policies",
                columns: new[] { "TenantId", "WorkspaceId", "CustomerKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_TenantId_WorkspaceId_PolicyKey",
                table: "Policies",
                columns: new[] { "TenantId", "WorkspaceId", "PolicyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnections_ConnectionType_ExternalAccountId",
                table: "IntegrationConnections",
                columns: new[] { "ConnectionType", "ExternalAccountId" },
                unique: true,
                filter: "[ExternalAccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LookupVersion",
                table: "Customers",
                column: "LookupVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_WorkspaceId_EmailKey",
                table: "Customers",
                columns: new[] { "TenantId", "WorkspaceId", "EmailKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_WorkspaceId_PhoneKey",
                table: "Customers",
                columns: new[] { "TenantId", "WorkspaceId", "PhoneKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_LookupVersion",
                table: "Cases",
                column: "LookupVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenantId_WorkspaceId_EmailKey",
                table: "Cases",
                columns: new[] { "TenantId", "WorkspaceId", "EmailKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenantId_WorkspaceId_PeriodKey",
                table: "Cases",
                columns: new[] { "TenantId", "WorkspaceId", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenantId_WorkspaceId_PhoneKey",
                table: "Cases",
                columns: new[] { "TenantId", "WorkspaceId", "PhoneKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenantId_WorkspaceId_PolicyKey",
                table: "Cases",
                columns: new[] { "TenantId", "WorkspaceId", "PolicyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenantId_WorkspaceId_StatusName",
                table: "Cases",
                columns: new[] { "TenantId", "WorkspaceId", "StatusName" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_TenantId_WorkspaceId_TeamKey",
                table: "Cases",
                columns: new[] { "TenantId", "WorkspaceId", "TeamKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_TenantId_WorkspaceId_PeriodKey",
                table: "CaseDocuments",
                columns: new[] { "TenantId", "WorkspaceId", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentActionLogs_TenantId_WorkspaceId_PeriodKey",
                table: "AgentActionLogs",
                columns: new[] { "TenantId", "WorkspaceId", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticityReports_TenantId_WorkspaceId",
                table: "AuthenticityReports",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticityReports_TenantId_WorkspaceId_CaseId",
                table: "AuthenticityReports",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticityReports_TenantId_WorkspaceId_PerceptualHash",
                table: "AuthenticityReports",
                columns: new[] { "TenantId", "WorkspaceId", "PerceptualHash" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Queue_Status_NotBefore",
                table: "BackgroundJobs",
                columns: new[] { "Queue", "Status", "NotBefore" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_TenantId_WorkspaceId",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_TenantId_WorkspaceId_CaseId",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_TenantId_WorkspaceId_Queue_Type_Key",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "WorkspaceId", "Queue", "Type", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelIdentityLinks_TenantId_WorkspaceId",
                table: "ChannelIdentityLinks",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelIdentityLinks_TenantId_WorkspaceId_Channel_AccountId_ExternalRecipientId",
                table: "ChannelIdentityLinks",
                columns: new[] { "TenantId", "WorkspaceId", "Channel", "AccountId", "ExternalRecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelIdentityLinks_TenantId_WorkspaceId_CustomerId_IsActive",
                table: "ChannelIdentityLinks",
                columns: new[] { "TenantId", "WorkspaceId", "CustomerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_TenantId_WorkspaceId",
                table: "ChannelMessages",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_TenantId_WorkspaceId_Channel_AccountId_ExternalRecipientId_CreatedAt",
                table: "ChannelMessages",
                columns: new[] { "TenantId", "WorkspaceId", "Channel", "AccountId", "ExternalRecipientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_TenantId_WorkspaceId_Channel_AccountId_IdempotencyKey",
                table: "ChannelMessages",
                columns: new[] { "TenantId", "WorkspaceId", "Channel", "AccountId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_TenantId_WorkspaceId_Channel_AccountId_ProviderMessageId",
                table: "ChannelMessages",
                columns: new[] { "TenantId", "WorkspaceId", "Channel", "AccountId", "ProviderMessageId" },
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_TenantId_WorkspaceId_Channel_AccountId_ThreadId_CreatedAt",
                table: "ChannelMessages",
                columns: new[] { "TenantId", "WorkspaceId", "Channel", "AccountId", "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAssessments_TenantId_WorkspaceId",
                table: "ClaimAssessments",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAssessments_TenantId_WorkspaceId_CaseId_GeneratedAt",
                table: "ClaimAssessments",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimsPackDispatches_TenantId_WorkspaceId",
                table: "ClaimsPackDispatches",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimsPackDispatches_TenantId_WorkspaceId_CaseId",
                table: "ClaimsPackDispatches",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimsPackShareLinks_TenantId_WorkspaceId",
                table: "ClaimsPackShareLinks",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimsPackShareLinks_TokenHash",
                table: "ClaimsPackShareLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUps_TenantId_WorkspaceId",
                table: "CustomerFollowUps",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUps_TenantId_WorkspaceId_Channel_AccountId_RecipientId_Status",
                table: "CustomerFollowUps",
                columns: new[] { "TenantId", "WorkspaceId", "Channel", "AccountId", "RecipientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUps_TenantId_WorkspaceId_OriginatingMessageId",
                table: "CustomerFollowUps",
                columns: new[] { "TenantId", "WorkspaceId", "OriginatingMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFollowUps_TenantId_WorkspaceId_Status_DueAtUtc",
                table: "CustomerFollowUps",
                columns: new[] { "TenantId", "WorkspaceId", "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRecords_TenantId_WorkspaceId",
                table: "DeletionRecords",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRecords_TenantId_WorkspaceId_AtUtc",
                table: "DeletionRecords",
                columns: new[] { "TenantId", "WorkspaceId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldProposals_TenantId_WorkspaceId",
                table: "FieldProposals",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldProposals_TenantId_WorkspaceId_CaseId",
                table: "FieldProposals",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_WorkspaceId",
                table: "Invoices",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_LocalCredentials_TenantId_WorkspaceId",
                table: "LocalCredentials",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResets_TenantId_WorkspaceId",
                table: "PasswordResets",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status_CreatedAtUtc",
                table: "PaymentIntents",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_TenantId_WorkspaceId",
                table: "PaymentIntents",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalIdentities_TenantId_WorkspaceId",
                table: "PortalIdentities",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_TenantId_WorkspaceId",
                table: "SignupRequests",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SlotPdfs_TenantId_WorkspaceId",
                table: "SlotPdfs",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SlotPdfs_TenantId_WorkspaceId_CaseId",
                table: "SlotPdfs",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_TenantId_WorkspaceId",
                table: "StaffInvitations",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PeriodEndUtc",
                table: "Subscriptions",
                column: "PeriodEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_WorkspaceId",
                table: "Subscriptions",
                columns: new[] { "TenantId", "WorkspaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_TenantId_WorkspaceId",
                table: "TenantSettings",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadParts_TenantId_WorkspaceId",
                table: "UploadParts",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadParts_TenantId_WorkspaceId_UploadId_PartIndex",
                table: "UploadParts",
                columns: new[] { "TenantId", "WorkspaceId", "UploadId", "PartIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadSessions_TenantId_WorkspaceId",
                table: "UploadSessions",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadSessions_TenantId_WorkspaceId_CaseId_Fingerprint",
                table: "UploadSessions",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_WordingDrafts_TenantId_WorkspaceId",
                table: "WordingDrafts",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_WordingDrafts_TenantId_WorkspaceId_ProductId",
                table: "WordingDrafts",
                columns: new[] { "TenantId", "WorkspaceId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticityReports");

            migrationBuilder.DropTable(
                name: "BackgroundJobs");

            migrationBuilder.DropTable(
                name: "ChannelIdentityLinks");

            migrationBuilder.DropTable(
                name: "ChannelMessages");

            migrationBuilder.DropTable(
                name: "ClaimAssessments");

            migrationBuilder.DropTable(
                name: "ClaimsPackDispatches");

            migrationBuilder.DropTable(
                name: "ClaimsPackShareLinks");

            migrationBuilder.DropTable(
                name: "CustomerFollowUps");

            migrationBuilder.DropTable(
                name: "DeletionRecords");

            migrationBuilder.DropTable(
                name: "FieldProposals");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "LocalCredentials");

            migrationBuilder.DropTable(
                name: "PasswordResets");

            migrationBuilder.DropTable(
                name: "PaymentIntents");

            migrationBuilder.DropTable(
                name: "PortalIdentities");

            migrationBuilder.DropTable(
                name: "SignupRequests");

            migrationBuilder.DropTable(
                name: "SlotPdfs");

            migrationBuilder.DropTable(
                name: "StaffInvitations");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TenantSettings");

            migrationBuilder.DropTable(
                name: "UploadParts");

            migrationBuilder.DropTable(
                name: "UploadSessions");

            migrationBuilder.DropTable(
                name: "WordingDrafts");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_TenantId_WorkspaceId_AccountId_ExternalRecipientId_CreatedAt",
                table: "WhatsAppMessages");

            migrationBuilder.DropIndex(
                name: "IX_Policies_LookupVersion",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_Policies_TenantId_WorkspaceId_CustomerKey",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_Policies_TenantId_WorkspaceId_PolicyKey",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationConnections_ConnectionType_ExternalAccountId",
                table: "IntegrationConnections");

            migrationBuilder.DropIndex(
                name: "IX_Customers_LookupVersion",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_WorkspaceId_EmailKey",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_WorkspaceId_PhoneKey",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Cases_LookupVersion",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_TenantId_WorkspaceId_EmailKey",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_TenantId_WorkspaceId_PeriodKey",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_TenantId_WorkspaceId_PhoneKey",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_TenantId_WorkspaceId_PolicyKey",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_TenantId_WorkspaceId_StatusName",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_Cases_TenantId_WorkspaceId_TeamKey",
                table: "Cases");

            migrationBuilder.DropIndex(
                name: "IX_CaseDocuments_TenantId_WorkspaceId_PeriodKey",
                table: "CaseDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AgentActionLogs_TenantId_WorkspaceId_PeriodKey",
                table: "AgentActionLogs");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "ExternalRecipientId",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "CustomerKey",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "LookupVersion",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "PolicyKey",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "ConnectionType",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "EmailKey",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LookupVersion",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PhoneKey",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "EmailKey",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LookupVersion",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PeriodKey",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PhoneKey",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PolicyKey",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "StatusName",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "TeamKey",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "PeriodKey",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "AgentActionLogs");

            migrationBuilder.DropColumn(
                name: "CostMicroUsd",
                table: "AgentActionLogs");

            migrationBuilder.DropColumn(
                name: "PeriodKey",
                table: "AgentActionLogs");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "AgentActionLogs");

            migrationBuilder.DropColumn(
                name: "RecordKind",
                table: "AgentActionLogs");
        }
    }
}

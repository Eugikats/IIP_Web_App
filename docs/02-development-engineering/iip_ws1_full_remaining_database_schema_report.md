# Workstream 1 — Full Remaining Database Schema

**Deliverable type:** Real C# schema source overlay and hand-authored EF Core migration classes  
**Repository target:** existing `InsuranceIntelligenceDbContext` foundation generated in `/mnt/data/iip_foundation_implementation`  
**Status:** Source produced; build and EF execution blocked by sandbox runtime

## Scope implemented

This workstream adds schema-only coverage for the remaining IIP data model implied by the implementation-ready service specifications. It does not implement repository behavior classes.

## Produced source package

- `src/InsuranceIntelligence.Infrastructure/Persistence/Entities/RemainingDomainEntities.cs`
- `src/InsuranceIntelligence.Infrastructure/Persistence/RemainingDomainModelBuilderExtensions.cs`
- `src/InsuranceIntelligence.Infrastructure/Migrations/202607020100_RemainingSchemaClaimsWorkflow.cs`
- `src/InsuranceIntelligence.Infrastructure/Migrations/202607020110_RemainingSchemaApprovalsDocuments.cs`
- `src/InsuranceIntelligence.Infrastructure/Migrations/202607020120_RemainingSchemaChannelsCustomersCommunications.cs`
- `src/InsuranceIntelligence.Infrastructure/Migrations/202607020130_RemainingSchemaKnowledgeReportingEmailPolicySession.cs`
- `tests/InsuranceIntelligence.Tests/Infrastructure/CommandCenterSessionStateSchemaTests.cs`

## Validation status

I attempted to validate the .NET environment in this sandbox.

```text
dotnet --info
bash: line 1: dotnet: command not found
```

Because the .NET SDK is not installed in this runtime, I could not truthfully run:

```text
dotnet ef migrations add ...
dotnet ef database update
dotnet build
dotnet test
```

These commands must be run in the real repository/IDE environment after copying the source overlay. The migration classes are SQL Server-only and intentionally do not introduce SQLite support.

## Integration note

The generated `RemainingDomainModelBuilderExtensions.ApplyRemainingDomainModelConfiguration()` extension must be called from the real `InsuranceIntelligenceDbContext.OnModelCreating` if the team wants fluent configuration applied in addition to the raw-SQL migration classes. The schema migrations themselves create the tables and indexes.

## Entities by domain

| Domain | Entities/Tables | Source documents | Repositories still to build |
|---|---|---|---|
| Claims intake/workflow | ClaimIntakeRecords, ClaimWorkflowTransitionHistory, ClaimNumberSequences | Claims, Claim Intake/Workflow Transition | `EfClaimIntakeRepository`, `EfClaimWorkflowTransitionRepository`, claim-number sequence allocator |
| Approvals | ApprovalTasks, ApprovalDecisions | Approval Query, Approval Decision, Notification Send | `EfApprovalQueryRepository`, `EfApprovalDecisionRepository` |
| Documents/extraction/review | DocumentUploadRecords, DocumentExtractionJobs, ExtractedFieldRecords, ExtractedFieldCorrectionHistory, ExtractedFieldStatusHistory, RequiredDocumentRules | Document Upload, Extraction Workflow, Review, Completeness | `EfDocumentUploadRepository`, `EfDocumentExtractionRepository`, `EfDocumentReviewRepository`, `EfCompletenessRuleRepository` |
| Channels/customers/sales | ChannelConversations, ChannelMessages, CustomerProfiles, SalesLeads | Channels, Customers, Sales Leads | `EfChannelsQueryRepository`, `EfCustomerQueryRepository`, `EfSalesLeadsQueryRepository` |
| Write-back | IntegrationWriteBackHistory | Integration Write-Back | `EfWriteBackHistoryRepository` |
| Notifications | NotificationDrafts | Notification Send | `EfNotificationDraftSendRepository`; if a draft table already exists in the real repo, convert this migration to an ALTER TABLE migration instead of creating a duplicate table. |
| Knowledge | KnowledgeDocuments, KnowledgeChunks | Knowledge Ingestion | `EfKnowledgeDocumentRepository` extension, chunk metadata repository |
| Reporting | ReportSnapshots, ReportExports | Reporting Service | `EfReportSnapshotRepository`, `EfReportingReadRepository` |
| Email ingestion | MailboxTenantMappings, EmailIngestionCheckpoints, InboundEmailProcessingRecords | Email Ingestion | `EfEmailMailboxMappingRepository`, `EfEmailCheckpointRepository`, inbound dispatch repository |
| Tenant policy | TenantAgentCapabilityPolicies | Tenant Agent Capability Policy | `EfTenantAgentCapabilityPolicyRepository` |
| Session state | CommandCenterSessionStates, CommandCenterSessionTaskReferences | Session/Context/Multi-Agent Architecture | `EfCommandCenterSessionStateRepository` |

## Completion report

Files created/modified are included in the linked Workstream 1 source zip. No repository behavior classes were implemented. No mock, simulated, or in-memory production path was added. Every table is tenant/workspace-scoped and indexed. Empty data remains empty; no seed/fake operational data is introduced.

**Next implementation step:** run the source overlay in the real repository, convert raw migration SQL to provider-reviewed EF migration operations if desired, run `dotnet ef database update` against a non-production SQL Server database, then start repository build-out in the established order: Claims/SLA first.

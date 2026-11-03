# Phase B3 report: worker and API compatibility

Date: 2026-11-03. Status: PHASE REPORT COMPLETE; GLOBAL GATE IN PROGRESS. `main` remains at `80155dc`.

## Source and branch

- Repository: `Otic-Group/IIP_Web_App`.
- Execution clone: `C:/work/iip-consolidation-verified`.
- Phase branch: `integ/02-v2-social`, with B3 evidence stacked after B2 under D-015.
- Live API revision: `4818bb1`.
- Live worker revision: `1872a14`, deployed 2026-09-14 according to the consolidation plan.
- Current Functions source at the API revision: `0d8af21` (`feat(saas): no single-tenant fallbacks in multi-tenant mode`).
- `src/InsuranceIntelligence.Functions` is byte-identical between `4818bb1` and `80155dc` (`git diff --quiet` exited 0). The Functions build at `main` therefore verifies the same worker source as the live API revision.

## Application and Infrastructure contracts used by the worker

| Worker dependency | Layer | Change from worker revision `1872a14` to API revision `4818bb1` | Compatibility result |
|---|---|---|---|
| `ITenantContextAccessor`, `ITenantContextSetter`, `TenantContextAccessor`, and `TenantScope` | Application and Domain | Queue envelope retains tenant and workspace scope. | Core dispatch path compatible when correctly scoped. |
| `IPermissionReadRepository`, `IPermissionService`, and `IAuditWriter` | Application | Permission read interface is unchanged; current permission implementation caches reads for the request. No breaking worker-facing contract change was found. | Compatible for existing worker operations. |
| `IAgentWorkflowService`, `IAgentWorkflowNodeExecutor`, `AgentWorkflowNodeDispatch`, `AgentWorkflowRun`, and workflow node contracts | Application and Domain | Dispatch shape and execution contracts are unchanged. | Compatible for the existing workflow-node message type. |
| `ICaseDocumentRepository` | Application | Still supplied to the workflow node executor; no breaking contract change found. | Compatible for the existing document node path. |
| `QueueMessageEnvelope` | Application/worker contract | Fields remain `QueueName`, `MessageType`, `TenantId`, `WorkspaceId`, `CorrelationId`, and `Payload`. API dispatch type remains `agent-workflow.node.execute.v1`. | Compatible for this message. |
| `IMessageQueueDispatcher` and `WorkflowQueueNames` | Application | Queue dispatch envelope and queue names used by the worker remain compatible. | Compatible for existing queue types. |
| `IMailboxIngestionService` | Application | Interface is unchanged. The current mailbox timer skips its single-tenant work when `MultiTenant` is enabled; the deployed worker does not contain that guard. | Not SaaS-safe with the deployed worker. |
| `IRetentionEnforcementService` and `ILegalHoldRepository` | Application | Service interface remains stable, but current implementation checks legal holds before deletion. The deployed worker's older implementation has no legal-hold check. | Not compatible for legal-hold enforcement. |
| `IWorkspaceDirectory` and `ScheduledWorkspaces` | Application and Functions | Added after `1872a14`; current timers enumerate active workspaces and scope work per workspace in multi-tenant mode. | Absent from deployed worker; SaaS scheduled work is incomplete. |
| `ICaseFollowThroughService` | Application | Added with `CaseFollowThroughTimerFunction`; no equivalent timer exists in `1872a14`. | Absent from deployed worker; scheduled follow-through is incomplete. |
| `ITeamsNotificationRepository` and `ITeamsNotificationProvider` | Application and Infrastructure | Both existed in the deployed worker. Their dispatch path is present. | Existing Teams notifications remain supported, subject to notification type compatibility below. |
| `INotificationEmitter` and `IMissingDocumentRequestService` | Application | Current Functions registration adds these services. | Newer notification emission and missing-document request workflows are absent from the deployed worker source. |
| `TeamsNotificationType` and notification payload | Domain and Application | API adds `LeadCreated`. The worker's current parser uses `Enum.TryParse`; an older worker may fail an unrecognized notification type. | Forward-compatibility gap for new notification types. |
| `InfrastructureOptions`, `InfrastructureRegistration.BindOptions`, and `ProviderModeSupport` | Infrastructure | Options are additive, including `MultiTenant` and newer provider/runtime settings. | Configuration can bind, but old worker logic does not implement the newer SaaS safeguards. |

The table covers worker-invoked Application and Infrastructure ports and the queue/domain contracts that cross the API-worker boundary. It records behavior changes as well as type-shape changes; a stable interface does not imply equivalent worker behavior.

## Compatibility conclusion and release pairing

The worker at `1872a14` **is not compatible as a complete production pairing with API `4818bb1` for SaaS operation**. The agent-workflow queue envelope and node-dispatch contract remain compatible, but the deployed worker lacks the multi-tenant mailbox guard, workspace iteration, legal-hold-aware retention, newer notification workflows and `LeadCreated` compatibility, and the case-follow-through timer. The old mailbox timer only processes the deployment-configured tenant/workspace instead of enumerating SaaS workspaces; old retention can delete records under legal hold; notification type handling can fail for an unknown type; and scheduled follow-through is absent.

For a release built from API source `4818bb1` or later, deploy the Functions source from the same release commit. At `4818bb1`, the Functions tree's last source-changing commit is `0d8af21`; the current `main` Functions tree at `80155dc` is identical. A release must not pair the API with worker `1872a14` for SaaS workloads. No artifact hash or evidence of the currently deployed worker changing was available, so this report does not claim a deployment occurred.

## Verification

All build and test commands were preceded by `scripts/guard/check-config-injection.sh`; each guard run exited 0.

| Check | Result |
|---|---|
| Functions source diff, `4818bb1` to `80155dc` | Passed, no differences |
| Functions Release build from `origin/main` worktree | Passed, 0 warnings and 0 errors |
| Focused `AgentWorkflowExecutionWorkerContractTests` and `FunctionsWorkflowRuntimeTests` | Passed, 25 tests, 0 failed |
| Deployment or database changes | None; explicitly out of scope |

The test command was `dotnet test tests/InsuranceIntelligence.Api.Tests/InsuranceIntelligence.Api.Tests.csproj -c Release --filter "FullyQualifiedName~AgentWorkflowExecutionWorkerContractTests|FullyQualifiedName~FunctionsWorkflowRuntimeTests" --verbosity normal`.

## Decisions and remaining gates

- D-018 records the compatibility finding and release pairing requirement.
- The Functions project at `80155dc` and its focused tests passed. B3 does not close the global section 8 gate.
- `main` remains unchanged at `80155dc`. No deployment, Azure resource, or database was touched.
- The previous phase branch push was refused with HTTP 408 and the remote branch was confirmed absent. B2 and B3 evidence remain local pending a successful reviewable push; no force push was attempted.
- Next: proceed to Phase C claims-core using Appendix A and B, keeping migration files and snapshots under the explicit rules in the plan.

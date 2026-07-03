import type { Page, Route } from "@playwright/test";

export type RuntimeRole = "Admin" | "ClaimsOfficer" | "ClaimsSupervisor" | "CustomerService" | "Compliance" | "Client";

export const visualCaseId = "11111111-1111-4111-8111-111111111111";

const rolePermissions: Record<RuntimeRole, string[]> = {
  Admin: ["all"],
  ClaimsOfficer: ["ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs", "ManageCustomerCommunications"],
  ClaimsSupervisor: ["ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAllClaims", "ViewAssignedClaims", "ApproveSettlementWorkflowStep", "ViewAuditLog", "ViewAiOutputs", "OverrideAiRecommendation", "ManageCustomerCommunications"],
  CustomerService: ["ViewCommandCenter", "CreateClaim", "UploadDocument", "ViewAssignedClaims", "ViewAiOutputs", "ManageCustomerCommunications"],
  Compliance: ["ViewCommandCenter", "ViewAllClaims", "ViewAuditLog", "ManageKnowledgeBase", "ViewAiOutputs", "ApproveSettlementWorkflowStep", "OverrideAiRecommendation"],
  Client: ["ViewClientPortal"]
};

const roleLabels: Record<RuntimeRole, string> = {
  Admin: "Tenant Admin",
  ClaimsOfficer: "Claims Officer",
  ClaimsSupervisor: "Claims Supervisor",
  CustomerService: "Customer Service",
  Compliance: "Compliance Officer",
  Client: "Policyholder"
};

export async function prepareVisualApi(page: Page, role: RuntimeRole) {
  await page.addInitScript((selectedRole) => {
    window.localStorage.setItem("iip-demo-role", selectedRole);
  }, role);

  await page.route("**/api/**", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
  });

  await page.route("**/api/v1/me", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        userId: role === "Client" ? "customer-visual" : "visual-user",
        displayName: role === "Client" ? "Policyholder" : "Maya Morgan",
        role,
        roleLabel: roleLabels[role],
        permissions: rolePermissions[role],
        tenantId: "tenant-visual",
        workspaceId: role === "Client" ? "client-portal" : "claims-operations",
        authMode: "Development",
        source: "PlaywrightVisual"
      })
    });
  });

  await page.route("**/api/v1/provider-status", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        overallStatus: "configured",
        providers: [
          { name: "graphMail", mode: "Graph", status: "configured", detail: "Synthetic visual test provider." },
          { name: "communications", mode: "GraphEmail", status: "configured", detail: "Synthetic visual test provider." },
          { name: "notifications", mode: "Configured", status: "configured", detail: "Synthetic visual test provider." },
          { name: "azureOpenAI", mode: "AzureOpenAI", status: "healthy", detail: "Synthetic visual test provider." },
          { name: "aiSearch", mode: "AzureAISearch", status: "healthy", detail: "Synthetic visual test provider." },
          { name: "documentIntelligence", mode: "AzureDocumentIntelligence", status: "healthy", detail: "Synthetic visual test provider." }
        ]
      })
    });
  });

  await page.route("**/api/v1/integrations/email/health", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        integrationId: "email",
        displayName: "Microsoft Graph mail",
        overallStatus: "healthy",
        providers: [
          { name: "graphMail", mode: "Graph", status: "configured", detail: "Synthetic visual test provider." },
          { name: "communications", mode: "GraphEmail", status: "configured", detail: "Synthetic visual test provider." },
          { name: "notifications", mode: "Configured", status: "configured", detail: "Synthetic visual test provider." }
        ],
        allowedActions: [
          { action: "configureMailbox", label: "Save mailbox configuration", requiredPermission: "ConfigureIntegrations", reason: null },
          { action: "processInboundEmail", label: "Process inbound email", requiredPermission: "ConfigureIntegrations", reason: null },
          { action: "sendCustomerEmail", label: "Send customer email", requiredPermission: "SendCustomerUpdate", reason: null }
        ],
        blockedActions: [
          {
            action: "runTestIngestion",
            label: "Run local test ingestion",
            requiredPermission: "ConfigureIntegrations",
            reason: "Local test ingestion is unavailable for this configured Graph provider."
          }
        ]
      })
    });
  });

  await page.route("**/api/v1/command-center/metrics**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        totalCases: 42,
        totalDocuments: 128,
        averageCompletenessScore: 82,
        knowledgeDocumentsApproved: 17,
        assistantQuestionsAnswered: 19,
        escalationsRequired: 4,
        openClaims: 42,
        newClaimsToday: 12,
        incompleteClaims: 7,
        slaAtRisk: 5,
        slaBreached: 2,
        pendingApprovals: 8,
        openTasks: 14,
        customerUpdatesPending: 6
      })
    });
  });

  await page.route("**/api/v1/command-center/assistant", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        answer: "SLA pressure is the immediate concern. Review the breached claims first, then the at-risk cases with pending approvals.",
        confidenceScore: 0.9,
        escalationRequired: true,
        guardrailMessage: "Open record-level workflow evidence before taking consequential action.",
        riskFlag: "needs_review",
        sources: [],
        agentActionLogId: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        insightType: "OperationalSummary",
        modelProvider: "AzureOpenAI",
        modelDeployment: "visual-chat",
        promptVersion: "command-center-operations-v1",
        visibility: "Internal",
        canTriggerDecision: false,
        generatedAt: "2026-06-20T09:15:00Z",
        correlationId: "visual-command-center-correlation"
      })
    });
  });

  await page.route("**/api/assistant/policy-questions", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        answer: "The approved motor policy includes windscreen damage subject to the listed policy conditions and human claim review.",
        confidenceScore: 0.88,
        escalationRequired: false,
        guardrailMessage: "Answer prepared only from approved retrieved sources. Final insurance decisions remain human-reviewed.",
        riskFlag: "grounded",
        coverageState: "GroundedAnswerAvailable",
        sources: [
          {
            knowledgeDocumentId: "33333333-3333-4333-8333-333333333333",
            chunkId: "motor-windscreen-1",
            documentName: "Motor Policy Wording",
            productLine: "Motor",
            version: "2026.1",
            excerpt: "Windscreen damage is included when shown in the policy schedule and remains subject to the applicable excess.",
            label: "Motor Policy Wording, Windscreen section"
          }
        ],
        agentActionLogId: "44444444-4444-4444-8444-444444444444",
        modelProvider: "AzureOpenAI",
        modelDeployment: "visual-chat",
        promptVersion: "policy-answer-v2",
        visibility: "Internal",
        canTriggerDecision: false,
        generatedAt: "2026-06-20T09:18:00Z",
        correlationId: "visual-policy-correlation"
      })
    });
  });

  await page.route("**/api/cases", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          caseId: "11111111-1111-4111-8111-111111111111",
          customerName: "Visual Fixture Customer",
          policyNumber: "POL-VISUAL-1001",
          productLine: "Motor",
          claimType: "Accident",
          channel: "Email",
          status: "New",
          completenessScore: 64,
          documentStatus: "Missing Documents",
          riskLevel: "High",
          priority: "High",
          slaStatus: "AtRisk",
          assignedUserId: "visual-reviewer",
          assignedTeam: "Claims team",
          updatedAt: "2026-06-18T09:30:00Z",
          audit: {
            lastActionName: "Visual fixture workflow item prepared for human review."
          },
          allowedActions: [
            { action: "openCaseReview", label: "Open case review", requiredPermission: "ViewAssignedClaims", reason: null }
          ],
          blockedActions: []
        },
        {
          caseId: "22222222-2222-4222-8222-222222222222",
          customerName: "Second Visual Customer",
          policyNumber: "POL-VISUAL-1002",
          productLine: "Property",
          claimType: "Damage",
          channel: "Portal",
          status: "ReadyForReview",
          completenessScore: 92,
          documentStatus: "Ready For Review",
          riskLevel: "Medium",
          priority: "Normal",
          slaStatus: "OnTrack",
          assignedUserId: null,
          assignedTeam: "Review team",
          updatedAt: "2026-06-18T10:30:00Z",
          audit: {
            lastActionName: "Completeness checklist reviewed by configured workflow."
          },
          allowedActions: [
            { action: "openCaseReview", label: "Open case review", requiredPermission: "ViewAssignedClaims", reason: null }
          ],
          blockedActions: []
        }
      ])
    });
  });

  await page.route(`**/api/cases/${visualCaseId}`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        caseId: visualCaseId,
        customerName: "Visual Fixture Customer",
        policyNumber: "VISUAL-POLICY-001",
        productLine: "Motor",
        claimType: "Accident",
        completenessScore: 67,
        audit: { lastActionName: "document.extraction.field_corrected" },
        allowedActions: [
          { action: "openCaseReview", label: "Open case review", requiredPermission: "ViewAssignedClaims", reason: null },
          { action: "evaluateCaseWorkflow", label: "Evaluate workflow", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
          { action: "createApprovalRequest", label: "Create approval request", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
          { action: "draftCustomerUpdate", label: "Draft customer update", requiredPermission: "ManageCustomerCommunications", reason: null }
        ],
        blockedActions: []
      })
    });
  });

  await page.route(`**/api/cases/${visualCaseId}/documents`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          documentId: "22222222-2222-4222-8222-222222222222",
          caseId: visualCaseId,
          originalFileName: "visual-claim-form.pdf",
          contentType: "application/pdf",
          fileSizeBytes: 245760,
          documentType: "ClaimForm",
          processingStatus: "Processed",
          confidenceScore: 0.86,
          fields: [
            {
              name: "policyNumber",
              extractedValue: "VISUAL-POLICY-01",
              effectiveValue: "VISUAL-POLICY-001",
              correctedValue: "VISUAL-POLICY-001",
              confidenceScore: 0.72,
              verificationStatus: "Corrected",
              sensitivity: "Sensitive",
              warnings: [],
              correctedAt: "2026-06-20T09:15:00Z",
              correctionReference: "visual-correction-reference"
            },
            {
              name: "incidentDate",
              extractedValue: "20 June 2026",
              effectiveValue: "20 June 2026",
              correctedValue: null,
              confidenceScore: 0.68,
              verificationStatus: "NeedsReview",
              sensitivity: "Standard",
              warnings: [
                {
                  code: "document.field.date_format",
                  message: "Confirm the incident date format against the submitted form.",
                  severity: "warning",
                  fieldName: "incidentDate",
                  expectedValue: "YYYY-MM-DD",
                  observedValue: "20 June 2026"
                }
              ],
              correctedAt: null,
              correctionReference: null
            }
          ],
          validationWarnings: [],
          extractionApproved: false,
          uploadedAt: "2026-06-20T09:00:00Z",
          processedAt: "2026-06-20T09:01:00Z",
          correlationId: "visual-document-correlation",
          previewUrl: `/api/cases/${visualCaseId}/documents/22222222-2222-4222-8222-222222222222/content`,
          allowedActions: [
            { action: "reclassifyDocument", label: "Reclassify document", requiredPermission: "UploadDocument", reason: null },
            { action: "approveDocumentExtraction", label: "Approve extraction", requiredPermission: "UploadDocument", reason: null },
            { action: "correctDocumentExtraction", label: "Correct extracted field", requiredPermission: "UploadDocument", reason: null }
          ],
          blockedActions: []
        }
      ])
    });
  });

  await page.route(`**/api/cases/${visualCaseId}/document-checklist`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        caseId: visualCaseId,
        productLine: "Motor",
        completenessScore: 67,
        requiredReceived: 2,
        requiredTotal: 3,
        requiredDocuments: [
          { documentType: "ClaimForm", label: "Claim form", received: true },
          { documentType: "PoliceAbstract", label: "Police abstract", received: false },
          { documentType: "PhotoEvidence", label: "Photo evidence", received: true }
        ],
        optionalDocuments: [],
        receivedDocuments: [
          { documentType: "ClaimForm", label: "Claim form", received: true },
          { documentType: "PhotoEvidence", label: "Photo evidence", received: true }
        ],
        missingRequiredDocuments: [
          { documentType: "PoliceAbstract", label: "Police abstract", received: false }
        ]
      })
    });
  });

  await page.route("**/api/approvals", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          approvalId: "approval-visual-1",
          caseId: "11111111-1111-4111-8111-111111111111",
          approvalType: "WorkflowReview",
          status: "Pending",
          riskLevel: "High",
          sourceReferences: ["case-evidence:visual-fixture"],
          requestedByUserId: "workflow-service",
          reviewerUserId: null,
          evidenceSummary: "Human review is required before this workflow item changes state.",
          comments: null,
          createdAt: "2026-06-18T09:30:00Z",
          allowedActions: [
            { action: "approveApproval", label: "Approve workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
            { action: "rejectApproval", label: "Reject workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
            { action: "escalateApproval", label: "Escalate workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
            { action: "overrideApproval", label: "Override AI recommendation", requiredPermission: "OverrideAiRecommendation", reason: null }
          ],
          blockedActions: []
        }
      ])
    });
  });

  await page.route("**/api/reports/operations-summary", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        openClaims: 42,
        newClaimsToday: 12,
        incompleteClaims: 7,
        averageCompletenessScore: 82,
        pendingApprovals: 8,
        openTasks: 14,
        customerUpdatesPending: 6,
        assistantQuestionsAnswered: 19,
        escalationsRequired: 4,
        slaAtRisk: 5,
        slaBreached: 2,
        claimsByStatus: [
          { label: "New", count: 12 },
          { label: "Under review", count: 20 },
          { label: "Ready for review", count: 10 }
        ],
        workQueueSummary: [
          { label: "Missing documents", count: 7 },
          { label: "Human review", count: 8 },
          { label: "Customer updates", count: 6 }
        ]
      })
    });
  });

  await page.route("**/api/admin/provider-health", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        overallStatus: "configured",
        providers: [
          { name: "graphMail", mode: "Graph", status: "configured", detail: "Synthetic visual test provider." },
          { name: "communications", mode: "GraphEmail", status: "configured", detail: "Synthetic visual test provider." },
          { name: "notifications", mode: "Configured", status: "configured", detail: "Synthetic visual test provider." },
          { name: "azureOpenAI", mode: "AzureOpenAI", status: "healthy", detail: "Synthetic visual test provider." },
          { name: "aiSearch", mode: "AzureAISearch", status: "healthy", detail: "Synthetic visual test provider." },
          { name: "documentIntelligence", mode: "AzureDocumentIntelligence", status: "healthy", detail: "Synthetic visual test provider." }
        ]
      })
    });
  });

  await page.route("**/api/admin/tenant-configuration", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        insurerName: "Visual Fixture Insurance",
        logoUrl: "https://assets.example.invalid/visual-insurer-logo.png",
        country: "Uganda",
        region: "East Africa",
        businessLines: [
          { name: "Motor", enabled: true },
          { name: "Medical", enabled: true }
        ],
        channels: [
          { name: "Email", enabled: true },
          { name: "Customer Portal", enabled: true }
        ],
        approvalThresholds: [
          { productLine: "Motor", amount: 5000, requiredRole: "ClaimsSupervisor" }
        ],
        checklists: [
          { productLine: "Motor", requiredDocuments: ["ClaimForm", "PoliceReport"] }
        ],
        auditRetentionDays: 2555,
        updatedAt: "2026-06-18T09:00:00Z"
      })
    });
  });

  await page.route("**/api/admin/users", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          userId: "visual-admin-user",
          name: "Maya Morgan",
          email: "maya.morgan@example.invalid",
          role: "Admin",
          status: "Active",
          allowedActions: [],
          blockedActions: []
        }
      ])
    });
  });

  await page.route("**/api/integrations/email/mailboxes", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          mailboxConnectionId: "mailbox-visual-1",
          displayName: "Visual Claims Mailbox",
          mailboxAddress: "claims.visual@example.invalid",
          ingestionEnabled: true,
          defaultChannel: "Email",
          productLine: "Motor",
          processedFolderName: "InsuranceIntelligence Processed"
        }
      ])
    });
  });

  await page.route("**/api/integrations/email/messages", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          messageId: "message-visual-1",
          sender: "customer.visual@example.invalid",
          subject: "Visual fixture claim document",
          receivedAt: "2026-06-18T09:00:00Z",
          processingStatus: "Processed",
          linkedCaseId: "11111111-1111-4111-8111-111111111111",
          attachments: [{ fileName: "visual-document.pdf", sizeBytes: 2048 }]
        }
      ])
    });
  });

  await page.route("**/api/compliance/audit-events**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          eventId: "audit-visual-1",
          caseId: "11111111-1111-4111-8111-111111111111",
          userId: "visual-reviewer",
          actorType: "Human",
          actionName: "Workflow review opened",
          timestamp: "2026-06-18T09:35:00Z",
          validationResult: "Allowed",
          correlationId: "correlation-visual-1"
        }
      ])
    });
  });

  await page.route("**/api/knowledge-documents", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          knowledgeDocumentId: "knowledge-visual-1",
          documentName: "Visual Fixture Claims Checklist",
          documentType: "Checklist",
          productLine: "Motor",
          version: "visual",
          approvalStatus: "Approved",
          indexedStatus: "Indexed",
          chunkCount: 12,
          updatedAt: "2026-06-18T09:00:00Z",
          allowedActions: [
            { action: "archiveKnowledgeDocument", label: "Archive knowledge document", requiredPermission: "ManageKnowledgeBase", reason: null }
          ],
          blockedActions: [
            { action: "approveKnowledgeDocument", label: "Approve knowledge document", requiredPermission: "ManageKnowledgeBase", reason: "This knowledge document is already approved." }
          ]
        }
      ])
    });
  });

  await page.route("**/api/client-portal/claims/**", async (route) => {
    if (route.request().method() === "POST") {
      await fulfillClientClaim(route, "Documents uploaded and sent for review.");
      return;
    }

    await fulfillClientClaim(route);
  });

}

export async function prepareDeployedProfileApi(page: Page, role: RuntimeRole) {
  await page.route("**/api/**", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
  });

  await page.route("**/api/v1/me", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        userId: role === "Client" ? "customer-deployed" : "staff-deployed",
        displayName: role === "Client" ? "Policyholder" : "Maya Morgan",
        role,
        roleLabel: roleLabels[role],
        permissions: rolePermissions[role],
        tenantId: "tenant-deployed",
        workspaceId: role === "Client" ? "client-portal" : "claims-operations",
        authMode: "EntraId",
        source: "PlaywrightDeployedProfile"
      })
    });
  });

  await page.route("**/api/v1/provider-status", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        overallStatus: "not_configured",
        providers: [
          { name: "graphMail", mode: "Graph", status: "not_configured", detail: "No tenant mailbox connection configured." },
          { name: "communications", mode: "NotConfigured", status: "not_configured", detail: "No customer communications provider configured." },
          { name: "notifications", mode: "NotConfigured", status: "not_configured", detail: "No notification provider configured." },
          { name: "azureOpenAI", mode: "AzureOpenAI", status: "not_configured", detail: "No Azure OpenAI deployment configured." },
          { name: "aiSearch", mode: "AzureAISearch", status: "not_configured", detail: "No Azure AI Search index configured." },
          { name: "documentIntelligence", mode: "AzureDocumentIntelligence", status: "not_configured", detail: "No Document Intelligence resource configured." }
        ]
      })
    });
  });

  await page.route("**/api/v1/command-center/metrics**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        totalCases: 12,
        totalDocuments: 36,
        averageCompletenessScore: 71,
        knowledgeDocumentsApproved: 4,
        assistantQuestionsAnswered: 5,
        escalationsRequired: 1,
        openClaims: 12,
        newClaimsToday: 3,
        incompleteClaims: 5,
        slaAtRisk: 2,
        slaBreached: 1,
        pendingApprovals: 2,
        openTasks: 6,
        customerUpdatesPending: 2
      })
    });
  });

  await page.route("**/api/approvals", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          approvalId: "approval-deployed-1",
          caseId: "11111111-1111-4111-8111-111111111111",
          approvalType: "WorkflowReview",
          status: "Pending",
          riskLevel: "High",
          sourceReferences: ["case-evidence:deployed-profile"],
          requestedByUserId: "workflow-service",
          reviewerUserId: null,
          evidenceSummary: "Human review is required before any workflow step changes state.",
          comments: null,
          createdAt: "2026-06-18T09:30:00Z",
          allowedActions: [
            { action: "approveApproval", label: "Approve workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
            { action: "rejectApproval", label: "Reject workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
            { action: "escalateApproval", label: "Escalate workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
            { action: "overrideApproval", label: "Override AI recommendation", requiredPermission: "OverrideAiRecommendation", reason: null }
          ],
          blockedActions: []
        }
      ])
    });
  });

  await page.route("**/api/client-portal/claims/**", async (route) => {
    await fulfillClientClaim(route);
  });
}

export async function selectDevelopmentRole(page: Page, role: RuntimeRole) {
  await page.getByLabel("Role").selectOption(role);
}

async function fulfillClientClaim(route: Route, statusMessage?: string) {
  await route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      caseId: "11111111-1111-4111-8111-111111111111",
      productLine: "Motor",
      claimType: "Accident",
      status: "UnderReview",
      policyNumber: "POL-VISUAL-1001",
      incidentDate: "2026-06-12T00:00:00Z",
      createdAt: "2026-06-13T08:00:00Z",
      updatedAt: "2026-06-18T09:30:00Z",
      completenessScore: statusMessage ? 80 : 60,
      requiredDocuments: [
        { documentType: "ClaimForm", label: "Completed claim form", uploaded: Boolean(statusMessage), status: statusMessage ? "Uploaded" : "Not uploaded" },
        { documentType: "PoliceReport", label: "Police report", uploaded: false, status: "Not uploaded" },
        { documentType: "Photos", label: "Photos of damage", uploaded: true, status: "Uploaded" }
      ],
      timeline: [
        { label: "Claim received", complete: true, date: "2026-06-13T08:00:00Z" },
        { label: "Under review", complete: true, date: "2026-06-14T09:00:00Z" },
        { label: "Assessment", complete: false, date: null },
        { label: "Decision", complete: false, date: null },
        { label: "Settlement", complete: false, date: null }
      ],
      messages: [
        {
          communicationId: "message-visual-1",
          channel: "Portal",
          direction: "Outbound",
          messageBody: "Your claim has been received and is under review.",
          createdAt: "2026-06-14T10:00:00Z",
          deliveryStatus: "Delivered"
        }
      ],
      nextSteps: [
        "Upload remaining required documents.",
        "The claims team will complete the review.",
        "A customer-safe update will appear here when available."
      ]
    })
  });
}

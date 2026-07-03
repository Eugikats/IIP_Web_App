import type { Page, Route } from "@playwright/test";

const caseId = "CLM-2026-00491";
const now = "2026-05-30T10:42:00+03:00";

const claim = {
  caseId,
  customerName: "Sarah N.",
  customerEmail: "sarah.visual@example.invalid",
  customerPhone: "+256••••123",
  policyNumber: "POL-UG-MTR-88201",
  productLine: "Motor",
  claimType: "Accident",
  channel: "CustomerPortal",
  status: "UnderReview",
  completenessScore: 72,
  documentStatus: "MissingDocuments",
  riskLevel: "High",
  priority: "High",
  slaStatus: "DueToday",
  assignedUserId: "usr-visual-supervisor",
  assignedTeam: "Motor Claims Review",
  incidentDate: "2026-05-30",
  incidentDescription: "Synthetic fixture accident claim for visual diagnostics.",
  slaDueAt: "2026-05-30T18:00:00+03:00",
  createdAt: now,
  updatedAt: now,
  audit: {
    eventCount: 6,
    lastActionName: "Document verification requested",
    lastActionAt: now,
    lastActorName: "Jane Namusoke"
  },
  allowedActions: ["RequestDocuments", "EscalateToSupervisor"],
  blockedActions: [
    {
      action: "ApproveReviewStep",
      reasonCode: "MISSING_REQUIRED_DOCUMENTS",
      message: "Police report and damage photographs are required before review can continue."
    }
  ]
};

const readyClaim = {
  ...claim,
  caseId: "CLM-2026-00492",
  customerName: "Daniel K.",
  policyNumber: "POL-UG-MED-11872",
  productLine: "Medical",
  claimType: "Reimbursement",
  channel: "Email",
  status: "UnderReview",
  completenessScore: 100,
  documentStatus: "Ready For Review",
  riskLevel: "Medium",
  priority: "Medium",
  slaStatus: "OnTrack",
  assignedUserId: "usr-visual-supervisor",
  assignedTeam: "Medical Claims",
  audit: {
    eventCount: 4,
    lastActionName: "Extraction approved by claims officer",
    lastActionAt: now,
    lastActorName: "Jane Namusoke"
  },
  allowedActions: ["ApproveReviewStep", "RequestMoreInformation"],
  blockedActions: []
};

const breachedClaim = {
  ...claim,
  caseId: "CLM-2026-00493",
  customerName: "Amina T.",
  policyNumber: "POL-UG-FIR-44018",
  productLine: "Fire",
  claimType: "Property Damage",
  channel: "Broker",
  status: "Escalated",
  completenessScore: 64,
  documentStatus: "Missing Documents",
  riskLevel: "Critical",
  priority: "Critical",
  slaStatus: "Breached",
  assignedUserId: null,
  assignedTeam: "Complex Claims",
  audit: {
    eventCount: 9,
    lastActionName: "SLA escalation generated",
    lastActionAt: now,
    lastActorName: "Workflow"
  },
  allowedActions: ["AssignOfficer", "RequestDocuments"],
  blockedActions: [
    {
      action: "SendCustomerUpdate",
      reasonCode: "PROVIDER_NOT_CONFIGURED",
      message: "Customer communication provider is not configured for this visual fixture."
    }
  ]
};

const documents = [
  {
    documentId: "DOC-2026-00214",
    caseId,
    originalFileName: "claim_form.pdf",
    fileName: "claim_form.pdf",
    contentType: "application/pdf",
    fileSizeBytes: 245000,
    objectKey: "visual/claim_form.pdf",
    documentType: "ClaimForm",
    processingStatus: "Completed",
    confidenceScore: 0.91,
    extractedFields: { claimant: "Sarah N.", incidentDate: "2026-05-30" },
    fieldConfidences: { claimant: 0.96, incidentDate: 0.91 },
    validationWarnings: [],
    extractionApproved: true,
    uploadedAt: now,
    allowedActions: ["ApproveExtraction"],
    blockedActions: []
  },
  {
    documentId: "DOC-2026-00215",
    caseId,
    originalFileName: "repair_estimate.pdf",
    fileName: "repair_estimate.pdf",
    contentType: "application/pdf",
    fileSizeBytes: 198000,
    objectKey: "visual/repair_estimate.pdf",
    documentType: "RepairEstimate",
    processingStatus: "VerificationRequired",
    confidenceScore: 0.63,
    extractedFields: { amount: "UGX 18,500,000" },
    fieldConfidences: { amount: 0.63 },
    validationWarnings: [{ code: "LOW_CONFIDENCE_AMOUNT", message: "Amount requires human verification.", severity: "Warning" }],
    extractionApproved: false,
    uploadedAt: now,
    allowedActions: [],
    blockedActions: [
      {
        action: "ApproveExtraction",
        reasonCode: "LOW_CONFIDENCE",
        message: "Human verification is required before extraction approval."
      }
    ]
  }
];

const checklist = {
  caseId,
  productLine: "Motor",
  requiredReceived: 2,
  requiredTotal: 4,
  requiredDocuments: [
    { documentType: "ClaimForm", label: "Claim form", received: true },
    { documentType: "PoliceReport", label: "Police report", received: false },
    { documentType: "RepairEstimate", label: "Repair estimate", received: true },
    { documentType: "DamagePhotographs", label: "Damage photographs", received: false }
  ],
  optionalDocuments: [{ documentType: "WitnessStatement", label: "Witness statement", received: false }],
  receivedDocuments: [
    { documentType: "ClaimForm", label: "Claim form", received: true },
    { documentType: "RepairEstimate", label: "Repair estimate", received: true }
  ],
  missingRequiredDocuments: [
    { documentType: "PoliceReport", label: "Police report", received: false },
    { documentType: "DamagePhotographs", label: "Damage photographs", received: false }
  ],
  completenessScore: 50
};

const approvals = [
  {
    approvalId: "APP-2026-00031",
    caseId,
    approvalType: "SupervisorReviewStep",
    status: "Pending",
    decision: null,
    riskLevel: "High",
    evidenceSummary: "Police report is missing; supervisor review is required.",
    sourceReferences: ["Motor Claim Required Evidence Checklist"],
    requestedByUserId: "usr-visual-supervisor",
    reviewerUserId: "Jane Namusoke",
    comments: null,
    decidedAt: null,
    createdAt: now,
    updatedAt: now
  }
];

const providerModes = {
  storageProvider: "NotConfigured",
  authMode: "Entra",
  aiProvider: "NotConfigured",
  documentProvider: "NotConfigured",
  retrievalProvider: "NotConfigured",
  emailProvider: "NotConfigured",
  communicationProvider: "NotConfigured",
  notificationProvider: "DisabledByPolicy",
  seedDemoData: false
};

const providerHealth = {
  overallStatus: "NotReady",
  status: "NotReady",
  providers: [
    { name: "Microsoft Graph Mail", mode: "NotConfigured", status: "missing", detail: "Not configured for visual fixture." },
    { name: "Azure Document Intelligence", mode: "NotConfigured", status: "missing", detail: "Not configured for visual fixture." },
    { name: "Azure OpenAI", mode: "NotConfigured", status: "missing", detail: "Not configured for visual fixture." },
    { name: "Azure AI Search", mode: "NotConfigured", status: "missing", detail: "Not configured for visual fixture." }
  ]
};

const providerStatus = {
  overallStatus: "NotReady",
  providers: [
    { name: "graphMail", mode: "NotConfigured", status: "missing", detail: "Microsoft Graph mail is not configured for visual fixture." },
    { name: "communications", mode: "NotConfigured", status: "missing", detail: "Customer communications are not configured for visual fixture." },
    { name: "notifications", mode: "DisabledByPolicy", status: "missing", detail: "Teams notifications are disabled by policy for visual fixture." },
    { name: "azureOpenAI", mode: "NotConfigured", status: "missing", detail: "Azure OpenAI is not configured for visual fixture." },
    { name: "aiSearch", mode: "NotConfigured", status: "missing", detail: "Azure AI Search is not configured for visual fixture." },
    { name: "documentIntelligence", mode: "NotConfigured", status: "missing", detail: "Document Intelligence is not configured for visual fixture." }
  ]
};

const systemCapabilities = {
  activeAi: false,
  activeDocumentExtraction: false,
  activeKnowledgeRetrieval: false,
  activeEmailIngestion: false,
  activeSmsDelivery: false,
  activeTeamsNotification: false,
  activeChannels: false,
  activeOnboarding: false,
  environment: "Development",
  authMode: "Development"
};

const emailIntegrationHealth = {
  integrationId: "email",
  displayName: "Microsoft Graph Mail",
  overallStatus: "NotReady",
  providers: providerStatus.providers,
  allowedActions: [
    { action: "configureMailbox", label: "Save mailbox configuration", requiredPermission: "ConfigureIntegrations", reason: null }
  ],
  blockedActions: [
    {
      action: "runTestIngestion",
      label: "Run local ingestion",
      requiredPermission: "ConfigureIntegrations",
      reason: "Local ingestion is not part of the target visual acceptance fixture."
    },
    {
      action: "processInboundEmail",
      label: "Process inbound email",
      requiredPermission: "ConfigureIntegrations",
      reason: "Microsoft Graph mailbox ingestion is not configured."
    }
  ]
};

const knowledgeDocument = {
  knowledgeDocumentId: "KNO-2026-00018",
  documentName: "Motor Policy Wording",
  productLine: "Motor",
  documentType: "PolicyWording",
  version: "1.0",
  effectiveDate: "2026-01-01",
  expiryDate: null,
  originalFileName: "motor-policy-wording.md",
  contentType: "text/markdown",
  fileSizeBytes: 42000,
  objectKey: "visual/knowledge/motor-policy-wording.md",
  approvalStatus: "Draft",
  indexedStatus: "NotIndexed",
  chunkCount: 2,
  createdAt: now,
  updatedAt: now
};

const knowledgeDetail = {
  document: knowledgeDocument,
  chunks: [
    {
      chunkId: "KNO-2026-00018-0",
      chunkIndex: 0,
      excerpt: "Windscreen damage is covered when listed in the active motor policy schedule and supported by repair evidence.",
      sourceLabel: "Motor Policy Wording / Motor / v1.0"
    },
    {
      chunkId: "KNO-2026-00018-1",
      chunkIndex: 1,
      excerpt: "Claims officers must request missing evidence when the policy schedule or repair estimate is incomplete.",
      sourceLabel: "Motor Policy Wording / Motor / v1.0"
    }
  ]
};

const groundedPolicyAnswer = {
  answer: "Based on approved policy information, windscreen damage can be covered when it appears in the active motor policy schedule and the claim has supporting repair evidence. Human review is required before using this in a claim decision.",
  confidenceScore: 0.87,
  escalationRequired: false,
  guardrailMessage: "Answer prepared only from approved retrieved knowledge. Final claim decisions remain human-supervised.",
  riskFlag: "Grounded",
  sources: [
    {
      knowledgeDocumentId: knowledgeDocument.knowledgeDocumentId,
      chunkId: "KNO-2026-00018-0",
      documentName: "Motor Policy Wording",
      productLine: "Motor",
      version: "1.0",
      excerpt: "Windscreen damage is covered when listed in the active motor policy schedule and supported by repair evidence.",
      label: "Motor Policy Wording / Motor / v1.0"
    }
  ],
  agentActionLogId: "AI-2026-00120",
  allowedActions: ["EscalateToHumanReview"],
  blockedActions: [
    {
      action: "UseAsCoverageDecision",
      reasonCode: "HUMAN_DECISION_REQUIRED",
      message: "AI policy answers cannot be used as final coverage decisions."
    }
  ]
};

const escalatedPolicyAnswer = {
  answer: "I cannot answer this from approved knowledge. Escalate this question to a human reviewer or knowledge manager before using it in a claim workflow.",
  confidenceScore: 0.22,
  escalationRequired: true,
  guardrailMessage: "No approved source was found. The assistant did not use model memory.",
  riskFlag: "NoApprovedSource",
  sources: [],
  agentActionLogId: "AI-2026-00121",
  allowedActions: ["EscalateToHumanReview"],
  blockedActions: [
    {
      action: "UseAsCoverageDecision",
      reasonCode: "HUMAN_DECISION_REQUIRED",
      message: "AI policy answers cannot be used as final coverage decisions."
    }
  ]
};

const assistantActions = [
  {
    agentActionLogId: "AI-2026-00119",
    caseId,
    agentName: "PolicyQnAAgent",
    assistantMode: "PolicyQnA",
    inputSummary: "Is windscreen damage covered under this motor policy?",
    retrievedSources: groundedPolicyAnswer.sources,
    modelOutput: "Grounded answer returned with approved source citations.",
    confidenceScore: 0.87,
    riskFlag: "Grounded",
    humanOverride: false,
    createdAt: now,
    correlationId: "corr-ai-visual-001"
  },
  {
    agentActionLogId: "AI-2026-00118",
    caseId: null,
    agentName: "PolicyQnAAgent",
    assistantMode: "PolicyQnA",
    inputSummary: "Question outside approved source coverage.",
    retrievedSources: [],
    modelOutput: "Escalation required because no approved source was found.",
    confidenceScore: 0.22,
    riskFlag: "NoApprovedSource",
    humanOverride: false,
    createdAt: now,
    correlationId: "corr-ai-visual-000"
  }
];

const responses: Record<string, unknown> = {
  "/health": { status: "Healthy", environment: "Development" },
  "/api/v1/me": {
    userId: "usr-visual-supervisor",
    displayName: "Jane Namusoke",
    email: "jane.visual@example.invalid",
    tenantId: "tenant-visual-fixture",
    tenantName: "Uganda Insurer Ltd.",
    workspaceId: "workspace-visual-fixture",
    workspaceName: "Kampala Motor Claims",
    role: "ClaimsSupervisor",
    roleLabel: "Claims Supervisor",
    roles: ["ClaimsSupervisor"],
    businessLines: ["Motor"],
    permissions: [
      "ApproveSettlementWorkflowStep",
      "CreateClaim",
      "OverrideAiRecommendation",
      "SendCustomerUpdate",
      "UploadDocument",
      "ViewAiOutputs",
      "ViewAllClaims",
      "ViewAssignedClaims",
      "ViewAuditLog",
      "ViewCommandCenter",
      "ConfigureIntegrations"
    ],
    authority: {
      currency: "UGX",
      reviewApprovalLimit: 5000000,
      requiresEscalationAbove: 5000000,
      canOverrideAiRecommendation: true,
      canApprovePayout: false
    },
    uiFeatures: {
      aiConversationsEnabled: true,
      policyIntelligenceEnabled: true,
      documentIntelligenceEnabled: true,
      integrationsEnabled: false,
      customerPortalEnabled: false
    },
    dataScope: {
      claims: "team",
      policies: "business_line",
      audit: "read_only_if_authorised"
    },
    environment: {
      name: "Local Development",
      mode: "LocalTest",
      source: "Development"
    }
  },
  "/api/command-center/metrics": {
    totalCases: 532,
    openClaims: 124,
    newClaimsToday: 18,
    incompleteClaims: 32,
    pendingApprovals: 16,
    openTasks: 41,
    customerUpdatesPending: 9,
    slaAtRisk: 28,
    slaBreached: 7,
    assistantQuestionsAnswered: 82,
    escalationsRequired: 9,
    averageCompletenessScore: 78,
    knowledgeDocumentsApproved: 14,
    totalDocuments: 221
  },
  "/api/v1/command-center/metrics": {
    totalCases: 532,
    openClaims: 124,
    newClaimsToday: 18,
    incompleteClaims: 32,
    pendingApprovals: 16,
    openTasks: 41,
    customerUpdatesPending: 9,
    slaAtRisk: 28,
    slaBreached: 7,
    assistantQuestionsAnswered: 82,
    escalationsRequired: 9,
    averageCompletenessScore: 78,
    knowledgeDocumentsApproved: 14,
    totalDocuments: 221
  },
  "/api/cases": [claim, readyClaim, breachedClaim],
  [`/api/cases/${caseId}`]: claim,
  [`/api/cases/${caseId}/documents`]: documents,
  [`/api/cases/${caseId}/document-checklist`]: checklist,
  [`/api/cases/${caseId}/recommended-actions`]: [
    {
      actionId: "ACT-2026-00012",
      caseId,
      actionType: "RequestDocuments",
      title: "Request missing evidence",
      rationale: "Police report and damage photographs are required before review can continue.",
      confidenceScore: 0.84,
      createdAt: now
    }
  ],
  [`/api/cases/${caseId}/timeline`]: [
    { eventId: "TIM-001", eventType: "ClaimCreated", title: "Claim created", description: "Synthetic claim submitted.", occurredAt: now, actorName: "Sarah N." },
    { eventId: "TIM-002", eventType: "DocumentUploaded", title: "Repair estimate uploaded", description: "Document requires verification.", occurredAt: now, actorName: "Jane Namusoke" }
  ],
  [`/api/cases/${caseId}/customer-updates`]: [
    {
      updateId: "UPD-2026-00009",
      caseId,
      channel: "Email",
      status: "Draft",
      messageBody: "Please upload the requested document so we can continue reviewing your claim.",
      requiresApproval: true,
      sensitive: false,
      deliveryStatus: "NotConfigured",
      createdAt: now,
      updatedAt: now,
      allowedActions: ["ApproveCustomerCommunication"],
      blockedActions: [
        {
          action: "SendCustomerCommunication",
          reasonCode: "HUMAN_APPROVAL_REQUIRED",
          message: "Human approval is required before this AI-drafted customer update can be sent."
        }
      ]
    }
  ],
  [`/api/cases/${caseId}/communications`]: [],
  [`/api/assistant/actions/${caseId}`]: [
    {
      actionLogId: "AI-2026-00091",
      caseId,
      actionType: "MissingEvidence",
      summary: "Police report has not been received for this motor accident claim.",
      confidenceScore: 0.94,
      createdAt: now
    }
  ],
  "/api/assistant/actions": assistantActions,
  "/api/workflow/tasks": [
    {
      taskId: "TSK-2026-00071",
      caseId,
      title: "Verify repair estimate",
      status: "Open",
      priority: "High",
      dueAt: now,
      assignedTo: "Jane Namusoke"
    }
  ],
  "/api/approvals": approvals,
  "/api/reports/operations-summary": {
    openClaims: 124,
    newClaimsToday: 18,
    incompleteClaims: 32,
    averageCompletenessScore: 78,
    pendingApprovals: 16,
    openTasks: 41,
    customerUpdatesPending: 9,
    assistantQuestionsAnswered: 82,
    escalationsRequired: 9,
    slaAtRisk: 28,
    slaBreached: 7,
    claimsByStatus: [
      { label: "Under review", status: "UnderReview", count: 124 },
      { label: "Awaiting documents", status: "AwaitingDocs", count: 88 }
    ],
    workQueueSummary: [
      { label: "Supervisor review", queueName: "Supervisor Review", count: 16 },
      { label: "Document verification", queueName: "Document Verification", count: 32 }
    ]
  },
  "/api/admin/provider-modes": providerModes,
  "/api/admin/provider-health": providerHealth,
  "/api/v1/provider-status": providerStatus,
  "/api/v1/system/capabilities": systemCapabilities,
  "/api/v1/integrations/email/health": emailIntegrationHealth,
  "/api/knowledge-documents": [knowledgeDocument],
  [`/api/knowledge-documents/${knowledgeDocument.knowledgeDocumentId}`]: knowledgeDetail,
  "/api/admin/tenant-configuration": {
    tenantName: "Uganda Insurer Ltd.",
    insurerName: "Uganda Insurer Ltd.",
    country: "Uganda",
    region: "Kampala",
    workspaceName: "Kampala Motor Claims",
    businessLines: [{ name: "Motor", enabled: true }],
    channels: [{ name: "Email", enabled: false }],
    currency: "UGX",
    auditRetentionDays: 365
  },
  "/api/admin/users": [
    {
      userId: "usr-visual-supervisor",
      name: "Jane Namusoke",
      displayName: "Jane Namusoke",
      email: "jane.visual@example.invalid",
      role: "ClaimsSupervisor",
      status: "Active",
      department: "Claims",
      branch: "Kampala",
      allowedActions: [
        { action: "updateUserRole", label: "Update user role", requiredPermission: "ManageTenantSettings", reason: null }
      ],
      blockedActions: []
    }
  ],
  "/api/admin/roles": [
    { role: "ClaimsSupervisor", permissions: ["ViewCommandCenter", "ViewAllClaims", "ApproveSettlementWorkflowStep"] }
  ],
  "/api/integrations/email/mailboxes": [],
  "/api/integrations/email/messages": [],
  "/api/communications/history": [],
  "/api/compliance/audit-events": [
    {
      eventId: "AUD-2026-11903",
      caseId,
      actionName: "Document verification requested",
      action: "Document verification requested",
      actorType: "User",
      actor: "Jane Namusoke",
      userId: "Jane Namusoke",
      timestamp: now,
      occurredAt: now,
      validationResult: "accepted",
      correlationId: "corr-audit-visual"
    }
  ]
};

async function fulfillJson(route: Route, value: unknown, status = 200) {
  await route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(value)
  });
}

export async function installVisualApiFixtures(page: Page) {
  await page.route("http://127.0.0.1:5167/**", async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;

    if (path === "/api/v1/me") {
      const requestedRole = route.request().headers()["x-demo-role"];
      if (requestedRole === "Admin") {
        await fulfillJson(route, {
          userId: "usr-visual-admin",
          displayName: "Amina Admin",
          email: "admin.visual@example.invalid",
          tenantId: "tenant-visual-fixture",
          workspaceId: "workspace-visual-fixture",
          role: "Admin",
          roleLabel: "Tenant Admin",
          permissions: ["all"],
          authMode: "Development",
          source: "Development"
        });
        return;
      }
    }

    if (route.request().method() === "POST" && path === "/api/assistant/policy-questions") {
      const body = route.request().postDataJSON?.() as { question?: string } | undefined;
      const answer = body?.question?.toLowerCase().includes("no approved source")
        || body?.question?.toLowerCase().includes("unlisted")
        ? escalatedPolicyAnswer
        : groundedPolicyAnswer;
      await fulfillJson(route, answer);
      return;
    }

    if (route.request().method() !== "GET") {
      await fulfillJson(route, { status: "Accepted", correlationId: "visual-fixture" });
      return;
    }

    const exact = responses[path];
    if (exact !== undefined) {
      await fulfillJson(route, exact);
      return;
    }

    if (path.startsWith("/api/compliance/cases/") && path.endsWith("/audit-pack")) {
      await fulfillJson(route, {
        caseId,
        status: "ReadyForReview",
        auditEvents: responses["/api/compliance/audit-events"],
        documents,
        assistantActions,
        approvals,
        customerUpdates: responses[`${"/api/cases/"}${caseId}/customer-updates`]
      });
      return;
    }

    await fulfillJson(route, []);
  });
}

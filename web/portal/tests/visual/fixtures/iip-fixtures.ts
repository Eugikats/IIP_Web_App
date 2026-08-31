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

const claimRows = [claim, readyClaim, breachedClaim].map((item) => ({
  claimId: item.caseId,
  customerName: item.customerName,
  policyNumber: item.policyNumber,
  productLine: item.productLine,
  claimType: item.claimType,
  channel: item.channel,
  status: item.status,
  completenessScore: item.completenessScore,
  riskLevel: item.riskLevel,
  priority: item.priority,
  slaStatus: item.slaStatus,
  slaDueAt: item.slaDueAt,
  assignedUserId: item.assignedUserId,
  assignedTeam: item.assignedTeam,
  updatedAt: item.updatedAt
}));

const claimDetails = Object.fromEntries(
  [claim, readyClaim, breachedClaim].map((item) => [
    `/api/v1/claims/${item.caseId}`,
    {
      ...item,
      claimId: item.caseId,
      documents: item.caseId === caseId ? [
        { documentId: "DOC-2026-00214", documentType: "ClaimForm", processingStatus: "ExtractionApproved", classificationConfidence: 0.91 },
        { documentId: "DOC-2026-00215", documentType: "GarageEstimate", processingStatus: "VerificationRequired", classificationConfidence: 0.63 }
      ] : []
    }
  ])
);

const documents = [
  {
    documentId: "DOC-2026-00214",
    caseId,
    originalFileName: "claim_form.pdf",
    contentType: "application/pdf",
    fileSizeBytes: 245000,
    documentType: "ClaimForm",
    processingStatus: "ExtractionApproved",
    confidenceScore: 0.91,
    fields: [
      { name: "claimant", extractedValue: "Sarah N.", effectiveValue: "Sarah N.", correctedValue: null, confidenceScore: 0.96, verificationStatus: "Approved", sensitivity: "Personal", warnings: [], correctedAt: null, correctionReference: null },
      { name: "incidentDate", extractedValue: "2026-05-30", effectiveValue: "2026-05-30", correctedValue: null, confidenceScore: 0.91, verificationStatus: "Approved", sensitivity: "Operational", warnings: [], correctedAt: null, correctionReference: null }
    ],
    validationWarnings: [],
    extractionApproved: true,
    uploadedAt: now,
    processedAt: now,
    correlationId: "visual-document-214",
    previewUrl: `/api/v1/cases/${caseId}/documents/DOC-2026-00214/content`,
    allowedActions: [],
    blockedActions: [
      { action: "approveDocumentExtraction", label: "Approve extraction", requiredPermission: "UploadDocument", reason: "This extraction has already been approved." },
      { action: "correctDocumentExtraction", label: "Correct extracted field", requiredPermission: "UploadDocument", reason: "Approved extractions are locked and cannot be corrected." }
    ]
  },
  {
    documentId: "DOC-2026-00215",
    caseId,
    originalFileName: "repair_estimate.pdf",
    contentType: "application/pdf",
    fileSizeBytes: 198000,
    documentType: "GarageEstimate",
    processingStatus: "VerificationRequired",
    confidenceScore: 0.63,
    fields: [
      { name: "amount", extractedValue: "UGX 18,500,000", effectiveValue: "UGX 18,500,000", correctedValue: null, confidenceScore: 0.63, verificationStatus: "RequiresReview", sensitivity: "Financial", warnings: [{ code: "LOW_CONFIDENCE_AMOUNT", message: "Amount requires human verification against the source.", severity: "Warning", fieldName: "amount", expectedValue: null, observedValue: "UGX 18,500,000" }], correctedAt: null, correctionReference: null },
      { name: "garageName", extractedValue: "Kampala Motor Works", effectiveValue: "Kampala Motor Works", correctedValue: null, confidenceScore: 0.89, verificationStatus: "Extracted", sensitivity: "Operational", warnings: [], correctedAt: null, correctionReference: null }
    ],
    validationWarnings: [{ code: "LOW_CONFIDENCE_AMOUNT", message: "Amount requires human verification against the source.", severity: "Warning", fieldName: "amount", expectedValue: null, observedValue: "UGX 18,500,000" }],
    extractionApproved: false,
    uploadedAt: now,
    processedAt: now,
    correlationId: "visual-document-215",
    previewUrl: `/api/v1/cases/${caseId}/documents/DOC-2026-00215/content`,
    allowedActions: [
      { action: "reclassifyDocument", label: "Reclassify document", requiredPermission: "UploadDocument", reason: null },
      { action: "approveDocumentExtraction", label: "Approve extraction", requiredPermission: "UploadDocument", reason: null },
      { action: "correctDocumentExtraction", label: "Correct extracted field", requiredPermission: "UploadDocument", reason: null }
    ],
    blockedActions: []
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
    updatedAt: now,
    allowedActions: [
      { action: "approveApproval", label: "Approve workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
      { action: "rejectApproval", label: "Reject workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
      { action: "escalateApproval", label: "Escalate workflow step", requiredPermission: "ApproveSettlementWorkflowStep", reason: null },
      { action: "overrideApproval", label: "Override AI recommendation", requiredPermission: "OverrideAiRecommendation", reason: null }
    ],
    blockedActions: [
      { action: "executeApprovedNextStep", label: "Execute approved next step", requiredPermission: "ApproveSettlementWorkflowStep", reason: "Only approved or overridden approval requests can execute a workflow next step." }
    ]
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
  "/api/v1/claims": claimRows,
  ...claimDetails,
  "/api/v1/cases": [claim, readyClaim, breachedClaim],
  [`/api/v1/cases/${caseId}`]: claim,
  [`/api/v1/cases/${caseId}/documents`]: documents,
  [`/api/v1/cases/${caseId}/document-checklist`]: checklist,
  [`/api/v1/cases/${caseId}/recommended-actions`]: [
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
  [`/api/v1/cases/${caseId}/timeline`]: [
    { eventId: "TIM-001", caseId, eventType: "ClaimCreated", actor: "Sarah N.", timestamp: now, summary: "Synthetic claim submitted." },
    { eventId: "TIM-002", caseId, eventType: "DocumentUploaded", actor: "Jane Namusoke", timestamp: now, summary: "Repair estimate requires verification." }
  ],
  [`/api/v1/cases/${caseId}/customer-updates`]: [
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
  [`/api/v1/cases/${caseId}/communications`]: [],
  [`/api/v1/assistant/actions/${caseId}`]: [
    {
      actionLogId: "AI-2026-00091",
      caseId,
      actionType: "MissingEvidence",
      summary: "Police report has not been received for this motor accident claim.",
      confidenceScore: 0.94,
      createdAt: now
    }
  ],
  "/api/v1/assistant/actions": assistantActions,
  "/api/v1/workflow/tasks": [
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
  "/api/v1/my-work": {
    ownership: "Mine",
    returnedCount: 3,
    hasMore: false,
    summary: { total: 3, overdue: 1, unassigned: 0, approvalRequired: 1 },
    items: [
      {
        id: "WorkflowTask:TSK-2026-00071",
        sourceType: "WorkflowTask",
        sourceId: "TSK-2026-00071",
        caseId,
        title: "Verify repair estimate",
        requiredAction: "Open task",
        sourceStatus: "Open",
        state: "Ready",
        ownerUserId: "usr-visual-supervisor",
        ownerTeam: "Motor Claims",
        ownerBasis: "Workflow task assignment",
        priority: "High",
        dueAt: now,
        dueState: "Overdue",
        evidenceReadiness: "Gaps",
        approvalRequired: false,
        href: `/app/claims/${caseId}?taskId=TSK-2026-00071`,
        updatedAt: now
      },
      {
        id: "Approval:APP-2026-00031",
        sourceType: "Approval",
        sourceId: "APP-2026-00031",
        caseId,
        title: "Supervisor review",
        requiredAction: "Review exact proposed action",
        sourceStatus: "Pending",
        state: "Ready",
        ownerUserId: "usr-visual-supervisor",
        ownerTeam: null,
        ownerBasis: "Approval reviewer",
        priority: "High",
        dueAt: now,
        dueState: "DueSoon",
        evidenceReadiness: "Gaps",
        approvalRequired: true,
        href: "/app/work-items?view=approvals&approvalId=APP-2026-00031",
        updatedAt: now
      },
      {
        id: "DocumentReview:DOC-2026-00491",
        sourceType: "DocumentReview",
        sourceId: "DOC-2026-00491",
        caseId,
        title: "Review repair-estimate.pdf",
        requiredAction: "Review extracted fields and source evidence",
        sourceStatus: "ReviewRequired",
        state: "Ready",
        ownerUserId: "usr-visual-supervisor",
        ownerTeam: "Motor Claims",
        ownerBasis: "Case assignment",
        priority: "High",
        dueAt: now,
        dueState: "DueSoon",
        evidenceReadiness: "Gaps",
        approvalRequired: false,
        href: `/app/claims/${caseId}/extraction?documentId=DOC-2026-00491`,
        updatedAt: now
      }
    ]
  },
  "/api/v1/approvals": approvals,
  "/api/v1/reports/operations-summary": {
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
  "/api/v1/admin/provider-modes": providerModes,
  "/api/v1/admin/provider-health": providerHealth,
  "/api/v1/provider-status": providerStatus,
  "/api/v1/system/capabilities": systemCapabilities,
  "/api/v1/integrations/email/health": emailIntegrationHealth,
  "/api/v1/onboarding": [
    {
      onboardingId: "ONB-VISUAL-01",
      customerName: "Uganda Insurer Ltd.",
      expectedEntraTenantOrDomain: "ugandainsurer.onmicrosoft.com",
      adminContactEmail: "it-admin@ugandainsurer.test",
      targetWorkspaces: ["workspace-claims"],
      azureSubscriptionIds: ["00000000-0000-0000-0000-000000000001"],
      internalTenantId: "tenant-visual-insurer",
      status: "Pending",
      externalEntraTenantId: null,
      createdAt: now,
      consentedAt: null
    }
  ],
  "/api/v1/onboarding/ONB-VISUAL-01/consent-url": {
    configured: false,
    url: null,
    message: "Microsoft Entra admin consent is not configured for this visual fixture."
  },
  "/api/v1/knowledge-documents": [knowledgeDocument],
  [`/api/v1/knowledge-documents/${knowledgeDocument.knowledgeDocumentId}`]: knowledgeDetail,
  "/api/v1/admin/tenant-configuration": {
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
  "/api/v1/admin/users": [
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
  "/api/v1/admin/roles": [
    { role: "ClaimsSupervisor", permissions: ["ViewCommandCenter", "ViewAllClaims", "ApproveSettlementWorkflowStep"] }
  ],
  "/api/v1/integrations/email/mailboxes": [],
  "/api/v1/integrations/email/messages": [],
  "/api/v1/communications/history": [],
  "/api/v1/compliance/audit-events": [
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
          source: "Development",
          environment: {
            name: "Local Development",
            reviewMode: "LocalDevelopment",
            syntheticData: true,
            dataClassification: "Synthetic visual test data"
          },
          availableWorkspaces: [
            { workspaceId: "workspace-visual-fixture", name: "Kampala Motor Claims", kind: "claims" }
          ]
        });
        return;
      }
    }

    if (route.request().method() === "POST" && path === "/api/v1/assistant/policy-questions") {
      const body = route.request().postDataJSON?.() as { question?: string } | undefined;
      const answer = body?.question?.toLowerCase().includes("no approved source")
        || body?.question?.toLowerCase().includes("unlisted")
        ? escalatedPolicyAnswer
        : groundedPolicyAnswer;
      await fulfillJson(route, answer);
      return;
    }

    if (route.request().method() === "POST" && path === "/api/v1/command-center/query") {
      await fulfillJson(route, {
        blocks: [
          {
            type: "text",
            markdown: "Claims needing attention are shown below for human review."
          },
          {
            type: "claims_table",
            rows: [
              {
                caseId,
                customer: "Sarah N.",
                productLine: "Motor",
                status: "UnderReview",
                slaStatus: "breached",
                completeness: 72,
                assignedTo: "Jane Namusoke",
                href: `/app/claims/${caseId}`
              }
            ]
          },
          {
            type: "document_gap_summary",
            items: [
              {
                caseId,
                customer: "Sarah N.",
                missing: ["Police report", "Damage photographs"]
              }
            ]
          },
          {
            type: "not_configured",
            provider: "Azure OpenAI",
            adminAction: "Configure Azure OpenAI before production AI generation."
          }
        ],
        canTriggerDecision: false,
        generatedAt: now
      });
      return;
    }

    if (route.request().method() !== "GET") {
      await fulfillJson(route, { status: "Accepted", correlationId: "visual-fixture" });
      return;
    }

    if (path.startsWith(`/api/v1/cases/${caseId}/documents/`) && path.endsWith("/content")) {
      await route.fulfill({
        status: 200,
        contentType: "image/svg+xml",
        body: `<svg xmlns="http://www.w3.org/2000/svg" width="900" height="1200" viewBox="0 0 900 1200"><rect width="900" height="1200" fill="#fff"/><rect x="48" y="48" width="804" height="1104" rx="20" fill="#f8faff" stroke="#d7deee"/><text x="90" y="120" font-family="Arial" font-size="30" font-weight="700" fill="#0b0f3b">Authorized source preview</text><text x="90" y="175" font-family="Arial" font-size="20" fill="#526070">Garage estimate · visual verification fixture</text><line x1="90" x2="810" y1="220" y2="220" stroke="#d7deee"/><text x="90" y="290" font-family="Arial" font-size="22" fill="#0b0f3b">Garage</text><text x="350" y="290" font-family="Arial" font-size="22" fill="#29334d">Kampala Motor Works</text><text x="90" y="350" font-family="Arial" font-size="22" fill="#0b0f3b">Estimate amount</text><text x="350" y="350" font-family="Arial" font-size="22" fill="#29334d">UGX 18,500,000</text><text x="90" y="430" font-family="Arial" font-size="17" fill="#6b7280">Visual test evidence only. Production loads the tenant-scoped stored source.</text></svg>`,
      });
      return;
    }

    const exact = responses[path];
    if (exact !== undefined) {
      await fulfillJson(route, exact);
      return;
    }

    if (path.startsWith("/api/v1/compliance/cases/") && path.endsWith("/audit-pack")) {
      await fulfillJson(route, {
        caseId,
        status: "ReadyForReview",
        auditEvents: responses["/api/v1/compliance/audit-events"],
        documents,
        assistantActions,
        approvals,
        customerUpdates: responses[`${"/api/v1/cases/"}${caseId}/customer-updates`]
      });
      return;
    }

    await fulfillJson(route, []);
  });
}

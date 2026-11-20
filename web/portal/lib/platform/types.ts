/** Shapes returned by /api/v1/org-platform/* (camelCase JSON of the API records). */

export type PlatformMe = {
  userId: string;
  name: string;
  email: string;
  role: string;
  isOrgAdmin: boolean;
  canApproveAutomations: boolean;
  accessMode: "open" | "strict";
  organisationName: string;
  primaryWorkspaceId: string;
};

export type ConnectorState = { key: string; name: string; status: string };

export type PlatformWorkspace = {
  id: string;
  templateKey: string;
  templateName: string;
  name: string;
  department: string | null;
  purpose: string | null;
  status: string;
  route: string | null;
  isPrimary: boolean;
  kpis: string[];
  connectors: ConnectorState[];
  members: string[];
  ownerUserId: string | null;
  automations: number;
  openInboxItems: number;
  createdAt: string;
  updatedAt: string;
};

export type AutomationField = {
  key: string;
  label: string;
  kind: "number" | "text" | "time" | "textarea";
  default: unknown;
  unit: string | null;
  help: string | null;
};

export type AutomationTemplate = {
  key: string;
  name: string;
  category: string;
  description: string;
  objective: string;
  defaultTrigger: string;
  defaultSchedule: string;
  fields: AutomationField[];
  outputs: string[];
  dataUsed: string[];
  hasRunner: boolean;
};

export type WorkspaceTemplate = {
  key: string;
  name: string;
  department: string;
  category: string;
  purpose: string;
  kpis: string[];
  connectors: string[];
  reports: string[];
  automations: string[];
  isLiveTemplate: boolean;
};

export type ConnectorDefinition = { key: string; name: string; description: string };

export type PlatformTemplatesResponse = {
  workspaces: WorkspaceTemplate[];
  automations: AutomationTemplate[];
  connectors: ConnectorDefinition[];
};

export type PlatformAutomation = {
  id: string;
  workspaceId: string;
  workspaceName: string;
  templateKey: string;
  templateName: string;
  name: string;
  objective: string | null;
  trigger: { type?: string; schedule?: string; time?: string };
  logic: Record<string, unknown>;
  outputs: string[];
  recipients: string[];
  approvers: string[];
  policy: { requireApproval?: boolean; maxItemsPerRun?: number };
  status: string;
  version: number;
  testedCurrentVersion: boolean;
  approvedAt: string | null;
  nextRunAt: string | null;
  lastRunAt: string | null;
  lastRunStatus: string | null;
  lastRunMessage: string | null;
  hasRunner: boolean;
  allowedActions: string[];
  createdAt: string;
  updatedAt: string;
};

export type PlatformRun = {
  id: string;
  automationId: string;
  automationVersion: number;
  kind: "test" | "run";
  status: string;
  summary: string | null;
  findings: number;
  newFindings: number;
  evidence: { summary?: unknown; findings?: { key: string; title: string; severity: string }[] };
  triggeredBy: string;
  startedAt: string;
  finishedAt: string | null;
};

export type InboxItem = {
  id: string;
  kind: string;
  status: string;
  severity: string;
  title: string;
  body: string | null;
  workspaceId: string | null;
  workspaceName: string;
  automationId: string | null;
  automationName: string | null;
  intentId: string | null;
  source: string;
  occurrences: number;
  evidence: unknown;
  actions: string[];
  href: string | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  resolutionNote: string | null;
};

export type InboxCounts = {
  needsAttention: number;
  recommendations: number;
  awaitingApproval: number;
  monitoring: number;
  resolved: number;
  total: number;
};

export type PlatformIntent = {
  id: string;
  name: string;
  description: string | null;
  measure: string | null;
  target: string | null;
  status: string;
  health: "on_track" | "at_risk" | "off_track";
  openItems: number;
  highItems: number;
  latestSignal: string | null;
  latestSignalAt: string | null;
  automationIds: string[];
  workspaceId: string | null;
  createdAt: string;
};

export type PlatformCard = {
  id: string;
  kind: "workspace" | "inbox" | "intent" | "automation";
  kicker: string;
  title: string;
  summary: string;
  iconKey: string;
  tone: "blue" | "amber" | "red" | "green";
  count: number | null;
  askPrompt: string | null;
  href: string | null;
  source: string;
};

export type PlatformNotifications = { counts: InboxCounts; cards: PlatformCard[]; intents: PlatformIntent[] };

export type PlatformInsights = { cards: PlatformCard[]; source: string; generatedAt: string };

export type PlatformReport = {
  id: string;
  title: string;
  type: string;
  status: string;
  workspaceName: string;
  origin: string;
  createdBy: string | null;
  generatedAt: string | null;
  updatedAt: string;
  href: string | null;
};

export type PlatformConversation = {
  id: string;
  title: string;
  workspaceId: string;
  workspaceName: string;
  lastMessageAt: string;
  href: string;
};

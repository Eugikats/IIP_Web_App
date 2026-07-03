/**
 * Local fixtures for A2 block renderer development.
 * These are never fetched in production — used only to drive local render
 * during development before A3 wires the live /query endpoint.
 */
import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";

export const FIXTURE_BLOCKS: CommandCenterBlock[] = [
  {
    type: "text",
    markdown:
      "You have **3 overdue claims** requiring immediate attention and **2 approvals** pending your review. SLA compliance is currently at 87%.",
  },
  {
    type: "metric_pills",
    items: [
      { label: "Open Claims", value: "23", tone: "info" },
      { label: "SLA At Risk", value: "5", tone: "warning" },
      { label: "Overdue", value: "3", tone: "danger" },
      { label: "Pending Approvals", value: "2", tone: "warning" },
      { label: "Resolved Today", value: "8", tone: "success" },
    ],
  },
  {
    type: "claims_table",
    rows: [
      {
        caseId: "CLM-2024-0892",
        customer: "Acme Corp",
        productLine: "Motor Comprehensive",
        status: "Awaiting Documents",
        slaStatus: "breached",
        completeness: 60,
        assignedTo: "J. Achieng",
        href: "/cases/CLM-2024-0892",
      },
      {
        caseId: "CLM-2024-0890",
        customer: "Beta Industries",
        productLine: "Property",
        status: "Under Review",
        slaStatus: "at_risk",
        completeness: 85,
        assignedTo: "M. Nantongo",
        href: "/cases/CLM-2024-0890",
      },
      {
        caseId: "CLM-2024-0885",
        customer: "Gamma Health",
        productLine: "Health",
        status: "Approved",
        slaStatus: "on_track",
        completeness: 100,
        assignedTo: "P. Okello",
        href: "/cases/CLM-2024-0885",
      },
    ],
  },
  {
    type: "sla_summary",
    items: [
      { caseId: "CLM-2024-0892", customer: "Acme Corp", slaStatus: "breached", dueLabel: "5 days overdue" },
      { caseId: "CLM-2024-0890", customer: "Beta Industries", slaStatus: "at_risk", dueLabel: "Due today" },
      { caseId: "CLM-2024-0888", customer: "Delta Motors", slaStatus: "due_today", dueLabel: "Due in 2 hours" },
    ],
  },
  {
    type: "document_gap_summary",
    items: [
      {
        caseId: "CLM-2024-0892",
        customer: "Acme Corp",
        missing: ["Vehicle Photos", "Witness Statement"],
      },
      {
        caseId: "CLM-2024-0888",
        customer: "Delta Motors",
        missing: ["Repair Quotation"],
      },
    ],
  },
  {
    type: "approval_queue",
    items: [
      {
        approvalId: "APR-2024-0042",
        caseId: "CLM-2024-0885",
        title: "Settlement approval — Gamma Health — UGX 4.2M",
        status: "Pending Review",
        href: "/approvals/APR-2024-0042",
      },
      {
        approvalId: "APR-2024-0041",
        caseId: "CLM-2024-0880",
        title: "Customer communication approval — Omega Life",
        status: "Pending Review",
        href: "/approvals/APR-2024-0041",
      },
    ],
  },
  {
    type: "action_proposal",
    actions: [
      {
        label: "Request missing documents for CLM-2024-0892",
        description:
          "Vehicle photos and witness statement are outstanding. Sending a document request now would prevent SLA breach.",
        targetHref: "/cases/CLM-2024-0892",
        requiresHumanApproval: true,
      },
      {
        label: "Review settlement for CLM-2024-0885",
        description:
          "The settlement pack is complete and ready for your approval decision.",
        targetHref: "/approvals/APR-2024-0042",
        requiresHumanApproval: true,
      },
    ],
  },
  {
    type: "evidence_list",
    sources: [
      {
        label: "Motor Comprehensive Policy — Section 4",
        documentName: "Motor-Comprehensive-Policy-v3.pdf",
        ref: "doc:motor-comp-v3#section-4",
      },
      {
        label: "Claims Handling SOP — SLA Framework",
        documentName: "Claims-SOP-2024.pdf",
        ref: "doc:claims-sop-2024#sla",
      },
    ],
  },
  {
    type: "not_configured",
    provider: "WhatsApp Business API",
    adminAction: "Connect a WhatsApp Business number in Integrations → Channels.",
  },
  {
    type: "error",
    code: "PLANNER_TIMEOUT",
    safeMessage: "The query planner did not respond in time. Please try again.",
    correlationId: "corr-fixture-0001",
  },
];

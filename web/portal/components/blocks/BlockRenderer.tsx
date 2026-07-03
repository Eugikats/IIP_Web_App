"use client";

import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { TextBlock } from "./TextBlock";
import { MetricPillsBlock } from "./MetricPillsBlock";
import { ClaimsTableBlock } from "./ClaimsTableBlock";
import { SlaSummaryBlock } from "./SlaSummaryBlock";
import { DocumentGapSummaryBlock } from "./DocumentGapSummaryBlock";
import { ApprovalQueueBlock } from "./ApprovalQueueBlock";
import { DraftCommunicationBlock } from "./DraftCommunicationBlock";
import { ManagementSummaryBlock } from "./ManagementSummaryBlock";
import { ActionProposalBlock } from "./ActionProposalBlock";
import { EvidenceListBlock } from "./EvidenceListBlock";
import { NotConfiguredBlock } from "./NotConfiguredBlock";
import { ErrorBlock } from "./ErrorBlock";

interface BlockRendererProps {
  block: CommandCenterBlock;
}

/**
 * Dispatcher: routes each block to its dedicated presentational renderer.
 * Falls through to null for unknown types so unrecognised shapes never
 * surface as runtime crashes — the parser in commandCenterBlocks.ts already
 * rejects them, so this is a belt-and-suspenders guard.
 */
export function BlockRenderer({ block }: BlockRendererProps) {
  switch (block.type) {
    case "text":
      return <TextBlock block={block} />;
    case "metric_pills":
      return <MetricPillsBlock block={block} />;
    case "claims_table":
      return <ClaimsTableBlock block={block} />;
    case "sla_summary":
      return <SlaSummaryBlock block={block} />;
    case "document_gap_summary":
      return <DocumentGapSummaryBlock block={block} />;
    case "approval_queue":
      return <ApprovalQueueBlock block={block} />;
    case "draft_communication":
      return <DraftCommunicationBlock block={block} />;
    case "management_summary":
      return <ManagementSummaryBlock block={block} />;
    case "action_proposal":
      return <ActionProposalBlock block={block} />;
    case "evidence_list":
      return <EvidenceListBlock block={block} />;
    case "not_configured":
      return <NotConfiguredBlock block={block} />;
    case "error":
      return <ErrorBlock block={block} />;
    default:
      return null;
  }
}

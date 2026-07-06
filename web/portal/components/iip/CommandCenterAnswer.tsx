"use client";
import { AlertTriangle, FileText, ShieldCheck, Sparkles } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { Badge } from "@/components/iip/Primitives";
import type { CommandCenterAnswer, CommandCenterBlock } from "@/lib/lovable-parity/data/commandCenterQuery";

// Renders one governed answer from POST /api/v1/command-center/query. Every block type has an
// explicit renderer; anything unknown falls through to a safe "unsupported" note (never raw JSON).
function Block({ block }: { block: CommandCenterBlock }) {
  switch (block.type) {
    case "text":
      return <p className="text-[13.5px] text-slate2 leading-relaxed">{String((block as { markdown?: string }).markdown ?? "")}</p>;
    case "metric_pills":
      return (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-2.5">
          {(block as Extract<CommandCenterBlock, { type: "metric_pills" }>).items.map((m, i) => (
            <div key={i} className="glass-subtle p-3">
              <div className="text-[11px] text-stone">{m.label}</div>
              <div className="mt-1 text-[20px] font-semibold text-navy">{m.value}</div>
              {m.detail ? <div className="text-[10.5px] text-stone mt-0.5">{m.detail}</div> : null}
            </div>
          ))}
        </div>
      );
    case "claims_table":
      return (
        <div className="divide-y divide-cloud/80">
          {(block as Extract<CommandCenterBlock, { type: "claims_table" }>).rows.map((r) => (
            <Link key={r.caseId} to={r.href || "/app/claims"} className="py-2.5 flex items-center gap-3 hover:bg-white/50 rounded-lg px-1">
              <div className="w-28 shrink-0 text-[12px] font-medium text-navy truncate">{r.caseId}</div>
              <div className="flex-1 min-w-0 text-[12.5px] text-navy truncate">{r.customer}</div>
              <div className="text-[11.5px] text-stone hidden md:block">{r.productLine}</div>
              <Badge tone="neutral">{r.status}</Badge>
            </Link>
          ))}
        </div>
      );
    case "sla_summary":
      return (
        <div className="space-y-1.5">
          {(block as Extract<CommandCenterBlock, { type: "sla_summary" }>).items.map((s) => (
            <div key={s.caseId} className="flex items-center justify-between text-[12.5px]">
              <span className="text-navy">{s.caseId} · {s.customer}</span>
              <Badge tone={s.slaStatus === "breached" ? "error" : s.slaStatus === "at_risk" ? "warning" : "neutral"}>{s.dueLabel}</Badge>
            </div>
          ))}
        </div>
      );
    case "document_gap_summary":
      return (
        <div className="space-y-2">
          {(block as Extract<CommandCenterBlock, { type: "document_gap_summary" }>).items.map((d) => (
            <div key={d.caseId} className="text-[12.5px]">
              <span className="text-navy font-medium">{d.caseId} · {d.customer}</span>
              <span className="text-stone"> — missing: {d.missing.join(", ")}</span>
            </div>
          ))}
        </div>
      );
    case "approval_queue":
      return (
        <div className="divide-y divide-cloud/80">
          {(block as Extract<CommandCenterBlock, { type: "approval_queue" }>).items.map((a) => (
            <Link key={a.approvalId} to={a.href || "/app/workflows"} className="py-2.5 flex items-center justify-between hover:bg-white/50 rounded-lg px-1">
              <span className="text-[12.5px] text-navy">{a.title}</span>
              <Badge tone="neutral">{a.status}</Badge>
            </Link>
          ))}
        </div>
      );
    case "management_summary":
      return (
        <div>
          <div className="text-[13px] font-semibold text-navy mb-2">{(block as Extract<CommandCenterBlock, { type: "management_summary" }>).title}</div>
          {(block as Extract<CommandCenterBlock, { type: "management_summary" }>).sections.map((s, i) => (
            <div key={i} className="mb-2"><div className="text-[12px] font-medium text-navy">{s.heading}</div><div className="text-[12.5px] text-slate2">{s.body}</div></div>
          ))}
        </div>
      );
    case "draft_communication": {
      const b = block as Extract<CommandCenterBlock, { type: "draft_communication" }>;
      return (
        <div className="rounded-xl border border-cloud bg-white/70 p-3">
          <div className="flex items-center gap-2 mb-1.5"><Sparkles className="h-3.5 w-3.5 text-violet2" /><span className="text-[11px] uppercase tracking-[0.12em] text-stone">Draft communication</span>{b.requiresApproval ? <Badge tone="warning">Human approval required</Badge> : null}</div>
          <div className="text-[12.5px] text-slate2 whitespace-pre-wrap">{b.preview}</div>
        </div>
      );
    }
    case "action_proposal":
      return (
        <div className="space-y-2">
          {(block as Extract<CommandCenterBlock, { type: "action_proposal" }>).actions.map((a, i) => (
            <div key={i} className="flex items-center justify-between gap-3 rounded-xl border border-cloud bg-white/70 px-3 py-2.5">
              <div><div className="text-[12.5px] text-navy">{a.label}</div><div className="text-[11.5px] text-stone">{a.description}</div></div>
              <Link to={a.targetHref || "/app"} className="text-[12px] text-info hover:underline shrink-0">Review →</Link>
            </div>
          ))}
        </div>
      );
    case "evidence_list":
      return (
        <div className="space-y-1">
          {(block as Extract<CommandCenterBlock, { type: "evidence_list" }>).sources.map((s, i) => (
            <div key={i} className="flex items-center gap-2 text-[12px] text-stone"><FileText className="h-3.5 w-3.5" />{s.label}: {s.documentName}</div>
          ))}
        </div>
      );
    case "not_configured": {
      const b = block as Extract<CommandCenterBlock, { type: "not_configured" }>;
      return (
        <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-[12.5px] text-amber-900">
          <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" /><div><span className="font-medium">{b.provider} not configured.</span> {b.adminAction}</div>
        </div>
      );
    }
    case "error": {
      const b = block as Extract<CommandCenterBlock, { type: "error" }>;
      return (
        <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-[12.5px] text-red-800">
          <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" /><div>{b.safeMessage}<div className="text-[10.5px] text-red-600 mt-0.5 font-mono">ref {b.correlationId}</div></div>
        </div>
      );
    }
    default:
      return <div className="text-[12px] text-stone italic">Unsupported response block ({String(block.type)}) — nothing rendered.</div>;
  }
}

export default function CommandCenterAnswerView({ answer }: { answer: CommandCenterAnswer }) {
  return (
    <div className="glass-elevated p-5 mb-8 space-y-4">
      <div className="flex items-center gap-2 text-[11px] uppercase tracking-[0.14em] text-stone">
        <ShieldCheck className="h-3.5 w-3.5 text-info" /> Prepared for review · AI does not decide
      </div>
      <div className="text-[12.5px] text-navy font-medium">“{answer.question}”</div>
      <div className="space-y-4">
        {answer.blocks.map((b, i) => <Block key={i} block={b} />)}
      </div>
    </div>
  );
}

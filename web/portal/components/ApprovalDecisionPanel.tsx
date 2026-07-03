"use client";

import type { ReactNode } from "react";
import { useState } from "react";
import { ConfirmationAuditNotice, GovernedButton } from "./GovernedUi";
import { AlertTriangle, CheckCircle2, GitPullRequestArrow, ShieldAlert } from "lucide-react";

export function ApprovalDecisionPanel({
  onDecision,
  disabled = false,
  allowedDecisions
}: {
  onDecision: (decision: "approve" | "reject" | "override" | "escalate", comments: string) => Promise<void>;
  disabled?: boolean;
  allowedDecisions?: Partial<Record<"approve" | "reject" | "override" | "escalate", boolean>>;
}) {
  const [comments, setComments] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  async function submit(decision: "approve" | "reject" | "override" | "escalate") {
    const allowed = allowedDecisions?.[decision] ?? true;
    const trimmedComments = comments.trim();

    if (!allowed) {
      return;
    }

    if (trimmedComments.length < 8) {
      setValidationError("Reviewer comments are required for every workflow decision before confirmation.");
      return;
    }

    setValidationError(null);
    setBusy(decision);
    try {
      await onDecision(decision, trimmedComments);
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="space-y-4">
      <textarea
        className="min-h-24 w-full rounded-xl border border-line bg-shell px-3 py-2 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono"
        placeholder="Reviewer comments required before confirming a workflow decision"
        value={comments}
        onChange={(event) => setComments(event.target.value)}
      />
      {validationError ? <p className="rounded-xl border border-danger/30 bg-danger/10 px-3 py-2 text-xs font-semibold text-danger">{validationError}</p> : null}
      <div className="grid grid-cols-2 gap-2">
        {(["approve", "reject", "override", "escalate"] as const).map((decision) => {
          const allowed = allowedDecisions?.[decision] ?? true;

          return (
          <button
            key={decision}
            className={`rounded-full px-3 py-2.5 text-sm font-semibold transition-all ${
              decision === "approve"
                ? "bg-azure border border-azure/40 text-white shadow-glow-cyan hover:brightness-110"
                : "border border-line bg-surface text-muted hover:border-accent/50 hover:text-ink"
            } disabled:cursor-not-allowed disabled:opacity-50`}
            disabled={disabled || busy !== null || !allowed}
            onClick={() => submit(decision)}
          >
            {busy === decision ? "Preparing..." : decisionLabel(decision)}
          </button>
          );
        })}
      </div>
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">These decisions update workflow state only. They do not settle, deny, repudiate, or pay a claim.</p>
    </div>
  );
}

function decisionLabel(decision: "approve" | "reject" | "override" | "escalate") {
  switch (decision) {
    case "approve":
      return "Approve step";
    case "reject":
      return "Reject step";
    case "override":
      return "Record override";
    case "escalate":
      return "Escalate step";
  }
}

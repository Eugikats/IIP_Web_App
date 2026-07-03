"use client";

import { FormEvent, useMemo, useState } from "react";
import Link from "next/link";
import { useRole } from "@/components/RoleContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { allowsAction, blockedActionReason, hasActionState, type ActionStateContract } from "@/lib/actionState";
import { motion } from "framer-motion";

type CreatedCase = ActionStateContract & {
  caseId: string;
  status: string;
  correlationId: string;
  audit: {
    eventCount: number;
    lastActionName: string | null;
  };
};

export function NewCaseForm() {
  const { role, tenantId, userId, workspaceId } = useRole();
  const [createdCase, setCreatedCase] = useState<CreatedCase | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const today = useMemo(() => new Date().toISOString().slice(0, 10), []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setCreatedCase(null);

    const formData = new FormData(event.currentTarget);
    if (!tenantId || !workspaceId || !userId) {
      setError("Runtime context is not configured. Sign in or configure tenant and workspace context before creating a case.");
      setIsSubmitting(false);
      return;
    }

    const payload = {
      customerName: String(formData.get("customerName") ?? ""),
      customerEmail: String(formData.get("customerEmail") ?? ""),
      customerPhone: String(formData.get("customerPhone") ?? ""),
      policyNumber: String(formData.get("policyNumber") ?? ""),
      productLine: String(formData.get("productLine") ?? ""),
      claimType: String(formData.get("claimType") ?? ""),
      channel: String(formData.get("channel") ?? ""),
      incidentDate: String(formData.get("incidentDate") ?? ""),
      incidentDescription: String(formData.get("incidentDescription") ?? "")
    };

    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases`, {
        method: "POST",
        headers: {
          ...scopedHeaders(role, userId),
          "Content-Type": "application/json",
          "X-Correlation-ID": `portal-${Date.now()}`
        },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        throw new Error(`Case intake failed with HTTP ${response.status}.`);
      }

      const responsePayload: unknown = await response.json();
      if (!hasActionState(responsePayload)) {
        throw new Error("Case intake response did not include required backend action authorization state.");
      }

      setCreatedCase(responsePayload as CreatedCase);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Case intake failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }} className="grid gap-6 lg:grid-cols-[1fr_360px]">
      <form className="rounded-xl border border-line bg-mist p-6" onSubmit={submit}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Customer name" name="customerName" defaultValue="" />
          <Field label="Customer email" name="customerEmail" defaultValue="" type="email" />
          <Field label="Customer phone" name="customerPhone" defaultValue="" />
          <Field label="Policy number" name="policyNumber" defaultValue="" />
          <Field label="Product line" name="productLine" defaultValue="" />
          <Field label="Claim type" name="claimType" defaultValue="" />
          <Field label="Channel" name="channel" defaultValue="" />
          <Field label="Incident date" name="incidentDate" defaultValue={today} type="date" />
          <label className="md:col-span-2 block">
            <span className="text-sm font-medium text-muted">Incident description</span>
            <textarea
              className="mt-1 min-h-[120px] w-full rounded-xl border border-line bg-shell px-4 py-3 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono"
              name="incidentDescription"
              required
            />
          </label>
        </div>
        <div className="mt-6 flex items-center justify-end">
          <button
            className="rounded-full bg-azure border border-azure/40 px-5 py-2.5 text-sm font-semibold text-white shadow-glow-cyan hover:brightness-110 transition-all disabled:cursor-not-allowed disabled:opacity-50"
            disabled={isSubmitting}
            type="submit"
          >
            {isSubmitting ? "Creating..." : "Create case"}
          </button>
        </div>
      </form>

      <aside className="rounded-xl border border-line bg-mist p-6 flex flex-col gap-4">
        <h2 className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Intake result</h2>
        {error ? <p className="rounded-xl border border-danger/30 bg-danger/10 p-4 text-sm text-danger shadow-glow-danger">{error}</p> : null}
        {createdCase ? (
          <div className="space-y-4 text-sm mt-2">
            <div className="rounded-xl border border-line bg-surface/50 p-3">
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Case ID</p>
              <p className="mt-2 break-all font-medium text-ink font-mono">{createdCase.caseId}</p>
            </div>
            <div className="rounded-xl border border-line bg-surface/50 p-3">
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Status</p>
              <p className="mt-2 font-medium text-ink">{createdCase.status}</p>
            </div>
            <div className="rounded-xl border border-line bg-surface/50 p-3">
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted">Audit</p>
              <p className="mt-2 font-medium text-ink">
                {createdCase.audit.eventCount} event, {createdCase.audit.lastActionName}
              </p>
            </div>
            {allowsAction(createdCase, "openCaseReview") ? (
              <Link
                className="mt-2 block text-center w-full rounded-full border border-accent/40 bg-accent/10 px-5 py-2.5 text-sm font-semibold text-accent hover:bg-accent hover:text-white shadow-glow-cyan transition-all"
                href={`/cases/${createdCase.caseId}`}
              >
                Open case detail
              </Link>
            ) : (
              <p className="rounded-xl border border-warning/30 bg-warning/10 p-3 text-sm text-warning">
                {blockedActionReason(createdCase, "openCaseReview") ?? "Case review is not available for this user."}
              </p>
            )}
          </div>
        ) : (
          <p className="text-sm text-muted">Submit a case to see the audit-backed response.</p>
        )}
      </aside>
    </motion.div>
  );
}

function Field({
  label,
  name,
  defaultValue,
  type = "text"
}: {
  label: string;
  name: string;
  defaultValue: string;
  type?: string;
}) {
  return (
    <label className="block">
      <span className="text-sm font-medium text-muted">{label}</span>
      <input
        className="mt-1 w-full rounded-xl border border-line bg-shell px-4 py-3 text-sm text-ink outline-none focus:border-accent focus:ring-1 focus:ring-accent transition-all font-mono"
        defaultValue={defaultValue}
        name={name}
        required={name !== "customerEmail" && name !== "customerPhone"}
        type={type}
      />
    </label>
  );
}

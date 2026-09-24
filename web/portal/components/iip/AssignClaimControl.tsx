"use client";
import { useState } from "react";
import { toast } from "sonner";
import { UserCheck } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { usePeople } from "@/lib/people";
import { Select } from "@/components/ui/Input";

// Assign a claim to a named person from the claim itself. Uses the same POST /cases/{id}/assign the
// claims queue already calls, so the audit trail and status change are identical wherever it is done.
// People are listed by name from the workspace directory, which fills as colleagues sign in.
export default function AssignClaimControl({
  caseId,
  currentAssigneeId,
  onAssigned,
}: {
  caseId: string;
  currentAssigneeId: string | null;
  onAssigned?: () => void | Promise<unknown>;
}) {
  const { roleDef } = useUI();
  const { people, currentUserId, loading, describe } = usePeople();
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState("");
  const [busy, setBusy] = useState(false);

  const options = [...people].sort((a, b) => {
    if (a.userId === currentUserId) return -1;
    if (b.userId === currentUserId) return 1;
    return a.name.localeCompare(b.name);
  });

  async function assign() {
    if (!selected || LOVABLE_VISUAL_PARITY_MODE) return;
    setBusy(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${encodeURIComponent(caseId)}/assign`, {
        method: "POST",
        headers: { ...scopedHeaders(roleDef.key, ""), "Content-Type": "application/json", "X-Audit-Reason": "Assigned from the claim page" },
        body: JSON.stringify({ assignedUserId: selected, auditReason: "Assigned from the claim page" }),
      });
      if (response.status === 403) {
        toast.error("Your role cannot assign this claim", { description: "A claims supervisor can assign claims to other people." });
        return;
      }
      if (!response.ok) {
        toast.error("The claim was not assigned", { description: "Nothing changed. Try again in a moment." });
        return;
      }
      toast.success("Claim assigned", { description: `It is now with ${describe(selected)}.` });
      setOpen(false);
      setSelected("");
      await onAssigned?.();
    } finally {
      setBusy(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="dls-focus-outline inline-flex items-center gap-1.5 rounded-dls-control border border-dls-border-input bg-dls-surface px-2.5 py-1.5 text-[12px] font-semibold text-dls-text-primary hover:bg-dls-bg-page"
      >
        <UserCheck className="h-3.5 w-3.5" aria-hidden="true" />
        {currentAssigneeId ? "Reassign" : "Assign"}
      </button>
    );
  }

  return (
    <div className="rounded-dls-control border border-dls-border-subtle bg-dls-bg-page p-2.5">
      <label htmlFor={`assign-${caseId}`} className="mb-1 block text-[11.5px] font-semibold text-dls-text-secondary">
        Assign this claim to
      </label>
      {loading ? (
        <div className="text-[12px] text-dls-text-muted">Loading people in this workspace.</div>
      ) : options.length === 0 ? (
        <div className="text-[12px] text-dls-text-muted">People appear here once they have signed in to IIP at least once.</div>
      ) : (
        <Select id={`assign-${caseId}`} value={selected} onChange={(event) => setSelected(event.target.value)} className="w-full text-[12.5px]">
          <option value="">Choose a person</option>
          {options.map((person) => (
            <option key={person.userId} value={person.userId} disabled={person.userId === currentAssigneeId}>
              {person.userId === currentUserId ? `Me (${person.name})` : person.name}
              {person.userId === currentAssigneeId ? " (already assigned)" : ""}
            </option>
          ))}
        </Select>
      )}
      <div className="mt-2 flex justify-end gap-1.5">
        <button type="button" onClick={() => { setOpen(false); setSelected(""); }} className="dls-focus-outline rounded-dls-control px-2.5 py-1.5 text-[12px] font-semibold text-dls-text-secondary hover:bg-dls-surface">
          Cancel
        </button>
        <button
          type="button"
          onClick={() => void assign()}
          disabled={!selected || busy}
          className="dls-focus-outline rounded-dls-control bg-dls-primary px-3 py-1.5 text-[12px] font-bold text-dls-text-inverse hover:bg-dls-primary-hover disabled:cursor-not-allowed disabled:opacity-50"
        >
          {busy ? "Assigning" : "Assign"}
        </button>
      </div>
    </div>
  );
}

"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

type VariantKey = "A" | "B" | "C";
type StateKey = "LOADING" | "EMPTY" | "PARTIAL" | "ERROR" | "PERMISSION" | "SUCCESS" | "RECOVERY";
type ViewportKey = "WIDE" | "INTERMEDIATE" | "COMPACT";
type NetworkKey = "NORMAL" | "LOW_BANDWIDTH" | "OFFLINE";

type Surface = {
  id: string;
  flow: string;
  title: string;
  purpose: string;
  pattern: string;
  entry: string;
  exit: string;
  concept: string;
  task: string;
  stages: [string, string, string, string];
  sample: [string, string, string, string];
};

const variants: Array<{ key: VariantKey; name: string }> = [
  { key: "A", name: "Guided sequence" },
  { key: "B", name: "Operations console" },
  { key: "C", name: "Role storyboard" },
];

const roles = [
  "Claims Officer",
  "Authorized Approver",
  "Customer Service / Complaints",
  "Compliance Officer",
  "Loss Assessor / Adjuster",
  "Policyholder / Claimant (mediated)",
  "Broker / Channel Submitter",
  "IT / Security Admin",
] as const;

const states: StateKey[] = ["LOADING", "EMPTY", "PARTIAL", "ERROR", "PERMISSION", "SUCCESS", "RECOVERY"];
const viewports: ViewportKey[] = ["WIDE", "INTERMEDIATE", "COMPACT"];
const networks: NetworkKey[] = ["NORMAL", "LOW_BANDWIDTH", "OFFLINE"];

const modelConcepts = [
  ["Case record", "Durable claim facts and current lifecycle state."],
  ["Work item", "An owned action that points to a record; completing it does not rewrite the record by itself."],
  ["Conversation", "Interaction history and working context; not evidence, permission, or a final decision."],
  ["Evidence", "A versioned source with provenance, eligibility, currentness, and review status."],
  ["Proposal", "An AI/rule-prepared next action; advisory until an eligible human decides."],
  ["Approval", "An accountable human outcome on one exact current proposal; distinct from later execution."],
  ["Audit event", "Immutable history of who did what and when; not the current business-object state."],
] as const;

const surfaces: Surface[] = [
  { id: "IA-SCR-001", flow: "Orientation", title: "Command Center", purpose: "Understand current work and reach the next owned task.", pattern: "Status regions + cues", entry: "App entry", exit: "Exact work item", concept: "Conversation → work item", task: "Find the evidence review that needs attention and open its owning task.", stages: ["Orient", "Ask or choose cue", "Check grounded result", "Open exact work"], sample: ["Recent analysis SYN-CONV-04", "Work item WI-011", "Case MOTOR-011", "Evidence review required"] },
  { id: "IA-SCR-002", flow: "FLW-R1-QUEUE-01", title: "My Work", purpose: "Find, open, hand off, and return to authorized work.", pattern: "CP-R1-QUEUE + CP-R1-TABLE", entry: "Navigation / handoff", exit: "Source-owned task", concept: "Work item", task: "Find the assigned evidence-conflict task and open the linked case without losing queue context.", stages: ["Filter owned work", "Compare state and evidence", "Open source task", "Return to queue context"], sample: ["WI-011 · evidence review", "MOTOR-011 · linked record", "Assigned to Claims Officer", "Conflict · action required"] },
  { id: "IA-SCR-003", flow: "Find / open claim", title: "Claims list", purpose: "Find and open only claims within current object scope.", pattern: "CP-R1-TABLE + return context", entry: "Navigation / search return", exit: "Permitted case workspace", concept: "Case record", task: "Find MOTOR-011 and distinguish its claim state from any pending work item.", stages: ["Search permitted records", "Check record identity", "Separate case and task state", "Open case workspace"], sample: ["MOTOR-011 · case record", "Own-damage · open", "Evidence completeness · partial", "Pending work · WI-011"] },
  { id: "IA-SCR-004", flow: "FLW-R1-INTAKE-01", title: "New claim / FNOL", purpose: "Create a bounded motor claim notification without duplicating work.", pattern: "Form + CP-R1-DIALOG", entry: "WhatsApp / manual handoff", exit: "Created case or manual review", concept: "Case record creation", task: "Enter a notification, resolve a possible duplicate, and reach the exact created case or manual-review queue.", stages: ["Identify channel submission", "Check prerequisites", "Resolve duplicate", "Create or route review"], sample: ["Submission SYN-IN-08", "Policy reference supplied", "Possible match MOTOR-009", "Manual duplicate review"] },
  { id: "IA-SCR-005", flow: "Case hub", title: "Case workspace", purpose: "Coordinate current facts, evidence, tasks, communications, and human decisions.", pattern: "CP-R1-PANEL + status regions", entry: "Queue / search", exit: "Evidence, approval, communication, export", concept: "Case record + linked objects", task: "Determine what is fact, evidence, pending work, conversation, and decision state for MOTOR-011.", stages: ["Confirm case identity", "Inspect linked objects", "Choose owned work", "Return with case context"], sample: ["Case MOTOR-011 · open", "Evidence set EV-03 · partial", "Work item WI-011 · assigned", "Conversation SYN-CONV-04 · context only"] },
  { id: "IA-SCR-006", flow: "FLW-R1-EVIDENCE-01", title: "Evidence review", purpose: "Compare source evidence with derived values and correct traceably.", pattern: "CP-R1-EVIDENCE", entry: "Case document", exit: "Reviewed evidence / re-evaluation", concept: "Evidence", task: "Identify a low-confidence value, compare the source, correct it, and understand what becomes stale.", stages: ["Open source version", "Compare derived value", "Correct with reason", "Review invalidation"] , sample: ["Evidence EV-03 · version 3", "Registration value · low confidence", "Source page 2 · textual reference", "Proposal PR-07 becomes stale"] },
  { id: "IA-SCR-005 (approval mode)", flow: "FLW-R1-DECISION-01", title: "Approval cockpit", purpose: "Enable an eligible human to decide one exact proposal using current evidence.", pattern: "CP-R1-APPROVAL + CP-R1-DIALOG", entry: "Approval queue", exit: "Human outcome / execution handoff", concept: "Proposal → human approval → audit", task: "Determine whether proposal PR-07 is current and record an accountable human outcome without confusing it with execution.", stages: ["Read exact proposal", "Check current evidence", "Verify authority", "Record human outcome"], sample: ["Proposal PR-07 · advisory", "Evidence set EV-03 · current", "Approver eligibility · confirmed", "Execution state · not started"] },
  { id: "IA-SCR-007", flow: "FLW-R1-COMMS-01", title: "Channel preflight", purpose: "Review minimum necessary disclosure before an external send attempt.", pattern: "Thread + preflight + status", entry: "Case communication", exit: "Provider attempt / reconciliation", concept: "Customer conversation", task: "Review a customer update, remove unnecessary disclosure, and distinguish accepted from delivered.", stages: ["Open linked thread", "Review disclosure", "Authorize send attempt", "Reconcile delivery"] , sample: ["Conversation WA-08", "Draft update · not sent", "Disclosure check · required", "Delivery state · unknown"] },
  { id: "IA-SCR-022", flow: "Audit review", title: "Audit trail", purpose: "Inspect correlated actions without exposing restricted metadata.", pattern: "CP-R1-TABLE + CP-R1-PANEL", entry: "Case / compliance navigation", exit: "Scoped audit detail", concept: "Audit event", task: "Reconstruct who changed evidence and who decided, while keeping audit history distinct from the business objects.", stages: ["Scope audit query", "Follow correlation", "Compare before and after", "Export permitted evidence"] , sample: ["Audit AUD-41", "Evidence correction EV-03", "Human outcome APR-02", "Correlation SYN-011"] },
  { id: "IA-SCR-023", flow: "Provider recovery", title: "Integration health", purpose: "Distinguish configured, unavailable, ambiguous, and reconciled provider states.", pattern: "Status regions + CP-R1-PANEL", entry: "Admin / failure handoff", exit: "Recovered or owned intervention", concept: "Provider attempt", task: "Determine whether an external effect occurred and choose reconciliation before retry.", stages: ["Inspect capability", "Check exact attempt", "Reconcile ambiguous effect", "Retry or intervene"] , sample: ["Provider WhatsApp", "Attempt ATT-08 · unknown", "Correlation SYN-008", "Retry blocked pending reconcile"] },
  { id: "IA-SCR-032", flow: "Find work", title: "Search", purpose: "Find only permitted objects and preserve safe return context.", pattern: "CP-R1-TABLE", entry: "Global search", exit: "Permitted result", concept: "Cross-object retrieval", task: "Find MOTOR-011 and choose correctly between the case, work item, conversation, and evidence result.", stages: ["Enter safe query", "Recognize result type", "Check permitted context", "Open exact object"] , sample: ["Case MOTOR-011", "Work item WI-011", "Conversation SYN-CONV-04", "Evidence EV-03"] },
  { id: "IA-SCR-033/034", flow: "Conversation continuity", title: "Conversations", purpose: "Resume a bounded work conversation with current authority and evidence.", pattern: "List + thread + status", entry: "Recent conversations", exit: "Restored work state", concept: "Staff conversation", task: "Resume an interrupted analysis and identify what changed before acting on its earlier recommendation.", stages: ["Choose prior conversation", "Restore work context", "Compare current evidence", "Resume intentionally"] , sample: ["Conversation SYN-CONV-04", "Prior evidence EV-02", "Current evidence EV-03", "Pending action · review changes"] },
];

const stateLabels: Record<StateKey, string> = {
  LOADING: "Loading current data",
  EMPTY: "No permitted content",
  PARTIAL: "Partially available",
  ERROR: "Outcome unconfirmed",
  PERMISSION: "Access restricted",
  SUCCESS: "Current and available",
  RECOVERY: "Recovery in progress",
};

const stateCopy: Record<StateKey, { heading: string; detail: string; exit: string }> = {
  LOADING: { heading: "Authoritative data is loading", detail: "The shell and region names remain stable. Dependent actions are unavailable; no synthetic result rows are implied.", exit: "Exit: current, empty, partial, permission, or exact failure." },
  EMPTY: { heading: "No authorized items match", detail: "The query succeeded. This does not imply that restricted records do not exist.", exit: "Exit: adjust safe filters, start an allowed task, or return." },
  PARTIAL: { heading: "Some regions are unavailable", detail: "Safe successful content remains visible. Missing, stale, or conflicted scope is named locally.", exit: "Exit: recover the affected region or continue only where safe." },
  ERROR: { heading: "The requested outcome is not confirmed", detail: "Work is preserved. Raw provider detail is hidden; the possible external effect and correlation reference remain visible.", exit: "Exit: correct, reconcile, retry when eligible, or escalate." },
  PERMISSION: { heading: "Current authority does not permit this content", detail: "Protected values, counts, and existence cues are removed. Navigation visibility is not treated as authority.", exit: "Exit: permitted route, reauthentication, or access-owner path." },
  SUCCESS: { heading: "The authoritative owner confirmed the result", detail: "Object, version, timestamp, and successor task are visible. This is not inferred from button activation.", exit: "Exit: continue to the named successor or stable terminal state." },
  RECOVERY: { heading: "Recovery is in progress", detail: "Attempt, owner, preserved work, cancellation boundary, and reconciliation status are explicit. Duplicate submission is suppressed.", exit: "Exit: confirmed success, safe failure, or owned manual intervention." },
};

const authorityByRole: Record<(typeof roles)[number], string> = {
  "Claims Officer": "May prepare, review, correct, return, and escalate within assignment. Cannot make the final consequential decision unless separately authorized.",
  "Authorized Approver": "May record one accountable human outcome after current authority, evidence, version, and separation-of-duty checks pass.",
  "Customer Service / Complaints": "May coordinate permitted customer communications and recourse. Cannot expose internal risk, audit, or restricted claim detail.",
  "Compliance Officer": "May inspect permitted audit and governance evidence. Read access does not imply claim action authority.",
  "Loss Assessor / Adjuster": "Receives minimum necessary evidence through a constrained handoff; cannot browse the internal tenant workspace.",
  "Policyholder / Claimant (mediated)": "May submit evidence, receive permitted status, correct supplied information, and contest an outcome through mediated channels; cannot access internal queues or reasoning.",
  "Broker / Channel Submitter": "May submit and receive permitted acknowledgement through mediated channels; cannot view internal queues or decision rationale.",
  "IT / Security Admin": "May inspect configuration and provider health within tenant scope. Cannot make claim decisions or treat provider status as business authority.",
};

function Frame({ children, viewport }: { children: React.ReactNode; viewport: ViewportKey }) {
  const width = viewport === "WIDE" ? "max-w-[1180px]" : viewport === "INTERMEDIATE" ? "max-w-[760px]" : "max-w-[390px]";
  return (
    <section aria-label={`${viewport.toLowerCase()} wireframe`} className={`${width} mx-auto min-h-[640px] border-2 border-slate-700 bg-white p-3 text-slate-950`}>
      {children}
    </section>
  );
}

function Box({ label, children, className = "" }: { label: string; children?: React.ReactNode; className?: string }) {
  return (
    <section className={`border border-dashed border-slate-500 bg-white p-3 ${className}`} aria-label={label}>
      <p className="mb-2 text-[11px] font-bold uppercase tracking-wide text-slate-600">{label}</p>
      {children}
    </section>
  );
}

function isMediatedRole(role: string) {
  return role === "Loss Assessor / Adjuster" || role === "Policyholder / Claimant (mediated)" || role === "Broker / Channel Submitter";
}

function ModelKey() {
  return (
    <section className="mx-auto mb-4 max-w-[1180px] border-2 border-slate-800 bg-white p-4" aria-labelledby="model-key-title">
      <h2 id="model-key-title" className="text-lg font-bold">How the model fits together</h2>
      <p className="mb-3 text-sm">These objects relate, but they are not interchangeable. Authority and state remain with each owning object.</p>
      <dl className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
        {modelConcepts.map(([term, description]) => <div key={term} className="border border-slate-500 p-2"><dt className="font-bold">{term}</dt><dd className="text-sm">{description}</dd></div>)}
      </dl>
    </section>
  );
}

function NetworkBanner({ network }: { network: NetworkKey }) {
  if (network === "NORMAL") return null;
  const copy = network === "LOW_BANDWIDTH"
    ? "Low-bandwidth mode: text and metadata load first; document previews are deferred; committed server state is never inferred from a spinner."
    : "Offline simulation: read-only safe context and the in-memory draft remain; provider, submit, approval, export, and write-back actions are blocked until reconnection and reconciliation.";
  return <p className="mx-auto mb-4 max-w-[1180px] border-2 border-slate-900 bg-white p-3 font-semibold" role="status">{copy}</p>;
}

function MediatedBoundary({ role, surface }: { role: string; surface: Surface }) {
  return (
    <div className="border-2 border-slate-900 bg-slate-100 p-4" role="status">
      <h3 className="font-bold">Mediated handoff-no staff workspace access</h3>
      <p className="mt-2">{role} does not enter {surface.title}. The authorized staff owner sends or receives only the minimum necessary information through the approved channel.</p>
      <p className="mt-2"><strong>Safe next step:</strong> acknowledge the external submission, issue a non-sensitive reference, and route it to the owned human queue.</p>
    </div>
  );
}

function SurfaceContent({ surface, role, state, compact = false }: { surface: Surface; role: string; state: StateKey; compact?: boolean }) {
  if (isMediatedRole(role)) return <MediatedBoundary role={role} surface={surface} />;
  if (state === "LOADING") return <div className="grid gap-2" aria-hidden="true">{surface.sample.slice(0, 3).map((_, index) => <div key={index} className="h-14 animate-pulse border border-slate-400 bg-slate-200 motion-reduce:animate-none" />)}</div>;
  if (state === "EMPTY") return <p>No permitted {surface.concept.toLowerCase()} content is available for this task. Adjust safe filters or return to the named entry point.</p>;
  if (state === "PERMISSION") return <p>Protected object values, counts, and existence cues are intentionally absent. Reauthenticate or return to a permitted route.</p>;
  if (state === "ERROR") return <div><p><strong>Preserved:</strong> safe draft and return context for {surface.flow}.</p><p><strong>Unknown:</strong> whether the requested external effect occurred. Reconcile before retry.</p></div>;
  if (state === "RECOVERY") return <div><p><strong>Recovery owner:</strong> current human queue.</p><p><strong>Attempt:</strong> SYN-REC-01 · rechecking authority, evidence version, and external effect.</p></div>;

  const visible = state === "PARTIAL" ? surface.sample.slice(0, 2) : surface.sample;
  return (
    <div className={`grid gap-2 ${compact ? "grid-cols-1" : "grid-cols-2"}`}>
      {visible.map((item) => <div key={item} className="min-h-16 border border-slate-500 bg-slate-50 p-2">{item}</div>)}
      {state === "PARTIAL" && <div className="min-h-16 border-2 border-dashed border-slate-700 p-2"><strong>Unavailable region:</strong> remaining current data needs owned recovery; unsafe actions stay blocked.</div>}
    </div>
  );
}

function StateRegion({ state }: { state: StateKey }) {
  const copy = stateCopy[state];
  return (
    <div className="border-2 border-slate-800 bg-slate-100 p-4" role="status" aria-live="polite" aria-busy={state === "LOADING"}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <strong>{copy.heading}</strong>
        <span className="border border-slate-700 bg-white px-2 py-1 text-xs font-bold">{stateLabels[state]} <span aria-hidden="true">· {state}</span></span>
      </div>
      <p className="mt-2">{copy.detail}</p>
      <p className="mt-2 text-sm font-semibold">{copy.exit}</p>
      {state === "LOADING" && <div className="mt-3 h-8 animate-pulse border border-slate-400 bg-slate-200 motion-reduce:animate-none" aria-hidden="true" />}
    </div>
  );
}

function ActionStrip({ onConfirm, onFeedback, state, surface, role, network }: { onConfirm: () => void; onFeedback: (message: string) => void; state: StateKey; surface: Surface; role: string; network: NetworkKey }) {
  const stateBlocked = ["LOADING", "EMPTY", "PARTIAL", "ERROR", "PERMISSION", "RECOVERY"].includes(state);
  const external = isMediatedRole(role);
  const offline = network === "OFFLINE";
  const approval = surface.title === "Approval cockpit";
  const decisionAllowed = approval && role === "Authorized Approver" && state === "SUCCESS" && !offline;
  const taskBlocked = stateBlocked || external || offline;
  const guard = external
    ? "This role uses a mediated handoff and cannot act in the staff workspace."
    : offline
      ? "Reconnect and reconcile before submitting."
      : approval && role !== "Authorized Approver"
        ? "Only an eligible authorized approver may record the human outcome."
        : stateBlocked
          ? "Resolve the current state before continuing."
          : "Current prerequisites are available for this simulated task.";
  return (
    <div className="flex flex-wrap gap-2" aria-label="Simulated actions">
      <button type="button" disabled={external || state === "PERMISSION" || state === "LOADING"} className="min-h-11 border-2 border-slate-800 bg-white px-4 font-semibold enabled:hover:bg-slate-100 enabled:active:bg-slate-200 disabled:cursor-not-allowed disabled:border-slate-400 disabled:text-slate-500" onClick={() => onFeedback(`Safe synthetic draft preserved for ${surface.id}; no server write occurred.`)}>Save safe draft</button>
      <button type="button" className="min-h-11 border-2 border-slate-800 bg-white px-4 font-semibold hover:bg-slate-100 active:bg-slate-200" onClick={() => onFeedback(external ? "Returned to the approved mediated channel; no staff-workspace action occurred." : `Returned to ${surface.entry}; synthetic context preserved.`)}>{external ? "Return to mediated channel" : "Return / escalate"}</button>
      {approval
        ? <button type="button" disabled={!decisionAllowed} aria-describedby="authority-note action-guard" className="min-h-11 border-2 border-slate-950 bg-slate-900 px-4 font-semibold text-white enabled:hover:bg-slate-700 disabled:cursor-not-allowed disabled:border-slate-400 disabled:bg-slate-300 disabled:text-slate-700" onClick={onConfirm}>Record human decision</button>
        : <button type="button" disabled={taskBlocked} aria-describedby="action-guard" className="min-h-11 border-2 border-slate-950 bg-slate-900 px-4 font-semibold text-white enabled:hover:bg-slate-700 disabled:cursor-not-allowed disabled:border-slate-400 disabled:bg-slate-300 disabled:text-slate-700" onClick={() => onFeedback(`Simulated task advanced to ${surface.exit}; no API or provider call occurred.`)}>Continue simulated task</button>}
      <p id="action-guard" className="basis-full text-sm"><strong>Action guard:</strong> {guard}</p>
    </div>
  );
}

function SharedHeader({ surface, role, state }: { surface: Surface; role: string; state: StateKey }) {
  return (
    <header className="mb-3 border-b-2 border-slate-700 pb-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase">{surface.id} · {surface.flow}</p>
          <h2 className="text-2xl font-bold">{surface.title}</h2>
          <p>{surface.purpose}</p>
          <p className="mt-2 text-sm"><strong>Object model:</strong> {surface.concept}</p>
          <p className="text-sm"><strong>Test task:</strong> {state === "PERMISSION" || isMediatedRole(role) ? "Internal task details are hidden; use the approved mediated handoff." : surface.task}</p>
        </div>
        <div className="border border-slate-600 p-2 text-xs"><strong>Role:</strong> {role}<br /><strong>State:</strong> {stateLabels[state]}<br /><span className="font-mono">{state}</span></div>
      </div>
    </header>
  );
}

function VariantA({ surface, role, state, viewport, actualCompact, network, onConfirm, onFeedback }: PrototypeViewProps) {
  const compact = viewport === "COMPACT" || actualCompact;
  return (
    <Frame viewport={viewport}>
      <SharedHeader surface={surface} role={role} state={state} />
      <ol className={`mb-3 grid gap-2 text-xs ${compact ? "grid-cols-1" : "grid-cols-4"}`} aria-label="Guided task stages">
        {surface.stages.map((step, index) => <li key={step} className={`border p-2 ${index === 1 ? "border-slate-900 bg-slate-200 font-bold" : "border-slate-400"}`}>{index + 1}. {step}</li>)}
      </ol>
      <div className={`grid gap-3 ${compact ? "grid-cols-1" : "grid-cols-[minmax(0,2fr)_minmax(230px,1fr)]"}`}>
        <main className="space-y-3">
          <StateRegion state={state} />
          <Box label={`${surface.pattern}, primary work`}>
            <SurfaceContent surface={surface} role={role} state={state} compact={compact} />
          </Box>
        </main>
        <aside className="space-y-3">
          <Box label="Authority + prerequisites"><p id="authority-note">{authorityByRole[role as keyof typeof authorityByRole]}</p></Box>
          <Box label="Entry / exit"><p><strong>Entry:</strong> {surface.entry}</p><p><strong>Exit:</strong> {surface.exit}</p></Box>
        </aside>
      </div>
      <div className="mt-3 border-t-2 border-slate-700 pt-3"><ActionStrip state={state} surface={surface} role={role} network={network} onConfirm={onConfirm} onFeedback={onFeedback} /></div>
    </Frame>
  );
}

function VariantB({ surface, role, state, viewport, actualCompact, network, onConfirm, onFeedback }: PrototypeViewProps) {
  const compact = viewport === "COMPACT" || actualCompact;
  const populated = (state === "SUCCESS" || state === "PARTIAL") && !isMediatedRole(role);
  const visibleRows = state === "PARTIAL" ? surface.sample.slice(0, 2) : surface.sample;
  return (
    <Frame viewport={viewport}>
      <SharedHeader surface={surface} role={role} state={state} />
      <div className={`grid gap-2 ${compact ? "grid-cols-1" : "grid-cols-[180px_minmax(0,1fr)_220px]"}`}>
        <nav aria-label="Synthetic work queue" className="border-2 border-slate-700 p-2">
          <strong className="block border-b border-slate-500 pb-2">Queue / filters</strong>
          {(state === "SUCCESS" ? ["Needs review · 4", "Waiting · 2", "Conflict · 1", "Recovery · 1"] : ["Permitted work", "My assignment", "Needs recovery"]).map((item, index) => <button key={item} type="button" className={`mt-2 min-h-11 w-full border px-2 text-left ${index === 0 ? "border-slate-900 bg-slate-200 font-bold" : "border-slate-400 bg-white"}`}>{item}</button>)}
        </nav>
        <main className="space-y-2">
          <StateRegion state={state} />
          <Box label={`${surface.pattern}, dense work canvas`}>
            {!populated || compact
              ? <SurfaceContent surface={surface} role={role} state={state} compact />
              : <table className="w-full table-fixed border-collapse text-left text-xs">
                  <caption className="sr-only">Synthetic permitted {surface.concept} items</caption>
                  <thead><tr>{["Object / state", "Owner", "Relationship", "Next"].map((head) => <th key={head} className="border border-slate-500 p-2">{head}</th>)}</tr></thead>
                  <tbody>{visibleRows.map((item, index) => <tr key={item}><td className="break-words border border-slate-400 p-2 font-bold">{item}</td><td className="break-words border border-slate-400 p-2">{role}</td><td className="break-words border border-slate-400 p-2">{surface.concept}</td><td className="border border-slate-400 p-2"><button type="button" className="min-h-11 underline" onClick={() => onFeedback(`Opened synthetic item ${index + 1}; no route or API call occurred.`)}>Open</button></td></tr>)}</tbody>
                </table>}
          </Box>
          <ActionStrip state={state} surface={surface} role={role} network={network} onConfirm={onConfirm} onFeedback={onFeedback} />
        </main>
        <aside className="space-y-2">
          <Box label="Inspector panel"><SurfaceContent surface={surface} role={role} state={state} compact /></Box>
          <Box label="Authority"><p id="authority-note">{authorityByRole[role as keyof typeof authorityByRole]}</p></Box>
          <Box label="Audit preview"><p>Correlation: SYN-011</p><p>Before / after retained</p></Box>
        </aside>
      </div>
    </Frame>
  );
}

function VariantC({ surface, role, state, viewport, network, onConfirm, onFeedback }: PrototypeViewProps) {
  return (
    <Frame viewport={viewport}>
      <SharedHeader surface={surface} role={role} state={state} />
      <StateRegion state={state} />
      <main className="mt-3 space-y-3" aria-label="Role and handoff storyboard">
        {[
          ["Before IIP / entry", surface.entry, "External channel or insurer system owns its committed record."],
          ["IIP intelligence step", surface.pattern, "IIP prepares and explains; evidence provenance and uncertainty remain visible."],
          ["Human authority boundary", role, authorityByRole[role as keyof typeof authorityByRole]],
          ["After IIP / exit", surface.exit, "Write-back or provider success remains separately confirmed and reconcilable."],
        ].map(([heading, owner, detail], index) => (
          <article key={heading} className="grid grid-cols-[44px_minmax(0,1fr)] gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-full border-2 border-slate-800 font-bold" aria-hidden="true">{index + 1}</div>
            <div className="border-2 border-slate-600 p-3"><h3 className="font-bold">{heading}</h3><p className="text-sm"><strong>Owner/touchpoint:</strong> {owner}</p><p>{detail}</p></div>
          </article>
        ))}
      </main>
      <Box label="Successor, interruption, and recovery" className="mt-3"><p>Preserve the exact object, safe draft, evidence version, pending action, and return focus. Recheck authority and currentness before resuming.</p></Box>
      <div className="mt-3"><ActionStrip state={state} surface={surface} role={role} network={network} onConfirm={onConfirm} onFeedback={onFeedback} /></div>
    </Frame>
  );
}

type PrototypeViewProps = {
  surface: Surface;
  role: string;
  state: StateKey;
  viewport: ViewportKey;
  actualCompact: boolean;
  network: NetworkKey;
  onConfirm: () => void;
  onFeedback: (message: string) => void;
};

function PrototypeSwitcher({ current, onChange }: { current: VariantKey; onChange: (value: VariantKey) => void }) {
  const index = variants.findIndex((item) => item.key === current);
  const cycle = (direction: -1 | 1) => onChange(variants[(index + direction + variants.length) % variants.length].key);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target?.matches("input, textarea, select, [contenteditable='true']")) return;
      if (event.key === "ArrowLeft") cycle(-1);
      if (event.key === "ArrowRight") cycle(1);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  });

  if (process.env.NODE_ENV === "production") return null;
  const active = variants[index];
  return (
    <div className="pointer-events-none fixed bottom-4 left-1/2 z-50 flex -translate-x-1/2 items-center gap-2 rounded-full border-2 border-white bg-slate-950 p-2 text-white shadow-xl" aria-label="Prototype variant switcher">
      <button type="button" className="pointer-events-auto flex min-h-11 min-w-11 items-center justify-center rounded-full border border-white" onClick={() => cycle(-1)} aria-label="Previous layout variant">←</button>
      <span className="min-w-44 text-center text-sm font-bold">{active.key}, {active.name}</span>
      <button type="button" className="pointer-events-auto flex min-h-11 min-w-11 items-center justify-center rounded-full border border-white" onClick={() => cycle(1)} aria-label="Next layout variant">→</button>
    </div>
  );
}

export default function Release1LowFidelityPrototype({ initialVariant }: { initialVariant: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [role, setRole] = useState<(typeof roles)[number]>("Claims Officer");
  const [surfaceId, setSurfaceId] = useState(surfaces[0].id);
  const [state, setState] = useState<StateKey>("SUCCESS");
  const [viewport, setViewport] = useState<ViewportKey>("WIDE");
  const [network, setNetwork] = useState<NetworkKey>("NORMAL");
  const [interrupted, setInterrupted] = useState(false);
  const [actualCompact, setActualCompact] = useState(false);
  const [result, setResult] = useState("No simulated action recorded.");
  const dialogRef = useRef<HTMLDialogElement>(null);
  const validInitial = variants.some((item) => item.key === initialVariant) ? initialVariant as VariantKey : "A";
  const currentVariant = (searchParams.get("variant") as VariantKey | null) ?? validInitial;
  const surface = useMemo(() => surfaces.find((item) => item.id === surfaceId) ?? surfaces[0], [surfaceId]);

  useEffect(() => {
    const query = window.matchMedia("(max-width: 599px)");
    const update = () => setActualCompact(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);

  const changeVariant = (next: VariantKey) => {
    const params = new URLSearchParams(searchParams.toString());
    params.set("variant", next);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  };

  const openConfirmation = () => dialogRef.current?.showModal();
  const confirmAction = () => {
    setResult(`Simulated human review recorded for ${surface.id}; no API or provider call occurred.`);
    dialogRef.current?.close();
  };

  const props = { surface, role, state, viewport, actualCompact, network, onConfirm: openConfirmation, onFeedback: setResult };

  return (
    <div className="min-h-screen bg-slate-200 px-3 pb-28 pt-5 text-slate-950">
      <a href="#prototype-canvas" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[60] focus:bg-white focus:p-3">Skip to prototype canvas</a>
      <header className="mx-auto mb-4 max-w-[1180px] border-2 border-slate-900 bg-white p-4">
        <p className="inline-block border-2 border-slate-950 bg-slate-950 px-2 py-1 text-xs font-bold text-white">THROWAWAY PROTOTYPE · SYNTHETIC DATA · NOT CONNECTED · NOT UAT</p>
        <h1 className="mt-2 text-2xl font-bold">Release 1 low-fidelity layout lab</h1>
        <p>Question: which structure best keeps purpose, evidence, human authority, state, and handoffs understandable across critical flows and responsive contexts?</p>
        <p className="mt-1 text-sm"><strong>Three variants on one development-only route, switchable via <code>?variant=A|B|C</code>.</strong></p>
      </header>

      <ModelKey />
      <NetworkBanner network={network} />

      <form className="mx-auto mb-4 grid max-w-[1180px] gap-3 border-2 border-slate-800 bg-white p-4 md:grid-cols-2 xl:grid-cols-5" aria-label="Prototype controls" onSubmit={(event) => event.preventDefault()}>
        <label className="font-semibold">Critical surface
          <select value={surfaceId} onChange={(event) => setSurfaceId(event.target.value)} className="mt-1 min-h-11 w-full border-2 border-slate-600 px-2 font-normal">
            {surfaces.map((item) => <option key={item.id} value={item.id}>{item.id}, {item.title}</option>)}
          </select>
        </label>
        <label className="font-semibold">Release role
          <select value={role} onChange={(event) => setRole(event.target.value as typeof role)} className="mt-1 min-h-11 w-full border-2 border-slate-600 px-2 font-normal">
            {roles.map((item) => <option key={item}>{item}</option>)}
          </select>
        </label>
        <label className="font-semibold">Governed state
          <select value={state} onChange={(event) => setState(event.target.value as StateKey)} className="mt-1 min-h-11 w-full border-2 border-slate-600 px-2 font-normal">
            {states.map((item) => <option key={item} value={item}>{stateLabels[item]} ({item})</option>)}
          </select>
        </label>
        <label className="font-semibold">Responsive context
          <select value={viewport} onChange={(event) => setViewport(event.target.value as ViewportKey)} className="mt-1 min-h-11 w-full border-2 border-slate-600 px-2 font-normal">
            {viewports.map((item) => <option key={item}>{item}</option>)}
          </select>
        </label>
        <label className="font-semibold">Network condition
          <select value={network} onChange={(event) => setNetwork(event.target.value as NetworkKey)} className="mt-1 min-h-11 w-full border-2 border-slate-600 px-2 font-normal">
            {networks.map((item) => <option key={item} value={item}>{item === "LOW_BANDWIDTH" ? "Low bandwidth" : item === "OFFLINE" ? "Offline" : "Normal"}</option>)}
          </select>
        </label>
      </form>

      <section className="mx-auto mb-4 max-w-[1180px] border-2 border-slate-800 bg-white p-3" aria-labelledby="resume-test-title">
        <h2 id="resume-test-title" className="font-bold">Interruption and resume test</h2>
        {interrupted
          ? <div className="mt-2"><p><strong>Resume point:</strong> {surface.id} · {surface.concept} · synthetic safe draft preserved. Authority and current evidence must be rechecked.</p><button type="button" className="mt-2 min-h-11 border-2 border-slate-900 bg-slate-900 px-4 font-semibold text-white" onClick={() => { setInterrupted(false); setState("SUCCESS"); setResult(`Resumed ${surface.id} after rechecking synthetic authority and currentness.`); }}>Resume intentionally</button></div>
          : <button type="button" className="mt-2 min-h-11 border-2 border-slate-800 px-4 font-semibold" onClick={() => { setInterrupted(true); setState("RECOVERY"); setResult(`Interrupted ${surface.id}; safe synthetic context preserved for explicit resume.`); }}>Simulate interruption</button>}
      </section>

      <div id="prototype-canvas" tabIndex={-1}>
        {currentVariant === "A" && <VariantA {...props} />}
        {currentVariant === "B" && <VariantB {...props} />}
        {currentVariant === "C" && <VariantC {...props} />}
      </div>

      <p className="mx-auto mt-3 max-w-[1180px] border border-slate-600 bg-white p-3" role="status" aria-live="polite">{result}</p>

      <dialog ref={dialogRef} aria-labelledby="prototype-dialog-title" className="max-w-lg border-2 border-slate-950 p-0 backdrop:bg-slate-950/60">
        <form method="dialog" className="p-5" onSubmit={(event) => { event.preventDefault(); if (event.currentTarget.reportValidity()) confirmAction(); }}>
          <h2 id="prototype-dialog-title" className="text-xl font-bold">Confirm simulated human review</h2>
          <p className="mt-2">This demonstrates a governed dialog only. It does not approve, reject, send, export, write back, or call a provider.</p>
          <label className="mt-3 block font-semibold">Required reason
            <textarea required className="mt-1 min-h-24 w-full border-2 border-slate-700 p-2" placeholder="Explain the evidence, authority, and reason for this human outcome." />
          </label>
          <div className="mt-4 flex flex-wrap justify-end gap-2">
            <button type="button" className="min-h-11 border-2 border-slate-800 px-4" onClick={() => dialogRef.current?.close()}>Cancel</button>
            <button type="submit" className="min-h-11 border-2 border-slate-950 bg-slate-950 px-4 text-white">Record simulated review</button>
          </div>
        </form>
      </dialog>

      <PrototypeSwitcher current={currentVariant} onChange={changeVariant} />
    </div>
  );
}

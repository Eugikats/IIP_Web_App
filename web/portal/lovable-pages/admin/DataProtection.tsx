"use client";

import { useCallback, useEffect, useId, useState } from "react";
import { toast } from "sonner";
import { useUI } from "@/context/UIStateContext";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Field as UIField } from "@/components/ui/Input";
import { Table, TableBody, TableCell, TableHead, TableHeaderCell, TableRow } from "@/components/ui/Table";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import { loadTenantSettings, saveTenantSettings, type TenantSettings } from "@/lib/tenantSettings";
import {
  categoryLabels, erase, listDeletions, listHolds, placeHold, previewErasure, releaseHold, runRetention,
  type Deletion, type ErasurePreview, type HeldClaim, type Subject,
} from "@/lib/dataLifecycle";

const when = (value: string) => new Date(value).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });

/** How long records are kept, which claims are on legal hold, and erasing one person's data with a record that it was done. */
export default function DataProtection() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [forbidden, setForbidden] = useState(false);
  useEffect(() => {
    void listHolds(role).then((result) => setForbidden(!result.ok && result.forbidden === true));
  }, [role]);
  if (forbidden) {
    return (
      <>
        <SectionHeader eyebrow="Admin · Data protection" title="Retention, legal holds and erasure" sub="Decide how long records are kept and erase a person's data when they ask." />
        <GlassCard><NoPermissionStateSurface permission="ManageTenantSettings" /></GlassCard>
      </>
    );
  }

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Data protection"
        title="Retention, legal holds and erasure"
        sub="Decide how long records are kept, protect claims that must not be deleted, and erase one person's data when they ask. Nothing is deleted unless you set a period or ask for an erasure."
      />
      <Retention role={role} />
      <Holds role={role} />
      <Erasure role={role} />
    </>
  );
}

function Retention({ role }: { role: string }) {
  const [settings, setSettings] = useState<TenantSettings | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [ran, setRan] = useState<Deletion[] | null>(null);

  const load = useCallback(async () => {
    const result = await loadTenantSettings(role);
    if (!result.ok) return setError(result.error);
    setSettings(result.value);
    setDraft(Object.fromEntries((result.value.retentionClasses ?? []).map((item) => [item.key, String(result.value.retentionMonths?.[item.key] ?? 0)])));
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  async function save() {
    setBusy(true);
    const months = Object.fromEntries(Object.entries(draft).map(([key, value]) => [key, Number.parseInt(value, 10) || 0]));
    const result = await saveTenantSettings(role, { retentionMonths: months });
    setBusy(false);
    if (!result.ok) return toast.error(result.error);
    toast.success("Retention periods saved.");
    setSettings(result.value);
  }

  async function runNow() {
    setBusy(true);
    const result = await runRetention(role);
    setBusy(false);
    if (!result.ok) return toast.error(result.error);
    setRan(result.value);
    toast.success(result.value.length === 0 ? "Nothing was past its period." : "Expired records were deleted.");
  }

  return (
    <GlassCard className="mb-4" data-testid="retention-card">
      <div className="text-[15px] font-bold text-dls-text-primary">How long records are kept</div>
      <p className="mb-3 mt-1 text-[12.5px] text-dls-text-secondary">
        After the period you set, records are deleted for good, together with their stored files. Leave 0 to keep a kind of record. A claim on legal hold is never deleted. Deleted files can still be recovered from the storage account for a few days (set by whoever runs the deployment).
      </p>
      {error ? <ErrorStateSurface title="Retention could not be loaded" message={error} /> : !settings ? <LoadingSkeleton rows={3} /> : (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            {(settings.retentionClasses ?? []).map((item) => (
              <UIField key={item.key} label={`${item.label}: months to keep`} htmlFor={`retention-${item.key}`}>
                <Input id={`retention-${item.key}`} inputMode="numeric" value={draft[item.key] ?? "0"} onChange={(event) => setDraft({ ...draft, [item.key]: event.target.value })} />
              </UIField>
            ))}
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            <Button variant="primary" onClick={() => void save()} disabled={busy}>Save periods</Button>
            <Button onClick={() => void runNow()} disabled={busy}>Delete expired records now</Button>
          </div>
          {ran ? (
            <p className="mt-3 text-[12.5px] text-dls-text-secondary" data-testid="retention-ran">
              {ran.length === 0 ? "Nothing was past its period." : ran.map((record) => `${record.subject}: ${record.total} deleted`).join("; ")}
            </p>
          ) : null}
        </>
      )}
    </GlassCard>
  );
}

function Holds({ role }: { role: string }) {
  const referenceId = useId();
  const reasonId = useId();
  const [holds, setHolds] = useState<HeldClaim[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reference, setReference] = useState("");
  const [reason, setReason] = useState("");

  const load = useCallback(async () => {
    const result = await listHolds(role);
    if (result.ok) {
      setHolds(result.value);
      setError(null);
    } else {
      setError(result.error);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  async function place() {
    const result = await placeHold(role, reference, reason);
    if (!result.ok) return toast.error(result.error);
    toast.success(`${result.value.reference} is on legal hold.`);
    setReference("");
    setReason("");
    await load();
  }

  return (
    <GlassCard className="mb-4" data-testid="holds-card">
      <div className="text-[15px] font-bold text-dls-text-primary">Legal holds</div>
      <p className="mb-3 mt-1 text-[12.5px] text-dls-text-secondary">A claim on hold cannot be deleted by a retention period or an erasure request, for example while a court case or investigation is open.</p>
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto] sm:items-end">
        <UIField label="Claim reference" htmlFor={referenceId}><Input id={referenceId} placeholder="CLM-1A2B3C4D" value={reference} onChange={(event) => setReference(event.target.value)} /></UIField>
        <UIField label="Why it is held" htmlFor={reasonId}><Input id={reasonId} value={reason} onChange={(event) => setReason(event.target.value)} /></UIField>
        <Button variant="primary" onClick={() => void place()} disabled={!reference.trim() || reason.trim().length < 5}>Place hold</Button>
      </div>
      <div className="mt-4">
        {error ? <ErrorStateSurface title="Holds could not be loaded" message={error} /> : holds === null ? <LoadingSkeleton rows={2} /> : holds.length === 0 ? (
          <EmptyStateSurface title="No claims are on hold" message="Place a hold above using the claim's reference." />
        ) : (
          <Table label="Claims on legal hold">
            <TableHead><TableRow><TableHeaderCell>Claim</TableHeaderCell><TableHeaderCell>Reason</TableHeaderCell><TableHeaderCell>Placed</TableHeaderCell><TableHeaderCell numeric>Action</TableHeaderCell></TableRow></TableHead>
            <TableBody>
              {holds.map((hold) => (
                <TableRow key={hold.caseId}>
                  <TableCell>{hold.reference} <span className="text-dls-text-muted">({hold.status})</span></TableCell>
                  <TableCell className="text-dls-text-secondary">{hold.reason}</TableCell>
                  <TableCell className="text-dls-text-secondary">{when(hold.placedAt)}</TableCell>
                  <TableCell numeric><Button variant="ghost" onClick={async () => { const done = await releaseHold(role, hold.caseId); if (!done.ok) return toast.error(done.error); toast.success("Hold released."); await load(); }}>Release</Button></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </GlassCard>
  );
}

function Erasure({ role }: { role: string }) {
  const phoneId = useId();
  const emailId = useId();
  const policyId = useId();
  const reasonId = useId();
  const confirmId = useId();
  const [subject, setSubject] = useState<Subject>({ phone: "", email: "", policyNumber: "" });
  const [preview, setPreview] = useState<ErasurePreview | null>(null);
  const [reason, setReason] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<Deletion | null>(null);
  const [evidence, setEvidence] = useState<Deletion[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadEvidence = useCallback(async () => {
    const result = await listDeletions(role);
    if (result.ok) setEvidence(result.value);
  }, [role]);

  useEffect(() => {
    void loadEvidence();
  }, [loadEvidence]);

  function change(next: Subject) {
    setSubject(next);
    setPreview(null);
    setDone(null);
    setError(null);
  }

  async function check() {
    setBusy(true);
    setError(null);
    const result = await previewErasure(role, subject);
    setBusy(false);
    if (!result.ok) return setError(result.error);
    setPreview(result.value);
  }

  async function run() {
    setBusy(true);
    setError(null);
    const result = await erase(role, subject, reason, confirm);
    setBusy(false);
    if (!result.ok) return setError(result.error);
    setDone(result.value);
    setPreview(null);
    setConfirm("");
    await loadEvidence();
  }

  const counts = preview ? Object.entries(preview.counts).filter(([, value]) => value > 0) : [];
  return (
    <>
      <GlassCard className="mb-4" data-testid="erasure-card">
        <div className="text-[15px] font-bold text-dls-text-primary">Erase one person's data</div>
        <p className="mb-3 mt-1 text-[12.5px] text-dls-text-secondary">
          For when a person asks for their data to be erased. Their documents, stored files, messages, chats and profile are deleted. The claim itself stays, with its status, dates and amounts but without the person, because an insurer must keep a record of its claims. This cannot be undone.
        </p>
        <div className="grid gap-3 sm:grid-cols-3">
          <UIField label="Phone number (with country code)" htmlFor={phoneId}><Input id={phoneId} value={subject.phone} onChange={(event) => change({ ...subject, phone: event.target.value })} /></UIField>
          <UIField label="Email" htmlFor={emailId}><Input id={emailId} value={subject.email} onChange={(event) => change({ ...subject, email: event.target.value })} /></UIField>
          <UIField label="Policy number" htmlFor={policyId}><Input id={policyId} value={subject.policyNumber} onChange={(event) => change({ ...subject, policyNumber: event.target.value })} /></UIField>
        </div>
        <div className="mt-3"><Button onClick={() => void check()} disabled={busy || (!subject.phone.trim() && !subject.email.trim() && !subject.policyNumber.trim())}>See what would be erased</Button></div>
        {error ? <div role="alert" className="mt-3 text-[12.5px] text-dls-danger-text">{error}</div> : null}

        {preview ? (
          <div className="mt-4 rounded-dls-control border border-dls-border-subtle p-4" data-testid="erasure-preview">
            {preview.nothingFound ? <p className="text-[13px] text-dls-text-secondary">Nothing is stored for this person in this workspace.</p> : (
              <>
                <div className="text-[13px] font-semibold text-dls-text-primary">{preview.claimsThatWillBeErased} claim(s) will lose the person's details{preview.claimsOnHold > 0 ? `; ${preview.claimsOnHold} on legal hold will be left alone` : ""}.</div>
                <ul className="mt-2 list-disc pl-5 text-[12.5px] text-dls-text-secondary">
                  {counts.map(([key, value]) => <li key={key}>{categoryLabels[key] ?? key}: {value}</li>)}
                </ul>
                {preview.claims.some((claim) => claim.onLegalHold) ? (
                  <p className="mt-2 text-[12.5px] text-dls-text-secondary">On hold: {preview.claims.filter((claim) => claim.onLegalHold).map((claim) => claim.reference).join(", ")}. Release a hold first if it should be erased too.</p>
                ) : null}
                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  <UIField label="Why (for the record)" htmlFor={reasonId}><Input id={reasonId} value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Written request received 3 October" /></UIField>
                  <UIField label="Type ERASE to confirm" htmlFor={confirmId}><Input id={confirmId} value={confirm} onChange={(event) => setConfirm(event.target.value)} /></UIField>
                </div>
                <div className="mt-3"><Button variant="primary" onClick={() => void run()} disabled={busy || confirm !== "ERASE" || reason.trim().length < 5}>Erase now</Button></div>
              </>
            )}
          </div>
        ) : null}
        {done ? (
          <div role="status" data-testid="erasure-done" className="mt-4 rounded-dls-control border border-dls-border-subtle p-4 text-[13px] text-dls-text-primary">
            Erased: {done.total} record(s) and {done.storedFilesDeleted} stored file(s).
            {done.heldClaims.length > 0 ? ` Left on legal hold: ${done.heldClaims.join(", ")}.` : ""}
            {done.failures.length > 0 ? ` Not finished (try again): ${done.failures.join("; ")}.` : ""}
          </div>
        ) : null}
      </GlassCard>

      <GlassCard data-testid="deletion-evidence">
        <div className="text-[15px] font-bold text-dls-text-primary">Evidence of deletion</div>
        <p className="mb-3 mt-1 text-[12.5px] text-dls-text-secondary">A record of every deletion: when, who or which rule, why, and how many of each kind. It never holds the data that was deleted or the phone or email asked about.</p>
        {evidence === null ? <LoadingSkeleton rows={2} /> : evidence.length === 0 ? <EmptyStateSurface title="Nothing has been deleted" message="Erasures and retention deletions are listed here." /> : (
          <Table label="Deletions">
            <TableHead><TableRow><TableHeaderCell>When</TableHeaderCell><TableHeaderCell>Kind</TableHeaderCell><TableHeaderCell>Why</TableHeaderCell><TableHeaderCell>Deleted</TableHeaderCell><TableHeaderCell>Left over</TableHeaderCell></TableRow></TableHead>
            <TableBody>
              {evidence.map((record) => (
                <TableRow key={record.deletionId}>
                  <TableCell className="whitespace-nowrap">{when(record.at)}</TableCell>
                  <TableCell><Badge tone={record.kind === "Erasure" ? "warning" : "neutral"}>{record.kind}</Badge></TableCell>
                  <TableCell className="text-dls-text-secondary">{record.reason}</TableCell>
                  <TableCell>{record.total} record(s), {record.storedFilesDeleted} file(s)</TableCell>
                  <TableCell className="text-dls-text-secondary">{[record.heldClaims.length ? `${record.heldClaims.length} on hold` : "", record.failures.length ? `${record.failures.length} to retry` : ""].filter(Boolean).join(", ")}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </GlassCard>
    </>
  );
}

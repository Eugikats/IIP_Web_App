"use client";

import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import {
  applyWordingDraft,
  createWordingDraft,
  decideWordingItem,
  discardWordingDraft,
  listWordingDrafts,
  listWordings,
  type WordingApplyResult,
  type WordingDocument,
  type WordingDraft,
  type WordingDraftItem,
  type WordingItemChange,
  type WordingItemKind,
} from "@/lib/wordingDrafts";

const KIND_ORDER: WordingItemKind[] = ["ClaimType", "Document", "Field", "Covered", "Exclusion"];
const KIND_HEADING: Record<WordingItemKind, string> = {
  ClaimType: "Kinds of claim",
  Document: "Documents asked for",
  Field: "Questions asked",
  Covered: "What is covered",
  Exclusion: "What is not covered",
};

const FIELD_TYPES = ["Text", "Number", "Currency", "Date", "Phone", "YesNo"];

function decisionTone(item: WordingDraftItem): { tone: PillTone; label: string } {
  if (item.problem) return { tone: "danger", label: "Cannot be accepted yet" };
  if (item.decision === "Accepted") return { tone: "success", label: "Accepted" };
  if (item.decision === "Rejected") return { tone: "neutral", label: "Rejected" };
  return { tone: "warning", label: "Needs a decision" };
}

type PanelProps = {
  role: string;
  productId: string;
  /** True while the product page has changes that are not saved: applying would replace them. */
  hasUnsavedChanges: boolean;
  canEdit: boolean;
  onApplied: (result: WordingApplyResult) => void;
};

/**
 * Reading a policy wording into proposed setup (build-loop item 7). The model's proposals are shown
 * with the clause each one quotes; nothing reaches the product until a person accepts items and
 * presses Add accepted items, which makes a draft version that still needs approval to publish.
 */
export function WordingDraftPanel({ role, productId, hasUnsavedChanges, canEdit, onApplied }: PanelProps) {
  const [wordings, setWordings] = useState<WordingDocument[] | null>(null);
  const [draft, setDraft] = useState<WordingDraft | null>(null);
  const [chosen, setChosen] = useState("");
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<WordingApplyResult | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    const [docs, drafts] = await Promise.all([listWordings(role), listWordingDrafts(role, productId)]);
    setLoading(false);
    if (!docs.ok) return setLoadError(docs.error);
    if (!drafts.ok) return setLoadError(drafts.error);
    const policyWordings = docs.value.filter((doc) => doc.documentType === "PolicyWording");
    setWordings(policyWordings);
    setChosen((current) => current || policyWordings[0]?.knowledgeDocumentId || "");
    setDraft(drafts.value.find((candidate) => candidate.status === "InReview") ?? null);
  }, [role, productId]);

  useEffect(() => { void load(); }, [load]);

  async function read() {
    setBusy(true);
    setError(null);
    setResult(null);
    const created = await createWordingDraft(role, productId, chosen);
    setBusy(false);
    if (!created.ok) return setError(created.error);
    setDraft(created.value);
  }

  async function decide(item: WordingDraftItem, change: WordingItemChange) {
    if (!draft) return;
    setError(null);
    const updated = await decideWordingItem(role, productId, draft.draftId, item.itemId, change);
    if (!updated.ok) return setError(updated.error);
    setDraft(updated.value);
  }

  async function apply() {
    if (!draft) return;
    setBusy(true);
    setError(null);
    const applied = await applyWordingDraft(role, productId, draft.draftId);
    setBusy(false);
    if (!applied.ok) return setError(applied.error);
    setResult(applied.value);
    setDraft(null);
    onApplied(applied.value);
  }

  async function discard() {
    if (!draft) return;
    setBusy(true);
    const discarded = await discardWordingDraft(role, productId, draft.draftId);
    setBusy(false);
    if (!discarded.ok) return setError(discarded.error);
    setDraft(null);
  }

  return (
    <div data-testid="wording-draft-panel">
      <div className="text-[14px] font-bold text-dls-text-primary">Set up from the policy wording</div>
      <p className="mb-3 mt-1 text-[12px] text-dls-text-secondary">
        The assistant reads the wording and proposes kinds of claim, documents, questions, and what is covered or not. Every proposal quotes the clause it relies on. Nothing is added until you accept it, and the result is a draft that still needs a second person to publish.
      </p>

      {loading && <LoadingSkeleton rows={2} />}
      {loadError && <ErrorStateSurface title="This could not be loaded" message={loadError} onRetry={load} />}

      {!loading && !loadError && !draft && (
        <>
          {result && (
            <div role="status" className="mb-3 rounded-dls-card border border-dls-border bg-dls-surface p-3 text-[12.5px] text-dls-text-primary" data-testid="wording-apply-result">
              Added to version {result.productVersion}: {result.claimTypesAdded} kinds of claim, {result.documentsAdded} documents, {result.fieldsAdded} questions, {result.coverageAdded} covered or excluded items.
              {result.skipped.length > 0 && (
                <ul className="mt-2 list-disc pl-5 text-dls-text-secondary">
                  {result.skipped.map((skipped) => <li key={skipped.itemId}>{skipped.title}: {skipped.reason}</li>)}
                </ul>
              )}
            </div>
          )}
          {wordings && wordings.length === 0 ? (
            <EmptyStateSurface title="No policy wording yet" message="Upload the policy wording under Knowledge, then come back here to have it read." />
          ) : (
            <div className="flex flex-wrap items-end gap-3">
              <div className="min-w-[14rem] flex-1">
                <label htmlFor="wording-choice" className="mb-1 block text-[12px] font-semibold text-dls-text-secondary">Wording to read</label>
                <Select id="wording-choice" value={chosen} onChange={(event) => setChosen(event.target.value)} disabled={!canEdit || busy}>
                  {(wordings ?? []).map((doc) => (
                    <option key={doc.knowledgeDocumentId} value={doc.knowledgeDocumentId}>{doc.documentName} (version {doc.version})</option>
                  ))}
                </Select>
              </div>
              <Button variant="secondary" onClick={() => void read()} disabled={!canEdit || busy || !chosen || hasUnsavedChanges}>
                {busy ? "Reading..." : "Read the wording"}
              </Button>
            </div>
          )}
          {hasUnsavedChanges && <p className="mt-2 text-[12px] text-dls-text-secondary">Save your changes to the product first. Adding items makes a new version.</p>}
          {!canEdit && <p className="mt-2 text-[12px] text-dls-text-secondary">You can read the proposals but not change the product.</p>}
        </>
      )}

      {draft && (
        <div data-testid="wording-draft-review">
          <div className="mb-3 flex flex-wrap items-center gap-2 text-[12px] text-dls-text-secondary">
            <span>From {draft.documentName}</span>
            <StatusPill tone="info" label={`Proposed by ${draft.provider === "Mock" ? "the local stand-in model" : "the assistant"}`} />
            <span>{draft.items.length} proposals, {draft.acceptedCount} accepted</span>
          </div>
          {draft.textTruncated && (
            <p role="status" className="mb-3 rounded-dls-card border border-dls-border bg-dls-surface p-3 text-[12.5px] text-dls-text-primary">
              The wording is long. Only the first {draft.charactersRead.toLocaleString()} characters were read, so later clauses are not covered by these proposals.
            </p>
          )}
          {draft.items.length === 0 && <EmptyStateSurface title="Nothing was proposed" message="No kinds of claim, documents or questions could be found in this wording." />}

          {KIND_ORDER.map((kind) => {
            const items = draft.items.filter((item) => item.kind === kind);
            if (items.length === 0) return null;
            return (
              <section key={kind} className="mb-4" aria-label={KIND_HEADING[kind]}>
                <h3 className="mb-2 text-[12.5px] font-semibold uppercase tracking-wide text-dls-text-secondary">{KIND_HEADING[kind]}</h3>
                <ul className="space-y-2">
                  {items.map((item) => (
                    <WordingItemRow key={item.itemId} item={item} sourceDocumentId={draft.knowledgeDocumentId} documentTypeKeys={draft.documentTypeKeys} canEdit={canEdit} onChange={(change) => void decide(item, change)} />
                  ))}
                </ul>
              </section>
            );
          })}

          <div className="flex flex-wrap items-center gap-3">
            <Button variant="primary" onClick={() => void apply()} disabled={!canEdit || busy || draft.acceptedCount === 0 || hasUnsavedChanges}>
              {busy ? "Adding..." : `Add ${draft.acceptedCount} accepted ${draft.acceptedCount === 1 ? "item" : "items"} to the product`}
            </Button>
            <Button variant="ghost" onClick={() => void discard()} disabled={!canEdit || busy}>Discard this reading</Button>
          </div>
        </div>
      )}
      {error && <p role="alert" className="mt-3 text-[12.5px] text-dls-danger-text">{error}</p>}
    </div>
  );
}

function WordingItemRow({ item, sourceDocumentId, documentTypeKeys, canEdit, onChange }: {
  item: WordingDraftItem;
  sourceDocumentId: string;
  documentTypeKeys: string[];
  canEdit: boolean;
  onChange: (change: WordingItemChange) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState(item.title);
  const [detail, setDetail] = useState(item.detail);
  const [claimTypes, setClaimTypes] = useState(item.claimTypeNames.join(", "));
  const pill = decisionTone(item);
  const citation = item.citation;
  const where = [citation.clause ? `clause ${citation.clause}` : null, citation.page ? `page ${citation.page}` : null].filter(Boolean).join(", ");

  function save() {
    onChange({
      title,
      detail,
      ...(item.kind === "Document" || item.kind === "Field" ? { claimTypeNames: claimTypes.split(",").map((name) => name.trim()).filter(Boolean) } : {}),
    });
    setEditing(false);
  }

  return (
    <li className="rounded-dls-card border border-dls-border bg-dls-surface p-3" data-testid="wording-item">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <div className="text-[13px] font-semibold text-dls-text-primary">{item.title}{item.edited ? " (edited)" : ""}</div>
          {item.detail && <div className="mt-0.5 text-[12.5px] text-dls-text-secondary">{item.detail}</div>}
          {item.kind === "Document" && (
            <div className="mt-1 text-[12px] text-dls-text-secondary">
              {item.claimTypeNames.length > 0 ? `For ${item.claimTypeNames.join(", ")} claims` : "For every kind of claim"}
              {item.documentKey ? `, kept as ${item.documentKey}` : ""}
            </div>
          )}
          {item.kind === "Field" && item.fieldType && <div className="mt-1 text-[12px] text-dls-text-secondary">Answer type: {item.fieldType}</div>}
        </div>
        <StatusPill tone={pill.tone} label={pill.label} />
      </div>

      <blockquote className="mt-2 border-l-2 border-dls-border pl-3 text-[12px] text-dls-text-secondary">
        {citation.quote}
        <span className="mt-0.5 block text-[11.5px] text-dls-text-muted">
          {citation.verified ? `Found in the wording${where ? `: ${where}` : ""}` : "This quote was not found in the wording"}
        </span>
      </blockquote>
      {citation.verified && (
        <a
          href={`/app/knowledge?documentId=${encodeURIComponent(sourceDocumentId)}`}
          className="mt-2 inline-flex text-[12px] font-semibold text-dls-info-text underline-offset-2 hover:underline"
        >
          Open source wording in the Data Hub
        </a>
      )}

      {item.problem && <p role="note" className="mt-2 text-[12px] text-dls-danger-text">{item.problem}</p>}
      {item.note && !item.problem && <p className="mt-2 text-[12px] text-dls-text-secondary">{item.note}</p>}

      {item.kind === "Document" && item.problem && item.citation.verified && canEdit && (
        <div className="mt-2 flex items-center gap-2">
          <label htmlFor={`doc-${item.itemId}`} className="text-[12px] font-semibold text-dls-text-secondary">Use document type</label>
          <Select id={`doc-${item.itemId}`} defaultValue="" onChange={(event) => event.target.value && onChange({ documentKey: event.target.value })}>
            <option value="">Choose one</option>
            {documentTypeKeys.map((key) => <option key={key} value={key}>{key}</option>)}
          </Select>
        </div>
      )}

      {editing && (
        <div className="mt-3 space-y-2">
          <Input aria-label={`Title of ${item.title}`} value={title} onChange={(event) => setTitle(event.target.value)} maxLength={120} />
          <Input aria-label={`Detail of ${item.title}`} value={detail} onChange={(event) => setDetail(event.target.value)} maxLength={500} />
          {(item.kind === "Document" || item.kind === "Field") && (
            <Input aria-label={`Claim types for ${item.title}`} value={claimTypes} onChange={(event) => setClaimTypes(event.target.value)} placeholder="Claim types, separated by commas, or leave empty for all" />
          )}
          {item.kind === "Field" && (
            <Select aria-label={`Answer type of ${item.title}`} value={item.fieldType ?? "Text"} onChange={(event) => onChange({ fieldType: event.target.value })}>
              {FIELD_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
            </Select>
          )}
          <div className="flex gap-2">
            <Button variant="secondary" onClick={save}>Save edit</Button>
            <Button variant="ghost" onClick={() => setEditing(false)}>Cancel</Button>
          </div>
        </div>
      )}

      {canEdit && !editing && (
        <div className="mt-3 flex flex-wrap gap-2">
          <Button variant="secondary" onClick={() => onChange({ decision: "Accepted" })} disabled={Boolean(item.problem) || item.decision === "Accepted"} aria-label={`Accept ${item.title}`}>Accept</Button>
          <Button variant="ghost" onClick={() => onChange({ decision: "Rejected" })} disabled={item.decision === "Rejected"} aria-label={`Reject ${item.title}`}>Reject</Button>
          <Button variant="ghost" onClick={() => setEditing(true)} aria-label={`Edit ${item.title}`}>Edit</Button>
        </div>
      )}
    </li>
  );
}

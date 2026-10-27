"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "@/components/iip/NextRouterCompat";
import { ArrowUpRight, BookOpenText, Loader2, ScanText } from "lucide-react";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Badge, GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Input, Select } from "@/components/ui/Input";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton } from "@/components/ui/StateSurfaces";
import { useUI } from "@/context/UIStateContext";
import { useKnowledgeDocs } from "@/lib/lovable-parity/data/knowledge";
import { createProduct, extractProductWording, listProductWordingProposals, type ProductWordingProposal } from "@/lib/products";

type ReviewItem = ProductWordingProposal["items"][number] & { include: boolean; editedValue: string };

const CATEGORY_LABELS: Record<string, string> = {
  product_name: "Product name",
  product_class: "Product class",
  insurer: "Insurer",
  intended_customer: "Who it is for",
  cover: "What it covers",
  exclusion: "Exclusion",
  waiting_period: "Waiting period",
  limit: "Limit",
  excess: "Excess",
  premium: "Premium",
  claim_document: "Claim document",
  claim_step: "Claim step",
  time_limit: "Time limit",
  contact: "Contact",
};

export default function ProductWordingSetup() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const navigate = useNavigate();
  const { docs, loading, error, reload } = useKnowledgeDocs();
  const wordings = useMemo(() => docs.filter((doc) => doc.category === "PolicyWording"), [docs]);
  const [selectedId, setSelectedId] = useState("");
  const [proposal, setProposal] = useState<ProductWordingProposal | null>(null);
  const [items, setItems] = useState<ReviewItem[]>([]);
  const [productName, setProductName] = useState("");
  const [productClass, setProductClass] = useState("");
  const [loadingProposal, setLoadingProposal] = useState(false);
  const [extracting, setExtracting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [notConfigured, setNotConfigured] = useState(false);
  const [createdProductId, setCreatedProductId] = useState<string | null>(null);

  const loadProposal = useCallback(async (documentId: string) => {
    setLoadingProposal(true);
    setProblem(null);
    const result = await listProductWordingProposals(role, documentId);
    setLoadingProposal(false);
    if (!result.ok) {
      setProblem(result.error);
      setProposal(null);
      return;
    }
    setProposal(result.value[0] ?? null);
  }, [role]);

  useEffect(() => {
    if (!selectedId) {
      setProposal(null);
      return;
    }
    void loadProposal(selectedId);
  }, [loadProposal, selectedId]);

  useEffect(() => {
    if (!proposal) {
      setItems([]);
      setProductName("");
      setProductClass("");
      return;
    }
    setItems(proposal.items
      .slice()
      .sort((a, b) => a.confidence - b.confidence)
      .map((item) => ({ ...item, include: true, editedValue: item.value })));
    setProductName(proposal.items.find((item) => item.category === "product_name")?.value ?? "");
    setProductClass(proposal.items.find((item) => item.category === "product_class")?.value ?? "");
  }, [proposal]);

  async function runExtraction() {
    if (!selectedId) return;
    setExtracting(true);
    setProblem(null);
    setNotConfigured(false);
    const result = await extractProductWording(role, selectedId);
    setExtracting(false);
    if (!result.ok) {
      setProblem(result.error);
      setNotConfigured(/not configured/i.test(result.error));
      return;
    }
    setProposal(result.value);
  }

  async function saveDraft() {
    if (!proposal) return;
    if (!productName.trim() || !productClass.trim()) {
      setProblem("Enter the product name and class from the wording before saving.");
      return;
    }
    setSaving(true);
    setProblem(null);
    const wordingFacts = items.filter((item) => item.include).map((item) => ({
      category: item.category,
      label: item.label,
      value: item.editedValue.trim(),
      sourceKnowledgeDocumentId: proposal.knowledgeDocumentId,
      sourceChunkId: item.sourceChunkId,
      sourceQuote: item.sourceQuote,
      confidence: item.confidence,
    }));
    const created = await createProduct(role, {
      name: productName.trim(),
      productClass: productClass.trim(),
      changeReason: `Human-confirmed product facts from ${proposal.sourceName}, version ${proposal.sourceVersion}.`,
      useDefaultStageLibrary: true,
      wordingFacts,
    });
    setSaving(false);
    if (!created.ok) {
      setProblem(created.error);
      return;
    }
    setCreatedProductId(created.value.productId);
  }

  const selected = wordings.find((doc) => doc.id === selectedId);

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Products"
        title="Prepare a product from its wording"
        sub="The reader proposes facts with source passages. Review each one before it becomes a draft product."
        breadcrumb={[{ label: "Products", href: "/app/admin/products" }, { label: "Prepare from wording" }]}
      />
      <div className="mb-4"><GuardrailBanner text="AI prepares a draft only. It does not set prices or publish a product. Publishing still needs the IRA approval details and a second person's approval." /></div>

      {loading ? <GlassCard><LoadingSkeleton rows={3} /></GlassCard> : error ? <GlassCard><ErrorStateSurface title="Wordings could not be loaded" message={error} onRetry={() => void reload()} /></GlassCard> : wordings.length === 0 ? (
        <GlassCard>
          <EmptyStateSurface title="No policy wordings are available" message="Upload a readable policy wording in the Data Hub first. Extraction will use only text available in that document." action={{ label: "Open the Data Hub", onClick: () => navigate("/app/knowledge") }} />
        </GlassCard>
      ) : (
        <div className="space-y-4">
          <GlassCard>
            <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
              <Field label="Policy wording" hint="Only wording text from this workspace is sent for extraction." htmlFor="wording-source">
                <Select id="wording-source" value={selectedId} onChange={(event) => { setSelectedId(event.target.value); setProblem(null); setNotConfigured(false); }}>
                  <option value="">Choose a wording</option>
                  {wordings.map((doc) => <option key={doc.id} value={doc.id}>{doc.title}</option>)}
                </Select>
              </Field>
              <Button variant="primary" disabled={!selectedId || extracting || loadingProposal} onClick={() => void runExtraction()} icon={extracting ? <Loader2 className="animate-spin" aria-hidden="true" /> : <ScanText aria-hidden="true" />}>
                {proposal ? "Read again" : "Read this wording"}
              </Button>
            </div>
            {selected ? <p className="mb-0 mt-2 text-[12px] text-dls-text-muted">{selected.status}. A person still needs to approve this wording separately for customer answers.</p> : null}
          </GlassCard>

          {loadingProposal ? <GlassCard><LoadingSkeleton rows={3} /></GlassCard> : null}
          {notConfigured ? <GlassCard><p className="m-0 text-[13px] text-dls-text-secondary">Wording extraction is <strong>Not configured</strong> on this platform. Ask a workspace administrator to configure the AI provider.</p></GlassCard> : null}
          {problem && !notConfigured ? <GlassCard><p className="m-0 text-[13px] text-dls-danger-text" role="alert">{problem}</p></GlassCard> : null}

          {proposal ? (
            <>
              <GlassCard>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div><h2 className="m-0 text-[15px] font-bold text-dls-text-primary">What the reader found</h2><p className="mb-0 mt-1 text-[12.5px] text-dls-text-secondary">{proposal.sourceName}, version {proposal.sourceVersion}. Prepared {new Date(proposal.preparedAt).toLocaleString()} using {proposal.provider}{proposal.modelVersion ? `, ${proposal.modelVersion}` : ""}.</p></div>
                  <Badge tone="warning">Human review required</Badge>
                </div>
                {proposal.items.length === 0 ? <p className="mb-0 mt-4 text-[13px] text-dls-text-secondary">No facts could be supported by exact source passages. Check the document has readable text or upload a clearer copy.</p> : null}
                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  <Field label="Product name" htmlFor="wording-product-name"><Input id="wording-product-name" required value={productName} onChange={(event) => setProductName(event.target.value)} /></Field>
                  <Field label="Product class" hint="For example, Life or Motor. Use the wording's own description." htmlFor="wording-product-class"><Input id="wording-product-class" required value={productClass} onChange={(event) => setProductClass(event.target.value)} /></Field>
                </div>
              </GlassCard>

              {items.map((item, index) => (
                <GlassCard key={`${item.sourceChunkId}-${item.category}-${index}`}>
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <label className="flex items-start gap-2 text-[13px] font-semibold text-dls-text-primary">
                      <input type="checkbox" className="mt-0.5" checked={item.include} onChange={(event) => setItems((current) => current.map((value, i) => i === index ? { ...value, include: event.target.checked } : value))} />
                      Include this item
                    </label>
                    <Badge tone={item.confidence < 0.6 ? "warning" : "neutral"}>{Math.round(item.confidence * 100)}% model estimate</Badge>
                  </div>
                  <p className="mb-1 mt-3 text-[11px] font-bold uppercase tracking-wide text-dls-text-muted">{CATEGORY_LABELS[item.category] ?? item.category} · {item.label}</p>
                  <Field label="Proposed value" htmlFor={`wording-value-${index}`}>
                    <Input id={`wording-value-${index}`} value={item.editedValue} disabled={!item.include} onChange={(event) => setItems((current) => current.map((value, i) => i === index ? { ...value, editedValue: event.target.value } : value))} />
                  </Field>
                  <div className="mt-3 rounded-dls-card border border-dls-border-subtle bg-dls-bg-page p-3">
                    <div className="mb-1 flex items-center gap-1.5 text-[11px] font-semibold text-dls-text-secondary"><BookOpenText className="h-3.5 w-3.5" aria-hidden="true" /> Source passage</div>
                    <blockquote className="m-0 text-[12.5px] leading-5 text-dls-text-primary">“{item.sourceQuote}”</blockquote>
                    <p className="mb-0 mt-1 text-[10.5px] text-dls-text-muted">Source chunk {item.sourceChunkId}. This quote is checked against the saved wording.</p>
                  </div>
                </GlassCard>
              ))}

              <GlassCard>
                <p className="mb-3 mt-0 text-[12.5px] text-dls-text-secondary">Saving creates a product draft with the facts you include. Add the IRA approval reference and effective date in the next screen, then submit it for a second person's review.</p>
                {problem ? <p className="mb-3 text-[13px] text-dls-danger-text" role="alert">{problem}</p> : null}
                <div className="flex flex-wrap items-center gap-2">
                  {createdProductId ? <><p className="m-0 text-[13px] font-semibold text-dls-text-primary" role="status">Product draft created.</p><Link to={`/app/admin/products/${createdProductId}`} className="text-[13px] font-semibold text-dls-text-secondary underline">Open product draft</Link></> : <Button variant="primary" disabled={saving || !proposal.items.length || items.every((item) => !item.include)} onClick={() => void saveDraft()} icon={saving ? <Loader2 className="animate-spin" aria-hidden="true" /> : <ArrowUpRight aria-hidden="true" />}>Create product draft</Button>}
                  <Link to="/app/admin/products" className="text-[13px] font-semibold text-dls-text-secondary underline">Return to products</Link>
                </div>
              </GlassCard>
            </>
          ) : null}
        </div>
      )}
    </>
  );
}

"use client";

import { useCallback, useEffect, useId, useState } from "react";
import { Link, useNavigate } from "@/components/iip/NextRouterCompat";
import { Plus } from "lucide-react";
import { GlassCard, SectionHeader } from "@/components/iip/Primitives";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { Button } from "@/components/ui/Button";
import { Input, Field } from "@/components/ui/Input";
import { LoadingSkeleton, ErrorStateSurface, EmptyStateSurface } from "@/components/ui/StateSurfaces";
import { Table, TableHead, TableBody, TableRow, TableHeaderCell, TableCell } from "@/components/ui/Table";
import { useUI } from "@/context/UIStateContext";
import { createProduct, listProducts, type ProductListItem, type ProductStatus } from "@/lib/products";

const STATUS_TONE: Record<ProductStatus, PillTone> = {
  Draft: "neutral",
  PendingPublishApproval: "warning",
  Published: "success",
  Retired: "danger",
};

const STATUS_LABEL: Record<ProductStatus, string> = {
  Draft: "Draft",
  PendingPublishApproval: "Pending publish approval",
  Published: "Published",
  Retired: "Retired",
};

export default function Products() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const navigate = useNavigate();

  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const result = await listProducts(role);
    setLoading(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setProducts(result.value);
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Products"
        title="Products"
        sub="A product's claim process, document matrix and IRA approval, configured here with no developer and no release. Every change is versioned and audited."
        action={<Button variant="primary" icon={<Plus aria-hidden="true" />} onClick={() => setCreateOpen(true)}>New product</Button>}
      />

      {loading && <GlassCard><LoadingSkeleton rows={4} /></GlassCard>}
      {!loading && error && <GlassCard><ErrorStateSurface title="Products could not be loaded" message={error} onRetry={load} /></GlassCard>}
      {!loading && !error && products.length === 0 && (
        <GlassCard>
          <EmptyStateSurface
            title="No products yet"
            message="A product configured here will appear on this list, with its claim process, document matrix and IRA approval."
            action={{ label: "New product", onClick: () => setCreateOpen(true) }}
          />
        </GlassCard>
      )}
      {!loading && !error && products.length > 0 && (
        <Table label="Products">
          <TableHead>
            <TableRow>
              <TableHeaderCell>Product</TableHeaderCell>
              <TableHeaderCell>Class</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell numeric>Version</TableHeaderCell>
              <TableHeaderCell>Last updated</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {products.map((product) => (
              <TableRow key={product.productId}>
                <TableCell>
                  <Link to={`/app/admin/products/${product.productId}`} className="dls-focus-outline font-bold text-dls-primary hover:underline">
                    {product.name}
                  </Link>
                </TableCell>
                <TableCell className="text-dls-text-secondary">{product.productClass}</TableCell>
                <TableCell><StatusPill tone={STATUS_TONE[product.status]} label={STATUS_LABEL[product.status]} /></TableCell>
                <TableCell numeric>v{product.version}</TableCell>
                <TableCell className="text-dls-text-muted">{new Date(product.updatedAt).toLocaleString()}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {createOpen && (
        <CreateProductDialog
          role={role}
          onClose={() => setCreateOpen(false)}
          onCreated={(product) => {
            setCreateOpen(false);
            navigate(`/app/admin/products/${product.productId}`);
          }}
        />
      )}
    </>
  );
}

function CreateProductDialog({ role, onClose, onCreated }: { role: string; onClose: () => void; onCreated: (product: { productId: string }) => void }) {
  const nameId = useId();
  const classId = useId();
  const reasonId = useId();
  const [name, setName] = useState("");
  const [productClass, setProductClass] = useState("");
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit() {
    if (!name.trim() || !productClass.trim() || !reason.trim()) {
      setError("Name, class and a reason are all required.");
      return;
    }
    setSubmitting(true);
    setError(null);
    const result = await createProduct(role, { name: name.trim(), productClass: productClass.trim(), changeReason: reason.trim(), useDefaultStageLibrary: true });
    setSubmitting(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    onCreated(result.value);
  }

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-dls-scrim/45 p-4" onClick={submitting ? undefined : onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-product-title"
        className="w-[min(480px,92vw)] rounded-dls-overlay border border-dls-border-subtle bg-dls-surface p-6 shadow-dls-modal"
        onClick={(event) => event.stopPropagation()}
      >
        <h3 id="create-product-title" className="text-[17px] font-extrabold text-dls-text-primary">New product</h3>
        <p className="mt-1 text-[12.5px] text-dls-text-secondary">Starts as a draft with the default eight-stage claim process. You can edit everything before publishing.</p>
        <div className="mt-4 space-y-4">
          <Field label="Product name" htmlFor={nameId}>
            <Input id={nameId} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Motor Comprehensive" />
          </Field>
          <Field label="Product class" htmlFor={classId}>
            <Input id={classId} value={productClass} onChange={(e) => setProductClass(e.target.value)} placeholder="e.g. Motor" />
          </Field>
          <Field label="Reason for audit trail" htmlFor={reasonId}>
            <Input id={reasonId} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. New product launch for Q4" />
          </Field>
        </div>
        {error && <p className="mt-3 text-[12.5px] text-dls-danger-text" role="alert">{error}</p>}
        <div className="mt-5 flex justify-end gap-3">
          <Button variant="ghost" onClick={onClose} disabled={submitting}>Cancel</Button>
          <Button variant="primary" onClick={onSubmit} disabled={submitting}>{submitting ? "Creating…" : "Create draft"}</Button>
        </div>
      </div>
    </div>
  );
}

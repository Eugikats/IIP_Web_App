"use client";

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { toast } from "sonner";
import { AlertTriangle, Check, Loader2 } from "lucide-react";
import { useUI } from "@/context/UIStateContext";
import { Badge, GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Input, Field as UIField } from "@/components/ui/Input";
import { Table, TableBody, TableCell, TableHead, TableHeaderCell, TableRow } from "@/components/ui/Table";
import { EmptyStateSurface, ErrorStateSurface, LoadingSkeleton, NoPermissionStateSurface } from "@/components/ui/StateSurfaces";
import {
  cancelPlan, describeAllowance, formatMoney, loadInvoices, loadPayment, loadPlan, loadReceipt, methodLabels, resumePlan, startCheckout,
  type BillingPlan, type InvoiceView, type PaymentView, type PlanView, type Receipt,
} from "@/lib/billingPlans";

const POLL_MS = 3000;

function periodText(value: string | null) {
  return value ? new Date(value).toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" }) : null;
}

function statusTone(status: string) {
  return status === "Active" ? "success" : status === "PastDue" ? "warning" : status === "Lapsed" ? "error" : "neutral";
}

/** Plan, upgrade prompts, the plans on offer, paying for one, and the invoices and receipts. Everything here is read from the service. */
export function BillingPlans({ onChanged, usage }: { onChanged: () => void; usage?: ReactNode }) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [plan, setPlan] = useState<BillingPlan | null>(null);
  const [invoices, setInvoices] = useState<InvoiceView[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [currency, setCurrency] = useState("UGX");
  const [choice, setChoice] = useState<PlanView | null>(null);
  const [receipt, setReceipt] = useState<Receipt | null>(null);

  const load = useCallback(async () => {
    const [planResult, invoiceResult] = await Promise.all([loadPlan(role), loadInvoices(role)]);
    if (planResult.ok) {
      setPlan(planResult.value);
      setError(null);
      setCurrency((current) => (planResult.value.currencies.includes(current) ? current : planResult.value.currencies[0] ?? "UGX"));
    } else {
      setForbidden(planResult.forbidden === true);
      setError(planResult.error);
    }

    if (invoiceResult.ok) setInvoices(invoiceResult.value);
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  async function changed() {
    await load();
    onChanged();
  }

  if (forbidden) return <GlassCard className="mb-4"><NoPermissionStateSurface permission="ViewBilling" /></GlassCard>;
  if (error) return <GlassCard className="mb-4"><ErrorStateSurface title="Plans could not be loaded" message={error} /></GlassCard>;
  if (!plan) return <GlassCard className="mb-4"><LoadingSkeleton rows={3} /></GlassCard>;

  const onPlan = plan.current.key !== "standard";
  const canPay = plan.paymentMethods.length > 0;

  return (
    <div data-testid="billing-plans">
      {plan.prompts.map((prompt) => (
        <div
          key={`${prompt.meter}-${prompt.level}`}
          role="status"
          data-testid={`upgrade-prompt-${prompt.meter}`}
          className="mb-3 flex items-start gap-2 rounded-dls-control border border-dls-border-subtle bg-dls-surface px-4 py-3 text-[13px] text-dls-text-primary"
        >
          <AlertTriangle className={`mt-0.5 h-4 w-4 shrink-0 ${prompt.level === "limit" ? "text-dls-danger-text" : "text-dls-warning-text"}`} aria-hidden="true" />
          <span>{prompt.message}</span>
        </div>
      ))}

      <GlassCard className="mb-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Your plan</div>
            <div className="mt-1 flex items-center gap-2">
              <span className="text-[20px] font-bold text-dls-text-primary" data-testid="current-plan-name">{plan.current.name}</span>
              <Badge tone={statusTone(plan.current.status) as never}>{plan.current.status === "PastDue" ? "Payment overdue" : plan.current.status === "Lapsed" ? "Lapsed" : "Active"}</Badge>
            </div>
            {plan.current.periodEnd ? (
              <div className="mt-1 text-[12.5px] text-dls-text-secondary">
                {plan.current.cancelAtPeriodEnd ? "Ends on" : "Paid until"} {periodText(plan.current.periodEnd)}
                {plan.current.cancelAtPeriodEnd ? ". It will not renew, and the workspace then moves to the Free plan." : ""}
              </div>
            ) : onPlan ? (
              <div className="mt-1 text-[12.5px] text-dls-text-secondary">No payment is due.</div>
            ) : (
              <div className="mt-1 text-[12.5px] text-dls-text-secondary">This workspace keeps the limits in its operating settings.</div>
            )}
          </div>
          {plan.current.periodEnd && plan.current.key !== "free" ? (
            <Button
              variant="ghost"
              onClick={async () => {
                const result = plan.current.cancelAtPeriodEnd ? await resumePlan(role) : await cancelPlan(role);
                if (!result.ok) return toast.error(result.error);
                toast.success(plan.current.cancelAtPeriodEnd ? "The plan will renew as before." : "The plan will end when the paid period does.");
                await changed();
              }}
            >
              {plan.current.cancelAtPeriodEnd ? "Keep my plan" : "Do not renew"}
            </Button>
          ) : null}
        </div>
      </GlassCard>

      {usage}

      {onPlan ? (
        <GlassCard className="mb-4">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <div className="text-[14px] font-bold text-dls-text-primary">Plans</div>
            <div className="inline-flex rounded-dls-control border border-dls-border-subtle p-0.5" role="group" aria-label="Currency">
              {plan.currencies.map((code) => (
                <button
                  key={code}
                  type="button"
                  aria-pressed={currency === code}
                  onClick={() => setCurrency(code)}
                  className={`rounded-dls-control px-3 py-1 text-[12px] font-semibold ${currency === code ? "bg-dls-primary text-white" : "text-dls-text-secondary"}`}
                >
                  {code}
                </button>
              ))}
            </div>
          </div>
          {!canPay ? <p className="mb-3 text-[12.5px] text-dls-text-muted" data-testid="payments-not-configured">Not configured: payments are not set up on this deployment, so a plan cannot be bought here yet.</p> : null}
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {plan.plans.map((item) => {
              const price = item.prices.find((entry) => entry.currency === currency);
              return (
                <div key={item.key} data-testid={`plan-${item.key}`} className={`flex flex-col rounded-dls-card border p-4 ${item.isCurrent ? "border-dls-primary" : "border-dls-border-subtle"}`}>
                  <div className="text-[15px] font-bold text-dls-text-primary">{item.name}</div>
                  <div className="mt-1 text-[12px] text-dls-text-secondary">{item.summary}</div>
                  <div className="mt-3 text-[20px] font-bold text-dls-text-primary">
                    {!item.selfServe ? "By agreement" : price && price.monthlyAmount > 0 ? <>{formatMoney(price.monthlyAmount, currency)}<span className="text-[12px] font-normal text-dls-text-muted"> a month</span></> : "Free"}
                  </div>
                  <ul className="mt-3 flex-1 space-y-1 text-[12.5px] text-dls-text-secondary">
                    <li>{describeAllowance(item.allowances.claimsPerMonth, "claims a month")}</li>
                    <li>{describeAllowance(item.allowances.documentsPerMonth, "documents a month")}</li>
                    <li>{describeAllowance(item.allowances.aiTokensPerMonth, "AI tokens a month")}</li>
                    <li>{describeAllowance(item.allowances.storageMb, "MB of storage")}</li>
                    <li>{describeAllowance(item.allowances.seats, "people")}</li>
                  </ul>
                  <div className="mt-4">
                    {item.isCurrent ? (
                      <Badge tone="info">Current plan</Badge>
                    ) : !item.selfServe ? (
                      <span className="text-[12px] text-dls-text-muted">Ask your IIP contact.</span>
                    ) : price && price.monthlyAmount > 0 ? (
                      <Button variant="primary" disabled={!canPay} onClick={() => setChoice(item)}>{plan.current.key === "free" || plan.current.status === "Lapsed" ? "Choose" : "Switch to"} {item.name}</Button>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>
        </GlassCard>
      ) : null}

      {choice ? (
        <Checkout
          plan={choice}
          currency={currency}
          methods={plan.paymentMethods}
          role={role}
          onClose={() => setChoice(null)}
          onPaid={async () => {
            setChoice(null);
            toast.success(`You are now on the ${choice.name} plan.`);
            await changed();
          }}
          onSettled={load}
        />
      ) : null}

      <GlassCard className="mb-4" data-testid="invoices">
        <div className="mb-3 text-[14px] font-bold text-dls-text-primary">Invoices and receipts</div>
        {invoices === null ? (
          <LoadingSkeleton rows={2} />
        ) : invoices.length === 0 ? (
          <EmptyStateSurface title="No invoices yet" message="An invoice appears here when you choose a paid plan." />
        ) : (
          <Table label="Invoices">
            <TableHead>
              <TableRow>
                <TableHeaderCell>Invoice</TableHeaderCell>
                <TableHeaderCell>Plan</TableHeaderCell>
                <TableHeaderCell>Amount</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
                <TableHeaderCell>Issued</TableHeaderCell>
                <TableHeaderCell numeric>Receipt</TableHeaderCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {invoices.map((invoice) => (
                <TableRow key={invoice.invoiceId}>
                  <TableCell>{invoice.number}</TableCell>
                  <TableCell>{invoice.planName} <span className="text-dls-text-muted">({invoice.kind.toLowerCase()})</span></TableCell>
                  <TableCell>{formatMoney(invoice.amount, invoice.currency)}</TableCell>
                  <TableCell><Badge tone={invoice.status === "Paid" ? "success" : "warning"}>{invoice.status === "Open" ? "Unpaid" : invoice.status}</Badge></TableCell>
                  <TableCell className="text-dls-text-secondary">{periodText(invoice.issuedAt)}</TableCell>
                  <TableCell numeric>
                    {invoice.status === "Paid" ? (
                      <Button
                        variant="ghost"
                        onClick={async () => {
                          const result = await loadReceipt(role, invoice.invoiceId);
                          if (!result.ok) return toast.error(result.error);
                          setReceipt(result.value);
                        }}
                      >
                        View
                      </Button>
                    ) : null}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </GlassCard>

      {receipt ? <ReceiptView receipt={receipt} onClose={() => setReceipt(null)} /> : null}
    </div>
  );
}

function Checkout({
  plan, currency, methods, role, onClose, onPaid, onSettled,
}: { plan: PlanView; currency: string; methods: string[]; role: string; onClose: () => void; onPaid: () => Promise<void>; onSettled: () => Promise<void> }) {
  const price = plan.prices.find((entry) => entry.currency === currency)?.monthlyAmount ?? 0;
  const [method, setMethod] = useState(methods[0] ?? "");
  const [phone, setPhone] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [payment, setPayment] = useState<PaymentView | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  const watch = useCallback((id: string) => {
    timer.current = setTimeout(async () => {
      const result = await loadPayment(role, id);
      if (!result.ok) return watch(id);
      setPayment(result.value);
      if (result.value.status === "Pending") return watch(id);
      if (result.value.status === "Succeeded") await onPaid();
      else await onSettled();
    }, POLL_MS);
  }, [role, onPaid, onSettled]);

  async function pay() {
    setBusy(true);
    setError(null);
    const result = await startCheckout(role, { planKey: plan.key, method, currency, payerPhone: method === "card" ? undefined : phone });
    setBusy(false);
    if (!result.ok) return setError(result.error);
    setPayment(result.value);
    if (result.value.status === "Pending") {
      watch(result.value.paymentIntentId);
      if (result.value.redirectUrl) window.location.assign(result.value.redirectUrl);
    } else if (result.value.status === "Succeeded") {
      await onPaid();
    } else {
      await onSettled();
    }
  }

  const pending = payment?.status === "Pending";
  return (
    <GlassCard className="mb-4" data-testid="checkout">
      <div className="mb-1 text-[15px] font-bold text-dls-text-primary">Pay for the {plan.name} plan</div>
      <p className="mb-3 text-[12.5px] text-dls-text-secondary">{formatMoney(price, currency)} for one month. It starts when the payment is received and does not renew by itself: we send an invoice before it ends.</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <fieldset disabled={pending || busy} className="space-y-2">
          <legend className="mb-1 text-[12px] font-semibold text-dls-text-primary">How would you like to pay?</legend>
          {methods.map((code) => (
            <label key={code} className="flex items-center gap-2 text-[13px] text-dls-text-primary">
              <input type="radio" name="payment-method" value={code} checked={method === code} onChange={() => setMethod(code)} />
              {methodLabels[code] ?? code}
            </label>
          ))}
        </fieldset>
        {method !== "card" ? (
          <UIField label="Mobile money number" htmlFor="payer-phone">
            <Input id="payer-phone" inputMode="numeric" autoComplete="tel" placeholder="256700123456" value={phone} disabled={pending || busy} onChange={(event) => setPhone(event.target.value)} />
            <p className="mt-1 text-[11.5px] text-dls-text-muted">With the country code and no plus sign. You will be asked to approve the payment on this phone.</p>
          </UIField>
        ) : (
          <p className="text-[12.5px] text-dls-text-secondary">You will be taken to a secure page to enter your card details. They are never sent to IIP.</p>
        )}
      </div>
      {error ? <div role="alert" className="mt-3 flex items-start gap-2 text-[12.5px] text-dls-danger-text"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /><span>{error}</span></div> : null}
      {payment ? (
        <div role="status" data-testid="payment-status" className="mt-3 rounded-dls-control border border-dls-border-subtle px-3 py-2 text-[13px] text-dls-text-primary">
          {payment.status === "Pending" ? (
            <span className="flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />{payment.payerInstruction ?? "Waiting for the payment."} Waiting for the payment to be confirmed.</span>
          ) : payment.status === "Succeeded" ? (
            <span className="flex items-center gap-2"><Check className="h-4 w-4" aria-hidden="true" />Payment received.</span>
          ) : (
            <span>The payment did not go through. {payment.failureReason} Nothing was charged and your plan is unchanged. You can try again.</span>
          )}
        </div>
      ) : null}
      <div className="mt-4 flex justify-end gap-2">
        <Button variant="ghost" onClick={onClose} disabled={busy}>{pending ? "Close" : "Cancel"}</Button>
        <Button variant="primary" onClick={pay} disabled={busy || pending || !method || (method !== "card" && phone.trim().length === 0)}>
          {busy ? "Starting..." : `Pay ${formatMoney(price, currency)}`}
        </Button>
      </div>
    </GlassCard>
  );
}

function ReceiptView({ receipt, onClose }: { receipt: Receipt; onClose: () => void }) {
  const { invoice } = receipt;
  return (
    <GlassCard className="mb-4 print:border-0" data-testid="receipt">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-[11px] font-bold uppercase tracking-[0.14em] text-dls-text-muted">Receipt</div>
          <div className="text-[18px] font-bold text-dls-text-primary" data-testid="receipt-number">{invoice.receiptNumber}</div>
        </div>
        <div className="flex gap-2 print:hidden">
          <Button variant="ghost" onClick={() => window.print()}>Print</Button>
          <Button variant="ghost" onClick={onClose}>Close</Button>
        </div>
      </div>
      <dl className="mt-3 grid gap-x-6 gap-y-1 text-[13px] sm:grid-cols-2">
        <div><dt className="text-dls-text-muted">Received from</dt><dd className="text-dls-text-primary">{receipt.customer}</dd></div>
        <div><dt className="text-dls-text-muted">Invoice</dt><dd className="text-dls-text-primary">{invoice.number}</dd></div>
        <div><dt className="text-dls-text-muted">Plan</dt><dd className="text-dls-text-primary">{invoice.planName}, one month</dd></div>
        <div><dt className="text-dls-text-muted">Amount</dt><dd className="text-dls-text-primary">{formatMoney(invoice.amount, invoice.currency)}</dd></div>
        <div><dt className="text-dls-text-muted">Paid with</dt><dd className="text-dls-text-primary">{invoice.paidWith}</dd></div>
        <div><dt className="text-dls-text-muted">Paid on</dt><dd className="text-dls-text-primary">{periodText(invoice.paidAt)}</dd></div>
        <div className="sm:col-span-2"><dt className="text-dls-text-muted">Covers</dt><dd className="text-dls-text-primary">{periodText(invoice.coversFrom)} to {periodText(invoice.coversTo)}</dd></div>
      </dl>
    </GlassCard>
  );
}

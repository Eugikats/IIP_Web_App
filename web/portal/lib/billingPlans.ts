"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type PlanAllowances = { claimsPerMonth: number; documentsPerMonth: number; aiTokensPerMonth: number; storageMb: number; seats: number };
export type PlanPrice = { currency: string; monthlyAmount: number };
export type PlanView = { key: string; name: string; summary: string; selfServe: boolean; isCurrent: boolean; prices: PlanPrice[]; allowances: PlanAllowances };
export type CurrentPlan = { key: string; name: string; status: string; periodEnd: string | null; cancelAtPeriodEnd: boolean; currency: string; allowances: PlanAllowances };
export type UpgradePrompt = { meter: string; level: "warning" | "limit"; message: string; upgradeToPlanKey: string | null };
export type BillingPlan = { current: CurrentPlan; plans: PlanView[]; paymentMethods: string[]; currencies: string[]; prompts: UpgradePrompt[] };

export type PaymentView = {
  paymentIntentId: string;
  status: "Pending" | "Succeeded" | "Failed" | "Expired";
  method: string;
  amount: number;
  currency: string;
  description: string;
  redirectUrl: string | null;
  payerInstruction: string | null;
  failureReason: string | null;
  invoiceId: string | null;
  createdAt: string;
};

export type InvoiceView = {
  invoiceId: string;
  number: string;
  planName: string;
  amount: number;
  currency: string;
  kind: string;
  status: "Open" | "Paid" | "Void";
  issuedAt: string;
  paidAt: string | null;
  receiptNumber: string | null;
  paidWith: string | null;
  coversFrom: string | null;
  coversTo: string | null;
};

export type Receipt = { customer: string; invoice: InvoiceView };

export type Outcome<T> = { ok: true; value: T } | { ok: false; error: string };

const unreachable = "The service could not be reached. Check your connection and try again.";

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { detail?: string; message?: string; errors?: Record<string, string[]> };
    const first = Object.values(payload.errors ?? {})[0]?.[0];
    return first ?? payload.detail ?? payload.message ?? fallback;
  } catch {
    return fallback;
  }
}

async function get<T>(role: string, path: string, fallback: string): Promise<Outcome<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    return response.ok ? { ok: true, value: (await response.json()) as T } : { ok: false, error: await failure(response, fallback) };
  } catch {
    return { ok: false, error: unreachable };
  }
}

async function post(role: string, path: string, body: unknown, fallback: string): Promise<Outcome<Response>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    return response.ok ? { ok: true, value: response } : { ok: false, error: await failure(response, fallback) };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export const loadPlan = (role: string) => get<BillingPlan>(role, "/api/v1/admin/billing/plan", "The plan could not be loaded.");
export const loadInvoices = (role: string) => get<InvoiceView[]>(role, "/api/v1/admin/billing/invoices", "Invoices could not be loaded.");
export const loadPayment = (role: string, id: string) => get<PaymentView>(role, `/api/v1/admin/billing/payments/${id}`, "The payment could not be checked.");
export const loadReceipt = (role: string, invoiceId: string) => get<Receipt>(role, `/api/v1/admin/billing/invoices/${invoiceId}/receipt`, "The receipt could not be loaded.");

export async function startCheckout(role: string, body: { planKey: string; method: string; currency: string; payerPhone?: string }): Promise<Outcome<PaymentView>> {
  const result = await post(role, "/api/v1/admin/billing/checkout", body, "The payment could not be started.");
  return result.ok ? { ok: true, value: (await result.value.json()) as PaymentView } : result;
}

export async function cancelPlan(role: string): Promise<Outcome<true>> {
  const result = await post(role, "/api/v1/admin/billing/cancel", undefined, "The plan could not be cancelled.");
  return result.ok ? { ok: true, value: true } : result;
}

export async function resumePlan(role: string): Promise<Outcome<true>> {
  const result = await post(role, "/api/v1/admin/billing/resume", undefined, "The plan could not be resumed.");
  return result.ok ? { ok: true, value: true } : result;
}

export function formatMoney(amount: number, currency: string): string {
  const number = new Intl.NumberFormat("en-US", { maximumFractionDigits: currency === "UGX" ? 0 : 2 }).format(amount);
  return currency === "USD" ? `US$ ${number}` : `${currency} ${number}`;
}

export const methodLabels: Record<string, string> = { "mtn-momo": "MTN Mobile Money", "airtel-money": "Airtel Money", card: "Card" };

export function describeAllowance(value: number, unit: string): string {
  return value > 0 ? `${value.toLocaleString("en-US")} ${unit}` : `No set limit on ${unit}`;
}

/** The pretend card page used in developer runtimes: settles the pretend payment the way an operator's own page would. */
export async function settleMockPayment(role: string, reference: string, outcome: "succeeded" | "failed"): Promise<Outcome<true>> {
  const result = await post(role, `/api/v1/dev/mock-payments/${encodeURIComponent(reference)}/settle`, { outcome }, "The pretend payment could not be settled.");
  return result.ok ? { ok: true, value: true } : result;
}

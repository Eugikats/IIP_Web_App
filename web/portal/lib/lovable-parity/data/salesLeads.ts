"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { leads as mockLeads } from "@/lib/lovable-parity/mock";

// The acquisition list and a lead's record, from GET /api/v1/sales-leads. Every label here (stage,
// relationship, consent) is derived by the API from recorded facts; the screen only shows it.

export type LeadStage = "New" | "GettingToKnow" | "Interested" | "Considering" | "ReadyToApply" | "Applied" | "WithAPerson" | "NotNow";
export type LeadRelationship = "NewToInsurer" | "Registered" | "ExistingCustomer";
export type ConsentStatus = "NotRecorded" | "Agreed" | "Declined" | "Withdrawn";

export type LeadFit = { product: string; productLine: string; insurer: string; score: number; reasons: string[]; citationTitle: string | null; citationDocumentId: string | null };
export type LeadTriage = {
  score: number;
  band: "Cold" | "Early" | "Warm" | "Qualified" | "HighPriority";
  bandLabel: string;
  confidence: string;
  scoredAt: string;
  fits: LeadFit[];
  signals: string[];
  missing: string[];
};

export type SalesLeadView = {
  leadId: string;
  customerName: string;
  leadSource: string;
  productFit: string;
  quoteStatus: string;
  assignedSalesOfficer: string | null;
  createdAt: string | null;
  maskedContact: string;
  occupation: string | null;
  relationship: LeadRelationship;
  relationshipLabel: string;
  primaryChannel: string | null;
  productInterest: string | null;
  stage: LeadStage;
  stageLabel: string;
  assignedOfficerName: string | null;
  lastInteractionAt: string | null;
  consentStatus: ConsentStatus;
  bankCustomer: boolean | null;
  bankCustomerLabel: string;
  conversationId: string | null;
  triage: LeadTriage | null;
};

export type CreateLeadInput = {
  customerName: string;
  leadSource: string;
  customerNeed: string;
  productFit?: string;
  assignedSalesOfficer?: string;
  contactInfo?: string;
  bankCustomer?: boolean | null;
};

export type InsightPoint = { text: string; refs: string[] };
export type InsightInterest = { product: string; strength: string; text: string; refs: string[] };
export type InsightSource = { tag: string; turnId: string; at: string; excerpt: string };
export type ConversationInsights = {
  generatedAt: string;
  messageCount: number;
  enoughConversation: boolean;
  summary: InsightPoint | null;
  interests: InsightInterest[];
  problems: InsightPoint[];
  objections: InsightPoint[];
  tone: string | null;
  nextStep: InsightPoint | null;
  sources: InsightSource[];
};

export type LeadFact = { key: string; label: string; value: string; source: string; recordedAt: string };
export type LeadInterest = { product: string; strength: string; strengthLabel: string; reason: string; recordedAt: string };
export type LeadNote = { noteId: string; text: string; authorUserId: string; authorName: string; at: string };
export type LeadActivity = { at: string; text: string; actor: string };
export type LeadPolicy = { policyNumber: string; productLine: string; effectiveTo: string | null };

export type SalesLeadDetail = {
  leadId: string;
  customerName: string;
  leadSource: string;
  customerNeed: string;
  conversationSummary: string;
  productFit: string;
  quoteStatus: string;
  requiredInformation: string[];
  followUpActions: string[];
  assignedSalesOfficer: string | null;
  conversionProbability: number | null;
  contactInfo: string | null;
  createdAt: string | null;
  createdBy: string | null;
  maskedContact: string;
  primaryChannel: string | null;
  lastInteractionAt: string | null;
  stage: LeadStage;
  stageLabel: string;
  relationship: LeadRelationship;
  relationshipLabel: string;
  assignedOfficerName: string | null;
  occupation: string | null;
  productInterest: string | null;
  appliedFor: string | null;
  bankCustomer: boolean | null;
  bankCustomerLabel: string;
  hasConversation: boolean;
  conversationId: string | null;
  consent: { status: ConsentStatus; statusLabel: string; at: string | null; wording: string | null; channel: string | null };
  facts: LeadFact[];
  missing: string[];
  interests: LeadInterest[];
  notes: LeadNote[];
  activity: LeadActivity[];
  policies: LeadPolicy[];
  insights: ConversationInsights | null;
  insightsStale: boolean;
  triage: LeadTriage | null;
};

export type LeadResult = { ok: true; detail: SalesLeadDetail } | { ok: false; message: string; status?: number };

const PARITY: SalesLeadView[] = mockLeads.map((l) => ({
  leadId: l.id,
  customerName: l.name,
  leadSource: l.source,
  productFit: l.product,
  quoteStatus: l.status,
  assignedSalesOfficer: l.officer,
  createdAt: null,
  maskedContact: "Hidden",
  occupation: null,
  relationship: "NewToInsurer",
  relationshipLabel: "New to insurer",
  primaryChannel: null,
  productInterest: l.product,
  stage: "New",
  stageLabel: "New",
  assignedOfficerName: l.officer,
  lastInteractionAt: null,
  consentStatus: "NotRecorded",
  bankCustomer: null,
  bankCustomerLabel: "Not known",
  conversationId: null,
  triage: null,
}));

async function readMessage(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (body.message) return body.message;
    const first = body.errors ? Object.values(body.errors)[0]?.[0] : undefined;
    return first ?? fallback;
  } catch {
    return fallback;
  }
}

export function useSalesLeads() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [leads, setLeads] = useState<SalesLeadView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [creating, setCreating] = useState(false);

  /** `silent` refreshes in place for the live view: no loading state, and a failed poll keeps what is shown. */
  const load = useCallback(async (silent?: boolean) => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    if (!silent) {
      setLoading(true);
      setError(null);
      setForbidden(false);
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (response.status === 403) {
        if (silent) return;
        setForbidden(true);
        setLeads([]);
        return;
      }
      if (!response.ok) throw new Error("The prospect list could not be loaded.");
      setLeads((await response.json()) as SalesLeadView[]);
    } catch (err) {
      if (silent) return;
      setError(err instanceof Error ? err.message : "The prospect list could not be loaded.");
      setLeads([]);
    } finally {
      if (!silent) setLoading(false);
    }
  }, [role]);

  useEffect(() => { void load(); }, [load]);
  const refresh = useCallback(() => load(true), [load]);

  const createLead = useCallback(async (input: CreateLeadInput): Promise<{ ok: boolean; message?: string }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) return { ok: false, message: "Not available in the visual preview." };
    setCreating(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify(input),
      });
      if (response.status === 403) return { ok: false, message: "You do not have permission to create leads." };
      if (!response.ok) return { ok: false, message: await readMessage(response, "The lead could not be saved.") };
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "The lead could not be saved." };
    } finally {
      setCreating(false);
    }
  }, [role, load]);

  const send = useCallback(async (path: string, init: RequestInit, fallback: string): Promise<LeadResult> => {
    if (LOVABLE_VISUAL_PARITY_MODE) return { ok: false, message: "Not available in the visual preview." };
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads/${path}`, {
        ...init,
        headers: { ...scopedHeaders(role, ""), ...(init.body ? { "Content-Type": "application/json" } : {}) },
        cache: "no-store",
      });
      if (response.status === 403) return { ok: false, status: 403, message: "You do not have permission to do this." };
      if (response.status === 404) return { ok: false, status: 404, message: "This prospect is no longer in this workspace." };
      if (!response.ok) return { ok: false, status: response.status, message: await readMessage(response, fallback) };
      return { ok: true, detail: (await response.json()) as SalesLeadDetail };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : fallback };
    }
  }, [role]);

  const getLeadDetail = useCallback((leadId: string) =>
    send(encodeURIComponent(leadId), { method: "GET" }, "The prospect's details could not be loaded."), [send]);

  const addNote = useCallback(async (leadId: string, text: string) => {
    const result = await send(`${encodeURIComponent(leadId)}/notes`, { method: "POST", body: JSON.stringify({ text }) }, "The note could not be saved.");
    if (result.ok) void load();
    return result;
  }, [send, load]);

  const assign = useCallback(async (leadId: string, userId: string | null) => {
    const result = await send(`${encodeURIComponent(leadId)}/assignee`, { method: "PUT", body: JSON.stringify({ userId }) }, "The owner could not be changed.");
    if (result.ok) void load();
    return result;
  }, [send, load]);

  const refreshInsights = useCallback(async (leadId: string) => {
    const result = await send(`${encodeURIComponent(leadId)}/insights/refresh`, { method: "POST" }, "The insights could not be prepared.");
    if (result.ok) void load();
    return result;
  }, [send, load]);

  /** Scores everyone (or one prospect) and stores the two best-fitting covers. */
  const runTriage = useCallback(async (leadId?: string): Promise<{ ok: boolean; message?: string; scored?: number; skipped?: number }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) return { ok: false, message: "Not available in the visual preview." };
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads/${leadId ? `${encodeURIComponent(leadId)}/` : ""}triage`, {
        method: "POST",
        headers: scopedHeaders(role, ""),
        cache: "no-store",
      });
      if (response.status === 403) return { ok: false, message: "You do not have permission to score prospects." };
      if (!response.ok) return { ok: false, message: await readMessage(response, "The prospects could not be scored.") };
      const result = (await response.json()) as { scored: number; skipped: number };
      await load();
      return { ok: true, scored: result.scored, skipped: result.skipped };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "The prospects could not be scored." };
    }
  }, [role, load]);

  return { leads, loading, error, forbidden, creating, reload: load, refresh, createLead, getLeadDetail, addNote, assign, refreshInsights, runTriage, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}

/**
 * The prospect behind one WhatsApp conversation, for the chat screen's customer panel. `detail` is
 * null when the person is not a prospect yet (they wrote in but have not asked about a cover).
 */
export function useThreadProspect(threadId?: string | null) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [detail, setDetail] = useState<SalesLeadDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!threadId || LOVABLE_VISUAL_PARITY_MODE) {
      setDetail(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/channels/whatsapp/threads/${encodeURIComponent(threadId)}/prospect`, {
        headers: scopedHeaders(role, ""),
        cache: "no-store",
      });
      if (response.status === 404) setDetail(null);
      else if (response.status === 403) {
        setDetail(null);
        setError("Your role cannot see prospect details.");
      } else if (!response.ok) throw new Error("failed");
      else setDetail((await response.json()) as SalesLeadDetail);
    } catch {
      setError("The customer details could not be loaded.");
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }, [role, threadId]);

  useEffect(() => { void load(); }, [load]);

  const call = useCallback(async (path: string, init: RequestInit, fallback: string): Promise<LeadResult> => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads/${path}`, {
        ...init,
        headers: { ...scopedHeaders(role, ""), ...(init.body ? { "Content-Type": "application/json" } : {}) },
        cache: "no-store",
      });
      if (response.status === 403) return { ok: false, status: 403, message: "You do not have permission to do this." };
      if (!response.ok) return { ok: false, status: response.status, message: await readMessage(response, fallback) };
      const next = (await response.json()) as SalesLeadDetail;
      setDetail(next);
      return { ok: true, detail: next };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : fallback };
    }
  }, [role]);

  const addNote = useCallback((leadId: string, text: string) =>
    call(`${encodeURIComponent(leadId)}/notes`, { method: "POST", body: JSON.stringify({ text }) }, "The note could not be saved."), [call]);
  const assign = useCallback((leadId: string, userId: string | null) =>
    call(`${encodeURIComponent(leadId)}/assignee`, { method: "PUT", body: JSON.stringify({ userId }) }, "The owner could not be changed."), [call]);
  const refreshInsights = useCallback((leadId: string) =>
    call(`${encodeURIComponent(leadId)}/insights/refresh`, { method: "POST" }, "The insights could not be prepared."), [call]);

  /** Makes this person a lead by hand, for someone who has written in but not asked about a cover. */
  const capture = useCallback(async (input: CreateLeadInput): Promise<{ ok: boolean; message?: string }> => {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify(input),
      });
      if (response.status === 403) return { ok: false, message: "You do not have permission to create leads." };
      if (!response.ok) return { ok: false, message: await readMessage(response, "The lead could not be saved.") };
      await load();
      return { ok: true };
    } catch {
      return { ok: false, message: "The lead could not be saved." };
    }
  }, [role, load]);

  return { detail, loading, error, reload: load, addNote, assign, refreshInsights, capture };
}


"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { leads as mockLeads } from "@/lib/lovable-parity/mock";

export type SalesLeadView = {
  leadId: string;
  customerName: string;
  leadSource: string;
  productFit: string;
  quoteStatus: string;
  assignedSalesOfficer: string | null;
  createdAt: string | null;
};

export type CreateLeadInput = {
  customerName: string;
  leadSource: string;
  customerNeed: string;
  productFit?: string;
  assignedSalesOfficer?: string;
  contactInfo?: string;
};

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
};

type SalesLeadDto = {
  leadId: string;
  customerName: string;
  leadSource: string;
  productFit: string;
  quoteStatus: string;
  assignedSalesOfficer: string | null;
  createdAt: string | null;
};

const PARITY: SalesLeadView[] = mockLeads.map((l) => ({
  leadId: l.id,
  customerName: l.name,
  leadSource: l.source,
  productFit: l.product,
  quoteStatus: l.status,
  assignedSalesOfficer: l.officer,
  createdAt: null,
}));

export function useSalesLeads() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [leads, setLeads] = useState<SalesLeadView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Sales leads failed.`);
      const rows = (await response.json()) as SalesLeadDto[];
      setLeads(rows.map((r) => ({ ...r })));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sales leads failed to load.");
      setLeads([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  const createLead = useCallback(async (input: CreateLeadInput): Promise<{ ok: boolean; message?: string }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setLeads((prev) => [{
        leadId: `LEAD-${Date.now()}`, customerName: input.customerName, leadSource: input.leadSource,
        productFit: input.productFit || "To be assessed", quoteStatus: "New",
        assignedSalesOfficer: input.assignedSalesOfficer || null, createdAt: new Date().toISOString(),
      }, ...prev]);
      return { ok: true };
    }
    setCreating(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify(input),
      });
      if (response.status === 403) return { ok: false, message: "You do not have permission to create leads." };
      if (response.status === 400) return { ok: false, message: "Please complete the required fields." };
      if (!response.ok) return { ok: false, message: `Create failed.` };
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Create failed." };
    } finally {
      setCreating(false);
    }
  }, [role, load]);

  const getLeadDetail = useCallback(async (leadId: string): Promise<{ ok: true; detail: SalesLeadDetail } | { ok: false; message: string }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      const found = PARITY.find((l) => l.leadId === leadId);
      if (!found) return { ok: false, message: "Lead not found." };
      return {
        ok: true,
        detail: {
          ...found,
          customerNeed: "Sample customer need for the visual-parity fixture.",
          conversationSummary: "Sample conversation summary for the visual-parity fixture.",
          requiredInformation: [], followUpActions: [], conversionProbability: null,
          contactInfo: null, createdBy: null,
        },
      };
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/sales-leads/${encodeURIComponent(leadId)}`, { headers: scopedHeaders(role, "") });
      if (response.status === 404) return { ok: false, message: "Lead not found." };
      if (!response.ok) return { ok: false, message: "Lead details could not be loaded." };
      const dto = await response.json() as {
        leadId: string; customerName: string; leadSource: string; customerNeed: string; conversationSummary: string;
        productFit: string; quoteStatus: string; requiredInformation: string[]; followUpActions: string[];
        assignedSalesOfficer: string | null; conversionProbability: number | null; contactInfo: string | null;
        createdAt: string | null; createdBy: string | null;
      };
      return { ok: true, detail: dto };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Lead details could not be loaded." };
    }
  }, [role]);

  return { leads, loading, error, creating, createLead, getLeadDetail, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}

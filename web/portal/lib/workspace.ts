"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type AnalysisStep = { label: string; detail: string; status: "Done" | "Warning" | string };

export type ColumnMapping = { header: string; field: string; fieldLabel: string; confidence: string };

export type AnalysedProspect = {
  rowNumber: number;
  customerRef: string;
  name: string;
  contactMasked: string;
  consent: string;
  contactable: boolean;
  suggestedProduct: string;
  productLine: string;
  insurer: string;
  score: number;
  band: string;
  confidence: string;
  reasons: string[];
  missing: string[];
  nextAction: string;
  reviewStatus: string;
  templateName: string;
  messagePreview: string;
};

export type ExcludedRow = { rowNumber: number; customerRef: string; reason: string; remediation: string };

export type CountItem = { label: string; count: number };

export type AnalysisSummary = {
  totalRecords: number;
  validContacts: number;
  eligible: number;
  excluded: number;
  needsReview: number;
  productRecommendations: CountItem[];
  scoreDistribution: CountItem[];
  exclusionReasons: CountItem[];
  campaignReadiness: string;
};

export type WorkbookAnalysis = {
  analysisId: string;
  fileName: string;
  analysedAt: string;
  bankName: string;
  mappings: ColumnMapping[];
  ignoredColumns: string[];
  sensitiveColumnsIgnored: string[];
  steps: AnalysisStep[];
  prospects: AnalysedProspect[];
  exclusions: ExcludedRow[];
  summary: AnalysisSummary;
  notices: string[];
  truncated: boolean;
};

export type ChartPoint = { label: string; value: number };

export type ChartSeries = {
  metric: string;
  title: string;
  chartType: string;
  period: string;
  timeZone: string;
  xAxis: string;
  yAxis: string;
  points: ChartPoint[];
  total: number;
  isEmpty: boolean;
  insight: string;
  source: string;
};

export type WorkspaceResult<T> = { ok: true; value: T } | { ok: false; error: string; forbidden?: boolean };

async function readError(response: Response, fallback: string): Promise<WorkspaceResult<never>> {
  if (response.status === 403) {
    return { ok: false, error: "Your role does not allow this action.", forbidden: true };
  }
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors)[0]?.[0] : undefined;
    if (first) return { ok: false, error: first };
    if (payload.message) return { ok: false, error: payload.message };
  } catch {
    // Non-JSON error bodies fall through to the safe message.
  }
  return { ok: false, error: fallback };
}

function unreachable(reason: unknown, fallback: string): WorkspaceResult<never> {
  const network = reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message));
  return { ok: false, error: network ? "The IIP service could not be reached. Check your connection and try again." : fallback };
}

export async function analyseWorkbook(role: string, file: File): Promise<WorkspaceResult<WorkbookAnalysis>> {
  const form = new FormData();
  form.set("file", file);
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/workspace/prospects/analyse`, {
      method: "POST",
      headers: scopedHeaders(role, ""),
      body: form,
    });
    if (!response.ok) return readError(response, "This file could not be analysed.");
    return { ok: true, value: (await response.json()) as WorkbookAnalysis };
  } catch (reason) {
    return unreachable(reason, "This file could not be analysed.");
  }
}

export async function exportAnalysis(role: string, analysis: WorkbookAnalysis): Promise<WorkspaceResult<{ blob: Blob; fileName: string }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/workspace/prospects/export`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(analysis),
    });
    if (!response.ok) return readError(response, "The workbook could not be created.");
    return { ok: true, value: { blob: await response.blob(), fileName: `iip-prospect-analysis-${analysis.analysisId}.xlsx` } };
  } catch (reason) {
    return unreachable(reason, "The workbook could not be created.");
  }
}

export async function getChart(role: string, metric: string, days: number): Promise<WorkspaceResult<ChartSeries>> {
  try {
    const query = new URLSearchParams({ metric, days: String(days) });
    const response = await fetch(`${apiBaseUrl}/api/v1/workspace/charts?${query.toString()}`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) return readError(response, "The chart data could not be loaded.");
    return { ok: true, value: (await response.json()) as ChartSeries };
  } catch (reason) {
    return unreachable(reason, "The chart data could not be loaded.");
  }
}

export type WorkspaceIntent =
  | { kind: "export" }
  | { kind: "chart"; metric: "new_prospects" | "stage" | "product_interest" | "channel"; days: number }
  | { kind: "unsupported"; reply: string }
  | { kind: "help" };

const NUMBER_WORDS: Record<string, number> = { one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, ten: 10, fourteen: 14 };

/**
 * Reads a staff instruction and picks which workspace tool it asks for. This is plain keyword routing,
 * not a language model: it can only reach tools that exist, and anything it cannot place gets an honest
 * list of what the workspace can do. An instruction that asks for a measure the platform does not record
 * (conversion rates, response rates) is declined by name instead of being answered with invented numbers.
 */
export function routeInstruction(text: string, hasAnalysis: boolean): WorkspaceIntent {
  const t = text.toLowerCase();

  if (/\b(export|excel|xlsx|download|spreadsheet)\b/.test(t)) {
    return hasAnalysis ? { kind: "export" } : { kind: "unsupported", reply: "There is no analysis to export yet. Attach a customer workbook first and I will analyse it, then export the result." };
  }

  if (/\b(conversion|converted|response rate|reply rate|delivery rate|read rate)\b/.test(t)) {
    return {
      kind: "unsupported",
      reply: "IIP does not measure that yet, so I will not draw it. I can chart new prospects per day, prospects by stage, by product interest, or by channel.",
    };
  }

  if (/\b(plot|graph|chart|show|how many|compare|trend|breakdown|by stage|by product|by channel)\b/.test(t)) {
    let metric: "new_prospects" | "stage" | "product_interest" | "channel" = "new_prospects";
    if (/\bstage|pipeline|funnel\b/.test(t)) metric = "stage";
    else if (/\b(product|motor|life|cover|insurance type)\b/.test(t) && !/new (prospects|leads)/.test(t)) metric = "product_interest";
    else if (/\bchannel|whatsapp|sms|email\b/.test(t)) metric = "channel";

    let days = 7;
    if (/\btoday\b/.test(t)) days = 1;
    else if (/\bweek\b/.test(t)) days = 7;
    else if (/\bmonth\b/.test(t)) days = 30;
    const digits = t.match(/(\d+)\s*(day|days)/);
    const words = t.match(/\b(one|two|three|four|five|six|seven|ten|fourteen)\s*(day|days)/);
    if (digits) days = Math.min(90, Math.max(1, Number(digits[1])));
    else if (words) days = NUMBER_WORDS[words[1]];
    return { kind: "chart", metric, days };
  }

  return { kind: "help" };
}

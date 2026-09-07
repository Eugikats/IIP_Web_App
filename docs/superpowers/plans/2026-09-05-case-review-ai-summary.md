# Case Review — AI Summary with Risk Signals (Updated Plan)

> **For agentic workers:** Read this plan top-to-bottom before touching any file. Implement one sub-task at a time, run the verification steps, then commit before moving on.

**Goal:** Complete the Case Review feature so a claims officer sees the AI-generated claim summary plus LLM-drafted fraud/underwriting risk signals and the "Get case review" retrieval path — all in one panel on the claim detail page.

**What the teammate already delivered (commit `24b1f74`):**
- `CaseReviewSummary` domain record + `ICaseReviewRepository` interface
- `CaseReviewSignal` (LLM-drafted, Fraud/Underwriting categories)
- `ParseModelOutput` — splits `RISK_SIGNAL|Category|note` lines from the model output
- Prompt extended with RISK_SIGNAL instructions + "do not assert fraud" guardrail, bumped to `v2`
- `fairInputDocuments` filtering and `DescribeDocument` with extracted field values
- `EfCaseReviewRepository` + `PersistedCaseReview` EF entity registered in DbContext + DI
- `InMemoryCaseReviewRepository` in LocalTestDoubles
- `AuditEvent.CaseReviewGenerated`
- `ClaimsSummaryResult` updated with `RiskSignals` + `CaseReviewId`
- `ClaimsSummaryResponse` updated with `RiskSignals` + `CaseReviewId`
- `GET /api/v1/cases/{caseId}/case-review` endpoint (returns latest persisted review)
- Backend tests: persistence, document filtering, GetLatestCaseReview, approval guard

**What still needs to be done (this plan):**
1. EF migration for the `CaseReviews` table
2. Portal type update — `ClaimsSummaryView` missing `riskSignals` and `caseReviewId`
3. Portal UI — render risk signals in the summary card on `ClaimReview.tsx`
4. Portal UI — load the persisted case review on page open (via `GET /case-review`) so the panel is populated without the user having to click "Generate summary" again

**Architecture:** The risk signals come from the same `POST /api/v1/cases/{caseId}/summary` call that generates the summary (the model emits `RISK_SIGNAL|Category|note` lines parsed server-side). A separate `GET /api/v1/cases/{caseId}/case-review` returns the latest persisted review on page load. The portal `ClaimReview.tsx` will auto-load the persisted review on mount, then let the user regenerate.

**Tech Stack:** .NET 8 / EF Core migrations, Next.js 14 / TypeScript / React, Tailwind CSS / Ambient Intelligence design tokens.

## Global Constraints

- AI must not assert confirmed fraud. Approved wording only: "advisory signal", "potential anomaly", "requires investigation".
- All API endpoints use `WorkflowEndpoints.TryResolveScope` — no browser-supplied tenant/workspace overrides.
- `dotnet build InsuranceIntelligence.sln --no-restore` must pass 0 errors, 0 warnings after each sub-task.
- `dotnet test InsuranceIntelligence.sln --no-restore` must pass all tests after each sub-task (currently ≥ 599).
- `pnpm lint` and `pnpm build` in `web/portal` must pass after portal sub-tasks.
- No fabricated data — honest empty/loading/error states only.

---

## Sub-Task 1 — EF Migration for `CaseReviews` table

**Status:** [ ] pending

**Intent:** The teammate registered `PersistedCaseReview` and `DbSet<PersistedCaseReview> CaseReviews` in the DbContext but never generated the migration. Without it, the deployed Azure SQL database has no `CaseReviews` table and every `SaveAsync` call will throw at runtime.

**Expected Outcomes:**
- A new migration file exists under `src/InsuranceIntelligence.Infrastructure/Migrations/` named `AddCaseReviews`.
- The migration creates a `CaseReviews` table with the same columns as all other `PersistedScopedJson` tables: `Id` (uniqueidentifier PK), `TenantId`, `WorkspaceId`, `CaseId` (indexed), `PayloadJson`, `CreatedAt`, `UpdatedAt`.
- `InsuranceIntelligenceDbContextModelSnapshot.cs` is updated by EF to include `CaseReviews`.
- `dotnet build` passes, `dotnet test` stays green.

**Relevant Context:**
- [`src/InsuranceIntelligence.Infrastructure/Persistence/InsuranceIntelligenceDbContext.cs`](src/InsuranceIntelligence.Infrastructure/Persistence/InsuranceIntelligenceDbContext.cs) — `PersistedCaseReview` is already configured via `ConfigureScopedJson` and indexed on `(TenantId, WorkspaceId, CaseId)`.
- The most recent migration is `20260904044200_AddFraudSignalDispositions` — the new one will be dated after that.
- Migration generation command (run from repo root):
  ```
  dotnet ef migrations add AddCaseReviews --project src/InsuranceIntelligence.Infrastructure --startup-project src/InsuranceIntelligence.Api
  ```
- After generating, open the migration file and verify it contains `CreateTable` for `CaseReviews` with the `CaseId` column and its index.
- The snapshot file is auto-updated by EF — do not edit it manually.

**Todo List:**

- [ ] From the repo root, run:
  ```
  dotnet ef migrations add AddCaseReviews --project src/InsuranceIntelligence.Infrastructure --startup-project src/InsuranceIntelligence.Api
  ```
- [ ] Open the generated migration file and confirm it has `migrationBuilder.CreateTable(name: "CaseReviews", ...)` with a `CaseId` column and `CreateIndex` for `(TenantId, WorkspaceId, CaseId)`.
- [ ] Run `dotnet build InsuranceIntelligence.sln --no-restore` → 0 errors.
- [ ] Run `dotnet test InsuranceIntelligence.sln --no-restore` → all tests pass.
- [ ] Commit: `feat(migration): add CaseReviews table migration`.

---

## Sub-Task 2 — Update `ClaimsSummaryView` TypeScript type and `useClaimsSummary` hook

**Status:** [ ] pending

**Intent:** The backend `ClaimsSummaryResponse` now includes `riskSignals` (array of `{ category, note }`) and `caseReviewId`. The portal TypeScript type `ClaimsSummaryView` in [`web/portal/lib/lovable-parity/data/claimsSummary.ts`](web/portal/lib/lovable-parity/data/claimsSummary.ts) still uses the old shape without these fields. This causes a silent data loss — `riskSignals` arrives from the API but is never typed or stored.

**Expected Outcomes:**
- `ClaimsSummaryView` gains `riskSignals: ClaimsSummaryRiskSignal[]` and `caseReviewId: string`.
- New type `ClaimsSummaryRiskSignal = { category: "Fraud" | "Underwriting"; note: string }`.
- `pnpm lint` and `pnpm build` pass with no new errors.

**Relevant Context:**
- [`web/portal/lib/lovable-parity/data/claimsSummary.ts`](web/portal/lib/lovable-parity/data/claimsSummary.ts) — `ClaimsSummaryView` is defined here, add two fields.
- Backend response shape (from [`src/InsuranceIntelligence.Api/Claims/ClaimsSummaryEndpoints.cs`](src/InsuranceIntelligence.Api/Claims/ClaimsSummaryEndpoints.cs)):
  ```
  ClaimsSummaryResponse:
    caseId: Guid
    summary: string
    openQuestions: string[]
    riskSignals: CaseReviewSignalResponse[]   ← NEW
    knowledgeSources: SourceCitationResponse[]
    evidenceRefs: string[]
    confidence: decimal
    agentActionLogId: Guid
    caseReviewId: Guid                         ← NEW
    correlationId: string
  CaseReviewSignalResponse: { category: string, note: string }
  ```

**Changes to `claimsSummary.ts`:**

```typescript
export type ClaimsSummaryRiskSignal = {
  category: "Fraud" | "Underwriting";
  note: string;
};

export type ClaimsSummaryView = {
  caseId: string;
  summary: string;
  openQuestions: string[];
  riskSignals: ClaimsSummaryRiskSignal[];   // NEW
  knowledgeSources: ClaimsSummaryKnowledgeSource[];
  evidenceRefs: string[];
  confidence: number;
  agentActionLogId: string;
  caseReviewId: string;                     // NEW
  correlationId: string;
};
```

No changes to the `useClaimsSummary` hook function body — it deserialises the full response, the new fields arrive automatically.

**Todo List:**

- [ ] In [`web/portal/lib/lovable-parity/data/claimsSummary.ts`](web/portal/lib/lovable-parity/data/claimsSummary.ts), add the `ClaimsSummaryRiskSignal` type and extend `ClaimsSummaryView` with `riskSignals` and `caseReviewId` as shown above.
- [ ] Run `pnpm lint` in `web/portal` → passes (existing warnings only).
- [ ] Run `pnpm build` in `web/portal` → passes.
- [ ] Commit: `feat(portal): add riskSignals + caseReviewId to ClaimsSummaryView type`.

---

## Sub-Task 3 — Add `useCaseReview` hook for loading the persisted review on page open

**Status:** [ ] pending

**Intent:** The existing `useClaimsSummary` hook only triggers on a user button click (it's `generate` not auto-load). We need a separate hook that calls `GET /api/v1/cases/{caseId}/case-review` on page mount so the last-generated review is visible immediately when the officer opens a claim — without requiring them to click "Generate summary" again. This is a thin read-only hook, not a mutation.

**Expected Outcomes:**
- New exported hook `useCaseReview(caseId, role)` in a new file `web/portal/lib/lovable-parity/data/caseReview.ts`.
- Returns `{ review: ClaimsSummaryView | null, loading: boolean, error: string | null, load: () => Promise<void> }`.
- On `404` (no review generated yet), sets `review = null` and `error = null` — this is a valid empty state, not an error.
- On other non-OK status, sets a descriptive `error` string.

**New file: `web/portal/lib/lovable-parity/data/caseReview.ts`**

```typescript
"use client";

import { useCallback, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { ClaimsSummaryView } from "./claimsSummary";

export function useCaseReview(caseId: string, role: string) {
  const [review, setReview] = useState<ClaimsSummaryView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/case-review`, {
        headers: scopedHeaders(role, ""),
      });
      if (res.status === 404) {
        setReview(null); // no review generated yet — valid empty state
        return;
      }
      if (!res.ok) {
        setError(`Case review load failed (HTTP ${res.status}).`);
        return;
      }
      setReview((await res.json()) as ClaimsSummaryView);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Case review load failed.");
    } finally {
      setLoading(false);
    }
  }, [caseId, role]);

  return { review, loading, error, load };
}
```

**Todo List:**

- [ ] Create `web/portal/lib/lovable-parity/data/caseReview.ts` with the hook above.
- [ ] Run `pnpm lint` in `web/portal` → passes.
- [ ] Run `pnpm build` in `web/portal` → passes.
- [ ] Commit: `feat(portal): useCaseReview hook for GET /cases/{id}/case-review`.

---

## Sub-Task 4 — Render risk signals in the summary card and auto-load persisted review

**Status:** [ ] pending

**Intent:** The "Claim summary" card in `ClaimReview.tsx` currently shows the summary text, open questions, and knowledge sources — but does not render `riskSignals`. It also only populates when the user clicks "Generate summary". We need to: (1) auto-load the last persisted review on page mount, (2) show `riskSignals` in the summary card with advisory badges, and (3) keep the "Generate summary / Regenerate" button working as before (which updates the displayed summary in place after a new generation).

**Expected Outcomes:**
- On page open, `useCaseReview` fires and pre-populates the summary card if a previous review exists.
- The "Generate summary" button still works; after it completes, the newly generated summary (including fresh risk signals) replaces what was loaded.
- A `RISK SIGNALS` subsection appears in the summary card when `riskSignals.length > 0`, showing each signal as: `[Category badge] note text`.
  - `Fraud` → amber `tone="warning"` badge labelled "Fraud signal"
  - `Underwriting` → neutral `tone="neutral"` badge labelled "Underwriting"
- Guardrail line under the risk signals section: *"Advisory observations only — a human reviewer decides what, if anything, to do."*
- When `riskSignals` is empty (or no review loaded yet), the section is simply absent.
- `pnpm lint` and `pnpm build` pass.

**Relevant Context:**
- [`web/portal/lovable-pages/ClaimReview.tsx`](web/portal/lovable-pages/ClaimReview.tsx) — the "Claim summary" `GlassCard` starts at ~line 440. The `claimsSummary` state is managed by `useClaimsSummary`.
- [`web/portal/components/iip/Primitives.tsx`](web/portal/components/iip/Primitives.tsx) — `Badge` accepts `tone` prop.
- The two hooks produce the same `ClaimsSummaryView` shape, so the summary card only needs one piece of state for what is displayed. The strategy:
  - `useCaseReview` provides the **initial value** on page load (read from persisted store).
  - `useClaimsSummary` provides the **freshly generated value** when the user clicks the button.
  - We merge them: initialise the `displayedSummary` state from `caseReview.review` on load; update it from `claimsSummary` when `claimsSummary` is populated.

**Changes to `ClaimReview.tsx`:**

1. Add imports:
   ```typescript
   import { useEffect } from "react";
   import { useCaseReview } from "@/lib/lovable-parity/data/caseReview";
   ```

2. After the existing `useClaimsSummary` call, add:
   ```typescript
   const { review: persistedReview, loading: reviewLoading, load: loadReview } = useCaseReview(claimId, roleDef.key);
   useEffect(() => { if (claimId) void loadReview(); }, [claimId, loadReview]);

   // The displayed summary is whichever is most recent: a freshly generated one
   // (claimsSummary) takes precedence over the persisted one loaded on mount.
   const displayedSummary = claimsSummary ?? persistedReview;
   const summaryIsLoading = summarizing || reviewLoading;
   ```

3. Replace all references to `claimsSummary` with `displayedSummary`, and `summarizing` with `summaryIsLoading`, inside the "Claim summary" GlassCard JSX only. The button's `disabled={summarizing}` should remain as-is (only disable during active generation, not during the initial load).

4. Inside the `displayedSummary ?` branch, after the `openQuestions` block and before the `knowledgeSources` block, add the risk signals section:
   ```tsx
   {displayedSummary.riskSignals.length > 0 ? (
     <div>
       <div className="mb-1 text-[11px] uppercase tracking-[0.14em] text-muted">Risk signals</div>
       <ul className="space-y-2">
         {displayedSummary.riskSignals.map((signal, i) => (
           <li key={i} className="flex items-start gap-2 text-[12.5px] text-ink-soft">
             <Badge tone={signal.category === "Fraud" ? "warning" : "neutral"} className="mt-0.5 shrink-0">
               {signal.category === "Fraud" ? "Fraud signal" : "Underwriting"}
             </Badge>
             <span>{signal.note}</span>
           </li>
         ))}
       </ul>
       <div className="mt-2 text-[11px] text-muted">
         Advisory observations only — a human reviewer decides what, if anything, to do.
       </div>
     </div>
   ) : null}
   ```

**Todo List:**

- [ ] Add `import { useEffect } from "react"` to `ClaimReview.tsx` (check if already imported; if `useEffect` is already there from another part of the file, skip this).
- [ ] Add `import { useCaseReview } from "@/lib/lovable-parity/data/caseReview"` to `ClaimReview.tsx`.
- [ ] Inside `ClaimReviewLive`, after the `useClaimsSummary` call, add the `useCaseReview` hook, the `useEffect` auto-load, and the `displayedSummary` / `summaryIsLoading` derived values as shown above.
- [ ] In the "Claim summary" GlassCard JSX, replace `claimsSummary` → `displayedSummary` and `summarizing` (loading spinner in the card body only, not the button) → `summaryIsLoading`.
- [ ] Add the risk signals `<ul>` section inside the `displayedSummary ?` branch, between the `openQuestions` block and the `knowledgeSources` block, exactly as shown above.
- [ ] Run `pnpm lint` in `web/portal` → passes (existing warnings only).
- [ ] Run `pnpm build` in `web/portal` → passes.
- [ ] Commit: `feat(portal): render risk signals + auto-load persisted case review in ClaimReview`.

---

## Verification Checklist (run before raising a PR)

- [ ] `dotnet build InsuranceIntelligence.sln --no-restore -c Release` → 0 errors, 0 warnings.
- [ ] `dotnet test InsuranceIntelligence.sln --no-restore` → all tests pass (≥ 599 + migration-related tests if any).
- [ ] The new migration file exists and contains `CreateTable` for `CaseReviews`.
- [ ] `cd web/portal && pnpm lint` → passes.
- [ ] `cd web/portal && pnpm build` → passes.
- [ ] Manually open a claim in dev mode — the summary card auto-populates if a review was previously generated.
- [ ] Click "Generate summary" — the card updates with fresh content including risk signals (if any).
- [ ] When `riskSignals` is empty, the "Risk signals" section is absent (no empty heading).
- [ ] No panel shows "fraud confirmed", "fraudulent", or "denied" — advisory language only.
- [ ] `GET /api/v1/cases/{caseId}/case-review` on a case with no prior review returns `404` with `{ "message": "No case review has been generated for this case yet." }`.

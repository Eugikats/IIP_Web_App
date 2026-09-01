# IIP Release 1 Responsive Accessibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Execute this plan task-by-task with a test and review gate after each coherent slice.

**Goal:** Make the Release 1 staff claims, evidence-review, shell, and Command Center workflows usable and state-preserving across desktop, tablet, mobile, zoom, keyboard, touch, safe-area, and long-content contexts.

**Architecture:** Keep one semantic source of truth for claim and evidence state, while using CSS breakpoints and small view-state controls to substitute presentation patterns. The desktop queue remains a comparison table; tablet receives a priority table with expandable detail; mobile receives semantic claim cards. The sidebar becomes a true modal drawer only below 768 CSS pixels, and extraction uses one persistent state model with desktop split view and compact source/review panels.

**Tech Stack:** Next.js 15, React 19, TypeScript, Tailwind CSS, Playwright, axe-core.

## Global Constraints

- Preserve server-resolved authority and human decision boundaries.
- English ships first; user-facing strings and layouts must tolerate translation expansion.
- Do not duplicate claim, extraction, correction, draft, or evidence-version state across responsive presentations.
- Primary and repeated controls target at least 44 by 44 CSS pixels; all other targets meet WCAG 2.2 minimums.
- Automated accessibility evidence is not WCAG conformance or physical-device validation.

---

### Task 1: Adaptive Claims Queue

**Files:**
- Modify: `web/portal/lovable-pages/ClaimsQueue.tsx`
- Test: `web/portal/tests/visual/specs/gate-17-responsive-accessibility.visual.spec.ts`

**Interfaces:**
- Consumes: the existing `useClaims()` result and queue filter state.
- Produces: desktop `ClaimsDesktopTable`, tablet `ClaimsTabletTable`, and mobile `ClaimsCardList` views over the same filtered `rows` array.

- [x] Add a failing Playwright assertion that 390 and 320 pixel viewports expose claim cards with claim identity, status, SLA, evidence readiness, owner, freshness, and Review without a page scrollbar.
- [x] Add a failing tablet assertion for priority columns and expandable claim detail.
- [x] Extract reusable claim action controls so responsive views invoke the same assignment and escalation handlers.
- [x] Render cards below 768 pixels, the priority table from 768 through 1279 pixels, and the complete table at 1280 pixels and above.
- [x] Run `pnpm test:visual -- tests/visual/specs/gate-17-responsive-accessibility.visual.spec.ts`.

### Task 2: Adaptive Evidence Workspace

**Files:**
- Modify: `web/portal/lovable-pages/DocumentExtraction.tsx`
- Test: `web/portal/tests/visual/specs/gate-17-responsive-accessibility.visual.spec.ts`

**Interfaces:**
- Consumes: existing selected document, correction values, review reason, preview, and allowed-action state.
- Produces: a desktop source-plus-review split and a compact labelled Source/Review panel switch that never duplicates domain state.

- [x] Add failing assertions for desktop simultaneous source/review visibility and compact labelled panel switching.
- [x] Introduce a compact-panel state whose buttons use tab semantics and move focus into the selected panel.
- [x] Keep both panels mounted so selected document, corrections, review reason, focusable controls, and scroll positions survive reflow.
- [x] Give compact panels viewport-bounded scrolling and safe-area padding.
- [x] Verify no action, warning, or evidence source is obscured at 1024, 768, 390, or 320 pixels.

### Task 3: Modal Mobile Navigation and Compact Shell

**Files:**
- Modify: `web/portal/components/iip/AppShell.tsx`
- Modify: `web/portal/components/iip/Sidebar.tsx`
- Modify: `web/portal/components/iip/TopBar.tsx`
- Modify: `web/portal/components/iip/StatusBar.tsx`
- Modify: `web/portal/lovable-pages/AIConversations.tsx`
- Modify: `web/portal/app/globals.css`
- Test: `web/portal/tests/visual/specs/gate-1-shell.visual.spec.ts`
- Test: `web/portal/tests/visual/specs/gate-17-responsive-accessibility.visual.spec.ts`

**Interfaces:**
- Consumes: `UIStateContext.sidebarOpen`, runtime identity, provider health, and composer draft state.
- Produces: mobile-only dialog semantics, focus containment, Escape close, inert background, focus return, 44-pixel shell controls, dynamic-viewport layout, and safe-area-aware composer/status regions.

- [x] Add failing tests for drawer role/name, `aria-modal`, initial focus, Tab containment, Escape close, background inertness, navigation close, and trigger focus return.
- [x] Track the opener and drawer refs in `AppShell`; apply modal behavior only when `matchMedia('(max-width: 767px)')` matches.
- [x] Pass mobile close/navigation behavior into `Sidebar` and distinguish header versus navigation close labels.
- [x] Reduce top-bar content by priority at narrow widths while retaining workspace, authority, environment/provider, and human-approval cues across the shell.
- [x] Replace fixed viewport assumptions with `100dvh` and safe-area padding; keep the composer above the status region and virtual keyboard.
- [x] Add a global reduced-motion rule and intentional touch behavior without disabling browser zoom.

### Task 4: Accessibility, Long Content, and Reflow Gates

**Files:**
- Modify: `web/portal/components/iip/Primitives.tsx`
- Modify: `web/portal/tests/visual/specs/gate-13-interaction-accessibility.audit.spec.ts`
- Create: `web/portal/tests/visual/specs/gate-17-responsive-accessibility.visual.spec.ts`

**Interfaces:**
- Consumes: critical staff routes and visual API fixtures.
- Produces: enforceable checks for names, focus, target size, no page overflow, 200% text, 320-pixel reflow, reduced motion, and translated-content expansion.

- [x] Make section headings wrap safely and actions reflow without changing semantic order.
- [x] Add automated axe assertions rather than evidence-only logging for critical Batch 5 surfaces.
- [x] Assert primary/repeated/icon controls are at least 44 by 44 CSS pixels.
- [x] Inject representative long translated strings and assert no hidden action or page-level horizontal overflow.
- [x] Run desktop, tablet, 768, 390, and 320 pixel projects plus 200% text/reflow checks.

### Task 5: Verification, Review, and Delivery

**Files:**
- Modify: this plan to mark completed steps only after evidence exists.

**Interfaces:**
- Consumes: Tasks 1-4 and repository merge governance.
- Produces: one reviewable Batch 5 PR with exact verification evidence and explicit manual gates.

- [x] Run `pnpm test`, `pnpm lint`, TypeScript no-emit, protected-profile `pnpm build`, affected visual tests, and deployed-DOM checks.
- [x] Run the applicable backend Release build/tests only if shared contracts changed.
- [x] Review the diff for authority loss, duplicate state, hidden actions, inaccessible names, unsupported conformance claims, and accidental prototype/runtime coupling.
- [x] Commit on `harry/ux-r1-responsive-accessibility`, push, and open a PR to `main`; wait for all three CI jobs before merge.
- [x] Leave maintainer approval, physical-device/assistive-technology verification, protected staging, and practicing-staff usability as explicit external gates.

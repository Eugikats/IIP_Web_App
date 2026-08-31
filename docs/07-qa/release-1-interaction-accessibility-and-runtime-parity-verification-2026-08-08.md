# Release 1 Interaction Accessibility and Runtime Parity Verification — 2026-08-08

| Field | Result |
|---|---|
| Artifact version | `iip_release_1_interaction_accessibility_and_runtime_parity.md` v0.1 |
| Environment | Local Next.js development runtime with deterministic Playwright API fixtures |
| Browser/viewport | Desktop Chromium, 1440 × 1000; mobile Chromium/Pixel 5 profile, 390 × 844 |
| Identity/scope | Development fixture, `tenant-visual-fixture` / `workspace-visual-fixture` |
| Result | Diagnostic executed; material accessibility/parity gaps found |
| Readiness | Source-reconciled and locally diagnosed; not accessibility-conformant, not protected-UAT complete |

## Verification performed

Command:

```text
pnpm.cmd exec playwright test -c tests/visual/playwright.config.ts --project=desktop-chromium tests/visual/specs/gate-13-interaction-accessibility.audit.spec.ts
```

Results: **3/3 desktop diagnostics passed in 1.6 minutes; 3/3 mobile diagnostics passed in 46.1 seconds**. “Passed” means the diagnostic completed and retained structured findings; it does not mean the sampled pages passed WCAG. The audit specification intentionally reports violations without suppressing execution, so remediation can be tracked independently.

Sampled surfaces:

- `/app/work-items` — prepared work and consequential approval entry.
- `/app/claims/CLM-2026-00491` — claim review, upload, governed export dialog.
- `/app/claims/CLM-2026-00491/extraction` — evidence fields, correction, approval.

## Actual observations

| Check | Actual result | Status |
|---|---|---|
| Dialog keyboard entry | Opening Export claim pack left focus on the underlying trigger. | Failed |
| Dialog focus containment | First Tab presses moved through underlying extraction, upload, and Copilot controls before reaching dialog controls. | Failed |
| Escape and focus return | Desktop Escape left focus on the now-hidden Cancel control; mobile returned focus to `body`, not the invoker or changed item. | Failed |
| Keyboard reachability | Sampled shell and task controls were reachable by Tab; route order exposed a long navigation sequence before task content. | Partial |
| Visible focus | Some controls used a strong two-layer white/blue ring; several sidebar links reported no computed focus shadow; the global fallback is a translucent ring. | Partial |
| Target size | All three pages contained many targets below the IIP 44-pixel target. The audit also reported targets below 24 pixels, including compact links/toggles; the 1-pixel hidden file input is acceptable only through its accessible upload trigger. | Failed |
| Names/labels | Desktop Axe reported critical `button-name` violations on all three pages; Claim Review also reported a critical label violation; Extraction reported critical label and select-name violations. Mobile retained critical Claim Review/Extraction label and select-name violations; some desktop-only unnamed shell buttons were not rendered in the mobile profile. | Failed |
| Status/progress semantics | Pages had a shared status/live region, but no progressbar was present; upload/action-specific error and outcome semantics remain inconsistent. | Partial |
| Contrast | Axe reported serious color-contrast violations on every sampled page. Token calculation also found `muted-soft`/white at 2.56:1 and raw danger/danger-soft at 4.41:1. | Failed |
| Reduced motion | `.hover-lift` computed with no transform, but still retained transitions; the governed dialog retained a 0.2-second transition under reduced motion. | Partial |
| Automated rules | Work items: critical button-name plus serious contrast. Claim Review: serious aria-hidden-focus, contrast, scrollable-region-focusable and critical button-name/label. Extraction: critical button-name/label/select-name plus serious contrast. | Failed |

## Client/runtime parity findings

| Contract | Evidence | Status |
|---|---|---|
| Tenant/workspace/permission | Client resolves `/api/v1/me`; server remains authority. Protected reauth, revocation, and focus-resume evidence not run. | Partial |
| API action/state | Current routes exist for case, assignment, documents, approvals, updates, WhatsApp, conversations, and export. Some clients flatten safe conflicts/provider ambiguity into generic HTTP text. | Partial |
| Provider boundary | `Not configured` is represented; WhatsApp provider truth and reconciliation exist server-side. No live provider used in this local fixture run. | Partial |
| Concurrency | Conversation lifecycle sends `expectedVersion` and maps 409. Queue, evidence, approval, update, and export clients do not expose a comparable version/reapply contract. | Partial |
| Audit | Current action names and audit-reason seams were source-reconciled. This run did not correlate UI → API → durable audit event in a protected runtime. | Partial |
| Idempotency | WhatsApp server requires an identity; Channels creates a UUID per invocation. It does not retain the original identity for an ambiguous retry. Upload/export durable identity and reconciliation are unproved. | Partial |

## Contrast calculations

| Pair | Ratio | Interpretation |
|---|---:|---|
| Ink / white | 18.33:1 | Pass normal text |
| Muted / white | 4.76:1 | Pass normal text |
| Muted-soft / white | 2.56:1 | Fail normal/essential text; decorative or disabled only |
| White / primary | 4.88:1 | Pass normal text |
| White / AI | 4.81:1 | Pass normal text |
| Success text / success soft | 7.29:1 | Pass |
| Warning text / warning soft | 6.84:1 | Pass |
| Danger text / danger soft | 7.60:1 | Pass |
| Raw danger / danger soft | 4.41:1 | Fail normal small text |
| Focus blue / white | 4.88:1 | Pass focus-component contrast when the solid token is used |

## Gaps and disposition

The owning gap register is in `iip_release_1_interaction_accessibility_and_runtime_parity.md`: `A11Y-GAP-01`–`08` and `PAR-GAP-01`–`03`. No runtime route, enum, schema, permission, audit action, or provider policy was changed in this documentation/verification pass.

Required next verification after remediation:

1. repeat desktop, tablet, and mobile Chromium diagnostics;
2. assert modal forward/reverse containment and invoker/changed-item fallback return;
3. assert zero critical/serious automated violations on critical states;
4. test 200%/400% zoom, 320 CSS-pixel reflow, forced colors, and reduced motion;
5. conduct NVDA + Chrome and another supported screen-reader/browser pass;
6. run authenticated allowed/denied, revoked permission, session expiry, conflict, `Not configured`, unknown provider outcome, retry/reconciliation, and correlated audit journeys in protected staging.

## Exclusions

No manual screen-reader session, physical touch-device test, forced-colors run, tablet browser run, 200%/400% zoom run, live provider call, authenticated protected identity, or practitioner/usability validation occurred. The mobile result is browser emulation, not physical-device evidence. These checks remain pending and must not be inferred from the local diagnostic.

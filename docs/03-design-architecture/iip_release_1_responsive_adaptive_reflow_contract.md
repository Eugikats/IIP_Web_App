# IIP Release 1 Responsive and Adaptive Reflow Contract

| Field | Value |
|---|---|
| Version | 0.1 |
| Date | 2026-08-10 |
| Status | Source-complete design contract; interactive wide/intermediate/compact prototype rendered; browser, device and practitioner verification pending |
| Screen authority | `iip_information_architecture_navigation_and_screen_inventory.md` |
| State/wireframe authority | `iip_release_1_low_fidelity_wireframe_state_inventory.md` |
| Complex-pattern authority | `iip_release_1_complex_operational_pattern_contracts.md` |
| Accessibility authority | `iip_release_1_interaction_accessibility_and_runtime_parity.md` |
| Scope | Desktop, tablet and mobile reflow for all canonical screens, with P0 Release 1 families defined first |

## 1. Principle and boundary

Responsive behavior preserves task priority, evidence meaning, authority, state and recovery across available space, input and accessibility settings. It is not “stack every desktop column.” This document does not create device-specific business rules, weaker mobile permissions, new routes, hidden consequential actions or implementation-complete claims.

Layout selection uses available CSS inline size and intrinsic component needs. User-agent/device labels never select authority or data. At 200%/400% zoom, text scaling, split-screen or desktop window resizing, the interface uses the same reflow rules as an equivalent narrow viewport.

## 2. Context matrix and breakpoint rationale

| Context ID | Reference inline size | Typical contexts | Density/task posture | Navigation | Primary composition |
|---|---:|---|---|---|---|
| `RSP-R1-COMPACT` | 320–599 CSS px | Mobile portrait/landscape, narrow split window, 400% desktop zoom | One decision region at a time; touch or keyboard; intermittent/low bandwidth likely but not assumed | Top app bar + modal navigation drawer; route/workspace always named | One column; sequential evidence/action steps; sticky action only when it cannot cover content/status |
| `RSP-R1-INTERMEDIATE` | 600–1023 CSS px | Tablet portrait/landscape, small laptop, 200% zoom, half-window | Moderate density; touch/pointer/keyboard may coexist | Collapsible rail or drawer; current destination remains visible | One primary column plus optional contextual side sheet; two columns only when both meet intrinsic minimums |
| `RSP-R1-WIDE` | ≥1024 CSS px | Desktop/laptop, large tablet landscape with pointer | Dense scanning and side-by-side comparison | Persistent sidebar where space permits; stable workspace/global status | Two/three regions only where evidence and action remain readable; bounded line length |

Reference thresholds are test anchors, not device truth. A component changes mode when its intrinsic minimum can no longer fit without clipping, unreadable lines, two-dimensional scrolling or loss of the next safe action. Large desktop width expands comparison/data capacity, not prose line length or the number of simultaneous consequential decisions.

Minimum supported content viewport is 320 CSS px. Device chrome, safe areas and the virtual keyboard reduce usable space and must not cover the focused control, error summary, status or required action.

## 3. Global layout rules

| Region | Min/max and intrinsic behavior | Desktop/wide | Tablet/intermediate | Mobile/compact | Order, overflow, visibility and state preservation |
|---|---|---|---|---|---|
| Shell/workspace | Content area min 320px; workspace name and global state never truncate to ambiguity | Persistent sidebar + top context | Collapsed rail when primary content still meets min; otherwise drawer | Top bar + modal drawer | DOM/focus order: skip link, workspace/status, nav, page. Drawer opening moves/contains/returns focus; route and workspace persist across mode change |
| Page header | Title/purpose/state must fit before secondary controls | Title/context left; safe primary action right | Actions wrap below title | Title, state and actions stack; purpose before action | No title/action is hidden. Long names wrap. Action order remains semantic and risk-aware |
| Breadcrumb/context | Preserve case/customer/work ID and return destination | Inline/breadcrumb | Wrap or shorten labels without dropping IDs | Back + current object label; full path in accessible text/menu | Never rely on ellipsis alone; return location/filter/scroll preserved |
| Global status | Session, outage, environment and recovery state | Banner/status region across content | Same | Same; may become compact summary with details control | Never covered by sticky UI. Announced once. State persists across resize/orientation |
| Primary content | Intrinsic min determined by task | Multi-region where useful | Single primary + side sheet or two balanced regions | Single sequential region | Visual order matches DOM/reading/focus order. No CSS reorder that changes meaning |
| Secondary/context | Evidence, metadata, help or audit context | Inline side region | Side sheet or collapsible section | Dedicated in-page section/route-like panel | “Hidden” means user-collapsed with visible state, never absent. Open/selection state persists |
| Action/status | Exact prerequisites, authority and result | Inline/sticky within owning region | Bottom/side region if content remains visible | Last in DOM; optional safe-area-aware sticky summary | Consequential controls never float without evidence/currentness. Sticky area cannot obscure focused content or errors |

## 4. Navigation transformations

| Behavior | Desktop | Tablet | Mobile |
|---|---|---|---|
| Primary navigation | Persistent labeled sidebar | Labeled rail if content fits; otherwise menu button + drawer | Menu button + modal drawer |
| Current destination | Persistent selected label and page heading | Selected icon/label plus page heading | Page heading and selected drawer item |
| Workspace switch | Top/shell control with current workspace | Same control; dialog/sheet may replace menu | Full-width dialog/sheet; current workspace and consequence explicit |
| Recent work/conversations | Sidebar secondary region | Collapsible rail region or drawer section | Separate list screen/drawer section; return restores conversation/task |
| Deep link | Resolve authority, then screen | Same | Same; drawer starts closed and content heading receives focus |
| Permission change | Remove inaccessible nav; show route-safe explanation if current screen lost | Same | Same; drawer and back path cannot reveal removed destination metadata |
| Alias route | Immediate canonical redirect status | Same | Same; no independent mobile destination |

Navigation content does not change because hover is unavailable. Tooltips supplement visible labels/accessible names; they never contain essential purpose, authority or status.

## 5. Layout-family transformations

| Family and screens | Desktop/wide | Tablet/intermediate | Mobile/compact | State/meaning that must persist |
|---|---|---|---|---|
| Dashboard/conversation `001`, `021`, `034` | Orientation/answer main region; recent work/context alongside where useful | Main region first; recent/context in side sheet or section | Status/purpose → answer/task → sources → next action → recent work | Query/conversation ID, run state, selected source, current-vs-historical warning, recovery owner |
| Queue/list/search `002`, `003`, `008`, `010–013`, `017`, `021–023`, `025`, `028`, `032`, `033` | Filter bar + sortable table; detail preview only if it does not narrow key columns | Filters in wrapping bar/sheet; table becomes priority columns or row cards | Filter summary + full-screen filter sheet; semantic cards/definition list; one row action menu | Query/filter/sort/page/selection, source type, count meaning, stale/restricted rows, return position |
| Record/detail `005`, `009`, `014–016`, `019`, `020` | Header + primary record + contextual rail; tabs only for stable peer sections | Header + primary; contextual rail becomes sheet/accordion | Summary/next action → facts → evidence/context → timeline/related → actions | Object/version/status, section selection, expanded items, source links, safe next action, stale/conflict warning |
| Form/configuration `004`, `018`, `024`, `026`, `027` | Logical field groups; optional summary/validation rail | One/two groups only when labels/errors fit; summary collapses inline | One column; long forms use semantic sections and progress, not hidden steps | Draft values, validation, current step, duplicate/conflict result, save state; virtual keyboard never hides focused error/action |
| Evidence/review `006` | Original preview and extracted fields side by side; dependencies/status rail | Preview and fields split only above intrinsic min; otherwise segmented sequential view | Source summary → `View source` section → field/value/reason → dependencies → commit action | Selected document/page/source coordinate, field focus, edited value/reason, version/confidence, conflict/invalidation state |
| Channel/communication `007`, `011` | Thread + case/preflight panel; composer within thread | Thread primary; case/preflight in side sheet or sequential panel | Thread metadata first, messages, delivery/status, composer, preflight as explicit full-width step | Thread/case link, message draft, recipient, provider/consent state, delivery ambiguity, scroll anchor and reconciliation attempt |
| Audit/export `022` | Filter/table + export/status panel | Reduced priority columns; row detail sheet; export panel below/aside | Filter summary, event cards, event detail, export scope/status | Filter/time/scope, event identity/correlation, export selection/version, partial/failed items, focus/return position |
| Provider/operations `023`, `025` | Capability/health table + selected detail/attempts | Cards/table plus detail sheet | Capability cards → exact status → attempts/owner/recovery | Configured vs unavailable vs failed, selected provider/agent, attempt/correlation, stopped/recovery state |
| Compatibility redirect `029–031` | Brief redirect status to canonical target | Same | Same | Original deep-link intent and canonical destination; no alias-owned UI state |

## 6. Complex component transformations

### Tables and dense lists

- Keep a semantic table only while required columns meet their intrinsic minimum and headers/cells remain associated.
- Tablet hides only secondary columns behind row detail; visible columns must retain identity, state, owner/next action and risk/currentness.
- Mobile substitutes a semantic card/definition-list pattern. It is not a horizontally clipped desktop table.
- Genuine two-dimensional comparisons may scroll inside a labeled region with sticky row/column context and an equivalent detail view; the whole page must not require horizontal scrolling at 320px.
- Selection count/scope, pagination, sort, filters and bulk scope persist. Consequential bulk decisions remain prohibited.

### Evidence comparison

- Desktop uses side-by-side source and derived value when both meet readable minimums.
- Tablet may use resizable split or source side sheet. If either pane becomes too narrow, switch to sequential mode.
- Mobile uses one logical sequence with a persistent source/value relationship: field label, current/derived value, source link/thumbnail, limitation/confidence, edit/reason and status.
- Spatial source coordinates receive a textual alternative. Moving between source and field restores the previous position/focus.

### Approval cockpit

- Desktop may show proposal, evidence/limits and decision region together.
- Tablet shows proposal/evidence first; decision region follows or opens as a governed sheet after prerequisites are visible.
- Mobile uses a review sequence: exact action/consequence → evidence and exclusions → authority/expiry → reason → distinct decision actions. The decision area is never the first viewport and no outcome is preselected.
- Resize cannot clear the review reason, collapse unresolved warnings or make a stale/unauthorized action available.

### Dialogs, drawers and sheets

- Desktop uses modal dialog for bounded confirmation; nonmodal side panel for supporting context.
- Tablet/mobile may substitute a full-height sheet/dialog when content would clip, preserving `dialog` semantics, heading, focus containment, Escape/cancel and focus return.
- A modal never becomes a nonmodal panel during an active consequential confirmation. Finish/cancel it or retain modal semantics across resize.
- Destructive/consequential confirm and cancel remain simultaneously reachable without scrolling past hidden prerequisites.

### Forms and virtual keyboard

- Labels remain above or consistently adjacent only when space supports reading order; errors follow and are programmatically associated.
- Input, unit, help and error may wrap; no fixed-height field container clips text.
- Mobile uses input purpose/type hints without changing validation authority. The focused field scrolls above the virtual keyboard and any sticky action.
- Autosave/save state, unsaved changes, duplicate submit suppression and session-expiry recovery remain visible after orientation or width change.

### Status regions, cues and charts

- Status text and icon/pattern persist when color, hover, animation or chart detail is unavailable.
- Charts provide a table/summary alternative. Mobile reduces simultaneous series/labels, not the underlying metric meaning, freshness, source or limitation.
- Loading, partial, permission, provider error, success and recovery stay region-local; reflow never converts a partial screen into apparent full success.

## 7. Input and capability adaptations

| Capability | Required behavior |
|---|---|
| Keyboard | Logical DOM/focus order follows visible order; skip links; no trap except governed modal; table/card actions and drawers fully operable; focus remains visible and unobscured. |
| Touch | Primary, consequential, destructive, icon-only and repeated targets ≥44×44 CSS px; spacing prevents adjacent activation; drag has button/keyboard alternative. |
| Pointer/hover | Hover may reveal redundant preview only; focus/pressed/selected states provide equivalent information; no hover-only action. |
| Mixed input | Do not lock a mode after first input. Focus styling remains for keyboard use on touch devices; target sizing remains touch-safe on pointer-capable tablets. |
| Screen reader | Landmarks/headings/state relationships remain stable across substitution; table-to-card mode preserves labels and reading sequence; dynamic status announced once. |
| Reduced motion | Drawer/sheet/reflow changes are immediate or use nonessential motion removed; state change remains textual/semantic. |
| Forced colors/contrast | Borders, focus, status and selection use system colors/non-color cues; hidden desktop columns/cards do not depend on color legend. |
| Low bandwidth/offline | Load identity/metadata/status before heavy preview; preserve draft and explicit sync/unknown state; never claim offline provider/domain success. |
| Localization/RTL | Components grow/reflow from content; no fixed label widths; icons with directional meaning mirror where appropriate; IDs/numbers retain correct bidi isolation. |
| Safe areas/orientation | Header, drawer, composer and sticky action respect insets; orientation change preserves task state and focus target, not raw pixel position. |

## 8. State preservation across reflow

Changing viewport, orientation, zoom, text size, input or device posture must not itself:

- refetch and replace authoritative data without normal freshness policy;
- replay a submit/provider attempt;
- clear a safe draft, correction reason, approval reason, filter, sort, selection or scroll anchor;
- change tenant/workspace, role, object or action authority;
- convert `PARTIAL`, `STALE`, `CONFLICT`, `UNAUTHORIZED`, `NOT-CONFIGURED`, `UNAVAILABLE`, `TIMEOUT`, `INTERRUPTED` or `RECOVERY` into success;
- move focus to the document start unless the previously focused object no longer exists and the documented fallback applies;
- silently close an error, warning, evidence limitation, dialog or recovery status.

When a region changes representation—table row to card, rail to sheet, split preview to sequential—the same logical object, selection, expanded state and accessible name remain. If preservation is impossible, show the reason and return focus to the closest owning heading/control.

## 9. Per-state reflow rules

| State | Desktop | Tablet | Mobile |
|---|---|---|---|
| Loading | Skeleton each real region; surrounding context remains | Same; side regions may become labeled placeholders below | One skeleton sequence; no large blank viewport or fake card count |
| Empty | Context/filter + valid next action within owning region | Same | Concise explanation and full-width valid action; no illustration that pushes action below viewport |
| Error | Region error plus safe detail/recovery panel | Error inline; details in sheet/expander | Error summary before affected region; preserved input and recovery action adjacent |
| Permission | Whole-screen safe denial or region redaction based on current contract | Same | Same; no drawer/back-path metadata leak |
| Partial | Successful regions remain alongside missing-region labels | Successful first; missing/recovery follows in semantic order | Summary of available/missing scope first; safe content then region-local recovery |
| Success | Confirmed status near action/result and successor link | Same | Status before successor; sticky action removed/updated so it cannot invite duplicate submit |
| Recovery | Attempt/owner/status can remain beside context | Recovery panel/sheet stays linked to object | Recovery status near affected object; progress does not rely on motion; cancel boundary explicit |

## 10. Responsive test scenarios

| ID | Scenario | Expected result |
|---|---|---|
| `RSP-TST-01` | Traverse 1280 → 1024 → 768 → 600 → 599 → 390 → 320 CSS px on each P0 screen and seven states | No clipped/lost content/action/status; transformation occurs at intrinsic limit; state and focus preserved |
| `RSP-TST-02` | Desktop at 200% and 400% zoom | Same intermediate/compact composition as equivalent CSS width; no page-level horizontal scroll except documented two-dimensional region |
| `RSP-TST-03` | Tablet portrait/landscape during open detail sheet, draft and partial provider state | Same object/draft/state; no duplicate request; focus remains logical and visible |
| `RSP-TST-04` | Mobile virtual keyboard during FNOL, correction reason, approval reason and WhatsApp draft | Focused field/error/actions are not obscured; safe draft persists; viewport does not jump to unrelated region |
| `RSP-TST-05` | My Work, Claims, Search, Audit and Provider data at zero, one, long, localized and high-volume records | Table/card substitution preserves labels, sort/filter/count meaning, identity, next action and return position |
| `RSP-TST-06` | Evidence review with long document name, unsupported preview, low-confidence fields, conflict and correction | Source/value relationship and limitations remain perceivable; edit/reason/conflict recovery stays operable at 320px |
| `RSP-TST-07` | Approval cockpit with stale evidence, long reason, permission revocation and modal confirmation while resizing | Warnings remain; reason preserved; decision cannot become enabled; modal semantics/focus remain correct |
| `RSP-TST-08` | WhatsApp unknown delivery while navigating/resizing/offline/online | Unknown remains explicit; draft/attempt identity persists; reconciliation precedes retry; no false sent/delivered state |
| `RSP-TST-09` | Keyboard-only and touch-only paths at each band; no-hover pointer emulation | Equivalent actions/status, visible focus, touch-safe targets and no hover dependency |
| `RSP-TST-10` | English long text, approved localized sample, RTL stress, Windows forced colors and reduced motion | No clipped controls/labels; reading order and IDs remain correct; state and focus perceivable without color/motion |
| `RSP-TST-11` | Session expiry during form/review, then reauthenticate at another width | Committed work and safe draft retained; current authority/evidence compared; intentional resume only |
| `RSP-TST-12` | Screen-reader traversal before and after table-to-card or split-to-sequential change | Same logical labels/relationships/order; status announced once; focus returns to equivalent object |

## 11. Traceability and gaps

| ID | Gap | Required treatment |
|---|---|---|
| `RSP-GAP-01` | An interactive prototype renders the three context bands, but each P0 screen/state has not been reviewed at exact browser/device test anchors. | Review the prototype at wide/intermediate/compact modes, then verify every P0 layout family and seven-state variant at exact anchors before visual polish. |
| `RSP-GAP-02` | Current portal breakpoint/component parity has not been reconciled comprehensively. | Inspect computed layout and behavior at intermediate widths; record component/API/state mismatches. |
| `RSP-GAP-03` | Physical tablet/mobile, virtual keyboard, safe-area, orientation and mixed-input evidence is absent. | Test supported physical devices, not emulation alone. |
| `RSP-GAP-04` | Table-to-card, evidence split/sequential, approval sequence and drawer/panel substitutions lack practitioner comprehension evidence. | Scenario-test with each affected role; measure errors, omissions, time and recovery. |
| `RSP-GAP-05` | Localization/RTL production scope and representative strings remain conditional. | Use stress strings now; validate approved locales when target-insurer policy exists. |
| `RSP-GAP-06` | Supported browser/device matrix and performance budgets for low-bandwidth responsive use are not approved. | Product/Architecture/Delivery approve matrix and measured budgets before protected UAT. |

Source completeness means the reflow behavior, state preservation and test targets are explicit. It is not proof of rendered layout quality, WCAG conformance, browser/device support, usability or protected-runtime readiness.

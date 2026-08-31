# IIP Visual Direction Review

**Scope:** design-only review of the three generated concept boards in `iip_visual_direction_exploration.md`.
**Standards baseline:** WCAG 2.2 AA design intent; no conformance claim without implementation and manual assistive-technology checks.

## Assumed user and task

Daily expert staff member, usually desktop-first, working under interruption and time pressure. They must move from queue to evidence review to a clearly human-owned approval outcome while retaining source, freshness, owner, priority and recovery context.

## Scorecard

| Direction | Clarity | Evidence | Human/AI | Comfort | Identity | Responsive | A11y | Coherence | Total / 40 | Result |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Casefile Ledger | 4 | 5 | 5 | 4 | 5 | 3 | 4 | 4 | 34 | viable, but document-first |
| Assurance Console | 5 | 5 | 5 | 4 | 4 | 4 | 4 | 5 | 36 | **recommended** |
| Operational Intelligence | 5 | 4 | 5 | 5 | 3 | 5 | 5 | 5 | 37 | viable, safest implementation path |

## Critique findings

| # | Severity | Direction | Evidence / principle | Required fix before prototype |
|---:|---|---|---|---|
| 1 | Major | Assurance Console | A permanent stage spine can imply that every claim follows one linear lifecycle, weakening the conceptual model. | Show it only when an authorised workflow owns the current task; otherwise use a case timeline labelled with actual events. |
| 2 | Major | Casefile Ledger | Editorial display typography risks slowing dense case/amount scanning. | Restrict display type to page and case titles; tables, controls and metadata use a compact sans with tabular numerals. |
| 3 | Major | Operational Intelligence | Dashboard counters could compete with the work queue, violating task-first hierarchy. | Keep metrics secondary and permit only actionable counters tied to a queue filter. |
| 4 | High | All | Colour remains prominent for priority, confidence and approval state. | Pair every state with plain label, icon/pattern and programmatic name; validate contrast once values are selected. |
| 5 | High | All | Desktop evidence rails will not fit narrow viewports without losing task context. | Specify a single active rail on tablet and labelled, focus-managed drawers on mobile; preserve unsaved work. |
| 6 | Medium | All | Generated boards do not prove focus order, live status messages, reading order, or screen-reader labels. | Implement semantic landmarks, headings, table headers, names/roles/values and tested status announcements before calling the prototype accessible. |

## Recommendation

Use **Assurance Console** as the direction to develop first: its shell best expresses the product’s central value—an accountable human operating stage between fragmented evidence and a decision. Adopt the following safeguards from the other directions: Casefile Ledger’s persistent source lineage and Operational Intelligence’s light, calm data density.

This is a recommendation, not an approved direction. The user/stakeholder must select a direction before token migration or a portal prototype is implemented.

## Selected-direction implementation gate

After approval, create the development-only prototype using synthetic data only. It must include the shell, My Work queue, evidence workspace, human approval preparation and Command Center. Validate lint, build, screenshots at desktop/mobile, axe-core, keyboard/focus, 200% text and 400%/320px reflow. Do not change production routes, APIs, tenant authority or workflow behaviour.

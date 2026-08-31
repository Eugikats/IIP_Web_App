# Conversation continuity UX research for insurance operations

**Date:** 2026-08-03
**Scope:** Generic desk research for a Release 1 staff experience. This note does not evaluate an existing interface and does not use repository assumptions as evidence.
**Source standard:** Primary law, regulator material, standards, official product guidance, and empirical research papers.

## Executive decision

Release 1 should treat a conversation as a **claim-linked work session**, not as an isolated chat transcript. The primary object staff recognize and reopen should be the claim or task, with the conversation and evidence restored in context.

The recommended Release 1 pattern is:

1. An active-work list grouped by operational state, with search and filters.
2. Recognition-rich rows showing claim reference, insured/claimant, workflow stage, last meaningful action, owner, updated time, and pending/unread state.
3. Reopening restores the exact claim, conversation position, evidence selection, draft, and pending action.
4. Evidence is explicitly marked `Current`, `Superseded`, or `Historical snapshot`, with source and dates.
5. `Archive`, `Restore`, `Delete request`, retention, and final disposal are separate concepts.
6. Work autosaves, survives re-authentication and disconnection, and can resume over a lightweight, text-first interface.

These are design recommendations, not proof that Ugandan insurance staff will succeed with them. Direct task testing is still required.

## 1. Which cues help staff recognize prior work?

### Findings

People re-find information through both search and contextual reminders. In an observational study, descriptions participants had written three to six months earlier supported a 95% or better return-to-site success rate; the authors emphasized the value of a reminder plus context of relevance. A later design-probe study found that concept-based expression complemented conventional search and browsing when people struggled to state exactly what they wanted to re-find. ([Bruce, Jones and Dumais, 2004](https://www.microsoft.com/en-us/research/?p=274701); [Park et al., 2023](https://www.microsoft.com/en-us/research/publication/foundwright-a-system-to-help-people-re-find-pages-from-their-web-history/))

Chronology alone is weak for older material. An empirical email re-finding study found time-based ranking degrades as messages age and recommended hybrid approaches. Repeat-search research also found that apparent consistency aids reuse: when a familiar result list appeared changed, participants were less successful, less satisfied, and slower. ([Mackenzie et al., 2019](https://www.microsoft.com/en-us/research/publication/exploring-user-behavior-in-email-re-finding-taskshttps-www2019-thewebconf-org/); [Teevan, 2008](https://www.microsoft.com/en-us/research/publication/how-people-recall-recognize-and-reuse-search-results/))

### Release 1 recommendation

Each conversation row should expose stable, domain-relevant cues:

- Claim reference
- Insured and claimant, kept distinct where both exist
- Claim type
- Workflow stage
- Last meaningful action, not merely the last message fragment
- Current owner or queue
- Last updated time
- Pending action, unread update, or missing-document marker
- A user-editable short label where useful

Keep the row layout and labels stable. Provide `Pin` or `Save for later` for deliberate reminders. Do not make an AI-generated title the only recognition cue; it may be inaccurate or use vocabulary the staff member would not recall.

**Confidence:** Medium. The re-finding evidence is empirical but comes from web and email tasks, not insurance claims.

## 2. Can users find and reopen a target conversation?

### Findings

No current evidence can answer this as a usability result because there is no interface and no task test. Research does show that people mix browse and search strategies and that retrieval behavior varies with context. A 16-person diary study found that structured work accounts supported folder-based retrieval while less-structured personal accounts relied more on search. ([Cecchinato et al., 2016](https://www.microsoft.com/en-us/research/?p=238093))

A larger field study instrumented 345 long-term email users and observed more than 85,000 re-finding actions. Complex foldering did not improve retrieval success and was inefficient; search and threading supported more effective re-finding. This is stronger evidence against making a deep manual folder hierarchy the primary retrieval model. ([Whittaker et al., CHI 2011](https://research.ibm.com/publications/am-i-wasting-my-time-organizing-email-a-study-of-email-refinding))

Current first-party collaboration tools expose complementary retrieval cues rather than a single navigation method. Microsoft Teams supports keyword/person search plus filters for location, sender, date, mentions, links, and attachments, then opens the matched message in context. Slack similarly supports conversation, sender, saved/pinned, phrase, and result-type filters. These product patterns are not proof of usability for insurance operations, but they reinforce the search-plus-browse recommendation. ([Microsoft Teams search guidance](https://support.microsoft.com/en-US/teams/chat/search-for-messages-and-more-in-microsoft-teams); [Slack search guidance](https://slack.com/help/articles/202528808-Search-in-Slack))

W3C guidance requires consistent identification of repeated functions, which supports using the same accessible names for `Open`, `Resume`, `Archive`, and `Restore` wherever those actions appear. ([WCAG 2.2, Consistent Identification](https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification))

### Release 1 recommendation

Support both browsing and search:

- Default views: `Assigned to me`, `Needs action`, `Waiting`, `Recently viewed`, and `Archived`
- Search: claim reference, policy reference, insured, claimant, broker, conversation text, and document name
- Filters: owner, claim type, workflow stage, update date, pending action, and evidence status
- Hybrid ranking: exact identifiers first, then text relevance and operational urgency, with recency as one signal rather than the sole ordering rule
- Reopen target: the work context, not the transcript top

When reopened, restore the last meaningful state: selected claim, conversation position, evidence item, unfinished draft, and pending action. Show a brief `Since you last worked here` summary, but link every summary item to the underlying event.

### Validation criterion

In a formative test, give staff only realistic memory cues, for example: “Reopen the motor claim you reviewed last week where the police report was missing.” Measure first-attempt success, time, query reformulation, wrong opens, and whether participants understand where work will resume.

**Confidence:** Medium for the proposed search-and-browse model; low for target performance until tested.

## 3. Do users understand historical versus current evidence?

### Findings

Uganda's Data Protection and Privacy Act requires personal data to be complete, accurate, up to date, and not misleading, and provides correction or deletion rights for inaccurate, irrelevant, excessive, out-of-date, incomplete, misleading, unlawfully obtained, or no-longer-authorized data. It also requires notification when a correction is made in relevant circumstances. ([Data Protection and Privacy Act 2019, ss. 15–16 and 28](https://ulii.org/en/akn/ug/act/2019/9/eng%402019-05-03))

W3C PROV distinguishes generation and invalidation time, revisions, derivation, and primary sources. This provides a standards-based model for representing an evidence object's history without presenting every version as equally current. ([W3C PROV-O](https://www.w3.org/TR/prov-o/))

For AI-supported work, a preregistered experiment with 308 participants found that explanations increased reliance on correct and incorrect responses, while sources and visible inconsistencies reduced reliance on incorrect responses. Evidence presentation should therefore help verification and expose contradictions, not merely make an AI answer more persuasive. ([Kim et al., 2025](https://www.microsoft.com/en-us/research/publication/fostering-appropriate-reliance-on-large-language-models-the-role-of-explanations-sources-and-inconsistencies/))

### Release 1 recommendation

Split the evidence rail into:

- `Current evidence` — the version presently valid for review
- `Earlier versions` — superseded evidence retained for traceability
- `Historical snapshots` — previously valid state captured at a decision or handoff

Every evidence item should show:

- Status: `Current`, `Superseded`, `Historical snapshot`, `Unverified`, or `Contradicted`
- Source and uploader/provider
- Document date or effective period
- Received date
- Verification date and verifier, where applicable
- Version and `Superseded by` link
- Which AI output or human action used it

Do not communicate status by color alone. Default AI synthesis to current evidence. If historical, superseded, or contradictory evidence materially affects an output, surface that fact beside the output and require staff verification.

Never silently overwrite a corrected item. Preserve the prior version where retention rules require it, link the correction, and make the current version unmistakable.

**Confidence:** High for provenance and legal data-quality requirements; low on staff comprehension until tested.

## 4. Does grouping and vocabulary match local practice?

### Findings

Uganda's Insurance Regulatory Authority describes a claims flow using `Claim Intimation/Notification`, `Claims Registration`, `Claims Documentation`, `Complete Claims`, `Reviewing Claim`, `Investigation Claim`, `Response to Claimant`, `Approval of Claim`, `Settlement of Claim`, and the `Complaints Bureau`. It also uses `insured`, `client`, `policyholder`, `claimant`, `claims officer`, `assessor/adjuster`, and `broker`. ([IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf))

The guidelines require ongoing communication, written reasons when a claim cannot be settled, and documented communication with the insured. Current licensing regulations also require insurers and HMOs to give customers ongoing claim-status information and maintain a claims register. ([IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf); [Insurance (Licensing and Governance) Regulations 2020, regs. 55–56](https://ira.go.ug/cp/uploads/Licensing%20and%20Governance%2C%20Reg%202020..pdf))

These sources establish regulatory vocabulary, not the exact shorthand used within every Ugandan insurer.

### Release 1 recommendation

Group active work by operational state rather than generic buckets such as `Chats` or purely temporal groups:

- New claim notifications
- Documents required
- Ready for review
- Assessment / investigation
- Awaiting claimant, insured, broker, or assessor
- Awaiting approval
- Settlement in progress
- Closed / archived
- Complaint or dispute

Use the IRA terms as the initial vocabulary baseline, with two important rules:

1. Keep `insured`, `policyholder`, and `claimant` distinct; they are not reliable synonyms.
2. Test `claim notification` against `claim intimation`, and `loss assessor` against `loss adjuster`, with staff from the pilot organization before freezing labels.

Avoid using `case`, `ticket`, `thread`, and `chat` as the primary staff-facing name when the work is a claim. Those terms can describe system containers, but they do not express the insurance task.

**Confidence:** Medium that the proposed words match formal Uganda practice; low that the grouping matches day-to-day local workflows without field research.

## 5. What should archive, deletion, and retention mean?

### Findings

Uganda's Data Protection and Privacy Act says personal data must not be retained longer than needed for its purpose unless retention is required or authorized by law, needed for a related lawful purpose, required by contract, or consented to. Data used to make a decision must remain available for a legally required period or, absent one, long enough for the person to request access. At the end of the retention period, data must be deleted, destroyed, or de-identified; deletion or destruction must prevent intelligible reconstruction. ([Data Protection and Privacy Act 2019, s. 18](https://ulii.org/en/akn/ug/act/2019/9/eng%402019-05-03))

The Insurance Act requires licensees to keep records sufficient to explain transactions and retain the financial records covered by section 106 for at least ten years after the relevant financial year. This does **not** establish that every chat utterance is automatically a ten-year financial record. ([Insurance Act 2017, s. 106](https://ulii.org/en/akn/ug/act/2017/6/eng%402017-06-08))

Where law requires an electronic record, Uganda's Electronic Transactions Act recognizes electronic retention if the record remains accessible for later reference, preserves the original or a demonstrably accurate representation, and identifies origin, destination, date, and time. ([Electronic Transactions Act 2011, s. 9](https://ulii.org/en/akn/ug/act/2011/8/eng%402011-03-18))

Official enterprise-product behavior also demonstrates why visible removal and compliance deletion must be separated: Microsoft Teams allows an archived team to remain viewable and restorable, while deletion can remain subject to retention or legal holds. ([Microsoft Teams: archive or delete a team](https://learn.microsoft.com/en-us/microsoftteams/archive-or-delete-a-team); [Microsoft Purview retention for Teams](https://learn.microsoft.com/en-us/purview/retention-policies-teams))

### Release 1 definitions

| Term | User-facing meaning | Data effect |
|---|---|---|
| `Archive` | Remove completed/inactive work from default active views | Reversible; retained, searchable from Archive, normally read-only |
| `Restore` | Return archived work to an active queue | Reversible state change; no content recreation |
| `Delete request` | Ask for governed erasure or disposition review | Starts an authorized workflow; does not promise instant hard deletion |
| `Remove from my view` | Personal organization only | Does not alter the authoritative record or another user's access |
| `Retention` | Policy-controlled preservation for a defined purpose and period | Overrides ordinary user deletion where law, contract, dispute, audit, or hold requires preservation |
| `Final disposal` | Policy-authorized deletion, destruction, or de-identification after expiry | Irreversible and auditable; must prevent intelligible reconstruction where deletion/destruction applies |

Release 1 should not offer an unrestricted `Delete conversation` action to ordinary claims staff. Provide `Archive` for work management and a separately worded privacy/records request for governed deletion.

Define retention by record class, purpose, trigger date, period, legal hold, owner, and disposal action. Classify the authoritative claim record, evidence, decisions, communications, audit events, drafts, and transient AI material separately. Obtain Ugandan legal and records-management review before assigning periods; do not apply the Insurance Act's ten-year rule indiscriminately to all conversation content.

**Confidence:** High on the semantic separation and statutory principles; medium on exact product permissions; exact schedules require legal classification.

## 6. Does the experience support interrupted and low-bandwidth work?

### Findings

W3C guidance says users should not lose data because of unknown timeouts; re-authentication should permit continuation without data loss; status messages such as waiting, progress, success, and error should be available to assistive technology without moving focus. W3C's cognitive guidance recommends saving as the user works and preserving place and data through breaks. ([WCAG Timeouts](https://www.w3.org/WAI/WCAG21/Understanding/timeouts); [WCAG Re-authenticating](https://www.w3.org/WAI/WCAG22/Understanding/re-authenticating.html); [WCAG Status Messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages); [W3C Avoid Data Loss and Timeouts](https://www.w3.org/WAI/WCAG2/supplemental/patterns/o4p09-data-loss/))

Controlled interruption research found that reliable visual resumption cues shortened resumption lag and reduced errors, with longer interruptions especially costly when no cue was present. A separate eye-tracking experiment found that marking the interruption location substantially aided resumption. This supports restoring a precise work location rather than merely reopening the same conversation. ([Jetter et al., CHI 2025](https://doi.org/10.1145/3706598.3713685); [Cane, Cauchard and Weger, 2012](https://doi.org/10.1080/17470218.2012.656666))

W3C mobile guidance recognizes slow, high-latency, and costly networks and recommends limiting content, minimizing external resources and page size, caching, informative errors, and a route back to useful information. ([W3C Mobile Web Best Practices](https://www.w3.org/TR/mobile-bp/))

Official web-platform guidance treats slow, flaky, and offline networks as normal failure conditions: service workers can make recent resources available offline, while background synchronization can queue failed writes and retry them after connectivity returns. Queued operations still need visible state, bounded retry, and duplicate protection. ([web.dev service-worker guidance](https://web.dev/learn/pwa/service-workers); [Chrome Workbox background-sync guidance](https://developer.chrome.com/docs/workbox/retrying-requests-when-back-online))

An in-situ study of 34 information workers over 14 days found structured start/end-of-day dialogues could support work reattachment, although it did not study claims or network failure. ([Williams et al., 2018](https://www.microsoft.com/en-us/research/publication/supporting-workplace-detachment-and-reattachment-with-conversational-intelligence-2/))

### Release 1 recommendation

- Autosave drafts and checkpoints server-side where lawful and authenticated.
- Show `Saving`, `Saved`, `Offline`, `Retrying`, and `Sync failed` as accessible status messages with timestamps.
- Reopen to the exact claim, conversation location, selected evidence, and incomplete action.
- Present `Resume review` plus a short, source-linked summary of what changed during the absence.
- Make submissions idempotent so retrying cannot create duplicate decisions, messages, or uploads.
- Queue safe draft changes during temporary disconnection; require reconfirmation before consequential submission after reconnect.
- Make uploads resumable and show per-file progress and failure.
- Load the lightweight work list and text metadata before previews or large attachments.
- Cache stable interface assets and permitted metadata; do not cache sensitive claim content indiscriminately on shared devices.
- Compress transfers, paginate long histories, lazy-load older messages and document previews, and avoid automatic refreshes that discard context.
- Warn before expiry when a secure session cannot be preserved, and restore the prior state after re-authentication.
- Provide explicit retry actions and useful error states; never clear entered work because a request failed.

**Confidence:** High that saving, recoverability, accessible status, and efficient transfer are sound requirements; medium that this exact feature set is the right minimum; low on actual performance under Ugandan field conditions until network testing.

## Release 1 priority

1. Claim-centered active-work list and hybrid search
2. Stable recognition cues and exact-state reopen
3. Current-versus-historical evidence model with provenance
4. Autosave, retry safety, and reconnect recovery
5. Archive/restore with governed retention and deletion requests
6. Pilot-insurer vocabulary configuration after terminology testing

## Validation plan

Recruit practicing Ugandan claims officers, supervisors, and staff who receive or hand off claims. Use a low-fidelity clickable prototype; working software is not required for the first round.

Test six tasks:

1. Reopen a remembered claim from partial contextual cues.
2. Find an older claim when recency is unhelpful.
3. Identify which of two evidence versions is current and explain why.
4. Resume an interrupted review and state what remains unfinished.
5. Archive and then restore completed work.
6. Explain what will happen after `Archive`, `Remove from my view`, and `Delete request`.

Run one session on a throttled, unstable mobile connection. Measure successful retrieval, time, wrong opens, search reformulations, evidence-version errors, recovery from disconnection, duplicate-action attempts, and comprehension of retention language.

Terminology testing should ask participants to name their work before showing proposed labels. Then test the regulator-derived labels in realistic retrieval and handoff tasks. Do not treat preference voting alone as proof of comprehension.

## Confidence and limits

| Question | Desk-research confidence | Main limit |
|---|---:|---|
| Prior-work cues | Medium | Evidence comes from adjacent re-finding domains |
| Find and reopen target | Low–Medium | No insurance prototype test |
| Historical vs current evidence | Medium–High | Model is strong; comprehension untested |
| Local grouping and vocabulary | Low–Medium | Formal regulator language is not workplace ethnography |
| Archive/delete/retention meaning | High for principles | Exact record classes and periods need legal review |
| Interrupted/low-bandwidth support | Medium | No Uganda network or claims-worker field test |

This research can set the prototype hypothesis and legal guardrails. It cannot truthfully claim that users can find, understand, resume, or trust the proposed experience until representative staff perform the tasks.

## Primary sources

- [Uganda Data Protection and Privacy Act 2019](https://ulii.org/en/akn/ug/act/2019/9/eng%402019-05-03)
- [Uganda Insurance Act 2017](https://ulii.org/en/akn/ug/act/2017/6/eng%402017-06-08)
- [Uganda Electronic Transactions Act 2011](https://ulii.org/en/akn/ug/act/2011/8/eng%402011-03-18)
- [IRA Insurance Claims Guidelines](https://ira.go.ug/cp/uploads/claimesguidelines.pdf)
- [Insurance (Licensing and Governance) Regulations 2020](https://ira.go.ug/cp/uploads/Licensing%20and%20Governance%2C%20Reg%202020..pdf)
- [W3C PROV-O](https://www.w3.org/TR/prov-o/)
- [WCAG 2.2 Understanding documents](https://www.w3.org/WAI/WCAG22/Understanding/)
- [W3C Mobile Web Best Practices](https://www.w3.org/TR/mobile-bp/)
- [Microsoft Research publications cited above](https://www.microsoft.com/en-us/research/)

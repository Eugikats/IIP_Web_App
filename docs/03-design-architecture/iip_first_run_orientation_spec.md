# First-run orientation — specification

Owner: UI/UX. Status: specification, implemented 2026-09-14, revised 2026-09-22 (build prompt outcome J: opt-in trigger).
Governed by [`DESIGN.md`](../../DESIGN.md); this document owns the first-run orientation only.

---

## 1. Why this exists, and the rule it bends

A claims officer opening IIP for the first time sees an icon rail of seven destinations, a chat box, and no explanation of either. Every destination is a noun they recognise — Claims, Inbox, Documents — but nothing tells them which one is *theirs*, what the Assistant is for, or that the AI prepares work rather than deciding it. They learn the product by clicking around it, which is how people form the impression that a system is complicated. That is what the orientation is for, on the occasions a person actually wants it.

This bends one line in `DESIGN.md`: *"It should read as a precise instrument, not a product tour."* That line is about the resting state of the product — the instrument must not be cluttered with tutorial furniture, coach marks that never go away, or a help panel competing with content. It is not an argument that the orientation itself, opened deliberately, should not exist.

The resolution, and the constraint every decision below answers to:

> **The orientation is a guest. It is off by default and only ever opens because someone asked for it. It can be refused in one action, it never blocks work, and after it leaves there is no trace of it in the interface.**

If any change to this feature would make that sentence untrue, the change is wrong.

Until 2026-09-22 this document instead specified an orientation that opened itself once, automatically, on a new user's first `/app` visit. That was a considered decision, not an oversight, but the build prompt sets the product's guidance model explicitly: *"the first run tour is off by default and available from help"* — nothing self-triggered, however well filtered. §2 below reflects that change; §3 and §4 (role filtering, step content) are unaffected by it.

---

## 2. Trigger and persistence

| Question | Answer |
|---|---|
| When does it run? | Only at `/app`, and only immediately after **Show me around** (profile menu) was clicked. Nothing else opens it — there is no automatic first-visit trigger. The route check is explicit, not implied by where the component is mounted: it lives in the shell, which wraps every `/app` route, so without it a user following a deep link into a claim would be shown "Start here" pointing at the Assistant while looking at something else. |
| What records that? | `localStorage`, key `iip.orientation.requested.v1`, value `"1"`. "Show me around" sets it, then hard-navigates to `/app`; the orientation reads it once on mount, opens if present, and removes it immediately either way, so a stale request cannot resurrect itself on a later visit. |
| Why not track per user id any more? | The old key tracked "has this user already seen it" so it could skip them automatically. There is no more automatic path to skip, so the flag is a one-shot request rather than per-user state. |
| Does it ever run again? | Every time someone clicks **Show me around** in the profile menu. There is no limit — it is a deliberate, repeatable request, not a once-ever hint. |
| What if `localStorage` is unavailable? | The request cannot survive the reload "Show me around" triggers, so the orientation does not open. A private window or blocked site data get an inert button rather than a broken tour. |

---

## 3. Role awareness — the part most tours get wrong

**Every step must point at a destination the current user can actually open.** Steps are filtered through the same `canAccess` check the rail itself uses, before the tour starts.

This is not a nicety. Showing a Claims Officer a step about the Admin console is a promise the product then breaks the moment they click it, and it teaches them that the interface says things that are not true about their own account. A tour that describes a screen the user cannot reach is worse than no tour.

Consequences that follow from this rule:

- If filtering leaves fewer than two steps, the orientation does not run at all. A one-step tour is a popup, not an orientation.
- Step numbering is computed after filtering. A user never sees "Step 2 of 6" and then reaches the end at step 4.
- The Assistant step is the only one shown to every role, because `/app` is where everyone lands.

---

## 4. The steps

Copy is written in the language of the job, not the language of the system — the same standard applied across the interface. Every step answers "what is this for?" in one sentence, then "what would I do here?" in one more. Nothing promises a capability that is not built.

| # | Anchor | Title | Body |
|---|---|---|---|
| 1 | Assistant (rail) | Start here | Ask for what you need in plain words — "what needs my attention today?" or "what does the policy say about flood damage?". Answers about cover come only from your approved documents, with a link to the source. |
| 2 | My work (rail) | What is waiting for you | Everything assigned to you, in one list — claims to review, approvals to give, documents to check. This is the one place that answers "what should I do next?". |
| 3 | Claims (rail) | Every claim | Search, filter and open any claim in your workspace. Each one shows its full history: what happened, who did it, and what the AI prepared along the way. |
| 4 | Inbox (rail) | Messages from customers | WhatsApp, email and web chat in one place, sorted by what each person is asking about. AI drafts replies; nothing reaches a customer until you approve it. |
| 5 | Documents (rail) | Everything received | Every document that has arrived across claims and channels. The AI reads what it can, and flags anything it is unsure about so you can check it. |
| 6 | Profile (rail, bottom) | Settings and everything else | Your account, search, connected channels, the knowledge base and the admin console live behind here. |

**Closing step.** The final card carries no anchor and one sentence, because it is the single most important thing to say and the interface should not have to be trusted to imply it:

> **The AI prepares, you decide.** It can summarise, draft and flag, but it never approves, rejects or settles a claim, and nothing goes to a customer without your say-so.

---

## 5. Interaction contract

- **Escape** closes it. So does the X, so does clicking the backdrop, so does **Skip**.
- **Enter** / **Next** advances. The final step's button reads **Done**, not **Next**.
- Focus moves into the card on open and returns to the element that had it when the tour closes.
- The card is `role="dialog"` with `aria-modal="true"` and is labelled by its own heading.
- Back is available from step 2 onward. A person who skimmed too fast must be able to return without restarting.
- The highlighted rail item gets a violet ring — the one accent, used to mark the thing being talked about, which is exactly what the accent is for.
- The backdrop is a plain scrim at low opacity. No spotlight cut-outs, no blur, no animated pulse on the anchor.
- **Nothing behind the card is clickable while it is open.** A half-interactive tour where some clicks work and others do not is more confusing than a modal one.

**Motion.** The card fades in over 160ms and moves between anchors over 220ms, matching the rail's own step. It does not bounce, pulse, or slide from off-screen.

---

## 6. Required states

The orientation is a UI surface and answers the same state questions as any other.

| State | Behaviour |
|---|---|
| Loading | Never renders during load. It waits for the rail to be present and for permissions to resolve, so it can never anchor to an element that has not rendered or describe a destination whose access is still unknown. |
| Empty | Not applicable — content is static, not fetched. |
| Error | If an anchor cannot be found in the DOM, that step is dropped rather than rendering a card pointing at nothing. If every step drops, the tour closes silently. |
| Not configured | Not applicable — it depends on no provider. It must work on a completely unconfigured workspace, because that is exactly when a first-time user arrives. |
| No permission | Handled by the filtering in §3. |
| Reduced motion | The global `prefers-reduced-motion` block neutralises the fade and the movement. Position changes become instant. The tour still works. |

---

## 7. Why not intro.js, driver.js, or shepherd

All three do more than this needs and none of them know the design language.

- Every one ships its own stylesheet, which then has to be fought to remove the shadows, rounded pills, blue accents and default button styling that `DESIGN.md` forbids. Overriding a library's opinions costs more than not having them.
- They assume an anchor exists and render a broken popover when it does not. §6 requires the opposite.
- None of them can filter steps by permission, which §3 makes the load-bearing requirement.
- The component this replaces them with is roughly 200 lines with no dependency, no stylesheet to override, and no upgrade to track.

The repository rule — *do not introduce new frameworks or packages without a clear reason tied to the implementation guide* — points the same way.

---

## 8. Copy rules

Inherited from the craft guardrails, restated because tour copy is where products most often break them:

- No emoji. Not in titles, not as step markers.
- No exclamation marks. No "Welcome aboard", no "Let's get started", no "You're all set".
- Second person, active voice, present tense.
- Never name a capability that is not built, and never describe a screen in terms of what it will do later.
- A step is at most two sentences. If it needs three, the screen it describes is too complicated and that is the real bug.

---

## 9. Verified in browser

Checked at 1280px and 768px against the parity-mode portal on 2026-09-14, not inferred:

| Check | Result |
|---|---|
| Appears on `/app`, absent on `/app/channels` and `/app/claims` | Pass |
| Seven steps for a Workspace Administrator, counter consistent throughout | Pass |
| Highlight ring lands on the described rail item | Pass |
| Card fully on-screen at both widths (340px wide at x=241) | Pass |
| Final button reads Done, not Next | Pass |
| Dismisses, and stays dismissed across a reload | Pass |
| Escape closes it | Pass |
| No horizontal page overflow at 768px | Pass |
| No console errors on any route visited | Pass |

Three real defects were found by looking rather than reasoning, and fixed:

- The tour ran on every `/app` route, not just the landing screen (the §2 row above now states the rule the code enforces).
- The composer placeholder wrapped to two lines at 768px and had its second line clipped outright — a `rows=1` textarea with a fixed min-height has no scrollbar for placeholder text.
- The conversation list clipped its search placeholder mid-word and wrapped timestamps onto two lines.

A fourth finding was a false alarm worth recording: the rail's width transition measured 150ms rather than the intended 220ms. That was a dev server serving CSS from before the Tailwind config change, not a defect — the built stylesheet contained `.duration-dls-panel{transition-duration:.22s}` all along, and a restarted server measured 220ms. **Verify against a server started after a `tailwind.config.ts` change, or the measurement is of stale CSS.**

---

## 10. What this deliberately does not do

- It does not gate anything. A user can dismiss it and use the product immediately.
- It does not track completion server-side, report progress, or feed analytics.
- It does not re-run after a release, a role change, or a new feature. Per-feature hints are a different pattern with a different cost, and this document does not authorise them.
- It does not appear for the claimant-facing surfaces at `/access`. Those are single-purpose screens used once by a member of the public; a tour there would be noise.

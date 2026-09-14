# First-run orientation — specification

Owner: UI/UX. Status: specification, implemented 2026-09-14.
Governed by [`DESIGN.md`](../../DESIGN.md); this document owns the first-run orientation only.

---

## 1. Why this exists, and the rule it bends

A claims officer opening IIP for the first time sees an icon rail of seven destinations, a chat box, and no explanation of either. Every destination is a noun they recognise — Claims, Inbox, Documents — but nothing tells them which one is *theirs*, what the Assistant is for, or that the AI prepares work rather than deciding it. They learn the product by clicking around it, which is how people form the impression that a system is complicated.

This bends one line in `DESIGN.md`: *"It should read as a precise instrument, not a product tour."* That line is about the resting state of the product — the instrument must not be cluttered with tutorial furniture, coach marks that never go away, or a help panel competing with content. It is not an argument that a person's first sixty seconds should be unguided.

The resolution, and the constraint every decision below answers to:

> **The orientation is a guest. It arrives once, it can be refused in one action, it never blocks work, and after it leaves there is no trace of it in the interface.**

If any change to this feature would make that sentence untrue, the change is wrong.

---

## 2. Trigger and persistence

| Question | Answer |
|---|---|
| When does it run? | On first arrival at `/app` for a signed-in user who has not seen or dismissed it. |
| What records that? | `localStorage`, key `iip.orientation.seen.v1`, value the user's own id. |
| Why per user id? | Two people sharing a workstation each get their own first run. A shared key would silently skip the second person. |
| Why `v1` in the key? | A future revision with materially different content can re-run once without a migration. Bumping it is a deliberate act, not a side effect of editing copy. |
| Does it ever run again? | Only if the user asks. A **Show me around** item in the profile menu clears the key and starts it. |
| What if `localStorage` is unavailable? | The orientation does not run. A private window or blocked site data must not produce a tour on every page load — that is worse than never showing it. |

**This is per browser, not per account.** The same person on a new machine sees it again. That is an honest limitation of client-side storage and is accepted deliberately: the alternative is a new server-side per-user preference table, which is not worth a schema change for a once-ever hint. It is recorded here so nobody later reports it as a bug.

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

## 9. What this deliberately does not do

- It does not gate anything. A user can dismiss it and use the product immediately.
- It does not track completion server-side, report progress, or feed analytics.
- It does not re-run after a release, a role change, or a new feature. Per-feature hints are a different pattern with a different cost, and this document does not authorise them.
- It does not appear for the claimant-facing surfaces at `/access`. Those are single-purpose screens used once by a member of the public; a tour there would be noise.

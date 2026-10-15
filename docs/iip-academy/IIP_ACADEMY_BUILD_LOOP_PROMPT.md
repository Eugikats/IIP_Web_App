# IIP Academy build loop: instructions for every iteration

You are building the new IIP Academy in `Projects/iip_saas` (branch `saas/merge-v2-2026-10-08`), live on
app./api.insuranceintelligenceplatform.com (dev apps in `rg-iip-dev`). The owner is asleep and cannot answer questions.
Do not stop to ask. Make the recommended choice, record it in the ledger, and carry on. Put questions for the owner in
the ledger's "Questions for the owner" list; they are asked at the very end. Stop only when every phase below is done
and verified live, or when a real blocker needs a person (a missing credential, an Azure permission). Then say exactly
what blocked you.

## Read first, every iteration

1. `docs/iip-academy/BUILD_LEDGER.md`: what is done, what is next, decisions made, questions. It is the memory.
2. `docs/iip-academy/IIP_ACADEMY_REBUILD_PLAN.md`: the plan and the owner's decisions.
3. The design: `docs/iip-academy/design/iip-academy-reference.html`. It is the visual authority. Copy it exactly: same
   markup, class names, colours, spacing, fonts, icons, copy, interactions. Differences allowed only where the design
   shows invented data (see the plan, section 2), and there the layout stays and the content becomes real or honestly empty.
4. The content: `docs/iip-academy/academy-content/*.md` (Course 1 research packs, assessments, final task and rubric;
   Course 2 map; the master register). It is the source for Course 1's lessons and quizzes.
5. Repo rules: `CLAUDE.md` files, memory (no em dashes anywhere, no emoji, no fabricated data, six UI states, honest
   "Not configured"), commit footer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## What "done" means

- Every screen of the design exists, looks like the design at 1280, 768 and 390 px, and **works**: a learner can open the
  Academy from the IIP sidebar, browse, open a course, enrol (free), start, read lessons, take unit quizzes, module
  assessments and the final task, earn and view a certificate, edit their profile and settings, read notifications, join
  and use the community, and ask Zenith. "Start now", "Continue", "Save changes" and every other button does what it says.
- A signed-out visitor can read the public pages and verify a certificate.
- The navbar has "Back to IIP". The sidebar "IIP Academy" opens the new dashboard. Old `/app/academy` redirects.
- No invented people, progress or numbers. Empty states in the design's style. No emoji (the waving hand is a drawn icon).
- Course 1 uses the supplied content for all 12 units, 12 unit quizzes (10 questions), 4 module assessments (15), and the
  final applied task with its rubric. Pass marks and attempt limits as the content says. The assistant (Zenith) is hidden on
  quiz, assessment and final-task pages.
- Tests: backend unit tests, tenant/identity isolation, browser tests at two widths, and design-parity screenshots.
  The full backend suite and `pnpm lint` and `tsc --noEmit` pass before every deploy.

## Method each iteration

1. Read the ledger. Pick the first unfinished task in the first unfinished phase.
2. Do a complete, verified slice: build it, test it, open the page in a real browser and look at it, fix what looks wrong.
3. Commit (small, honest messages). Update the ledger (done, next, decisions, open questions, what was verified and what
   was not). Keep the ledger short and current.
4. Deploy at the phase gates marked DEPLOY (API with `scripts/local/deploy-dev-api.sh`, then apply new migrations at once
   with `POST /api/v1/admin/db-migrate` using `az account get-access-token --resource api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73`,
   then the portal with `IIP_PRESENTATION_MODE=true IIP_API_BASE_URL=https://api.insuranceintelligenceplatform.com
   IIP_ENTRA_REDIRECT_URI=https://app.insuranceintelligenceplatform.com/app bash scripts/local/deploy-dev-portal.sh`).
   Deploys are slow on B1 (portal 20 to 30 minutes). The CLI can report failure after Azure succeeded: check
   `az webapp log deployment show` and `/health/ready` before retrying. Run deploys in the background and keep working on
   source that the running build has already finished compiling. Never leave the live site broken: a new read on a request
   path must tolerate a missing table.
5. Then continue straight to the next task. Do not end a turn on a status message while work remains.

## Hard rules

- Tenant and identity isolation: Academy data is in the Academy platform scope keyed by the learner's sign-in identity.
  It never reads or writes an insurer's workspace data. Tests prove one learner cannot read another's data.
- Quiz and assessment answers never reach the browser before submission. Grading is on the server.
- Certificates are issued only by the server, only when every requirement passes.
- Zenith answers only from course lessons, cites them, has per-learner and monthly limits, and sends no personal or claims data.
- Community: consent before posting, reporting and moderation, rate limits, no customer data.
- Secrets never in the repo or chat. Do not weaken security, tenant isolation or two-factor to make a check pass.
- No em dashes or emoji in code, content shown to users, docs or commits. The supplied content files use em dashes:
  the content builder converts them (comma, colon or parentheses). Do not edit the supplied files.
- Do not delete or overwrite the owner's files. Do not push or merge to main.

## Phases (the ledger tracks them)

0. Content pipeline. 1. Frontend foundation. 2. Read screens on real content. 3. Learner backend. 4. Learner screens
live (DEPLOY). 5. Community (DEPLOY). 6. Zenith (DEPLOY). 7. Cut-over, cleanup, final live verification (DEPLOY).
Details and gates are in the ledger and the plan.

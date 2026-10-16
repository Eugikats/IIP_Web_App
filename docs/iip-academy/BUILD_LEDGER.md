# IIP Academy build ledger

Updated at the end of every iteration. Newest facts win. Keep it short.

## Decisions made (owner asleep, recommended choices)

- Course 1 "AI & Agentic AI for Insurance" (id `ai-agentic-insurance`) takes its lessons, quizzes, assessments and final task
  from `academy-content/`. It replaces the older shorter content of the same course. The other existing courses keep their
  content, reshaped into lessons (idea, in IIP, worked example) with their quiz as the course check.
- Course 2 "Claims Communication & Service Recovery" is listed "Coming soon" with its map as the outline (no lessons yet).
- Course content lives on the server as JSON built by a script from the supplied markdown and the existing TS content. The
  browser gets lessons and questions without answers. The server grades and issues certificates.
- Academy data lives in one table `AcademyRecords` (kind, owner, payload) in the Academy platform scope.
- The Academy operator is an administrator of the platform's own workspace (config `Academy:OperatorTenantId`, default the
  deployment's tenant). Operators enrol people, mark final tasks, create groups and events, moderate, set the Discord link.
- Final applied task: learner submits text and the ten-box canvas; an operator marks it against the rubric (pass 60 of 100);
  the certificate follows a pass. Until marked it shows "Submitted, waiting for review".
- Lessons have no recordings yet (video production not started in the content register): the player area says so honestly and
  the lesson text carries the lesson.
- Payments are not connected: free courses enrol at once; a priced course shows the checkout dialog with "Payments are not
  connected yet" and an operator can enrol. Course price list stays as it is (UGX 500,000), shown on the course page.
- Public sign-in beyond Microsoft work/school accounts needs External ID (owner action). Noted on the sign-in page.

## Phase status

| Phase | State |
|---|---|
| 0 Content pipeline | done, committed |
| 1 Frontend foundation | done, committed |
| 2 Read screens on real content | done, committed |
| 3 Learner backend | done, committed (42 Academy backend tests pass) |
| 4 Learner screens live | built and browser-tested locally; live deploy is the single release below |
| 5 Community | built and browser-tested locally |
| 6 Zenith | built and browser-tested locally (no language model offline: explanations say so) |
| 7 Cut-over | done in code: sidebar link, old routes redirect, old screens and workspace price settings removed |
| Deploy and live verification | next: one combined release (API, migration AddAcademyRecords, portal) |

## Done log

- 2026-10-16: content pipeline (10 courses, 116 lessons, Course 1 exact from the supplied files).
- 2026-10-16: new Academy frontend from the design (dashboard, courses, course, lesson, quiz, final task, certificates and public
  verification, profile, progress, settings, notifications, community, Zenith, Academy team screen).
- 2026-10-16: server-side grading, attempts, certificates, community with consent and reports, Zenith grounded in lessons with
  daily and monthly limits.
- 2026-10-16: cut-over: sidebar and website link to /academy, /app/academy redirects, legacy sources moved to
  web/portal/scripts/academy-source (the content builder reads them).
- Browser tests (desktop 1280, tablet 768, phone 390): tests/academy/{journey,community,zenith,navigation}.spec.ts.

## Next

Run the full backend suite, lint and tsc; deploy the API and apply the migration; deploy the portal; verify live (sign-in with a
real Microsoft account, enrol, quiz, certificate, community, Zenith explanations with the real model); then finish.

## Questions for the owner (asked at the very end)

1. Discord invite link.
2. Do you hold the rights to the hero photograph in the design (the woman in the dashboard hero)? It is embedded in the HTML.
3. Which payment provider for mobile money and card?
4. Who should be the Academy operators besides platform administrators? (Default: administrators of the platform workspace.)
5. Seven courses the design lists but the content register does not name: they are not listed now. Add them as "Coming soon" or leave them out?
6. Payments are not connected, so enrolment is open and charges nothing. Is that right until a provider is chosen?
7. Zenith limits: 30 written explanations per person per day and 2,000,000 tokens a month for everyone (Academy:ZenithDailyLimit, Academy:ZenithMonthlyTokens). Change?

## Not verified / known gaps

(none yet)

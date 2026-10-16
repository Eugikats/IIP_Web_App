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
| Deploy and live verification | done 2026-10-16: API, migration AddAcademyRecords and portal live; visitor pages checked in a real browser at 1280, 768 and 390; signed-in API checked with a real Microsoft token; a Zenith explanation checked against the live model |

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

Nothing is queued. Owner to open https://app.insuranceintelligenceplatform.com/academy while signed in and walk through enrol,
a unit quiz, the profile and settings, the community and Zenith in the browser (the signed-in screens were verified locally in
a browser and live through the API, but not live in a browser because that needs a real Microsoft sign-in with two-factor).

Live notes: the deployed API rejected every anonymous call, so the public catalogue, certificate check and who-am-I are now
exempt in RuntimeHeaderGuard (tests pin that the rest of the Academy API still needs sign-in). The portal needed a manual
`az webapp restart` after deploy before it served the new build. The admin account (OticGroup@...) was enrolled in Course 1
once while testing Zenith.

## Questions for the owner

Answered 2026-10-17: Discord link is set in the admin screen (built). Hero photo is AI generated (no rights question). Payments:
Flutterwave, same approach as oticlms. Seven design-only courses: left out. Zenith: 10 a day, 1,000,000 tokens a month.
Price: USD 85 per course.

Open, see IIP_ACADEMY_PRICING_PAYMENTS_ASSESSMENT_PLAN.md: the other eight courses (short checks only, or full 10/15 banks);
card plus mobile money with a shilling rate; same or separate Flutterwave account; is 85 dollars tax inclusive; rotate the keys
found in oticlms/FLUTTERWAVE_INTEGRATION.md. Who the Academy operators are besides platform administrators.

## 2026-10-17 additions (local, not deployed)

USD 85 pricing, Flutterwave payments behind IPaymentGateway (stand-in for local tests), order records, reconcile worker, Payments
tab, Zenith lock during assessments and the final task, Zenith limits 10 a day and 1,000,000 tokens, editable settings.
Browser tests: tests/academy/payments.spec.ts (start the API with scripts/local/run-offline-payments.sh). Needs the owner: the
Flutterwave keys and a go for the deploy. See IIP_ACADEMY_PRICING_PAYMENTS_ASSESSMENT_PLAN.md.

## Not verified / known gaps

(none yet)

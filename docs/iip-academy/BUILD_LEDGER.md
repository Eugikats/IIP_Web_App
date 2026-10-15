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
| 0 Content pipeline | not started |
| 1 Frontend foundation | not started |
| 2 Read screens on real content | not started |
| 3 Learner backend | not started |
| 4 Learner screens live (DEPLOY) | not started |
| 5 Community (DEPLOY) | not started |
| 6 Zenith (DEPLOY) | not started |
| 7 Cut-over, cleanup, final verification (DEPLOY) | not started |

## Done log

(none yet)

## Next

Phase 0, first task: content builder script for Course 1 from the supplied markdown.

## Questions for the owner (asked at the very end)

1. Discord invite link.
2. Do you hold the rights to the hero photograph in the design (the woman in the dashboard hero)? It is embedded in the HTML.
3. Which payment provider for mobile money and card?
4. Who should be the Academy operators besides platform administrators? (Default: administrators of the platform workspace.)
5. Seven courses the design lists but the content register does not name: keep them as "Coming soon" or remove them?

## Not verified / known gaps

(none yet)

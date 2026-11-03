# IIP Academy flagship release audit

Date: 19 October 2026.

## Decision

Reviewer editions suitable for structured testing, not approved paid releases. The planned price is USD 120 per course. This audit does not establish that either course merits UGX 500,000, is accredited, or has professional endorsement.

## Delivered material

| Item | Foundation | Sales, distribution and customer experience |
|---|---:|---:|
| Modules | 4 | 4 |
| Lessons | 12 | 12 |
| Editable slide decks | 12 | 12 |
| Narrated videos | 12 | 12 |
| Audio downloads | 12 | 12 |
| Caption files | 12 | 12 |
| Transcripts | 12 | 12 |
| Unit question-bank entries | 120 | 120 |
| Module assessment question placements | 60 | 60 |
| Human-marked final task | 1 | 1 |

The module placements reuse selected unit-bank questions. They are not another 120 independent unseen questions. Both courses together contain 114.9 minutes of recorded teaching, plus reading, practice and final work. The earlier 8 to 12 minute expectation is not achieved by every individual video; current durations are measured, not advertised estimates.

## Canonical files and live integration

All authoring folders and generated media are under:

C:\Users\Administrator\Documents\Projects\Otic\Projects\agenticIip-v2\docs\iip-academy

- flagship-content/foundation: twelve lesson source folders and the foundation instructor manifest.
- flagship-content/sales: twelve lesson source folders and the sales instructor manifest.
- flagship-content/sales-lesson-plans.json: the sales teaching plan.
- media/C01_U1-1 through media/C01_U4-3: foundation videos, decks, audio, captions, transcripts and per-slide production cache.
- media/C02_U1-1 through media/C02_U4-3: the equivalent sales media.
- quality/media-audit.json: source/audio fingerprint and timing audit.
- quality/foundation-slide-review.jpg and sales-slide-review.jpg: sampled slide contact sheets.
- quality/*-top.png: browser screenshots used to inspect desktop/tablet/phone presentation.
- FLAGSHIP_FILE_MANIFEST.csv: the exact generated-file inventory, sizes and SHA-256 fingerprints.
- PRODUCTION_RUNBOOK.md and LEARNER_TEST_GUIDE.md: maintenance and reviewer instructions.

The existing live Academy is owned by the sibling iip_saas application checkout. Its already-deployed AcademyRecords persistence and existing enrolment, assessment, final marking and certificate contracts are reused. The speculative learner implementation initially prepared in the older checkout was removed, rather than introducing a competing system or downgrading the live database.

Existing live IDs are retained: ai-agentic-insurance and ai-sales-distribution. There are no duplicate runtime flagship courses. The sales course changes from the older sixteen-lesson structure to the new twelve-lesson structure. Existing historical progress/certificates are not deleted; versioning and any treatment of historical completion require operator review before paid release.

## Verification evidence

- All twenty-four finished videos passed decoding. Source/transcript equality, slide audio fingerprints and video/audio duration checks passed.
- There are 120 private learner-facing media blobs: video.mp4, audio.mp3, captions.vtt, slides.pptx and transcript.md for each unit. Instructor answer keys are not uploaded as learner media.
- Anonymous direct blob access was refused. The API requires the actual signed-in learner's enrolment and uses its existing managed identity.
- Backend suite: 722 Domain tests and 1,554 API tests passed. Both courses' final-task submission, correction, human marking and certificate gates are covered.
- Six browser media tests passed: both courses on desktop, tablet and phone. They cover video playback, caption tracks, real downloads, enrolment isolation, quiz display and the assistant lock. The mobile content overflow discovered during visual inspection was corrected and a bounding-box assertion was added.
- Eleven learner-journey browser tests passed, including the foundation course through a human-marked final and a verifiable certificate, plus responsive navigation checks.
- Portal production build, type checks and lint passed. Existing image-optimisation/deprecation warnings remain.
- Runtime-data hygiene, provider-status, UI-release gates and seven separate trust/authority checks passed.
- The overall portal test command is not green: the pre-existing /academy role-route guard and role/action parity checks fail. Their files were not weakened or changed to make this release appear green.
- Both reviewer application packages are deployed and Azure reports RuntimeSuccessful. All 120 live authenticated media byte-range requests returned HTTP 206; their results are in quality/azure-media-verification.json. Live browser playback, captions and transcript downloads passed for both courses, using the existing deployment operator's own identity. Downloaded transcript SHA-256 fingerprints match their canonical sources. See quality/deployed-browser-verification.json and the deployed-player screenshots. This is not an interactive login test as Eugene.
- Interactive Microsoft sign-in as Eugene has not been performed on his behalf. Course enrolment is granted to his existing guest identity, without adding administrative or Azure storage privileges.

## Azure and spending

Narration uses the existing Speech resource in subscription 88c8b4ab-3510-4a8d-832c-73bf72088b5a. Application deployment and the existing storage account are in subscription 5f5765ae-985f-42e4-a443-8e2447be33ca.

The narration ledger reserves USD 2.746876, including the requests made to correct changed lessons, within the authorised USD 20 total. This is a conservative estimate, not an Azure invoice or a verified credit balance.

The temporary publisher assignment on the Academy media container was removed after verification. No Azure privileges were granted to Eugene. Existing API managed-identity permissions were unchanged.

Pre-deployment API and portal code archives were saved under artifacts/academy-rollback. No database migration is needed for this integration. Existing cloud application settings were preserved. Source changes remain uncommitted; no new branch, commit or Git push was made.

## Outstanding paid-release gates

1. Subject-matter and source review. Confirm the depth and accuracy of every unit, especially legal/regulatory material and any industry examples. Draft research is not an authoritative legal opinion.
2. Assessment review. Rewrite overly mechanical sales distractors, assess difficulty, and create independent module examination banks if the intended standard requires them.
3. Listening review. Technical decoding and duration checks do not establish natural pronunciation, good delivery or absence of distracting speech artefacts. No independent listening pass is claimed.
4. Caption review. Current captions use estimated sentence/character timing, not verified word alignment.
5. Learner pilot. Observe an insurer learner completing both courses, check task clarity, measure study time and collect evidence of practical competence and value.
6. Existing learner/version policy. Decide how historical sales-course progress and certificates relate to the revised curriculum.
7. Engineering release gates. Resolve the pre-existing portal guard/parity failures and existing high-severity System.Security.Cryptography.Xml package advisories before claiming a clean production candidate.

paidReleaseApproved remains false for both courses. The API blocks paid checkout and new self-enrolment; authorised operators can arrange reviewer access. No automated audit is substituted for these outstanding approvals.

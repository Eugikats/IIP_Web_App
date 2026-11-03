# Flagship course production and integration

Started 19 October 2026. User-authorised scope: complete AI & Agentic AI for Insurance and AI for Insurance Sales, Distribution & Customer Experience, with slides, Azure narration, video, quizzes, learner integration, audit and deployment where verified.

## Constraints and decisions

- Narration cap: USD 20 total including retries. Do not infer free usage from subscription names.
- Existing narration resource: oticgroup-8430-resource, rg-OticGroup-8430, subscription 88c8b4ab-3510-4a8d-832c-73bf72088b5a. Confirmed Succeeded on 19 October 2026.
- Existing application target: app-portal-dev-h5girbnzrayqe and app-api-dev-h5girbnzrayqe, rg-iip-dev, subscription 5f5765ae-985f-42e4-a443-8e2447be33ca. Portal confirmed Running. Deploy only after affected checks and without replacing unrelated settings.
- Requested course price: USD 120. Preserve unrelated legacy-course prices and existing workspace settings unless explicitly updating these flagships.
- Requested tester: eugene.katusiime@oticgroup.net. Resolve actual identity and least-privilege course access; never fabricate account access or broaden administrative privileges.
- No new Azure service instances or resource groups. A private Academy media container was added to the existing storage account. Use keyless authentication; never log tokens or secrets.

## Audit at start

- Existing portal has four legacy sample courses, short text modules and browser-local completion/certificates.
- The older agenticIip-v2 checkout initially appeared to support only pricing. Live migration inspection identified the newer iip_saas checkout, which already owns enrolment, assessment, progress, final marking and certificates. Integration uses that implementation instead of deploying a competing learner system.
- Foundation research covers twelve units; assessment drafts exist. Legal and time-sensitive source claims require primary-source checking before narration.
- Sales course has a first-unit script and quiz and eleven outlined units.
- No complete lesson media pipeline or finished course videos were found.

## Required evidence before paid-course readiness

- All twenty-four units have substantive reviewed teaching content, usable slides, actual narration, rendered video, captions and downloads.
- Video/audio timing and visual review pass, and failures are corrected rather than merely recorded as success.
- Ten questions per unit and fifteen per module are available with server-side grading and restricted answers.
- Final tasks, assessment progression and durable scoped progress are implemented; human assessment is not automatically marked passed.
- Course access, price, learner navigation, media delivery, assessment assistant restrictions and certificate conditions are verified with the actual signed-in learner flow.
- Azure deployment and Eugene's access are separately verified. Local success is not a deployment claim.
- Expert/legal review, if not completed, remains an explicit readiness limitation. No automated audit alone proves that a course is commercially or professionally approved.

## Current status

## Verified progress

- Twenty-four units have editable slide decks, real Azure narration, rendered video, captions and transcripts.
- The media source/audio fingerprint and duration audit passed for all twenty-four units. Recorded teaching totals 114.9 minutes across both courses.
- Conservative narration reservation: USD 2.746876 out of the authorised USD 20. This is an estimate, not a billing statement.
- All 120 learner media files were uploaded to the existing storage account's private academy-media container. Anonymous access was refused.
- Both existing live course IDs now expose twelve lessons, four modules and a USD 120 planned price. No duplicate runtime courses were added.
- Eugene's existing Microsoft guest identity received reviewer enrolment in both courses. No staff administrator or Azure learner role was granted.
- Browser checks passed for both courses on desktop, tablet and phone, including playback, captions, downloads, private access, quiz display and assistant locking. The mobile lesson overflow caught by visual review was corrected.
- Backend checks passed: 722 Domain tests and 1,554 API tests, including final submission, marking and certificate gates for both flagships. Portal production build, type checks and lint passed, with pre-existing warnings.
- The broader portal test command is not green: existing role-route and action-permission-parity checks fail outside this change. Runtime-data, provider-status, UI-release checks and the separate trust/authority tests passed. No guard was weakened.
- Pre-deployment API and portal rollback archives were saved outside Git. Both reviewer builds are deployed and Azure reports RuntimeSuccessful. All 120 authenticated live media requests returned HTTP 206. The deployed browser player, captions and transcript download passed for both courses using the existing deployment operator's own identity, not Eugene's identity. Downloaded transcript fingerprints match their canonical sources.
- API deployment: f925563c-19a9-473d-9d49-ef37a3f2792b. Portal deployment: d1e2a998-98db-4309-9482-bea5af6e4d93. No database migration or application-setting replacement was needed.
- The temporary publisher permission on the media container was removed. The existing API managed-identity permission was not changed.

## Paid release remains blocked

These are reviewer editions. Subject-matter/legal/source review, independent assessment-quality review, listening review, caption alignment and an insurer learner pilot are not completed. Module assessments currently reuse selected unit questions, and the sales question generation needs further editorial work. Existing high-severity package advisories also remain a broader engineering release limitation.

No claim is made that this is an accredited or commercially approved UGX 500,000 course. The API refuses paid checkout while paidReleaseApproved is false. A real interactive Microsoft sign-in as Eugene must still be completed by Eugene.

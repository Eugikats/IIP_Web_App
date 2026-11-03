# Flagship media integration, 19 October 2026

The live Academy keeps its existing course IDs, AcademyRecords persistence, enrolment, grading, human marking and certificate rules. No new learner database or migration is introduced.

## Authoring and generation

Canonical authoring and generated media are in the sibling agenticIip-v2 checkout at docs/iip-academy. See its PRODUCTION_RUNBOOK.md, FLAGSHIP_RELEASE_AUDIT.md and FLAGSHIP_FILE_MANIFEST.csv. Do not make another manual copy of those source packs here.

The API's existing embedded academy-content.json is a generated application snapshot. To regenerate it, first use any applicable existing catalogue builder, then run the sibling scripts/academy/package_catalog.py with --catalogue pointing to this application's src/InsuranceIntelligence.Api/Academy/Content/academy-content.json. The second step overlays only ai-agentic-insurance and ai-sales-distribution, retaining the other course entries. A normal portal build does not regenerate the API catalogue.

## Media contract

An AcademyLesson may carry MediaUnitId. It is an authored mapping, not a path supplied by a learner. The authenticated lesson view returns that mapping's presence, while the public catalogue never returns question answers or lesson text.

GET /api/v1/academy/courses/{courseId}/lessons/{lessonId}/media/{fileName} checks the signed-in learner's own enrolment and the actual lesson before accessing private storage. Only video.mp4, audio.mp3, captions.vtt, slides.pptx and transcript.md are accepted. The blob path is flagships/v1/{MediaUnitId}/{fileName} in the private academy-media container on the existing AzureBlob account. The existing API managed identity has access; no storage key or SAS is delivered to the browser. Responses are private/no-store and support byte ranges.

The portal obtains media through its authenticated API helper and uses browser object URLs, cleaning them up on navigation. A download is prepared only when requested, then a visible Save link gives the browser an explicit user action, including on mobile. Captions are labelled as estimated timing pending review. Missing recordings on unrelated legacy courses retain their existing honest no-recording state.

## Assessments and reviewer gate

Each assessment opening returns a sittingId. Leaving posts that ID to the existing assessment leave route. Each opening owns a separate server-side sitting key, so delayed cleanup from an earlier browser render cannot release another active sitting. Cleanup uses an authenticated keepalive request on page exit. Submitting an assessment ends its relevant sittings; abandoned records expire under the existing ninety-minute policy. A leave request without a valid sitting ID deliberately does not clear newer sessions. Older open clients may remain locked until expiry and should reload the new portal.

paidReleaseApproved is false for these two reviewer courses. The API refuses paid checkout and new self-enrolment, while existing enrolment and authorised operator grants permit review. Other course policies are unchanged. The public course page explains the planned USD 120 price and why paid enrolment is unavailable.

Source, assessment, listening and caption reviews remain release gates. Sales lessons change from the older sixteen-lesson layout to twelve new lesson IDs; historical learner records are not deleted. A historical completion/version policy needs approval before paid release.

## Checks

Full backend suite: 722 Domain and 1,554 API tests passed. Both flagship final tasks and certificates are covered. Browser media checks passed for both courses on phone, tablet and desktop; the foundation learner journey through human marking and certificate verification also passed. Build/type/lint checks passed with existing warnings.

The wider portal test command still fails its existing role-route and action-permission-parity checks. No check was weakened. Existing System.Security.Cryptography.Xml advisories are unresolved. These facts prevent a clean production-candidate claim, even though the dev reviewer deployment is testable.

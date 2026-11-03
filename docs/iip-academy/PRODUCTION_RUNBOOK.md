# Flagship course production

## Canonical locations

- Source and course folders: this iip-academy directory in agenticIip-v2.
- Generated media: media/C01_U1-1 through C01_U4-3 and media/C02_U1-1 through C02_U4-3. Large generated media is excluded from Git, not deleted.
- Production lesson scripts and private question banks: flagship-content/foundation and flagship-content/sales.
- The current deployed application source is the sibling iip_saas checkout, not the older agenticIip-v2 application. This was established by matching the live AcademyRecords migration and existing Academy contracts. Do not deploy the older application over it.
- The live course IDs are ai-agentic-insurance and ai-sales-distribution. The sales authoring manifest uses insurance-sales-customer-experience; package_catalog.py maps it to the existing live ID and does not create a duplicate course.

## Rebuild procedure

1. Review the existing teaching sources and the affected narration, questions and practical work. Generated drafts do not constitute expert approval.
2. Run scripts/academy/build_foundation.py and scripts/academy/build_sales.py from the repository root using Python with Pillow and python-pptx installed.
3. Run scripts/academy/produce_courses.py. The generator uses the existing Azure Speech resource and reserves estimated cost before every request, including retries. Its aggregate cap is USD 20. Run only one producer process at a time because the budget lock is process-local.
4. Run scripts/academy/audit_media.py. It checks all expected assets, source/audio fingerprints, durations and question counts where packaged. Review the contact sheets in quality. Rendering already includes a full video decoding check.
5. Package into the existing application's private embedded catalogue with scripts/academy/package_catalog.py and its --catalogue argument. This changes only the two flagship entries and preserves other courses. Run this after any use of the older application content generator.
6. Run backend tests, portal type checks, lint, production build and the Academy browser tests. Record broader pre-existing test failures separately, never disable their guards.
7. Upload only video.mp4, audio.mp3, captions.vtt, slides.pptx and transcript.md for each unit to the private academy-media container, under flagships/v1/UNIT_ID/. Never upload instructor question keys, receipts, tokens or build configuration.
8. Back up the deployed code before replacing it. Package the API for linux-x64. Assemble the Next.js standalone server with static/public assets and flat, pinned production dependencies, excluding environment files. Preserve all existing application settings.
9. Deploy only to the existing dev App Services in subscription 5f5765ae-985f-42e4-a443-8e2447be33ca. Do not create staging infrastructure or apply unrelated migrations. This integration requires no database migration.
10. Verify the actual public catalogue, reviewer identity, own enrolments, lesson mappings, authenticated byte-range media responses and unauthenticated refusals. A successful deployment command is not evidence that new code is serving.

## Azure and access controls

- Narration subscription: 88c8b4ab-3510-4a8d-832c-73bf72088b5a.
- Speech resource: oticgroup-8430-resource in rg-OticGroup-8430, eastus2.
- Voice: en-GB-Ada:DragonHDLatestNeural.
- Application/storage subscription: 5f5765ae-985f-42e4-a443-8e2447be33ca.
- Storage account: stiiph5girbnzrayqe. The Academy media container is private.
- The API uses its existing managed identity. Browser access requires the signed-in learner's actual server-side enrolment. Blob keys and SAS links are not embedded in the portal.
- A temporary container-scoped contributor assignment may be used for publishing with the existing deployment account. Remove that temporary assignment after upload and verification. Never give learners cloud-storage roles.
- Eugene's existing guest directory identity is 6aac969e-d03e-4380-baa5-7e605b24949d. Reviewer enrolment grants course access, not IIP administration or Azure permissions.

## Release gate

Both courses are reviewer editions. paidReleaseApproved remains false, and checkout and self-enrolment are blocked for new learners. An authorised Academy operator can grant reviewer access.

Before paid release, obtain recorded subject-matter and assessment review, legal/source review where needed, subjective audio review and caption alignment review. Replace overly mechanical questions, independently evaluate assessment difficulty and improve any lesson needing depth or worked examples. Module tests currently reuse selected unit-bank questions; they are not independent unseen examination banks.

Videos are short recorded teaching segments, not evidence of a particular total study time. Practical work and final assignments add time that must be measured in a learner pilot. Neither these files nor automated checks prove that an insurer will value the course at UGX 500,000 or USD 120. Certificates are certificates of completion, not accreditation.

The narration ledger records conservative reserved estimates at USD 22 per million SSML characters. It is not an Azure billing statement.

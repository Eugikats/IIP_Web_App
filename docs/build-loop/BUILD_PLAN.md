# Build plan

Status values: TODO, IN PROGRESS, DONE, BLOCKED. Item numbers follow `IIP_BUILD_LOOP_PROMPT.md`. Branch: `build/claims-core` (worktree `../iip-build`), based on audited HEAD `e8b2b42` (see DECISIONS.md D-001).

Baseline before the loop: dotnet build 0 warnings 0 errors; dotnet test 1764 passing (Domain 681, Api 1083).

## 1. Mock providers for local runs

Status: DONE

Acceptance criteria:
- AI (chat, assistant, drafting, embeddings, tool-calling), document processing, retrieval/index, evidence-pack storage, document storage read, and email/SMS sending each have a deterministic Mock adapter.
- Document classification comes from a content marker, else whole-token file name; a fixture always gives the same result.
- A Mock setting is honoured only in a developer runtime; any other profile resolves it to the NotConfigured adapter, and the startup validator refuses Mock in Production, Staging, Demo and ManagedApplicationReviewer, including when ALLOW_PRODUCTION_MOCKS is set.
- Provider health shows `mock` or `not_configured`, never `healthy`/Operational, for a mock or unconfigured provider; the portal labels it "Mock".
- `scripts/run-local-mock.sh` starts the API and portal with all mocks.
- `tests/claims-core.e2e.spec.ts` and `pnpm test:claims-core` exist and pass; a second tenant cannot read the first tenant's mock outbox.

## 2. Fix known defects

Status: DONE

Acceptance criteria:
- `check:action-permission-parity` passes: server is authority; CustomerService no longer holds every permission (admin-level ones removed); the portal mirror's Broker no longer claims ViewAssignedClaims and Broker lands on the new-claim page.
- One role vocabulary: `UserRole` removed, `InsuranceUser` uses `PlatformRole`.
- File-name classification matches whole tokens (`accident_report.pdf` is not a customer ID); one shared classifier used by the Azure adapter, the mock and the test double.
- A document whose processing fails or throws is saved as `Failed` with an error warning and an audit event, never left as Uploaded; the customer sees "Could not be read" with a Try again button; staff get a Retry processing action; both retries reprocess the stored file; staff upload still answers 503 with the capability when the provider is missing.
- One completeness rule: a required slot counts when a file is stored in it (customer's slot, else classifier type) unless failed, rejected, superseded or Unknown; classifier disagreement raises a staff-only warning and `classifierDisagrees` flag and is never shown to the customer as an error.
- Agent registry: comment no longer claims "ten canonical agents"; Kato is ModelBacked.

## 3. Claim types per product (A3)

Status: DONE

Acceptance criteria:
- `ClaimTypeDefinition` (key, name, description, active, order, aliases) lives inside the product version snapshot; every edit is a new version; an old claim keeps the types of the version it was pinned to.
- Admin UI on the product page: add, rename, describe, add aliases, reorder (up/down), switch off, remove; "Import from existing claims" turns the free-text types already used into configured types and links those claims (additive, idempotent).
- Validation: unique keys and names, lower-case slug keys (derived from the name when blank), at most 60 types, at most 10 aliases each.
- A person filling a form (staff or claimant) must pick a configured active type when the product has any; unmatched or retired types are refused with the allowed names; automatic channels (email, WhatsApp) get a suggestion from the description and staff confirm it; with no configured types everything behaves exactly as before.
- Dropdowns: staff new-claim form and claimant new-claim page read `GET /api/v1/claim-types` and `GET /api/v1/client-portal/claim-types` (tenant scoped) and fall back to the built-in list when none are configured; Kato's file_claim takes a `claim_type` from the cover's list.
- `ClaimCase.ClaimTypeKey` stored and returned; seeded demo products carry claim types and seeded claims are linked.
- `ClaimTypeClassifier` is now only the Motor routing fallback; the configured product types and `ClaimTypeMatching.Suggest` are the suggestion path.

## 4. Required documents per claim type (A4)

Status: DONE

Acceptance criteria:
- `ProductDocumentRequirement` gains `ClaimTypeKeys` (empty = every claim type), `Kind` (Document or Evidence), `AllowsMultiplePages`, `MaxFiles`, `AcceptedFormats`; every field is optional so requirements saved before this read exactly as before; a missing kind defaults by document (photos are Evidence).
- Resolution is by product version and claim type: a claim sees the entries that apply to its type; a more specific entry beats a general one for the same document key; a claim with no resolved type sees only the "every type" entries; a product with no matrix keeps the built-in rules; a matrix with nothing for a claim type means nothing is required for it.
- One checklist everywhere: `ProductAwareDocumentChecklistProvider` now lives in Application so the API, the follow-through reminder timer (Functions) and every other caller build the same slots and completeness; slots carry wording, kind, limits and formats.
- Defaults per kind: a document is one file (PDF or image) unless pages are allowed (up to 10); evidence is several photos (up to 10, images only); explicit limits win; a single-file slot is never more than 1.
- Validation: claim type keys must exist, no duplicate entries at the same level, kind and formats must be known, file limit 1 to 30; a claim type still used by a document cannot be removed.
- Customer upload is enforced by the slot: unknown slot, wrong file type, too many files are refused in plain words; a single-file slot is replaced by a new upload, a multi-file slot keeps what is there and adds to it; the customer page shows evidence wording, "n of N sent" and accepts only the slot's formats.
- The customer page and the staff checklist use the tenant's own wording for built-in documents (this also fixes the hard-coded label table, item 6 B2).
- Admin UI: per document, kind, file limit, file types, "Applies to" (every type or chosen types), a claim-type filter showing what a claim of that type is asked for, and a warning when a type has nothing configured.

## 5. Claim fields per claim type (A5)

Status: DONE

Acceptance criteria:
- `ClaimFieldDefinition` (key, label, type Text/Number/Date/Select/Phone/Currency/YesNo, required, options, help text, order, customer-visible or staff-only, optional claim type keys) is part of the product version snapshot; every edit is a new version.
- Admin builder on the product page: add, label, type, choices, help text, required, staff only, "asked for" every or chosen claim types, reorder, remove; keys derived from the label (camelCase) and validated (unique, 2 to 30 choices for a choice, claim types must exist, a claim type still used by a question cannot be removed).
- `ClaimCase.FieldValues` holds the answers; reads go through `ClaimFieldValues.For`, which also reads the original Motor/Medical FNOL records, so no claim is migrated.
- New Motor and Medical products (and seeded demo products) start with built-in definitions whose keys mirror the FNOL records; answers to those keys still fill `MotorFnolDetails`/`MedicalFnolDetails`, so injury detection and routing keep working. Claims on a product with no configured questions behave exactly as before.
- One validator: types parsed and normalised (dates to ISO, amounts and numbers to invariant digits, phone digits, yes/no), unknown keys refused, a customer can only answer customer-visible questions, required answers enforced when a claim is filed from a form (original FNOL answers count as present) and not when saving part of the details.
- One renderer (`DynamicFieldsForm`) is used by the staff new-claim form (when the product has configured questions), the claimant new-claim page and the claim details card; the claim page shows every question with its answer and lets staff correct them (audited as `case.fields.updated`, field keys only, never values).
- APIs: `GET /claim-fields`, `GET /cases/{id}/fields`, `PUT /cases/{id}/field-values`, `GET /client-portal/claim-fields`, `GET /client-portal/claims/{id}/fields`, `PUT /client-portal/claims/{id}/field-values`; `fieldValues` on claim creation and in the claim response; the claims summary prompt lists the answers.

## 6. Product settings and labels (A2, B2)

Status: DONE

Acceptance criteria:
- `TenantSettings` (one row per workspace, additive migration `AddTenantSettings`): require an IRA approval reference to publish, missing-document reminder days, SLA warning lead hours, seat limit, claims allowance. Defaults are exactly the old constants (true, 3, 24, 50, 5000), so a workspace that never visits the page behaves as before.
- The values are read where the constants used to be: `Product.CanPublishWith`, `ProductCommandService`/`ProductQueryService` (the publish check and `canPublish`), `CaseFollowThroughService` (reminder and SLA warning timings), `BillingQueryService` (seat limit) and `RecordBackedBillingUsageSource` (claims meter).
- `GET`/`PUT /api/v1/admin/tenant-settings` (ManageTenantSettings, partial update, validated ranges with a reason, audited as `tenant.settings.updated` with setting names only). Admin page `/app/admin/settings` shows each value with what it usually is; a second person must still approve every publish.
- Customer-facing label wording per document type (B2) was delivered with item 4 (the matrix label and the built-in wording table).

## 7. Policy wording to draft configuration (A6)

Status: DONE

Acceptance criteria:
- On a product page an admin chooses a stored policy wording and presses "Read the wording". The model returns proposed kinds of claim, required documents, questions, and covered and excluded items; each carries a quote of the clause it relies on.
- Every quote is checked against the wording text. A quote found in it shows its clause number and page; a quote that is not found is kept but flagged and cannot be accepted, so an invented clause never reaches a product.
- A person accepts, edits or rejects each item (editing an accepted item returns it to "needs a decision"). A document whose type does not exist cannot be accepted until a real document type is chosen. Nothing is part of the product until "Add accepted items" is pressed.
- Applying goes through the normal product update: a new draft version, validated, versioned and audited, additive only (existing claim types, documents and questions are never changed). Accepted items that duplicate something already there or name a claim type the product does not have are left out with the reason shown. Publishing the new version still needs a second approver.
- `Product.CoverageItems` (covered and not covered, with clause and page) is part of the version snapshot and is shown on the product page.
- Wordings longer than 60,000 characters are read from the start and the screen says so. Provider failures show a plain message, never the provider's error.
- Audit events record ids, counts and the model provider (`product.wording.drafted`, `.item.decided`, `.applied`, `.discarded`), never the wording text. Every route needs ManageProducts and works only in the caller's own workspace.

## 8. Preview as customer (A8)

Status: DONE

Acceptance criteria:
- "Preview as customer" on a product page opens `/app/admin/products/{id}/preview`: the claimant's own "Start your claim" form and document list for a chosen claim type, with a sample policy number and sample text, in a 390px phone frame or a wide frame.
- It works for a draft: the data comes from the product's current version, resolved exactly as a filed claim resolves it (customer-visible questions for the claim type, the document slots for the claim type, built-in rules when the product has nothing configured).
- It saves and sends nothing: the endpoint is a read, the submit button is disabled and the upload controls are locked, with the page saying "Preview only".
- It renders the same components the customer sees (`ClaimantNewClaimForm`, `ClaimantDocumentsCard`), extracted from the claimant pages, so later changes to the customer pages show up in the preview with no second copy to keep in step.
- Needs ManageProducts and only reads the caller's own workspace; an unknown claim type is refused with a reason.

## 9. Privacy notice and consent (H2)

Status: DONE

Acceptance criteria:
- Before the first upload the claimant sees a short privacy notice (what we collect, why, who sees it, how long we keep it, their choices and how to withdraw) on their claim page and must press "I agree"; until they do, every upload control is disabled and the server refuses an upload with `consent_required`.
- The agreement is a `ConsentRecord` (purpose `claim_data_processing`, channel `portal`, basis ExplicitOptIn) naming the notice version and the portal session; it is append-only and tied to the claimant's verified contact, not to a single claim, in the claimant's own workspace.
- The notice text is a workspace setting with a plain default; each change of wording raises the version, so a customer who agreed to an older version is asked again (`NoticeChanged`) and an agreement to a stale version is refused (409).
- Withdrawal is on the claim page ("Withdraw permission", with a plain explanation): it adds a withdrawal row, stops new uploads until the claimant agrees again, and keeps what was already sent (erasure is item 34).
- Staff see "Privacy notice: Agreed / Not yet agreed / Withdrawn / Notice changed" on the claim; the admin settings page edits the notice with "Use the default text" and says the default is a draft for legal to confirm; "Preview as customer" shows the notice card.
- Audit events (`claimant.consent.given`, `claimant.consent.withdrawn`) record the notice version only; never the notice text or the claimant's contact.

## 10. Photo or file per slot (B3)

Status: DONE

Acceptance criteria:
- Every document slot on the customer page offers "Take photo" (a file input with `capture="environment"`, images only, one at a time: it opens the rear camera on a phone and the normal file chooser on a computer) and "Choose file" (or "Choose files" for a multi-file slot, "Replace file" once filled, "Add more files" for more).
- A slot that accepts no image format (for example PDF only) offers no camera. "Something else" offers both.
- Multi-file slots keep their limit: both buttons lock when the slot is full, and the count ("2 of 10 sent") is kept.
- The buttons are 36px tall touch targets that wrap on a 390px screen with no horizontal scroll; they are real labelled controls reachable by keyboard.
- One component (`SlotPicker` inside `ClaimantDocumentsCard`) draws them for the customer page and for "Preview as customer".

## 11. Evidence originals (B6)

Status: DONE

Acceptance criteria:
- A file uploaded by a customer or by staff is stored byte for byte as received. A photo keeps its EXIF (make, model, time taken, location tags); nothing on the server recompresses, resizes or strips it.
- The SHA-256 of the received bytes is stored on the document (`Sha256`) and shown to staff next to the document ("Evidence, kept exactly as sent. SHA-256 ..."). The customer's own response never contains the hash or any storage detail.
- A separate preview is made on the server for pictures (JPEG, PNG, TIFF, WebP and HEIC): upright, at most 1600px, JPEG, with every profile and tag removed (so no location or device details). It is stored as its own object beside the original, served to staff at `.../documents/{id}/image-preview` (View claims permission, own workspace only), and used by the staff viewer for formats a browser cannot show (HEIC, TIFF). A HEIC original stays HEIC.
- A preview that cannot be made (a PDF, a damaged file) never fails the upload.
- The slot kind (Evidence or Document) is recorded on the document. In the browser there is one compressor, `prepareFileForSlot`, which scales down pictures of paperwork only; evidence is never touched. The second compressor and its use on staff uploads were removed.
- Decoding is bounded (maximum size, memory and time) so a hostile image cannot exhaust the server.

## 12. Document photos to PDF (B5)

Status: DONE

Acceptance criteria:
- Pictures sent into a document slot (paperwork, not evidence) become one PDF built on the server, one page per picture, in the order the customer arranged; HEIC, TIFF, PNG and JPEG all work. The PDF is a derived copy; every original stays stored and downloadable exactly as sent.
- A multi-page document shows "Add another page", a list "Page 1: name, Page 2: name" with Up, Down and Remove for each page, and the line "These pages are sent as one PDF of N pages, in this order". "Take photo" adds the next page from the camera.
- Moving or removing a page rebuilds the PDF to match; removing the last page removes the PDF. A removed page's file is kept and marked superseded, never deleted.
- A PDF is rebuilt only when the slot holds nothing but pictures; a slot holding a PDF (or any non-picture), an evidence slot, or a picture that cannot be read gets no combined PDF and the files stay as sent. A failed build never fails the upload.
- Staff see, for each page, "Page n of N in one PDF" with a button that opens the PDF; the staff list API carries the PDF address, page and page count; the claimant API carries the page count only.
- Rearranging must list every page exactly once (otherwise refused); another customer's session cannot move or remove pages; the PDF route needs View claims permission and is limited to the caller's workspace.

## 13. Camera document scanner (B4)

Status: DONE

Acceptance criteria:
- Document slots (not evidence) offer "Scan document" next to "Take photo" and "Choose file". Tapping it opens a full-screen scanner: the rear camera, a Capture button, then the picture with four draggable corner handles placed where the page was found, then the straightened page with Clean, Grey, Black and white and Original, a Turn button, "Adjust corners", "Retake" and "Use this page". The finished page is a JPEG that goes in as the next page of the document, through the PDF flow of item 12.
- The page is found by plain image maths (brightness split, biggest shape, its four extreme corners) and straightened with a projective warp; clean-up stretches contrast, or makes black and white using the brightness around each pixel so a shadow across a page does not become a black patch. When no page can be found it says so and offers the whole frame to drag. No library was added, so nothing is downloaded beyond our own code.
- The scanner code is loaded only when Scan is tapped (a separate chunk, checked by a test that sees no request for it before the tap).
- Automatic fallback: if the device has no camera interface, the camera is refused or fails, the scanner chunk does not load, or the picture cannot be processed, the scanner closes, a plain sentence explains ("We could not use the scanner (...). Use Take photo instead.") and that slot's ordinary camera input is opened.
- Unit tests of the image maths run on synthetic fixtures (find the corners of a page photographed at an angle, straighten it back to the original, dark-on-light and light-on-dark pages, no page found, shadow handling, rotation, size limits).

## 14. Recognise and rename every file (B7)

Status: DONE

Acceptance criteria:
- Every uploaded file, from the customer page, from staff, from WhatsApp and from email, is looked at by a recogniser against the workspace's whole document catalogue (the built-in types with the insurer's own wording, plus every type the insurer added). It returns a type, a confidence and a one-sentence reason. Locally this is a deterministic Mock; with Azure OpenAI configured a vision model reads the picture; with neither configured every file honestly goes to a person.
- The customer's slot always wins. When the recogniser is confident (80% or more) it is something else, the file stays where the customer put it and a staff-only flag and warning are raised; the customer is never shown an error.
- A file with no slot is filed into the claim's matching slot when the recogniser is confident and the slot can take it. Otherwise it is marked "needs a person" with a reason: it could not be told, it reads as something the claim does not ask for, or that single-file document is already in place.
- Every document gets a display name `<LabelWithoutSpaces>_<ClaimRef>[_<n>].<ext>` (for example `PoliceReport_CLM-1A2B3C4D.jpg`, `_2` for the second of a kind, `Unsorted_...` when unplaced). The file as sent keeps its own name in `OriginalFileName`; staff see both.
- A person can put a file into any of the claim's slots (`file-into`, UploadDocument permission); this clears the review flag, renames it, updates completeness and the slot's PDF, and is audited. A person's reclassification settles what the document is and replaces the recogniser's view.
- A recogniser that fails or names a type the workspace does not have never blocks a claim and is never trusted: the file just goes to a person.
- The recogniser's reading is advice only. It never changes the file, and consequential decisions stay with people.

## 15. Extracted data into the claim (C2)

Status: DONE

Acceptance criteria:
- Each document type carries reading rules (`ExtractionFields`: which value read from the document fills which claim question), set per workspace through the document type API and versioned with the type; built-in types have a small default set (the medical report fills patient, treatment date and provider). A type with no rules offers nothing.
- When a document has been processed, the values its extraction found are offered as proposals for the questions the claim's product actually asks, each with its source document (shown by display name), page where the provider gives one, and confidence. A proposal changes nothing on the claim.
- An officer accepts a proposal, optionally typing a correction first, or rejects it. Accepting writes the answer through the normal field save (validated and normalised to the question's type, audited by question key), and records provenance: source Document, the document, page, confidence, who confirmed it, and whether it was corrected. A value that does not fit the question's type is flagged and cannot be accepted until corrected.
- A person-confirmed answer is never overwritten on its own: a proposal for a question that already has a different answer is marked as conflicting and is accepted only with an explicit Replace; a proposal equal to the claim's answer is not made; an officer's earlier accept or reject of the same reading is respected when the document is processed again; a newer reading supersedes an older undecided one.
- Staff see "Suggested from the documents" on the claim and, under each answer, whether it was given by the customer, entered by staff, or read from a document and confirmed by whom. The customer's view never carries any of this.
- Works for documents arriving by the portal, staff upload, WhatsApp and email. The audit trail records question keys and decisions, never the values.

## 16. Authenticity checks (E2)

Status: DONE

Acceptance criteria:
- Every stored picture (JPEG, PNG, HEIC, TIFF, WebP) from the customer, staff, WhatsApp or email is checked from its original bytes: camera details present or missing, when it was taken against the incident date and the date the claim was reported, device make and model, the software that last saved it, whether it was changed long after it was taken, whether its real size matches what the camera recorded, and whether it records a location.
- Each finding has a severity (For information, Low, Medium, High) and a reason in plain words: a photo dated 30 days before the incident is High, editing software or a resize after capture is Medium, no camera details on an evidence photo is Low, a recorded location is information only.
- Duplicates: a 64-bit perceptual hash survives resizing and recompression; a picture that looks like one on another claim in the same workspace is High (with that claim's reference), like another document on the same claim in a different slot is Medium, and the same slot twice is ignored. Plain-colour pictures have no usable hash and never match. Comparison never crosses a workspace.
- The result is advice: it blocks nothing and scores nobody. A person gives a disposition (No concern, Needs follow-up, Concern raised); anything other than No concern needs a note; it is recorded with who and when and audited, and it survives the picture being processed again.
- PDFs and unreadable files get no report. The customer's view never carries any of this. Audit events record the finding codes, not the picture, its details or the person's note.

## 17. Background specialist agents (E3)

Status: DONE

Acceptance criteria:
- An upload returns as soon as the file is safely stored: the customer sees "Being checked" and the claim page checks again every two seconds, so the row turns to "Received" by itself. (Staff uploads, retries and "reprocess" are queued the same way.)
- Document processing and claim review are durable jobs in the database (`BackgroundJobs`), in the job's own workspace, with identifiers only in the payload. A worker (a few loops in the API process) takes due jobs with a lease; a crash simply lets the lease run out and the job is taken again; two workers can never both hold one (optimistic lease token); asking for the same work twice queues it once.
- Per document, in one job: read and classify, name, extract, offer values for the claim's questions, check the picture, rebuild the slot's PDF if it was filed by recognition, refresh the claim's completeness; then a claim-review job is queued (several documents arriving together are reviewed once, after a short settle time) which runs the claims agent's summary. The old in-memory claim triage queue is gone.
- Failures are retried with a back-off (5 s, 30 s, 2 min) while the customer keeps seeing "Being checked"; after the attempts run out the job is dead-lettered with a safe one-sentence reason, the document shows "Could not be read" with the customer's "Try again", and nothing is ever retried silently again. Nothing in a job record is a provider message, a stack trace or content.
- Visibility: Admin, "Background work" shows waiting, running, done and gave-up counts and every job (what, claim reference, attempt n of m, next try, reason), refreshes itself, and an administrator can send a dead-lettered job back (audited). Officers can read a claim's own background work. Everything is limited to the workspace.
- Existing behaviour is preserved by an inline mode (an upload does the work before it returns), used by the fast API tests; the development server and the Playwright run use the background mode.

## 18. Fraud scoring and recommendation (E2)

Status: DONE

Acceptance criteria:
- Each claim has a review score from 0 to 100 and a band (Low, Moderate, Elevated, High), built from reasons a person can read: the fixed fraud rules that apply to the claim, the authenticity findings on its pictures, and what the model noticed while summarising it. Every reason says where it came from (fixed rule, picture check, model reading), how strong it is, what a person has already done with it, and links to its source. Reasons add with diminishing weight, so the total never reaches 100 from a handful of weak ones.
- A person's reading counts: a reason set aside (no concern, could not be substantiated) stops adding to the score; a concern a person raised makes the advice "consider referring" even if the score is low.
- The model alone can never raise the band above Low: only its two strongest notes count, at a small weight each. The low-documents rule is not part of the score (missing documents are advice, not suspicion).
- A recommendation agent turns the same facts into one of four pieces of advice: ready for a decision, ask for more (naming each missing or unreadable document), look at flagged items first, or consider referring. Every reason in it names its source and the claim page links to it. A model adds a short plain explanation only if its wording contains no decision or accusation words; otherwise it is dropped and the score and advice still show.
- It is advice for a person, in a container separate from the person's own response ("I agree" or "I disagree", a reason is required to disagree). The response is audited and belongs to the facts it was given on: when the facts change it no longer applies.
- Prepared automatically as part of the claim review job after the summary; staff can also prepare the explanation on request. Limited to the roles that already see fraud signals; responding needs the permission to override an AI recommendation.

Fraud use-case coverage: [Claim document fraud review use cases](CLAIM_DOCUMENT_FRAUD_FLAG_USE_CASES.md) records the product-by-product evidence and mismatch scenarios that the system should flag for human investigation. The `DONE` status above describes the delivered scoring/recommendation baseline; it does not mean every listed product-specific scenario, external verification connector, or linked-claim pattern has been implemented or tested. Track those cases against code and tests before claiming full fraud-screening coverage.

## 19. Claimant status wording and timeline (B9)

Status: DONE

Acceptance criteria:
- Every claim status has wording written for the customer (a short title and one or two plain sentences), with defaults for all ten statuses: no staff terms such as "Escalated" or "Ready for export", no dashes, no emoji. The defaults are marked on screen as a starting point to be confirmed by the insurer and legal.
- An administrator can replace any status's title or explanation on a new Admin page, "Claim status wording" (shows the staff name, the default and the workspace's own, with "Use the default"). Saving is audited by name, scrubs dashes, limits length (60 and 400 characters) and applies at once. One workspace's wording never reaches another's customers.
- The customer's claim page now leads with that title and explanation, and the timeline is a list that works at phone width: each step says Done, Now or Next, with the current one marked.
- The timeline is drawn from the claim's own product: each stage can carry a customer label, whether the customer sees it, and the claim status at which it is reached. The default process shows six customer steps (internal steps such as the discharge voucher and complaint escalation are not shown); a stage a product adds appears only when marked visible with a milestone. A claim with no product gets a plain five step timeline.
- The WhatsApp assistant tells a customer where their claim stands in the same wording instead of the staff label.

## 20. Officer status change (C6)

Status: DONE

Acceptance criteria:
- Claim statuses used to change from five unrelated places with no single list. There is now one table of every move a claim's status can make and how (filing, assigning, the workflow rules, the Approvals queue, escalating, resolving, and a manual change), and a test proves every status is reachable from a newly filed claim by some defined path.
- The claim page has one "Change status" action. It offers only the moves an officer decides (between Under review, Waiting on documents and Ready for review), and asks for a reason that is kept on the record with the person's name and the time. The two moves that need a supervisor (taking a claim out of escalation, reopening a closed claim) are shown and switched off for anyone without that authority, and refused by the server too.
- The statuses that decide something cannot be chosen from the list: sending a claim to approval, the hand-off for export, escalating, settling and closing keep their own actions and rules (the Approvals queue, Escalate, Resolve). The panel says plainly how each of those is reached instead of hiding them. Approval and resolve rules are unchanged.
- An automated actor can never change a status (it must be a person), and the change is limited to the caller's own workspace.
- The customer's page follows the change at once, and the timeline steps now show the date the claim actually reached them, taken from the recorded status changes.

## 21. Customer notifications on status change (C7)

Status: DONE

Acceptance criteria:
- When a claim's status changes, the customer is sent the workspace's own wording for the new status (from the wording page of item 19), on the channel they reached the insurer by: WhatsApp if the claim came in on WhatsApp, otherwise a text message to the phone on the claim, otherwise email. It applies to every route that changes a status: Change status, Assign, Escalate, Resolve, the approval step, and the workflow rules.
- It is off until an administrator turns it on. When on, each message is written as an ordinary customer update, so it appears in the same queue, follows the same approval rules and carries the same audit trail. With "send without approval" off, every message waits for a person. With it on, only messages the existing policy treats as routine go straight out; anything about a decision, an escalation, or a claim being ready for review always waits for a person.
- The words are always the insurer's own set wording, never text a model wrote. "New" and "Waiting on documents" send nothing extra (the acknowledgement and the missing-documents request already exist).
- WhatsApp is now part of the same send path as email and text. It needs the customer's recorded, unwithdrawn consent. If the customer last wrote within 24 hours the message is plain text; after that WhatsApp only allows a template Meta has approved, so the administrator enters the approved template name per status (the template can use claim_reference and status), and the platform sends that. With no template, or no consent, or WhatsApp not connected, the message is kept with its reason ("ConsentRequired", "TemplateRequired", "ConfigurationRequired") and is never shown as sent.
- Admin, "Claim status wording" now starts with the message settings: the two switches and the WhatsApp template per status.

## 22. Staff Send claim link (B1)

Status: DONE

Acceptance criteria:
- The claim page has a "Send claim link" box. A staff member who may message customers sends the claimant a secure link to that one claim, by the way the claim came in (WhatsApp, text message, or email) or by a channel they choose.
- The link is the same single use link the customer portal already uses, valid for 24 hours. When the customer opens it they get a session for that claim only, and the same link does not work a second time. Only a fingerprint of the link is stored.
- The customer's consent is respected: WhatsApp needs a recorded, unwithdrawn consent (and, once the customer's last message is over 24 hours old, WhatsApp refuses plain messages, so staff are told to choose text or email); a text or an email is never sent after the customer withdrew consent for that channel.
- The screen confirms where it went with the address half hidden and never shows the link. The send is audited with the channel and the outcome but not the link, and the message record that is kept (WhatsApp thread) holds a plain sentence instead of the link. A person must send it; an automated process cannot.
- If it cannot be sent (no contact, withdrawn consent, WhatsApp not connected) the officer gets a plain reason, and the link that was made is switched off straight away.

## 23. Slow connections (B10)

Status: DONE

Acceptance criteria:
- Sending a file now shows how far it has got ("Sending IMG_1.jpg: 43%") with a bar, and says what is happening when the connection is poor: "The connection dropped. Trying again in 4 s (try 3 of 8)" or "You are offline. Sending carries on as soon as you are back online."
- A failed request is retried automatically with a growing wait (1, 2, 4 ... 30 seconds, spread a little so many phones do not retry together), up to eight tries, for lost connections and server trouble. A refusal the customer must act on (not agreed to the privacy notice, wrong kind of file, slot full) is shown straight away and not retried.
- A file bigger than one small request is sent in quarter-megabyte pieces. A dropped connection costs only the piece in flight. If the page is reloaded, or the phone closes the browser, the row says the send was interrupted; choosing the same file again carries on from the last piece that arrived instead of starting again. The server keeps unfinished uploads for 24 hours, then removes them.
- When the last piece arrives the file is put back together and checked against a fingerprint computed on the phone, then goes through exactly the same checks and storage as any other upload: consent, the slot's rules, the untouched original with its own SHA-256, and the reading. Finishing twice cannot store it twice.
- Photos of the damage are still never shrunk or changed; a poor connection is handled by sending them in pieces. Pictures of paperwork are brought down to about 450 KB each by trying smaller and plainer encodings in order, only when they are too wide or too heavy, and never enlarged.
- Proved at phone width on a throttled connection (about 2G speed): a 1.1 MB photo shows progress, survives the signal dropping completely and coming back, and arrives byte for byte identical; and after a reload in the middle, the same file carries on and sends fewer pieces the second time.

## 24. Claims pack (C4)

Status: DONE

Acceptance criteria:
- The claim page has "Claims pack" (for roles that may export evidence: supervisors, compliance and administrators). One press downloads `ClaimsPack_<ClaimRef>.zip`.
- Inside: `00_ClaimSummary_<ClaimRef>.pdf` (a readable summary: the claim, the customer, the policy, the product and kind of claim, the details given with where each came from, the documents asked for and whether each was received, every file in the pack with a fingerprint, and what happened on the claim in order), `01_Documents/` (the customer's paperwork renamed after what it is, pages of one document joined into one PDF), `02_Evidence/` (the photos and other evidence exactly as received, byte for byte), and `manifest.json` (original name, new name, type, SHA-256 and upload time of every file).
- Everything the platform prepared is in the summary but is labelled as advisory and decides nothing: the summary of the claim, the open questions and things the model suggested checking, the review score with its reasons, the recommendation and its explanation, a person's response to it, and the checks on each picture with a person's reading.
- A replaced document is not in the pack. Each download is audited with how many files and bytes it held, never their content. The JSON audit pack export is untouched and still available. Another workspace's claim returns "not found".
- Checked beyond the project's own tests: the summary opens and reads correctly in two independent PDF readers.

## 25. Pack emailed automatically (C5)

Status: DONE

Acceptance criteria:
- Admin has a new page, "Claims pack email": who receives a claim's pack (up to ten email addresses, each checked), and when it is sent by itself: only by hand, when a claim's documents become complete, or when a claim reaches a status chosen from a list. An automatic trigger cannot be saved with nobody to send to.
- On the claim page the "Claims pack" box now also has "Send pack now" and shows the last sends: when, to whom (addresses half hidden), whether it went attached or as a link, and why. Each press sends again.
- A pack up to 3 MB is attached to the email. A larger one is stored and each recipient gets a secure link that works for 7 days and 10 downloads, opened on a plain page that downloads only when the person presses the button (so a mail scanner that opens links does not use it up). An unknown, expired or used up link all get the same plain answer.
- Automatic sends run as durable background jobs: they are retried if the mail connection fails and give up visibly in Admin, Background work with a plain reason. Each automatic reason sends the pack once per claim, so replacing a document on a complete claim does not email it again.
- Every send and every opening of a link is audited (how many, which way, the outcome, never the content or the link). Emails now carry their own subject ("Claims pack CLM-...") and can carry an attachment; that support was added to the mock, SMTP and Graph senders.

## 26. Customer memory (D3)

Status: DONE

Acceptance criteria:
- A returning customer is no longer asked for what they already gave. On their claim page a new card, "What we already have", offers (1) details they gave on an earlier claim (driving permit number, how to contact them and the vehicle registration by default) with a tick box each and "Use these details", and (2) documents they sent before (their ID by default) with "Use the one on file". Every line says which claim it came from. Nothing is filled in or copied until the customer presses a button.
- When the customer starts a new claim from a link made for that, the form is prefilled with the same remembered details, with a line saying so and where from; they can change anything.
- A reused document is copied into the new claim as a fresh file (the earlier claim keeps its own), goes through the same privacy agreement, slot rules, hashing and reading as a file just sent, and staff see on it which claim it was reused from.
- On WhatsApp, Kato now says which papers we already have when it opens a claim ("we already have Your ID (from claim CLM-...)") and points to "Use the one on file" on the secure page. Papers are still never asked for or sent in the chat.
- It is the same customer only by the same policy number, phone number or email, inside one workspace. Another customer in the same workspace, or the same phone number in another workspace, is offered nothing, and a claimant can only ask about their own claim.
- Administrators choose (Admin, Operating settings, "Returning customers") whether memory is on, which details are worth carrying over and which documents may be reused. Photos of the damage and other evidence of what happened are never reused unless an administrator names them.

## 27. Multi-tenant WhatsApp (A9, D1)

Status: DONE

Acceptance criteria:
- Several insurers can now share one deployment's WhatsApp channel. Each connects its own business number from the settings screen; an incoming WhatsApp webhook is routed to the workspace that owns the number it names, and is accepted only if it is signed with that workspace's own Meta app secret. A webhook for a number nobody connected, or signed with another workspace's secret, is refused the same way as a bad signature.
- A number can be connected to one workspace only. Trying to connect a number another workspace already holds is refused with a plain message.
- Replies, typing indicators, voice notes and file downloads each use the credentials of the workspace that owns the number, never another's. The sender used for claim-status messages is the workspace's own number, and refuses to send from any other.
- The health check on the WhatsApp settings screen reports the caller's own workspace.
- A file a customer sends is stored on that workspace's claims only, even when the same phone number is a customer of two insurers. If the caption names exactly one document the claim still needs (for example "police report"), the file goes into that slot; otherwise the document reader files it when it is sure, and a person looks when it is not.
- Kato speaks for the workspace's organisation (Admin, Operating settings, "Kato, the WhatsApp assistant") and sells only the covers in that workspace's published products that carry a description for WhatsApp (set through the product's API). A workspace with nothing configured sells nothing and Kato says a person will follow up; only developer and demonstration runtimes fall back to the built-in demonstration covers.
- Answering a customer on WhatsApp is now a saved background job, not an in-memory queue. A message waiting when the API restarts is still answered, and a batch that keeps failing appears under Admin, Background work as "Answering a customer on WhatsApp". The job holds identifiers only, not what the customer wrote.

## 28. Multi-tenant email (A9, D2)

Status: DONE

Acceptance criteria:
- Mail is now collected for every insurer that connected a mailbox, not only one per deployment. The schedule (every five minutes) visits each workspace with a mailbox switched on, one at a time, each in its own scope. A workspace whose mailbox fails is marked and skipped, and the others carry on.
- An insurer without Microsoft 365 can connect any mail host over IMAP (Admin, Email Mailboxes, "Add mailbox", "Where the mail is"). The host, port, username and password are tried against the real host before anything is saved, so a mailbox that cannot work is never saved as if it did, and the reason is shown in plain words (wrong password, host not reachable, host not on the public internet).
- The connection is always encrypted. The password is stored encrypted and is never shown or returned again; the screen only says a password is saved. A mail host must be on the public internet, so a workspace cannot point the platform at its own internal network.
- A Microsoft 365 mailbox can name the insurer's own Microsoft Entra directory, so each insurer's mail is read through its own directory.
- Mail lands in the workspace's own inbox, and two insurers using the same mail host (or the same address) each keep their own copy. Collecting twice never duplicates a message.
- A mailbox shows when it was last checked and whether it could be reached ("Could not connect" is visible instead of looking like an empty inbox).
- An email becomes a claim exactly as before, by a person pressing "Process" with a reason. Its attachments are stored untouched, read, and filed into the slot they belong to: by what the file is called when that names exactly one slot ("police_report.pdf"), otherwise by the document reader when it is sure, otherwise left for a person.
- Once processed, an IMAP message is marked read at the host (and moved to a folder if the mailbox names one).

## 29. Multi-tenant claimant portal and channels

Status: DONE

Acceptance criteria:
- One deployment can serve many insurers' customers. A customer's link carries the insurer's link name (for example `...&t=acme-insurance`), or the customer opens the insurer's own verified web address, and the platform works out whose portal it is from that. It no longer depends on a setting fixed when the portal was built.
- Nothing a browser sends can choose an insurer on a shared deployment: a link name or web address must be one the platform knows, an unknown one is refused plainly ("This claim link is not valid"), and a deployment dedicated to one insurer still refuses another insurer's link. Development keeps its convenience headers.
- Each insurer sets its customer portal under Admin, Operating settings, "Customer portal": the link name, the name customers know it by, a logo, a button colour (refused if white text would be hard to read), who to call and write to, and optionally its own web address.
- Customers' pages show that insurer: its name and logo in the header, its colour on the main buttons, and a "Need help?" line with its phone and email. Pages for an insurer that has set nothing keep the platform's own look.
- An insurer's own web address is used only after it proves it controls it: the platform shows a text to publish at `/.well-known/iip-portal-verification` on that address and checks it (only public internet addresses, over TLS). Until then customers are sent to the shared portal. Once verified, links use the address and the browser pages on it are allowed to talk to the service (per-insurer origin allowance). Changing the address asks for proof again.
- Links the platform makes for a customer (by text message, email, WhatsApp, a staff "send link" and a shared claims pack) use the insurer's address, or the shared portal with its link name.
- SMS delivery reports: each insurer can make its own secret callback address for its text-message gateway (shown once; only a fingerprint is kept; making a new one stops the old at once). A report sent to one insurer's address can only update that insurer's messages. A deployment dedicated to one insurer can still use its single shared secret.

## 30. Self-serve company sign-up (A1)

Status: DONE

Acceptance criteria:
- A company can open its own workspace without anyone at IIP: a public sign-up page takes the company name, the administrator's name, work email and a password, and sends a confirmation link. Nothing is created until the address is proved; then the workspace, its administrator and three starter products (Motor, Medical, Property, as drafts to review) appear, and the company's customer portal gets a link name made from its name.
- Companies without Microsoft 365 sign in with email and password, next to Microsoft sign-in. Passwords need at least 12 characters, may not be a very common password and may not contain the person's email or company name. Five wrong tries pause the account for 15 minutes. A wrong email, a wrong password and a paused account all get the same plain answer, so the screen never says whether an email has an account.
- Forgotten passwords are reset from an emailed link that works once and expires. Signed-in people can change their own password by giving the current one.
- An administrator invites colleagues by email under Admin, Users and roles, choosing the role they will have. The colleague opens the link, chooses a name and password (or, if they already have a sign-in, confirms it) and joins. The link works once, can be withdrawn, and counts against the workspace's seat limit while open. Invitations and sign-ups are audited.
- Nothing secret is kept readable: passwords are stored only as a salted one-way fingerprint, and email links are stored only as fingerprints. Sign-up and sign-in endpoints are rate limited, and the sign-up form has a hidden field that only software fills in.
- Sign-up can be turned off for a deployment; then the page says so and points to sign-in. A deployment that cannot send email says "Not configured" instead of pretending the link was sent.
- The terms and privacy page is a draft and says plainly that its wording is to be confirmed by legal.

## 31. Token metering (E4)

Status: DONE

Acceptance criteria:
- Every call to an AI model is now counted for the insurer that caused it: summaries and drafts, assessments, policy wording extraction, the WhatsApp assistant's steps, search embeddings, customer-update drafting and the policy assistant's answers. Each call records how many tokens went in and out, which model answered, and an estimated cost. No prompt and no answer text is kept for this; only counts.
- The Billing page now shows this month's usage for the workspace: claims, documents received, AI calls, AI tokens, estimated AI cost in US dollars, and document storage in MB. Usage is by calendar month (UTC). Nothing is shared between insurers.
- The cost comes from the price set for the model actually in use (a deployment setting, per model), not from numbers fixed in the product. A model with no price set shows "no price is set for the AI model in use" instead of a guess; a mock model in a demonstration shows US$ 0.00.
- Counting never stops real work: if recording a call fails, the person still gets their summary, and the gap is written to the service log.
- Usage records do not appear in the lists people read (the assistant's activity, a claim's AI history); they are used only to total usage.

## 32. Plans, limits and payments (G)

Status: DONE

Acceptance criteria:
- Every workspace can be on a plan: Free, Agent (about US$ 20 a month), Team or Enterprise, priced in both UGX and US dollars and shown with their monthly allowances for claims, documents, AI use, storage and people. A company that signs up for itself starts on Free. Workspaces that existed before plans keep exactly the limits they have today ("Standard"); nothing about them changes.
- The allowances are enforced. When a month's claims are used up the next claim is refused with a plain message and an upgrade suggestion (and the WhatsApp assistant tells the customer a person will follow up, never why). AI use stops before a model is paid for once the month's AI allowance is used. People are limited by the plan's seats. Documents and storage warn but never turn a customer away.
- The Billing page shows the plan and its status, upgrade prompts at 80% and 100% of any allowance, this month's usage against the plan, the plans on offer, and invoices with receipts.
- A plan is bought with MTN Mobile Money, Airtel Money or a card. Mobile money asks the payer to approve on their phone; a card goes to the gateway's own page, so card details never reach IIP. The plan starts only when the operator confirms the payment, and for the amount asked. A failed payment is explained in plain words, nothing is charged, and the plan is unchanged.
- Each paid month is one payment. Mobile money cannot be charged on its own, so about five days before the paid month ends the administrator is sent an invoice and a reminder. If it is not paid the plan carries on for a week, then the workspace falls back to the Free limits. Nothing is ever deleted for non-payment, and paying restores the plan. A plan can be set not to renew.
- A receipt is emailed when a payment is received, and can be viewed and printed from the Billing page.
- Payments that are not set up show "Not configured" instead of a button that cannot work. In a developer runtime a pretend operator stands in for all three methods.

## 33. Academy for real learners (F)

Status: DONE

Acceptance criteria:
- IIP Academy now has a public learner area at `/learn`, outside the staff app. Anyone can see the four courses with their prices in UGX and US dollars. A learner signs in with a link emailed to them (no password), which works once and expires in an hour.
- A learner joins a course by paying (MTN Mobile Money, Airtel Money or a card, through the same payment service as plan billing) or with a single-use access code that an insurer issued. The course opens only when the payment is confirmed or the code is accepted. Wrong codes are refused with one plain answer, and five wrong tries pause code entry for 15 minutes.
- Progress is kept on the server. Each step is marked done as the learner moves on; the check questions are marked by the server (the right answers never leave it) and the learner sees which are right and why. The old browser-only record is replaced for these learners.
- On passing, the learner chooses the name for the certificate and gets a numbered certificate (IIPA-XXXXX-XXXXX) as a PDF that carries a link to check it. A public page checks any number and shows only the name, the course and the date. The certificate says it is a certificate of completion, not an accredited qualification.
- An insurer (Admin, Academy access codes) issues codes for a course, sees each code's last four characters, status and who used it, withdraws unused ones, and marks the written work of learners who joined with its codes: "Satisfactory" or "Try again" with feedback that the learner sees. The codes are shown once and cannot be recovered. One insurer never sees another's codes or learners' work.
- A course can have a video per step: set its address and it appears on that step. Written work can be sent only when someone is set up to mark it; otherwise the page says the exercise is for the learner's own practice.

## 34. Retention, deletion and erasure (H1)

Status: DONE

Acceptance criteria:
- An administrator can now set how long each kind of record is kept (Admin, Data protection): closed and settled claims, claim documents with their stored files, messages about claims, and WhatsApp conversations. After the period a daily pass deletes them for good, files included. Nothing is deleted unless a period is set; a period of 0 keeps the record.
- A claim can be put on legal hold with a reason. While it is held, no retention period and no erasure request touches the claim, its documents or its files. The hold is released by an administrator, and both steps are audited.
- One person's data can be erased on request: enter their phone number, email or policy number, see exactly what would go (and which claims on hold would be left alone), give a reason and type ERASE. Their documents, stored files, messages, chats, consent records, customer portal links and profile are deleted. The claim stays, with its status, dates, amounts and policy number but without the person's name, contact details, description or answers, because an insurer must keep a record of its claims.
- Every deletion leaves a record that cannot be used to find the person again: when, who or which rule, why, and how many of each kind, never the data or the phone or email asked about.
- If the file store cannot be reached, the claim is not half-erased: its records stay as the list of what is left and the request can simply be repeated.
- Storage and database encryption at rest are now stated explicitly in the deployment templates, with an option to use the customer's own key (customer-managed key) for both, and the length of time a deleted file stays recoverable inside storage is now a setting that defaults to 7 days.

## 35. Scale

Status: DONE

Acceptance criteria:
- The everyday lookups no longer read every record in a workspace. Finding a claim by policy number, by phone number or email, by status or by team, finding a customer by contact, and finding a policy by number or by customer now use indexes. This covers a customer asking for a login link, the daily reminder pass, opening a new claim (choosing its owner), the WhatsApp and email assistants recognising who is writing and which claim they mean, a customer's remembered details, and finding a person's claims for erasure.
- The same phone number written differently (+256 700 123 456, 0700123456, 256700123456) is still recognised as the same, and an email is matched ignoring case, exactly as before.
- Records saved before this change are still found. A short job fills in their lookup details after start-up; until it has, those few records are read the slow way in addition, so nothing is missed during or after an upgrade.
- Nothing about what a customer or officer sees changes. The change is speed at volume: each of these used to take longer the more claims a workspace had.

## 36. Design direction (DESIGN.md)

Status: DONE

Acceptance criteria:
- The staff portal and the new screens follow the design direction: one accent colour, hairline borders instead of shadows, the icon rail and a left-aligned content column. The sign-in page now uses the same building blocks as the rest of the portal.
- Billing, the Academy admin page, the data protection page and the invitations card tell a person plainly when they do not have permission, instead of showing an empty or broken panel.
- On a phone the sidebar now starts as the narrow icon rail, so the page has room. Before this, a phone showed a wide sidebar and squeezed or hid the page. Tapping a link in the rail now works the first time.
- Small text meets the contrast the design asks for, including the workspace chip and the review reminder. After "More actions" on a claim, keyboard focus returns to that button.
- The claim page no longer goes blank when one of its cards gets an unexpected reply; that card shows its own error instead.
- The older visual tests that described screens which have since been redesigned were brought up to date on purpose (listed in D-043), and every visual test now passes.

## 37. Final pass

Status: DONE

Acceptance criteria:
- Every check and every Playwright suite was run on the finished build and passes: build with no warnings, 1178 + 1738 backend tests, lint, the portal tests and checks, the end-to-end journey (110 tests), the runtime-recovery and deployed-profile checks, and the full visual suite on desktop, tablet and phone widths (150 passed, 51 skipped by their own conditions).
- A new journey covers two companies at once: both sign up, both file a claim for the same phone and policy number, each sees only its own, neither can open the other's claim, usage counts only its own, erasing that phone in one company leaves the other's claim and audit trail untouched.
- Two small defects were found and fixed on the way: the Billing page now says "no permission" instead of a generic error when the person lacks access, and the claim page's "not found" state is titled "Claim unavailable".

## 38. Move to the organisation repository and push (LAST)

Status: BLOCKED

Added by the owner on 2026-10-11, to run after everything else including `SUMMARY.md`. Add the organisation's new empty repository as a remote (keep `origin`), push `build/claims-core` to it, nothing else: no other branch, no force, no merge, no pull request. The repository address was not supplied with the request; if it cannot be found, or the push is refused, mark BLOCKED with the exact command for the owner. Record the outcome in `SUMMARY.md`.

The original push completed on 2026-10-12. Follow-up commits from the 2026-11-02 verification and security cleanup are locally committed, but the latest push was refused because the stored GitHub credential is invalid. `gh` is not installed and SSH authentication is unavailable. After GitHub authentication is restored, run `git push org build/claims-core` from `../iip-build`. No force push or merge is needed; the remote branch is at `f901ab2` and local `build/claims-core` is ahead by three commits.

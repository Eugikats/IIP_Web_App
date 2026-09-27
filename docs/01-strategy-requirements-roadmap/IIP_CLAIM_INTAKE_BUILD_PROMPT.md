# IIP claim intake build prompt

One continuous build pass, written to be run with `/loop` (no interval, so the agent self-paces).
Point the loop at this file.

Companion design: "The Roadside Claim" (the four gaps, concept screens and evidence). Where that
page and this file disagree, this file wins: it was written after a second pass through the code.

---

## 1. How to run this

- Run with `/loop` and no interval.
- Work on a new branch, `feature/claim-intake`, cut from `feature/local-language-replies` (which
  holds the live translation work and has not been merged to `main`).
- The agent does not stop to ask between outcomes. It stops when every acceptance criterion is met
  and verified on the Azure dev environment, or when it hits a genuine blocker: a missing
  credential, missing live access, or a decision only a person can make.
- The order below is a suggested order, not a gate. Nothing here is a phase.

---

## 2. Mission

A first-time claimant, on a small phone, between customers at a Rolex stand or a boda stage, can:

1. fill in a claim form that asks only what the insurer needs for **this** product, never what the
   system already knows about them;
2. send each required document **as that document**, see what they have already sent, and replace
   a wrong one themselves;
3. leave halfway and come back without losing anything.

And the insurer can:

4. decide, per product, what the claim form asks and which documents it needs, without a developer;
5. see on the claim page what each document says, and use an approved value in the claim record
   with the document cited.

## 3. What is already true, so it is not rebuilt

Checked in the code on 2026-09-27. Build on these; do not replace them.

- The portal's new-claim form never asks a signed-in customer for name, phone, email or policy
  number (`ClaimAccessNewClaim.tsx`).
- Every uploaded document is stored, classified and has fields extracted on arrival
  (`ClientPortalEndpoints.UploadDocuments`, WhatsApp and email attachment processors).
- Staff can already review extraction on `DocumentExtraction.tsx`: preview, correct a field with a
  recorded reason, reclassify, approve. Corrections feed `DocumentFieldCorrection`,
  `DocumentLabelledExample` and `DocumentClassificationAccuracy`.
- The completeness checklist already reads the product's document matrix when the claim is pinned
  to a product version (`StaticDocumentChecklistProvider`, `DocumentChecklistRules.ForProduct`).
- The WhatsApp assistant already lists a product's configured documents when it opens a claim, and
  WhatsApp captions are already captured on inbound media.

## 4. What is actually wrong

1. **A misspelt document key is silently ignored.** Document types are the fixed `DocumentType`
   enum (eight values). The admin document matrix field (`ProductDetail.tsx:277`) is free text.
   `DocumentChecklistRules.ForProduct` skips any key that does not parse, so that requirement is
   never enforced and nothing says so.
2. **Classification is mostly guessing from the file name.** `AzureDocumentIntelligenceProvider`
   maps a few prebuilt model types, then falls back to keywords in the file name. A phone photo
   called `IMG_2034.jpg` has no keywords, so it lands as `Unknown` or `PhotoEvidence`, and the
   required item it was meant to satisfy stays missing. The customer choosing the type is the
   primary signal, not a convenience.
3. **One upload button for everything.** The portal and WhatsApp take any file with no stated
   purpose, and the customer never sees a list of what they actually sent.
4. **The claim form is two hardcoded shapes.** `Product` has stages and a document matrix, no form.
   The portal hardcodes Motor and Medical only.
5. **Approved extraction never reaches the claim.** `ApproveExtraction` only recalculates the
   completeness score. The claim page shows one link to the first document's review.
6. **iPhone photos are likely refused.** Accepted types are pdf, png, jpg, jpeg, tif, tiff and txt.
   An iPhone camera saves HEIC, and the portal's file input has no `accept` attribute that would
   make Safari convert it. Verify on a real iPhone or with a HEIC sample before claiming either way.

## 5. Read first, and follow

`AGENTS.md`, `CLAUDE.md`, `DESIGN.md` and the four UI documents it indexes, the 27 point checklist
in `docs/03-design-architecture/iip_ui_craft_guardrails.md`, the Product configuration code in
`src/InsuranceIntelligence.Domain/Products/`, and
`docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` before any deploy,
including its "Known gotchas" section. Where a document and the code disagree, the code wins.

## 6. Non-negotiables

1. AI prepares, people decide. Nothing here approves, declines, settles or pays. Using an extracted
   value in the claim record is an officer's action, audited, never automatic.
2. Evidence is append-only. A customer "removing" or "replacing" a document marks it superseded; the
   file and its history stay, and staff still see it. Nothing a customer sends is ever deleted by
   this build.
3. Existing data keeps working. Documents are stored as JSON with the type as a string name. Every
   document, product and claim that exists today must read and behave exactly as before.
4. English customers and existing products see no regression. A product with no configured form
   still gets today's Motor or Medical form.
5. Fields the system already holds (name, phone, email, policy number, product) can never be
   configured as form questions, and are never asked of a signed-in customer.
6. Tenant isolation, permission checks and the six states on every new surface: loading, empty,
   empty after filter, error, not configured, no permission.
7. Product configuration changes are versioned, need a second person to publish, and a claim keeps
   the form and document list of the version it was filed under, exactly as stages do today.
8. Plain words for customers. Every label a customer sees comes from a customer-facing label an
   administrator wrote, never from an internal key.
9. No emoji. No em dashes anywhere, including code, comments, commit messages and documentation.
10. Migrations are generated with `dotnet ef migrations add`, never written by hand.
11. Smallest coherent forward change. No competing architecture, no unrelated cleanup.

---

## 7. Outcomes and acceptance criteria

### A. A document type catalog the insurer owns

**Design decision, made here so the loop does not relitigate it:** keep the `DocumentType` enum.
27 files depend on it and it is what the classifier returns. Add a catalog on top of it instead of
replacing it.

- `DocumentTypeDefinition`, tenant-scoped and versioned like templates: `Key`, `CustomerLabel`,
  `Description` (why it is needed, in plain words), `BuiltInType` (a `DocumentType` value, or null for
  a type the insurer added), `Active`.
- The eight built-in types are the default catalog for every tenant, with keys equal to the enum
  names. Stored documents already hold those names, so existing data needs no migration.
- An administrator can add a type the classifier does not know (Death certificate, Hospital
  admission form, Burial permit). The admin screen says plainly that it will not be recognised
  automatically: it relies on the customer choosing it, or staff reclassifying.
- `CaseDocument` gains `DocumentTypeKey` (string). When absent on old records it is read as the
  enum name. Completeness and every checklist match on the key.
- The admin document matrix key becomes a dropdown over the catalog. Saving a product with a key
  that is not in the catalog is refused with a message, and `ForProduct` no longer skips unknown keys
  silently: it reports them.
- An admin view (or a banner on the product screen) lists any existing product whose document
  matrix holds a key not in the catalog, so the silent drops that already happened become visible.
  Do not rewrite those products automatically.

Accepted when: tests prove an old document with only the enum still satisfies its requirement, a
custom type can be required and satisfied, an unknown key is refused on save, and the existing
products report lists a deliberately misspelt key.

### B. The customer sends each document as that document

Portal (`ClaimAccessStatus.tsx`):

- One row per required and optional document from the claim's pinned product version, using the
  catalog's customer label and description, each with its own **Upload** action. A final row,
  "Something else", for anything that does not fit.
- The upload sends the chosen `documentTypeKey`. The server stores it as the customer's selection,
  still runs classification, and stores the classifier's guess beside it. The customer's choice
  decides which requirement is satisfied. A disagreement between the two is flagged for staff, never
  shown to the customer as an error.
- Under each row: what was sent for it (file name, time, and "Received" or "Being checked"), with
  **Replace** and **Remove**. Both mark the earlier file superseded (non-negotiable 2).
- The file input sets `accept="image/*,application/pdf"` so phones offer the camera and Safari
  converts HEIC to JPEG. Add `.heic` and `.heif` to the accepted types server side as a backstop,
  storing them as received. Do not promise extraction on them unless it is verified to work.
- Before upload, photos wider than about 1600 pixels are scaled down in the browser to a JPEG. A
  customer on a paid bundle should not send 8 MB per photo. Keep the original quality high enough to
  read a document; verify a scaled photo still extracts on a real sample.

WhatsApp (`CustomerAssistantService`):

- When a claim opens, list the needed documents as a numbered list instead of one sentence.
- A photo whose caption is a number from that list, or matches a customer label, is stored against
  that requirement. A number sent as its own message just before a photo counts for that photo.
  Otherwise the classifier decides, as today.
- The acknowledgement says what the photo was counted as and what is still missing: "Got it, that
  is your police abstract. Still needed: 3. Driving permit." This is an English fixed reply and goes
  through the existing translation path unchanged; numbers survive translation.

Staff: the claim page and the extraction review show the customer's choice, the classifier's guess,
and a clear marker when they differ.

Learning: a customer's choice is a candidate label only. It becomes a `DocumentLabelledExample` when
staff approve that document's extraction without reclassifying it. Never train on unreviewed
customer choices.

Accepted when: tests cover choosing a type on upload, a custom type satisfying its requirement,
replace and remove keeping the old file, a caption number and a preceding number on WhatsApp, the
disagreement flag, and promotion to a labelled example only after staff approval.

### C. The insurer configures the claim form per product

- `Product` gains `IntakeFields`: ordered `ClaimIntakeField` entries with `Key`, `Label`, `HelpText`,
  `Type` (ShortText, LongText, Number, Money, Date, YesNo, Choice), `Choices` (for Choice),
  `Required`. Versioned and published exactly like `DocumentMatrix`.
- Validation refuses reserved keys (name, phone, email, policy number, product), duplicate keys,
  empty labels, and a Choice with fewer than two options. A product cannot publish with an invalid
  form.
- Default forms for the classes in `DefaultClaimStageLibrary`: Motor mirrors today's hardcoded
  fields (so nothing changes for Motor customers), Medical adds hospital, admission date and reason,
  and Life, Personal accident and Property get short, sensible defaults. Mark the defaults as
  starting points an insurer is expected to edit.
- `ClaimCase` gains `IntakeAnswers`: the key, the label as it was at that product version, and the
  value. The existing `MotorFnolDetails` stays as it is; Motor answers are also written there so
  every existing reader keeps working.
- Admin: a form builder on the product screen, next to the document matrix, with a preview of what
  the customer will see.
- Portal: `ClaimAccessNewClaim.tsx` renders the pinned product version's form. On a phone it is
  short steps of one to three questions with a progress line, not one long page. With no configured
  form it falls back to today's Motor or Medical form.
- Answers are saved in the browser as the customer goes, per claim draft, and restored if the link
  is reopened, so an interruption does not lose work. Clear the draft on submit. Wrap every storage
  call so a phone with storage blocked still works.
- Staff: the claim page shows the answers under the configured labels.
- WhatsApp does not ask the form's questions in this pass. After it opens a claim, its reply
  includes the portal link with "finish your claim form here" when the product has required
  questions the conversation did not cover.

Accepted when: tests cover validation, versioning and pinning (a claim filed under version 2 keeps
version 2's questions after version 3 is published), the Motor fallback, reserved-key refusal, and
the answers reaching the claim; and the stepped form is opened in a real browser on the dev portal at
400px and 768px.

### D. Approved extraction reaches the claim

- On the claim page, a compact summary for each processed document: its type (customer's choice and
  classifier's guess when they differ), its key extracted values, whether a person has approved
  them, and a link to the existing Extraction Review screen for that document. Not only the first.
- After a document's extraction is approved, an officer can use a value for a mapped claim field:
  Money values can become the claim's `ClaimedAmount`; Date values can become `IncidentDate` or a
  Date intake answer; other values can fill an intake answer of the same type. One action per value,
  shown as "Use UGX 1,240,000 as the claimed amount".
- Using a value records who did it, the document it came from, and the value it replaced, as an
  audit event and as a citation on the claim field. It never happens automatically.
- Where the claimed amount a customer typed differs from an approved invoice or estimate total, the
  claim page says so plainly. Check whether `DocumentFieldValidationRuleDefaults` already flags this
  and reuse it rather than adding a second rule.

Accepted when: tests prove an unapproved extraction cannot be used, using a value writes the claim
field with its citation and audit event, the replaced value is kept in the audit, and the mismatch is
shown.

### E. Documentation and honesty

Update the blueprint, the agent registry if the assistant's reply changed, the Roadside Claim page's
source (republish it if anything it says is no longer true), and the stakeholder overview's status
table (leave that file uncommitted, as it is today). Record what was not verified: HEIC on a real
iPhone, extraction quality on scaled phone photos, classifier behaviour on insurer-added types.

---

## 8. Verification

Run these and report the real results:

```
dotnet build InsuranceIntelligence.sln
dotnet test InsuranceIntelligence.sln
cd web/portal && pnpm lint && pnpm build
```

The claimant portal does not need Entra sign-in, and the Demo profile shows the access link on
screen, so the portal screens can and must be verified in a real browser on dev: request a link for a
demo policyholder with no claim (see the demo seed policyholders endpoint in the deployment
document), open it, fill the stepped form at 400px and 768px, upload a document into a named slot,
replace it, and read the claim back. Staff screens need an interactive Entra sign-in; if the loop
cannot sign in, say so and verify them through their API responses instead.

## 9. Commit and deploy

Commit and push after each coherent outcome on `feature/claim-intake`, with plain-language messages.
Never bundle unrelated work.

Deploy as `AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` describes, and apply its recorded lessons:

- check free space on `C:` before building; it has been at 0 bytes;
- after `az webapp deploy`, run `az webapp restart` and confirm with a request only the new code can
  answer, because a deploy has reported success while the old code kept running;
- the API takes 4 to 8 minutes to warm after a restart; wait for three consecutive good responses
  from `/api/v1/me` before testing;
- apply migrations through `POST /api/v1/admin/db-migrate` and compare the count with the source.

Do not change dev app settings to test a failure. Prove failure paths with tests.

## 10. When something is blocked

Build against the existing interfaces, prove the behaviour with tests, mark the item code complete
with live verification blocked and the reason, and move on. Never fake a success path, disable a
guard, or weaken validation to make a check pass.

## 11. Progress log

The loop appends a dated line here after each outcome: what changed, what was verified, what is
next.

- **2026-09-27 - Outcome A code complete, verified locally.** Added `DocumentTypeDefinition`
  (tenant-scoped, versioned like `CommunicationTemplate`) and `IDocumentTypeCatalogRepository`,
  with `EfDocumentTypeCatalogRepository` and the `AddDocumentTypeDefinitions` migration (generated
  via `dotnet ef migrations add`, confirmed visible to `dotnet ef migrations list`). `CaseDocument`
  gained the optional `DocumentTypeKey`. `MissingDocumentRequirement` now carries a string `Key`
  instead of a `DocumentType`, and `DocumentChecklistRules.ForProduct` routes a matrix key that
  does not match a built-in enum name into `CustomRequired`/`CustomOptional` instead of silently
  dropping it (the bug this outcome exists to close); `CompletenessScore` and
  `DocumentCompletenessChecklist` gained matching custom-key overloads/fields.
  `ProductCommandService.ValidateDocumentMatrix` now refuses to save a document matrix entry whose
  key is neither a built-in `DocumentType` name nor an active row in the tenant's catalog.
  Added `DocumentTypeCatalogEndpoints` (list, list versions, save, seed-defaults, and
  `unresolved-product-keys`, the admin report of existing products whose matrix already holds a key
  the catalog does not recognise) at `/api/v1/document-types/*`, permission-gated by
  `ManageProducts`. Portal: `ProductDetail.tsx`'s document matrix key is now a dropdown over the
  tenant's active catalog (auto-seeded on first visit via `ensureDocumentTypeCatalog`), with an
  inline warning on any row whose key is not recognised; `Products.tsx` shows a list-level banner
  linking to every product the unresolved-keys report flags. Verified: `dotnet build
  InsuranceIntelligence.sln` (0 errors), `dotnet test InsuranceIntelligence.sln` (1570/1570
  passing, including new repository round-trip, `ProductCommandService` refusal, and checklist
  custom-key tests satisfying this outcome's four acceptance tests), `pnpm lint` (clean), `pnpm
  build` (clean, all 48 routes). Not yet done: committed to git but not yet pushed or deployed to
  Azure dev in this pass. Next: outcome B (document-by-document upload).

- **2026-09-27 - Outcome B partially complete: customer-portal document-by-document upload is
  code complete and verified locally; WhatsApp, the staff-facing disagreement view, and the
  learning-gate check are still open.** `CaseDocument` gained `IsSuperseded`/`SupersededAt`; a
  customer "removing" or "replacing" a document now marks the earlier row superseded rather than
  deleting anything (non-negotiable 2), and `StaticDocumentChecklistProvider` ignores a superseded
  document as if it had never been sent. `DocumentUploadValidation` now accepts `.heic`/`.heif` as
  a backstop (extraction is not promised on them). `ClientPortalEndpoints.UploadDocuments` reads an
  optional `documentTypeKey` form field, the customer's own choice; the server still runs
  classification independently and stores its guess alongside. A new
  `POST /api/v1/client-portal/claims/{caseId}/documents/remove` endpoint supersedes the active
  document for a key with no replacement upload. `ClientPortalEndpoints.ToResponse` now returns one
  row per required and optional document (built-in and, via `checklist.CustomRequired`/
  `CustomOptional`, any custom type the tenant's catalog carries a `Reason` for), each with its
  label, description, uploaded state, status ("Received"/"Being checked"/"Not uploaded"), the
  active file's name and upload time, and a `disagreesWithClassifier` flag computed server side
  (never shown to the customer as an error, only carried in the API for a future staff surface).
  `CustomDocumentRequirement` gained a `Description` field (from the product matrix entry's
  `Reason`) to support this. `DocumentResponse` (the staff-facing document contract) now also
  carries `CustomerDocumentTypeKey`/`IsSuperseded`, though no staff screen reads them yet. Portal:
  `ClaimAccessStatus.tsx`'s single generic upload button is replaced with one row per document
  (required and optional), each with its own Upload/Replace/Remove, a "Something else" catch-all
  row for anything that does not fit, `accept="image/*,application/pdf"` on every file input, and
  browser-side JPEG downscaling (`lib/photoDownscale.ts`) for a photo wider than 1600px before
  upload. `lib/clientPortal.ts` gained `removeClaimDocument` and an optional `documentTypeKey` on
  `uploadClaimDocuments`. Verified: `dotnet build`/`dotnet test` (1574/1574 passing, including four
  new tests: choosing a type on upload with a deliberate classifier disagreement, replace keeping
  the old file as history, remove marking superseded without deleting, remove on nothing sent
  returning 404), `pnpm lint` (clean), `pnpm build` (clean, all 48 routes). Not yet run: opening the
  stepped upload flow in a real browser at 400px/768px (this outcome's own acceptance list does not
  require it the way outcome C explicitly does, but it is still owed before calling this outcome
  done). Not started: the WhatsApp numbered-list/caption-matching/acknowledgement changes to
  `CustomerAssistantService`, the staff claim-page and extraction-review display of the customer's
  choice versus the classifier's guess, and confirming the `DocumentLabelledExample` promotion path
  still only fires on staff approval (a read-through of `DocumentEndpoints.cs` during this pass
  found all three creation sites are already staff-gated and none reads `DocumentTypeKey`, so this
  is believed already satisfied, but it is not yet asserted by a test naming it explicitly). Next:
  finish outcome B with the WhatsApp and staff-view pieces, then its own accepted-when tests, before
  moving to outcome C.

## 12. Reporting and stopping

Each report says which outcomes are complete and verified live, which are code complete but blocked
and why, what is left, and what happens next. Keep it short. Stop when every acceptance criterion is
met and verified live, or a blocker genuinely needs a person, and say which.

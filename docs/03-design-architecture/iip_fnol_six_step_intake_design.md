# FNOL Six-Step Intake — Design Definition

**Status:** Proposed design, not yet implemented
**Date:** 4 September 2026
**Decides:** Decision A from the claims workflow gap analysis ("what are the six steps?")

---

## 1. Where these steps come from

The six steps below are not invented. They are derived from three sources that already
constrain the answer:

**a. The fields the system already captures.** `CreateCaseRequest` carries claimant
identity, policy number, product line, claim type, channel, incident date and
description. `MotorFnolDetails` adds ten motor-specific fields; `MedicalFnolDetails`
adds nine medical ones. The step boundaries follow the natural seams in that data.

**b. A hard technical dependency.** `DocumentChecklistRules.Resolve` needs
`productLine`, `claimType` and — for motor — the injury indicator before it can
generate the required-document list. **Documents therefore cannot be collected until
those are known.** This single constraint fixes the position of the document step and
rules out any ordering that asks for uploads first.

**c. The reference platform's own FNOL sequence.** Bevaya's FNOL/FROI agent is
published as: *ingest notice of loss, deduplicate, check available policy context,
create a claim, assign it, and prepare a brief.* Our steps map onto that sequence
rather than diverging from it, because the sequence is sound.

---

## 2. The six steps

### Step 1 — Identify the claimant

Who is reporting, and how do we reach them.

| Field | Source | Required |
|---|---|---|
| Customer name | `CreateCaseRequest.CustomerName` | Yes |
| Phone | `CreateCaseRequest.CustomerPhone` | One of phone/email |
| Email | `CreateCaseRequest.CustomerEmail` | One of phone/email |
| Channel | `CreateCaseRequest.Channel` | Auto-set, not asked |

Contact details are mandatory rather than cosmetic: they are what the magic-link
authentication binds to, and what `BelongsToCustomer` matches on when the claimant
later opens the portal. A claim created without a reachable contact cannot be
progressed by the customer.

**On the customer-facing portal**, this step is pre-filled from the magic link and
shown read-only for confirmation — the claimant should not retype identity we already
hold.

---

### Step 2 — Find the policy

Turn a policy number into real, validated context before anything else is asked.

| Field | Source | Required |
|---|---|---|
| Policy number | `CreateCaseRequest.PolicyNumber` | Yes |
| Product line | `CreateCaseRequest.ProductLine` | Derived from policy, confirmable |

This step should do three things beyond capturing a number:

1. **Look the policy up** and show the policyholder, product line and cover status, so
   the user sees they have the right policy before continuing.
2. **Check for duplicates** — an existing open claim on the same policy for a nearby
   incident date is the single most common intake error, and it is cheaper to catch
   here than at review. This is Bevaya's "deduplicate" step.
3. **Derive the product line** from the policy rather than asking the user to choose
   it, falling back to a selector only when lookup fails.

Deriving product line here is what makes step 4 able to branch without asking the user
what kind of claim form they want — a question no claimant should be asked.

---

### Step 3 — Describe the incident

What happened, in the claimant's own words plus the facts every product line needs.

| Field | Source | Required |
|---|---|---|
| Incident date | `CreateCaseRequest.IncidentDate` | Yes |
| Incident description | `CreateCaseRequest.IncidentDescription` | Yes |
| Claim type | `CreateCaseRequest.ClaimType` | Yes |

Claim type belongs here rather than later because `ClaimTypeClassifier` and
`ClaimRoutingPolicy` both consume it, and because it is the second input the document
checklist needs. For motor, the free-text description is also what the classifier reads
to detect injury or fatality language — so the description field must be substantial,
not a one-line box.

---

### Step 4 — Product-line specifics

The only step whose shape changes. This is where a composite insurer's book is actually
served, and where the reference platform's US property-and-casualty taxonomy does not
fit a Ugandan one.

**Motor** — `MotorFnolDetails`: vehicle registration, accident time, accident location,
driver name, driving permit number, third-party involved, injury indicator, police
report status.

**Medical** — `MedicalFnolDetails`: patient name, relationship to policyholder,
treatment date, provider name, admission type, treating doctor, diagnosis summary.

Two fields in the motor set are not merely descriptive and should be visually separated
as **routing indicators**, because they change what happens next rather than just
recording facts:

- **Injury indicator** — drives escalation in `ClaimRoutingPolicy` and changes the
  required documents.
- **Police report status** — determines whether a police abstract is required.

Life, General and Agriculture have no structured detail record yet. Until they do, this
step falls back to the incident description alone, and the checklist falls back to the
product-line default. That is honest behaviour, not a gap to hide.

---

### Step 5 — Upload documents

The checklist is generated from steps 2, 3 and 4 and shown as a live requirement list.

Nothing here is guesswork: `DocumentChecklistRules.Resolve(productLine, claimType,
motorFnolDetails)` already returns the exact required set, and the existing
`GET /api/v1/document-checklist/preview` endpoint already serves it before a claim
exists. The step shows each required document, its status, and what is still missing.

**Two constraints apply on the customer-facing path:**

- Sensitive identity documents (national ID, passport, driving permit) must be
  collected **in the portal, never over WhatsApp**, per Meta's WhatsApp Business
  policy. The WhatsApp journey ends at a deep link into this step.
- Uploads should be permitted to be incomplete. A claimant who cannot photograph a
  police abstract at 9pm should still be able to submit; the claim opens with a
  documented gap rather than being blocked. Completeness scoring already models this.

---

### Step 6 — Review and submit

A read-back of everything captured, then submission.

On submit the system already does the remaining work automatically: creates the claim,
classifies it, applies routing and SLA, assigns a team, writes the audit event, and —
once the claims-summary agent runs — prepares the brief a reviewer sees. That covers
Bevaya's "create, assign, prepare a brief" in one action.

The review screen should show the completeness score and any missing documents plainly,
so the claimant or officer submits with an accurate picture rather than a false sense of
being finished.

---

## 3. Why this ordering and not another

| Constraint | Consequence |
|---|---|
| Checklist needs product line + claim type + injury indicator | Documents must be step 5, after 2/3/4 |
| Product line is derivable from the policy | Policy lookup must precede the branching step |
| Magic link binds to phone/email | Identity must be step 1, and is pre-filled on the customer path |
| Injury language is read from free text | Description must be substantial and captured before routing |
| Completeness tolerates gaps | Submission must not be blocked on missing documents |

The ordering is therefore not a style choice. Four of the six positions are forced by
dependencies that already exist in the code.

---

## 4. What this does not decide

- **Life, General and Agriculture detail records.** Step 4 has no structured schema for
  three of five product lines. Adding them is separate work.
- **Duplicate-claim detection.** Step 2 calls for it; no such check exists today. It
  needs its own task.
- **Policy lookup on the customer path.** A claimant knows their policy number; whether
  they may see policyholder details before authenticating needs a privacy decision.

---

## 5. Implementation note

The backend already accepts everything above in a single `POST /api/v1/cases` call, and
the checklist preview endpoint already exists. **This is predominantly a front-end
restructuring of `web/portal/lovable-pages/FNOL.tsx` into six validated steps**, plus
two genuinely new pieces of backend work: policy lookup and duplicate detection in
step 2.

That makes it a smaller task than a six-step form usually implies — the data path is
built; the guidance around it is not.

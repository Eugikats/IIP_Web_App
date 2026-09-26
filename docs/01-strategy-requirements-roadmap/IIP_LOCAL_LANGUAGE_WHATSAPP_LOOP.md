# IIP local-language WhatsApp: loop instructions

The scoped build for `/loop`. It narrows `IIP_LOCAL_LANGUAGE_BUILD_PLAN.md` to what puts Luganda and
Kiswahili on the live WhatsApp claim flow, and records the one place it deliberately departs from
that plan. Read the plan for the model background; follow this file for what to do.

Work on `feature/local-language-replies`. Do not stop until the behaviour is deployed to the Azure
dev environment and verified there (section 7), or a genuine blocker needs a person.

---

## 1. Mission

A customer writes to the insurer's WhatsApp in Luganda, Kiswahili or English. IIP understands the
message, runs the existing claim process unchanged, and replies in the customer's language. Staff
see the English next to what the customer actually wrote.

Translation goes around the claim process, never into it: translate the customer's message to
English at the door, run the existing English flow, translate the reply on the way out. Every gate,
consent check, identity check and classification runs on the English text.

## 2. Decisions made for this loop

- **The engine that goes live is the existing Azure OpenAI deployment**, through a new
  `AzureOpenAiTranslationProvider` behind the existing `ITranslationProvider`. Reasons: it needs no
  new billable Azure resource, customer text already flows to that deployment for classification
  and grounded answers, and the AfriSLM container is unproven and needs a person's yes to provision.
  AfriSLM stays a later swap behind the same interface. Do not build the container here.
- **Fixed replies are templates, and templates carry a review status.** A Luganda or Kiswahili
  template written by an agent is `Draft` until a native speaker marks it `Reviewed`. Customers
  receive a translated fixed reply only if it is `Reviewed`, or if the workspace setting
  `AllowUnreviewedTranslations` is on. Otherwise they get the English template. That setting is
  seeded on for the dev workspace only, defaults off everywhere else, and every staff view that
  shows such a message labels it "Draft translation".
- **The customer's original words are kept.** The claim record stores the customer's own text next
  to the English description, marked as translated.

## 3. Read first, and follow

`AGENTS.md`, `CLAUDE.md`, `DESIGN.md` and the four UI documents it indexes, the 27 point checklist in
`docs/03-design-architecture/iip_ui_craft_guardrails.md`, `IIP_LOCAL_LANGUAGE_BUILD_PLAN.md`,
`IIP_HACKATHON_READINESS_BLUEPRINT.md`, and
`docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` before any deploy.
Where a document and the code disagree, the code wins.

## 4. Non-negotiables

1. AI prepares, people decide. Translation never changes what the assistant may say or do.
2. Every gate, consent check, identity check and classification runs on the English text.
3. Fixed replies never pass through the translation model. They come from templates.
4. A machine translation reaches a customer only after it passes `TranslationQualityJudge`. On
   failure the customer gets the English original, marked as such. Never an unvalidated candidate.
5. An inbound message that cannot be translated is escalated to a person with its original text.
   Never guess an intent from a failed translation.
6. Policy numbers, amounts, dates, phone numbers and claim references survive translation unchanged.
7. English customers must see no change. Every existing assistant test passes unmodified.
8. Tenant isolation and permission checks stay server side. Never weaken a guard to pass a check.
9. Six states on every new surface: loading, empty, empty after filter, error, not configured, no
   permission. No engine configured renders Not configured, never a fake success.
10. No emoji. No em dashes anywhere, including code, comments, commit messages and documentation.
11. No fabricated data, dead controls or placeholder names. `MockTranslationProvider` stays refused
    in every deployed profile; verify how the dev environment's profile treats it and do not rely
    on it there.
12. The audit event records language and translation status, never the message body.
13. Smallest coherent forward change. No competing architecture, no unrelated cleanup.

## 5. Outcomes and acceptance criteria

### 0. Bring the branch up to date with `main`

The branch is 4 commits behind. The conversational claim assistant (`45e4d1d`) exists only on
`main`. Merge `main` into the branch, resolve conflicts (only `Program.cs` is known to overlap), and
run the full build and test suite before anything else.

Accepted when: the merge is committed, the solution builds, and the whole test suite passes.

### 1. The webhook answers Meta immediately

Today `ReceiveWebhook` awaits `HandleInboundMessagesAsync` inside the request, so a slow assistant
can outlast Meta's patience, and the request's cancellation token then cancels the work after the
inbound message is already saved, leaving the customer unanswered.

- Save the inbound message, enqueue assistant handling on a background queue, return 200.
- The background work must not use the request's cancellation token.
- Idempotency stays on the provider message id: a redelivery is neither answered twice nor lost.
- A failure in background handling is logged, audited, and escalated to a person. It is never
  silently swallowed, and never leaves a customer with no route to a human.

Accepted when: tests show the webhook returns before assistant work completes, a redelivered message
is answered once, a cancelled request does not cancel the work, and a handler exception produces an
escalation.

### 2. Inbound messages are understood in any supported language

In `HandleTextMessageAsync`, after the kill switch and consent and before classification: resolve the
language with `ICustomerLanguageResolutionService`, translate to English with the protected-token
masker, and run everything else on the English. Keep the customer's original in the stored turn.
Media messages are unchanged.

Accepted when, by test: a Luganda new-claim message and a Kiswahili status question reach
`NewClaim` and `ExistingClaimStatus`; an English message follows the identical path as before; a
failed inbound translation reaches escalation with the original text; a policy number inside a
translated message is extracted correctly; the conversation continuation (answering the assistant's
own question) still works when the answer is in Luganda.

### 3. Fixed replies are templates

The assistant hardcodes about 25 customer-facing messages (24 `AssistantOutcome.Of` calls plus
`Welcome`, `HoldingReply`, `DefaultEscalationMessage` and the document-received replies). Move every
one into `CommunicationTemplate` rows keyed by a stable message key, with variables for first name,
reference, product, policy number, document list, link and status lines.

- Seed English so English output is byte-identical to today's.
- Author `lg` and `sw` versions, marked `Draft`. Document type names ("police abstract", "driving
  permit") need a small per-language glossary so the document list reads correctly.
- Enforce the review rule in section 2: `Reviewed`, or the workspace setting on, else English.
- A missing template falls back to English, never to a machine translation.

Accepted when: no customer-facing sentence remains hardcoded in `CustomerAssistantService`, every
existing assistant test passes unmodified, tests cover the review rule and the English fallback, and
a template with an unresolved variable is never sent.

### 4. Model-written answers are translated on the way out

For `GeneralQuestion` answers: the auto-reply gate runs on the English, then the answer is
translated, validated and given a short "machine translated" note in the customer's language. Failure
sends the English, marked as such. The gate-declined queue shows English and translation side by
side so an approver sees what the customer will read.

Accepted when: tests prove the gate still blocks on English content, fixed replies never call the
provider, and a failed outbound translation sends English with the right status.

### 5. Every turn records both languages, and the claim keeps the customer's words

- `CustomerConversationTurn` gains `CustomerLanguage`, `InboundEnglishText`, `OutboundEnglishText`
  and `TranslationStatus` (NotNeeded, Translated, FellBackToEnglish, HumanTemplate, DraftTemplate).
- A claim opened from a translated message stores the customer's original text next to the English
  incident description, marked as translated, and the claim page shows both.
- `SendReplyAsync` passes the real language code instead of the hardcoded `"en"`.
- Generate the migration with `dotnet ef migrations add`; never hand-write it.

Accepted when: tests prove both texts persist, the audit event carries no body text, and the claim
page shows the original words for a translated claim.

### 6. A real engine, honestly reported

- `AzureOpenAiTranslationProvider` in the API project, using the existing deployment, with the
  model-card style prompt, temperature 0, a token budget scaled to the input, and a system
  instruction to output only the translation.
- Provider selection through `INSURANCEINTELLIGENCE_TRANSLATION_PROVIDER` (`Mock`, `AzureOpenAi`,
  and a reserved `AfriSlm`), refused-Mock rule intact, unknown values fail closed to English.
- A health signal: Not configured, healthy, or failing, shown on the staff surface.

Accepted when: tests cover provider selection and the fail-closed path, and the dev deployment
answers a real translation.

### 7. Staff can see and control it

- Conversation views: the original message, the English, a language pill, and a "Machine
  translated", "Sent in English" or "Draft translation" marker per turn.
- Admin > Agents > Customer assistant: enabled languages, engine status, the
  `AllowUnreviewedTranslations` setting, and the fallback behaviour in plain words.
- A way to mark a template `Reviewed` (admin only, audited).

Accepted when: six states on each surface, the 27 point checklist cleared, and every changed screen
opened in a real browser at 1280px and 768px.

### 8. A review sheet with real output

Produce `docs/04-testing-security-quality/local-language-review-sheet.md`: 30 realistic customer
messages and 30 real assistant replies per language, with the live engine's output and an empty
column for a native speaker's score (correct, understandable, wrong). Record scoring as pending.

### 9. Documentation and honesty

Update the blueprint, the agent registry and `IIP_LOCAL_LANGUAGE_BUILD_PLAN.md` (record the engine
decision in section 2 above). Update the Languages section of
`iip_consolidated_stakeholder_overview.md` to match the code, but leave that file uncommitted: it is
untracked and was not asked to be committed. State what is not covered: Lugisu, Ateso, Runyankole
and Acholi, staff-drafted messages, WhatsApp template messages outside the 24-hour window, SMS,
email, the claimant portal and the staff screens. Never claim a language or quality level that was
not verified.

## 6. Verification, run it and report real results

```
dotnet build InsuranceIntelligence.sln
dotnet test InsuranceIntelligence.sln
cd web/portal && pnpm lint && pnpm build
```

New tests for every behaviour above. Open every changed screen in a real browser at 1280px and 768px.

## 7. Deploy and confirm it is live

Commit and push after each coherent outcome on `feature/local-language-replies`, with plain-language
messages, never bundling unrelated work. Read the deployment document, confirm resources with `az`,
then deploy the API and the portal to the dev environment, applying the migration through the
documented separate step. Expect the portal container to take three to five minutes to start and the
API to stall for several minutes after a restart while SQL connections recover.

Do not change dev app settings just to test a failure. Prove failure paths with tests.

Then verify by using the deployed system, not by inferring:

1. Send correctly signed WhatsApp webhook payloads to the deployed API from a phone number that
   matches a demo policyholder: a Luganda new-claim message, a Kiswahili status question, and an
   English message as the regression check.
2. Read the stored turns and claim: original text, English, language, translation status.
3. Confirm the fixed reply came from a template and is labelled a draft translation, that a grounded
   answer carries the machine-translated note, and that the English path is unchanged.
4. Confirm the webhook returned before the assistant finished.
5. Open the conversation and admin screens signed in and read them.

If the dev WhatsApp connection is a real Meta number, also state exactly what the person must send
from a handset and where to read the result, and report the outbound message's stored body and
delivery state. Do not block on it. Report what you saw.

## 8. When something is blocked

If a live provider is not connected or a credential is missing, build against the existing provider
interfaces, prove the behaviour with tests and the sandbox path, mark the item code complete with
live verification blocked and the reason, and move on. Never fake a success path, disable a guard,
or weaken validation to make a check pass.

## 9. Progress log

The loop appends a dated line here after each outcome: what changed, what was verified, what is
next. Keep it short.

- (none yet)

## 10. Reporting and stopping

Each report says which outcomes are complete and verified live, which are code complete but blocked
and why, what is left, and what happens next. Keep it short. Stop when every acceptance criterion is
met and verified live, or a blocker genuinely needs a person, and say which.

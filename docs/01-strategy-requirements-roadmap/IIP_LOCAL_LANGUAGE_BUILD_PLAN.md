# IIP local-language build prompt

One continuous build pass, written to be run with `/loop` (no interval, so the agent self-paces).
Paste the whole of section 2 onwards as the loop prompt, or point the loop at this file.

Decisions settled 2026-09-25: launch languages are Luganda (`lg`) and Kiswahili (`sw`); the
translation engine is the free, self-hosted AfriSLM model; native-speaker review is owned by the
product team.

---

## 1. How to run this

- Run with `/loop` and no interval.
- Work on a new branch, `feature/local-language-replies`, cut from `main`.
- The agent does not stop to ask between outcomes. It stops when every acceptance criterion is met
  and verified on the Azure dev environment, or when it hits a genuine blocker: a missing
  credential, missing live access, or a decision only a person can make.
- One exception: creating a new billable Azure resource (the translation container, its
  environment, the model blob container) needs a person's yes once, before the first
  provisioning. Ask, then carry on with the rest of the build while waiting.
- The order below is a suggested order, not a gate. Nothing here is a phase.

---

## 2. Mission

A customer writes to the insurer on WhatsApp in Luganda, Kiswahili or English. The assistant
understands the message, answers from the same grounded, gated pipeline it uses today, and replies
in the customer's language. Staff always see the English next to the original.

Translation is a transform on either side of the existing assistant, not a second assistant and
not an agent with authority of its own. The human-decision boundary, the five-condition auto-reply
gate, consent enforcement and the kill switch apply unchanged, and they are always evaluated on the
English text.

## 3. Read first, and follow

`AGENTS.md`, `CLAUDE.md`, `DESIGN.md` and the four UI documents it indexes, the 27 point checklist
in `docs/03-design-architecture/iip_ui_craft_guardrails.md`,
`docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md`, and
`docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` before any deploy.

Then read the current assistant in full before changing it:
`src/InsuranceIntelligence.Application/CustomerAssistant/CustomerAssistantService.cs`,
`src/InsuranceIntelligence.Domain/CustomerAssistant/` (classifier, auto-reply gate, models),
`src/InsuranceIntelligence.Api/Integrations/WhatsAppIntegrationEndpoints.cs`, and the
`CommunicationTemplate` model, which already carries a `Language` field.

## 4. Non-negotiables, in every change

1. AI prepares, people decide. Translation never changes what the assistant is allowed to say.
2. Every gate, consent check and identity check runs on the English text, before localisation.
3. Fixed replies (claim acknowledgement, "please share your policy number", lead confirmation,
   escalation message) never pass through the translation model. They use human-written
   translations stored as `CommunicationTemplate` rows.
4. A model translation is shown to a customer only after it passes validation. On failure the
   customer gets the English original, marked as such. Never an unvalidated candidate.
5. An inbound message that cannot be translated is escalated to a human with its original text.
   Never guess an intent from a failed translation.
6. English is stored as the record for every turn, next to what the customer actually sent and
   received.
7. Policy numbers, amounts, dates and phone numbers are protected from translation.
8. Tenant isolation and permission checks stay server side. Never weaken a guard to pass a check.
9. Six states on every surface: loading, empty, empty after filter, error, not configured, no
   permission. No engine connected renders Not configured, never a fake success.
10. No emoji. No em dashes anywhere, including code, comments, commit messages and documentation.
11. No fabricated data, dead controls or placeholder names. Test doubles live in
    `InsuranceIntelligence.LocalTestDoubles` and are never registered in Staging or Production.
12. The model weights are never committed. `languages/` and `*.gguf` are git-ignored; the model
    lives in Blob Storage.
13. Smallest coherent forward change. No competing architecture, no unrelated cleanup.

## 5. The model

The git-ignored `languages/` folder holds `afrislm-0.8b-q4_k_m.gguf` (641 MB, TranslatePsy-AfriSLM,
a 0.8B Qwen fine-tune, 4-bit) and four Dart files from a Flutter app. The Dart files cannot run
here; they are the design reference to port. It is not the Sunbird model.

It covers Luganda and Kiswahili. It does not cover Lugisu, Ateso, Runyankole or Acholi, and it has
no language identification of its own.

It does not fail loudly; it invents. Documented failure modes: echoes the instruction, prefixes
commentary ("Here is the translation"), returns the input unchanged, loops a phrase until the token
budget, emits wrong scripts, still reads as English.

What to port into .NET:

| Reference | Port as | Notes |
|-----------|---------|-------|
| Model-card prompt | Adapter constant | System: "You are a professional {from} to {to} translator. ... Produce only the {to} translation, without any additional explanations or commentary." User: "Please translate the following {from} text into {to}: {text}\n\nTranslation:" |
| `judgeTranslation`, `cleanTranslationOutput`, `hasRepetitionLoop` | `TranslationQualityJudge` (Domain, pure) | Rejections: empty, unchanged, echoed prompt, repetition loop, length blowup (more than 6x words + 20), still English, garbled script. |
| One retry, then fallback | `TranslationService` | Strict English check on the first attempt only. |
| `splitTranslationUnits` | Sentence splitter | Translate per sentence past 72 characters; keep successful pieces. |
| Token budget `words*3+32`, clamp 64..512 | Adapter | No streaming; the model loops. |
| Cache key `sha256(modelTag|direction|lang|normalised text)` | `ITranslationCache` | Model tag includes file name and size, so a new quant never reuses old rows. |
| `looksLikeEnglish` | Language detection | Deliberately hard to trigger on African-language input. |
| `TranslationOutcome(text, translated, fromCache, failure)` | Record in Domain | A passthrough can never be mistaken for a translation. |

## 6. Outcomes and acceptance criteria

### A. The translation core exists and refuses bad output

- Domain: `SupportedLanguage` catalog (`en`, `lg`, `sw`, per-tenant enablement, English always on),
  `TranslationOutcome`, `TranslationQualityJudge`, `LanguageResolution` (code, source, confidence).
- Application: `ITranslationProvider`, `ILanguageDetector`, `ITranslationCache`, and
  `TranslationService` (clean, validate, retry once, split into sentences, fall back).
- LocalTestDoubles: a deterministic fake provider for Development and Demo, clearly labelled.
- Accepted when: tests cover every rejection kind, prove the fallback never returns an unvalidated
  candidate, prove the cache key includes the model tag, and prove protected tokens (policy numbers,
  amounts, dates) survive a round trip unchanged.

### B. The assistant knows which language a customer speaks

Resolution order for each inbound message:

1. The contact's remembered language (new per-tenant `CustomerLanguagePreference`, keyed by
   WhatsApp id, on `JsonRepositoryBase`), if set in the last 90 days.
2. An explicit request in the message ("reply in Luganda", "Kiswahili tafadhali").
3. A word-list scorer for English, Luganda and Kiswahili combined with `looksLikeEnglish`.
4. If still uncertain, reply in English with "Reply 1 for English, 2 for Luganda, 3 for Kiswahili",
   and save the answer as the preference.

A detected language overrides a remembered one only with high confidence on a message longer than a
few words. "ok", "hello" or a bare policy number never flips a customer's language.

- Accepted when: tests cover each step of the order, the short-message rule, the choice prompt and
  its answer being saved, and tenant isolation of preferences.

### C. A Luganda or Kiswahili message reaches the right handler

- `CustomerAssistantService` resolves the language, translates the inbound text to English, and
  runs the existing classifier and handlers on the English.
- An inbound translation failure escalates to a human with the original text.
- Accepted when: a Luganda claim message and a Kiswahili status question, through the fake
  provider, reach `NewClaim` and `ExistingClaimStatus`, and a failed translation reaches the
  escalation path, all by test.

### D. The reply goes back in the customer's language, honestly

- Fixed replies come from human-written `lg` and `sw` `CommunicationTemplate` rows, seeded per
  tenant. Where a translation is missing, the English template is sent.
- Grounded answers are translated after the auto-reply gate passes, validated, and carry a short
  "machine translated" note in the customer's language. On failure the English is sent.
- `SendReplyAsync` passes the real language code instead of the hardcoded `"en"`.
- Accepted when: tests prove the gate still blocks on English content, fixed replies never call the
  translation provider, and a failed outbound translation sends English with the right status.

### E. Every turn records what was said in both languages

- `CustomerConversationTurn` gains `CustomerLanguage`, `InboundEnglishText`, `OutboundEnglishText`
  and `TranslationStatus` (NotNeeded, Translated, FellBackToEnglish, HumanTemplate). The migration
  is generated with `dotnet ef migrations add`, never written by hand.
- The audit event records the language and the translation status, never the message body.
- Accepted when: tests prove both texts are persisted and the audit event carries no body text.

### F. The webhook stays fast

- Translation adds seconds per message. Check whether the WhatsApp webhook handles the assistant
  synchronously. If it does, move assistant handling onto a background queue so the webhook
  acknowledges Meta promptly, keeping idempotency on the provider message id.
- Accepted when: a test or measurement shows the webhook returns before translation completes, and
  a redelivered message is not answered twice.

### G. AfriSLM runs in its own container

- A `llama.cpp` server container on Azure Container Apps, one replica, roughly 2 vCPU and 4 GB,
  reachable from the API only. Never on the API's App Service plan.
- The model is uploaded once to Blob Storage and pulled at container start with a SHA-256 check;
  a mismatch fails the start rather than serving a wrong model.
- `AfriSlmTranslationProvider` in Infrastructure calls the container with the model-card prompt,
  the token budget and no streaming. The endpoint is platform configuration, provisioned in Bicep
  next to the existing dev resources.
- A health check reports Not configured, healthy or failing.
- Accepted when: the container answers on dev, the API's health check shows it healthy, and the
  measured median and 95th percentile latency for a typical assistant reply are recorded in this
  file.

### H. A review sheet for the team's native speakers

- Produce `docs/04-testing-security-quality/local-language-review-sheet.md`: 30 realistic customer
  messages and 30 real assistant replies per language, each with the model's output from the live
  container and an empty column for the reviewer's score (correct, understandable, wrong).
- The same set becomes an automated regression suite against the fake provider for pipeline
  behaviour.
- Accepted when: the sheet exists with real model output. Scoring is done by the product team and
  does not block this build; record it as pending.

### I. Staff can see and control it

- Admin > Agents > Customer assistant: enabled languages, translation engine status, and the
  fallback behaviour in plain words.
- Conversation views: the original message, the English, a language pill, and a
  "Machine translated" or "Sent in English" marker per turn.
- The queue for gate-declined replies shows the English and the translation side by side, so the
  person approving sees what the customer will read.
- Accepted when: six states on every new surface, the 27 point checklist cleared, and each screen
  opened in a real browser at 1280px and 768px.

### J. Documentation and honesty

- Update the blueprint and the agent architecture document with what was built, including what is
  not covered (Lugisu and other languages, staff-drafted messages, WhatsApp templates outside the
  24-hour window, SMS and email).
- A customer writing in an unsupported language gets English with a short note that their
  language is not yet supported, and is escalated if they cannot continue.
- Accepted when: documents match the code, and nothing claims a language, channel or quality level
  that was not verified.

## 7. Verification

Run these and report the real results:

```
dotnet build InsuranceIntelligence.sln
dotnet test InsuranceIntelligence.sln
cd web/portal && pnpm lint && pnpm build
```

Open every changed screen in a real browser at 1280px and 768px and look at it before calling it
done.

## 8. Commit and deploy

Commit and push after each coherent outcome on `feature/local-language-replies`, with messages that
say what changed and why in plain language. Never bundle unrelated work.

Read the deployment document and confirm resources with `az` before deploying. Deploy the API, the
portal and the translation container to the dev environment, and apply the migration through the
documented route. Expect the API to stall for several minutes after a restart while SQL
connections recover.

Then verify by using it, not by inferring it:

1. Send a Luganda claim message and a Kiswahili status question through the webhook sandbox path
   and read the stored turns: original, English, language, status.
2. Confirm a fixed reply used the human template and a grounded answer carries the machine
   translated note.
3. Stop the container and confirm replies fall back to English and the portal shows the engine as
   failing.
4. Open the admin and conversation screens signed in.

Report what you saw.

## 9. When something is blocked

If WhatsApp is not connected live, prove the behaviour with tests and the sandbox path, mark the
item code complete with live verification blocked and the reason, and move on. Do not fake a
success path, disable a guard, or weaken validation to make a check pass.

## 10. Reporting and stopping

Each time you report: which outcomes are complete and verified live, which are code complete but
blocked and why, what is left, and what you are doing next. Keep it short.

Stop when every acceptance criterion is met and verified live, or when a blocker genuinely needs a
person. Say which it is.

---

## 11. Status, 2026-09-27

Built on `feature/local-language-replies` and deployed to `dev`. See `IIP_LOCAL_LANGUAGE_WHATSAPP_LOOP.md`
for the narrower loop that did the work and its dated progress log.

| Outcome | State | Notes |
|---|---|---|
| A. Translation core | Built | Judge, masker, splitter, service, cache. Two defects found by testing against the real model and fixed: the provider setting was never read from configuration, and invisible placeholder characters were stripped by the model (a policy number could vanish). The service now refuses any translation that drops a protected value. |
| B. Language resolution | Built | Word lists extended so a realistic sentence scores above the threshold, and kept clear of words English also uses. |
| C. Inbound handled in any language | Built | Translated at the door. A failure escalates to a person with the original words. |
| D. Reply in the customer's language | **Different from the plan** | Fixed replies are **not** human-written templates in this pass. Where `Translation:AllowUnreviewedTranslations` is on (dev only) every reply is machine translated, labelled as such, and falls back to English on any failure. Human-reviewed overrides are the follow-up, seeded from the review sheet. English output is byte-identical to before. |
| E. Turns record both languages | Built | Stored in the turn's JSON payload, so no schema change was needed. The claim keeps the customer's own words. |
| F. Webhook stays fast | Built | The message is saved, queued in-process, and Meta gets its 200 at once. A queued message is lost if the API restarts in that window and stays visible in the Inbox. |
| G. Model in its own container | Built | Azure Container Apps, file share, key in Key Vault, image pinned by digest. Recorded in `AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md` and `infra/bicep/afrislm-translation.bicep`. |
| H. Review sheet | Built, unscored | `docs/04-testing-security-quality/local-language-review-sheet.md`, 120 rows of real engine output. |
| I. Staff visibility | Partly built | Channels inbox shows the language and what was understood. Admin shows whether the engine is reachable. Not built: marking a template reviewed (there are no templates yet). |

### What the model does well and badly (measured, not reviewed)

Customer to English is accurate in both languages. English to Kiswahili is good. English to Luganda
makes mistakes with insurance vocabulary ("claim on your policy" became "case on your plot", "claims
officer" became "debt trader"), so Luganda replies keep those three terms in English. Speed on a 6-core
laptop CPU was 1 to 5 seconds per message.

### Not verified

- A real WhatsApp message from a handset through to a translated reply. Everything up to the webhook
  is verified live (the deployed API reaches the hosted model and translates), and the assistant is
  tested end to end with the real model locally, but the last hop needs a handset.
- The new staff screens have not been opened in a browser (they need an interactive Entra sign-in).
- No translation has been reviewed by a native speaker. Do not claim Luganda or Kiswahili quality.

### Deliberately not decided by the loop

The model runs always on, in one container, on a small CPU: enough for a pilot, not for volume.
Moving to a hosted translation service (Azure Translator, Sunbird) is a change to one class behind
`ITranslationProvider`, and worth comparing against this model using the review sheet.

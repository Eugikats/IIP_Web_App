# IIP over WhatsApp, text and voice, for boda boda riders

Technical design and feasibility study for servicing insurance entirely through WhatsApp text and voice notes in Ugandan languages, using Sunbird AI for speech and translation, built on the IIP platform as it exists today.

- Researched: 29 September 2026
- Sources: Sunbird docs and model cards, Meta developer docs, Data Protection and Privacy Act 2019, IIP source code
- Companion document: [IIP_KATO_FIELD_TEST_AUDIT.md](IIP_KATO_FIELD_TEST_AUDIT.md)

Status markers used below:

- **[Verified]** read at the primary source
- **[Secondary]** secondary source only, confirm before citing externally
- **[Unverified]** could not be verified

---

## Verdict

Every technical piece exists and is documented. WhatsApp can receive a voice note, we can download it, Sunbird can transcribe it in Luganda, Acholi, Lugbara, Ateso, Runyankole and English, and we can reply with a true voice message.

Three things decide whether it works for riders:

1. **Transcription accuracy.** Sunbird's own published word error rates for these languages run from about 14% to 32% on clean benchmark audio. Street speech with engine noise and Luganda-English code-switching will be worse. The mandatory read-back step (section 11) is what makes this acceptable.
2. **Sunbird's commercial terms.** Pricing, free-tier quota, numeric rate limits, latency, SLA and data retention are not published. They need a direct conversation with Sunbird before this is a production dependency.
3. **The regulatory line on selling.** Explaining cover, taking claims and giving status updates are servicing. Recommending or enrolling a rider into a product is intermediation under the Insurance Act and needs a licensed insurer or broker to own that step.

---

## 1. What IIP already has

| Capability | State | Where |
|---|---|---|
| WhatsApp webhook, inbound text and images, outbound text and templates | Built | `WhatsAppCloudApiInboundProvider`, `WhatsAppCloudApiOutboundProvider` |
| Typing indicator while the assistant prepares a reply | Built, not yet deployed | `CustomerAssistantService.StartTypingIndicator` |
| Intent handling: new claim, claim status, general question, product interest, human request | Built | `CustomerAssistantService` |
| Recognising a sender by phone, asking "which cover is this about?" for two policies | Built | `CustomerAssistantServiceTests` |
| Grounded policy answers with citations, auto-reply gate holding uncited answers | Built | Azure OpenAI, Azure AI Search, `CustomerAutoReplyGate` |
| Verbatim translation via a dashboard-configured Sunbird connection, quality checks, English fallback | Built, not yet deployed | `SunbirdTranslationProvider`, `TranslationService` |
| Consent records, retention, data subject requests, immutable audit | Built | `ConsentRecord`, `RetentionPolicy`, `AuditEvent` |
| Claimant portal link (24 h), document-by-document upload, Document Intelligence extraction | Built | `WhatsAppClaimantAccessLinkIssuer`, `ClientPortalEndpoints` |
| Voice notes | **Dropped today** | Audio maps to `WhatsAppContentType.Unsupported` and gets no reply |
| Speech to text, text to speech, audio transcoding | Missing | new |
| Read-back and confirmation before anything binding | Missing | new; claims open as "unconfirmed" from text |
| Acholi, Lugbara, Ateso, Runyankole | Missing | `SupportedLanguageCode` is En, Lg, Sw only |
| Customer-managed keys, Data Protection keys in Key Vault, shared-key access off | Missing | section 10 |

---

## 2. Architecture

The core keeps reasoning in English, as today. Language handling happens at the edges, so every gate, classifier, consent check and audit rule stays in one language staff can read.

```text
 Rider's phone (WhatsApp)
   |  voice note (audio/ogg; codecs=opus) or text, or a button tap
   v
 Meta WhatsApp Cloud API --webhook-->  IIP API  (Azure App Service, South Africa North)
   ^                                     | 1  verify signature, dedupe, store message, answer 200
   |                                     | 2  queue the turn
   |                                     v
   |                            Conversation worker
   |  typing indicator <-------  |- read receipt + "typing..." (refreshed every 20 s)
   |                              |- GET /{media-id} -> download within 5 minutes
   |                              |- keep original audio as evidence -> Blob (customer-managed key)
   |                              |- Media worker (Container App, ffmpeg): normalise, split > 28 s
   |                              |- Sunbird  POST /tasks/audio/transcriptions   (speech -> text)
   |                              |- Sunbird  chat completions, verbatim prompt  (lug -> eng)
   |                              |- Assistant core, English only
   |                              |     intent, policy lookup, grounded answer with citations,
   |                              |     claim state machine, read-back and confirm gate,
   |                              |     consent, audit, hand-off to a person
   |                              |- Sunbird  chat completions, verbatim prompt  (eng -> lug)
   |                              |- Sunbird  POST /tasks/audio/speech            (text -> MP3)
   |                              |- Media worker: MP3 -> Ogg Opus, mono
   |  voice note <-------------   |- POST /{phone-number-id}/media -> send audio {id, voice: true}
   |  buttons, reference text <-  '- interactive message (Yes / Fix it) and short text where needed
   |
 Secure upload link (browser, opened from WhatsApp)
   '-> IIP client portal -> IIP API -> Blob (private, CMK, malware scan) -> Document Intelligence
                                                      '-> extracted fields -> claim record -> audit

 Staff portal: claims officers review, decide, approve updates. AI prepares; people decide.
```

The media worker is separate from the API because the API runs on App Service, where shipping an ffmpeg binary is awkward. A small Container App next to `ca-afrislm-dev` is enough, and the AfriSLM container can be retired once Sunbird translation is proven.

---

## 3. External dependencies, costs and limits

### Sunbird AI

| Item | What the source says | Status |
|---|---|---|
| Authentication | Bearer token. Sign up at `api.sunbird.ai/register`, key issued after email verification. Tokens do not expire; `POST /auth/refresh-token` rotates. Rate limits are keyed by user, not token. | [Verified] |
| Pricing | No pricing page. Tiers named Free, Professional, Enterprise ("contact sales"). | [Unverified] not published |
| Free tier | "Designed for testing and development. Usage is limited to prevent abuse." No quota figure. | [Unverified] |
| Rate limits | Per account by tier. Over quota returns `429` with `Retry-After`. No numbers for any tier. | [Unverified] |
| Speech to text | `POST /tasks/audio/transcriptions`, multipart: `audio`, `language` (ISO 639-3, required), `timestamps` optional. Returns transcript, language, segments, `duration_seconds`, usage. Formats: "WAV, MP3, OGG, M4A, and more". Over 100 MB: only the first 10 minutes transcribed. Auto language detection described as unreliable; pass the language. | [Verified] |
| Streaming transcription | No streaming, WebSocket or real-time mode documented. One file per request. | [Verified] batch only |
| Clip length | Model card: max input 30 seconds, 16 kHz; longer clips need segmentation. Whether the API segments is not documented. | model card only |
| Confidence score | None in the documented response. | [Verified] none |
| Text to speech | `POST /tasks/audio/speech`: `text`, `voice` (e.g. `salt_lug_0001`), `language`, `response_mode` = `url` (signed GCS link, default), `stream` (raw bytes, "no storage") or `both`. Output MP3. 20 languages incl. Acholi, Ateso, English, Luganda, Lugbara, Runyankole. Lugbara carries an unexplained dagger in Sunbird's language table. | [Verified] |
| Translation | `POST /tasks/translate` (no instruction field, job-style) or chat completions on `sunflower-9b` (67 languages) / `sunflower-14b` (32). IIP uses chat completions to enforce verbatim-only translation. | [Verified] |
| Latency | Not documented for any endpoint. | measure in pilot |
| Data retention and location | Not documented. TTS `url` mode stores audio in Google Cloud Storage; region not stated. | ask Sunbird |
| Open source | Speech model on Hugging Face, Apache 2.0, self-hostable. Production API repo no longer public. | [Verified] |

### Published speech accuracy

Two Sunbird models publish figures and they differ a lot. The docs do not say which serves `api.sunbird.ai`.

| Language | SunflowerASR, 51 languages (WER / CER) | whisper-large-v3-salt (WER / CER) |
|---|---|---|
| Luganda (lug) | 30.3% / 7.7% | 14.2% / 2.9% |
| Acholi (ach) | 32.2% / 13.0% | 19.5% / 4.5% |
| Lugbara (lgg) | 20.3% / 5.4% | 18.9% / 4.5% |
| Ateso (teo) | 27.8% / 9.1% | 20.2% / 5.1% |
| Runyankole (nyn) | 31.8% / 7.9% | 23.4% / 4.3% |
| English (eng) | 9.4% / 3.6% | 1.8% / 0.9% (Ugandan English) |

Both are benchmark numbers. The SunflowerASR card says it "performs poorly on spontaneous, overlapping, noisy or code-switched speech" and can loop. The whisper-v3-salt card says training included Kampala street noise and 8 kHz phone audio, which is closer to our case.

### WhatsApp Cloud API

| Item | What the source says | Status |
|---|---|---|
| Inbound voice note | Webhook `audio` object: `id`, `mime_type`, `sha256`, `url` (rolling out), `voice` ("Boolean indicating if audio is a recording made with the WhatsApp client voice recording feature"). | [Verified] |
| Retrieving media | `GET /{media-id}` returns `url`, `mime_type`, `sha256`, `file_size`. "Media URLs expire after 5 minutes." Download needs the access token. Inbound media available 7 days. | [Verified] |
| Uploading media | `POST /{phone-number-id}/media` with `file`, `type`, `messaging_product=whatsapp`. Uploaded media persists 30 days. | [Verified] |
| Sending a true voice message | `{"type":"audio","audio":{"id":"...","voice":true}}`. With `voice: true` it shows as a voice message with transcription and profile picture. Must be Ogg with OPUS codec, mono, up to 16 MB. "base audio/ogg not supported". | [Verified] |
| Template headers | TEXT, IMAGE, VIDEO, DOCUMENT, GIF (marketing only), LOCATION. **AUDIO is not among them.** | [Verified] |
| Pricing model | Per message since 1 July 2025, charged only when a template is delivered. Free: non-template replies inside the 24 h window, and utility templates delivered inside it. Uganda is in "Rest of Africa". | [Verified] |
| Rest of Africa rates | Marketing US$0.0225 per message; utility typically under US$0.01. Meta's rate card not opened. | [Secondary] |
| AI chatbots on the Business API | From 15 January 2026, providers whose primary product is a general-purpose AI assistant may not use the Business API. Business-specific customer service assistants remain allowed. | [Secondary] |

### Per-voice-turn cost

Inside the 24 h window every reply is free on WhatsApp. A voice turn costs one Sunbird transcription, two translations (skipped when the rider speaks English), one Azure OpenAI call, and one Sunbird synthesis. The Sunbird portion cannot be priced until Sunbird quotes it.

---

## 4. Message flow: text path

1. Rider types. Meta posts the webhook; IIP verifies the signature, stores the message, dedupes by message id and answers 200 (as today).
2. Worker sends read receipt with typing indicator (clears on reply or after 25 s; refreshed every 20 s, up to four times).
3. Language: remembered choice, otherwise detector. A Ugandan-language message is translated to English through the verbatim Sunbird connection. Names, policy numbers and protected terms are masked as placeholders first; a translation that drops any placeholder is refused.
4. Assistant core classifies intent, looks the rider up by phone, and either answers from approved documents with citations, runs the claim flow, or hands off to a person.
5. English reply is translated back, labelled as machine translated, and sent. A translation failing any check goes out in English rather than unchecked.

## 5. Message flow: voice path

1. Rider records a voice note. Webhook carries `type: "audio"` and `audio.voice: true`. IIP stores it and answers 200. A new `Audio` content type replaces today's `Unsupported` mapping.
2. Typing indicator as in the text path.
3. `GET /{media-id}`, download within five minutes with the access token. Check SHA-256 against the webhook value; store the original Ogg as evidence in Blob storage.
4. Media worker converts to 16 kHz mono; clips over 28 s are split at silences into pieces under 30 s.
5. `POST /tasks/audio/transcriptions` with `language` set to the rider's chosen language. No auto-detection.
6. Transcript checks: repetition loop, words-per-second against `duration_seconds` (catches truncation), empty result. Failure produces a spoken "Sorry, I could not hear that clearly, please send it again", never a guess.
7. Translate to English, run the assistant core exactly as in the text path.
8. Write the reply for listening: two or three short sentences, well under 30 s of audio, digits one at a time, no lists, links or markdown.
9. Translate back, `POST /tasks/audio/speech` with `response_mode=stream` so nothing is stored at Sunbird.
10. Convert MP3 to Ogg Opus mono, voice-tuned (e.g. `ffmpeg -ac 1 -c:a libopus -b:a 24k -application voip`). Upload with `POST /{phone-number-id}/media`, send with `audio: {id, voice: true}`.
11. Anything the rider must keep (claim reference, date, amount) also goes as a short text line after the voice note.

### Latency budget

| Step | Expected | Basis |
|---|---|---|
| Webhook to worker start | < 1 s | IIP today |
| Media lookup and download (10 to 60 kB note) | ~1 s | estimate |
| Normalise and split | < 1 s | estimate |
| Sunbird speech to text | unknown | not documented |
| Translate in, assistant, translate out | 3 to 10 s | Azure OpenAI today; Sunbird translation unknown |
| Sunbird text to speech | unknown | not documented |
| Transcode, upload, send | ~2 s | estimate |

Target: median under 15 s, 95th percentile under 25 s. If Sunbird speech is slow, send a cached pre-recorded acknowledgement ("Got it, give me a moment") instantly while the real reply is prepared.

---

## 6. Onboarding by voice

First contact comes from the rider, which also satisfies Meta's opt-in rule. Entry points: stage poster or association card with QR code, or a SACCO officer saving the number.

1. **Language.** One short voice greeting in Luganda and English plus an interactive list of languages written in their own words (Luganda, Acholi, Lugbara, Ateso, Runyankole, English). Choice is remembered.
2. **Consent, spoken.** About 40 s in his language: what we keep (voice notes, accident and injury details, claim details), who handles it (insurer, IIP, Meta, Sunbird, Microsoft Azure in South Africa), that this is outside Uganda, that he can say no, how to ask for deletion. Buttons: "Yes, I agree" / "No". The consent record stores the script version and a hash of the audio played.
3. **Identity.** WhatsApp number matched against the insurer's records and the association or SACCO register. On a match, read back "Are you John Ssemwogerere, member of Kisenyi Stage Boda Association?" with Yes / No. National ID is never asked for in chat (section 12).
4. **No match.** Take his name by voice, read it back, record a lead. Full identity verification through the secure link or in person with the association officer.
5. **Product selection.** Explain the insurer's own products one at a time. Never recommend across insurers or enrol him; "I want this one" becomes a lead for a licensed person who calls back.

## 7. Policy information and disambiguation

- Which cover applies comes from his records (phone to policies and memberships), not from what he says. Association cover needs the association's register and wording loaded.
- One policy: answer directly. Several: ask which one in words he uses ("your motorcycle cover from the stage association, or the accident cover from your SACCO?"), as buttons for voice.
- Question matching a different cover than the one chosen: say plainly this cover does not include it and whether another cover he holds does.
- Answers only from approved documents, citations kept in the audit record. No grounding means "I will ask a person and they will get back to you".
- Exclusions stated as clearly as inclusions; the main relevant exclusion is required in every policy answer.
- Every policy answer ends with where it came from and that it is not a claim decision.

## 8. Claims, end to end

**Intake**

1. Voice note describing what happened; extract what, when, where, who was hurt, other party, registration, cover.
2. Ask only for what is missing, one question per voice note.
3. Read-back voice note summarising everything, Yes / Fix something buttons. Claim created only on "Yes".
4. Reference spoken digit by digit and also sent as text.

**Evidence**

- Photos of motorcycle and scene: fine in chat, already filed onto the claim.
- Police report, medical report, national ID, driving permit, payout details: secure upload link only.
- Checklist from the product's configured claim process.

**Status checks:** by voice or text, identified by phone, coarse status plus the next thing needed. No staff notes or decision reasons read out.

**Settlement notification:** a person decides and approves the update. Sent as a utility template (text) with a "Listen" quick-reply button; the tap opens the 24 h window and the voice version follows free.

## 9. Proactive updates

A business-initiated message cannot be a voice note: outside the 24 h window only templates may be sent, and AUDIO is not a template header format.

1. Pre-approve short utility templates per language, one per status change (documents needed, under review, decision ready, payment sent), each with a "Listen" quick-reply button.
2. Template says only that there is an update on claim *reference*; no amount, reason or health detail.
3. His tap is an inbound message and opens the 24 h window.
4. IIP immediately sends the full update as a voice note, plus reference and any amount as text.
5. No tap in 48 h: one reminder template, then the association officer or insurer calls him, flagged in the staff portal.

Inside an already-open window, updates go straight out as voice notes.

## 10. Document handling

**The link**

- Single-use token, 32 random bytes, only its hash stored (existing claimant token generator).
- Bound to one claim and to the WhatsApp number it was sent to. Opens within 30 minutes or expires; session lasts 60 minutes.
- Session is upload-only for that claim's outstanding documents; shows the checklist and nothing else.
- Rate-limited; every issue, open, upload and failure audited.

**Storage and encryption**

| Layer | Design | Today |
|---|---|---|
| In transit | TLS 1.2 minimum end to end, HTTPS only | In place |
| Upload path | Through the IIP API with content-based type, size and page checks | In place |
| Malware scanning | Defender for Storage on-upload scanning; file hidden until clean | Add |
| Storage account | Private container per insurer, public access off, **shared-key access off**, managed identity only | Shared key currently allowed |
| Encryption at rest | Customer-managed key in Key Vault Premium (HSM), purge protection, infrastructure encryption; one **encryption scope per insurer** | Microsoft-managed keys only |
| Key rotation | Key Vault rotation policy, yearly, key access logged | Add |
| Erasure | Blob deletion on retention or valid request, audited; deleting an insurer's scope key crypto-shreds at contract end | Retention job exists; key-level erasure new |
| Application secrets | ASP.NET Data Protection keys persisted to Blob and wrapped with a Key Vault key | Default file-system keys today |
| Location | Azure South Africa North (no Uganda region); cross-border under s.19, covered by consent | Decision |

**Extraction on ingest**

- Document Intelligence extracts only needed fields. Police report: reference, station, date, place. Medical report: date, facility, whether an injury is recorded (not diagnosis text). ID: name and ID number.
- National ID stored encrypted as its own field, masked to staff (last 4 digits) unless revealed with permission, audited.
- Extracted values are suggestions until a claims officer confirms.

## 11. Accuracy, read-back and audit

Nothing binding happens on a transcription alone. It happens on explicit confirmation of a read-back.

**Binding actions:** opening a claim or changing what it says happened; giving or withdrawing consent; confirming identity; expressing interest in a product; confirming a payout destination; withdrawing a claim.

**Read-back step**

1. Turn the transcript into structured fields, then write the summary from the fields, not the raw transcript.
2. Send as voice note and text: "I heard: yesterday on Jinja Road, a taxi hit your motorcycle and your leg was hurt. Is that right?"
3. Buttons "Yes, that is right" / "No, fix it". A tap is the confirmation; a spoken "yes" is not accepted for binding steps.
4. Numbers and identifiers read back digit by digit; text copy shows the plate.
5. "No, fix it" asks what is wrong and repeats. After two failed rounds, hand to a person who calls him.
6. Low-quality input (failed transcript checks, refused translation, language mismatch) never reaches read-back; it gets a resend request or a person.

**Audit record** (one per binding confirmation, append-only `AuditEvent`):

```json
{
  "event": "customer_assistant.claim_confirmed",
  "tenant": "...", "case_id": "...", "thread_id": "...", "correlation_id": "...",
  "inbound": [{
    "wamid": "wamid.HBg...", "received_at": "2026-10-02T09:14:07Z",
    "media_id": "...", "sha256": "...", "evidence_blob": "claims/.../orig-1.ogg",
    "duration_seconds": 23.4, "language": "lug"
  }],
  "transcription": {
    "provider": "sunbird", "endpoint": "/tasks/audio/transcriptions", "model_tag": "...",
    "text": "<verbatim transcript>", "checks": {"repetition": "pass", "rate": "pass"}
  },
  "translation": { "model_tag": "sunbird:sunflower-9b", "english": "...", "judge": "pass" },
  "extracted_fields": { "incident_date": "2026-10-01", "place": "Jinja Road" },
  "read_back": {
    "text_lug": "...", "text_eng": "...", "tts_voice": "salt_lug_0001",
    "audio_blob": "claims/.../readback-1.ogg", "sent_wamid": "wamid...."
  },
  "confirmation": {
    "method": "button", "button_payload": "confirm_claim_v1", "wamid": "wamid....",
    "tapped_at": "2026-10-02T09:15:31Z", "rounds": 1
  },
  "consent_record_id": "...", "prompt_versions": { "extraction": "v3", "read_back": "v2" },
  "result": "case_opened_unconfirmed_to_submitted"
}
```

Original voice note, transcript, read-back audio and the confirming tap are kept for the claim's retention period.

---

## 12. Compliance constraints

### Data Protection and Privacy Act, 2019 (Uganda) [Verified]

- **s.9 special personal data:** "(1) A person shall not collect or process personal data which relates to the religious or philosophical beliefs, political opinion, sexual life, financial information, health status or medical records of an individual." ... "(3) ... may collect or process personal data specified under subsection (1) where ... (b) the information is given freely and with the consent of the data subject". Injury and payout details are special personal data; the spoken consent names both.
- **s.19 processing outside Uganda:** "Where a data processor or data controller based in Uganda processes or stores personal data outside Uganda, the data processor or data controller shall ensure that [...] (a) the country in which the data is processed or stored has adequate measures in place for the protection of personal data at least equivalent to the protection provided for by this Act; or (b) the data subject has consented." Meta, Sunbird's cloud and Azure South Africa are all outside Uganda.
- **s.21 processors:** "A data controller shall not permit a data processor to process personal data for the data controller, unless the data processor establishes and complies with the security measures specified under this Act." A written processing agreement with Sunbird is needed before real rider audio is sent.
- **s.23(1) breach notification:** "... shall immediately notify the Authority in the prescribed manner, of the unauthorised access or acquisition and the remedial action taken."
- **s.27(1) automated decisions:** a data subject may require that "any decision taken by or on behalf of the data controller which significantly affects that data subject is not based solely on the processing by automatic means". Claim outcomes stay with named people.
- **s.7(1)** requires prior consent, with exceptions including "(c) for the performance of a contract to which the data subject is party". PDPO registration, an information security policy and a retention policy are business-owned prerequisites.

### WhatsApp Business Messaging Policy [Secondary]

Meta's policy site was unreachable from the research environment. Confirm verbatim wording before citing.

- Sensitive identifiers: do not share or ask people to share full-length payment card numbers, financial account numbers, personal ID numbers or other sensitive identifiers ("Don't share or ask people to share sensitive, personal information & identifiers"). ID numbers, ID photos and payout details go through the secure link only.
- Health: "Don't use WhatsApp for telemedicine or to send or request any health related information, if applicable regulations prohibit distribution of such information to systems that do not meet heightened requirements". No medical records in chat.
- Opt-in: "Only contact people who have opted in". First contact is always from the rider.
- Escalation: "You must have available prompt, clear, and direct escalation paths". "I want to talk to a person" works from any point.

### Insurance regulation

- Explaining cover, taking claims and giving status are servicing on behalf of a licensed insurer; IIP is the insurer's software vendor.
- Recommending across insurers, collecting details to place cover or completing a sale is intermediation under the Insurance Act 2017 and needs a broker or agent licence. Onboarding stops at "interest recorded, a licensed person will call".
- AI never approves or rejects claims, binds policies or sets premiums.

---

## 13. Ranked risks

1. **Speech recognition fails on real rider audio.** Published Luganda WER 14% to 30% on benchmark audio; both cards warn about noise and code-switching. *Mitigate:* run 100 real voice notes through the API first and measure; confirm which model serves the API; mandatory read-back with button confirmation; hand-off after two failed rounds.
2. **Sunbird as an unpriced, unmeasured dependency.** No price, quota, rate limit, latency, SLA or retention statement; three or four Sunbird calls per voice turn. *Mitigate:* written agreement on price, limits, uptime, retention, location, and a DPA (s.21); keep the self-hosted Apache model as a costed fallback.
3. **Crossing into intermediation.** *Mitigate:* the bot explains and records interest; a licensed person sells.
4. **Reply time too slow.** Up to eight sequential hops, three with unknown timing. *Mitigate:* measure; cache fixed prompts as pre-rendered audio; instant acknowledgement; skip translation for English.
5. **Errors compounding through the chain.** *Mitigate:* verbatim translation and quality judge (built); read-back from structured fields; native-speaker review of fixed prompts; English core so staff can audit.
6. **Special personal data processed abroad.** *Mitigate:* explicit spoken consent naming health and financial data, recorded with script version; DPAs with every processor; PDPO registration.
7. **Identity and payout details cannot be taken by voice.** *Mitigate:* association and SACCO officers as assisted-onboarding points; say so openly in the submission.
8. **Meta policy wording and 2026 AI terms.** *Mitigate:* keep the assistant to the insurer's own servicing tasks; confirm policy text before citing.
9. **Thinner support beyond Luganda.** Lugbara TTS carries an unexplained mark; IIP languages stop at En, Lg, Sw. *Mitigate:* launch in Luganda and English; add a language after native-speaker review of its prompts.

## 14. Could not verify

- Sunbird pricing, free-tier quota, numeric rate limits, SLA, latency.
- Which speech model serves `api.sunbird.ai`.
- Whether the API splits audio over 30 s or truncates.
- Sunbird data retention and processing location.
- Sunbird TTS max text length; meaning of the Lugbara dagger.
- Verbatim text of Meta's Business Messaging Policy.
- Meta's exact Rest of Africa rates.
- The 2026 AI terms (from press and provider summaries).
- Whether AUDIO is explicitly refused as a template header or just not listed (treated as not supported).

## 15. Build order

1. **Measure:** Sunbird account, 100 real rider voice notes, error rate and timing per step.
2. **Voice in, text out:** accept `audio`, download, transcribe, reply in text.
3. **Read-back and confirmation** with interactive buttons, and the section 11 audit record. Required before any claim is opened from voice.
4. **Voice out:** TTS, transcoding, voice-message send, cached fixed prompts.
5. **Onboarding and consent by voice**, association register as identity source.
6. **Storage hardening:** CMK, encryption scopes, shared key off, Data Protection keys in Key Vault, malware scanning, tighter upload link.
7. **Proactive updates:** utility templates per language with the "Listen" button.
8. **More languages**, one at a time after native-speaker review.

## 16. Sources

- Sunbird AI docs: [Authentication](https://docs.sunbird.ai/authentication), [Rate limits](https://docs.sunbird.ai/guides/rate-limits), [Speech to text guide](https://docs.sunbird.ai/guides/speech-to-text), [Speech to text reference](https://docs.sunbird.ai/api-reference/speech/speech-to-text), [Text to speech reference](https://docs.sunbird.ai/api-reference/speech/text-to-speech), [Translate](https://docs.sunbird.ai/api-reference/translation/translate), [Languages](https://docs.sunbird.ai/languages)
- Sunbird model cards: [SunflowerASR-51-african-languages](https://huggingface.co/Sunbird/SunflowerASR-51-african-languages), [asr-whisper-large-v3-salt](https://huggingface.co/Sunbird/asr-whisper-large-v3-salt)
- Meta developer docs: [Audio messages](https://developers.facebook.com/docs/whatsapp/cloud-api/messages/audio-messages), [Media](https://developers.facebook.com/docs/whatsapp/cloud-api/reference/media), [Audio webhook](https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks/reference/messages/audio), [Template components](https://developers.facebook.com/docs/whatsapp/business-management-api/message-templates/components), [Pricing](https://developers.facebook.com/docs/whatsapp/pricing)
- WhatsApp Business Messaging Policy: [business.whatsapp.com/policy](https://business.whatsapp.com/policy) (unreachable from research environment); secondary: [Messente summary](https://support.messente.com/whatsapp-usage-restrictions)
- AI terms change: [TechCrunch](https://techcrunch.com/2025/10/18/whatssapp-changes-its-terms-to-bar-general-purpose-chatbots-from-its-platform/), [respond.io](https://respond.io/blog/whatsapp-general-purpose-chatbots-ban)
- Rates: [BSG pricing guide](https://bsg.world/blog/guides/whatsapp-api-pricing-in-2026-what-it-costs-how-to-optimize) (secondary)
- [Data Protection and Privacy Act, 2019](https://media.ulii.org/media/legislation/18002/source_file/b6ae5cce4290322a/2019-9.pdf) (ULII published text)
- IIP source code and [IIP_HACKATHON_READINESS_BLUEPRINT.md](IIP_HACKATHON_READINESS_BLUEPRINT.md)

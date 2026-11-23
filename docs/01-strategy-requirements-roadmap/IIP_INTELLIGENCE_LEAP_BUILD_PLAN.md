# IIP intelligence leap: build plan

Live WhatsApp calls in English and Ugandan languages, a language arbiter that confirms before it acts, interactive document review, and an agent on the insurer's phone that visibly does the work. One ordered build, from research to a rehearsed demo.

- Researched: 23 November 2026
- Status: proposal, revised 23 November 2026 with the owner's answers (section 9). Nothing in this document is built.
- Sources: Meta developer docs (calling, SIP, permissions, FAQ), Microsoft Learn (Voice Live, SIP, Foundry), Anthropic platform docs and engineering posts, Sunbird model cards, the IIP source tree, and the earlier IIP voice, language and BEVAYA documents
- Builds on: [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md), [IIP_LOCAL_LANGUAGE_BUILD_PLAN.md](IIP_LOCAL_LANGUAGE_BUILD_PLAN.md), [IIP_KATO_FIELD_TEST_AUDIT.md](IIP_KATO_FIELD_TEST_AUDIT.md), [bevaya-competitive-benchmark-2026-07-16.md](../03-design-architecture/bevaya-competitive-benchmark-2026-07-16.md), [iip_organisation_platform.md](../02-development-engineering/iip_organisation_platform.md)

Status markers used below:

- **[Verified]** read at the primary source, or confirmed in the IIP source tree
- **[Secondary]** secondary source only, confirm before citing externally
- **[Unverified]** could not be verified, or the sources conflict

---

## 1. Verdict

A customer can call IIP on WhatsApp and talk to an agent. IIP only receives calls and never places one. Meta supports that, it is free to the business, and Uganda is not on Meta's exclusion list. The part that decides whether it feels real is not the call. It is Luganda: no realtime speech-to-speech model speaks it, so the Luganda path must be a cascaded pipeline (listen, think, speak) that we own, and its latency has to be measured before anything else is committed.

Four things are broken or missing today, and each one is verified in code (section 2):

1. **Voice is batch and blind.** A voice note goes through Sunbird as one file with the language hard-coded to Luganda unless the customer chose Swahili. Nothing checks what came back.
2. **Language is a word list.** About 70 Luganda words decide whether a message is Luganda. Audio never takes part in the decision.
3. **Document review is not interactive, and its coordinates are probably wrong.** The data to highlight a field on the page is captured, but the screen never draws it, and the numbers are not scaled to the page.
4. **The insurer assistant is three tools and one blocking request.** Nothing streams, so the person cannot watch it work.

The plan fixes these in an order where each step is useful alone and carries the next (section 7). The demo that follows from it is in section 5.

---

## 2. What is true today (verified against source)

### 2.1 Voice

| Finding | Evidence |
|---|---|
| Voice notes are transcribed as one batch file through Sunbird. No streaming, no confidence score, no retry on a poor result. | `SunbirdVoiceNoteTranscriber.TranscribeAsync` |
| The language sent to Sunbird is `lug` for everyone except a customer who chose Swahili. English, Runyankole, Acholi, Ateso and Lugbara speech is decoded as Luganda. The code comment says this works for English because Sunbird's Luganda model writes English speech as English. | `SunbirdVoiceNoteTranscriber.LanguageFor` |
| Voice replies exist (text to speech, then WAV to Ogg Opus) but measured about 12 seconds for a short reply, which is fine for a voice note and unusable in a live call. | Same file, `SpeakAsync` comment |
| Sunbird's first request after a quiet spell took almost 40 seconds, later ones about 3. | Same file |
| Sunbird publishes no streaming mode, no confidence, no rate numbers, no latency, and says its own language auto-detection is unreliable. | [IIP_WHATSAPP_VOICE_DESIGN.md](IIP_WHATSAPP_VOICE_DESIGN.md) section 3 [Verified] |
| Published accuracy on benchmark audio: Luganda 14.2% WER on `whisper-large-v3-salt`, 30.3% on SunflowerASR; the SunflowerASR card says it does poorly on noisy, overlapping or code-switched speech. | Same document |

### 2.2 Language

| Finding | Evidence |
|---|---|
| Detection is a hand-written word list: about 70 Luganda and 85 Kiswahili words, scored by the share that match. English is "at least 4 words and 3 hits from a 30-word list". | `WordListLanguageDetector`, `LanguageWordListScorer`, field audit section 5 |
| A language is saved only when the customer asks for it ("reply in Luganda") or answers a menu. It lasts 90 days per number. | `CustomerLanguageResolutionService` |
| A known failure: with a stored Luganda preference, a plain English sentence is treated as Luganda, translated, rejected by the quality judge, and the customer gets a refusal and an escalation. | Field audit section 6 |
| `SupportedLanguageCode` holds English, Luganda and Swahili only. Acholi, Lugbara, Ateso and Runyankole are missing. | Voice design section 1 |
| No spoken-language decision exists at all. The audio is never examined. | Code review |

### 2.3 Documents

| Finding | Evidence |
|---|---|
| Azure Document Intelligence returns a polygon per field and IIP stores a `BoundingBox` per field, documented as "normalised fractions of the page (0 to 1)". | `BoundingBox.cs`, `DocumentExtractionResult.FieldBoundingBoxes` |
| The mapper copies the polygon's first corner and the width and height straight from the polygon with no division by the page size. Azure's polygon is in page units (inches for PDFs, pixels for images). So the stored values are very probably not 0 to 1. **[Unverified against a live response; verify first in the build, section 6.2.]** | `AzureDocumentIntelligenceProvider.BuildExtraction` lines 90 to 105 |
| Only the first region of a field is kept, and the box is built from two polygon corners, so a rotated or multi-line field is wrong. Key-value pairs keep no box at all. | Same method |
| The API returns the box to the portal. The portal never draws it: the extraction page shows the file in an `iframe` or `img`, and no portal source file references a bounding box (only tests do). | `DocumentMapper.cs` line 312; `lovable-pages/DocumentExtraction.tsx` lines 240 and 241 |
| BEVAYA's best-rated idea in our own benchmark is field-level review with source-coordinate highlighting ("Highlight Mode", tracing a value to page, paragraph and coordinates). The benchmark's recommendation is to add the same traceability with immutable before and after audit. | `bevaya-competitive-benchmark-2026-07-16.md` lines 177 and 705 |

### 2.4 Insurer assistant and the app

| Finding | Evidence |
|---|---|
| The Command Center analyst has three tool specs and returns one finished answer. The portal has no streaming reader (no `EventSource`, no stream reader) and the API has no event-stream endpoint. | `CommandCenterAnalyst.ToolSpecs`; searches of `src` and `web/portal` |
| The tool-calling provider port exists and is model-agnostic. | `IToolCallingChatProvider`, `AgentToolSpec` |
| The Android app is one `Activity` with one `WebView`. It holds only the Internet and network-state permissions: no microphone, no camera, no push. | `android/README.md`, `AndroidManifest.xml` |
| Governed automations, workspaces, inbox, reports and intents were added on 2026-11-20 and give the agent real things to act on. | `iip_organisation_platform.md` |

---

## 3. Research findings

### 3.1 WhatsApp Business calling

All of this is from Meta's calling pages unless marked.

| Question | Answer |
|---|---|
| Can a customer call a business and talk to software? | Yes. A user-initiated call arrives as a `calls` webhook with an SDP offer; the business answers over the Graph API with an SDP answer and WebRTC carries the audio. **[Verified]** |
| What does it cost the business? | User-initiated calls are free (Meta page, updated July 2026). Business-initiated calls are billed per minute in six-second pulses at a rate set by the called country; Meta's rate card was not opened and a reseller example for Botswana was about US$0.01 a minute. Permission request messages carry normal messaging charges. **[Verified] for free inbound; rate [Unverified]** |
| Is Uganda allowed? | Business-initiated calling is blocked only for business numbers with United States, Canada, Egypt, Vietnam and Nigeria country codes. Uganda is on no exclusion list, and Meta says availability follows Cloud API availability. No page names Uganda or +256. **[Unverified for Uganda; ask Meta or test on the number]** |
| What must the number have? | A Cloud API number (not the WhatsApp Business app), subscribed to the `calls` webhook, calling enabled in call settings, a daily messaging limit of at least 2,000 unique recipients (listed as a prerequisite for the Calling API as a whole; Meta's pages do not say whether it is enforced for a number that only receives calls, so assume it is), a credit line on the messaging account, Graph API v17 or later. Test and sandbox numbers are exempt from the 2,000 rule. **[Verified]** |
| How to reach the 2,000 tier | New business portfolios start at 250. The tiers are 250, 2,000, 10,000, 100,000 and unlimited. Any one of these lifts a portfolio to 2,000: Meta business verification, verification by the partner that onboarded it, or 2,000 delivered template messages to unique recipients inside a 30-day window. The limit belongs to the business portfolio and is shared by every number in it. Meta's calling FAQ says business verification is not required for calling, while its limits page lists verification as a route to 2,000. **[Verified]** |
| How does a call start and end? | `connect` webhook with offer, then optional `pre_accept`, then `accept` with answer, then media, then `terminate`. The business has about 30 to 60 seconds to accept. Webhooks can repeat or arrive out of order, so every handler must be idempotent and ordered by timestamp. **[Verified]** |
| Audio | Opus at 48 kHz by default, G.711 also supported (March 2026). One audio stream. Meta is ICE-lite, so the business must be the controlling ICE agent. DTMF arrives as RFC 4733 events in the RTP stream. **[Verified]** |
| Calling the customer back | **Not used: IIP receives calls only.** For the record, it needs the customer's permission: an interactive `call_permission_request` message (inside the 24-hour window) or a template. Permission lasts 7 days (temporary) or is permanent. A request may be sent once per 24 hours and twice per 7 days. Four unanswered calls in a row revoke it. **[Verified]** |
| Limits | No call duration limit. Concurrency is stated as 5,000 per number on one page and 1,000 on another. Up to 100 connected calls per 24 hours (the scope, per pair or per number, is described both ways). Text and media messages can be sent during a call. **[Conflicting; treat 1,000 as the planning ceiling]** |
| SIP instead of Graph calling | Possible: Meta sends SIP over TLS to a server we host (one server per number, certificate must match the hostname, digest auth), with SRTP by DTLS or SDES. While SIP is enabled the Graph calling endpoints stop working on that number. **[Verified]** |
| Policy risk | From 15 January 2026 general-purpose AI assistants may not use the Business API; business-specific customer service assistants remain allowed. IIP is the second kind. The insurer-side assistant must therefore live in the IIP Go app, not on WhatsApp. **[Secondary]** |

### 3.2 Realtime voice runtimes

| Option | What it is | Fit for IIP |
|---|---|---|
| Azure Voice Live over WebRTC or WebSocket | Microsoft's speech-to-speech service with turn detection, noise suppression, echo cancellation and function calling. 146 input locales. Models include `gpt-realtime` and `gpt-realtime-mini`. This is what MIP uses. | Strong for English. Luganda is not a listed locale and Swahili is not confirmed. A stable .NET SDK exists (`Azure.AI.VoiceLive` 1.0.0, targets .NET 8, tools are run by our own code). **[Verified for the feature set; locales [Unverified]; SDK from the package listing]** |
| Voice Live over SIP | Carrier or SBC sends TLS SIP to a Voice Live ingress (port 5063, digest auth, SRTP, Opus and G.711 supported). The page is in **public preview, with no SLA, and not recommended for production**. It does not document tool calls on a SIP call. The FAQ still says SIP is unsupported. Regions: East US 2, France Central, Southeast Asia, Sweden Central, West US, West US 2. | A tempting zero-media-server path (Meta SIP to our SBC to Voice Live), blocked today by preview status and the missing tool-call detail. Revisit when it is generally available. |
| Cascaded pipeline (voice activity detection, speech to text, language model, text to speech) | Frameworks exist: Pipecat (Python, BSD-2) and LiveKit Agents (Apache-2.0). Neither ships a WhatsApp calling connector, so the Graph API signalling is ours to write. | The only path that supports Luganda. We choose each engine and can swap it. We write it in .NET on SIPSorcery rather than adopt a Python framework. |
| Sunbird for Luganda | `whisper-large-v3-salt` is open (Apache 2.0) and can be self-hosted. Sunbird's speech-LLM (Sunflower-Speech) reports a 55 ms time to first token with a Luganda WER of 22.6%, or 16.7% after reinforcement tuning, against 5.0% for standalone Whisper in the same table. | Self-hosting would remove the 3 to 40 second API variance but needs a GPU, so it is not planned now. **[Secondary for the speech-LLM figures]** |
| Google Chirp 3 | Streams, with one of the widest African language footprints. No Luganda-specific figure was found. | A candidate to benchmark, nothing more. **[Unverified]** |

### 3.3 Spoken language identification

- Sunbird says its own auto-detection is unreliable, so IIP passes a language today. That moves the problem to us.
- Research notes that spoken language identification for low-resource and tonal African languages remains weak, and that code-switching means the language can change inside one sentence.
- No source describes a voice agent that confirms the language with the caller before answering, so there is no pattern to copy. The design in section 6.3 is ours, built from three standard parts: decode under more than one hypothesis, score the results, and ask when the scores are close.

### 3.4 How Claude takes actions, and what to copy

From Anthropic's documentation (Managed Agents is in **beta**; computer use is partly beta):

| Idea | What Anthropic does | What IIP copies |
|---|---|---|
| Separate the brain from the hands | A harness loop calls the model and routes tool calls; the sandbox that runs them is disposable and treated as untrusted. Sessions keep history and outputs server-side and resume after a pause. | An IIP Agent Run: a loop in the API, tools as typed adapters, a sandbox only for code. State in the database, so a phone that goes offline can resume. |
| Events, not one answer | Responses stream as server-sent events and the full event history can be fetched. | Every step (plan, tool call, result, artifact, approval request) is a stored event and a live event. This is what makes the agent look alive. |
| Typed tools first, pixels last | The computer-use tool is a client-run loop (screenshot, click, type) inside a Docker desktop. Anthropic recommends a dedicated VM, no credentials in the model's reach, an internet allowlist, and a human for consequential actions. It lists direct integrations and MCP as preferred. | Our "computer" is IIP's own API, which is faster, safer and auditable. A pixel-driving browser is kept only for an insurer's legacy core system that has no API, and only behind an approval gate. |
| Code beats chains of calls | "Code execution with MCP": expose tools as code the model writes, keep intermediate data in the sandbox, filter large results before they reach the model, tokenise personal data so the model sees `[EMAIL_1]`. Anthropic's own example cut 150,000 tokens to about 2,000. | A Python sandbox for analysis and charts, with data passed by file and personal fields tokenised. |
| Workflows before agents | Prompt chaining, routing, parallelisation, orchestrator-workers, evaluator-optimiser. Use the simplest thing that works. Design tools like an interface: hard to misuse (absolute paths removed a whole error class), documented with examples. | Voice turns are a fixed workflow with an agent inside the thinking step. The insurer assistant is a true agent but every tool is poka-yoke. |
| Compliance | Managed Agents is not eligible for zero data retention or HIPAA cover, except when self-hosted. Computer use on the Messages API is ZDR-eligible. | We do not use Anthropic's hosted agent service. We build the same shape ourselves on a model port, so tenant data stays under IIP's controls. |

Claude models are available in Microsoft Foundry (generally available since 29 June 2026, per a secondary source), hosted on Azure with a US data zone option, in East US 2 and Sweden Central, with Anthropic as data processor. Whether the Claude 5 models are there, and what residency this gives under the Data Protection and Privacy Act, is **[Unverified]** and is a decision in section 9.

### 3.5 Document review

- Azure returns a polygon of four points per element, in page units, with page number. Page width, height and unit come back with the pages and are needed to scale.
- Nothing in Azure's documentation describes a review interface. The viewer is ours: render the page, draw the polygons on top, link both ways between the field list and the page. Mindee documents the same pattern with normalised 0 to 1 polygons. **[Secondary]**

### 3.6 What exists elsewhere

Voice agents for first notice of loss exist and are sold by Hippo (Clara), insured.io (voice and chat, English and Spanish), Retell and others; most keep a human fallback and leave coverage, liability and payment decisions to adjusters. We found no public example of an insurer taking a claim by WhatsApp call in Luganda. That is a finding of a limited search, not proof, so the pitch says "we have not found another", never "there is no other".

---

## 4. What stays fixed

These come from the repository rules and from the earlier field audit. Every phase below obeys them.

1. **AI prepares, people decide.** The call agent and the insurer agent may read, explain, draft and request. They never approve, decline, settle or pay. The platform preamble carrying this boundary is not tenant-editable.
2. **Tenant isolation and permissions** are enforced in Domain and Application. A tool call runs as a specific user or customer, inside one tenant.
3. **Credentials** live in the per-tenant, encrypted, validated-before-save connection model, never in startup configuration. Calling settings join the WhatsApp connection, and the secret stays write-only.
4. **Every actionable claim carries a citation that opens its source.** In voice, a spoken fact is also logged with its source.
5. **No fabricated data, no dead controls.** What is absent renders as `Not configured`.
6. **Binding facts are read back** in the caller's language before anything is written as confirmed.
7. **No em dashes, no emoji**, anywhere.

---

## 5. Hat one: what wins the room

An insurer or a judge sees many demos with a chat box in them. What they remember is a moment where something happened that they did not think was possible, followed by proof that it was not staged. The build is organised around five such moments, in the order a demo plays them.

**Moment 1, the call.** The evaluator dials the IIP number from their own WhatsApp. It answers in under two seconds, says in plain words what it is and that the call may be recorded, and asks how it can help. They speak English and report an accident. Mid-call they switch to Luganda; the agent follows without being told, confirms in one short sentence which language it heard, asks what happened, and reads the key facts back. While still on the call it sends a WhatsApp message with a secure link to upload a photo of the damage. They send the photo. The agent says it has it. The call ends with a claim number. Nobody typed anything.

**Moment 2, the same second on the insurer's phone.** The adjuster's IIP Go app buzzes: "Claim opened by voice call, 2 min 14 s, Luganda and English, 5 facts confirmed, 1 photo, no fraud signals." Tapping it opens the transcript with the audio, each fact linked to the sentence it came from, and the photo next to the claim.

**Moment 3, the insurer talks to the system.** The adjuster types or speaks: "Which claims need me today, and what is the biggest risk?" The screen does not freeze. They watch it work: a line for each step ("Reading 14 open claims", "Checking document confidence", "Comparing with the last 90 days"), a table and a chart appearing, and a short answer with every number linked to its source. They ask it to prepare the settlement packet for the first claim. It does, and then it stops, shows exactly what it prepared, and asks the human to approve. The ceiling is visible, and that is the point: it is the thing a regulator and a chief claims officer both want to see.

**Moment 4, the document.** The adjuster opens the police report that arrived with the claim. It sits on the left as a real page. Hovering over "Date of loss" in the list lights the exact words on the page; hovering a word on the page lights the field. A field at 61% confidence is marked with a dot and the word "Check", and a correction is saved with who changed what and when.

**Moment 5, the proof.** A page that shows the numbers, not the story: accuracy by language measured on real recordings, calls handled, how many needed a person, cost per call in shillings, and a plain list of what the system will not do. This is the page that turns a good demo into a credible company.

What would sink it, and what the plan guards against:

- Dead air. A pause over two seconds on the call reads as broken. Section 6.1 treats latency as a gate, not a polish item.
- Answering in the wrong language. This is the exact failure already seen in the field audit. Section 6.3 is built around it.
- A claim we cannot back. "First in the world" is out. "We have not found another insurer taking claims by WhatsApp call in Luganda" is in.
- Staged data. Every number on screen comes from the live system.
- An agent that seems to have authority. The visible human gate is a feature.

---

## 6. Hat two: how we build it

### 6.1 System picture

```text
 Customer (WhatsApp call, message, voice note)            Insurer (IIP Go app, portal)
        |                                                          |
        v                                                          v
 Meta Cloud API  --calls / messages webhooks-->  IIP API (.NET, existing)  <-- SSE run stream, REST
        ^                                          |   |   |
        | WebRTC audio (Opus)                      |   |   '-- Agent Run service (new)
        |                                          |   |         loop, tools, approvals, events
        v                                          |   '------ Language arbiter (new, shared)
 Call media service (new, .NET)                   |
   one WebRTC peer per call (SIPSorcery)  <------- |       Tools: claims, policies, documents,
   English: relay audio to Voice Live              |       inbox, reports, automations, approvals
   Luganda: arbiter -> ASR -> LLM -> TTS           |
        |                                          '-- Sandbox (Azure Container Apps dynamic session,
        |                                              Python, no network, data by file)
        '-- Engines called over HTTPS or WebSocket, none hosted by us:
            Voice Live (English), Sunbird API (Luganda and others),
            Azure OpenAI or Claude in Foundry for reasoning
```

The call media service is written in .NET, so the estate stays one stack: SIPSorcery for the WebRTC peer and the `Azure.AI.VoiceLive` SDK for the English engine. It holds only audio and engine adapters. It owns no business rule: identity, tools, policy, consent, claim creation and audit all stay in the API, reached through a narrow internal contract. Because every engine is a remote service, it needs no GPU and little CPU. It needs an ADR for where it runs (see the hosting spike in Phase 0).

### 6.2 Workstream A: voice pipeline repair and the language arbiter

This workstream helps voice notes today and is the base of live calls. Build it first.

**A1. Measure before building.** No call audio exists yet, so the test set is built from three sources: public corpora (Common Voice Luganda, said to be CC0; the Makerere Radio Speech Corpus, about 155 hours of Luganda radio; licences to be checked before use), about 60 clips that staff record on their own phones and send as WhatsApp voice notes (scripted claim scenarios in English, Luganda and mixed, some recorded outdoors), and later consented pilot audio. The staff clips matter most because the public sets are read or broadcast speech and miss the WhatsApp channel and street noise. Score word error rate and time to result on Sunbird's API and, for English, Voice Live, with Chirp 3 if available. Measure Sunbird text to speech and at least one alternative for time to first audio. These numbers decide the Luganda call path (6.3) and are the first slide of the proof page.

**A2. Transcript acceptance gate.** A new Application service sits between speech to text and the assistant core. A transcript passes only if all hold: not empty, no repetition loop, words per second plausible for the audio length (catches truncation), language consistent with the arbiter's decision, and every extracted entity valid against the store (a spoken policy number must exist for this sender, a plate must match a known format). A failing transcript triggers a spoken re-ask, never a guess. This is the "orchestrator that confirms before it reaches" the agent.

**A3. Language arbiter** (a Domain policy plus an Application service, replacing `LanguageWordListScorer` as the deciding step):

1. **Candidate set.** English plus the tenant's enabled local languages, with a prior from the caller's remembered language and recent turns.
2. **Parallel decode.** Run speech to text for the top two candidates on the first speech segment (English and Luganda for most callers). A Luganda model that writes English as English (already observed) helps with code-switching.
3. **Score each decode** with three signals: engine log-probabilities where an engine returns them (Sunbird's API gives none, so this signal is often absent), a trained text language classifier replacing the word list (trained on SALT text and our own consented transcripts, covering all seven languages), and a short language-model judgement of which transcript reads as coherent text in its language.
4. **Decide with a margin.** A clear winner proceeds silently. A thin margin asks one bilingual question and takes the answer as an explicit choice. The question text is written and checked by a native speaker, not generated.
5. **Per-turn re-check with hysteresis.** The language can change mid-call, but it flips only on two agreeing turns, so one borrowed word does not switch the agent.
6. **Close the known bugs.** A message with no local-language signal is English even with a stored preference. Add the missing Luganda forms listed in the field audit. Extend `SupportedLanguageCode` to Acholi, Lugbara, Ateso and Runyankole, gated by what A1 shows is usable.

**A4. Read-back.** Before any claim, policy change or follow-up request is saved as confirmed, the agent restates the facts in the caller's language (names, dates, amounts, plate, place) and waits for a clear yes. Digits are read one at a time.

**A5. Evaluation harness.** A repeatable test that replays the A1 clips through the full path and reports word error rate, language-decision accuracy and a confusion matrix, per language. It runs in CI against recorded fixtures and on demand against live engines. Release gates are thresholds on these numbers, agreed after A1.

**A6. Voice note improvements that come free:** typing indicator while transcribing, voice reply in the caller's language when the caller sent a voice note and the reply fits under 30 seconds, and a fallback to text when speech output is slow.

### 6.3 Workstream B: the live call

**B1. Connection mode.** Use Graph API calling (webhooks plus WebRTC), not SIP. Reasons: no SIP server, public TLS hostname or digest setup to run; the permission and call-state APIs stay available; SIP and Graph calling cannot run together on one number, so choosing SIP would remove those. SIP stays an option if Voice Live SIP reaches general availability and documents tool calls.

**B2. Signalling in the API.** New endpoints in the existing WhatsApp area receive the `calls` webhook (signature checked, deduplicated by call id and event, ordered by timestamp), resolve tenant and caller, create a `CallSession` record, and hand the SDP offer to the media service. The media service returns an SDP answer; the API sends `pre_accept` and `accept`. On `terminate` it stores duration, status and the transcript. Calling settings (enabled, recording notice text, languages) are added to the WhatsApp connection configuration with write-only secrets, validated before save.

**B3. Media service.** One WebRTC peer per call using SIPSorcery (open source .NET with Opus through Concentus, ICE and DTLS-SRTP): be the controlling ICE agent, receive Opus, resample to the engine's rate, pass the audio to the engine adapter, play the reply back as Opus, and honour barge-in. Where Voice Live does the listening, turn detection and speaking (English), this service only relays and transcodes audio, which is light work. It runs as a small separate host, not inside the API process, because it needs UDP and holds long calls. Calls are stateful, so routing is sticky by call id and scale is by concurrent calls. A call that loses its media node ends with a spoken apology and an offer of a WhatsApp follow-up, never silence.

**B4. Two engine paths behind one interface.**

- **English:** Voice Live speech-to-speech through the .NET SDK, held server-side so tools run in our API as the identified caller. Lowest latency and best turn-taking. The owner has accepted processing outside Africa (section 9).
- **Luganda and the others:** cascaded, on Sunbird's API with no GPU of our own: Sunbird for listening, our reasoning model for thinking, the fastest text to speech that A1 proves. Sentences are spoken as they are produced, not after the whole reply exists. A bank of pre-recorded, native-checked phrases covers the confirmations and fillers that must be instant.
- **Gate G1:** if the Luganda path cannot answer within about 2.5 seconds at the median in A1, the call launches English first and Luganda continues as voice notes with a spoken "send me a voice note" hand-over, until the engines catch up. With Sunbird's API timings measured so far (3 to 40 seconds to transcribe, about 12 seconds to speak), G1 is likely to apply at first, and the plan does not hide that.

**B5. The orchestrator for a call.** A fixed workflow, with an agent in the thinking step:

1. Greet, state the recording notice, identify the caller from the verified WhatsApp number (not from what they say), and note which language they started in.
2. Each turn: acceptance gate, arbiter, then one model call with a small tool set.
3. Filler while a tool runs, so nothing is silent for more than about a second.
4. Read back and confirm before any write.
5. End with what happened and what comes next, in the caller's language.

**B6. Customer-side tools** (typed, permissioned, tenant-scoped, every call audited):

| Tool | Does | Authority |
|---|---|---|
| `find_policies` | Policies for the verified caller | Read |
| `answer_policy_question` | Grounded answer with citation from approved documents | Read |
| `get_claim_status` | Status in plain words | Read |
| `start_claim` | Opens a claim draft with the confirmed facts | Write, after read-back |
| `request_photo_upload` | Sends the secure upload link in the same WhatsApp thread, during the call | Write |
| `request_callback` | Records that the caller wants to be contacted and creates a work item (no call is placed) | Write |
| `handoff_to_person` | Creates a work item with the transcript and tells the caller a person will follow up by WhatsApp, because Graph calling has no transfer | Write |

There is no tool to approve, pay or decline. The agent says "I have sent this to our team for review".

**B7. Consent and data.** The recording notice plays first and the call continues only on staying on the line; the recording, transcript and audio evidence follow the retention policy and the data-subject rights that exist today. Voice is personal data. Where the audio is processed (section 9) is stated on the data-processing page.

**B8. No outbound calls.** IIP never places a call. A follow-up is a WhatsApp message from the claim screen (a draft the person sends), or a call the adjuster makes from their own phone outside the platform. This removes the permission flow, the per-minute charge and the country restrictions on business-initiated calling.

### 6.4 Workstream C: the insurer agent and IIP Go

**C1. Agent Run service** (Application layer, ports for the model and the sandbox):

- A `Run` has an owner, a tenant, a goal, a budget, a status, and an ordered list of `RunEvent` records stored in the existing store.
- Event types: `plan`, `step_started`, `tool_called`, `tool_result`, `artifact`, `message`, `approval_requested`, `approval_decided`, `error`, `done`.
- The loop extends `IToolCallingChatProvider`: parallel tool calls, a step limit, a cost and token cap per run, cancellation, and resume from the last event after a disconnect.
- Streaming is a server-sent event endpoint under `/api/v1/agent/runs/{id}/events`, with `Last-Event-ID` resume.

**C2. The toolbelt.** Read tools over claims, policies, customers, documents, inbox, reports, workspaces and automations, using the services the Command Center already calls. Prepare tools: draft a reply, prepare a settlement packet, draft a report. Act tools are gated: `request_approval` creates a work item the human decides; `run_automation` runs only governed automations whose approval policy permits it; sending anything outward is a draft the human sends. Each tool's description reads like documentation for a new colleague, with examples and the boundary against the nearest tool, and its arguments are shaped so the common mistake is impossible.

**C3. Sandbox.** Azure Container Apps dynamic sessions, Python code interpreter (billed on runtime; custom container sessions run on dedicated compute; isolation details **[Unverified]**). The agent writes code to analyse, join and chart; data goes in as files; personal fields are tokenised before the model sees them; there is no network. Output comes back as artifacts: tables, charts, XLSX and PDF. The sandbox is created per run and billed on runtime, so nothing runs while nobody asks, and it is the only Python in the plan. It is also optional: Phase 3 can ship with .NET tools alone (tables and simple charts built in the API) and add the sandbox when a question needs real computation.

**C4. The "browser".** Not a general computer. Two things:
- A web research tool with a domain allowlist (regulator and reference sites), fetched server-side and returned as clean text with the source URL as the citation.
- A legacy-system bridge using Anthropic's computer-use pattern (client-run loop in a locked-down container, a virtual desktop, no credentials in the model's context, an allowlist, and an approval before any consequential click) for insurer core systems that have no API. Built only when a named insurer needs it.

**C5. The Run view.** On desktop and phone: the conversation on one side, a workbench on the other (phone: a bottom sheet) with four tabs, Steps, Artifacts, Sources and Approvals. The agent can also operate the product itself: a `show_in_app` tool pushes a navigation event that opens a claim, highlights a field or scrolls a table, so the agent appears to drive IIP the way Claude appears to drive a browser, with no pixel clicking. Follows `DESIGN.md`: hairline borders, one blue action, six states, a status dot plus a word, no emoji.

**C6. IIP Go.** Stay on the WebView shell, adding native pieces in this order:
1. Microphone permission and the WebView permission handler, so the web voice and dictation work.
2. Push notifications for approvals, call summaries and run completion.
3. A native file bridge for the downloads the current shell cannot make.
4. A Chrome Custom Tab sign-in, if Google sign-in is needed.
The mobile screen is `/app/go`: one thread, voice input, the Run view, and the approval card as the primary action.

**C7. Skills and memory.** Per-tenant playbooks (for example "triage a motor claim") stored as documents the agent reads on demand, so behaviour improves by editing a document, and a per-user memory of preferences. Both are scoped to the tenant and visible to its admins.

**C8. Safety.** Documents, emails and web pages are untrusted data: their text never grants a tool or changes a rule, tool results are labelled as data, and an instruction found inside a document is surfaced to the user instead of followed. Every tool call is an audit event. Each run has a visible cost, and a cap.

### 6.5 Workstream D: interactive documents

**D1. Fix the geometry first.** Store each page's width, height and unit with the document. Convert every polygon to normalised 0 to 1 coordinates using the page size, keep all points (not two corners), and keep every region of a field, not the first. Add the same for key-value pairs and table cells. Add a test with a recorded Azure response.

**D2. The viewer.** Render pages with PDF.js on a canvas (images directly), with an SVG overlay in page coordinates. Two-way link: hovering a field in the list lights the box on the page and scrolls to it; hovering a box lights the field row; click selects, and Enter opens the correction. Confidence is a dot and a word ("Check", "Verified"), not colour alone. Zoom, page thumbnails, and keyboard navigation. Six states: loading, empty, filtered-empty, error, not configured, no permission.

**D3. Correction and audit.** Edits write `DocumentFieldCorrection` with the before value, the after value, the user and the time, and the box that was on screen. The approved field list feeds the claim, as today.

**D4. Evidence everywhere.** Any assistant answer that cites a document field links to the document with that box already highlighted. The call agent's spoken facts do the same for transcripts.

**D5. Verification.** Open the screen in a browser at 1280 px and 768 px, run the 27-point checklist in `iip_ui_craft_guardrails.md` section 8, and add a Playwright spec that hovers a field and asserts the overlay.

### 6.6 Cross-cutting

- **Metering:** voice minutes, model tokens, sandbox seconds and Sunbird calls go through the existing metered recorders, so cost per call in shillings is a real number.
- **Observability:** per-call and per-run traces with latency per stage (listen, decide, think, speak), the number the proof page quotes.
- **Resilience:** idempotent webhooks, a call that outlives a deploy ends cleanly, and rate-limit handling for every external engine.
- **Security:** webhook signature checks, no secrets in the browser, injection handling in C8, and the media service in a private network with only the API and Meta reaching it.

---

## 7. Build order

Ordered by dependency, not by time. Each phase ends in something the owner can open and judge. Workstreams A and D have no dependency on each other and can run in parallel. Live calls are deliberately after the agent runtime and IIP Go (owner decision, 23 November 2026), though the Phase 0 gates should start now because Meta's verification takes calendar time.

| Phase | Contents | Done when |
|---|---|---|
| 0. Gates | Business verification with Meta to reach the 2,000 tier, and calling enabled on the number. Hosting spike: a small .NET WebRTC peer that completes a real call from a phone and carries audio both ways from the chosen host. A1 measurement from public corpora and staff clips. ADR for where the media service runs. | Meta confirms receive-only calling for +256. The spike carries audio. A1 numbers exist. |
| 1. Voice and language repair (A2 to A6) | Acceptance gate, arbiter, read-back, new languages, evaluation harness, voice-note fixes | A replay of the A1 clips passes the agreed thresholds, and the Kato field-test failures no longer reproduce |
| 2. Interactive documents (D1 to D5) | Geometry fix, viewer, correction audit, evidence links | Hovering a field lights the right words on a real Azure result, in a browser at both widths |
| 3. Agent runtime (C1 to C3, C7, C8) | Runs, events, streaming, toolbelt, sandbox, skills, injection handling | A run streams steps to a test client, resumes after a disconnect, and stops at an approval |
| 4. Run view and IIP Go (C5, C6) | Workbench UI, `/app/go`, mic, push | The adjuster scenario in Moment 3 works on an Android phone |
| 5. Live call, English first (B1 to B3, B5 to B8) | Signalling, media service, English path, tools, consent, hand-off by WhatsApp | A real WhatsApp call opens a claim in English and the adjuster sees it |
| 6. Live call, Luganda and the rest (B4, G1) | Cascaded path, phrase bank, per-language gates | The same call works in Luganda at the measured latency, or G1 applies and says so |
| 7. Proof and rehearsal | Proof page, demo script, load test, rehearsal against the five moments | Two clean runs of the full demo on production-like infrastructure |

Updating the owning documents happens in each phase's commit: the handbook for capability and edition changes, `DESIGN.md` for the Run view, the data-processing page for audio, and a new ADR for the media service and the agent runtime.

---

## 8. Risks

| Risk | Effect | Handling |
|---|---|---|
| Luganda call latency too high | Dead air, the demo fails | Gate G1 and the English-first branch; self-hosted engines; phrase bank |
| Luganda accuracy on street speech | Wrong facts | Read-back, entity validation, human review of every claim |
| Meta eligibility (2,000-recipient limit, credit line, Uganda unconfirmed) | No calls at all | Business verification lifts the portfolio to 2,000; Phase 0 confirms with Meta that this is enough for a receive-only number |
| Voice Live and Foundry regions are outside Africa | Cross-border transfer of insurance data | Owner accepted it (section 9); consent wording at the start of the call, a processor agreement with each provider, and a line on the data-processing page |
| Meta policy on AI assistants | Number at risk | Business-specific assistant only; the general assistant is in IIP Go |
| Webhook duplicates and ordering | Double claims, stuck calls | Idempotency by call id and event, timestamp ordering |
| Prompt injection through documents | Agent misled | C8: documents are data, never instructions |
| Beta and preview dependencies (Voice Live SIP, Managed Agents, some computer-use versions) | Breakage | None is on the critical path; all are behind a port |
| Meta concurrency limits conflict (1,000 or 5,000) | Capacity planning | Plan for 1,000, confirm in production |
| UDP media from a cloud host (outbound UDP from App Service is unverified; Container Apps has no inbound UDP and may drop return packets) | A call connects but carries no audio | Phase 0 hosting spike; fallback is the smallest Linux VM or container instance running only the media service |

### Honesty register

Do not claim:

- "First", "only" or "never existed". Say what we looked for and did not find.
- A language accuracy figure that A1 and A5 have not produced on our own recordings.
- That the agent decides claims. It prepares and a person decides.
- Uganda call availability, until Meta or the number confirms it.
- Real-time Luganda speech-to-speech. It is a fast cascaded pipeline.
- Data residency in Africa for audio, until the engines run there.

---

## 9. Decisions

### Taken (23 November 2026)

1. **Calls are inbound only.** IIP never calls anyone. Business-initiated calling, call permissions, per-minute call charges and call-backs are out of scope.
2. **Calls are a later phase.** Phases 1 to 4 come first.
3. **The number is a Cloud API number that probably cannot reach 2,000 by volume.** The route is Meta business verification, which lifts a portfolio to the 2,000 tier (section 3.1). Open point, to confirm with Meta in Phase 0: whether the 2,000 prerequisite is enforced for a number that only receives calls.
4. **No recordings exist.** The test set comes from public corpora and staff-recorded WhatsApp clips (A1).
5. **Call audio may be processed outside Africa.** This selects Voice Live for English. The conditions that still apply: a recording notice and consent at the start of the call, a processor agreement with each provider, a line on the data-processing page, and retention under the existing policy.
6. **Keep resources small.** No GPU, no self-hosted models, no second language stack. The media service is .NET, calls are relayed to remote engines, and Luganda uses Sunbird's API until a measured need says otherwise.

### Still open

1. **Meta business verification.** Who owns the business portfolio for the number, and are the registration documents ready? This is the longest lead item.
2. **Reasoning model for the insurer agent.** Keep Azure OpenAI or add Claude in Foundry. The port makes it a configuration choice; cost, residency and contract differ.
3. **Where the media service runs.** Decided by the Phase 0 spike: App Service, Container Apps, or the smallest Linux VM.
4. **Legacy-system bridge.** Build the computer-use bridge for a named insurer now, or leave it as a documented later option.
5. **Payments.** This plan keeps mobile-money premium payment out.

---

## 10. Unverified items to close early

- Uganda (+256) availability of Cloud API calling and business-initiated calling.
- Meta's current per-minute calling rate for the destination, and the concurrency and 100-calls scope conflicts.
- Voice Live locale list for Swahili, and whether any custom speech model reaches a live call.
- Whether Voice Live SIP supports tool calls, and when it leaves preview.
- Whether Claude 5 models are offered in Foundry and in which regions.
- Azure Container Apps dynamic sessions isolation and current prices.
- That the stored bounding boxes are not normalised (section 2.3).
- Whether Meta enforces the 2,000 prerequisite for a number that only receives calls.
- Whether a WebRTC peer on App Service or Container Apps can exchange UDP media with Meta (outbound UDP from App Service is not documented; Container Apps has no inbound UDP and one report says return packets can be dropped).
- That SIPSorcery can act as the controlling ICE agent and handle Meta's Opus offer (48 kHz) end to end. Its package lists Opus through Concentus, ICE and DTLS-SRTP, but no source shows this exact bridge.
- Licences of the public corpora (Common Voice Luganda CC0 comes from a third-party summary; one Makerere set is CC BY-SA 4.0).
- Sunbird's commercial terms, rate limits and retention (unchanged from the voice design).

---

## 10a. Build status (2026-11-23)

The remaining live voice work, the Voice Live resource and the realtime design (browser audio direct to Voice Live, IIP holding the control socket) are planned in [IIP_LIVE_VOICE_BUILD_PLAN.md](IIP_LIVE_VOICE_BUILD_PLAN.md), which supersedes the earlier idea of relaying audio through the IIP API.

Source-complete and locally verified (not deployed). Each line names what a person can now do and what is still unproven.

| Piece | State |
|---|---|
| Interactive document review | Done. The extraction provider's page positions are scaled to page fractions on the server (`DocumentGeometry`), key-value pairs now carry boxes, and the review screen shows the source with every located value outlined; hover or focus links the list and the page both ways, and a value with no position says so. 9 Playwright checks at 1280 px and 768 px, 9 domain tests. Not yet run against real Ugandan police abstracts. |
| Agent run stream | Done. `POST /api/v1/command-center/query/stream` answers with server-sent events (`step`, `tool`, `answer`, `error`, `done`); the analyst reports each real step. The portal falls back to `/query` when the stream is absent. |
| Presence and brief on open | Done. The orb shows Ready or Working on it; while it works the person sees each step; the home screen opens with a brief counted from the Command Center metrics (zero counts are left out, a quiet workspace says so). |
| Voice note | Done in the portal and the app shell. Recording in the browser, converted to 16 kHz WAV, transcribed through the tenant's Sunbird connection (`POST /command-center/voice-note`), words placed in the message box to read and send. Nothing is sent on its own and no audio is kept. Not yet run against the real Sunbird endpoint, so its acceptance of the WAV and its Luganda and English accuracy are unproven. |
| Live voice conversation | Built in two ways behind one screen: a realtime call (browser audio direct to Azure Voice Live over WebRTC, IIP holding the control socket and the tools) for English, and the turn-based engine for Luganda, Swahili and as the fall-back. The call lives in the app shell, so it continues across screens. Locally verified with a fake peer connection at 1280, 768 and 375 px; not run against a live Voice Live call. See `IIP_LIVE_VOICE_BUILD_PLAN.md` section 1a. |
| Android shell | Source changed (microphone for IIP's own origin only, haptic bridge, a foreground call service with Mute and Hang up, device lock sent to the page, `versionCode` 3). The Java compiles against the SDK; an APK now builds without Gradle (`android/build.sh`, `versionCode` 4 with Hey Kato, closed-app nudges and a narrow device credential) but nothing was run on a phone, and Gradle still could not start here ("Unable to establish loopback connection"). |
| Not done | Tuning "Hey Kato" on real recordings (the wake word and the closed-app nudges are built, untuned and never run on a phone); Luganda quality (the A1 measurement has not been run); every real-service and on-phone check listed in the live voice plan section 1b. |

## 11. Sources

- Meta, Cloud API Calling: <https://developers.facebook.com/documentation/business-messaging/whatsapp/calling>
- Meta, user-initiated calls, business-initiated calls, call permissions, SIP, FAQ and pricing pages under the same path
- Meta, Calling pricing: <https://developers.facebook.com/documentation/business-messaging/whatsapp/calling/pricing/>
- Microsoft, Voice Live over SIP: <https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live-sip>
- Microsoft, Voice Live FAQ: <https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live-faq>
- Microsoft, Voice Live overview: <https://learn.microsoft.com/azure/ai-services/speech-service/voice-live>
- Microsoft, Claude in Foundry: <https://azure.microsoft.com/blog/claude-in-microsoft-foundry-is-now-generally-available/>
- Microsoft, Document Intelligence analyze response: <https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/concept/analyze-document-response>
- Microsoft, Container Apps billing (dynamic sessions): <https://learn.microsoft.com/azure/container-apps/billing>
- Anthropic, Managed Agents overview: <https://platform.claude.com/docs/en/managed-agents/overview>
- Anthropic, Computer use tool: <https://platform.claude.com/docs/en/agents-and-tools/tool-use/computer-use-tool>
- Anthropic, Building effective agents: <https://www.anthropic.com/engineering/building-effective-agents>
- Anthropic, Code execution with MCP: <https://www.anthropic.com/engineering/code-execution-with-mcp>
- Sunbird, `asr-whisper-large-v3-salt`: <https://salt.sunbird.ai/models/asr-whisper-large-v3-salt/>
- Sunbird, SunflowerASR and Sunflower-Speech models on Hugging Face
- Meta, Messaging limits: <https://developers.facebook.com/documentation/business-messaging/whatsapp/messaging-limits>
- SIPSorcery (.NET WebRTC and SIP): <https://www.nuget.org/packages/SIPSorcery>
- Azure.AI.VoiceLive for .NET: <https://learn.microsoft.com/dotnet/api/overview/azure/AI.VoiceLive-readme>
- Makerere Radio Speech Corpus: <https://arxiv.org/abs/2206.09790>; Luganda and Swahili speech dataset: <https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12337013/>
- Pipecat: <https://github.com/pipecat-ai/pipecat>; LiveKit Agents: <https://docs.livekit.io/agents/> (considered, not chosen)
- Industry coverage of voice AI for first notice of loss (Hippo, insured.io, Retell, Branch), 2026

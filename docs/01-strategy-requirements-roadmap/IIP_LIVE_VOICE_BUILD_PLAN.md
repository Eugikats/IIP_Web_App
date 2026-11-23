# IIP live voice: build plan for the remaining gaps

How IIP gets from today's turn-based voice conversation to a realtime, tool-using, phone-ready live agent, including creating the Azure resource it needs. It closes the gaps found by comparing the working MIP hands-free design (`docs/handsfree-voice-implementation.md`) with what IIP has built.

- Written: 23 November 2026
- Status: built in one pass on 2026-11-23 and locally verified, not deployed, not run against a live Voice Live call or on a phone. The wake word and the closed-app nudges were added the same day, following the MIP app. Section 1a says what was built, what was verified and what is not built.
- Builds on: [IIP_INTELLIGENCE_LEAP_BUILD_PLAN.md](IIP_INTELLIGENCE_LEAP_BUILD_PLAN.md) (section 10a is the status). This plan **supersedes** that plan's idea of relaying audio through the IIP API (section 3 below says why).
- Authority: the human-decision boundary in `AGENTS.md` and `DESIGN.md`. The agent reads, shows and prepares. A person decides and sends.

Status markers: **[Verified]** read at the source or confirmed live on 2026-11-23. **[Unverified]** an assumption to prove at the step named.

---

## 1. Where things stand

Built, locally verified, not deployed (22 browser checks, 2466 API tests, 1295 domain tests):

- Command Center answer stream (`POST /api/v1/command-center/query/stream`), live steps, presence orb, brief on open.
- Staff voice note (`/voice-note`, `/voice-note/status`), transcribed through the tenant's Sunbird connection.
- Turn-based live conversation (`useLiveVoice`, `LiveVoiceSheet`, `AgentOrb`, start and end sounds, haptics, mute, minimise, transcript).
- Android shell source: microphone for IIP's own origin only, haptic bridge, version 2 (not rebuilt).

Not built when this plan was written, from the comparison: the realtime engine, the broker and its endpoints, the tool bridge, the locked-phone rule, paced captions and listening cues, on-screen visuals during a call, the standby call, rate limits and caps, API tests for the new endpoints, timing metrics, a conversation that survives leaving the workspace page, Luganda and Swahili turn by turn, the Android call service, wake word and nudges. Section 1a records which of these now exist.

## 1a. Build status (2026-11-23)

Built in one pass, locally verified, **not deployed**. The Azure resource exists (`ais-iip-dev-h5girbnzrayqe` in `rg-iip-dev`, Sweden Central, Cognitive Services User and Foundry User granted to the API's managed identity). No App Service setting was changed and nothing was released.

| Phase | State |
|---|---|
| A. Limits, mobile test project | Done. `VoiceLimits` (20 transcriptions a minute, 30 minutes of audio a day, 2 live calls, 15 minutes a call, all configurable under `Voice:Limits`); the voice-note endpoint answers 429 with a plain reason. Agent and documents Playwright configs have a 375 px mobile project. |
| B. Broker and endpoints | Done. `VoiceSessionBroker` and `VoiceToolbox` in Application behind `IVoiceLiveTransport` and `IVoiceScopeFactory`; `VoiceEndpoints` for status, prepare, start, lock, unlock, end, the screen event stream and timings; audit events carry the tool name and outcome only. Tools: `ask_analyst`, `make_visual`, `show_on_screen` (links only from earlier answers), `prepare_draft` (saved for approval, never sent: D4), `end_conversation`. While locked every tool but `end_conversation` returns locked. 53 API tests on the broker, toolbox, limits, timings and endpoints. |
| C. Browser engine | Done. `RealtimeVoiceEngine` (pure class, injected peer, microphone and API), paced captions, orb states from the data channel, reconnect twice, timing marks as numbers. 15 node unit tests. |
| D. Across screens | Done. `LiveVoiceProvider` in the app shell holds the call and a shared `liveThread`, so a call continues while the person moves between screens and the exchange is in the workspace thread afterwards. |
| E. Languages | Done as designed: English on the realtime path; Luganda and Swahili turn by turn through Sunbird with the reply shown first and spoken only when it could be translated and voiced. Luganda quality is not measured (leap plan A1 has not been run). |
| F1. Android call service | Source done: `VoiceCallService` (foreground service, type microphone, Mute and Hang up in the notification, a wake lock for at most 16 minutes), lock state from `KeyguardManager.isDeviceLocked()` sent to the page, `IipNative` bridge methods that act only for IIP's own site, notification permission asked once from the person's tap. Java compiles against the Android 35 SDK. An APK now builds with `android/build.sh` (SDK tools only, see F2), `versionCode` 4, but **nothing was run on a phone**. Gradle still cannot start in this environment, so the Gradle build is unverified. |
| F2. Wake word | **Built, not run on a phone, not tuned.** The phrase is "Hey Kato" only, one on-device tier instead of two (decision D2 changed on the owner's instruction to follow the MIP app). `WakeWordService` is a foreground microphone service running sherpa-onnx keyword spotting (the small English zipformer model, threshold 0.05, boost 2.0, five spellings of the phrase in `android/wake/keywords.txt`). A hit takes about 3 seconds of audio and posts it to `POST /api/v1/voice/device/wake`, which runs Azure Speech fast transcription with the API's managed identity and matches the transcript against the phrase (a greeting plus Kato or a near spelling, or the name alone). A "no" ends the conversation that was just opened; an unreachable server lets the phone's own match stand, a refused credential does not. Off until the person turns it on in Settings, This phone; asks for the microphone only then; shows an ongoing notification with Turn off; steps aside while a call runs or while the page itself holds the microphone; wake audio is never stored. The model and thresholds were checked on three synthesized clips only (fires on "Hey Kato", not on "hello" or "Hey, at last"). **They are untuned on real voices and accents, and Kato is a common name, so false wakes are expected until the thresholds are tuned on recordings.** The Azure Speech call with a managed-identity bearer is unproven: only a key was used in a smoke test. |
| G. Nudges | Built for the open app and, now, for the closed app on Android. `GET /api/v1/voice/nudge` returns the next nudge from the same brief the person sees (3 a day, 08:00 to 18:00 Africa/Kampala on weekdays, the same thing not repeated within 3 days, claimed under a lock so concurrent requests make one, private wording with no names, figures or link when the phone is locked). The portal asks six seconds after opening and every 20 minutes while visible and shows one line with Open, Hear it and Dismiss. **Closed app:** the portal's bearer token cannot be used by a background job, so the signed-in page asks `POST /api/v1/voice/device` for a narrow device credential (32 random bytes, only its SHA-256 stored, tied to the person's tenant context, 14 days sliding, at most 3 per person, revoked on sign-out) and hands it to the app, which keeps it in app-private storage (`allowBackup` off, https only). It can call only `GET /voice/device/nudge` (6 a minute) and `POST /voice/device/wake` (20 a minute) with header `X-Device-Check`. `NudgeReceiver` runs about every 40 minutes (`setAndAllowWhileIdle`, so Android may delay it), skips outside working hours, sends whether the phone is locked, and shows a notification with Talk now. On by default, can be switched off in Settings. **Limits:** the credentials and the nudge counts are in memory per API instance, so a restart or a second instance means the phone is refused once (the app clears it and the page asks again at its next open); Android 14 needs the person to allow full-screen notifications; nothing has run on a phone. |
| H. Verification | See section 1b. |

### 1b. What was run

- API tests: 2530 passed (the whole suite). Voice engine unit tests: 15 passed.
- Browser: agent suite 72 passed (24 checks on each of desktop, tablet at 768 px and mobile at 375 px, including the fake peer connection, consent, locked banner, language chooser, minimise across screens, nudges and no sideways scrolling); documents suite 13 passed.
- `pnpm check:*` release gates, `tsc --noEmit` and `next lint` clean (lint has only older image warnings elsewhere).
- Not run: a real Voice Live call, the SDP call-setup message pair (`rtc.call.sdp.create` and `rtc.call.sdp.created` come from the docs and are unproven; the realtime event shapes were proven on the plain socket), WebRTC through a real network (`iceServers` is empty so no third-party STUN server is used), a keyless call from the API identity, Sunbird's acceptance of the recorded WAV, Android WebView WebRTC, the Gradle build, any latency table on a phone.

### 1c. Settings to add when this is deployed (documented only, none set)

`Voice__Live__Endpoint` = `https://ais-iip-dev-h5girbnzrayqe.services.ai.azure.com`, `Voice__Live__Model`, `Voice__Live__ApiVersion`; optional `Voice__Live__SpeechEndpoint` (the Azure Speech host for the "Hey Kato" check; derived from the endpoint above when unset, and needs the API identity to hold a Cognitive Services Speech role on the resource), `Voice__Limits__*` and `Voice__Nudges__*`. Turn off key authentication on the resource only after the first keyless call succeeds.

## 2. Decisions the owner takes before the phases that need them

| # | Decision | Needed by | Recommendation |
|---|---|---|---|
| D1 | Create the voice resource (section 4) | Phase B | Yes, in Sweden Central, keyless. |
| D2 | Wake phrase on Android: "Hey Kato", "Hey IIP", or none this release | Phase F | Resolved 2026-11-23 on the owner's instruction to follow the MIP app: "Hey Kato", built, off by default. (Originally: none in this release.) Ship tap-to-talk and the call service first; the wake word is the largest single piece and needs real-phone tuning. |
| D3 | Spoken proactive nudges, yes or no | Phase G | No in this release. The brief on open covers it without a push channel. |
| D4 | Sending email from voice | Phase B | Never. Port the draft only (`prepare_draft`, saved for approval). The guide's `send_email` breaks the authority rule. |

D1 is the only one that blocks the main path.

## 3. Architecture (the change from the earlier plan)

The earlier plan had audio flow browser to IIP API to Voice Live. Microsoft's WebRTC page and the MIP guide both support something better **[Verified]**:

```
 Browser / app WebView                 IIP API (.NET)                         Voice Live (Foundry)
 ─────────────────────                 ──────────────                         ────────────────────
 mic ──── WebRTC audio (direct) ───────────────────────────────────────────▶ realtime model
 ◀──────── Atlas-style voice (direct) ◀──────────────────────────────────────
 POST /voice/session {sdp offer} ───▶  opens control WebSocket  ───────────▶  rtc.call.sdp.create
 ◀── {id, sdp answer} ◀──────────────  keeps the socket for the call  ◀────  rtc.call.sdp.created
 GET /voice/session/{id}/events (SSE) ◀ tool calls arrive here, run as the
                                        signed-in person, results go back
```

- The endpoint is `wss://<resource>.services.ai.azure.com/voice-live/realtime/calls?api-version=2026-07-15&model=<model>`. The offer goes in `rtc.call.sdp.create`, the answer comes back in `rtc.call.sdp.created`, and the same socket then carries `session.update` and tool calls. Audio is RTP; captions, voice activity and response events arrive on a data channel named `voice-live-events` **[Verified]**.
- **The browser never holds credentials.** Microsoft's page says resource credentials stay on the backend. Entra tokens are sent as a bearer header on the socket, which a browser cannot set, so IIP holds the socket **[Verified]**.
- **Audio does not touch our server**, so no media hosting, no GPU, no UDP on App Service, and no extra cost on the plan.
- Tools run in the API as the signed-in user, inside the read, show and prepare ceiling.

Risk: Microsoft states WebRTC uses global standard deployments that route to the nearest region, so audio may be processed outside Africa **[Verified]**. The owner already accepted this for calls with consent (leap plan, decision 5). The voice screen therefore shows a one-line notice the first time it is used. The page does not say whether the `2026-07-15` API version is preview or GA **[Unverified]**; treat it as changeable and keep it in configuration.

## 4. Creating the resource (Phase 0, needs the owner's go-ahead)

### 4.1 What is there today **[Verified live, 2026-11-23, subscription "Microsoft Azure Sponsorship"]**

| Resource | Kind | Region | Use for voice? |
|---|---|---|---|
| `oai-iip-dev-h5girbnzrayqe` | OpenAI | eastus | No. Voice Live needs a Foundry or Speech resource, not an OpenAI one. |
| `di-iip-dev-h5girbnzrayqe` | Document Intelligence | westus2 | No. |
| no AIServices or Speech resource | n/a | n/a | n/a |

The API app (`app-api-dev-h5girbnzrayqe`) has a system-assigned identity (`549d4a78-d4b2-48d1-94e3-4586b08853a9`) and already holds access to the OpenAI and Document Intelligence resources by role, with no keys. Voice follows the same keyless pattern.

### 4.2 Choices

- **Kind `AIServices`** (a Microsoft Foundry resource), tier S0. Microsoft recommends Foundry over a plain Speech resource and a Speech resource cannot use Foundry agents or bring-your-own-model **[Verified]**.
- **Region `swedencentral`.** Voice Live lists 31 regions including `westus2` and `swedencentral` **[Verified]**, so dev's West US 2 would work, but the audio control path and first-response latency matter for a person in Kampala and Sweden Central is roughly half the distance from Kampala that West US 2 is (the media itself is routed to the nearest region either way, so this mostly helps the control socket). It is also MIP's default voice region. `gpt-realtime` is a Global deployment there; the data-zone variant is not offered there **[Verified]**, which is consistent with section 3's risk. A resource in a different region from the rest of `rg-iip-dev` is normal (the OpenAI resource already is).
- **Keyless.** The resource is created with local (key) authentication disabled after the roles are confirmed working. Nothing goes in app settings except the endpoint and model name.
- **No model deployment** is created for the first run: Voice Live takes a built-in model name in the `model` query parameter **[Unverified for a new resource]**; if the first call says a deployment is needed, create one then and record it.

### 4.3 Exact commands (nothing has been run)

Names follow the existing suffix pattern.

```powershell
# 1. Confirm the role name this tenant uses (it was "Azure AI User" before the rename; "Foundry User" resolved on 2026-11-23).
az role definition list --name "Foundry User" --query "[].roleName" -o tsv

# 2. Create the Foundry resource. The custom domain gives the keyless endpoint https://ais-iip-dev-h5girbnzrayqe.services.ai.azure.com
az cognitiveservices account create `
  --resource-group rg-iip-dev `
  --name ais-iip-dev-h5girbnzrayqe `
  --kind AIServices --sku S0 `
  --location swedencentral `
  --custom-domain ais-iip-dev-h5girbnzrayqe `
  --yes

# 3. Give the API's managed identity the two roles Voice Live needs.
$scope = az cognitiveservices account show -g rg-iip-dev -n ais-iip-dev-h5girbnzrayqe --query id -o tsv
az role assignment create --assignee-object-id 549d4a78-d4b2-48d1-94e3-4586b08853a9 --assignee-principal-type ServicePrincipal --role "Cognitive Services User" --scope $scope
az role assignment create --assignee-object-id 549d4a78-d4b2-48d1-94e3-4586b08853a9 --assignee-principal-type ServicePrincipal --role "Foundry User" --scope $scope

# 4. After the first successful keyless call (Phase B), turn off key access.
az cognitiveservices account update -g rg-iip-dev -n ais-iip-dev-h5girbnzrayqe --disable-local-auth true
```

Roles: Microsoft's page says to assign **both** `Cognitive Services User` and `Foundry User`, and does not name either as enough alone **[Verified]**. Token scope is `https://ai.azure.com/.default`.

### 4.4 Settings, cost and undoing it

- **App settings** (set by the owner or on explicit request, because changing them restarts the API): `Voice__Endpoint=https://ais-iip-dev-h5girbnzrayqe.services.ai.azure.com`, `Voice__Model=gpt-realtime-mini`, `Voice__ApiVersion=2026-07-15`, `Voice__MaxSessionMinutes`, `Voice__MaxSessionsPerUser`. Production later gets its own resource.
- **Cost:** the resource has no standing charge; cost is per audio and token use while a call runs. Pricing and quota are not on the Microsoft pages read **[Unverified]**; check the price sheet before the first long test, set a budget alert on the resource group, and rely on the per-user caps in Phase A.
- **Undo:** `az cognitiveservices account delete` then `az cognitiveservices account purge` (deleted resources are soft-deleted). Nothing else depends on it until Phase B ships.
- **Not touched:** the App Service plan size, the deployed apps, the Azure OpenAI resource and the pending organisation-platform migration on dev.

## 5. Build phases (ordered, each leaves the product working)

### Phase A. Harden what exists (no Azure needed)

1. **Limits.** Per-user voice limits in one `VoiceLimits` service: transcriptions per minute (suggest 20), per day, and audio seconds per day; the live loop and `/voice-note` both use it; an over-limit answer says what to do ("You have used today's voice minutes. Type instead, or ask an administrator."). Record usage with the existing usage recorders so billing sees it; check whether Sunbird calls should draw on AI credits and decide with the owner.
2. **API tests** for the three endpoints that only have browser stand-ins: the stream (`step`, `tool`, `answer`, `error`, `done` order; permission refusal as an `error` event; credits-used-up answer), `/voice-note` (no audio, too large, not configured, not understood, success, em dashes removed) and `/voice-note/status`. Add a real-server Playwright run against the API on `localhost`.
3. **Android build.** Make Gradle start (the "Unable to establish loopback connection" failure; try the Android Studio JBR as `JAVA_HOME` in PowerShell, a project-local `gradle-wrapper` pinned to 8.9 as the README says, and disabling the IPv6 loopback), then build and `aapt dump badging` to confirm the permissions. Replace the repo-root `IIP.apk` only when asked.
4. **Gesture test.** A Playwright touch test for swipe down (transcript) and swipe up (orb).
5. **Timing marks** in the client: tap, mic ready, offer sent, answer received, connected, first spoken audio, each posted as numbers only to `POST /api/v1/voice/metrics`; an admin read of p50 and p95. Budgets: call ready under 2 s, first words under 3.5 s.

Done when: limits refuse correctly under test, the new endpoints have passing API tests, the APK builds and shows `RECORD_AUDIO`, `VIBRATE` and versionCode 2.

### Phase B. The voice broker (needs the resource from section 4)

New in `InsuranceIntelligence.Application/Voice` (rules) and `InsuranceIntelligence.Api/Voice` (endpoints and socket):

1. `VoiceLiveOptions`, a token provider using the API's managed identity for scope `https://ai.azure.com/.default`, cached until five minutes before expiry.
2. `VoiceSessionBroker`: opens the control socket (`ClientWebSocket`) on `prepare`, so the socket is warm by the time the offer arrives; on `session` sends `rtc.call.sdp.create` with the offer and the session settings, returns the answer.
3. Session settings: instructions, voice, `azure_semantic_vad` with `silence_duration_ms` 500 and `interrupt_response` true (the MIP guide measured 350 ms cutting in mid-sentence), `azure_deep_noise_suppression`, `server_echo_cancellation`, and the tool list.
4. **Instructions carry the immutable platform preamble** (the human-decision boundary, as `AgentBehaviourComposer` does for every agent), then the workspace's agent profile. A tenant never edits the preamble.
5. Endpoints: `POST /api/v1/voice/session/prepare`, `POST /api/v1/voice/session`, `POST /session/{id}/ready`, `GET /session/{id}/events` (SSE of screen events), `POST /session/{id}/lock` and `/unlock`, `POST /session/{id}/end`, `POST /api/v1/voice/metrics`. All tenant-scoped, all behind `ViewCommandCenter`; a session belongs to the user who opened it and nobody else can read its events.
6. **Session registry** with the Phase A limits: sessions per user, maximum minutes, idle close, close on socket drop.
7. **Tool bridge.** The socket loop runs outside the HTTP request, so each session captures the `TenantContext` and user at creation and sets it on every tool call; a tool call without a live session context is refused. Tools:

   | Tool | Behaviour | Ceiling |
   |---|---|---|
   | `ask_analyst` | calls the same `ICommandCenterConversationService.QueryAsync` as typing, so answers match | read only |
   | `make_visual` | returns chart or table blocks drawn only from tool results | read only |
   | `show_on_screen` | an event that opens a claim, queue or document in the app | navigation only |
   | `prepare_draft` | creates the existing saved-for-approval customer draft and reads it back | prepare only, never sent |
   | `end_conversation` | ends the call | none |

   Arguments are validated against a schema; a refused or failed tool returns a plain reason to the model, never an exception text. Nothing the model says can raise its own ceiling.
8. **Locked-phone rule.** Every tool except `end_conversation` returns `{ "locked": true }` while the session is locked; this is enforced in the toolset and tested there, not only in the instructions.
9. **Persistence.** On end, the transcript is saved as turns of a Command Center conversation (existing `ICommandCenterConversationRepository`) so it appears in the person's chats; audit events for session start, each tool call and end; **no audio is stored**.
10. Tests: a scripted fake Voice Live socket in the API tests (offer to answer, tool call to result, interruption, drop and cleanup, locked refusal, over-limit refusal, cross-user event access refused, tenant isolation). A manual first call on dev closes the unverified items in section 4.2.

Done when: a call placed from a test page on dev reaches Voice Live keyless, a spoken question runs `ask_analyst` as the signed-in user, and the locked test passes.

### Phase C. Realtime client engine

1. Split `useLiveVoice` into an engine interface (`start`, `end`, `mute`, `lock`, events) with two implementations: the existing turn-based engine (kept as the fallback when the broker is not configured or the browser lacks WebRTC) and a new WebRTC engine.
2. WebRTC engine: `prepare` on tap; get the microphone in parallel with creating the offer using a transceiver then `replaceTrack`; wait at most 300 ms for the first candidate, not full gathering; apply the answer; open the `voice-live-events` data channel.
3. Orb states come from the data channel (speech started and stopped, response started and done, interrupted) instead of the client's own voice check.
4. **Paced captions** (about 16 characters a second, whole words only; flush on interruption) because response text arrives well ahead of the audio; treat "output audio buffer stopped" as "text done", never as "finished speaking" (both are in the MIP guide's measured quirks list).
5. Listening cues ("mm-hmm"), paced by the guide's rules, behind a setting.
6. Screen events from the SSE feed: `visual` and `show_on_screen` render in the sheet using the existing `CommandCenterBlocks`, so voice and chat look the same.
7. **Standby call** while a person has touched the page in the last five minutes (no microphone, nothing sent) so a tap only attaches the microphone; close it after about 110 s and when the page is hidden. Build this last in the phase, after the timing marks show it is worth it.
8. First-use notice about processing outside Africa; the sheet shows "Not configured" with the reason when voice is unavailable.
9. Tests: the engine takes an injected peer-connection factory, so Playwright drives it with a fake peer and a scripted broker (states, caption pacing, interruption, drop and reconnect). The real WebRTC path is proved by a manual call on dev, recorded in the readiness table.

Done when: on dev, a person speaks, hears an answer in under the budget, interrupts it, and sees a chart appear during the call.

### Phase D. A conversation that survives navigation

1. Move the live conversation state out of `KatoWorkspace` into a provider mounted in the app shell; the sheet and the minimised bar render there on every screen.
2. Replace the direct `push` into the workspace thread with a small store the workspace reads, so exchanges made while elsewhere are in the thread when the person returns.
3. Tests: start on the workspace, navigate to claims with the bar showing, return, and see the exchange in the thread.

### Phase E. Ugandan languages, turn by turn

1. A language choice (English, Luganda, Swahili) in the sheet, remembered per device. English stays on the realtime path.
2. Luganda and Swahili run turn by turn through the broker's local mode: transcribe with Sunbird, answer through the same analyst, speak the reply with Sunbird speech (`IVoiceNoteSpeaker`, measured at about 12 seconds for a short reply, so the orb says "Working on it" with the step and the reply is text first, voice after).
3. Plain English questions skip the language check, as the guide does.
4. This depends on the language-arbiter design in the leap plan; do not claim Luganda quality until the A1 measurement there has been run.

### Phase F. Android call and, optionally, wake word

1. **Always:** a `VoiceCallService` (foreground service, type microphone) so a call continues with the phone in a pocket, with Mute and Hang up in the notification; permissions `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MICROPHONE`, `POST_NOTIFICATIONS`; lock state from `KeyguardManager.isDeviceLocked()` (not the keyguard-showing flag, which Samsung reports wrongly while the app covers it) sent to the page, which posts `lock` and `unlock`. Settings prompts follow the guide: one permission at a time, the person's own tap, never set by adb.
2. **Built as "Hey Kato", one tier (see 1a, F2):** sherpa-onnx keyword spotting in a foreground service with two tiers, `POST /api/v1/voice/wake` doing the second-stage check with Azure Speech fast transcription (reuse one kept-alive HTTP client, which the guide measured at 0.4 s against 2.2 s for a fresh connection), a 20 a minute limit per person, and a fail-open rule that opens only on a clear first-tier match when the server is unreachable. The keyword file and thresholds need tuning on the chosen phrase with real recordings in several accents, so this phase carries a test set and not only code.
3. The digital-assistant role is a later, separate decision (it needs the person to choose IIP as their assistant).

### Phase G. Spoken nudges (only if D3 is yes)

Source from the same metrics as the brief; at most three a day, working hours only, not repeated within three days, claimed under a lock so concurrent requests make one; Web Push for the web and a periodic check on Android; a private wording with no figures when the phone is locked; the audio made when the nudge is created, not when played.

### Phase H. Verify, document, release gates

1. Latency table on a real phone (MIP open, background, locked) against the budgets.
2. Update the handbook, `DESIGN.md` (orb states in a call), the Android README, the leap plan's section 10a and the readiness blueprint's honesty register (what must not be claimed: Luganda voice quality, realtime on production, anything not run on a phone).
3. Run the 27-point UI guardrail checklist at 1280 px and 768 px for each new screen, `pnpm test`, the .NET suites, and `pnpm exec playwright test` for the agent, documents and voice configs.
4. Regenerate `docs/CATALOG.md`.

## 6. Cross-cutting rules (carry into every phase)

1. No business figures over a locked phone, enforced in the toolset and tested.
2. Nothing is sent without a person: no email, message or status change from voice. Drafts only.
3. Nothing invented: every figure, chart point and link comes from a tool result.
4. Audio is not stored; transcripts are saved as text in the person's own conversations; metrics are numbers only.
5. No keys in code or app settings; managed identity for Azure, the tenant connection for Sunbird.
6. Tenant isolation on every endpoint and every tool call; a session cannot be read or ended by another user.
7. Caps on sessions, minutes and checks per person, with an honest message when reached.
8. No emoji and no em dashes in any user-visible text, including model output (the provider boundary already cleans completions).

## 7. Readiness stages

| Stage | Meaning for this plan |
|---|---|
| Source-complete | Phases A to E merged with their tests. |
| Locally verified | Suites pass against a scripted Voice Live socket. |
| Deployed to dev | Resource created, settings set, API and portal deployed (explicit request only). |
| Verified on dev | A real call on a real phone, latency table filled, locked-phone test run on a device. |
| Production candidate | A production resource, budgets and alerts, privacy notice approved, Phase H complete. |

## 8. Open items to prove

- Whether a built-in realtime model needs a deployment on the new resource.
- Whether `api-version=2026-07-15` is preview or GA, and whether its WebRTC behaviour changes.
- Price and quota for `gpt-realtime-mini` and `gpt-realtime` on a global deployment.
- Whether the Sunbird transcription endpoint accepts the 16 kHz WAV the portal sends, and its accuracy on Luganda and English (carried over from the voice-note work).
- Whether an Android WebView on the target phones completes the WebRTC exchange as Chrome does.

## 9. Sources

- Microsoft, Voice Live over WebRTC: <https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live-webrtc>
- Microsoft, Voice Live how-to: <https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live-how-to>
- Microsoft, Speech service regions (Voice Live tab): <https://learn.microsoft.com/en-us/azure/ai-services/speech-service/regions>
- Microsoft, Voice Live FAQ: <https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live-faq>
- `docs/handsfree-voice-implementation.md` (the MIP design this plan is measured against)
- Live Azure state read on 2026-11-23 with `az` (subscription "Microsoft Azure Sponsorship", `rg-iip-dev`)

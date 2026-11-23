# Hands-free voice (MIP Live + "Hey Atlas"): implementation guide

How MIP's hands-free voice works and how to bring it to another platform. It is written from the working
implementation in this repo (November 2026) and gives every file path, endpoint and setting. It also records the
measurements and mistakes behind each choice, so you don't repeat them.

The assistant is called **Atlas**. "MIP Live" is the voice conversation. "Hey Atlas" is the wake phrase on Android.

---

## 1. What the person experiences

1. **Starting a conversation:** tap the waveform button in the chat box, or say **"Hey Atlas"** on Android. Atlas
   greets them by the name they chose and listens.
2. **Talking:** they speak naturally and can interrupt at any time. Atlas answers aloud while a paced transcript
   appears. During longer speech it gives a short "mm-hmm" so they know it is listening.
3. **Visuals and actions:** Atlas can open pages and draw charts or cards on screen. It can prepare an email, read it
   back, and send it only after a clear "yes".
4. **Phone locked:** Atlas still talks, but gives **no business figures** until the phone is unlocked. This is
   enforced on the server, not only in the prompt.
5. **Atlas speaks first:** when the person's data has something urgent ("Hey Fil, heads up…"), Atlas says it aloud.
   This is at most 3 times a day, during working hours.
6. **Same everywhere:** every workspace has the same chat box, voice and visuals.

---

## 2. Architecture

```
 Phone / browser                       Platform server (Flask)                     Azure
 ───────────────                       ───────────────────────                     ─────
 [mic] ──WebRTC audio (direct)────────────────────────────────────────────────▶  Voice Live
   ▲                                                                              (gpt-realtime-mini,
   └──────────── Atlas's voice (direct) ◀──────────────────────────────────────    Azure voices)
 page ── POST /api/voice/session (SDP offer) ──▶ opens Voice Live control socket ──▶ │
      ◀─ SDP answer ─────────────────────────── configures session, relays offer  ◀── │
      ── data channel events (captions, VAD) ◀────────────────────────────────────── │
 page ◀─ GET /session/<id>/events (screen events) ◀── tool calls arrive on the socket, run as the
                                                       signed-in person (data tools, visuals, email draft)
 Android "Hey Atlas" (on-device KWS) ── POST /api/voice/wake (3 s clip) ──▶ Azure Speech fast transcription
```

The key design decisions:

- **Audio never passes through our server.** The browser sends a WebRTC offer, our server relays it to Voice Live,
  and the audio goes phone ↔ Azure directly. Our server keeps only the *control* WebSocket.
- **Tools run on the server as the signed-in user.** The model never gets credentials. Every tool checks the
  person's workspace access, as the typed chat does.
- **One brain for chat and voice.** The voice tools call the same analyst and data functions as the typed chat, so
  their answers match.
- **Server-side safety.** The locked-phone rule, the email confirmation and the rate limits are all enforced in
  code, not only in instructions.

### Files

| Concern | File |
|---|---|
| Endpoints | `pulse/voice/api.py` (blueprint `/api/voice`) |
| One live call (control socket, config, tools, events) | `pulse/voice/session.py` |
| Tools Atlas can call | `pulse/voice/tools.py` |
| Voices, tones, paces | `pulse/voice/voices.py` |
| Settings (endpoint, model, tokens) | `pulse/voice/config.py` |
| Preferences, logs, nudges (SQLite) | `pulse/voice/store.py` |
| Spoken nudges | `pulse/voice/nudge.py` (+ `pulse/scheduler.py`) |
| Ugandan languages (turn by turn) | `pulse/voice/local.py`, `pulse/lang/*` (Sunbird) |
| Persona and name | `pulse/persona.py` |
| Web client (everything on the page) | `static/voice.js`, `static/voice.css` |
| Chat box and streaming | `static/mip-core.js` |
| Push | `static/sw.js`, `pulse/voice/push.py` |
| Android | `mobile/android/src/com/mip/platform/*.java`, `mobile/android/wake/keywords.txt`, `mobile/android/build.sh` |

---

## 3. Server: brokering a Voice Live call

### 3.1 Settings

| Variable | Meaning | Default |
|---|---|---|
| `MIP_VOICE_ENDPOINT` | Azure AI Foundry / AI Services endpoint (`https://<name>.services.ai.azure.com`) | required |
| `MIP_VOICE_MODEL` / `MIP_VOICE_MODEL_PRO` | realtime model | `gpt-realtime-mini` / `gpt-realtime` |
| `MIP_VOICE_REGION` | region of the resource | `swedencentral` |
| `MIP_VOICE_SILENCE_MS` | end-of-turn silence | `500` |
| `MIP_VOICE_MAX_MINUTES`, `MIP_VOICE_MAX_SESSIONS` | per-person limits | see `config.py` |
| `MIP_VAPID_PUBLIC` / `MIP_VAPID_PRIVATE` | Web Push keys | for nudges and check-ins |
| `SUNBIRD_*` | Ugandan languages; the key is a **Key Vault reference** | optional |

**Authentication:** use the web app's **managed identity**, never keys. Voice Live uses the scope
`https://ai.azure.com/.default` and Speech uses `https://cognitiveservices.azure.com/.default`. Tokens are cached
until 5 minutes before they expire (`config.token`).

### 3.2 Call flow

1. **`POST /api/voice/session/prepare`**, sent the moment someone taps or the phone hears "Hey Atlas". The server
   opens the Voice Live control WebSocket in the background:
   `wss://<endpoint>/voice-live/realtime/calls?api-version=2026-07-15&model=<model>`.
2. **`POST /api/voice/session`** with `{sdp, voice, mode, page, history, question, locked}`. The server takes the
   prepared socket and sends `session.update` with the instructions, voice, turn detection, noise suppression, echo
   cancellation and the tool list. It then relays the SDP and returns `{id, sdp, timing}`.
3. **`POST /session/<id>/ready`**, sent once the audio line is connected. Atlas then speaks first: the greeting uses
   the person's chosen name.
4. **During the call:**
   - `GET /session/<id>/events`: screen events such as visuals, pages to open, email drafts and the end of the call.
   - `POST /session/<id>/unlock`: the phone was unlocked, so figures are allowed again.
   - `POST /session/<id>/metrics`: timings only (numbers, never words).
5. **`POST /session/<id>/end`.** The conversation is saved as text in the person's chats; audio is never stored.

The session config (`session.py`):

```python
{
  "instructions": ...,                       # persona + workspace context + locked note if locked
  "voice": {"name": "en-US-...", "type": "azure-standard", "temperature": .., "rate": ..},
  "turn_detection": {"type": "azure_semantic_vad", "silence_duration_ms": 500,
                     "interrupt_response": True, "remove_filler_words": True},
  "input_audio_noise_reduction": {"type": "azure_deep_noise_suppression"},
  "input_audio_echo_cancellation": {"type": "server_echo_cancellation"},
  "tools": [...],
}
```

**Turn detection, measured:** with 350 ms of silence, a half-second pause mid-sentence ended the turn and Atlas cut
in. 500 ms held a long sentence together at a cost of 150 ms.

### 3.3 Tools (`pulse/voice/tools.py`)

Each tool is a plain function that runs **as the person**. It returns `(result_for_model, screen_event_or_None)`.

| Tool | What it does |
|---|---|
| `ask_analyst` | the same analyst as the typed chat (long questions run in the background with spoken progress) |
| data tools | supply-chain views, CRM, group overview, morning brief, economics |
| `make_visual` | `{chart: bar/line/donut/kpi/table, labels, series, unit, note}` → drawn on screen |
| `show_on_screen` | opens a page in a panel over the current page; the call carries on |
| `read_screen` | what the page in front of them shows (the page posts it to `/api/voice/screen`) |
| `prepare_email` → `send_email` | the draft is read back; `send_email` works only on a draft under 10 minutes old, by the same person |
| `end_conversation` | ends the call |

**Locked phone:** `LOCKED_ALLOWED` is a short list of tools that give no business information. Every other tool
returns `{"locked": true, ...}` until `/unlock` is called. Test this rule in the toolset itself
(`tests/test_wake.py::test_the_toolset_itself_refuses_while_locked`).

---

## 4. Web client (`static/voice.js`)

### 4.1 Starting a call fast

These are the timings we measured. Build for them from day one.

| Step | What we do |
|---|---|
| Tap | `POST /session/prepare` straight away (server opens the Voice Live socket in parallel) |
| Microphone | **fetched in parallel** with the offer: `pc.addTransceiver('audio', {direction:'sendrecv'})`, then `sender.replaceTrack(micTrack)` when ready. On Android, handing the mic over from the wake listener takes 0.5–1.6 s, so never wait for it before sending the offer. |
| ICE | don't wait for full gathering: wait at most 300 ms for the first host candidate (Voice Live is on a public address) |
| Standby | while a voice user is on the page (touched in the last 5 min), keep **one standby call** connected with no mic and nothing sent. A tap only attaches the mic, so Atlas listens in about 0.3 s. Close it after about 110 s, then set up another. Drop it when the page is hidden. |

Each step is marked (`mark('mic'|'offer'|'session'|'connected'|'verdict'|'ready'|'speaking')`) and reported to
`/metrics`. Admins can read p50/p95 at `GET /api/voice/metrics`.

### 4.2 Voice Live event quirks (important)

- The text of a reply arrives in **bursts**, well ahead of the audio. If you print it as it arrives, the captions
  run ahead of the voice. **Pace captions** at about 16 characters a second (26 when more than 600 behind), whole
  words only. Flush on interrupt or when the call stops.
- `output_audio_buffer.stopped` arrives **when the text is done, not when the voice stops**. Never treat it as
  "finished speaking".
- `output_audio_buffer.cleared` means the person interrupted, so flush the captions.

### 4.3 Listening cues ("mm-hmm")

The model gives the backchannel, and the client paces it so it doesn't feel robotic:
`CUE_RULES = { minimal: {speech: 3000, gap: 12000, perTurn: 1}, natural: {speech: 1800, gap: 6000, perTurn: 2} }`.
Wait 300 ms after a breath. Drop a lone "mm-hmm" heard within 4 s of Atlas's own. Short questions end the turn
before a cue fits; that is expected.

### 4.4 Screen events

`visual` and `chart` events are drawn into the conversation sheet. Each card has Expand, Share link, Open in chat
and Email this. The same renderer (`renderBody`) draws ` ```mip-visual ` blocks in the typed chats, so chat and voice
look identical. The typed agents are told how to write them in `persona.VISUALS`.

### 4.5 Dictation (the mic in the chat box)

The mic in the chat box is dictation, not a call. MediaRecorder posts to `POST /api/voice/transcribe` (Azure Speech
fast transcription, or Sunbird for Ugandan languages), and the text lands in the box.

---

## 5. Android: "Hey Atlas"

### 5.1 Two-stage wake

1. **On the phone (free, private):** sherpa-onnx keyword spotting with
   `sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01`, running in `WakeWordService`, a foreground service of type
   `microphone`. **Nothing leaves the phone while it listens.**
   - Keywords (`mobile/android/wake/keywords.txt`) have two tiers:
     - `HEY_ATLAS`: open at once and check at the same time.
     - `ATLAS` alone (far away, echo): check first, then open.
   - Settings: threshold 0.05 and score 2.0, deliberately loose (tuned on 84 phrases in 12 English accents). The
     second stage removes near misses.
   - The service keeps a 3-second ring buffer of audio.
2. **On the server:** `POST /api/voice/wake` (multipart `audio` = 3 s WAV at 16 kHz, `tier` = `hey|atlas`). Azure
   Speech fast transcription, then `WAKE_RE`.
   - It accepts misheard greetings such as "Pay Atlas" and "They, Atlas" and rejects "Hey, at last…".
   - With `tier=hey`, a lone "<word> Atlas" also counts (`WAKE_CLOSE`).
   - Rate limit: 20 checks a minute per person.
   - **Reuse one kept-alive HTTP session to Speech.** A fresh connection costs 2.2 s; a reused one 0.4 s. Warm it
     when the phone sends `/session/prepare`, and from the scheduler every 60 s.

**On the phone:** if the server can't be reached, a clear `HEY_ATLAS` still opens; a lone `ATLAS` does not.
(Counting failures as "no" dropped real wakes in testing.) Keep the HTTP connection alive (don't call
`disconnect()`).

The page holds Atlas's voice and the microphone until `MIPAndroid.wakeState()` says yes (or 6 s pass). On "no",
the sheet closes; MIP goes to the background only if it wasn't already open.

### 5.2 What Android allows (the hard limits)

| Situation | What happens | What's needed |
|---|---|---|
| MIP on screen | conversation opens in place | nothing |
| Phone locked, screen off | full-screen intent opens MIP over the lock screen (`setShowWhenLocked`) | `USE_FULL_SCREEN_INTENT` |
| MIP in the background or swiped away, phone unlocked | **Android blocks a background app from opening itself**; only a "tap to talk" alert shows | **"Appear on top"** (`SYSTEM_ALERT_WINDOW`) or MIP as the **digital assistant** |
| MIP closed / after a restart | only the default assistant may listen | **digital assistant** (`AtlasAssistantService`, `BIND_VOICE_INTERACTION`) |

Only the person can grant these. **Never set them with adb or by other means.** MIP asks the way it asks for the
microphone (`MainActivity.askWakeSettings`): one at a time, with where to tap. "Not now" waits 3 days, and MIP
confirms whether the setting took when the person comes back.

Other Android lessons:

- **Start the listener only once MIP has window focus on an unlocked phone.** Started from `onResume`, Android
  refuses the mic (`createRecord -1`).
- **Samsung reports the keyguard as not showing while MIP covers it.** Use `KeyguardManager.isDeviceLocked()` to
  decide "locked".
- **While the page uses the mic** (dictation or a call), the listener pauses. The page tells it through
  `MIPAndroid.micInUse(on)`. The flag only counts while MIP is on screen, and is cleared if a start comes to nothing,
  so the listener can never be left paused.
- **Calls continue in the background** as a phone-call-style foreground service (`VoiceCallService`), with Mute and
  Hang up in the notification.

### 5.3 Measured latency (phone, MIP open, Nov 2026)

| Step (from the page starting) | Before tuning | After |
|---|---|---|
| Call ready | 3.2–4.0 s | 1.9–3.8 s |
| Atlas's first words | 4.0–5.6 s | 3.1–5.1 s |
| Word check (after the phone hears it) | ~3.7 s | 1.0–1.9 s |

The remaining time is mostly the session set-up (Voice Live's SDP exchange, about 0.7 s on the server) and the
model's first audio (0.9–2 s).

---

## 6. Atlas speaks up (nudges)

- **Source:** the person's own "On your radar" cards. One card per nudge, with the same figure never repeated within
  3 days.
- **Limits:** at most 3 a day, 2 hours apart, 08:00–19:00 Kampala time, Monday to Saturday. On by default; each
  person can switch them off.
- **Wording by tone:** critical gets "Hey Fil, heads up…"; warning gets "did you know…?"; good news gets "some good
  news from your data…"; anything else gets "according to your data, I think this is worth a look".
- **Private line:** when the phone is locked, Atlas says a version with no figures: "I've spotted something in Supply
  Chain…".
- **Delivery:**
  - Web: Web Push. The service worker hands it to a visible page, which says it aloud; otherwise it shows a
    notification with "Talk now".
  - Android: its own check about every 40 minutes. It speaks only when the ringer is on normal and not in a call.
- **Audio:** each clip is made **when the nudge is created**, not when it is played. On demand it took more than 4 s.
- **Duplicates:** claims go through a lock, so 30 requests at once produced exactly 1 nudge in the stress test.

---

## 7. Email from voice and chat

- **Voice:** `prepare_email` → Atlas reads it back → a clear yes → `send_email`.
- **Chat:** every answer has **Email**, and typing "email this to Nesta" also works. Both make a draft that sends
  only when the person presses **Send**. Endpoints: `POST /api/voice/email/prepare`, `POST /api/voice/email/send`.
- **Visuals in email** (`pulse/mail_visual.py`) are drawn as **HTML tables**: bars, columns, shares, tiles. Outlook,
  Gmail and phone mail don't display SVG and often hide images, so never send charts as SVG.
- **Sending:** Azure Communication Services through the web app's identity, with Microsoft Graph as the fallback.

---

## 8. Languages

English uses Voice Live end to end. Luganda, Runyankole, Acholi and Ateso go **turn by turn** through Sunbird:
the person speaks, pauses, and Atlas answers in their language, a few seconds slower (`pulse/voice/local.py`).
Plain English questions skip the language check (`bridge.looks_english`).

---

## 9. Rules that must carry over

1. No business figures over a locked phone. Enforce it in the toolset, and test it.
2. Nothing is sent without the person confirming: emails, quotes, anything leaving the platform.
3. Nothing is invented. Every figure comes from a tool, and every visual is drawn only from tool results.
4. Audio is not stored. Conversations are saved as text in the person's chats. Metrics are numbers only.
5. No keys in code: use the managed identity, and Key Vault references for third parties.
6. Phone permissions and the assistant choice are the person's own tap.
7. Rate-limit the wake check and cap call minutes and sessions per person.

---

## 10. Testing

| What | How |
|---|---|
| Unit | `tests/test_voice.py`, `tests/test_wake.py` (wake words, locked toolset, rate limit), `tests/test_nudge.py`, `tests/test_mail_visual.py` |
| Browser end to end | `tests/e2e/web.js voice`: headless Chrome with `--use-fake-device-for-media-stream --use-file-for-fake-audio-capture=q1.wav`. **Pad the WAV with 90 s of silence**, or it loops and re-asks. |
| Captions and cues | `tests/e2e/cap.js`, `tests/e2e/bc.js` |
| Push | `tests/e2e/swtest.js`: headless Chrome shows no notifications, so the service worker runs in a node vm |
| Load | `tests/e2e/stress.py` (radar polling, concurrent streams, wake rate limit, nudge claims) |
| Phone | `tests/e2e/phone/`: adb, plus spoken clips played from a laptop speaker in many accents. Test all four situations: MIP open, background, swiped away, locked. |

The demo login is shared, so run voice tests one at a time.

---

## 11. Porting checklist

- [ ] Azure AI Foundry resource with Voice Live and Speech in the same region; the web app's identity has access.
- [ ] Server: `/session/prepare`, `/session`, `/session/<id>/ready|events|end|unlock|metrics`, `/wake`,
      `/transcribe`, `/email/prepare|send`.
- [ ] Toolset that runs as the user, with the locked-phone list and a test for it.
- [ ] Client: parallel mic and offer, a short ICE wait, the standby call, paced captions, listening cues, screen
      events, an end-of-call summary saved to chats.
- [ ] One visual format (`mip-visual`) used by voice, chat and email.
- [ ] Android: sherpa-onnx keyword service (foreground, microphone) with two tiers, the server check (tier, kept-alive
      Speech connection), the settings prompts, and the assistant service.
- [ ] Nudges: from the platform's own insight cards, with limits and a lock; clips made at creation.
- [ ] Timing marks reported from day one; budgets of call ready under 2 s and first words under 3.5 s.

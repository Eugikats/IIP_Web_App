import { CaptionPacer } from "./captionPacer.ts";
import type { ChannelLike, EngineDeps, EngineHandlers, MicrophoneLike, PeerLike, VoiceOrbState } from "./types.ts";

/**
 * A live conversation over WebRTC. The phone's audio goes straight to the voice service; this class sets the call up,
 * turns what the service reports on its event channel into the orb, the words and the steps on screen, and lets the
 * person mute, lock and end. It acts on nothing: what the agent does, it does through the server as the signed-in person.
 */

const CHANNEL = "voice-live-events";
const ICE_WAIT_MS = 300;
const TICK_MS = 100;
const LISTEN_TAIL_MS = 350;
const INTERRUPT_HOLD_MS = 420;
const DROP_GRACE_MS = 4000;
const MAX_RECONNECTS = 2;

const TOOL_STEP: Record<string, string> = {
  ask_analyst: "Checking the records",
  make_visual: "Drawing the chart",
  prepare_draft: "Preparing the draft",
  show_on_screen: "Getting that ready",
};

/** The agent's voice is not shown with dashes as punctuation, the same as everywhere else in the product. */
export function cleanSpoken(text: string): string {
  return text.replace(/\s*[\u2013\u2014]\s*/g, ", ");
}

export class RealtimeVoiceEngine {
  private peer: PeerLike | null = null;
  private microphone: MicrophoneLike | null = null;
  private stopRemote: (() => void) | null = null;
  private stopMeter: (() => void) | null = null;
  private closeEvents: (() => void) | null = null;
  private sessionId: string | null = null;
  private conversationId: string | undefined;
  private muted = false;
  private locked = false;
  private active = false;
  private ending = false;
  private reconnects = 0;
  private state: VoiceOrbState = "idle";
  private responseActive = false;
  private spokenSomething = false;
  private pacer = new CaptionPacer();
  private ticker: ReturnType<typeof setInterval> | null = null;
  private lastTick = 0;
  private dropTimer: ReturnType<typeof setTimeout> | null = null;
  private startedAt = 0;
  private marks: Record<string, number> = {};
  private marksSent = false;

  private readonly deps: EngineDeps;
  private readonly out: EngineHandlers;

  constructor(deps: EngineDeps, out: EngineHandlers) {
    this.deps = deps;
    this.out = out;
  }

  get isActive() {
    return this.active;
  }

  /** Begins the call. Resolves when the voice service has answered; rejects never, a failure is told to the screen. */
  async start(conversationId?: string): Promise<void> {
    if (this.active) return;
    this.active = true;
    this.ending = false;
    this.reconnects = 0;
    this.conversationId = conversationId;
    this.startedAt = this.deps.now();
    this.marks = {};
    this.marksSent = false;
    this.setState("processing");
    this.out.step("Setting up the call");
    try {
      await this.connect();
    } catch (failure) {
      this.out.notice(this.messageOf(failure, "The call could not start. Try again, or type."));
      await this.finish("error");
    }
  }

  async end(): Promise<void> {
    if (!this.active) return;
    await this.finish("ended");
  }

  setMuted(muted: boolean) {
    this.muted = muted;
    this.microphone?.getAudioTracks().forEach((track) => { track.enabled = !muted; });
    if (muted) {
      this.setState("muted");
    } else if (this.state === "muted") {
      this.setState("listening");
    }
  }

  /** The phone locked or unlocked. While locked, no figures are given: the server enforces it, and the page says so. */
  async setLocked(locked: boolean): Promise<void> {
    if (this.locked === locked) return;
    this.locked = locked;
    if (locked) {
      this.pacer.interrupt();
      this.out.step("The phone is locked, so I am not reading anything out.");
    } else {
      this.out.step(null);
    }
    const id = this.sessionId;
    if (!id) return;
    try {
      await (locked ? this.deps.api.lock(id) : this.deps.api.unlock(id));
    } catch {
      /* the call carries on; the next tool call is still refused while the server believes the phone is locked */
    }
  }

  // ---- setting up ----------------------------------------------------------------------------

  private mark(name: string) {
    if (!(name in this.marks)) this.marks[name] = this.deps.now() - this.startedAt;
  }

  private async connect(): Promise<void> {
    // Ask for the place, open the microphone and make the offer at the same time: they do not depend on one another.
    const preparing = this.deps.api.prepare().catch((failure: unknown) => failure);
    const peer = this.deps.createPeer();
    this.peer = peer;
    const transceiver = peer.addTransceiver("audio", { direction: "sendrecv" });
    const channel = peer.createDataChannel(CHANNEL);
    this.watchChannel(channel);
    peer.onconnectionstatechange = () => this.onConnectionState(peer.connectionState);
    peer.ontrack = (event) => {
      this.stopRemote?.();
      this.stopRemote = this.deps.playRemote(event.streams[0]);
    };

    const microphone = this.microphone
      ? Promise.resolve(this.microphone)
      : this.deps.getMicrophone();
    const attaching = microphone.then(async (stream) => {
      if (!this.active) {
        // The call failed or ended while the microphone was opening: do not leave it on.
        stream.getTracks().forEach((track) => track.stop());
        return;
      }
      this.microphone = stream;
      const track = stream.getAudioTracks()[0];
      await transceiver.sender.replaceTrack(track ?? null);
      if (this.muted) stream.getAudioTracks().forEach((t) => { t.enabled = false; });
      if (this.deps.meter) {
        this.stopMeter?.();
        this.stopMeter = this.deps.meter(stream, (level) => { if (this.state === "listening") this.out.level(level); });
      }
      this.mark("microphone_ready");
    });

    attaching.catch(() => undefined);
    const offer = await peer.createOffer();
    await peer.setLocalDescription(offer);
    await this.waitForFirstCandidate(peer);
    this.mark("offer_sent");

    const prepared = await preparing;
    if (prepared instanceof Error || (prepared && typeof prepared === "object" && !("prepareId" in prepared))) {
      throw prepared;
    }
    await attaching;

    const started = await this.deps.api.start({
      prepareId: (prepared as { prepareId: string }).prepareId,
      sdpOffer: peer.localDescription?.sdp ?? offer.sdp ?? "",
      conversationId: this.conversationId,
      timeZoneOffsetMinutes: this.deps.timeZoneOffsetMinutes,
    });
    this.sessionId = started.sessionId;
    this.mark("answer_received");
    await peer.setRemoteDescription({ type: "answer", sdp: started.sdpAnswer });
    this.closeEvents?.();
    this.closeEvents = this.deps.api.events(started.sessionId, (event, data) => this.onScreenEvent(event, data));
    if (this.locked) void this.deps.api.lock(started.sessionId).catch(() => undefined);
  }

  private waitForFirstCandidate(peer: PeerLike): Promise<void> {
    return new Promise((resolve) => {
      if (peer.iceGatheringState === "complete") {
        resolve();
        return;
      }
      const timer = setTimeout(done, ICE_WAIT_MS);
      function done() {
        clearTimeout(timer);
        peer.onicecandidate = null;
        resolve();
      }
      peer.onicecandidate = done;
    });
  }

  private watchChannel(channel: ChannelLike) {
    channel.onopen = () => {
      this.mark("connected");
      this.out.step(null);
      this.setState(this.muted ? "muted" : "listening");
    };
    channel.onmessage = (event) => {
      if (typeof event.data !== "string") return;
      try {
        this.onVoiceEvent(JSON.parse(event.data) as { type?: string; [key: string]: unknown });
      } catch {
        /* an event that is not JSON is ignored, never shown */
      }
    };
    channel.onclose = () => {
      if (this.active && !this.ending) this.recover();
    };
  }

  private onConnectionState(connection: string) {
    if (connection === "connected") {
      if (this.dropTimer) clearTimeout(this.dropTimer);
      this.dropTimer = null;
      return;
    }
    if (connection === "failed" || connection === "closed") {
      this.recover();
    } else if (connection === "disconnected" && !this.dropTimer) {
      this.dropTimer = setTimeout(() => {
        this.dropTimer = null;
        if (this.peer?.connectionState !== "connected") this.recover();
      }, DROP_GRACE_MS);
    }
  }

  /** The link dropped: try again with the same microphone and the same written conversation, a couple of times, then say so. */
  private recover() {
    if (!this.active || this.ending) return;
    if (this.dropTimer) clearTimeout(this.dropTimer);
    this.dropTimer = null;
    if (this.reconnects >= MAX_RECONNECTS) {
      this.out.notice("The connection dropped and could not be restored. Tap Talk to start again.");
      void this.finish("dropped");
      return;
    }
    this.reconnects += 1;
    this.setState("reconnecting");
    this.out.step("The connection dropped. Trying again.");
    const old = this.sessionId;
    this.sessionId = null;
    this.stopTicker();
    this.pacer.reset();
    this.closeEvents?.();
    this.closeEvents = null;
    this.stopRemote?.();
    this.stopRemote = null;
    this.peer?.close();
    this.peer = null;
    if (old) void this.deps.api.end(old).catch(() => undefined);
    this.connect().catch(() => this.recover());
  }

  // ---- what the voice service reports --------------------------------------------------------

  private onVoiceEvent(event: { type?: string; [key: string]: unknown }) {
    switch (event.type) {
      case "input_audio_buffer.speech_started": {
        if (this.muted) return;
        if (this.state === "speaking" || this.responseActive) {
          const shown = this.pacer.interrupt();
          if (shown) {
            this.out.caption("iip", shown, true);
            this.out.line("iip", shown);
          }
          this.pacer.reset();
          this.responseActive = false;
          this.spokenSomething = false;
          this.stopTicker();
          this.setState("interrupted");
          setTimeout(() => { if (this.state === "interrupted") this.setState("listening"); }, INTERRUPT_HOLD_MS);
        } else {
          this.setState("listening");
        }
        this.out.step(null);
        break;
      }
      case "input_audio_buffer.speech_stopped":
        if (!this.muted) {
          this.setState("processing");
          this.out.step("Thinking");
        }
        break;
      case "conversation.item.input_audio_transcription.completed": {
        const transcript = typeof event.transcript === "string" ? cleanSpoken(event.transcript.trim()) : "";
        if (transcript) {
          this.out.caption("you", transcript, true);
          this.out.line("you", transcript);
        }
        break;
      }
      case "response.created":
        this.responseActive = true;
        this.pacer.reset();
        this.spokenSomething = false;
        break;
      case "response.function_call_arguments.done": {
        const name = typeof event.name === "string" ? event.name : "";
        if (name !== "end_conversation") this.out.step(TOOL_STEP[name] ?? "Working on it");
        if (this.state !== "muted" && this.state !== "interrupted") this.setState("processing");
        break;
      }
      case "output_audio_buffer.started":
        this.mark("first_audio");
        this.sendMarks();
        this.beginSpeaking();
        break;
      case "response.audio_transcript.delta":
      case "response.output_audio_transcript.delta": {
        if (this.locked) break;
        const delta = typeof event.delta === "string" ? event.delta : "";
        if (!delta) break;
        this.pacer.push(cleanSpoken(delta));
        this.spokenSomething = true;
        this.startTicker();
        break;
      }
      case "response.audio_transcript.done":
      case "response.output_audio_transcript.done": {
        const transcript = typeof event.transcript === "string" ? cleanSpoken(event.transcript) : "";
        if (!this.spokenSomething && transcript && !this.locked) {
          this.pacer.push(transcript);
          this.spokenSomething = true;
          this.startTicker();
        }
        this.pacer.finish();
        break;
      }
      case "response.done":
        // The text is done well before the voice is: the reply ends when the words on screen catch up, not now.
        this.responseActive = false;
        if (this.spokenSomething) this.pacer.finish();
        break;
      case "error": {
        const detail = event.error as { message?: string } | undefined;
        this.out.notice(detail?.message ? cleanSpoken(detail.message) : "The voice service reported a problem.");
        break;
      }
      default:
        break;
    }
  }

  private beginSpeaking() {
    if (this.muted || this.state === "interrupted") return;
    this.out.step(null);
    this.setState("speaking");
  }

  private startTicker() {
    if (this.ticker) return;
    this.lastTick = this.deps.now();
    this.ticker = setInterval(() => this.tick(), TICK_MS);
  }

  private stopTicker() {
    if (this.ticker) clearInterval(this.ticker);
    this.ticker = null;
  }

  private tick() {
    const now = this.deps.now();
    const elapsed = Math.max(0, now - this.lastTick);
    this.lastTick = now;
    const before = this.pacer.text;
    const shown = this.pacer.tick(elapsed);
    if (shown !== before) {
      if (this.state === "processing") this.beginSpeaking();
      this.out.caption("iip", shown, false);
    }
    if (this.pacer.done && !this.responseActive) {
      this.stopTicker();
      const text = this.pacer.text.trim();
      if (text) {
        this.out.caption("iip", text, true);
        this.out.line("iip", text);
      }
      this.pacer.reset();
      this.spokenSomething = false;
      setTimeout(() => {
        if (this.active && (this.state === "speaking" || this.state === "processing")) {
          this.out.step(null);
          this.setState(this.muted ? "muted" : "listening");
        }
      }, LISTEN_TAIL_MS);
    }
  }

  private onScreenEvent(event: string, data: unknown) {
    if (event === "blocks") {
      const blocks = (data as { blocks?: unknown[] } | null)?.blocks;
      if (Array.isArray(blocks) && blocks.length > 0) this.out.blocks(blocks);
    } else if (event === "open") {
      const open = data as { label?: string; href?: string } | null;
      if (open?.label && open.href) this.out.open(open.label, open.href);
    } else if (event === "conversation") {
      const id = (data as { conversationId?: string } | null)?.conversationId;
      if (id) this.out.conversation(id);
    } else if (event === "ended") {
      if (this.active && !this.ending) void this.finish("ended");
    }
  }

  // ---- ending --------------------------------------------------------------------------------

  private async finish(reason: "ended" | "dropped" | "error") {
    if (this.ending) return;
    this.ending = true;
    this.active = false;
    this.stopTicker();
    if (this.dropTimer) clearTimeout(this.dropTimer);
    this.dropTimer = null;
    this.closeEvents?.();
    this.closeEvents = null;
    this.stopMeter?.();
    this.stopMeter = null;
    this.stopRemote?.();
    this.stopRemote = null;
    this.microphone?.getTracks().forEach((track) => track.stop());
    this.microphone = null;
    this.peer?.close();
    this.peer = null;
    const id = this.sessionId;
    this.sessionId = null;
    this.sendMarks();
    this.out.level(0);
    this.out.step(null);
    this.setState("idle");
    this.out.ended(reason);
    if (id) {
      try {
        await this.deps.api.end(id);
      } catch {
        /* the server also ends a call whose page has gone */
      }
    }
  }

  private sendMarks() {
    if (this.marksSent || Object.keys(this.marks).length === 0) return;
    this.marksSent = true;
    void this.deps.api.metrics({ ...this.marks }).catch(() => undefined);
  }

  private setState(next: VoiceOrbState) {
    this.state = next;
    this.out.state(next);
  }

  private messageOf(failure: unknown, fallback: string): string {
    const name = failure && typeof failure === "object" && "name" in failure ? String((failure as { name: unknown }).name) : "";
    if (name === "NotAllowedError" || name === "SecurityError") return "The microphone is blocked. Allow it for this site, then try again.";
    if (name === "NotFoundError") return "No microphone was found.";
    const message = failure && typeof failure === "object" && "message" in failure ? String((failure as { message: unknown }).message) : "";
    return message && message.length < 240 ? message : fallback;
  }
}

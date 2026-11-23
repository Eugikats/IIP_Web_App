import { test, mock } from "node:test";
import assert from "node:assert/strict";
import { RealtimeVoiceEngine, cleanSpoken } from "./realtimeEngine.ts";
import type { ChannelLike, EngineHandlers, MicrophoneLike, PeerLike, VoiceApi, VoiceOrbState } from "./types.ts";

// The engine against a scripted peer and a scripted server: no browser, no network, no Azure.

class FakePeer implements PeerLike {
  connectionState = "new";
  iceGatheringState = "gathering";
  localDescription: { sdp?: string } | null = null;
  onicecandidate: ((event: { candidate: unknown }) => void) | null = null;
  onconnectionstatechange: (() => void) | null = null;
  ontrack: ((event: { streams: unknown[] }) => void) | null = null;
  channel: ChannelLike & { send(event: object): void } = {
    onopen: null,
    onmessage: null,
    onclose: null,
    send(event: object) { this.onmessage?.({ data: JSON.stringify(event) }); },
  };
  replaced: unknown[] = [];
  closed = false;
  remote: { type: string; sdp: string } | null = null;

  addTransceiver() {
    return { sender: { replaceTrack: async (track: unknown) => { this.replaced.push(track); } } };
  }
  createDataChannel(label: string) {
    assert.equal(label, "voice-live-events");
    return this.channel;
  }
  async createOffer() { return { type: "offer", sdp: "offer-sdp" }; }
  async setLocalDescription(description: { sdp?: string }) { this.localDescription = { sdp: `${description.sdp}+candidates` }; }
  async setRemoteDescription(description: { type: "answer"; sdp: string }) { this.remote = description; }
  close() { this.closed = true; }
  setState(state: string) { this.connectionState = state; this.onconnectionstatechange?.(); }
}

function track() {
  return { enabled: true, stopped: false, stop() { this.stopped = true; } };
}

type Rig = ReturnType<typeof makeRig>;

function makeRig(options: { prepareFails?: Error; startFails?: Error } = {}) {
  let clock = 0;
  const peers: FakePeer[] = [];
  const log: string[] = [];
  const states: VoiceOrbState[] = [];
  const captions: Array<{ who: string; text: string; final: boolean }> = [];
  const lines: Array<{ who: string; text: string }> = [];
  const steps: Array<string | null> = [];
  const notices: Array<string | null> = [];
  const blocks: unknown[][] = [];
  const opens: Array<{ label: string; href: string }> = [];
  const ended: string[] = [];
  const requests: Array<Record<string, unknown>> = [];
  const metrics: Array<Record<string, number>> = [];
  const mic = track();
  const stream: MicrophoneLike = { getAudioTracks: () => [mic], getTracks: () => [mic] };
  let session = 0;
  let screenListener: ((event: string, data: unknown) => void) | null = null;

  const api: VoiceApi = {
    async prepare() {
      log.push("prepare");
      if (options.prepareFails) throw options.prepareFails;
      return { prepareId: "prep-1" };
    },
    async start(request) {
      log.push("start");
      requests.push(request as unknown as Record<string, unknown>);
      if (options.startFails) throw options.startFails;
      session += 1;
      return { sessionId: `s${session}`, sdpAnswer: "answer-sdp", maxMinutes: 15 };
    },
    async lock(id) { log.push(`lock:${id}`); },
    async unlock(id) { log.push(`unlock:${id}`); },
    async end(id) { log.push(`end:${id}`); },
    async metrics(marks) { metrics.push(marks); },
    events(_id, onEvent) { screenListener = onEvent; return () => { screenListener = null; }; },
  };

  const handlers: EngineHandlers = {
    state: (s) => states.push(s),
    level: () => undefined,
    caption: (who, text, final) => captions.push({ who, text, final }),
    line: (who, text) => lines.push({ who, text }),
    notice: (n) => notices.push(n),
    step: (s) => steps.push(s),
    blocks: (b) => blocks.push(b),
    open: (label, href) => opens.push({ label, href }),
    conversation: () => undefined,
    ended: (reason) => ended.push(reason),
  };

  const engine = new RealtimeVoiceEngine(
    {
      api,
      createPeer: () => { const peer = new FakePeer(); peers.push(peer); return peer; },
      getMicrophone: async () => { log.push("mic"); return stream; },
      playRemote: () => () => log.push("remote-stopped"),
      now: () => clock,
      timeZoneOffsetMinutes: 180,
    },
    handlers,
  );

  return {
    engine, peers, log, states, captions, lines, steps, notices, blocks, opens, ended, requests, metrics, mic,
    get peer() { return peers[peers.length - 1]; },
    get lastState() { return states[states.length - 1]; },
    advance(ms: number) { clock += ms; mock.timers.tick(ms); },
    voice(event: object) { peers[peers.length - 1].channel.send(event); },
    screen(event: string, data: unknown) { screenListener?.(event, data); },
  };
}

async function begin(rig: Rig, conversationId?: string) {
  const starting = rig.engine.start(conversationId);
  // Voice Live's first candidate never arrives in a fake peer, so the 300 ms wait is what releases the offer.
  await Promise.resolve();
  await Promise.resolve();
  mock.timers.tick(300);
  await starting;
  rig.peer.channel.onopen?.();
}

async function flush() {
  for (let i = 0; i < 5; i += 1) await Promise.resolve();
}

test("the call is set up: microphone, offer and place are asked for together, and the answer is applied", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig, "conv-9");

    assert.ok(rig.log.includes("prepare") && rig.log.includes("mic"), "the place and the microphone are both asked for before the call starts");
    assert.ok(rig.log.indexOf("prepare") < rig.log.indexOf("start"));
    assert.ok(rig.log.indexOf("mic") < rig.log.indexOf("start"));
    assert.equal(rig.requests[0].sdpOffer, "offer-sdp+candidates");
    assert.equal(rig.requests[0].prepareId, "prep-1");
    assert.equal(rig.requests[0].conversationId, "conv-9");
    assert.equal(rig.requests[0].timeZoneOffsetMinutes, 180);
    assert.deepEqual(rig.peer.remote, { type: "answer", sdp: "answer-sdp" });
    assert.equal(rig.peer.replaced.length, 1, "the microphone track is attached to the call");
    assert.equal(rig.lastState, "listening");
    assert.equal(rig.states[0], "processing");
  } finally {
    mock.timers.reset();
  }
});

test("the orb follows what the voice service reports, and captions are paced to the voice", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig);

    rig.voice({ type: "input_audio_buffer.speech_started" });
    assert.equal(rig.lastState, "listening");
    rig.voice({ type: "input_audio_buffer.speech_stopped" });
    assert.equal(rig.lastState, "processing");
    rig.voice({ type: "conversation.item.input_audio_transcription.completed", transcript: "How many claims today?" });
    assert.deepEqual(rig.lines.at(-1), { who: "you", text: "How many claims today?" });

    rig.voice({ type: "response.created" });
    rig.voice({ type: "response.function_call_arguments.done", name: "ask_analyst", call_id: "c1", arguments: "{}" });
    assert.equal(rig.steps.at(-1), "Checking the records");
    assert.equal(rig.lastState, "processing");

    rig.voice({ type: "response.created" });
    rig.voice({ type: "response.audio_transcript.delta", delta: "There were four claims " });
    rig.voice({ type: "response.audio_transcript.delta", delta: "today. Two are open." });
    rig.voice({ type: "response.audio_transcript.done", transcript: "There were four claims today. Two are open." });
    rig.voice({ type: "output_audio_buffer.started" });
    assert.equal(rig.lastState, "speaking");
    assert.equal(rig.steps.at(-1), null);

    rig.voice({ type: "response.done" });
    // The reply is complete on the wire, but nothing has been said on screen yet: captions are paced.
    assert.equal(rig.captions.filter((c) => c.who === "iip").length, 0);
    rig.advance(500);
    const first = rig.captions.filter((c) => c.who === "iip" && !c.final).at(-1);
    assert.ok(first && first.text.length > 0 && first.text.length < "There were four claims today. Two are open.".length, JSON.stringify(first));
    assert.equal(rig.lastState, "speaking");

    rig.advance(4000);
    assert.deepEqual(rig.lines.at(-1), { who: "iip", text: "There were four claims today. Two are open." });
    assert.equal(rig.captions.at(-1)?.final, true);
    rig.advance(400);
    assert.equal(rig.lastState, "listening");
  } finally {
    mock.timers.reset();
  }
});

test("speaking over the agent cuts the captions at what was shown and says it was interrupted", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig);
    rig.voice({ type: "response.created" });
    rig.voice({ type: "response.audio_transcript.delta", delta: "Here is a long answer about every claim in the queue today. " });
    rig.voice({ type: "output_audio_buffer.started" });
    rig.advance(700);
    const shown = rig.captions.filter((c) => c.who === "iip").at(-1)!.text;

    rig.voice({ type: "input_audio_buffer.speech_started" });

    assert.equal(rig.lastState, "interrupted");
    assert.deepEqual(rig.lines.at(-1), { who: "iip", text: shown });
    assert.ok(shown.length < "Here is a long answer about every claim in the queue today. ".length);
    rig.advance(5000);
    assert.equal(rig.lines.filter((l) => l.who === "iip").length, 1, "the unshown words are never added later");
    assert.equal(rig.lastState, "listening");
  } finally {
    mock.timers.reset();
  }
});

test("muting closes the microphone to the call, and unmuting opens it", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig);
    rig.engine.setMuted(true);
    assert.equal(rig.mic.enabled, false);
    assert.equal(rig.lastState, "muted");
    rig.voice({ type: "input_audio_buffer.speech_started" });
    assert.equal(rig.lastState, "muted", "nothing is acted on while muted");
    rig.engine.setMuted(false);
    assert.equal(rig.mic.enabled, true);
    assert.equal(rig.lastState, "listening");
  } finally {
    mock.timers.reset();
  }
});

test("locking tells the server, stops captions and unlocking tells it again", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig);
    await rig.engine.setLocked(true);
    assert.ok(rig.log.includes("lock:s1"));
    rig.voice({ type: "response.created" });
    rig.voice({ type: "response.audio_transcript.delta", delta: "Four claims came in today. " });
    rig.advance(3000);
    assert.equal(rig.captions.filter((c) => c.who === "iip").length, 0, "no figures are shown while the phone is locked");
    assert.match(String(rig.steps.at(-1)), /locked/);

    await rig.engine.setLocked(false);
    assert.ok(rig.log.includes("unlock:s1"));
    assert.equal(rig.steps.at(-1), null);
  } finally {
    mock.timers.reset();
  }
});

test("ending closes everything, tells the server once and reports the start-up timings as numbers", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig);
    rig.voice({ type: "output_audio_buffer.started" });

    await rig.engine.end();
    await rig.engine.end();

    assert.equal(rig.mic.stopped, true);
    assert.equal(rig.peer.closed, true);
    assert.equal(rig.log.filter((x) => x === "end:s1").length, 1);
    assert.deepEqual(rig.ended, ["ended"]);
    assert.equal(rig.lastState, "idle");
    assert.equal(rig.metrics.length, 1);
    for (const [name, value] of Object.entries(rig.metrics[0])) {
      assert.match(name, /^(microphone_ready|offer_sent|answer_received|connected|first_audio)$/);
      assert.equal(typeof value, "number");
    }
    assert.ok("first_audio" in rig.metrics[0] && "connected" in rig.metrics[0]);
  } finally {
    mock.timers.reset();
  }
});

test("a dropped link is retried with the same conversation, then given up with an honest message", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig, "conv-1");
    const first = rig.peer;

    first.setState("failed");
    assert.equal(rig.lastState, "reconnecting");
    assert.equal(first.closed, true);
    assert.ok(rig.log.includes("end:s1"), "the old call is ended on the server");
    await flush();
    mock.timers.tick(300);
    await flush();
    assert.equal(rig.peers.length, 2);
    assert.equal(rig.requests[1].conversationId, "conv-1");
    assert.equal(rig.mic.stopped, false, "the microphone stays open across the retry");

    rig.peers[1].channel.onopen?.();
    assert.equal(rig.lastState, "listening");

    rig.peers[1].setState("failed");
    await flush();
    mock.timers.tick(300);
    await flush();
    rig.peers[2].setState("failed");
    await flush();

    assert.deepEqual(rig.ended, ["dropped"]);
    assert.match(String(rig.notices.at(-1)), /could not be restored/);
    assert.equal(rig.mic.stopped, true);
  } finally {
    mock.timers.reset();
  }
});

test("a call the server refuses says why and opens nothing", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig({ prepareFails: Object.assign(new Error("You already have live conversations open. End one before starting another."), { code: "too_many_calls" }) });
    const starting = rig.engine.start();
    await flush();
    mock.timers.tick(300);
    await starting;
    await flush();

    assert.deepEqual(rig.ended, ["error"]);
    assert.match(String(rig.notices.at(-1)), /already have live conversations/);
    assert.ok(!rig.log.includes("start"));
    assert.equal(rig.mic.stopped, true, "the microphone is not left on");
  } finally {
    mock.timers.reset();
  }
});

test("a blocked microphone is told plainly", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    let clock = 0;
    const notices: Array<string | null> = [];
    const engine = new RealtimeVoiceEngine(
      {
        api: {
          prepare: async () => ({ prepareId: "p" }),
          start: async () => ({ sessionId: "s", sdpAnswer: "a", maxMinutes: 15 }),
          lock: async () => undefined, unlock: async () => undefined, end: async () => undefined, metrics: async () => undefined,
          events: () => () => undefined,
        },
        createPeer: () => new FakePeer(),
        getMicrophone: async () => { throw Object.assign(new Error("denied"), { name: "NotAllowedError" }); },
        playRemote: () => () => undefined,
        now: () => clock,
      },
      {
        state: () => undefined, level: () => undefined, caption: () => undefined, line: () => undefined, notice: (n) => notices.push(n),
        step: () => undefined, blocks: () => undefined, open: () => undefined, conversation: () => undefined, ended: () => undefined,
      },
    );
    const starting = engine.start();
    await flush();
    mock.timers.tick(300);
    await starting;
    clock += 1;
    assert.match(String(notices.at(-1)), /microphone is blocked/);
  } finally {
    mock.timers.reset();
  }
});

test("charts, tables and open-this-screen offers from the agent reach the screen untouched", async () => {
  mock.timers.enable({ apis: ["setTimeout", "setInterval"] });
  try {
    const rig = makeRig();
    await begin(rig);

    rig.screen("blocks", { blocks: [{ type: "chart", title: "Claims by day" }] });
    rig.screen("open", { label: "Overdue claims", href: "/app/claims?sla=breached" });
    rig.screen("blocks", { blocks: [] });
    rig.screen("open", { label: "", href: "" });

    assert.deepEqual(rig.blocks, [[{ type: "chart", title: "Claims by day" }]]);
    assert.deepEqual(rig.opens, [{ label: "Overdue claims", href: "/app/claims?sla=breached" }]);

    rig.screen("ended", {});
    await flush();
    assert.deepEqual(rig.ended, ["ended"]);
  } finally {
    mock.timers.reset();
  }
});

test("dashes used as punctuation are removed from what the agent says", () => {
  assert.equal(cleanSpoken("Four claims \u2014 two are open"), "Four claims, two are open");
  assert.equal(cleanSpoken("10\u201320 days"), "10, 20 days");
});

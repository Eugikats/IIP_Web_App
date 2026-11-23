import http from "node:http";
import { expect, test, type Page } from "@playwright/test";

// The agent should look awake: it gives a brief when the workspace opens, and while it works a person
// watches each step it takes. A small stand-in API runs here so the stream is real (the answer is held
// until the test lets it go), which a canned response could not show.

const PORT = 5167;
let server: http.Server;
let metrics: { status: number; body: Record<string, number> } = { status: 200, body: {} };
let voice: { status: number; body: Record<string, unknown> } = { status: 200, body: { text: "Show me the claims past their deadline" } };
let voiceStatus = true;
let realtimeAvailable = false;
let voiceCalls: Array<{ path: string; body: string }> = [];
let prepareFails: { status: number; body: Record<string, unknown> } | null = null;
let nudgeNext: { id: string; text: string; href: string | null; private: boolean } | null = null;
let spokenReply: Record<string, unknown> = { text: "Four claims came in today.", language: "en", translated: false, audio: null, mimeType: null };
const screenFeeds: http.ServerResponse[] = [];
let releaseAnswer: (() => void) | null = null;
let answerGate: Promise<void> = Promise.resolve();

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "*",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
};

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

test.beforeAll(async () => {
  server = http.createServer(async (req, res) => {
    if (req.method === "OPTIONS") {
      res.writeHead(204, cors).end();
      return;
    }
    if (req.url?.startsWith("/api/v1/voice/")) {
      const path = req.url.replace("/api/v1/voice/", "").split("?")[0];
      if (path === "nudge") {
        voiceCalls.push({ path: req.url.replace("/api/v1/voice/", ""), body: "" });
        if (!nudgeNext) { res.writeHead(204, cors).end(); return; }
        const given = nudgeNext;
        nudgeNext = null;
        const locked = req.url.includes("locked=true");
        res.writeHead(200, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify(locked
          ? { id: "n1", text: "Something in IIP needs your attention. Open the app when you can.", href: null, private: true }
          : given));
        return;
      }
      if (path === "status") {
        res.writeHead(200, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify({ available: realtimeAvailable, turnBasedAvailable: voiceStatus, processedIn: "Sweden" }));
        return;
      }
      if (path.endsWith("/events")) {
        res.writeHead(200, { ...cors, "Content-Type": "text/event-stream", "Cache-Control": "no-cache" });
        res.write(": open\n\n");
        screenFeeds.push(res);
        req.on("close", () => { const at = screenFeeds.indexOf(res); if (at >= 0) screenFeeds.splice(at, 1); });
        return;
      }
      let body = "";
      req.on("data", (chunk) => { body += chunk; });
      await new Promise((resolve) => req.on("end", resolve));
      voiceCalls.push({ path, body });
      if (path === "session/prepare") {
        if (prepareFails) { res.writeHead(prepareFails.status, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify(prepareFails.body)); return; }
        res.writeHead(200, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify({ prepareId: "prep-1" }));
        return;
      }
      if (path === "session") {
        res.writeHead(200, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify({ sessionId: "s1", sdpAnswer: "answer-sdp", maxMinutes: 15 }));
        return;
      }
      res.writeHead(204, cors).end();
      return;
    }
    if (req.url?.startsWith("/api/v1/command-center/voice-reply")) {
      req.resume();
      await new Promise((resolve) => req.on("end", resolve));
      res.writeHead(200, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify(spokenReply));
      return;
    }
    if (req.url?.startsWith("/api/v1/command-center/metrics")) {
      res.writeHead(metrics.status, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify(metrics.body));
      return;
    }
    if (req.url?.startsWith("/api/v1/command-center/voice-note/status")) {
      res.writeHead(200, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify({ available: voiceStatus }));
      return;
    }
    if (req.url?.startsWith("/api/v1/command-center/voice-note")) {
      let raw = "";
      req.on("data", (chunk) => { raw += chunk.toString("latin1"); });
      await new Promise((resolve) => req.on("end", resolve));
      voiceCalls.push({ path: "voice-note", body: raw.includes('name="language"') ? raw.slice(raw.indexOf('name="language"'), raw.indexOf('name="language"') + 40) : "" });
      res.writeHead(voice.status, { ...cors, "Content-Type": "application/json" }).end(JSON.stringify(voice.body));
      return;
    }
    if (req.url?.startsWith("/api/v1/command-center/query/stream")) {
      res.writeHead(200, { ...cors, "Content-Type": "text/event-stream", "Cache-Control": "no-cache" });
      const send = (event: string, data: unknown) => res.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);
      send("step", { label: "Checking your access" });
      await sleep(150);
      send("step", { label: "Reading your question" });
      send("tool", { label: "Counting prospects (today)" });
      await answerGate;
      send("answer", {
        blocks: [{ type: "text", markdown: "Two prospects came in today." }],
        canTriggerDecision: false,
        generatedAt: "2026-11-23T08:00:00Z",
        conversationId: "11111111-1111-1111-1111-111111111111",
      });
      send("done", {});
      res.end();
      return;
    }
    res.writeHead(404, { ...cors, "Content-Type": "application/json" }).end("{}");
  });
  await new Promise<void>((resolve) => server.listen(PORT, resolve));
});

test.afterAll(async () => {
  await new Promise<void>((resolve) => server.close(() => resolve()));
});

test.beforeEach(() => {
  metrics = { status: 200, body: {} };
  voice = { status: 200, body: { text: "Show me the claims past their deadline" } };
  voiceStatus = true;
  realtimeAvailable = false;
  voiceCalls = [];
  prepareFails = null;
  nudgeNext = null;
  spokenReply = { text: "Four claims came in today.", language: "en", translated: false, audio: null, mimeType: null };
  answerGate = new Promise<void>((resolve) => {
    releaseAnswer = resolve;
  });
});

async function openHome(page: Page) {
  await page.goto("/app/workspace");
  await expect(page.getByTestId("agent-brief")).toBeVisible({ timeout: 30_000 });
  // The brief leaves "Reading your workspace" only once the page is interactive, so this also waits for it to hydrate.
  await expect(page.getByTestId("agent-brief")).not.toContainText("Reading your workspace", { timeout: 30_000 });
}

test("the brief lists only what really needs a person, each as a dot and a word", async ({ page }) => {
  metrics = { status: 200, body: { slaBreached: 1, pendingApprovals: 3, incompleteClaims: 0, slaAtRisk: 0 } };
  await openHome(page);
  const brief = page.getByTestId("agent-brief");
  await expect(brief).toContainText("1 claim is past its service deadline.");
  await expect(brief).toContainText("3 approvals are waiting for a person to decide.");
  await expect(brief).not.toContainText("missing documents");
  await expect(brief.getByRole("link")).toHaveCount(2);
  await expect(brief.getByRole("link").first()).toContainText("Overdue");
});

test("a quiet workspace says so instead of inventing news", async ({ page }) => {
  await openHome(page);
  await expect(page.getByTestId("agent-brief")).toContainText("Nothing needs you right now.");
});

test("when the brief cannot be read it says so and the person can still ask", async ({ page }) => {
  metrics = { status: 500, body: {} };
  await openHome(page);
  await expect(page.getByTestId("agent-brief")).toContainText("could not be read");
  await expect(page.getByRole("textbox", { name: "Message Kato" })).toBeEnabled();
});

test("while it works the person watches each step, then the answer takes their place", async ({ page }) => {
  await openHome(page);
  await expect(page.getByTestId("agent-orb").first()).toHaveAttribute("data-state", "idle");

  await page.getByRole("textbox", { name: "Message Kato" }).fill("What needs my attention today?");
  await page.getByRole("button", { name: "Send message" }).click();

  const working = page.getByTestId("agent-working");
  await expect(working).toBeVisible();
  await expect(working.getByTestId("agent-step")).toHaveText(["Checking your access", "Reading your question", "Counting prospects (today)"]);
  await expect(working.getByTestId("agent-step").last()).toHaveAttribute("data-state", "active");
  await expect(working.getByTestId("agent-step").first()).toHaveAttribute("data-state", "done");

  releaseAnswer?.();
  await expect(page.getByText("Two prospects came in today.")).toBeVisible();
  await expect(working).toBeHidden();
});

test("a voice note becomes words in the box for the person to read, and is not sent on its own", async ({ page }) => {
  await openHome(page);
  await page.getByRole("button", { name: "Record a voice note" }).click();
  await expect(page.getByTestId("voice-recording")).toBeVisible();
  await expect(page.getByTestId("voice-recording").getByTestId("agent-orb")).toHaveAttribute("data-state", "listening");
  await page.waitForTimeout(1200);
  await page.getByRole("button", { name: "Done" }).click();

  const box = page.getByRole("textbox", { name: "Message Kato" });
  await expect(box).toHaveValue("Show me the claims past their deadline");
  // Still the empty home screen: nothing was sent.
  await expect(page.getByTestId("agent-brief")).toBeVisible();
  await expect(page.getByTestId("agent-working")).toHaveCount(0);
});

test("cancelling a recording keeps nothing", async ({ page }) => {
  await openHome(page);
  await page.getByRole("button", { name: "Record a voice note" }).click();
  await page.getByRole("button", { name: "Cancel the recording" }).click();
  await expect(page.getByTestId("voice-recording")).toHaveCount(0);
  await expect(page.getByRole("textbox", { name: "Message Kato" })).toHaveValue("");
});

test("a voice note that cannot be read says why and leaves the box as it was", async ({ page }) => {
  voice = { status: 422, body: { code: "not_understood", message: "I could not make out any words in that recording. Try again, or type it." } };
  await openHome(page);
  await page.getByRole("textbox", { name: "Message Kato" }).fill("Draft");
  await page.getByRole("button", { name: "Record a voice note" }).click();
  await page.waitForTimeout(800);
  await page.getByRole("button", { name: "Done" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "could not make out any words" })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Message Kato" })).toHaveValue("Draft");
});

// The browser's own voice is replaced by one that "speaks" for a moment, so the states can be watched.
async function stubSpeech(page: Page) {
  await page.addInitScript(() => {
    const spoken: string[] = [];
    (window as unknown as { __spoken: string[] }).__spoken = spoken;
    let current: { onend?: () => void } | null = null;
    Object.defineProperty(window, "speechSynthesis", {
      configurable: true,
      value: {
        cancel() { const u = current; current = null; u?.onend?.(); },
        speak(u: { text: string; onstart?: () => void; onend?: () => void }) {
          current = u;
          spoken.push(u.text);
          u.onstart?.();
          setTimeout(() => { if (current === u) { current = null; u.onend?.(); } }, 1500);
        },
      },
    });
    (window as unknown as { SpeechSynthesisUtterance: unknown }).SpeechSynthesisUtterance = function (this: { text: string }, text: string) { this.text = text; };
  });
}

test("a live conversation: listening, working with its steps showing, speaking, then listening again", async ({ page }) => {
  await stubSpeech(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();

  const sheet = page.getByTestId("live-sheet");
  await expect(sheet).toBeVisible();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");

  // The fake microphone speaks once; the words are read, then the agent works with its steps in view.
  await expect(page.getByTestId("live-state")).toHaveText("Working on it", { timeout: 20_000 });
  await expect(page.getByTestId("live-step")).toHaveText("Counting prospects (today)");
  await expect(sheet).toHaveAttribute("data-state", "processing");

  releaseAnswer?.();
  await expect(page.getByTestId("live-state")).toHaveText("Speaking");
  await expect(page.getByTestId("live-caption")).toContainText("Two prospects came in today.");
  expect(await page.evaluate(() => (window as unknown as { __spoken: string[] }).__spoken)).toEqual(["Two prospects came in today."]);
  await expect(page.getByTestId("live-state")).toHaveText("Listening", { timeout: 10_000 });

  // The transcript has both sides, and ending the call leaves them in the written conversation.
  await page.getByRole("button", { name: "Transcript" }).click();
  await expect(page.getByTestId("live-transcript")).toContainText("Show me the claims past their deadline");
  await expect(page.getByTestId("live-transcript")).toContainText("Two prospects came in today.");
  await page.getByRole("button", { name: "End the conversation" }).click();
  await expect(sheet).toHaveCount(0);
  await expect(page.getByText("Show me the claims past their deadline")).toBeVisible();
  await expect(page.getByText("Two prospects came in today.")).toBeVisible();
});

test("muting stops listening, shows it, and unmuting listens again; Escape ends the call", async ({ page }) => {
  await stubSpeech(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
  await page.getByRole("button", { name: "Mute", exact: true }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Muted");
  await expect(page.getByTestId("live-step")).toContainText("microphone is off");
  await page.getByRole("button", { name: "Unmute" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
  await page.keyboard.press("Escape");
  await expect(page.getByTestId("live-sheet")).toHaveCount(0);
});

test("the conversation can be minimised and carries on behind the screen", async ({ page }) => {
  await stubSpeech(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await page.getByRole("button", { name: "Minimise the conversation" }).click();
  await expect(page.getByTestId("live-minimised")).toBeVisible();
  await expect(page.getByTestId("live-sheet")).toHaveCount(0);
  await page.getByRole("button", { name: "Open the live conversation" }).click();
  await expect(page.getByTestId("live-sheet")).toBeVisible();
});

test("a workspace without a speech provider says so on the Talk button", async ({ page }) => {
  voiceStatus = false;
  await openHome(page);
  await expect(page.getByRole("button", { name: "Talk to IIP" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Talk to IIP" })).toHaveAttribute("title", /not set up/);
  await expect(page.getByRole("button", { name: "Record a voice note" })).toBeDisabled();
});

// A phone is the main place this is used: nothing may push the page sideways.
async function expectNoSidewaysScroll(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
}

test("the home screen and the live sheet fit the screen width without sideways scrolling", async ({ page }) => {
  metrics = { status: 200, body: { slaBreached: 1, pendingApprovals: 3 } };
  await stubSpeech(page);
  await openHome(page);
  await expectNoSidewaysScroll(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
  await expectNoSidewaysScroll(page);
  const box = await page.getByTestId("live-sheet").boundingBox();
  const viewport = page.viewportSize()!;
  expect(box!.width).toBeLessThanOrEqual(viewport.width);
  // Every control on the sheet can be reached with a thumb: at least 40px square.
  for (const name of ["Mute", "End the conversation", "Transcript"]) {
    const size = await page.getByRole("button", { name, exact: true }).first().boundingBox();
    expect(size!.width, name).toBeGreaterThanOrEqual(40);
    expect(size!.height, name).toBeGreaterThanOrEqual(40);
  }
});

test("swiping down on the live sheet opens the transcript and swiping up returns to the orb", async ({ page }) => {
  await stubSpeech(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");

  const swipe = (fromY: number, toY: number) =>
    page.getByTestId("live-sheet").evaluate((element, [from, to]) => {
      const touch = (y: number) => new Touch({ identifier: 1, target: element, clientX: 100, clientY: y });
      element.dispatchEvent(new TouchEvent("touchstart", { bubbles: true, touches: [touch(from)], changedTouches: [touch(from)] }));
      element.dispatchEvent(new TouchEvent("touchend", { bubbles: true, touches: [], changedTouches: [touch(to)] }));
    }, [fromY, toY]);

  await expect(page.getByTestId("live-transcript")).toHaveCount(0);
  await swipe(200, 400);
  await expect(page.getByTestId("live-transcript")).toBeVisible();
  await swipe(400, 200);
  await expect(page.getByTestId("live-transcript")).toHaveCount(0);
  // A short movement is a tap, not a swipe.
  await swipe(300, 320);
  await expect(page.getByTestId("live-transcript")).toHaveCount(0);
});


// ---- the realtime call, with a stand-in for the browser's peer connection -----------------------------------------

async function fakeWebRtc(page: Page) {
  await page.addInitScript(() => {
    type Channel = { onopen: (() => void) | null; onmessage: ((event: { data: string }) => void) | null; onclose: (() => void) | null };
    const w = window as unknown as { __peers: unknown[]; RTCPeerConnection: unknown };
    w.__peers = [];
    class FakePeer {
      connectionState = "new";
      iceGatheringState = "complete";
      localDescription: { sdp?: string } | null = null;
      onicecandidate = null;
      onconnectionstatechange: (() => void) | null = null;
      ontrack = null;
      channel: Channel = { onopen: null, onmessage: null, onclose: null };
      closed = false;
      constructor() { w.__peers.push(this); }
      addTransceiver() { return { sender: { replaceTrack: async () => undefined } }; }
      createDataChannel() { return this.channel; }
      async createOffer() { return { type: "offer", sdp: "offer-sdp" }; }
      async setLocalDescription(description: { sdp?: string }) { this.localDescription = { sdp: description.sdp }; }
      async setRemoteDescription() {
        setTimeout(() => { this.connectionState = "connected"; this.channel.onopen?.(); this.onconnectionstatechange?.(); }, 20);
      }
      close() { this.closed = true; }
    }
    w.RTCPeerConnection = FakePeer;
  });
}

const fromVoiceService = (page: Page, event: Record<string, unknown>) =>
  page.evaluate((e) => (window as unknown as { __peers: Array<{ channel: { onmessage: ((x: { data: string }) => void) | null } }> }).__peers.at(-1)!.channel.onmessage?.({ data: JSON.stringify(e) }), event);

function fromAgentScreen(event: string, data: unknown) {
  for (const feed of screenFeeds) feed.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);
}

async function startRealtime(page: Page, consent = true) {
  realtimeAvailable = true;
  await fakeWebRtc(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  if (consent) {
    await expect(page.getByTestId("live-consent")).toContainText("Sweden");
    await page.getByRole("button", { name: "Continue" }).click();
  }
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
}

test("a realtime call: consent once, orb states from the voice service, paced captions, a chart and an offer on screen, then it ends", async ({ page }) => {
  await startRealtime(page);

  // The call was set up with the place asked for and the offer sent, and start-up timings are posted as numbers only.
  expect(voiceCalls.map((c) => c.path)).toEqual(expect.arrayContaining(["session/prepare", "session"]));
  expect(JSON.parse(voiceCalls.find((c) => c.path === "session")!.body)).toMatchObject({ prepareId: "prep-1", sdpOffer: "offer-sdp" });

  await fromVoiceService(page, { type: "input_audio_buffer.speech_started" });
  await fromVoiceService(page, { type: "input_audio_buffer.speech_stopped" });
  await expect(page.getByTestId("live-state")).toHaveText("Working on it");
  await fromVoiceService(page, { type: "conversation.item.input_audio_transcription.completed", transcript: "How many claims came in today?" });
  await fromVoiceService(page, { type: "response.created" });
  await fromVoiceService(page, { type: "response.function_call_arguments.done", name: "ask_analyst", call_id: "c1", arguments: "{}" });
  await expect(page.getByTestId("live-step")).toHaveText("Checking the records");

  fromAgentScreen("blocks", { blocks: [{ type: "chart", title: "Claims by day", kind: "bar", unit: "claims", points: [{ label: "Mon", value: 2 }, { label: "Tue", value: 5 }], summary: "Tuesday was the busiest." }] });
  fromAgentScreen("open", { label: "Overdue claims", href: "/app/claims?sla=breached" });
  await expect(page.getByTestId("live-visuals")).toContainText("Claims by day");
  await expect(page.getByTestId("live-offer")).toHaveText("Open Overdue claims");
  fromAgentScreen("open", { label: "Elsewhere", href: "https://example.com/phish" });

  await fromVoiceService(page, { type: "response.created" });
  const sentence = "There were four claims today and two of them are still open.";
  await fromVoiceService(page, { type: "response.audio_transcript.delta", delta: sentence });
  await fromVoiceService(page, { type: "response.audio_transcript.done", transcript: sentence });
  await fromVoiceService(page, { type: "output_audio_buffer.started" });
  await fromVoiceService(page, { type: "response.done" });
  await expect(page.getByTestId("live-state")).toHaveText("Speaking");
  // Captions are paced to the voice: the whole reply is not on screen the moment it arrives.
  const early = (await page.getByTestId("live-caption").textContent()) ?? "";
  expect(early.length).toBeLessThan(sentence.length);
  await expect(page.getByTestId("live-caption")).toContainText("two of them are still open.", { timeout: 10_000 });
  await expect(page.getByTestId("live-state")).toHaveText("Listening", { timeout: 5_000 });
  await expect(page.getByTestId("live-offer")).toHaveCount(1); // the link to another website was never offered

  await page.getByRole("button", { name: "End the conversation" }).click();
  await expect(page.getByTestId("live-sheet")).toHaveCount(0);
  const paths = voiceCalls.map((c) => c.path);
  expect(paths).toContain("session/s1/end");
  const metrics = JSON.parse(voiceCalls.find((c) => c.path === "metrics")!.body) as { marks: Record<string, number> };
  expect(Object.values(metrics.marks).every((v) => typeof v === "number")).toBe(true);
  expect(Object.keys(metrics.marks)).toEqual(expect.arrayContaining(["microphone_ready", "offer_sent", "answer_received", "connected", "first_audio"]));

  // What was said and shown is in the written conversation.
  await expect(page.getByText("How many claims came in today?")).toBeVisible();
  await expect(page.getByText(sentence)).toBeVisible();
  await expect(page.getByText("Claims by day")).toBeVisible();
});

test("the first-use notice is asked once on a device, and Not now makes no call", async ({ page }) => {
  realtimeAvailable = true;
  await fakeWebRtc(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await expect(page.getByTestId("live-consent")).toContainText("outside Africa");
  await page.getByRole("button", { name: "Not now" }).click();
  await expect(page.getByTestId("live-sheet")).toHaveCount(0);
  expect(voiceCalls).toHaveLength(0);

  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await page.getByRole("button", { name: "Continue" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
  await page.getByRole("button", { name: "End the conversation" }).click();

  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await expect(page.getByTestId("live-consent")).toHaveCount(0);
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
});

test("a locked phone shows it, tells the server, and nothing is shown while locked", async ({ page }) => {
  await startRealtime(page);
  await page.evaluate(() => window.dispatchEvent(new CustomEvent("iip:device-lock", { detail: { locked: true } })));
  await expect(page.getByTestId("live-locked")).toContainText("will not read out any figures");
  await expect.poll(() => voiceCalls.some((c) => c.path === "session/s1/lock")).toBe(true);

  await fromVoiceService(page, { type: "response.created" });
  await fromVoiceService(page, { type: "response.audio_transcript.delta", delta: "Four claims came in today. " });
  await page.waitForTimeout(1200);
  await expect(page.getByTestId("live-caption")).toHaveCount(0);

  await page.evaluate(() => window.dispatchEvent(new CustomEvent("iip:device-lock", { detail: { locked: false } })));
  await expect(page.getByTestId("live-locked")).toHaveCount(0);
  await expect.poll(() => voiceCalls.some((c) => c.path === "session/s1/unlock")).toBe(true);
});

test("the conversation carries on across screens and is in the thread when the person returns", async ({ page }) => {
  await startRealtime(page);
  await fromVoiceService(page, { type: "conversation.item.input_audio_transcription.completed", transcript: "What is overdue?" });
  await fromVoiceService(page, { type: "response.created" });
  await fromVoiceService(page, { type: "response.audio_transcript.delta", delta: "One claim is past its deadline. " });
  await fromVoiceService(page, { type: "response.audio_transcript.done", transcript: "One claim is past its deadline. " });
  await fromVoiceService(page, { type: "output_audio_buffer.started" });
  await fromVoiceService(page, { type: "response.done" });

  await page.getByRole("button", { name: "Minimise the conversation" }).click();
  await expect(page.getByTestId("live-minimised")).toBeVisible();

  // Leave the workspace without reloading the page: the call is held by the app shell, not by the screen.
  await page.evaluate(() => (window as unknown as { next: { router: { push: (to: string) => void } } }).next.router.push("/app/claims"));
  await expect(page).toHaveURL(/\/app\/claims/);
  await expect(page.getByTestId("live-minimised")).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as { __peers: Array<{ closed: boolean }> }).__peers.length)).toBe(1);
  expect(await page.evaluate(() => (window as unknown as { __peers: Array<{ closed: boolean }> }).__peers[0].closed)).toBe(false);

  await page.evaluate(() => (window as unknown as { next: { router: { push: (to: string) => void } } }).next.router.push("/app/workspace"));
  await expect(page.getByText("What is overdue?")).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText("One claim is past its deadline.")).toBeVisible();
  await expect(page.getByTestId("live-minimised")).toBeVisible();
});

test("a call the server refuses says why and offers to try again, with the microphone left alone", async ({ page }) => {
  prepareFails = { status: 429, body: { code: "too_many_calls", message: "You already have live conversations open. End one before starting another." } };
  await startRealtime(page, true).catch(() => undefined);
  await expect(page.getByTestId("live-sheet").getByRole("alert")).toContainText("already have live conversations open");
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
  expect(voiceCalls.some((c) => c.path === "session")).toBe(false);
  await page.getByRole("button", { name: "End the conversation" }).click();
  await expect(page.getByTestId("live-sheet")).toHaveCount(0);
});

test("without realtime voice, Luganda is chosen in the call and the reply that cannot be translated is shown, not spoken", async ({ page }) => {
  await stubSpeech(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
  await page.getByRole("button", { name: "Luganda" }).click();
  await expect(page.getByRole("button", { name: "Luganda" })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByTestId("live-state")).toHaveText("Listening");

  await expect(page.getByTestId("live-state")).toHaveText("Working on it", { timeout: 20_000 });
  expect(voiceCalls.find((c) => c.path === "voice-note")?.body ?? "").toContain("lg");
  releaseAnswer?.();
  await expect(page.getByTestId("live-sheet").getByRole("alert")).toContainText("could not turn that reply into Luganda", { timeout: 15_000 });
  expect(await page.evaluate(() => (window as unknown as { __spoken: string[] }).__spoken)).toEqual([]);
});

test("Luganda and Swahili cannot be chosen when no speech provider is connected", async ({ page }) => {
  realtimeAvailable = true;
  voiceStatus = false;
  await fakeWebRtc(page);
  await openHome(page);
  await page.getByRole("button", { name: "Talk to IIP" }).click();
  await page.getByRole("button", { name: "Continue" }).click();
  await expect(page.getByTestId("live-state")).toHaveText("Listening");
  await expect(page.getByRole("button", { name: "Luganda" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Swahili" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "English" })).toHaveAttribute("aria-pressed", "true");
});

test("a realtime sheet with a chart still fits the screen width", async ({ page }, testInfo) => {
  await startRealtime(page);
  fromAgentScreen("blocks", { blocks: [{ type: "chart", title: "Claims received each day this week", kind: "line", unit: "claims", points: [1, 2, 3, 4, 5, 6, 7].map((n) => ({ label: `Day ${n}`, value: n })), summary: "Rising." }] });
  await expect(page.getByTestId("live-visuals")).toBeVisible();
  await expectNoSidewaysScroll(page);
  const box = await page.getByTestId("live-visuals").boundingBox();
  expect(box!.x + box!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  await page.screenshot({ path: process.env.IIP_SHOT_DIR ? `${process.env.IIP_SHOT_DIR}/realtime-${testInfo.project.name}.png` : testInfo.outputPath("realtime.png") });
});

// ---- spoken nudges -------------------------------------------------------------------------------------------------

test("a nudge from the server shows as one quiet line, can be opened, and fits the screen", async ({ page }) => {
  nudgeNext = { id: "n1", text: "Grace Nakato, Motor claim. It is past its deadline.", href: "/app/claims/c1", private: false };
  await openHome(page);
  await expect(page.getByTestId("nudge")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("nudge-text")).toContainText("past its deadline");
  await expectNoSidewaysScroll(page);
  const box = await page.getByTestId("nudge").boundingBox();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  await page.getByRole("button", { name: "Dismiss" }).click();
  await expect(page.getByTestId("nudge")).toHaveCount(0);
});

test("a locked phone is asked for the private wording and is offered no link", async ({ page }) => {
  nudgeNext = { id: "n1", text: "Grace Nakato, Motor claim. It is past its deadline.", href: "/app/claims/c1", private: false };
  await page.addInitScript(() => {
    (window as unknown as { IipNative: { isLocked: () => boolean } }).IipNative = { isLocked: () => true };
  });
  await openHome(page);
  await expect(page.getByTestId("nudge")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("nudge-text")).not.toContainText("Grace");
  await expect(page.getByTestId("nudge-open")).toHaveCount(0);
  expect(voiceCalls.some((c) => c.path.includes("locked=true"))).toBe(true);
});

test("nothing is shown when the server has nothing to say", async ({ page }) => {
  await openHome(page);
  await expect.poll(() => voiceCalls.some((c) => c.path.startsWith("nudge")), { timeout: 15_000 }).toBe(true);
  await expect(page.getByTestId("nudge")).toHaveCount(0);
});

// Shared by the live voice engines and the screen. No imports from the app: these files are also run by the unit tests.

/** What the orb shows. The same seven states as the orb component; every one is also written as a word. */
export type VoiceOrbState = "idle" | "listening" | "speaking" | "processing" | "interrupted" | "muted" | "reconnecting";

export type VoiceWho = "you" | "iip";

export type VoiceLine = { id: number; who: VoiceWho; text: string };

/** What the live conversation tells the screen. The engine never touches the page itself. */
export type EngineHandlers = {
  state(state: VoiceOrbState): void;
  level(level: number): void;
  /** The words of one side as they are shown, a few at a time. `final` once that side has finished. */
  caption(who: VoiceWho, text: string, final: boolean): void;
  /** A line of the conversation that is complete. */
  line(who: VoiceWho, text: string): void;
  notice(text: string | null): void;
  /** What the agent is doing now ("Checking the records"), or null when it is not working. */
  step(label: string | null): void;
  /** Charts, tables and drafts the agent put on screen during the call. */
  blocks(blocks: unknown[]): void;
  /** A screen the agent offers to open. Navigation happens only when the person taps. */
  open(label: string, href: string): void;
  conversation(conversationId: string): void;
  ended(reason: "ended" | "dropped" | "error"): void;
};

/** The server side of a call. Real in the app; scripted in the tests. */
export type VoiceApi = {
  prepare(): Promise<{ prepareId: string }>;
  start(request: { prepareId?: string; sdpOffer: string; conversationId?: string; timeZoneOffsetMinutes?: number }): Promise<{ sessionId: string; sdpAnswer: string; maxMinutes: number }>;
  lock(sessionId: string): Promise<void>;
  unlock(sessionId: string): Promise<void>;
  end(sessionId: string): Promise<void>;
  metrics(marks: Record<string, number>): Promise<void>;
  /** The agent's screen events for one call. Returns a function that closes the feed. */
  events(sessionId: string, onEvent: (event: string, data: unknown) => void): () => void;
};

/** The part of a browser peer connection the engine uses, so a test can stand in for it. */
export type PeerLike = {
  connectionState: string;
  iceGatheringState: string;
  localDescription: { sdp?: string } | null;
  onicecandidate: ((event: { candidate: unknown }) => void) | null;
  onconnectionstatechange: (() => void) | null;
  ontrack: ((event: { streams: unknown[] }) => void) | null;
  addTransceiver(kind: "audio", init: { direction: "sendrecv" }): { sender: { replaceTrack(track: unknown | null): Promise<void> } };
  createDataChannel(label: string): ChannelLike;
  createOffer(): Promise<{ type?: string; sdp?: string }>;
  setLocalDescription(description: { type?: string; sdp?: string }): Promise<void>;
  setRemoteDescription(description: { type: "answer"; sdp: string }): Promise<void>;
  close(): void;
};

export type ChannelLike = {
  onopen: (() => void) | null;
  onmessage: ((event: { data: unknown }) => void) | null;
  onclose: (() => void) | null;
};

export type MicrophoneLike = {
  getAudioTracks(): Array<{ enabled: boolean; stop(): void }>;
  getTracks(): Array<{ stop(): void }>;
};

export type EngineDeps = {
  api: VoiceApi;
  createPeer(): PeerLike;
  getMicrophone(): Promise<MicrophoneLike>;
  /** Plays the agent's voice. Returns a stop function. */
  playRemote(stream: unknown): () => void;
  /** A loudness for the orb, 0 to 1, while the microphone is open. Returns a stop function. */
  meter?(stream: MicrophoneLike, onLevel: (level: number) => void): () => void;
  now(): number;
  timeZoneOffsetMinutes?: number;
};

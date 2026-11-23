/**
 * Words appear on screen at about the speed they are spoken. The agent's text arrives well ahead of its voice,
 * so showing it at once would run far in front of what the person hears. The pacer releases whole words only,
 * and drops what has not yet been shown when the person interrupts, so the screen never claims words that were
 * never said aloud.
 */
export class CaptionPacer {
  private pending = "";
  private shown = "";
  private budget = 0;
  private complete = false;

  private readonly charactersPerSecond: number;

  constructor(charactersPerSecond = 16) {
    this.charactersPerSecond = charactersPerSecond;
  }

  /** More of the agent's text, as it arrives. */
  push(text: string) {
    this.pending += text;
  }

  /** No more text is coming for this reply, so the last word is whole. */
  finish() {
    this.complete = true;
  }

  /** Moves time forward and returns everything shown so far. */
  tick(elapsedMilliseconds: number): string {
    this.budget += (elapsedMilliseconds * this.charactersPerSecond) / 1000;
    for (;;) {
      const next = this.nextWord();
      if (next === null || this.budget < Math.min(next.length, 6)) break;
      this.budget -= next.length;
      this.shown += next;
      this.pending = this.pending.slice(next.length);
    }
    return this.shown;
  }

  /** The person spoke over the agent: what was not yet shown is dropped, and what was shown stays. */
  interrupt(): string {
    this.pending = "";
    this.budget = 0;
    return this.shown;
  }

  get text(): string {
    return this.shown;
  }

  /** Everything the agent said has been shown. */
  get drained(): boolean {
    return this.pending.length === 0;
  }

  get done(): boolean {
    return this.complete && this.drained;
  }

  reset() {
    this.pending = "";
    this.shown = "";
    this.budget = 0;
    this.complete = false;
  }

  // A word is released with the space after it. A word with no space after it yet may still be growing, so it waits
  // unless the reply is complete.
  private nextWord(): string | null {
    if (this.pending.length === 0) return null;
    const match = /^\s*\S+\s/.exec(this.pending);
    if (match) return match[0];
    return this.complete ? this.pending : null;
  }
}

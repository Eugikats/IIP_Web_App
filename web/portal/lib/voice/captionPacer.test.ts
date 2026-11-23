import { test } from "node:test";
import assert from "node:assert/strict";
import { CaptionPacer } from "./captionPacer.ts";

test("words appear at about the speed of speech, whole words only", () => {
  const pacer = new CaptionPacer(16);
  pacer.push("There were four claims that came in today. ");
  pacer.finish();
  assert.equal(pacer.tick(0), "");
  const first = pacer.tick(500);
  assert.ok(first.length > 0 && first.length <= 16, first);
  assert.ok(first.endsWith(" "), "a word is released with its space");
  const later = pacer.tick(3000);
  assert.ok(later.length > first.length);
  assert.equal(pacer.drained, true);
});

test("a word still arriving is held back until its end is known", () => {
  const pacer = new CaptionPacer(100);
  pacer.push("Four clai");
  assert.equal(pacer.tick(2000), "Four ");
  pacer.push("ms came in");
  assert.equal(pacer.tick(2000), "Four claims came ");
  assert.equal(pacer.drained, false);
  pacer.finish();
  assert.equal(pacer.tick(2000), "Four claims came in");
  assert.equal(pacer.done, true);
});

test("an interruption keeps what was shown and drops what was not", () => {
  const pacer = new CaptionPacer(16);
  pacer.push("One two three four five six seven eight nine ten eleven twelve ");
  pacer.tick(1000);
  const shown = pacer.interrupt();
  assert.ok(shown.length > 0);
  assert.ok(shown.length < "One two three four five six seven eight nine ten eleven twelve ".length);
  assert.equal(pacer.drained, true);
  assert.equal(pacer.tick(5000), shown);
});

test("reset starts a new reply", () => {
  const pacer = new CaptionPacer(100);
  pacer.push("Hello there ");
  pacer.finish();
  pacer.tick(5000);
  pacer.reset();
  assert.equal(pacer.text, "");
  assert.equal(pacer.done, false);
});

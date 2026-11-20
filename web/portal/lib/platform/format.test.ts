import { test } from "node:test";
import assert from "node:assert/strict";
import { automationStatus, formatWhen, groupBy, humanize, intentHealth, lifecycleIndex, scheduleLabel, severityTone } from "./format.ts";

test("automation statuses have labels and tones", () => {
  assert.deepEqual(automationStatus("awaiting_approval"), { label: "Awaiting approval", tone: "warning" });
  assert.equal(automationStatus("active").tone, "success");
  assert.equal(automationStatus("something_new").label, "Something new");
});

test("lifecycle index follows draft → active, paused and failed sit at the end", () => {
  assert.equal(lifecycleIndex("draft"), 0);
  assert.equal(lifecycleIndex("approved"), 3);
  assert.equal(lifecycleIndex("paused"), 4);
  assert.equal(lifecycleIndex("retired"), -1);
});

test("intent health and severity tones", () => {
  assert.equal(intentHealth("off_track").tone, "danger");
  assert.equal(intentHealth("unknown").label, "On track");
  assert.equal(severityTone("high"), "danger");
  assert.equal(severityTone("low"), "info");
});

test("schedule labels", () => {
  assert.equal(scheduleLabel({ schedule: "hourly" }), "Every hour");
  assert.equal(scheduleLabel({ schedule: "daily", time: "08:00" }), "Daily at 08:00 UTC");
  assert.equal(scheduleLabel({ type: "manual" }), "Manual");
});

test("formatWhen is relative for recent times", () => {
  const now = new Date("2026-11-18T12:00:00Z");
  assert.equal(formatWhen("2026-11-18T11:30:00Z", now), "30 min ago");
  assert.equal(formatWhen("2026-11-18T15:00:00Z", now), "in 3 h");
  assert.equal(formatWhen(null, now), "Not yet");
});

test("groupBy keeps group order and humanize", () => {
  const groups = groupBy([{ d: "Risk" }, { d: "Credit" }, { d: "Risk" }], (x) => x.d);
  assert.deepEqual(groups.map(([k, v]) => [k, v.length]), [["Risk", 2], ["Credit", 1]]);
  assert.equal(humanize("needs_attention"), "Needs attention");
});

import { expect, test } from "@playwright/test";
import { DOCUMENT_IMAGE_TARGET_BYTES, encodingAttempts, needsDownscale } from "../../lib/photoDownscale";
import {
  MAX_ATTEMPTS,
  backoffDelayMs,
  chunkCount,
  fingerprintFor,
  isRetryableStatus,
  percentOf,
} from "../../lib/resumableUpload";

// Unit tests for the parts of slow-connection uploading that are plain arithmetic (build-loop item 23).

test("the wait before a retry doubles from one second and stops growing at thirty", () => {
  const exact = () => 0.5;
  expect(backoffDelayMs(1, exact)).toBe(1000);
  expect(backoffDelayMs(2, exact)).toBe(2000);
  expect(backoffDelayMs(3, exact)).toBe(4000);
  expect(backoffDelayMs(5, exact)).toBe(16000);
  expect(backoffDelayMs(6, exact)).toBe(30000);
  expect(backoffDelayMs(20, exact)).toBe(30000);
});

test("the wait is spread by a fifth either way so many phones do not retry together", () => {
  expect(backoffDelayMs(3, () => 0)).toBe(3200);
  expect(backoffDelayMs(3, () => 1)).toBe(4800);
  for (let attempt = 1; attempt < 10; attempt += 1) {
    const base = Math.min(30000, 1000 * 2 ** (attempt - 1));
    const wait = backoffDelayMs(attempt);
    expect(wait).toBeGreaterThanOrEqual(Math.floor(base * 0.8));
    expect(wait).toBeLessThanOrEqual(Math.ceil(base * 1.2));
  }
});

test("a lost connection and server trouble are retried, a refusal is not", () => {
  for (const status of [0, 408, 425, 429, 500, 502, 503, 504]) expect(isRetryableStatus(status)).toBe(true);
  for (const status of [200, 204, 400, 401, 403, 404, 409, 410, 422]) expect(isRetryableStatus(status)).toBe(false);
  expect(MAX_ATTEMPTS).toBeGreaterThanOrEqual(5);
});

test("progress is a whole percentage that never leaves 0 to 100", () => {
  expect(percentOf(0, 1000)).toBe(0);
  expect(percentOf(499, 1000)).toBe(49);
  expect(percentOf(1000, 1000)).toBe(100);
  expect(percentOf(5000, 1000)).toBe(100);
  expect(percentOf(-5, 1000)).toBe(0);
  expect(percentOf(10, 0)).toBe(0);
});

test("a file is cut into the fewest whole pieces", () => {
  const piece = 256 * 1024;
  expect(chunkCount(0, piece)).toBe(0);
  expect(chunkCount(1, piece)).toBe(1);
  expect(chunkCount(piece, piece)).toBe(1);
  expect(chunkCount(piece + 1, piece)).toBe(2);
  expect(chunkCount(piece * 10, piece)).toBe(10);
});

test("the same file in the same slot has the same identity after a reload, a different one does not", () => {
  const file = { name: "IMG_1.jpg", size: 123456, lastModified: 1700000000000 };
  expect(fingerprintFor(file, "PhotoEvidence")).toBe(fingerprintFor({ ...file }, "PhotoEvidence"));
  expect(fingerprintFor(file, "PhotoEvidence")).not.toBe(fingerprintFor(file, "PoliceAbstract"));
  expect(fingerprintFor(file, "PhotoEvidence")).not.toBe(fingerprintFor({ ...file, size: 123457 }, "PhotoEvidence"));
  expect(fingerprintFor(file, undefined)).toContain("other");
});

test("a page of paperwork is brought under the size target by trying smaller and plainer encodings in order", () => {
  const attempts = encodingAttempts(3000);
  expect(attempts[0]).toEqual({ width: 1600, quality: 0.85 });
  expect(attempts.at(-1)).toEqual({ width: 1000, quality: 0.55 });
  // Never wider than the step before it, never higher quality than the step before it at the same width.
  for (let index = 1; index < attempts.length; index += 1) {
    expect(attempts[index].width).toBeLessThanOrEqual(attempts[index - 1].width);
    if (attempts[index].width === attempts[index - 1].width) expect(attempts[index].quality).toBeLessThan(attempts[index - 1].quality);
  }
});

test("a picture is never enlarged to reach a width", () => {
  for (const attempt of encodingAttempts(900)) expect(attempt.width).toBeLessThanOrEqual(900);
  expect(encodingAttempts(1300).map((attempt) => attempt.width)).toEqual(expect.not.arrayContaining([1600]));
  expect(encodingAttempts(1300).every((attempt) => attempt.width <= 1300)).toBe(true);
});

test("a picture is touched only when it is too wide or too heavy", () => {
  expect(needsDownscale(1200, 100 * 1024)).toBe(false);
  expect(needsDownscale(1601, 100 * 1024)).toBe(true);
  expect(needsDownscale(1200, DOCUMENT_IMAGE_TARGET_BYTES + 1)).toBe(true);
  expect(needsDownscale(1200, DOCUMENT_IMAGE_TARGET_BYTES)).toBe(false);
  expect(DOCUMENT_IMAGE_TARGET_BYTES).toBeLessThanOrEqual(512 * 1024);
});

import { expect, test } from "@playwright/test";
import {
  detectDocument,
  downscale,
  enhance,
  frameQuad,
  homographyToQuad,
  mapThrough,
  outputSize,
  polygonArea,
  rotate90,
  warpPerspective,
  type Pixels,
  type Quad,
} from "../../lib/scan/documentScan";

// Unit tests for the scanner's image maths on synthetic fixtures: a "page" with known bars is
// projected into a "photo" at a known angle, and the maths must find and straighten it again.

const PAGE_W = 300;
const PAGE_H = 420;

/** An upright page: pale paper with dark bars, so orientation and content can be compared. */
function makePage(paper = 235, ink = 25): Pixels {
  const data = new Uint8ClampedArray(PAGE_W * PAGE_H * 4);
  for (let y = 0; y < PAGE_H; y++) {
    for (let x = 0; x < PAGE_W; x++) {
      const bar = (y % 60 > 22 && y % 60 < 34 && x > 30 && x < PAGE_W - 30 - (y % 120 === 0 ? 0 : 60)) || (x > 14 && x < 22 && y > 20);
      const o = (y * PAGE_W + x) * 4;
      const v = bar ? ink : paper;
      data[o] = data[o + 1] = data[o + 2] = v;
      data[o + 3] = 255;
    }
  }
  return { data, width: PAGE_W, height: PAGE_H };
}

function invert3(h: number[]): number[] {
  const [a, b, c, d, e, f, g, i] = h;
  const m = [a, b, c, d, e, f, g, i, 1];
  const det = m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
  const inv = [
    (m[4] * m[8] - m[5] * m[7]) / det, (m[2] * m[7] - m[1] * m[8]) / det, (m[1] * m[5] - m[2] * m[4]) / det,
    (m[5] * m[6] - m[3] * m[8]) / det, (m[0] * m[8] - m[2] * m[6]) / det, (m[2] * m[3] - m[0] * m[5]) / det,
    (m[3] * m[7] - m[4] * m[6]) / det, (m[1] * m[6] - m[0] * m[7]) / det, (m[0] * m[4] - m[1] * m[3]) / det,
  ];
  return inv.map((value) => value / inv[8]);
}

/** A photo of the page lying inside `quad` on a plain background. */
function makePhoto(page: Pixels, quad: Quad, width: number, height: number, background = 55, shadow = false): Pixels {
  const h = homographyToQuad(quad, page.width, page.height);
  const inv = invert3([...h, 1].slice(0, 8));
  const data = new Uint8ClampedArray(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const w = inv[6] * x + inv[7] * y + inv[8];
      const u = (inv[0] * x + inv[1] * y + inv[2]) / w;
      const v = (inv[3] * x + inv[4] * y + inv[5]) / w;
      const o = (y * width + x) * 4;
      let value = background;
      if (u >= 0 && v >= 0 && u < page.width - 1 && v < page.height - 1) {
        value = page.data[(Math.floor(v) * page.width + Math.floor(u)) * 4];
        if (shadow) value = value * (0.55 + 0.45 * (x / width));
      }
      data[o] = data[o + 1] = data[o + 2] = value;
      data[o + 3] = 255;
    }
  }
  return { data, width, height };
}

const PHOTO_QUAD: Quad = [{ x: 150, y: 70 }, { x: 520, y: 100 }, { x: 560, y: 410 }, { x: 110, y: 380 }];
const diagonal = (w: number, h: number) => Math.hypot(w, h);

function greyAt(p: Pixels, x: number, y: number) {
  return p.data[(y * p.width + x) * 4];
}

function meanAbsoluteDifference(a: Pixels, b: Pixels) {
  let total = 0;
  for (let i = 0; i < a.width * a.height; i++) total += Math.abs(a.data[i * 4] - b.data[i * 4]);
  return total / (a.width * a.height);
}

test("a page photographed at an angle is found, with its four corners", () => {
  const photo = makePhoto(makePage(), PHOTO_QUAD, 640, 480);

  const found = detectDocument(photo);

  expect(found.found).toBe(true);
  const tolerance = diagonal(640, 480) * 0.03;
  found.quad.forEach((corner, i) => {
    expect(Math.hypot(corner.x - PHOTO_QUAD[i].x, corner.y - PHOTO_QUAD[i].y)).toBeLessThan(tolerance);
  });
  expect(found.areaRatio).toBeGreaterThan(0.3);
});

test("straightening the found page gives back the original page", () => {
  const page = makePage();
  const photo = makePhoto(page, PHOTO_QUAD, 640, 480);
  const { quad } = detectDocument(photo);
  const size = outputSize(quad);

  const straight = warpPerspective(photo, quad, size.width, size.height);

  // Compare at the page's own size: resample both to 150 by 210 and require a close match.
  const reference = warpPerspective(page, frameQuad(page.width, page.height, 0), 150, 210);
  const result = warpPerspective(straight, frameQuad(straight.width, straight.height, 0), 150, 210);
  expect(meanAbsoluteDifference(reference, result)).toBeLessThan(28);
  // The vertical bar down the left edge is still on the left, so it was not mirrored or turned.
  expect(greyAt(result, 9, 100)).toBeLessThan(120);
  expect(greyAt(result, 140, 100)).toBeGreaterThan(150);
});

test("a dark page on a light table is found too", () => {
  const photo = makePhoto(makePage(40, 210), PHOTO_QUAD, 640, 480, 225);

  const found = detectDocument(photo);

  expect(found.found).toBe(true);
  expect(found.areaRatio).toBeGreaterThan(0.3);
});

test("when no page can be seen it says so and offers the whole frame to adjust", () => {
  const flat: Pixels = { data: new Uint8ClampedArray(320 * 240 * 4).fill(120), width: 320, height: 240 };

  const found = detectDocument(flat);

  expect(found.found).toBe(false);
  expect(found.quad).toEqual(frameQuad(320, 240));
});

test("a page that fills the picture almost edge to edge is not claimed as a small shape", () => {
  const photo = makePhoto(makePage(), [{ x: 2, y: 2 }, { x: 637, y: 2 }, { x: 637, y: 477 }, { x: 2, y: 477 }], 640, 480);

  const found = detectDocument(photo);

  // Either it is found as nearly the whole frame, or not found and the frame is offered: both are fine.
  expect(polygonArea(found.quad) / (640 * 480)).toBeGreaterThan(0.85);
});

test("the projective map sends the page corners to the quad corners and back", () => {
  const h = homographyToQuad(PHOTO_QUAD, PAGE_W, PAGE_H);
  const corners = [mapThrough(h, 0, 0), mapThrough(h, PAGE_W, 0), mapThrough(h, PAGE_W, PAGE_H), mapThrough(h, 0, PAGE_H)];
  corners.forEach((corner, i) => {
    expect(corner.x).toBeCloseTo(PHOTO_QUAD[i].x, 3);
    expect(corner.y).toBeCloseTo(PHOTO_QUAD[i].y, 3);
  });
});

test("output size keeps the page's shape, never enlarges and stays light to send", () => {
  const small = outputSize([{ x: 0, y: 0 }, { x: 300, y: 0 }, { x: 300, y: 420 }, { x: 0, y: 420 }]);
  expect(small).toEqual({ width: 300, height: 420 });

  const big = outputSize([{ x: 0, y: 0 }, { x: 4000, y: 0 }, { x: 4000, y: 5600 }, { x: 0, y: 5600 }]);
  expect(big.width).toBeLessThanOrEqual(1600);
  expect(big.height).toBeLessThanOrEqual(2263);
  expect(big.height / big.width).toBeCloseTo(1.4, 1);
});

test("clean-up stretches a dull photo so paper is white and ink is dark", () => {
  const dull = makePage(160, 95);

  const clean = enhance(dull, "clean");

  expect(greyAt(clean, 150, 5)).toBeGreaterThan(245);
  expect(greyAt(clean, 18, 100)).toBeLessThan(10);
});

test("black and white copes with a shadow across the page", () => {
  const photo = makePhoto(makePage(), frameQuad(PAGE_W, PAGE_H, 0), PAGE_W, PAGE_H, 55, true);

  const bw = enhance(photo, "bw");

  const values = new Set<number>();
  for (let i = 0; i < bw.width * bw.height; i++) values.add(bw.data[i * 4]);
  expect([...values].sort()).toEqual([0, 255]);
  // Paper well away from ink stays white on the shadowed left side as well as the bright right side.
  expect(greyAt(bw, 150, 5)).toBe(255);
  expect(greyAt(bw, 290, 410)).toBe(255);
  // The ink bar down the left is still ink.
  expect(greyAt(bw, 18, 200)).toBe(0);
});

test("grey keeps one value per pixel and original changes nothing", () => {
  const colour: Pixels = { data: new Uint8ClampedArray([200, 50, 50, 255, 20, 120, 220, 255]), width: 2, height: 1 };
  const grey = enhance(makePage(), "grey");
  expect(grey.data[0]).toBe(grey.data[1]);
  expect(grey.data[1]).toBe(grey.data[2]);
  expect(enhance(colour, "original")).toBe(colour);
});

test("turning a page four quarter turns brings it back, and one turn swaps its sides", () => {
  const page = makePage();

  const once = rotate90(page, 1);
  const four = rotate90(page, 4);

  expect(once.width).toBe(PAGE_H);
  expect(once.height).toBe(PAGE_W);
  expect(four).toBe(page);
  // The bar down the left of the upright page runs along the top after a clockwise turn.
  expect(greyAt(once, 200, 18)).toBeLessThan(120);
  expect(Array.from(rotate90(rotate90(rotate90(rotate90(page, 1), 1), 1), 1).data)).toEqual(Array.from(page.data));
});

test("shrinking a photo keeps its average colour", () => {
  const photo = makePhoto(makePage(), PHOTO_QUAD, 640, 480);

  const small = downscale(photo, 160);

  expect(Math.max(small.width, small.height)).toBe(160);
  const mean = (p: Pixels) => p.data.reduce((sum, v, i) => (i % 4 === 0 ? sum + v : sum), 0) / (p.width * p.height);
  expect(Math.abs(mean(small) - mean(photo))).toBeLessThan(3);
});

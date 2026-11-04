// The image maths of the camera document scanner (build-loop item 13): find the page in a photo,
// straighten it, and clean it up. Plain functions over pixel buffers with no DOM, so they run in
// the browser and in unit tests on fixtures. Nothing here talks to a camera or a server.

export type Pixels = { data: Uint8ClampedArray; width: number; height: number };
export type Point = { x: number; y: number };
/** Corners in order: top left, top right, bottom right, bottom left. */
export type Quad = [Point, Point, Point, Point];
export type EnhanceMode = "original" | "clean" | "grey" | "bw";

export type Detection = {
  quad: Quad;
  /** False when no page-like shape was found and the quad is just the frame, for the person to drag. */
  found: boolean;
  /** Share of the picture the quad covers, 0 to 1. */
  areaRatio: number;
};

const DETECT_EDGE = 240;
const MIN_AREA_RATIO = 0.12;
const MIN_RECTANGULARITY = 0.8;

export function frameQuad(width: number, height: number, inset = 0.04): Quad {
  const dx = width * inset;
  const dy = height * inset;
  return [
    { x: dx, y: dy },
    { x: width - dx, y: dy },
    { x: width - dx, y: height - dy },
    { x: dx, y: height - dy },
  ];
}

export function polygonArea(points: Point[]): number {
  let sum = 0;
  for (let i = 0; i < points.length; i++) {
    const a = points[i];
    const b = points[(i + 1) % points.length];
    sum += a.x * b.y - b.x * a.y;
  }
  return Math.abs(sum) / 2;
}

/** Shrinks a picture so its longest side is at most maxEdge, averaging the pixels it merges. */
export function downscale(source: Pixels, maxEdge: number): Pixels {
  const scale = Math.min(1, maxEdge / Math.max(source.width, source.height));
  if (scale >= 1) return source;
  const width = Math.max(1, Math.round(source.width * scale));
  const height = Math.max(1, Math.round(source.height * scale));
  const out = new Uint8ClampedArray(width * height * 4);
  for (let y = 0; y < height; y++) {
    const y0 = Math.floor((y / height) * source.height);
    const y1 = Math.max(y0 + 1, Math.floor(((y + 1) / height) * source.height));
    for (let x = 0; x < width; x++) {
      const x0 = Math.floor((x / width) * source.width);
      const x1 = Math.max(x0 + 1, Math.floor(((x + 1) / width) * source.width));
      let r = 0, g = 0, b = 0, count = 0;
      for (let yy = y0; yy < y1; yy++) {
        for (let xx = x0; xx < x1; xx++) {
          const i = (yy * source.width + xx) * 4;
          r += source.data[i]; g += source.data[i + 1]; b += source.data[i + 2];
          count++;
        }
      }
      const o = (y * width + x) * 4;
      out[o] = r / count; out[o + 1] = g / count; out[o + 2] = b / count; out[o + 3] = 255;
    }
  }
  return { data: out, width, height };
}

export function toGrey(source: Pixels): Uint8Array {
  const grey = new Uint8Array(source.width * source.height);
  for (let i = 0, p = 0; i < grey.length; i++, p += 4) {
    grey[i] = (source.data[p] * 299 + source.data[p + 1] * 587 + source.data[p + 2] * 114) / 1000;
  }
  return grey;
}

function blur3(grey: Uint8Array, width: number, height: number): Uint8Array {
  const out = new Uint8Array(grey.length);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      let sum = 0, count = 0;
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const xx = x + dx, yy = y + dy;
          if (xx >= 0 && yy >= 0 && xx < width && yy < height) { sum += grey[yy * width + xx]; count++; }
        }
      }
      out[y * width + x] = sum / count;
    }
  }
  return out;
}

/** Otsu's threshold: the grey level that best separates a dark group from a bright one. */
export function otsuThreshold(grey: Uint8Array): number {
  const histogram = new Array<number>(256).fill(0);
  for (const value of grey) histogram[value]++;
  const total = grey.length;
  let sumAll = 0;
  for (let i = 0; i < 256; i++) sumAll += i * histogram[i];
  let sumBackground = 0, weightBackground = 0, best = 0, threshold = 128;
  for (let t = 0; t < 256; t++) {
    weightBackground += histogram[t];
    if (weightBackground === 0) continue;
    const weightForeground = total - weightBackground;
    if (weightForeground === 0) break;
    sumBackground += t * histogram[t];
    const meanBackground = sumBackground / weightBackground;
    const meanForeground = (sumAll - sumBackground) / weightForeground;
    const between = weightBackground * weightForeground * (meanBackground - meanForeground) ** 2;
    if (between > best) { best = between; threshold = t; }
  }
  return threshold;
}

/** The pixels of the biggest 4-connected group in a mask, as indexes. */
function largestComponent(mask: Uint8Array, width: number, height: number): number[] {
  const seen = new Uint8Array(mask.length);
  let best: number[] = [];
  const stack: number[] = [];
  for (let start = 0; start < mask.length; start++) {
    if (!mask[start] || seen[start]) continue;
    const component: number[] = [];
    stack.push(start);
    seen[start] = 1;
    while (stack.length > 0) {
      const index = stack.pop()!;
      component.push(index);
      const x = index % width, y = (index - x) / width;
      if (x > 0 && mask[index - 1] && !seen[index - 1]) { seen[index - 1] = 1; stack.push(index - 1); }
      if (x < width - 1 && mask[index + 1] && !seen[index + 1]) { seen[index + 1] = 1; stack.push(index + 1); }
      if (y > 0 && mask[index - width] && !seen[index - width]) { seen[index - width] = 1; stack.push(index - width); }
      if (y < height - 1 && mask[index + width] && !seen[index + width]) { seen[index + width] = 1; stack.push(index + width); }
    }
    if (component.length > best.length) best = component;
  }
  return best;
}

function convexHull(points: Point[]): Point[] {
  const sorted = [...points].sort((a, b) => a.x - b.x || a.y - b.y);
  if (sorted.length < 3) return sorted;
  const cross = (o: Point, a: Point, b: Point) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
  const lower: Point[] = [];
  for (const p of sorted) {
    while (lower.length >= 2 && cross(lower[lower.length - 2], lower[lower.length - 1], p) <= 0) lower.pop();
    lower.push(p);
  }
  const upper: Point[] = [];
  for (let i = sorted.length - 1; i >= 0; i--) {
    const p = sorted[i];
    while (upper.length >= 2 && cross(upper[upper.length - 2], upper[upper.length - 1], p) <= 0) upper.pop();
    upper.push(p);
  }
  lower.pop();
  upper.pop();
  return lower.concat(upper);
}

function quadFromHull(hull: Point[]): Quad {
  const pick = (score: (p: Point) => number, maximise: boolean) =>
    hull.reduce((best, p) => ((maximise ? score(p) > score(best) : score(p) < score(best)) ? p : best), hull[0]);
  return [
    pick((p) => p.x + p.y, false), // top left
    pick((p) => p.x - p.y, true), // top right
    pick((p) => p.x + p.y, true), // bottom right
    pick((p) => p.x - p.y, false), // bottom left
  ];
}

function candidateFromMask(mask: Uint8Array, width: number, height: number): { quad: Quad; areaRatio: number; rectangularity: number } | null {
  const component = largestComponent(mask, width, height);
  if (component.length < width * height * MIN_AREA_RATIO * 0.5) return null;
  // Only the outline matters for the hull: pixels with a neighbour outside the group.
  const inside = new Uint8Array(mask.length);
  for (const index of component) inside[index] = 1;
  const outline: Point[] = [];
  for (const index of component) {
    const x = index % width, y = (index - x) / width;
    if (x === 0 || y === 0 || x === width - 1 || y === height - 1
      || !inside[index - 1] || !inside[index + 1] || !inside[index - width] || !inside[index + width]) {
      outline.push({ x, y });
    }
  }

  const hull = convexHull(outline);
  if (hull.length < 3) return null;
  const quad = quadFromHull(hull);
  const hullArea = polygonArea(hull);
  const quadArea = polygonArea(quad);
  if (hullArea === 0 || quadArea === 0) return null;
  return {
    quad,
    areaRatio: quadArea / (width * height),
    rectangularity: Math.min(component.length, quadArea) / Math.max(component.length, quadArea),
  };
}

/**
 * Finds the page in a photo of paperwork on a table: the biggest bright (or, failing that, dark)
 * area that fills a good part of the picture and is close to a four-cornered shape. When nothing
 * convincing is there it says so and offers the whole frame, for the person to adjust by hand.
 */
export function detectDocument(source: Pixels): Detection {
  const small = downscale(source, DETECT_EDGE);
  const grey = blur3(toGrey(small), small.width, small.height);
  const threshold = otsuThreshold(grey);
  const scale = source.width / small.width;

  let best: { quad: Quad; areaRatio: number; rectangularity: number } | null = null;
  for (const brightPaper of [true, false]) {
    const mask = new Uint8Array(grey.length);
    for (let i = 0; i < grey.length; i++) mask[i] = (brightPaper ? grey[i] > threshold : grey[i] <= threshold) ? 1 : 0;
    const candidate = candidateFromMask(mask, small.width, small.height);
    if (candidate && candidate.areaRatio >= MIN_AREA_RATIO && candidate.areaRatio <= 0.97 && candidate.rectangularity >= MIN_RECTANGULARITY
      && (!best || candidate.rectangularity > best.rectangularity)) {
      best = candidate;
    }
  }

  if (!best) {
    return { quad: frameQuad(source.width, source.height), found: false, areaRatio: 0 };
  }

  const quad = best.quad.map((p) => ({ x: p.x * scale, y: p.y * scale })) as Quad;
  return { quad, found: true, areaRatio: best.areaRatio };
}

// ---------- straightening ----------

/** Solves for the 3 by 3 projective map taking the unit rectangle 0..width, 0..height onto a quad. */
export function homographyToQuad(quad: Quad, width: number, height: number): number[] {
  const dst: Point[] = [{ x: 0, y: 0 }, { x: width, y: 0 }, { x: width, y: height }, { x: 0, y: height }];
  // For each pair (u,v) -> (x,y):  x = (h0 u + h1 v + h2) / (h6 u + h7 v + 1), same for y.
  const matrix: number[][] = [];
  for (let i = 0; i < 4; i++) {
    const { x: u, y: v } = dst[i];
    const { x, y } = quad[i];
    matrix.push([u, v, 1, 0, 0, 0, -u * x, -v * x, x]);
    matrix.push([0, 0, 0, u, v, 1, -u * y, -v * y, y]);
  }
  for (let column = 0; column < 8; column++) {
    let pivot = column;
    for (let row = column + 1; row < 8; row++) if (Math.abs(matrix[row][column]) > Math.abs(matrix[pivot][column])) pivot = row;
    [matrix[column], matrix[pivot]] = [matrix[pivot], matrix[column]];
    const divisor = matrix[column][column];
    if (Math.abs(divisor) < 1e-12) return [1, 0, 0, 0, 1, 0, 0, 0];
    for (let k = column; k < 9; k++) matrix[column][k] /= divisor;
    for (let row = 0; row < 8; row++) {
      if (row === column) continue;
      const factor = matrix[row][column];
      for (let k = column; k < 9; k++) matrix[row][k] -= factor * matrix[column][k];
    }
  }
  return matrix.map((row) => row[8]);
}

export function mapThrough(h: number[], u: number, v: number): Point {
  const w = h[6] * u + h[7] * v + 1;
  return { x: (h[0] * u + h[1] * v + h[2]) / w, y: (h[3] * u + h[4] * v + h[5]) / w };
}

const distance = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y);

/** How big the straightened page should be: the quad's own proportions, never enlarged, and no
 *  larger than about A4 at 200 dpi so a scan stays light to send. */
export function outputSize(quad: Quad, maxShort = 1600, maxLong = 2263): { width: number; height: number } {
  let width = Math.max(distance(quad[0], quad[1]), distance(quad[3], quad[2]));
  let height = Math.max(distance(quad[0], quad[3]), distance(quad[1], quad[2]));
  const limitShort = Math.min(width, height) > maxShort ? maxShort / Math.min(width, height) : 1;
  const limitLong = Math.max(width, height) > maxLong ? maxLong / Math.max(width, height) : 1;
  const scale = Math.min(limitShort, limitLong);
  width = Math.max(64, Math.round(width * scale));
  height = Math.max(64, Math.round(height * scale));
  return { width, height };
}

/** The page inside the quad, straightened to a rectangle (bilinear sampling). */
export function warpPerspective(source: Pixels, quad: Quad, width: number, height: number): Pixels {
  const h = homographyToQuad(quad, width, height);
  const out = new Uint8ClampedArray(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const p = mapThrough(h, x + 0.5, y + 0.5);
      const o = (y * width + x) * 4;
      const fx = p.x - 0.5, fy = p.y - 0.5;
      const x0 = Math.floor(fx), y0 = Math.floor(fy);
      if (x0 < -1 || y0 < -1 || x0 >= source.width || y0 >= source.height) { out[o] = out[o + 1] = out[o + 2] = 255; out[o + 3] = 255; continue; }
      const ax = fx - x0, ay = fy - y0;
      const sample = (sx: number, sy: number, channel: number) => {
        const cx = Math.min(source.width - 1, Math.max(0, sx));
        const cy = Math.min(source.height - 1, Math.max(0, sy));
        return source.data[(cy * source.width + cx) * 4 + channel];
      };
      for (let c = 0; c < 3; c++) {
        out[o + c] =
          sample(x0, y0, c) * (1 - ax) * (1 - ay) + sample(x0 + 1, y0, c) * ax * (1 - ay)
          + sample(x0, y0 + 1, c) * (1 - ax) * ay + sample(x0 + 1, y0 + 1, c) * ax * ay;
      }
      out[o + 3] = 255;
    }
  }
  return { data: out, width, height };
}

export function rotate90(source: Pixels, quarterTurnsClockwise: number): Pixels {
  const turns = ((quarterTurnsClockwise % 4) + 4) % 4;
  if (turns === 0) return source;
  let current = source;
  for (let t = 0; t < turns; t++) {
    const out = new Uint8ClampedArray(current.data.length);
    const width = current.height, height = current.width;
    for (let y = 0; y < current.height; y++) {
      for (let x = 0; x < current.width; x++) {
        const from = (y * current.width + x) * 4;
        const to = (x * width + (current.height - 1 - y)) * 4;
        out[to] = current.data[from]; out[to + 1] = current.data[from + 1]; out[to + 2] = current.data[from + 2]; out[to + 3] = 255;
      }
    }
    current = { data: out, width, height };
  }
  return current;
}

// ---------- clean-up ----------

function levels(source: Pixels): { low: number; high: number } {
  const histogram = new Array<number>(256).fill(0);
  for (let i = 0; i < source.data.length; i += 4) {
    histogram[Math.round((source.data[i] * 299 + source.data[i + 1] * 587 + source.data[i + 2] * 114) / 1000)]++;
  }
  const total = source.width * source.height;
  let low = 0, high = 255, running = 0;
  for (let i = 0; i < 256; i++) { running += histogram[i]; if (running >= total * 0.01) { low = i; break; } }
  running = 0;
  for (let i = 255; i >= 0; i--) { running += histogram[i]; if (running >= total * 0.01) { high = i; break; } }
  return high - low < 20 ? { low: 0, high: 255 } : { low, high };
}

/**
 * Makes a photographed page easier to read. "clean" stretches the contrast so paper goes white and
 * ink dark, keeping colour; "grey" does the same in greys; "bw" makes it pure black and white using
 * the brightness around each pixel, so a shadow across the page does not turn into a black patch.
 */
export function enhance(source: Pixels, mode: EnhanceMode): Pixels {
  if (mode === "original") return source;
  const { low, high } = levels(source);
  const range = Math.max(1, high - low);
  const out = new Uint8ClampedArray(source.data.length);
  const stretch = (v: number) => Math.min(255, Math.max(0, ((v - low) * 255) / range));

  if (mode === "clean") {
    for (let i = 0; i < out.length; i += 4) {
      out[i] = stretch(source.data[i]); out[i + 1] = stretch(source.data[i + 1]); out[i + 2] = stretch(source.data[i + 2]); out[i + 3] = 255;
    }
    return { data: out, width: source.width, height: source.height };
  }

  const grey = new Uint8Array(source.width * source.height);
  for (let i = 0, p = 0; i < grey.length; i++, p += 4) {
    grey[i] = stretch((source.data[p] * 299 + source.data[p + 1] * 587 + source.data[p + 2] * 114) / 1000);
  }

  if (mode === "grey") {
    for (let i = 0, p = 0; i < grey.length; i++, p += 4) { out[p] = out[p + 1] = out[p + 2] = grey[i]; out[p + 3] = 255; }
    return { data: out, width: source.width, height: source.height };
  }

  // Black and white: compare each pixel with the average of the area around it.
  const { width, height } = source;
  const integral = new Float64Array((width + 1) * (height + 1));
  for (let y = 0; y < height; y++) {
    let row = 0;
    for (let x = 0; x < width; x++) {
      row += grey[y * width + x];
      integral[(y + 1) * (width + 1) + (x + 1)] = integral[y * (width + 1) + (x + 1)] + row;
    }
  }
  const radius = Math.max(8, Math.round(Math.min(width, height) / 16));
  for (let y = 0; y < height; y++) {
    const y0 = Math.max(0, y - radius), y1 = Math.min(height, y + radius + 1);
    for (let x = 0; x < width; x++) {
      const x0 = Math.max(0, x - radius), x1 = Math.min(width, x + radius + 1);
      const sum = integral[y1 * (width + 1) + x1] - integral[y0 * (width + 1) + x1] - integral[y1 * (width + 1) + x0] + integral[y0 * (width + 1) + x0];
      const mean = sum / ((x1 - x0) * (y1 - y0));
      const value = grey[y * width + x] < mean * 0.88 ? 0 : 255;
      const p = (y * width + x) * 4;
      out[p] = out[p + 1] = out[p + 2] = value; out[p + 3] = 255;
    }
  }
  return { data: out, width, height };
}

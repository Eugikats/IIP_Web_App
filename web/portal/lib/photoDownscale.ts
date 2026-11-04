"use client";

const MAX_WIDTH = 1600;

/** A page of paperwork should be about this many bytes once sent, so a customer on a paid bundle is not asked to send megabytes. */
export const DOCUMENT_IMAGE_TARGET_BYTES = 450 * 1024;

export type SlotKind = "Document" | "Evidence";

export type EncodingAttempt = { width: number; quality: number };

/**
 * The widths and JPEG qualities to try, best looking first, to bring a picture of paperwork under the size
 * target (build-loop item 23). Text stays readable down to the last step; if even that is too big the smallest
 * is used anyway, since it is always smaller than the original. A picture narrower than a width is not
 * enlarged, so steps wider than the picture are dropped. Pure so it can be tested without a canvas.
 */
export function encodingAttempts(pictureWidth: number): EncodingAttempt[] {
  const widths = [MAX_WIDTH, 1400, 1200, 1000].filter((width, index) => width <= pictureWidth || index === 0);
  const attempts: EncodingAttempt[] = [];
  for (const width of widths) {
    for (const quality of [0.85, 0.75, 0.65, 0.55]) {
      attempts.push({ width: Math.min(width, pictureWidth), quality });
    }
  }

  return attempts;
}

/** Whether a picture of paperwork needs touching at all: too wide, or too heavy for what it shows. */
export function needsDownscale(pictureWidth: number, fileBytes: number): boolean {
  return pictureWidth > MAX_WIDTH || fileBytes > DOCUMENT_IMAGE_TARGET_BYTES;
}

/**
 * The one place a file is changed before it is sent (build-loop item 11), and only for a document
 * slot. A customer on a paid data bundle should not send an 8 MB phone photo of a letter, so a
 * picture of paperwork wider than MAX_WIDTH, or heavier than the size target, is re-encoded as a JPEG
 * that fits it. Evidence (photos of the damage) is NEVER touched: it is sent exactly as taken, with its
 * metadata, because it may be examined later (a slow connection is handled by sending it in pieces, not by
 * shrinking it). Anything that is not an image, a HEIC file the browser cannot decode, or a file already
 * small enough is also sent unchanged.
 */
export async function prepareFileForSlot(file: File, kind: SlotKind): Promise<File> {
  return kind === "Evidence" ? file : downscalePhotoIfNeeded(file);
}

/**
 * Re-encodes a picture of paperwork to fit the size target. Never throws: canvas support is inconsistent
 * across older phone browsers, and a failed downscale should still let the original file upload rather
 * than block the customer. Use prepareFileForSlot, which never applies it to evidence.
 */
async function downscalePhotoIfNeeded(file: File): Promise<File> {
  if (!file.type.startsWith("image/") || file.type === "image/heic" || file.type === "image/heif") {
    return file;
  }

  try {
    const bitmap = await createImageBitmap(file);
    if (!needsDownscale(bitmap.width, file.size)) {
      bitmap.close?.();
      return file;
    }

    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");
    if (!context) {
      bitmap.close?.();
      return file;
    }

    let best: Blob | null = null;
    for (const attempt of encodingAttempts(bitmap.width)) {
      canvas.width = attempt.width;
      canvas.height = Math.round(bitmap.height * (attempt.width / bitmap.width));
      context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", attempt.quality));
      if (!blob) continue;
      best = blob;
      if (blob.size <= DOCUMENT_IMAGE_TARGET_BYTES) break;
    }

    bitmap.close?.();
    // Never send something bigger than what the customer chose.
    if (!best || best.size >= file.size) return file;

    const downscaledName = file.name.replace(/\.[^.]+$/, "") + ".jpg";
    return new File([best], downscaledName, { type: "image/jpeg", lastModified: Date.now() });
  } catch {
    return file;
  }
}

export function offlineUploadMessage(): string {
  return "You appear to be offline. Connect and try the upload again.";
}

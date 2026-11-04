"use client";

const MAX_WIDTH = 1600;
const JPEG_QUALITY = 0.85;

export type SlotKind = "Document" | "Evidence";

/**
 * The one place a file is changed before it is sent (build-loop item 11), and only for a document
 * slot. A customer on a paid data bundle should not send an 8 MB phone photo of a letter, so a
 * picture of paperwork wider than MAX_WIDTH is scaled down to a JPEG. Evidence (photos of the
 * damage) is NEVER touched: it is sent exactly as taken, with its metadata, because it may be
 * examined later. Anything that is not an image, a HEIC file the browser cannot decode, or a file
 * already small enough is also sent unchanged.
 */
export async function prepareFileForSlot(file: File, kind: SlotKind): Promise<File> {
  return kind === "Evidence" ? file : downscalePhotoIfNeeded(file);
}

/**
 * Downscales an image wider than MAX_WIDTH. Never throws: canvas support is inconsistent across
 * older phone browsers, and a failed downscale should still let the original file upload rather
 * than block the customer. Use prepareFileForSlot, which never applies it to evidence.
 */
async function downscalePhotoIfNeeded(file: File): Promise<File> {
  if (!file.type.startsWith("image/") || file.type === "image/heic" || file.type === "image/heif") {
    return file;
  }

  try {
    const bitmap = await createImageBitmap(file);
    if (bitmap.width <= MAX_WIDTH) {
      bitmap.close?.();
      return file;
    }

    const scale = MAX_WIDTH / bitmap.width;
    const targetWidth = MAX_WIDTH;
    const targetHeight = Math.round(bitmap.height * scale);

    const canvas = document.createElement("canvas");
    canvas.width = targetWidth;
    canvas.height = targetHeight;
    const context = canvas.getContext("2d");
    if (!context) return file;
    context.drawImage(bitmap, 0, 0, targetWidth, targetHeight);
    bitmap.close?.();

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", JPEG_QUALITY));
    if (!blob) return file;

    const downscaledName = file.name.replace(/\.[^.]+$/, "") + ".jpg";
    return new File([blob], downscaledName, { type: "image/jpeg", lastModified: Date.now() });
  } catch {
    return file;
  }
}

export function offlineUploadMessage(): string {
  return "You appear to be offline. Connect and try the upload again.";
}

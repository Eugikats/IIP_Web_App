"use client";

const MAX_WIDTH = 1600;
const JPEG_QUALITY = 0.85;

/**
 * A customer on a paid data bundle should not send an 8 MB phone photo (claim intake build
 * prompt outcome B). Downscales an image file wider than MAX_WIDTH to a JPEG at that width before
 * upload; anything that is not an image (a PDF, a HEIC file the browser cannot decode into a
 * canvas) or that is already small enough is returned unchanged. Never throws: canvas support is
 * inconsistent across older phone browsers, and a failed downscale should still let the original
 * file upload rather than block the customer.
 */
export async function downscalePhotoIfNeeded(file: File): Promise<File> {
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

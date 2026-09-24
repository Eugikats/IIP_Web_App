/**
 * Client-side image downscale for low-bandwidth claim photo upload.
 * PDFs and non-images pass through unchanged. Does not invent offline queues,
 * callers must surface honest connection failures.
 */
export async function compressImageForUpload(
  file: File,
  options?: { maxEdgePx?: number; jpegQuality?: number },
): Promise<File> {
  if (!file.type.startsWith("image/") || file.type === "image/svg+xml") {
    return file;
  }

  const maxEdgePx = options?.maxEdgePx ?? 1800;
  const jpegQuality = options?.jpegQuality ?? 0.75;

  const bitmap = await createImageBitmap(file);
  try {
    const scale = Math.min(1, maxEdgePx / Math.max(bitmap.width, bitmap.height));
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));

    if (scale >= 1 && file.type === "image/jpeg" && file.size <= 900_000) {
      return file;
    }

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) {
      return file;
    }

    context.drawImage(bitmap, 0, 0, width, height);
    const blob = await new Promise<Blob | null>((resolve) => {
      canvas.toBlob(resolve, "image/jpeg", jpegQuality);
    });
    if (!blob) {
      return file;
    }

    const baseName = file.name.replace(/\.[^.]+$/, "") || "photo";
    return new File([blob], `${baseName}.jpg`, { type: "image/jpeg", lastModified: Date.now() });
  } finally {
    bitmap.close();
  }
}

export function offlineUploadMessage(): string {
  return "You appear to be offline. Connect and try the upload again.";
}

"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { PointerEvent as ReactPointerEvent } from "react";
import { Camera, Loader2, RotateCw, X } from "lucide-react";
import { Button } from "@/components/ui/Button";
import {
  detectDocument,
  enhance,
  frameQuad,
  outputSize,
  rotate90,
  warpPerspective,
  type EnhanceMode,
  type Pixels,
  type Quad,
} from "@/lib/scan/documentScan";

type DocumentScannerProps = {
  /** What is being scanned, for the screen reader and the heading. */
  label: string;
  /** The finished page as a JPEG file. */
  onDone: (file: File) => void;
  onCancel: () => void;
  /** The camera, or the maths, could not be used: the caller falls back to the plain camera button. */
  onUnavailable: (reason: string) => void;
};

type Stage = "camera" | "adjust" | "result";

const MAX_CAPTURE_EDGE = 2000;
const MODES: { mode: EnhanceMode; label: string }[] = [
  { mode: "clean", label: "Clean" },
  { mode: "grey", label: "Grey" },
  { mode: "bw", label: "Black and white" },
  { mode: "original", label: "Original" },
];

function drawPixels(canvas: HTMLCanvasElement | null, pixels: Pixels) {
  if (!canvas) return;
  canvas.width = pixels.width;
  canvas.height = pixels.height;
  canvas.getContext("2d")?.putImageData(new ImageData(new Uint8ClampedArray(pixels.data), pixels.width, pixels.height), 0, 0);
}

/**
 * The camera document scanner (build-loop item 13): take a picture of a page, drag its four
 * corners onto the page (they start where the page was found), straighten and clean it up, and use
 * it as the next page of the document. Loaded only when a customer taps Scan; if the camera or
 * anything here fails, the caller opens the plain camera instead, so a customer is never stuck.
 */
export default function DocumentScanner({ label, onDone, onCancel, onUnavailable }: DocumentScannerProps) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const adjustCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const resultCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const svgRef = useRef<SVGSVGElement | null>(null);
  const draggingRef = useRef<number | null>(null);

  const [stage, setStage] = useState<Stage>("camera");
  const [cameraReady, setCameraReady] = useState(false);
  const [captured, setCaptured] = useState<Pixels | null>(null);
  const [quad, setQuad] = useState<Quad | null>(null);
  const [found, setFound] = useState(true);
  const [straight, setStraight] = useState<Pixels | null>(null);
  const [mode, setMode] = useState<EnhanceMode>("clean");
  const [turns, setTurns] = useState(0);
  const [busy, setBusy] = useState(false);

  const stopCamera = useCallback(() => {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
  }, []);

  // Open the rear camera whenever we are on the camera step.
  useEffect(() => {
    if (stage !== "camera") return;
    let cancelled = false;
    setCameraReady(false);
    (async () => {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 } },
          audio: false,
        });
        if (cancelled) { stream.getTracks().forEach((track) => track.stop()); return; }
        streamRef.current = stream;
        const video = videoRef.current;
        if (!video) return;
        video.srcObject = stream;
        await video.play();
        if (!cancelled) setCameraReady(true);
      } catch {
        if (!cancelled) { stopCamera(); onUnavailable("the camera could not be opened"); }
      }
    })();
    return () => { cancelled = true; stopCamera(); };
  }, [stage, onUnavailable, stopCamera]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => { if (event.key === "Escape") onCancel(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onCancel]);

  function capture() {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    try {
      const scale = Math.min(1, MAX_CAPTURE_EDGE / Math.max(video.videoWidth, video.videoHeight));
      const width = Math.round(video.videoWidth * scale);
      const height = Math.round(video.videoHeight * scale);
      const canvas = document.createElement("canvas");
      canvas.width = width;
      canvas.height = height;
      const context = canvas.getContext("2d");
      if (!context) throw new Error("no canvas");
      context.drawImage(video, 0, 0, width, height);
      const image = context.getImageData(0, 0, width, height);
      const pixels: Pixels = { data: image.data, width, height };
      let detection;
      try {
        detection = detectDocument(pixels);
      } catch {
        detection = { quad: frameQuad(width, height), found: false, areaRatio: 0 };
      }

      stopCamera();
      setCaptured(pixels);
      setQuad(detection.quad);
      setFound(detection.found);
      setStage("adjust");
    } catch {
      onUnavailable("the picture could not be processed");
    }
  }

  // Show the captured picture under the corner handles.
  useEffect(() => {
    if (stage === "adjust" && captured) drawPixels(adjustCanvasRef.current, captured);
  }, [stage, captured]);

  function moveHandle(event: ReactPointerEvent<SVGCircleElement>, index: number) {
    if (draggingRef.current !== index || !captured || !svgRef.current || !quad) return;
    const rect = svgRef.current.getBoundingClientRect();
    const x = Math.min(captured.width, Math.max(0, ((event.clientX - rect.left) / rect.width) * captured.width));
    const y = Math.min(captured.height, Math.max(0, ((event.clientY - rect.top) / rect.height) * captured.height));
    setQuad(quad.map((point, i) => (i === index ? { x, y } : point)) as Quad);
  }

  function straighten() {
    if (!captured || !quad) return;
    setBusy(true);
    // Let the "working" state paint before the heavy loop runs.
    setTimeout(() => {
      try {
        const size = outputSize(quad);
        setStraight(warpPerspective(captured, quad, size.width, size.height));
        setTurns(0);
        setStage("result");
      } catch {
        onUnavailable("the page could not be straightened");
      } finally {
        setBusy(false);
      }
    }, 20);
  }

  const finished = useMemo(() => (straight ? enhance(rotate90(straight, turns), mode) : null), [straight, turns, mode]);

  useEffect(() => {
    if (stage === "result" && finished) drawPixels(resultCanvasRef.current, finished);
  }, [stage, finished]);

  function useThisPage() {
    const canvas = resultCanvasRef.current;
    if (!canvas) return;
    setBusy(true);
    canvas.toBlob((blob) => {
      setBusy(false);
      if (!blob) { onUnavailable("the page could not be saved"); return; }
      onDone(new File([blob], `scan_${Date.now()}.jpg`, { type: "image/jpeg", lastModified: Date.now() }));
    }, "image/jpeg", 0.85);
  }

  const handleRadius = captured ? Math.max(captured.width, captured.height) * 0.03 : 12;

  return (
    <div role="dialog" aria-modal="true" aria-label={`Scan ${label}`} data-testid="document-scanner" className="fixed inset-0 z-50 flex flex-col bg-dls-bg-page">
      <div className="flex items-center justify-between border-b border-dls-border-subtle bg-dls-surface px-4 py-3">
        <div className="text-sm font-semibold text-dls-text-primary">Scan: {label}</div>
        <Button variant="ghost" onClick={onCancel} icon={<X aria-hidden="true" />} aria-label="Close the scanner">Close</Button>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto p-4">
        {stage === "camera" && (
          <div className="mx-auto max-w-xl">
            <p className="mb-3 text-xs leading-5 text-dls-text-muted">Put the page flat on a plain surface in good light, with the whole page in view, then press Capture.</p>
            <video ref={videoRef} playsInline muted className="aspect-[3/4] w-full rounded-dls-card bg-black object-cover" data-testid="scanner-video" />
            {!cameraReady && <p className="mt-2 text-xs text-dls-text-muted">Opening the camera...</p>}
          </div>
        )}

        {stage === "adjust" && captured && quad && (
          <div className="mx-auto max-w-xl">
            <p className="mb-3 text-xs leading-5 text-dls-text-muted" role="status">
              {found ? "We found the page. Drag a corner if it is not exactly on the edge." : "We could not find the edges of the page. Drag the four corners onto it."}
            </p>
            <div className="relative touch-none select-none">
              <canvas ref={adjustCanvasRef} className="block w-full rounded-dls-card" aria-label="Your picture" />
              <svg ref={svgRef} viewBox={`0 0 ${captured.width} ${captured.height}`} className="absolute inset-0 h-full w-full" data-testid="scanner-corners">
                <polygon points={quad.map((p) => `${p.x},${p.y}`).join(" ")} fill="rgba(91,42,232,0.12)" stroke="#5B2AE8" strokeWidth={Math.max(2, handleRadius / 8)} />
                {quad.map((point, index) => (
                  <circle
                    key={index}
                    cx={point.x}
                    cy={point.y}
                    r={handleRadius}
                    fill="#5B2AE8"
                    fillOpacity={0.85}
                    stroke="#fff"
                    strokeWidth={handleRadius / 5}
                    tabIndex={0}
                    role="slider"
                    aria-label={["Top left corner", "Top right corner", "Bottom right corner", "Bottom left corner"][index]}
                    data-testid={`scanner-corner-${index}`}
                    style={{ touchAction: "none", cursor: "grab" }}
                    onPointerDown={(event) => { draggingRef.current = index; event.currentTarget.setPointerCapture(event.pointerId); }}
                    onPointerMove={(event) => moveHandle(event, index)}
                    onPointerUp={(event) => { draggingRef.current = null; event.currentTarget.releasePointerCapture(event.pointerId); }}
                  />
                ))}
              </svg>
            </div>
          </div>
        )}

        {stage === "result" && (
          <div className="mx-auto max-w-xl">
            <canvas ref={resultCanvasRef} className="block w-full rounded-dls-card border border-dls-border-subtle bg-white" aria-label="The straightened page" data-testid="scanner-result" />
            <div role="group" aria-label="Clean-up" className="mt-3 flex flex-wrap gap-2">
              {MODES.map((option) => (
                <Button key={option.mode} variant={mode === option.mode ? "secondary" : "ghost"} aria-pressed={mode === option.mode} onClick={() => setMode(option.mode)}>{option.label}</Button>
              ))}
              <Button variant="ghost" icon={<RotateCw aria-hidden="true" />} onClick={() => setTurns((value) => (value + 1) % 4)}>Turn</Button>
            </div>
          </div>
        )}
      </div>

      <div className="flex flex-wrap gap-2 border-t border-dls-border-subtle bg-dls-surface px-4 py-3">
        {stage === "camera" && (
          <Button variant="primary" onClick={capture} disabled={!cameraReady} icon={<Camera aria-hidden="true" />}>Capture</Button>
        )}
        {stage === "adjust" && (
          <>
            <Button variant="primary" onClick={straighten} disabled={busy} icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}>Looks right</Button>
            <Button variant="ghost" onClick={() => captured && setQuad(frameQuad(captured.width, captured.height))}>Reset corners</Button>
            <Button variant="ghost" onClick={() => setStage("camera")}>Retake</Button>
          </>
        )}
        {stage === "result" && (
          <>
            <Button variant="primary" onClick={useThisPage} disabled={busy} icon={busy ? <Loader2 className="animate-spin" aria-hidden="true" /> : undefined}>Use this page</Button>
            <Button variant="ghost" onClick={() => setStage("adjust")}>Adjust corners</Button>
            <Button variant="ghost" onClick={() => setStage("camera")}>Retake</Button>
          </>
        )}
      </div>
    </div>
  );
}

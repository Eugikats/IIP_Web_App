"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { FileText, Loader2 } from "lucide-react";

/**
 * The source document with every extracted value outlined on it. Hovering or focusing a value on the
 * page lights its field in the list, and the list does the same to the page, so a reviewer can check a
 * value against the exact words it came from. Boxes come from the extraction provider as fractions of
 * the page; a field the provider gave no position for is simply not outlined, and nothing is invented.
 */

export type SourceBox = {
  pageNumber: number;
  x: number;
  y: number;
  width: number;
  height: number;
  /** Exact corners (x0, y0, x1, y1, ...) as fractions of the page. */
  polygon?: number[] | null;
  /** The separate pieces of a value that spans lines or pages. */
  parts?: SourceBox[] | null;
};

export type SourceField = {
  name: string;
  label: string;
  confidence?: number | null;
  box?: SourceBox | null;
};

type Props = {
  url?: string;
  contentType?: string;
  fileName: string;
  error?: string;
  fields: SourceField[];
  /** The field to light up: hovered or focused anywhere, else the one being edited. */
  activeField: string | null;
  /** A field to bring into view (set when the list, not the page, started the highlight). */
  scrollToField?: string | null;
  onActiveChange: (name: string | null) => void;
  onSelect: (name: string) => void;
};

const MAX_PAGES = 20;
const LOW_CONFIDENCE = 0.7;

function regionsOf(box: SourceBox): SourceBox[] {
  return box.parts && box.parts.length > 0 ? box.parts : [box];
}

function pointsOf(region: SourceBox): string | null {
  const p = region.polygon;
  if (!p || p.length < 6 || p.length % 2 !== 0) return null;
  const pairs: string[] = [];
  for (let i = 0; i < p.length; i += 2) pairs.push(`${p[i]},${p[i + 1]}`);
  return pairs.join(" ");
}

function PageOverlay({
  pageNumber,
  fields,
  activeField,
  onActiveChange,
  onSelect,
}: { pageNumber: number } & Pick<Props, "fields" | "activeField" | "onActiveChange" | "onSelect">) {
  const onPage = fields.flatMap((field) =>
    field.box
      ? regionsOf(field.box)
          .filter((region) => region.pageNumber === pageNumber)
          .map((region, index) => ({ field, region, key: `${field.name}-${index}` }))
      : [],
  );
  if (onPage.length === 0) return null;
  // Shapes keep one fixed order, so a press that starts on one is released on the same node and counts as a
  // click. The active field is drawn again on top, as a layer that takes no pointer events.
  const activeShapes = onPage.filter(({ field }) => field.name === activeField);

  return (
    <svg
      className="pointer-events-none absolute inset-0 h-full w-full"
      viewBox="0 0 1 1"
      preserveAspectRatio="none"
      aria-label={`Extracted values on page ${pageNumber}`}
    >
      {onPage.map(({ field, region, key }) => {
        const active = field.name === activeField;
        const low = typeof field.confidence === "number" && field.confidence < LOW_CONFIDENCE;
        return (
          <g
            key={key}
            data-field={field.name}
            data-active={active ? "true" : undefined}
            role="button"
            tabIndex={0}
            aria-label={`${field.label} on the page${low ? ", low confidence" : ""}`}
            onMouseEnter={() => onActiveChange(field.name)}
            onMouseLeave={() => onActiveChange(null)}
            onFocus={() => onActiveChange(field.name)}
            onBlur={() => onActiveChange(null)}
            onClick={() => onSelect(field.name)}
            onKeyDown={(event) => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                onSelect(field.name);
              }
            }}
            className="outline-none"
          >
            <Shape region={region} look={active ? "active" : low ? "low" : "normal"} interactive />
          </g>
        );
      })}
      <g aria-hidden="true" data-testid="active-layer">
        {activeShapes.map(({ region, key }) => (
          <Shape key={`top-${key}`} region={region} look="active" />
        ))}
      </g>
    </svg>
  );
}

function Shape({ region, look, interactive }: { region: SourceBox; look: "normal" | "low" | "active"; interactive?: boolean }) {
  const points = pointsOf(region);
  const shared = {
    fill: look === "active" ? "rgb(var(--color-primary) / 0.16)" : "rgb(var(--color-primary) / 0.03)",
    stroke: look === "active" ? "rgb(var(--color-primary))" : look === "low" ? "rgb(var(--color-warning-dot))" : "rgb(var(--color-primary) / 0.45)",
    strokeWidth: look === "active" ? 2 : 1,
    strokeDasharray: look === "low" ? "4 3" : undefined,
    vectorEffect: "non-scaling-stroke" as const,
    style: { pointerEvents: interactive ? ("all" as const) : ("none" as const), cursor: interactive ? "pointer" : undefined },
  };
  return points ? (
    <polygon points={points} {...shared} />
  ) : (
    <rect x={region.x} y={region.y} width={region.width} height={region.height} {...shared} />
  );
}

type PdfPage = { pageNumber: number; width: number; height: number; canvas: HTMLCanvasElement };

function PdfPages({ url, ...overlay }: { url: string } & Pick<Props, "fields" | "activeField" | "onActiveChange" | "onSelect">) {
  const [pages, setPages] = useState<PdfPage[]>([]);
  const [total, setTotal] = useState(0);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");

  useEffect(() => {
    let cancelled = false;
    let destroy: (() => void) | undefined;
    setPages([]);
    setState("loading");
    (async () => {
      try {
        const pdfjs = await import("pdfjs-dist");
        pdfjs.GlobalWorkerOptions.workerSrc = new URL("pdfjs-dist/build/pdf.worker.min.mjs", import.meta.url).toString();
        const task = pdfjs.getDocument({ url });
        destroy = () => void task.destroy();
        const doc = await task.promise;
        if (cancelled) return;
        setTotal(doc.numPages);
        const ratio = Math.min(window.devicePixelRatio || 1, 2);
        for (let number = 1; number <= Math.min(doc.numPages, MAX_PAGES); number += 1) {
          const page = await doc.getPage(number);
          // Render near the screen's reading width; the browser scales it to the panel.
          const base = page.getViewport({ scale: 1 });
          const viewport = page.getViewport({ scale: (1100 / base.width) * ratio });
          const canvas = document.createElement("canvas");
          canvas.width = Math.floor(viewport.width);
          canvas.height = Math.floor(viewport.height);
          await page.render({ canvas, viewport }).promise;
          if (cancelled) return;
          setPages((current) => [...current, { pageNumber: number, width: canvas.width, height: canvas.height, canvas }]);
          if (number === 1) setState("ready");
        }
        setState("ready");
      } catch {
        if (!cancelled) setState("error");
      }
    })();
    return () => {
      cancelled = true;
      destroy?.();
    };
  }, [url]);

  if (state === "error") {
    return <p role="alert" className="m-0 p-6 text-center text-[13px] text-dls-text-secondary">This PDF could not be drawn here. The original is unchanged.</p>;
  }
  if (pages.length === 0) {
    return (
      <div className="grid min-h-[320px] place-items-center text-[13px] text-dls-text-secondary" role="status">
        <span className="inline-flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Drawing the document</span>
      </div>
    );
  }
  return (
    <div className="space-y-3 p-3">
      {pages.map((page) => (
        <div key={page.pageNumber} data-testid="source-page" data-page={page.pageNumber} className="relative mx-auto w-full max-w-[900px] bg-white shadow-none ring-1 ring-dls-border-subtle" style={{ aspectRatio: `${page.width} / ${page.height}` }}>
          <CanvasMount canvas={page.canvas} />
          <PageOverlay pageNumber={page.pageNumber} {...overlay} />
          <span className="absolute left-2 top-2 rounded-dls-tight bg-dls-surface/90 px-1.5 py-0.5 text-[10.5px] font-medium text-dls-text-muted">Page {page.pageNumber}</span>
        </div>
      ))}
      {total > MAX_PAGES ? <p className="m-0 text-center text-xs text-dls-text-muted">Showing the first {MAX_PAGES} of {total} pages.</p> : null}
    </div>
  );
}

function CanvasMount({ canvas }: { canvas: HTMLCanvasElement }) {
  const host = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = host.current;
    if (!node) return;
    canvas.style.width = "100%";
    canvas.style.height = "100%";
    canvas.style.display = "block";
    node.replaceChildren(canvas);
    return () => node.replaceChildren();
  }, [canvas]);
  return <div ref={host} className="absolute inset-0" aria-hidden="true" />;
}

export default function DocumentSourceViewer({ url, contentType, fileName, error, fields, activeField, scrollToField, onActiveChange, onSelect }: Props) {
  const rootRef = useRef<HTMLDivElement>(null);
  const located = useMemo(() => fields.filter((field) => field.box).length, [fields]);
  const isPdf = contentType === "application/pdf";
  const isImage = !!contentType?.startsWith("image/");

  useEffect(() => {
    if (!scrollToField || !rootRef.current) return;
    const target = Array.from(rootRef.current.querySelectorAll<SVGElement>("[data-field]")).find((node) => node.dataset.field === scrollToField);
    const calm = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    target?.scrollIntoView({ block: "center", behavior: calm ? "auto" : "smooth" });
  }, [scrollToField]);

  const overlay = { fields, activeField, onActiveChange, onSelect };

  return (
    <div ref={rootRef} data-testid="source-viewer">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2 text-xs text-dls-text-secondary">
        <span data-testid="source-located">
          {fields.length === 0
            ? "No extracted values to place on the page."
            : located === 0
              ? "The extraction provider gave no page positions for this document, so no values are outlined."
              : `${located} of ${fields.length} values are outlined on the page. Hover one to see its field.`}
        </span>
        {located > 0 ? (
          <span className="inline-flex items-center gap-3 text-dls-text-muted">
            <span className="inline-flex items-center gap-1"><i className="inline-block h-2.5 w-4 border border-dls-primary/50" aria-hidden="true" /> Outlined</span>
            <span className="inline-flex items-center gap-1"><i className="inline-block h-2.5 w-4 border border-dashed" style={{ borderColor: "rgb(var(--color-warning-dot))" }} aria-hidden="true" /> Low confidence</span>
          </span>
        ) : null}
      </div>

      <div className="max-h-[820px] min-h-[420px] overflow-auto rounded-dls-card border border-dls-border-subtle bg-dls-bg-page">
        {url && isPdf ? <PdfPages url={url} {...overlay} /> : null}
        {url && isImage ? (
          <div className="p-3">
            <div className="relative mx-auto w-fit max-w-full">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={url} alt={`Authorized preview of ${fileName}`} className="block max-h-[760px] max-w-full" />
              <PageOverlay pageNumber={1} {...overlay} />
            </div>
          </div>
        ) : null}
        {url && !isPdf && !isImage ? (
          <div className="grid min-h-[420px] place-items-center p-8 text-center text-[13px] text-dls-text-secondary">This file type cannot be shown here. The original is unchanged.</div>
        ) : null}
        {!url ? (
          <div className="grid min-h-[420px] place-items-center p-8 text-center" role={error ? "alert" : "status"}>
            <div>
              <FileText className="mx-auto h-8 w-8 text-dls-text-muted" aria-hidden="true" />
              <div className="mt-3 text-[13px] font-bold text-dls-text-primary">{error ? "Source preview unavailable" : "Loading authorized source preview…"}</div>
              <div className="mt-1 max-w-md text-[11.5px] leading-5 text-dls-text-muted">{error ?? "This preview comes from your own workspace’s documents."}</div>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}

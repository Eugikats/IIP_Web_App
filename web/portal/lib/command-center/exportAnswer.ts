// Exporting a Command Center chart or table: as a picture (PNG), a PDF, or an Excel workbook (.xlsx).
// Everything is built in the browser from the same numbers the chart shows. Nothing is sent anywhere.

import { zipSync, strToU8 } from "fflate";

export type ExportRow = { label: string; value: number };

function download(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 2000);
}

export function safeFileName(title: string, extension: string): string {
  const base = title.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 60) || "iip-export";
  return `${base}.${extension}`;
}

/** Draws a chart element (a card holding an SVG) onto a canvas, under its title, and returns it. */
async function renderToCanvas(element: HTMLElement, scale = 2): Promise<HTMLCanvasElement> {
  const rect = element.getBoundingClientRect();
  const width = Math.ceil(rect.width);
  const headerHeight = 64;
  const footerHeight = 28;
  const height = Math.ceil(rect.height) + headerHeight + footerHeight;
  const canvas = document.createElement("canvas");
  canvas.width = width * scale;
  canvas.height = height * scale;
  const context = canvas.getContext("2d");
  if (!context) throw new Error("The picture could not be drawn in this browser.");
  context.scale(scale, scale);
  context.fillStyle = "#ffffff";
  context.fillRect(0, 0, width, height);

  const title = element.dataset.exportTitle ?? "";
  const caption = element.dataset.exportCaption ?? "";
  context.fillStyle = "#0B1640";
  context.font = "600 16px Inter, Arial, sans-serif";
  context.fillText(title, 16, 28);
  if (caption) {
    context.fillStyle = "#64748b";
    context.font = "12px Inter, Arial, sans-serif";
    context.fillText(caption, 16, 48);
  }

  const svg = element.querySelector("svg");
  if (svg) {
    const svgRect = svg.getBoundingClientRect();
    const clone = svg.cloneNode(true) as SVGElement;
    clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
    clone.setAttribute("width", String(svgRect.width));
    clone.setAttribute("height", String(svgRect.height));
    clone.setAttribute("font-family", "Inter, Arial, sans-serif");
    const markup = new XMLSerializer().serializeToString(clone);
    const image = new Image();
    const loaded = new Promise<void>((resolve, reject) => {
      image.onload = () => resolve();
      image.onerror = () => reject(new Error("The chart could not be turned into a picture."));
    });
    image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(markup)}`;
    await loaded;
    context.drawImage(image, svgRect.left - rect.left, headerHeight + (svgRect.top - rect.top), svgRect.width, svgRect.height);
  }

  context.fillStyle = "#94a3b8";
  context.font = "11px Inter, Arial, sans-serif";
  context.fillText("Insurance Intelligence Platform", 16, height - 10);
  return canvas;
}

export async function exportPng(element: HTMLElement, title: string): Promise<void> {
  const canvas = await renderToCanvas(element);
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/png"));
  if (!blob) throw new Error("The picture could not be saved.");
  download(blob, safeFileName(title, "png"));
}

export async function exportPdf(element: HTMLElement, title: string, rows: ExportRow[], unit: string): Promise<void> {
  const { jsPDF } = await import("jspdf");
  const canvas = await renderToCanvas(element);
  const pdf = new jsPDF({ orientation: "landscape", unit: "pt", format: "a4" });
  const pageWidth = pdf.internal.pageSize.getWidth();
  const pageHeight = pdf.internal.pageSize.getHeight();
  const margin = 36;

  pdf.setFont("helvetica", "bold");
  pdf.setFontSize(16);
  pdf.setTextColor(11, 22, 64);
  pdf.text(title, margin, margin + 10);
  pdf.setFont("helvetica", "normal");
  pdf.setFontSize(9);
  pdf.setTextColor(100, 116, 139);
  pdf.text(`Prepared ${new Date().toLocaleString()} from live workspace records`, margin, margin + 26);

  const maxWidth = pageWidth - margin * 2;
  const maxHeight = pageHeight - margin * 2 - 60;
  const ratio = Math.min(maxWidth / canvas.width, maxHeight / canvas.height);
  const imageWidth = canvas.width * ratio;
  const imageHeight = canvas.height * ratio;
  pdf.addImage(canvas.toDataURL("image/png"), "PNG", margin, margin + 40, imageWidth, imageHeight);

  if (rows.length > 0) {
    pdf.addPage();
    pdf.setFont("helvetica", "bold");
    pdf.setFontSize(12);
    pdf.setTextColor(11, 22, 64);
    pdf.text(title, margin, margin + 6);
    pdf.setFontSize(10);
    let y = margin + 30;
    pdf.text("Label", margin, y);
    pdf.text(unit.charAt(0).toUpperCase() + unit.slice(1), margin + 320, y);
    pdf.setFont("helvetica", "normal");
    for (const row of rows) {
      y += 16;
      if (y > pageHeight - margin) {
        pdf.addPage();
        y = margin + 10;
      }
      pdf.text(row.label, margin, y);
      pdf.text(String(row.value), margin + 320, y);
    }
  }

  pdf.save(safeFileName(title, "pdf"));
}

const XML_ESCAPES: Record<string, string> = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&apos;" };
const escapeXml = (value: string) => value.replace(/[&<>"']/g, (c) => XML_ESCAPES[c]);

/** A real .xlsx workbook (a zip of XML parts) with one sheet: label and value columns. */
export function buildXlsx(title: string, rows: ExportRow[], unit: string): Uint8Array {
  const header = ["Label", unit.charAt(0).toUpperCase() + unit.slice(1)];
  const sheetRows = [
    `<row r="1"><c r="A1" t="inlineStr"><is><t>${escapeXml(header[0])}</t></is></c><c r="B1" t="inlineStr"><is><t>${escapeXml(header[1])}</t></is></c></row>`,
    ...rows.map(
      (row, index) =>
        `<row r="${index + 2}"><c r="A${index + 2}" t="inlineStr"><is><t>${escapeXml(row.label)}</t></is></c><c r="B${index + 2}"><v>${Number.isFinite(row.value) ? row.value : 0}</v></c></row>`,
    ),
  ].join("");
  const sheetName = escapeXml(title.replace(/[\\/?*[\]:]/g, " ").slice(0, 31) || "Data");

  return zipSync({
    "[Content_Types].xml": strToU8(
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>',
    ),
    "_rels/.rels": strToU8(
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>',
    ),
    "xl/workbook.xml": strToU8(
      `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="${sheetName}" sheetId="1" r:id="rId1"/></sheets></workbook>`,
    ),
    "xl/_rels/workbook.xml.rels": strToU8(
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>',
    ),
    "xl/worksheets/sheet1.xml": strToU8(
      `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${sheetRows}</sheetData></worksheet>`,
    ),
  });
}

export function exportXlsx(title: string, rows: ExportRow[], unit: string): void {
  const bytes = buildXlsx(title, rows, unit);
  const blob = new Blob([bytes as BlobPart], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
  download(blob, safeFileName(title, "xlsx"));
}

// Reads a customer list in the browser, so nothing leaves the computer until the person presses Import.
// CSV and Excel (.xlsx) are supported. An .xlsx file is a zip of XML parts: the first sheet is read, with
// its text from the shared-strings part.

import { unzipSync, strFromU8 } from "fflate";

export function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  const input = text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
  for (let i = 0; i < input.length; i++) {
    const ch = input[i];
    if (quoted) {
      if (ch === '"' && input[i + 1] === '"') { cell += '"'; i++; }
      else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { row.push(cell); cell = ""; }
    else if (ch === "\n" || ch === "\r") {
      if (ch === "\r" && input[i + 1] === "\n") i++;
      row.push(cell); cell = "";
      if (row.some((value) => value.trim() !== "")) rows.push(row);
      row = [];
    } else cell += ch;
  }
  row.push(cell);
  if (row.some((value) => value.trim() !== "")) rows.push(row);
  return rows;
}

function columnIndex(reference: string): number {
  const letters = reference.replace(/[^A-Z]/gi, "").toUpperCase();
  let index = 0;
  for (const letter of letters) index = index * 26 + (letter.charCodeAt(0) - 64);
  return Math.max(0, index - 1);
}

function textOf(node: Element | null): string {
  if (!node) return "";
  return Array.from(node.getElementsByTagName("t")).map((t) => t.textContent ?? "").join("");
}

export function parseXlsx(buffer: ArrayBuffer): string[][] {
  const files = unzipSync(new Uint8Array(buffer));
  const parser = new DOMParser();
  const read = (name: string) => (files[name] ? parser.parseFromString(strFromU8(files[name]), "application/xml") : null);

  const shared = read("xl/sharedStrings.xml");
  const strings = shared ? Array.from(shared.getElementsByTagName("si")).map((si) => textOf(si)) : [];

  const sheetName = Object.keys(files).filter((name) => /^xl\/worksheets\/sheet\d+\.xml$/.test(name)).sort()[0];
  const sheet = sheetName ? read(sheetName) : null;
  if (!sheet) throw new Error("This Excel file has no sheet to read.");

  const rows: string[][] = [];
  for (const rowNode of Array.from(sheet.getElementsByTagName("row"))) {
    const row: string[] = [];
    for (const cell of Array.from(rowNode.getElementsByTagName("c"))) {
      const at = columnIndex(cell.getAttribute("r") ?? "A1");
      const type = cell.getAttribute("t");
      let value = "";
      if (type === "s") value = strings[Number(cell.getElementsByTagName("v")[0]?.textContent ?? -1)] ?? "";
      else if (type === "inlineStr") value = textOf(cell);
      else value = cell.getElementsByTagName("v")[0]?.textContent ?? "";
      while (row.length < at) row.push("");
      row[at] = value;
    }
    if (row.some((value) => value.trim() !== "")) rows.push(row);
  }
  return rows;
}

export async function readListFile(file: File): Promise<string[][]> {
  if (/\.csv$/i.test(file.name)) return parseCsv(await file.text());
  if (/\.xlsx$/i.test(file.name)) return parseXlsx(await file.arrayBuffer());
  throw new Error("Only CSV and Excel (.xlsx) files can be read. Older .xls files should be saved as .xlsx or CSV first.");
}

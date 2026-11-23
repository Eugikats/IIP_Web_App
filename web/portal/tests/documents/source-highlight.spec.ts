import { expect, test, type Page } from "@playwright/test";
import { jsPDF } from "jspdf";

// A reviewer hovers a value on the page and sees its field, and hovers a field and sees its words on the
// page. The documents API is answered here with a real PDF whose text sits at known positions, so the
// boxes can be checked against the page the browser actually draws.

const CLAIM = "CLM-9001";
const DOCUMENT = "DOC-9001";

// US Letter in inches, as Azure reports a PDF page. Text is drawn at known positions.
const PAGE_W = 8.5;
const PAGE_H = 11;

function buildPdf(): Buffer {
  const pdf = new jsPDF({ unit: "in", format: "letter" });
  pdf.setFontSize(18);
  pdf.text("Police abstract", 1, 1.2);
  pdf.setFontSize(12);
  pdf.text("Date of loss: 12 March 2026", 1, 2.0);
  pdf.text("Vehicle registration: UBF 214K", 1, 3.0);
  pdf.text("Reporting officer: Sgt A. Namutebi", 1, 4.0);
  return Buffer.from(pdf.output("arraybuffer"));
}

// A box in inches turned into the fractions the API returns.
function box(page: number, xIn: number, yIn: number, wIn: number, hIn: number) {
  const x = xIn / PAGE_W, y = yIn / PAGE_H, width = wIn / PAGE_W, height = hIn / PAGE_H;
  return { pageNumber: page, x, y, width, height, polygon: [x, y, x + width, y, x + width, y + height, x, y + height], parts: null };
}

const FIELDS = [
  { name: "field.DateOfLoss", extractedValue: "12 March 2026", confidence: 0.96, box: box(1, 0.98, 1.85, 2.4, 0.2) },
  { name: "field.VehicleRegistration", extractedValue: "UBF 214K", confidence: 0.58, box: box(1, 0.98, 2.85, 2.9, 0.2) },
  { name: "field.ReportingOfficer", extractedValue: "Sgt A. Namutebi", confidence: 0.91, box: box(1, 0.98, 3.85, 2.9, 0.2) },
  { name: "field.CaseReference", extractedValue: "PR/2026/0412", confidence: 0.88, box: null },
];

function documentItem() {
  return {
    documentId: DOCUMENT,
    caseId: CLAIM,
    originalFileName: "police-abstract.pdf",
    contentType: "application/pdf",
    fileSizeBytes: 4096,
    documentType: "PoliceAbstract",
    processingStatus: "Processed",
    confidenceScore: 0.9,
    fields: FIELDS.map((f) => ({
      name: f.name,
      extractedValue: f.extractedValue,
      effectiveValue: f.extractedValue,
      correctedValue: null,
      confidenceScore: f.confidence,
      verificationStatus: "NeedsReview",
      sensitivity: "Normal",
      warnings: [],
      boundingBox: f.box,
    })),
    validationWarnings: [],
    extractionApproved: false,
    uploadedAt: "2026-03-13T08:00:00Z",
    processedAt: "2026-03-13T08:00:09Z",
    correlationId: "corr-1",
    previewUrl: `/api/v1/cases/${CLAIM}/documents/${DOCUMENT}/preview`,
    allowedActions: [{ action: "correctDocumentExtraction", label: "Correct", requiredPermission: "x" }],
    blockedActions: [],
  };
}

async function openReview(page: Page) {
  const pdf = buildPdf();
  await page.route(`**/api/v1/cases/${CLAIM}/documents`, (route) => route.fulfill({ json: [documentItem()] }));
  await page.route(`**/api/v1/cases/${CLAIM}/documents/${DOCUMENT}/preview`, (route) =>
    route.fulfill({ status: 200, contentType: "application/pdf", body: pdf }),
  );
  await page.goto(`/app/claims/${CLAIM}/extraction?documentId=${DOCUMENT}`);
  // Below the wide layout the review and the source are two tabs; open the source.
  if (!isWide(page)) await page.getByRole("tab", { name: "Source document" }).click();
  await expect(page.getByTestId("source-page").first()).toBeVisible({ timeout: 30_000 });
}

// The list and the page sit side by side only at the wide layout (1280 px and up).
const isWide = (page: Page) => (page.viewportSize()?.width ?? 0) >= 1280;

test("the page outlines the located values and says how many were placed", async ({ page }) => {
  await openReview(page);
  await expect(page.getByTestId("source-located")).toHaveText(/3 of 4 values are outlined/);
  await expect(page.locator("svg g[data-field]")).toHaveCount(3);
  // The value the provider gave no position for says so in the list, and is not outlined.
  await expect(page.locator('[data-testid="extracted-field"][data-field="field.CaseReference"]')).toContainText("Not located on the page");
});

test("hovering a field lights its words on the page, and hovering the page lights the field", async ({ page }) => {
  test.skip(!isWide(page), "The list and the page are separate tabs below the wide layout.");
  await openReview(page);
  const row = page.locator('[data-testid="extracted-field"][data-field="field.DateOfLoss"]');
  const outline = page.locator('svg g[data-field="field.DateOfLoss"]');

  await row.hover();
  await expect(outline).toHaveAttribute("data-active", "true");

  await page.locator('[data-testid="extracted-field"][data-field="field.ReportingOfficer"]').hover();
  await expect(outline).not.toHaveAttribute("data-active", "true");

  // From the page back to the list. The shape is a thin outline, so hover its centre.
  const target = page.locator('svg g[data-field="field.ReportingOfficer"] rect, svg g[data-field="field.ReportingOfficer"] polygon').first();
  await target.hover({ force: true });
  await expect(page.locator('[data-testid="extracted-field"][data-field="field.ReportingOfficer"]')).toHaveAttribute("data-active", "true");
});

test("the outline sits on the words it names, in the page the browser drew", async ({ page }) => {
  await openReview(page);
  const pageBox = await page.getByTestId("source-page").first().boundingBox();
  const outline = await page.locator('svg g[data-field="field.DateOfLoss"]').first().boundingBox();
  expect(pageBox && outline).toBeTruthy();
  // 1 inch from the left and 1.85 inches from the top of an 8.5 by 11 inch page.
  // The outline's stroke is a fixed 1 to 2 pixels wide whatever the page size, so on a narrow phone page it is a
  // larger share of the width. Compare in pixels with that stroke as the tolerance.
  const strokeTolerance = 2.5;
  expect(Math.abs(outline!.x - pageBox!.x - (0.98 / PAGE_W) * pageBox!.width)).toBeLessThanOrEqual(strokeTolerance + 0.005 * pageBox!.width);
  expect(Math.abs(outline!.y - pageBox!.y - (1.85 / PAGE_H) * pageBox!.height)).toBeLessThanOrEqual(strokeTolerance + 0.005 * pageBox!.height);
});

test("editing a field keeps its outline lit, and the keyboard reaches the outline", async ({ page }) => {
  test.skip(!isWide(page), "The list and the page are separate tabs below the wide layout.");
  await openReview(page);
  await page.getByRole("textbox", { name: "Vehicle Registration" }).focus();
  await expect(page.locator('svg g[data-field="field.VehicleRegistration"]')).toHaveAttribute("data-active", "true");

  await page.locator('svg g[data-field="field.DateOfLoss"]').focus();
  await expect(page.locator('[data-testid="extracted-field"][data-field="field.DateOfLoss"]')).toHaveAttribute("data-active", "true");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("textbox", { name: "Date Of Loss" })).toBeFocused();
});

test("tapping an outline on a narrow screen opens its field for editing", async ({ page }) => {
  test.skip(isWide(page), "Only the narrow layout switches panels.");
  await openReview(page);
  await page.locator('svg g[data-field="field.DateOfLoss"]').first().click({ force: true });
  await expect(page.getByRole("tab", { name: "Review fields" })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("textbox", { name: "Date Of Loss" })).toBeFocused();
});

test("a document the provider gave no positions for says so and draws no outlines", async ({ page }) => {
  const pdf = buildPdf();
  const item = documentItem();
  item.fields = item.fields.map((f) => ({ ...f, boundingBox: null }));
  await page.route(`**/api/v1/cases/${CLAIM}/documents`, (route) => route.fulfill({ json: [item] }));
  await page.route(`**/api/v1/cases/${CLAIM}/documents/${DOCUMENT}/preview`, (route) => route.fulfill({ status: 200, contentType: "application/pdf", body: pdf }));
  await page.goto(`/app/claims/${CLAIM}/extraction?documentId=${DOCUMENT}`);
  if (!isWide(page)) await page.getByRole("tab", { name: "Source document" }).click();
  await expect(page.getByTestId("source-located")).toHaveText(/gave no page positions/);
  await expect(page.locator("svg g[data-field]")).toHaveCount(0);
});

import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
const headers = { "x-demo-role": "Admin", "X-User-Id": "spec-claim-summary", "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" };
const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;

let claimId = "";

test.beforeAll(async ({ request }) => {
  const response = await request.get(`${API}/api/v1/claims`, { headers });
  expect(response.ok()).toBeTruthy();
  const claims = (await response.json()) as { claimId?: string; caseId?: string; id?: string; status?: string }[];
  expect(claims.length).toBeGreaterThan(0);
  const open = claims.find((claim) => claim.status === "MissingDocuments") ?? claims[0];
  claimId = open.claimId ?? open.caseId ?? open.id ?? "";
  expect(claimId).not.toBe("");
});

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem("iip.active-organisation-id", "tenant-demo");
    window.localStorage.setItem("iip.active-workspace-id", "workspace-operations");
  });
});

test("the claim summary is the first panel under the claim heading and says what is missing", async ({ page }) => {
  await page.goto(`/app/claims/${claimId}`);
  const summary = page.getByText("Claim summary", { exact: true });
  await expect(summary).toBeVisible();

  // The summary sits above the decision card, the policy question panel and the documents.
  const summaryBox = await summary.boundingBox();
  const decisionBox = await page.getByText("Ask a policy question about this claim").boundingBox();
  const documentsBox = await page.getByText("Documents received", { exact: true }).boundingBox();
  expect(summaryBox).not.toBeNull();
  expect(decisionBox).not.toBeNull();
  expect(documentsBox).not.toBeNull();
  expect(summaryBox!.y).toBeLessThan(decisionBox!.y);
  expect(summaryBox!.y).toBeLessThan(documentsBox!.y);

  // It is within the first screen, not below the fold.
  const viewport = page.viewportSize()!;
  expect(summaryBox!.y).toBeLessThan(viewport.height * 0.6);

  // What is missing comes from the backend checklist.
  await expect(page.getByText(/% complete: documents \d+ of \d+/)).toBeVisible();

  // The decision card and the summary agree on how many documents are required and received.
  const fromSummary = (await page.getByText(/% complete: documents \d+ of \d+/).innerText()).match(/documents (\d+) of (\d+)/)!;
  const fromDecision = (await page.getByText(/\d+ of \d+ required documents received/).innerText()).match(/(\d+) of (\d+)/)!;
  expect([fromDecision[1], fromDecision[2]]).toEqual([fromSummary[1], fromSummary[2]]);
  await page.screenshot({ path: `test-results/workspace/claim-summary-${test.info().project.name}.png`, fullPage: false });
});

test("no raw claim id is shown anywhere on the claim screen", async ({ page }) => {
  await page.goto(`/app/claims/${claimId}`);
  await expect(page.getByText("Claim summary", { exact: true })).toBeVisible();
  const text = await page.locator("main").innerText();
  expect(text).not.toMatch(GUID);
});

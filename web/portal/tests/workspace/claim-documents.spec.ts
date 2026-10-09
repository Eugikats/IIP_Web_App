import { expect, test, type APIRequestContext } from "@playwright/test";

const API = "http://localhost:5167";
const HEADERS = {
  "X-Tenant-Id": "tenant-demo",
  "X-Workspace-Id": "workspace-operations",
  "x-demo-role": "Admin",
  "X-User-Id": "user-1",
  "Content-Type": "application/json",
};

type Claim = { caseId: string; customerName: string; policyNumber: string; status: string };
type Customer = { fullName: string; email?: string | null; phone?: string | null };

// A claim in the local demo data whose owner can be reached, and the secure link the sandbox returns for it.
async function linkForADemoClaim(request: APIRequestContext): Promise<{ link: string; claim: Claim }> {
  const claims = (await (await request.get(`${API}/api/v1/cases`, { headers: HEADERS })).json()) as Claim[];
  const customers = (await (await request.get(`${API}/api/v1/customers`, { headers: HEADERS })).json()) as Customer[];
  for (const claim of claims) {
    const owner = customers.find((customer) => customer.fullName === claim.customerName);
    const contact = owner?.phone || owner?.email;
    if (!contact || !claim.policyNumber) continue;
    const issued = await request.post(`${API}/api/v1/client-portal/magic-links`, {
      headers: HEADERS,
      data: { policyNumber: claim.policyNumber, contactValue: contact },
    });
    const body = (await issued.json()) as { sandboxAccessLink?: string | null };
    if (body.sandboxAccessLink) return { link: body.sandboxAccessLink, claim };
  }
  throw new Error("No demo claim with a reachable contact was found.");
}

test("The link names the organisation and opens the claim with the documents it needs", async ({ page, request }) => {
  const { link } = await linkForADemoClaim(request);
  const url = new URL(link);
  expect(url.searchParams.get("t")).toBe("tenant-demo");
  expect(url.searchParams.get("w")).toBe("workspace-operations");

  await page.goto(`${url.pathname}${url.search}`);
  await expect(page.getByText("Documents", { exact: true })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Documents we asked for")).toBeVisible();
  // A customer reads words, not a stored state name.
  await expect(page.getByText(/^(We have your claim|Someone is looking at it|Being reviewed|We need more from you|Ready for our team to review|With our team|With a senior team member|Settled|Closed)$/)).toBeVisible();
  await expect(page.getByText(/MissingDocuments|ReadyForReview|InReview/)).toHaveCount(0);
  await expect(page.getByText("Something else")).toBeVisible();
});

test("Sending a document puts it on the claim, says so, and the list of what is still needed shrinks", async ({ page, request }) => {
  const { link } = await linkForADemoClaim(request);
  const url = new URL(link);

  // A claim with two required documents, answered here as the real API answers it, so the test does not depend on
  // what earlier runs left in the demo data. (The upload path itself is covered by the API tests.)
  const row = (key: string, label: string, description: string, uploaded: boolean) => ({
    documentTypeKey: key, label, description, required: true, uploaded,
    status: uploaded ? "Received" : "Not uploaded", fileName: uploaded ? "police-abstract.png" : null,
    uploadedAt: uploaded ? new Date().toISOString() : null, disagreesWithClassifier: false,
  });
  const claimWith = (abstractSent: boolean) => ({
    caseId: "00000000-0000-0000-0000-000000000001", productLine: "Motor", claimType: "AccidentDamage", status: "MissingDocuments",
    policyNumber: "MOT/2026/00001", incidentDate: "2026-10-01T00:00:00Z", createdAt: "2026-10-02T08:00:00Z", updatedAt: "2026-10-02T08:00:00Z",
    completenessScore: abstractSent ? 50 : 0,
    requiredDocuments: [row("PoliceReport", "Police abstract", "From the police station.", abstractSent), row("DrivingLicence", "Driving permit", "Front and back.", false)],
    timeline: [{ label: "Claim received", complete: true, occurredAt: "2026-10-02T08:00:00Z" }, { label: "Under review", complete: false, occurredAt: null }, { label: "Assessment", complete: false, occurredAt: null }, { label: "Decision", complete: false, occurredAt: null }, { label: "Settlement", complete: false, occurredAt: null }],
    messages: [], nextSteps: ["Upload the remaining required documents."], reviewRequests: [],
  });
  let sent = false;
  await page.route(/\/client-portal\/claims\/[^/]+$/, (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(claimWith(sent)) }));
  await page.route(/\/client-portal\/claims\/[^/]+\/documents$/, (route) => {
    sent = true;
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(claimWith(true)) });
  });

  await page.goto(`${url.pathname}${url.search}`);
  await expect(page.getByText("Documents we asked for")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Accident damage")).toBeVisible();
  await expect(page.getByText("We need more from you")).toBeVisible();
  await expect(page.getByText("Still needed: Police abstract, Driving permit.")).toBeVisible();
  await expect(page.getByText("0 of 2 sent")).toBeVisible();

  const abstractRow = page.locator("li", { hasText: "Police abstract" });
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");
  await abstractRow.locator('input[type="file"]').setInputFiles({ name: "police-abstract.png", mimeType: "image/png", buffer: png });

  await expect(page.getByRole("status").filter({ hasText: "Police abstract sent. Thank you." })).toBeVisible({ timeout: 30_000 });
  await expect(abstractRow.getByText("Received")).toBeVisible();
  await expect(abstractRow.getByRole("button", { name: "Remove" })).toBeVisible();
  await expect(abstractRow.getByText("Replace")).toBeVisible();
  await expect(page.getByText("Still needed: Driving permit.")).toBeVisible();
  await expect(page.getByText("1 of 2 sent")).toBeVisible();
});

test("When the upload cannot be completed the customer is told, in words, without losing the page", async ({ page, request }) => {
  const { link } = await linkForADemoClaim(request);
  const url = new URL(link);
  await page.goto(`${url.pathname}${url.search}`);
  await expect(page.getByText("Documents we asked for")).toBeVisible({ timeout: 30_000 });

  // The offline demo API answers 503 "not configured" for a document, which is exactly the failure to show.
  const firstUpload = page.locator("li", { has: page.locator('input[type="file"]') }).filter({ hasText: "Upload" }).first();
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");
  await firstUpload.locator('input[type="file"]').setInputFiles({ name: "x.png", mimeType: "image/png", buffer: png });

  await expect(page.getByRole("alert").filter({ hasText: /could not upload|try again/i })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Documents we asked for")).toBeVisible();
});

test("The page can be used with the keyboard and on a small phone without sideways scrolling", async ({ page, request }) => {
  const { link } = await linkForADemoClaim(request);
  const url = new URL(link);
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto(`${url.pathname}${url.search}`);
  await expect(page.getByText("Documents we asked for")).toBeVisible({ timeout: 30_000 });

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // The file chooser is a real, focusable control even though it is drawn as a button.
  const input = page.locator('input[type="file"]').first();
  await input.focus();
  await expect(input).toBeFocused();
});

test("An expired or unknown link says so and offers a new one", async ({ page }) => {
  await page.goto("/access/verify?token=not-a-real-token&t=tenant-demo&w=workspace-operations");
  await expect(page.getByRole("heading", { name: /didn.t work/ })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("link", { name: "Request a new link" })).toBeVisible();
});

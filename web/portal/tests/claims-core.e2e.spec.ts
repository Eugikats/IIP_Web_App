import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { createHash, randomBytes } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

// The claims-core end-to-end oracle. It grows with every build-loop item and, when the loop ends,
// walks the whole journey across two tenants (see docs/build-loop/BUILD_PLAN.md). Each describe
// block below belongs to one item, so a failure names the item that broke.

const API = `http://127.0.0.1:${process.env.IIP_E2E_API_PORT ?? 5170}`;
const FIXTURES = join(__dirname, "fixtures", "claims-core");

export function scopeHeaders(tenantId: string, workspaceId = "workspace-main", role = "Admin", userId = "e2e-admin") {
  return {
    "X-Tenant-Id": tenantId,
    "X-Workspace-Id": workspaceId,
    "x-demo-role": role,
    "x-demo-user-id": userId,
  };
}

/** Documents are read in the background after they are sent (item 17). Waits until none of a claim's
 *  documents is still waiting, so a test can look at what the reading produced. */
export async function waitForDocumentsRead(request: APIRequestContext, caseId: string, tenantId = PORTAL_TENANT, workspaceId = PORTAL_WORKSPACE) {
  await expect.poll(async () => {
    const response = await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers: scopeHeaders(tenantId, workspaceId) });
    const documents = (await response.json()) as { processingStatus: string }[];
    return documents.filter((document) => document.processingStatus === "Uploaded").length;
  }, { timeout: 30_000, message: "the documents were not read in time" }).toBe(0);
}

export function newTenant(prefix = "e2e") {
  return `${prefix}-${Math.random().toString(36).slice(2, 10)}`;
}

export function fixture(name: string) {
  return { name, mimeType: mimeFor(name), buffer: readFileSync(join(FIXTURES, name)) };
}

function mimeFor(name: string) {
  if (name.endsWith(".pdf")) return "application/pdf";
  if (name.endsWith(".jpg") || name.endsWith(".jpeg")) return "image/jpeg";
  if (name.endsWith(".png")) return "image/png";
  return "text/plain";
}

export async function createMotorClaim(request: APIRequestContext, tenantId: string, workspaceId = "workspace-main") {
  const response = await request.post(`${API}/api/v1/cases`, {
    headers: scopeHeaders(tenantId, workspaceId),
    data: {
      customerName: "E2E Customer",
      customerEmail: "e2e.customer@example.com",
      customerPhone: "+256700000123",
      policyNumber: `POL-${Math.random().toString(36).slice(2, 8).toUpperCase()}`,
      productLine: "Motor",
      claimType: "Own damage",
      channel: "Phone",
      incidentDate: new Date(Date.now() - 2 * 86_400_000).toISOString().slice(0, 10),
      incidentDescription: "Collision on the highway.",
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return (await response.json()) as { caseId: string };
}


// The portal under test is built for one tenant (NEXT_PUBLIC_TENANT_ID); item 29 removes that
// limit. Until then, browser journeys for a customer run in the seeded demo tenant.
export const PORTAL_TENANT = "tenant-demo";
export const PORTAL_WORKSPACE = "workspace-operations";

/** Creates a claim in the portal's tenant and returns the access link a customer would be sent
 *  (read from the mock outbox, because the Mock communication provider "delivers" it there). */
export async function claimantLinkFor(request: APIRequestContext, claimType = "Accident", productLine = "Motor") {
  const policyNumber = `POL-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;
  const email = `claimant.${Math.random().toString(36).slice(2, 8)}@example.com`;
  const created = await request.post(`${API}/api/v1/cases`, {
    headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE),
    data: {
      customerName: "Portal Customer",
      customerEmail: email,
      customerPhone: "+256700000777",
      policyNumber,
      productLine,
      claimType,
      channel: "Phone",
      incidentDate: new Date(Date.now() - 2 * 86_400_000).toISOString().slice(0, 10),
      incidentDescription: "Collision on the highway.",
    },
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const { caseId } = (await created.json()) as { caseId: string };

  const asked = await request.post(`${API}/api/v1/client-portal/magic-links`, {
    headers: { "X-Tenant-Id": PORTAL_TENANT, "X-Workspace-Id": PORTAL_WORKSPACE },
    data: { policyNumber, contactValue: email },
  });
  expect(asked.ok(), await asked.text()).toBeTruthy();

  let link: string | undefined;
  for (let attempt = 0; attempt < 10 && !link; attempt++) {
    const outbox = (await (await request.get(`${API}/api/v1/dev/mock-outbox`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) })).json()) as { recipient: string; body: string }[];
    const mail = outbox.find((message) => message.recipient === email && /token=/.test(message.body));
    link = mail?.body.match(/https?:\/\/[^\s"]*token=[A-Za-z0-9_-]+/)?.[0];
    if (!link) await new Promise((resolve) => setTimeout(resolve, 300));
  }
  expect(link, "the access link was not delivered to the mock outbox").toBeTruthy();
  // The mock outbox holds the link as the API built it (its configured portal base URL); the
  // browser under test is on a different origin, so keep only the token.
  const token = new URL(link!).searchParams.get("token")!;
  return { caseId, policyNumber, email, token };
}

/** Opens the claimant's page from their access link. By default the claimant also reads and agrees
 *  to the privacy notice, the step every upload now needs (item 9); pass `agree: false` to see the
 *  page as it is before that. */
export async function openClaimantPortal(page: Page, token: string, options: { agree?: boolean } = {}) {
  await page.goto(`/access/verify?token=${token}`);
  await expect(page.getByText("Your claim", { exact: true })).toBeVisible();
  if (options.agree ?? true) {
    const consent = page.getByTestId("claimant-consent");
    await expect(consent).toBeVisible();
    if ((await consent.getAttribute("data-state")) !== "Given") {
      await consent.getByRole("button", { name: "I agree" }).click();
      await expect(page.getByTestId("claimant-consent")).toHaveAttribute("data-state", "Given");
    }
  }
}

test.describe("Item 1: mock providers", () => {
  test("provider status reports Mock honestly, never healthy", async ({ request }) => {
    const response = await request.get(`${API}/api/v1/provider-status`, { headers: scopeHeaders(newTenant()) });
    expect(response.ok()).toBeTruthy();
    const body = (await response.json()) as { overallStatus: string; providers: { name: string; status: string }[] };
    const byName = Object.fromEntries(body.providers.map((provider) => [provider.name, provider.status]));
    expect(byName.azureOpenAI).toBe("mock");
    expect(byName.documentIntelligence).toBe("mock");
    expect(byName.communications).toBe("mock");
    expect(body.providers.map((provider) => provider.status)).not.toContain("healthy");
  });

  test("the portal shows the Mock status, not Operational, for mock providers", async ({ page }) => {
    await page.goto("/app/claims");
    await expect(page.getByText("Provider health", { exact: true })).toBeVisible();
    await expect(page.getByText("Mock", { exact: true }).first()).toBeVisible();
    await expect(page.getByText("Operational", { exact: true })).toHaveCount(0);
  });

  test("an uploaded police letter fixture is classified, processed and extracted by the mock provider", async ({ request }) => {
    const tenant = newTenant();
    const { caseId } = await createMotorClaim(request, tenant);
    const upload = await request.post(`${API}/api/v1/cases/${caseId}/documents`, {
      headers: scopeHeaders(tenant),
      multipart: { files: fixture("police_letter.jpg") },
    });
    expect(upload.ok(), await upload.text()).toBeTruthy();
    // The upload returns as soon as the file is stored; a worker reads it a moment later.
    await waitForDocumentsRead(request, caseId, tenant, "workspace-main");

    const documents = (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers: scopeHeaders(tenant) })).json()) as {
      documentType: string;
      processingStatus: string;
      fields: unknown[];
    }[];
    expect(documents).toHaveLength(1);
    expect(documents[0].documentType).toBe("PoliceAbstract");
    expect(documents[0].processingStatus).toBe("Processed");
    expect(JSON.stringify(documents[0].fields)).toContain("POL/KLA/2026/00471");
  });

  test("the claim summary is a labelled mock output", async ({ request }) => {
    const tenant = newTenant();
    const { caseId } = await createMotorClaim(request, tenant);
    const response = await request.post(`${API}/api/v1/cases/${caseId}/summary`, { headers: scopeHeaders(tenant) });
    expect(response.ok(), await response.text()).toBeTruthy();
    expect(await response.text()).toContain("MOCK AI OUTPUT");
  });

  test("a second tenant cannot read the first tenant's mock outbox or claim", async ({ request }) => {
    const tenantA = newTenant("a");
    const tenantB = newTenant("b");
    const { caseId } = await createMotorClaim(request, tenantA);

    const outboxB = await request.get(`${API}/api/v1/dev/mock-outbox`, { headers: scopeHeaders(tenantB) });
    expect(await outboxB.json()).toEqual([]);
    const claimB = await request.get(`${API}/api/v1/cases/${caseId}`, { headers: scopeHeaders(tenantB) });
    expect(claimB.status()).toBe(404);
  });
});

test.describe("Item 2: known defects", () => {
  test("a file that fails once is read again automatically while the customer still sees Being checked", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const policeRow = page.locator("li", { hasText: "Police abstract" }).first();
    await policeRow.locator('input[data-upload="choose"]').setInputFiles({
      name: "police.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ONCE"),
    });
    const documentProgress = page.getByText("Documents we asked for").locator("..");
    // The first attempt fails; the customer is never shown that, only that it is being checked.
    await expect(policeRow.getByText("Could not be read")).toHaveCount(0);
    await expect(policeRow.getByText("Received")).toBeVisible({ timeout: 30_000 });
    await expect(policeRow.getByRole("button", { name: "Try again" })).toHaveCount(0);
    await expect(documentProgress).toContainText("1 of 3 sent");
  });

  test("a file that keeps failing ends as Could not be read, with a Try again for the customer", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const policeRow = page.locator("li", { hasText: "Police abstract" }).first();
    await policeRow.locator('input[data-upload="choose"]').setInputFiles({
      name: "police.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ALWAYS"),
    });
    await expect(policeRow.getByText("Could not be read")).toBeVisible({ timeout: 30_000 });
    await expect(policeRow.getByRole("button", { name: "Try again" })).toBeVisible();
    await expect(page.getByText("Completeness").locator("..").getByText("0%")).toBeVisible();

    // Trying again queues it again: it goes back to being checked before it fails again.
    await policeRow.getByRole("button", { name: "Try again" }).click();
    await expect(policeRow.getByText("Could not be read")).toBeVisible({ timeout: 30_000 });
  });

  test("a slot the customer filled counts even when the file name tells nothing", async ({ request }) => {
    const tenant = newTenant();
    const { caseId } = await createMotorClaim(request, tenant);
    const upload = await request.post(`${API}/api/v1/cases/${caseId}/documents`, {
      headers: scopeHeaders(tenant),
      multipart: { files: { name: "accident_report.txt", mimeType: "text/plain", buffer: Buffer.from("plain text, no marker") } },
    });
    expect(upload.ok(), await upload.text()).toBeTruthy();
    const documents = (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers: scopeHeaders(tenant) })).json()) as { documentType: string }[];
    // The old substring matcher filed this as a customer ID because "accident" contains "id".
    expect(documents[0].documentType).toBe("Unknown");
  });

  test("customer service cannot manage products; the permission mirror matches the server", async ({ request }) => {
    const denied = await request.post(`${API}/api/v1/products`, {
      headers: scopeHeaders(newTenant(), "workspace-main", "CustomerService"),
      data: { name: "Sneaky", productClass: "Motor" },
    });
    expect(denied.status()).toBe(403);
    const me = await request.get(`${API}/api/v1/me`, { headers: scopeHeaders(newTenant(), "workspace-main", "CustomerService") });
    const permissions = ((await me.json()) as { permissions: string[] }).permissions;
    expect(permissions).not.toContain("ManageProducts");
    expect(permissions).not.toContain("ManageTenantOnboarding");
    expect(permissions).toContain("CreateClaim");
  });
});

test.describe("Item 3: claim types per product", () => {
  test("an admin adds and reorders claim types on a product in the portal and they are saved", async ({ page, request }) => {
    // A draft product on a spare line, so editing it cannot unpublish the seeded Motor product
    // that the other tests in this run rely on.
    const created = await request.post(`${API}/api/v1/products`, {
      headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE),
      data: { name: "Marine Cover", productClass: `Marine${Math.random().toString(36).slice(2, 6)}`, changeReason: "e2e" },
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    const { productId } = (await created.json()) as { productId: string };

    await page.goto(`/app/admin/products/${productId}`);
    const editor = page.getByTestId("claim-types-editor");
    await expect(editor).toBeVisible();
    await expect(editor.getByText("No claim types yet.")).toBeVisible();

    await editor.getByRole("button", { name: "Add claim type" }).click();
    await editor.getByLabel("Claim type 1 name").fill("Cargo loss");
    await editor.getByLabel("Claim type 1 aliases").fill("lost cargo, missing goods");
    await editor.getByRole("button", { name: "Add claim type" }).click();
    await editor.getByLabel("Claim type 2 name").fill("Hull damage");
    await editor.getByRole("button", { name: "Move Hull damage up" }).click();

    await page.getByRole("button", { name: "Save changes" }).click();
    await page.getByLabel("Reason for audit trail").fill("Set up the first claim types");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect.poll(async () => {
      const saved = await request.get(`${API}/api/v1/products/${productId}`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) });
      const body = (await saved.json()) as { claimTypes: { key: string; name: string; aliases: string[] }[] };
      return body.claimTypes.map((type) => type.key);
    }).toEqual(["hull-damage", "cargo-loss"]);
  });

  test("the seeded Motor product offers claim types; staff can file with one and not with a made-up one", async ({ request }) => {
    const list = (await (await request.get(`${API}/api/v1/claim-types?productLine=Motor`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) })).json()) as {
      configured: boolean;
      claimTypes: { name: string }[];
    };
    expect(list.configured).toBe(true);
    expect(list.claimTypes.map((type) => type.name)).toContain("Theft");

    const post = (claimType: string) => request.post(`${API}/api/v1/cases`, {
      headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE),
      data: {
        customerName: "Claim Type Tester", customerEmail: "ct@example.com", customerPhone: "+256700000555",
        policyNumber: `POL-${Math.random().toString(36).slice(2, 8).toUpperCase()}`, productLine: "Motor", claimType,
        channel: "Phone", incidentDate: new Date(Date.now() - 86_400_000).toISOString().slice(0, 10), incidentDescription: "Test",
      },
    });
    const accepted = await post("Theft");
    expect(accepted.status()).toBe(201);
    expect(((await accepted.json()) as { claimTypeKey: string }).claimTypeKey).toBe("theft");
    expect((await post("Made-up type")).status()).toBe(400);
  });

  test("another tenant does not see this insurer's claim types", async ({ request }) => {
    const other = (await (await request.get(`${API}/api/v1/claim-types?productLine=Motor`, { headers: scopeHeaders(newTenant()) })).json()) as { configured: boolean; claimTypes: unknown[] };
    expect(other.configured).toBe(false);
    expect(other.claimTypes).toEqual([]);
  });
});

test.describe("Item 4: documents and evidence per claim type", () => {
  test("an admin scopes a document to a claim type and marks photos as evidence; the API reports it", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const created = await request.post(`${API}/api/v1/products`, {
      headers,
      data: { name: "Cargo Cover", productClass: `Cargo${Math.random().toString(36).slice(2, 6)}`, changeReason: "e2e" },
    });
    const { productId } = (await created.json()) as { productId: string };
    const configured = await request.put(`${API}/api/v1/products/${productId}`, {
      headers,
      data: {
        changeReason: "claim types",
        claimTypes: [{ name: "Cargo loss", active: true }, { name: "Hull damage", active: true }],
      },
    });
    expect(configured.ok(), await configured.text()).toBeTruthy();

    await page.goto(`/app/admin/products/${productId}`);
    await page.getByLabel("Show what a claim of this type is asked for").selectOption("hull-damage");
    await page.getByRole("button", { name: "Add document" }).click();

    const slot = page.getByTestId("document-slot-settings").first();
    await expect(slot).toBeVisible();
    await page.getByLabel("Document key").first().selectOption("PhotoEvidence");
    await page.getByLabel("What the customer sees").first().fill("Photos of the hull");
    await page.getByLabel("Why it is needed").first().fill("Shows the damage");
    await expect(page.getByLabel("Kind of Photos of the hull")).toHaveValue("Document");
    await page.getByLabel("Kind of Photos of the hull").selectOption("Evidence");
    await page.getByLabel("File types for Photos of the hull").fill("jpg, png");
    await page.getByLabel("Most files for Photos of the hull").fill("6");

    await page.getByRole("button", { name: "Save changes" }).click();
    await page.getByLabel("Reason for audit trail").fill("Photos of the hull are evidence");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect.poll(async () => {
      const saved = (await (await request.get(`${API}/api/v1/products/${productId}`, { headers })).json()) as {
        documentMatrix: { documentKey: string; kind: string; claimTypeKeys: string[]; maxFiles: number; acceptedFormats: string[] }[];
      };
      const entry = saved.documentMatrix[0];
      return entry && `${entry.documentKey}|${entry.kind}|${entry.claimTypeKeys.join(",")}|${entry.maxFiles}|${entry.acceptedFormats.join(",")}`;
    }).toBe("PhotoEvidence|Evidence|hull-damage|6|jpg,png");
  });

  test("at 390px the customer sees photos as evidence, adds several, and the count is kept", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await expect(photos.getByText("These photos are kept exactly as you take them.")).toBeVisible();
    const damage = fixture("damage_photo.jpg");
    await photos.locator('input[data-upload="choose"]').setInputFiles([damage, { ...damage, name: "damage_photo_2.jpg" }]);
    await expect(photos.getByText("2 of 10 sent")).toBeVisible();
    await expect(photos.getByRole("button", { name: /Remove/ })).toBeVisible();
    await photos.locator('input[data-upload="choose"]').setInputFiles([{ ...damage, name: "damage_photo_3.jpg" }]);
    await expect(photos.getByText("3 of 10 sent")).toBeVisible();
  });
});

test.describe("Item 5: claim fields per claim type", () => {
  test("an admin builds questions in the portal, including a choice, and they are saved in order", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const created = await request.post(`${API}/api/v1/products`, {
      headers,
      data: { name: "Fleet Cover", productClass: `Fleet${Math.random().toString(36).slice(2, 6)}`, changeReason: "e2e" },
    });
    const { productId } = (await created.json()) as { productId: string };

    await page.goto(`/app/admin/products/${productId}`);
    const editor = page.getByTestId("claim-fields-editor");
    await expect(editor.getByText("No questions yet.")).toBeVisible();

    await editor.getByRole("button", { name: "Add question" }).click();
    await editor.getByLabel("Question 1 label").fill("Police report number");
    await editor.getByRole("button", { name: "Add question" }).click();
    await editor.getByLabel("Question 2 label").fill("Was the vehicle driveable?");
    await editor.getByLabel("Question 2 type").selectOption("YesNo");
    await editor.getByRole("button", { name: "Add question" }).click();
    await editor.getByLabel("Question 3 label").fill("Damage level");
    await editor.getByLabel("Question 3 type").selectOption("Select");
    await editor.getByLabel("Question 3 choices").fill("Minor, Major, Total loss");
    await editor.getByRole("button", { name: "Move Damage level up" }).click();

    await page.getByRole("button", { name: "Save changes" }).click();
    await page.getByLabel("Reason for audit trail").fill("Set up the fleet questions");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect.poll(async () => {
      const saved = (await (await request.get(`${API}/api/v1/products/${productId}`, { headers })).json()) as { claimFields: { key: string; type: string; options: string[] }[] };
      return saved.claimFields.map((field) => `${field.key}:${field.type}`).join(",");
    }).toBe("policeReportNumber:Text,damageLevel:Select,wasTheVehicleDriveable:YesNo");
  });

  test("staff see a claim's answers on its page and can correct one", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const created = await request.post(`${API}/api/v1/cases`, {
      headers,
      data: {
        customerName: "Fields Tester", customerEmail: "ft@example.com", customerPhone: "+256700000444",
        policyNumber: `POL-${Math.random().toString(36).slice(2, 8).toUpperCase()}`, productLine: "Motor", claimType: "Accident",
        channel: "Phone", incidentDate: new Date(Date.now() - 86_400_000).toISOString().slice(0, 10), incidentDescription: "Fields test",
        fieldValues: { vehicleRegistration: "UAX 123A", accidentLocation: "Jinja Road" },
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    const { caseId } = (await created.json()) as { caseId: string };

    await page.goto(`/app/claims/${caseId}`);
    const card = page.getByTestId("claim-details-card");
    await expect(card.getByText("UAX 123A")).toBeVisible();
    await expect(card.getByText("Jinja Road")).toBeVisible();

    await card.getByRole("button", { name: "Edit details" }).click();
    await card.getByLabel("Where it happened").fill("Northern Bypass");
    await card.getByRole("button", { name: "Save details" }).click();
    await expect(card.getByText("Northern Bypass")).toBeVisible();

    const stored = (await (await request.get(`${API}/api/v1/cases/${caseId}`, { headers })).json()) as { fieldValues: Record<string, string> };
    expect(stored.fieldValues.accidentLocation).toBe("Northern Bypass");
    expect(stored.fieldValues.vehicleRegistration).toBe("UAX 123A");
  });

  test("another tenant cannot read a claim's answers", async ({ request }) => {
    const tenant = newTenant();
    const headers = scopeHeaders(tenant);
    const created = await request.post(`${API}/api/v1/cases`, {
      headers,
      data: {
        customerName: "Iso Tester", customerEmail: "iso@example.com", customerPhone: "+256700000333", policyNumber: "POL-ISO1",
        productLine: "Motor", claimType: "Own damage", channel: "Phone",
        incidentDate: new Date(Date.now() - 86_400_000).toISOString().slice(0, 10), incidentDescription: "Iso",
        fieldValues: { vehicleRegistration: "UBB 777B" },
      },
    });
    const { caseId } = (await created.json()) as { caseId: string };

    const other = await request.get(`${API}/api/v1/cases/${caseId}/fields`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 6: product settings and labels", () => {
  test("operating settings fit mobile, tablet and desktop viewports", async ({ page }) => {
    for (const width of [390, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto("/app/admin/settings");
      await expect(page.getByTestId("operating-settings")).toBeVisible();
      const overflows = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
      expect(overflows, `horizontal overflow at ${width}px`).toBe(false);
    }
  });

  test("an admin changes the reminder days and the IRA rule in the portal and the API follows", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const before = (await (await request.get(`${API}/api/v1/admin/tenant-settings`, { headers })).json()) as { requireIraApprovalForPublish: boolean; missingDocumentReminderDays: number };
    try {
      await page.goto("/app/admin/settings");
      const form = page.getByTestId("operating-settings");
      await expect(form).toBeVisible();
      const days = form.getByLabel(/Remind the customer about missing documents/);
      await days.fill("9");
      await form.getByLabel(/Require an IRA approval reference/).setChecked(!before.requireIraApprovalForPublish);
      await page.getByRole("button", { name: "Save settings" }).click();
      await expect(page.getByText("Settings saved.")).toBeVisible();

      const saved = (await (await request.get(`${API}/api/v1/admin/tenant-settings`, { headers })).json()) as { requireIraApprovalForPublish: boolean; missingDocumentReminderDays: number };
      expect(saved.missingDocumentReminderDays).toBe(9);
      expect(saved.requireIraApprovalForPublish).toBe(!before.requireIraApprovalForPublish);

      await page.reload();
      await expect(page.getByTestId("operating-settings").getByLabel(/Remind the customer about missing documents/)).toHaveValue("9");
    } finally {
      await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { requireIraApprovalForPublish: before.requireIraApprovalForPublish, missingDocumentReminderDays: before.missingDocumentReminderDays } });
    }
  });

  test("an out-of-range value is refused with a reason and nothing changes", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    await page.goto("/app/admin/settings");
    const form = page.getByTestId("operating-settings");
    await form.getByLabel(/Remind the customer about missing documents/).fill("99");
    await page.getByRole("button", { name: "Save settings" }).click();
    await expect(page.getByText(/must be between 1 and 30 days/)).toBeVisible();
    const stored = (await (await request.get(`${API}/api/v1/admin/tenant-settings`, { headers })).json()) as { missingDocumentReminderDays: number };
    expect(stored.missingDocumentReminderDays).not.toBe(99);
  });

  test("turning the IRA rule off lets a product be submitted without an IRA reference, and another tenant is unaffected", async ({ request }) => {
    const tenant = newTenant();
    const headers = scopeHeaders(tenant);
    const other = scopeHeaders(newTenant());
    const created = await request.post(`${API}/api/v1/products`, { headers, data: { name: "Plain Cover", productClass: "General", changeReason: "e2e" } });
    const { productId } = (await created.json()) as { productId: string };
    const refused = await request.post(`${API}/api/v1/products/${productId}/submit`, { headers, data: { changeReason: "go" } });
    expect(refused.status()).toBe(400);

    await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { requireIraApprovalForPublish: false } });
    const accepted = await request.post(`${API}/api/v1/products/${productId}/submit`, { headers, data: { changeReason: "go" } });
    expect(accepted.status(), await accepted.text()).toBe(200);

    const untouched = (await (await request.get(`${API}/api/v1/admin/tenant-settings`, { headers: other })).json()) as { requireIraApprovalForPublish: boolean };
    expect(untouched.requireIraApprovalForPublish).toBe(true);
  });
});

async function uploadWording(request: APIRequestContext, headers: Record<string, string>, documentName: string) {
  const uploaded = await request.post(`${API}/api/v1/knowledge-documents`, {
    headers,
    multipart: {
      file: fixture("sample_policy_wording.txt"),
      documentName,
      productLine: "Motor",
      version: "1.0",
      documentType: "PolicyWording",
    },
  });
  expect(uploaded.status(), await uploaded.text()).toBe(201);
}

test.describe("Item 7: policy wording to drafted configuration", () => {
  test("an admin reads a wording, decides each proposal and adds only the accepted ones", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const wordingName = `Motor wording ${Math.random().toString(36).slice(2, 8)}`;
    await uploadWording(request, headers, wordingName);
    const created = await request.post(`${API}/api/v1/products`, {
      headers,
      data: { name: "Wording Cover", productClass: `Wording${Math.random().toString(36).slice(2, 6)}`, changeReason: "e2e" },
    });
    const { productId } = (await created.json()) as { productId: string };

    await page.goto(`/app/admin/products/${productId}`);
    const panel = page.getByTestId("wording-draft-panel");
    for (const width of [390, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(panel).toBeVisible();
      const overflows = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
      expect(overflows, `horizontal overflow at ${width}px`).toBe(false);
    }
    await panel.getByLabel("Wording to read").selectOption({ label: `${wordingName} (version 1.0)` });
    await panel.getByRole("button", { name: "Read the wording" }).click();

    const review = page.getByTestId("wording-draft-review");
    await expect(review.getByText("What is covered")).toBeVisible();
    await expect(review.getByText("Found in the wording: clause 1.3, page 1").first()).toBeVisible();
    const sourceHref = await review.getByRole("link", { name: "Open source wording in the Data Hub" }).first().getAttribute("href");
    const sourcePage = await page.context().newPage();
    await sourcePage.goto(new URL(sourceHref!, page.url()).toString());
    await expect(sourcePage.getByRole("button", { name: wordingName })).toHaveAttribute("aria-expanded", "true");
    await expect(sourcePage.getByTestId("knowledge-chunk-preview")).toBeVisible();
    await sourcePage.close();

    // Nothing is part of the product yet.
    const before = (await (await request.get(`${API}/api/v1/products/${productId}`, { headers })).json()) as { version: number; coverageItems: unknown[] };
    expect(before.coverageItems).toHaveLength(0);

    await review.getByRole("button", { name: "Accept Accident", exact: true }).click();
    await review.getByRole("button", { name: "Accept Police report" }).first().click();
    await review.getByRole("button", { name: "Accept Fire", exact: true }).click();
    await review.getByRole("region", { name: "What is covered" }).getByRole("button", { name: "Reject Windscreen", exact: true }).click();

    // A document with no matching document type cannot be accepted until one is chosen.
    const logBook = review.getByTestId("wording-item").filter({ hasText: "Log book" }).first();
    await expect(logBook.getByRole("button", { name: /^Accept / })).toBeDisabled();

    await panel.getByRole("button", { name: /Add 3 accepted items to the product/ }).click();
    await expect(page.getByTestId("wording-apply-result")).toContainText("1 kinds of claim, 1 documents, 0 questions, 1 covered or excluded items");

    await expect(page.getByTestId("coverage-items").getByText("Fire")).toBeVisible();
    const after = (await (await request.get(`${API}/api/v1/products/${productId}`, { headers })).json()) as {
      version: number; status: string; claimTypes: { key: string }[]; coverageItems: { title: string; clause: string }[];
      documentMatrix: { documentKey: string; claimTypeKeys: string[] }[];
    };
    expect(after.version).toBeGreaterThan(before.version);
    expect(after.status).toBe("Draft");
    expect(after.claimTypes.map((type) => type.key)).toEqual(["accident"]);
    expect(after.coverageItems.map((item) => item.title)).toEqual(["Fire"]);
    expect(after.documentMatrix.find((entry) => entry.documentKey === "PoliceAbstract")?.claimTypeKeys).toEqual(["accident"]);
  });

  test("another tenant cannot read or apply a reading", async ({ request }) => {
    const tenant = newTenant();
    const headers = scopeHeaders(tenant);
    await uploadWording(request, headers, "Isolated wording");
    const created = await request.post(`${API}/api/v1/products`, { headers, data: { name: "Iso", productClass: "Motor", changeReason: "e2e" } });
    const { productId } = (await created.json()) as { productId: string };
    const drafted = await request.post(`${API}/api/v1/products/${productId}/wording-drafts`, { headers, data: {} });
    expect(drafted.status(), await drafted.text()).toBe(400);

    const wordings = (await (await request.get(`${API}/api/v1/knowledge-documents`, { headers })).json()) as { knowledgeDocumentId: string }[];
    const ok = await request.post(`${API}/api/v1/products/${productId}/wording-drafts`, { headers, data: { knowledgeDocumentId: wordings[0].knowledgeDocumentId } });
    expect(ok.status(), await ok.text()).toBe(201);
    const { draftId } = (await ok.json()) as { draftId: string };

    const other = scopeHeaders(newTenant());
    expect((await request.get(`${API}/api/v1/products/${productId}/wording-drafts/${draftId}`, { headers: other })).status()).toBe(404);
    expect((await request.post(`${API}/api/v1/products/${productId}/wording-drafts/${draftId}/apply`, { headers: other, data: {} })).status()).toBe(404);
  });
});

test.describe("Item 8: preview as customer", () => {
  async function productWithClaimTypes(request: APIRequestContext) {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const created = await request.post(`${API}/api/v1/products`, {
      headers,
      data: { name: "Preview Cover", productClass: `Preview${Math.random().toString(36).slice(2, 6)}`, changeReason: "e2e" },
    });
    const { productId } = (await created.json()) as { productId: string };
    const stage = "acknowledgement_and_document_request";
    const updated = await request.put(`${API}/api/v1/products/${productId}`, {
      headers,
      data: {
        claimTypes: [
          { name: "Accident", description: "Collision", active: true, sortOrder: 1 },
          { name: "Theft", description: "Stolen", active: true, sortOrder: 2 },
        ],
        documentMatrix: [
          { documentKey: "ClaimForm", customerLabel: "Signed claim form", reason: "needed", isRequired: true, requiredAtStageKey: stage, isRegulatoryOrWordingMandated: false },
          { documentKey: "PoliceAbstract", customerLabel: "Police report", reason: "needed", isRequired: true, requiredAtStageKey: stage, isRegulatoryOrWordingMandated: false, claimTypeKeys: ["theft"] },
          { documentKey: "PhotoEvidence", customerLabel: "Photos of the damage", reason: "needed", isRequired: true, requiredAtStageKey: stage, isRegulatoryOrWordingMandated: false, claimTypeKeys: ["accident"], kind: "Evidence" },
        ],
        claimFields: [
          { label: "Where did it happen", type: "Text", required: true, sortOrder: 1, customerVisible: true, claimTypeKeys: [] },
          { label: "Was the car driveable", type: "YesNo", required: false, sortOrder: 2, customerVisible: true, claimTypeKeys: ["accident"] },
        ],
        changeReason: "e2e",
      },
    });
    expect(updated.ok(), await updated.text()).toBeTruthy();
    return { productId, headers };
  }

  test("an admin previews a draft product as a customer for each claim type and nothing is saved", async ({ page, request }) => {
    const { productId, headers } = await productWithClaimTypes(request);
    const casesBefore = ((await (await request.get(`${API}/api/v1/cases`, { headers })).json()) as unknown[]).length;

    await page.goto(`/app/admin/products/${productId}`);
    await page.getByRole("link", { name: "Preview as customer" }).click();

    const preview = page.getByTestId("customer-preview");
    await expect(preview.getByText("Draft, customers do not see this yet")).toBeVisible();
    const frame = preview.getByTestId("customer-preview-frame");
    await expect(frame).toHaveAttribute("data-width", "phone");
    const box = await frame.boundingBox();
    expect(box!.width).toBeLessThanOrEqual(390);

    // Step 1: the questions this claim type asks, and a submit that cannot be pressed.
    await expect(frame.getByText("Where did it happen")).toBeVisible();
    await expect(frame.getByText("Was the car driveable")).toBeVisible();
    await expect(frame.getByRole("button", { name: /Submit claim/ })).toBeDisabled();

    // Step 2: the documents for Accident, then for Theft.
    await preview.getByRole("button", { name: "2. Send documents" }).click();
    await expect(frame.getByText("How we use your information")).toBeVisible();
    await expect(frame.getByText("Preview only. Nothing is recorded.")).toBeVisible();
    await expect(frame.getByText("Photos of the damage")).toBeVisible();
    await expect(frame.getByText("These photos are kept exactly as you take them.")).toBeVisible();
    await expect(frame.getByText("Police report")).toHaveCount(0);
    await expect(frame.getByText("Preview only. Nothing can be sent from here.")).toBeVisible();

    await preview.getByLabel("Claim type").selectOption("Theft");
    await expect(frame.getByText("Police report")).toBeVisible();
    await expect(frame.getByText("Photos of the damage")).toHaveCount(0);

    await preview.getByRole("button", { name: "1. Start the claim" }).click();
    await expect(frame.getByText("Was the car driveable")).toHaveCount(0);

    await preview.getByRole("button", { name: "Wide" }).click();
    await expect(frame).toHaveAttribute("data-width", "wide");

    const casesAfter = ((await (await request.get(`${API}/api/v1/cases`, { headers })).json()) as unknown[]).length;
    expect(casesAfter).toBe(casesBefore);
  });

  test("the preview has no horizontal scroll at 390px", async ({ page, request }) => {
    const { productId } = await productWithClaimTypes(request);
    await page.setViewportSize({ width: 390, height: 844 });

    await page.goto(`/app/admin/products/${productId}/preview`);
    await expect(page.getByTestId("customer-preview-frame")).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test("another tenant cannot preview the product", async ({ request }) => {
    const { productId } = await productWithClaimTypes(request);
    const other = await request.get(`${API}/api/v1/products/${productId}/customer-preview`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 9: privacy notice and consent", () => {
  async function consentState(request: APIRequestContext, caseId: string) {
    const response = await request.get(`${API}/api/v1/cases/${caseId}/privacy-consent`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) });
    expect(response.ok(), await response.text()).toBeTruthy();
    return ((await response.json()) as { state: string; consentedVersion: string | null }).state;
  }

  test("a customer reads the notice and agrees before the first upload", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token, { agree: false });

    const consent = page.getByTestId("claimant-consent");
    await expect(consent).toHaveAttribute("data-state", "NotGiven");
    for (const heading of ["What we collect.", "Why.", "Who sees it.", "How long we keep it.", "Your choices."]) {
      await expect(consent.getByText(heading)).toBeVisible();
    }

    const policeRow = page.locator("li", { hasText: "Police abstract" }).first();
    await expect(policeRow.locator('input[data-upload="choose"]')).toBeDisabled();
    await expect(page.getByText("Read and agree to the privacy notice above to send documents.")).toBeVisible();
    expect(await consentState(request, caseId)).toBe("NotGiven");

    await consent.getByRole("button", { name: "I agree" }).click();
    await expect(consent).toHaveAttribute("data-state", "Given");
    expect(await consentState(request, caseId)).toBe("Given");

    await expect(policeRow.locator('input[data-upload="choose"]')).toBeEnabled();
    await policeRow.locator('input[data-upload="choose"]').setInputFiles({ name: "police.pdf", mimeType: "application/pdf", buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract") });
    await expect(policeRow.getByText("Received")).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test("a customer withdraws from the claim page and can no longer send documents until they agree again", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const consent = page.getByTestId("claimant-consent");
    await consent.getByRole("button", { name: "Withdraw permission" }).click();
    await expect(consent.getByText("What you already sent stays with your claim.")).toBeVisible();
    await consent.getByRole("button", { name: "Withdraw my permission" }).click();

    await expect(consent).toHaveAttribute("data-state", "Withdrawn");
    await expect(consent.getByText("You withdrew your permission.")).toBeVisible();
    const policeRow = page.locator("li", { hasText: "Police abstract" }).first();
    await expect(policeRow.locator('input[data-upload="choose"]')).toBeDisabled();
    expect(await consentState(request, caseId)).toBe("Withdrawn");

    await consent.getByRole("button", { name: "I agree" }).click();
    await expect(consent).toHaveAttribute("data-state", "Given");
    await expect(policeRow.locator('input[data-upload="choose"]')).toBeEnabled();
  });

  test("changing the notice asks customers who agreed to the old one to agree again", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    try {
      const changed = await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { privacyNoticeText: "What we collect: only what we need.\n\nWhy: to handle your claim." } });
      expect(changed.ok(), await changed.text()).toBeTruthy();

      await page.reload();
      const consent = page.getByTestId("claimant-consent");
      await expect(consent).toHaveAttribute("data-state", "NoticeChanged");
      await expect(consent.getByText("Our privacy notice has changed since you last agreed.")).toBeVisible();
      await expect(consent.getByText("only what we need.")).toBeVisible();
      expect(await consentState(request, caseId)).toBe("NoticeChanged");
      await expect(page.locator("li", { hasText: "Police abstract" }).first().locator('input[data-upload="choose"]')).toBeDisabled();

      await consent.getByRole("button", { name: "I agree" }).click();
      await expect(consent).toHaveAttribute("data-state", "Given");
    } finally {
      await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { privacyNoticeText: "" } });
    }
  });

  test("staff see the consent on the claim and an admin writes the notice in settings", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const { caseId } = await claimantLinkFor(request);

    await page.goto(`/app/claims/${caseId}`);
    const line = page.getByTestId("privacy-consent-line");
    await expect(line.getByText("Not yet agreed")).toBeVisible();

    try {
      await page.goto("/app/admin/settings");
      const box = page.getByLabel(/Notice shown to customers/);
      await expect(box).toHaveValue(/What we collect/);
      await box.fill("What we collect: your claim details.\n\nHow long we keep it: seven years.");
      await page.getByRole("button", { name: "Save settings" }).click();
      await expect(page.getByText("Settings saved.")).toBeVisible();
      const saved = (await (await request.get(`${API}/api/v1/admin/tenant-settings`, { headers })).json()) as { privacyNoticeText: string | null; privacyNoticeVersion: number };
      expect(saved.privacyNoticeText).toContain("seven years");

      await page.getByRole("button", { name: "Use the default text" }).click();
      await page.getByRole("button", { name: "Save settings" }).click();
      await expect(page.getByText("Settings saved.")).toBeVisible();
      const reset = (await (await request.get(`${API}/api/v1/admin/tenant-settings`, { headers })).json()) as { privacyNoticeText: string | null; privacyNoticeVersion: number };
      expect(reset.privacyNoticeText).toBeNull();
      expect(reset.privacyNoticeVersion).toBeGreaterThan(saved.privacyNoticeVersion);
    } finally {
      await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { privacyNoticeText: "" } });
    }
  });

  test("another tenant cannot read a consent", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const other = await request.get(`${API}/api/v1/cases/${caseId}/privacy-consent`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 10: photo or file per slot", () => {
  test("each slot offers Take photo and Choose file, and the camera button asks for the rear camera", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const policeRow = page.locator("li", { hasText: "Police abstract" }).first();
    await expect(policeRow.getByText("Take photo")).toBeVisible();
    await expect(policeRow.getByText("Choose file")).toBeVisible();
    const camera = policeRow.locator('input[data-upload="capture"]');
    await expect(camera).toHaveAttribute("capture", "environment");
    await expect(camera).toHaveAttribute("accept", "image/*");
    await expect(camera).not.toHaveAttribute("multiple", /.*/);

    // A single-file slot is filled by a photo taken on the spot.
    await camera.setInputFiles(fixture("damage_photo.jpg"));
    await expect(policeRow.getByText("Received")).toBeVisible();
    await expect(policeRow.getByText("Replace file")).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test("a multi-file slot is filled by taking several photos, and choosing several files at once is also offered", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const photos = page.locator("li", { hasText: "Photo" }).filter({ hasText: "kept exactly as you take them" }).first();
    await expect(photos.locator('input[data-upload="choose"]')).toHaveAttribute("multiple", /.*/);
    await expect(photos.locator('input[data-upload="capture"]')).not.toHaveAttribute("multiple", /.*/);

    const shot = fixture("damage_photo.jpg");
    await photos.locator('input[data-upload="capture"]').setInputFiles({ ...shot, name: "camera_1.jpg" });
    await expect(photos.getByText(/1 of \d+ sent/)).toBeVisible();
    await photos.locator('input[data-upload="capture"]').setInputFiles({ ...shot, name: "camera_2.jpg" });
    await expect(photos.getByText(/2 of \d+ sent/)).toBeVisible();
    await expect(photos.getByText("Add more files")).toBeVisible();
  });

  test("a slot that takes only PDFs offers no camera", async ({ page, request }) => {
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const created = await request.post(`${API}/api/v1/products`, {
      headers,
      data: { name: "Pdf Cover", productClass: `Pdf${Math.random().toString(36).slice(2, 6)}`, changeReason: "e2e" },
    });
    const { productId } = (await created.json()) as { productId: string };
    const updated = await request.put(`${API}/api/v1/products/${productId}`, {
      headers,
      data: {
        claimTypes: [{ name: "Loss", description: "Any loss", active: true, sortOrder: 1 }],
        documentMatrix: [
          { documentKey: "Invoice", customerLabel: "Signed invoice", reason: "needed", isRequired: true, requiredAtStageKey: "acknowledgement_and_document_request", isRegulatoryOrWordingMandated: false, acceptedFormats: ["pdf"] },
          { documentKey: "PoliceAbstract", customerLabel: "Police report", reason: "needed", isRequired: true, requiredAtStageKey: "acknowledgement_and_document_request", isRegulatoryOrWordingMandated: false },
        ],
        changeReason: "e2e",
      },
    });
    expect(updated.ok(), await updated.text()).toBeTruthy();

    await page.goto(`/app/admin/products/${productId}/preview`);
    await page.getByRole("button", { name: "2. Send documents" }).click();
    const invoice = page.locator("li", { hasText: "Signed invoice" });
    await expect(invoice.getByText("Choose file")).toBeVisible();
    await expect(invoice.getByText("Take photo")).toHaveCount(0);
    await expect(page.locator("li", { hasText: "Police report" }).getByText("Take photo")).toBeVisible();
  });
});

test.describe("Item 11: evidence originals", () => {
  async function staffDocuments(request: APIRequestContext, caseId: string) {
    const response = await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) });
    expect(response.ok(), await response.text()).toBeTruthy();
    return (await response.json()) as { documentId: string; sha256: string | null; slotKind: string | null; imagePreviewUrl: string | null; originalFileName: string }[];
  }

  test("a photo sent as evidence reaches the server byte for byte, with its hash and a separate preview", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const photo = fixture("damage_photo.jpg");

    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await photos.locator('input[data-upload="choose"]').setInputFiles(photo);
    await expect(photos.getByText("1 of 10 sent")).toBeVisible();

    const [document] = await staffDocuments(request, caseId);
    const { createHash } = await import("node:crypto");
    // The hash the server computed over what it received equals the hash of the file chosen: the
    // browser sent it exactly as chosen, nothing recompressed or stripped.
    expect(document.sha256).toBe(createHash("sha256").update(photo.buffer).digest("hex"));
    expect(document.slotKind).toBe("Evidence");
    expect(document.imagePreviewUrl).toBeTruthy();

    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const original = await request.get(`${API}/api/v1/cases/${caseId}/documents/${document.documentId}/content`, { headers });
    expect(Buffer.compare(await original.body(), photo.buffer)).toBe(0);
    const preview = await request.get(`${API}${document.imagePreviewUrl}`, { headers });
    expect(preview.status()).toBe(200);
    expect(preview.headers()["content-type"]).toContain("image/jpeg");
  });

  test("a HEIC photo is accepted as evidence and stays HEIC, with a JPEG preview beside it", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const heic = fixture("damage_photo.heic");

    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await photos.locator('input[data-upload="capture"]').setInputFiles({ name: "IMG_0042.HEIC", mimeType: "image/heic", buffer: heic.buffer });
    await expect(photos.getByText("1 of 10 sent")).toBeVisible();

    const [document] = await staffDocuments(request, caseId);
    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const original = await request.get(`${API}/api/v1/cases/${caseId}/documents/${document.documentId}/content`, { headers });
    expect(original.status()).toBeLessThan(500);
    const preview = await request.get(`${API}${document.imagePreviewUrl}`, { headers });
    expect(preview.status()).toBe(200);
    expect(preview.headers()["content-type"]).toContain("image/jpeg");
    expect(document.originalFileName).toBe("IMG_0042.HEIC");
  });

  test("another tenant cannot read the preview", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await photos.locator('input[data-upload="choose"]').setInputFiles(fixture("damage_photo.jpg"));
    await expect(photos.getByText("1 of 10 sent")).toBeVisible();
    const [document] = await staffDocuments(request, caseId);

    const other = await request.get(`${API}${document.imagePreviewUrl}`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 12: document photos to one PDF", () => {
  /** Publishes a product whose police report is a multi-page document, in the portal's workspace. */
  async function multiPageProduct(request: APIRequestContext) {
    const productClass = `Pages${Math.random().toString(36).slice(2, 7)}`;
    const maker = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "Admin", "pages-maker");
    const checker = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "Admin", "pages-checker");
    const created = await request.post(`${API}/api/v1/products`, { headers: maker, data: { name: "Pages Cover", productClass, changeReason: "e2e" } });
    const { productId } = (await created.json()) as { productId: string };
    const updated = await request.put(`${API}/api/v1/products/${productId}`, {
      headers: maker,
      data: {
        iraApprovalReference: "E2E-1",
        iraApprovalEffectiveDate: "2026-01-01",
        documentMatrix: [{
          documentKey: "PoliceAbstract", customerLabel: "Police report", reason: "needed", isRequired: true,
          requiredAtStageKey: "acknowledgement_and_document_request", isRegulatoryOrWordingMandated: false,
          allowsMultiplePages: true, maxFiles: 5,
        }],
        changeReason: "e2e",
      },
    });
    expect(updated.ok(), await updated.text()).toBeTruthy();
    expect((await request.post(`${API}/api/v1/products/${productId}/submit`, { headers: maker, data: { changeReason: "go" } })).ok()).toBeTruthy();
    expect((await request.post(`${API}/api/v1/products/${productId}/approve`, { headers: checker, data: { changeReason: "ok" } })).ok()).toBeTruthy();
    return productClass;
  }

  test("a customer adds pages, reorders and removes one, and staff get one PDF in that order", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const productClass = await multiPageProduct(request);
    const { token, caseId } = await claimantLinkFor(request, "Accident", productClass);
    await openClaimantPortal(page, token);

    const police = page.locator("li", { hasText: "Police report" }).first();
    await police.locator('input[data-upload="choose"]').setInputFiles(fixture("police_letter.jpg"));
    await expect(police.getByText("Page 1: police_letter.jpg")).toBeVisible();
    await police.getByText("Add another page").click({ trial: true });
    await police.locator('input[data-upload="capture"]').setInputFiles({ ...fixture("damage_photo.jpg"), name: "second_page.jpg" });
    await expect(police.getByText("Page 2: second_page.jpg")).toBeVisible();
    await expect(police.getByText("These pages are sent as one PDF of 2 pages, in this order.")).toBeVisible();

    await police.getByRole("button", { name: "Move page 2 of Police report earlier" }).click();
    await expect(police.getByText("Page 1: second_page.jpg")).toBeVisible();
    await expect(police.getByText("Page 2: police_letter.jpg")).toBeVisible();

    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    const documents = (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers })).json()) as { slotPdfUrl: string | null; slotPdfPageCount: number }[];
    expect(documents.every((document) => document.slotPdfPageCount === 2)).toBe(true);
    const pdf = await request.get(`${API}${documents[0].slotPdfUrl}`, { headers });
    expect(pdf.status()).toBe(200);
    expect(pdf.headers()["content-type"]).toContain("application/pdf");
    expect((await pdf.body()).subarray(0, 5).toString()).toBe("%PDF-");

    await police.getByRole("button", { name: "Remove page 1 of Police report" }).click();
    await expect(police.getByText("Page 1: police_letter.jpg")).toBeVisible();
    await expect(police.getByText("Page 2:")).toHaveCount(0);
    await expect(police.getByText("These pages are sent as one PDF of 1 page, in this order.")).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test("another tenant cannot read the PDF", async ({ request }) => {
    const other = await request.get(`${API}/api/v1/cases/00000000-0000-0000-0000-000000000001/document-slots/PoliceAbstract/pdf`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 13: camera document scanner", () => {
  /** A stand-in camera: a canvas showing a page photographed at an angle on a dark table. */
  async function installFakeCamera(page: Page) {
    await page.addInitScript(() => {
      navigator.mediaDevices.getUserMedia = async () => {
        const canvas = document.createElement("canvas");
        canvas.width = 640;
        canvas.height = 480;
        const context = canvas.getContext("2d")!;
        const draw = () => {
          context.fillStyle = "#3a3a3a";
          context.fillRect(0, 0, 640, 480);
          context.fillStyle = "#f0f0f0";
          context.beginPath();
          context.moveTo(150, 70);
          context.lineTo(520, 100);
          context.lineTo(560, 410);
          context.lineTo(110, 380);
          context.closePath();
          context.fill();
          context.fillStyle = "#202020";
          for (let i = 0; i < 8; i++) context.fillRect(190, 130 + i * 32, 260, 10);
        };
        draw();
        setInterval(draw, 100);
        return canvas.captureStream(10);
      };
    });
  }

  test("a customer scans a page: the scanner loads only when tapped, finds the page, and the result becomes the next page", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await installFakeCamera(page);
    const scannerRequests: string[] = [];
    page.on("request", (candidate) => { if (/DocumentScanner/.test(candidate.url())) scannerRequests.push(candidate.url()); });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    expect(scannerRequests).toHaveLength(0);

    const police = page.locator("li", { hasText: "Police abstract" }).first();
    await police.getByTestId("scan-document").click();
    const scanner = page.getByTestId("document-scanner");
    await expect(scanner).toBeVisible();
    expect(scannerRequests.length).toBeGreaterThan(0);

    await expect(scanner.getByRole("button", { name: "Capture" })).toBeEnabled({ timeout: 15_000 });
    await scanner.getByRole("button", { name: "Capture" }).click();
    await expect(scanner.getByText("We found the page.")).toBeVisible();

    // Drag the top-left corner a little: the handles are real, movable controls.
    const handle = scanner.getByTestId("scanner-corner-0");
    const before = await handle.boundingBox();
    await page.mouse.move(before!.x + before!.width / 2, before!.y + before!.height / 2);
    await page.mouse.down();
    await page.mouse.move(before!.x + before!.width / 2 + 12, before!.y + before!.height / 2 + 12, { steps: 4 });
    await page.mouse.up();
    const after = await handle.boundingBox();
    expect(after!.x).toBeGreaterThan(before!.x + 5);

    await scanner.getByRole("button", { name: "Looks right" }).click();
    await expect(scanner.getByTestId("scanner-result")).toBeVisible();
    await scanner.getByRole("button", { name: "Black and white" }).click();
    await expect(scanner.getByRole("button", { name: "Black and white" })).toHaveAttribute("aria-pressed", "true");
    await scanner.getByRole("button", { name: "Use this page" }).click();

    await expect(scanner).toHaveCount(0);
    await expect(police.getByText("Received")).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });

  test("when the camera is refused the customer is handed the plain camera and can still send the page", async ({ page, request }) => {
    await page.addInitScript(() => {
      navigator.mediaDevices.getUserMedia = async () => { throw new DOMException("Permission denied", "NotAllowedError"); };
    });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const police = page.locator("li", { hasText: "Police abstract" }).first();
    const chooser = page.waitForEvent("filechooser");
    await police.getByTestId("scan-document").click();

    await expect(page.getByTestId("scan-notice")).toContainText("We could not use the scanner");
    await expect(page.getByTestId("document-scanner")).toHaveCount(0);
    // The plain camera input was opened for the same slot.
    const picker = await chooser;
    await picker.setFiles(fixture("police_letter.jpg"));
    await expect(police.getByText("Received")).toBeVisible();
  });

  test("on a device with no camera interface the scan button falls back the same way", async ({ page, request }) => {
    await page.addInitScript(() => {
      Object.defineProperty(navigator, "mediaDevices", { value: undefined, configurable: true });
    });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const police = page.locator("li", { hasText: "Police abstract" }).first();
    await police.getByTestId("scan-document").click();

    await expect(page.getByTestId("scan-notice")).toContainText("no camera we can use");
    await expect(police.getByText("Take photo")).toBeVisible();
  });

  test("evidence photos are never scanned: no Scan button on that slot", async ({ page, request }) => {
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await expect(photos.getByText("Take photo")).toBeVisible();
    await expect(photos.getByTestId("scan-document")).toHaveCount(0);
  });
});

test.describe("Item 14: recognise and name every file", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  async function staffUpload(request: APIRequestContext, caseId: string, name: string, content: string) {
    const upload = await request.post(`${API}/api/v1/cases/${caseId}/documents`, {
      headers,
      multipart: { files: { name, mimeType: "application/pdf", buffer: Buffer.from(content) } },
    });
    expect(upload.ok(), await upload.text()).toBeTruthy();
    await waitForDocumentsRead(request, caseId);
    const documents = (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers })).json()) as Record<string, unknown>[];
    return documents.find((document) => document.originalFileName === name)!;
  }

  test("a file the customer sends is named by its type and claim, and the original name is kept", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const police = page.locator("li", { hasText: "Police abstract" }).first();
    await police.locator('input[data-upload="choose"]').setInputFiles(fixture("police_letter.jpg"));
    await expect(police.getByText("Received")).toBeVisible();

    const documents = (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers })).json()) as { originalFileName: string; displayName: string; needsReview: boolean }[];
    expect(documents).toHaveLength(1);
    expect(documents[0].originalFileName).toBe("police_letter.jpg");
    expect(documents[0].displayName).toMatch(/^PoliceAbstract_CLM-[0-9A-F]{8}\.jpg$/);
    expect(documents[0].needsReview).toBe(false);
  });

  test("a file staff add with no slot is filed into the matching one when the match is clear, and the customer sees it received", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    const document = await staffUpload(request, caseId, "scan_0001.pdf", "IIP-MOCK-DOC:PoliceAbstract");
    expect(document.filedByRecognition).toBe(true);
    expect(document.customerDocumentTypeKey).toBe("PoliceAbstract");
    expect(String(document.displayName)).toMatch(/^PoliceAbstract_CLM-[0-9A-F]{8}\.pdf$/);

    await openClaimantPortal(page, token);
    await expect(page.locator("li", { hasText: "Police abstract" }).first().getByText("Received")).toBeVisible();
  });

  test("a file nobody can place goes to staff review, and a person files it into a slot", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    const document = await staffUpload(request, caseId, "mystery.pdf", "nothing readable here");
    expect(document.needsReview).toBe(true);
    expect(String(document.displayName)).toMatch(/^Unsorted_CLM-/);

    const filed = await request.post(`${API}/api/v1/cases/${caseId}/documents/${String(document.documentId)}/file-into`, { headers, data: { documentTypeKey: "ClaimForm" } });
    expect(filed.ok(), await filed.text()).toBeTruthy();
    const after = (await filed.json()) as { needsReview: boolean; displayName: string; originalFileName: string };
    expect(after.needsReview).toBe(false);
    expect(after.displayName).toMatch(/^[A-Za-z]+_CLM-[0-9A-F]{8}\.pdf$/);
    expect(after.originalFileName).toBe("mystery.pdf");

    await openClaimantPortal(page, token);
    await expect(page.locator("li", { hasText: "Claim form" }).first().getByText("Received")).toBeVisible();
  });

  test("another tenant cannot file a document", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const document = await staffUpload(request, caseId, "mystery2.pdf", "nothing readable here");
    const other = await request.post(`${API}/api/v1/cases/${caseId}/documents/${String(document.documentId)}/file-into`, {
      headers: scopeHeaders(newTenant()),
      data: { documentTypeKey: "ClaimForm" },
    });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 15: extracted data into the claim", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  async function productThatReadsPoliceReports(request: APIRequestContext) {
    const productClass = `Reads${Math.random().toString(36).slice(2, 7)}`;
    const maker = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "Admin", "reads-maker");
    const checker = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "Admin", "reads-checker");
    const created = await request.post(`${API}/api/v1/products`, { headers: maker, data: { name: "Reads Cover", productClass, changeReason: "e2e" } });
    const { productId } = (await created.json()) as { productId: string };
    const updated = await request.put(`${API}/api/v1/products/${productId}`, {
      headers: maker,
      data: {
        iraApprovalReference: "E2E-15",
        iraApprovalEffectiveDate: "2026-01-01",
        documentMatrix: [{ documentKey: "PoliceAbstract", customerLabel: "Police abstract", reason: "needed", isRequired: true, requiredAtStageKey: "acknowledgement_and_document_request", isRegulatoryOrWordingMandated: false }],
        claimFields: [
          { label: "Police report number", key: "policeReportNumber", type: "Text", required: false, sortOrder: 1, customerVisible: true, claimTypeKeys: [] },
          { label: "Report date", key: "reportDate", type: "Date", required: false, sortOrder: 2, customerVisible: true, claimTypeKeys: [] },
        ],
        changeReason: "e2e",
      },
    });
    expect(updated.ok(), await updated.text()).toBeTruthy();
    expect((await request.post(`${API}/api/v1/products/${productId}/submit`, { headers: maker, data: { changeReason: "go" } })).ok()).toBeTruthy();
    expect((await request.post(`${API}/api/v1/products/${productId}/approve`, { headers: checker, data: { changeReason: "ok" } })).ok()).toBeTruthy();
    const rules = await request.post(`${API}/api/v1/document-types/PoliceAbstract`, {
      headers: { ...headers, "X-Audit-Reason": "reading rules for the e2e" },
      data: { customerLabel: "Police abstract", description: "Confirms the incident was reported to the police.", active: true, extractionFields: [{ extractedKey: "ReportNumber", claimFieldKey: "policeReportNumber" }, { extractedKey: "ReportDate", claimFieldKey: "reportDate" }] },
    });
    expect(rules.ok(), await rules.text()).toBeTruthy();
    return productClass;
  }

  test("an officer sees what the document says, corrects one answer, rejects another, and the claim shows where its answer came from", async ({ page, request }) => {
    const productClass = await productThatReadsPoliceReports(request);
    const { caseId } = await claimantLinkFor(request, "Accident", productClass);
    const upload = await request.post(`${API}/api/v1/cases/${caseId}/documents`, {
      headers,
      multipart: { files: { name: "scan.pdf", mimeType: "application/pdf", buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract") } },
    });
    expect(upload.ok(), await upload.text()).toBeTruthy();
    await waitForDocumentsRead(request, caseId);

    await page.goto(`/app/claims/${caseId}`);
    const card = page.getByTestId("field-proposals");
    await expect(card.getByText("Suggested from the documents")).toBeVisible();
    await expect(card.getByTestId("field-proposal")).toHaveCount(2);
    await expect(card.getByText("MOCK-POL-0001")).toBeVisible();
    await expect(card.getByText(/From PoliceAbstract_CLM-[0-9A-F]{8}\.pdf/).first()).toBeVisible();

    // Nothing is on the claim yet.
    const before = (await (await request.get(`${API}/api/v1/cases/${caseId}/fields`, { headers })).json()) as { fields: { key: string; value: string | null }[] };
    expect(before.fields.find((field) => field.key === "policeReportNumber")?.value).toBeNull();

    await card.getByLabel("Correct Police report number").fill("CRB/2026/4471");
    await card.getByRole("button", { name: "Accept Police report number" }).click();
    await card.getByRole("button", { name: "Reject Report date" }).click();

    const details = page.getByTestId("claim-details-card");
    await expect(details.getByText("CRB/2026/4471")).toBeVisible();
    await expect(details.getByTestId("field-source").first()).toContainText("Read from a document");
    await expect(card.getByText("Accepted")).toBeVisible();
    await expect(card.getByText("Rejected")).toBeVisible();

    const after = (await (await request.get(`${API}/api/v1/cases/${caseId}/fields`, { headers })).json()) as { fields: { key: string; value: string | null }[] };
    expect(after.fields.find((field) => field.key === "policeReportNumber")?.value).toBe("CRB/2026/4471");
    expect(after.fields.find((field) => field.key === "reportDate")?.value).toBeNull();
  });

  test("another tenant cannot read the suggestions", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const other = await request.get(`${API}/api/v1/cases/${caseId}/field-proposals`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 16: picture checks", () => {
  test("staff see the checks on a customer's photo and record their own reading, which needs a note unless there is no concern", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await photos.locator('input[data-upload="choose"]').setInputFiles(fixture("damage_photo.jpg"));
    await expect(photos.getByText("1 of 10 sent")).toBeVisible();
    await waitForDocumentsRead(request, caseId);

    await page.goto(`/app/claims/${caseId}`);
    const card = page.getByTestId("authenticity-card");
    await expect(card.getByText("Picture checks")).toBeVisible();
    await expect(card.getByText("These are reasons to look at a picture, not a finding that anything is wrong.")).toBeVisible();
    const report = card.getByTestId("authenticity-report");
    await expect(report).toHaveCount(1);
    await expect(report.getByText("Not looked at yet")).toBeVisible();

    await report.getByLabel(/Your reading of/).selectOption("NeedsFollowUp");
    await report.getByRole("button", { name: /Save reading of/ }).click();
    await expect(card.getByRole("alert")).toContainText("Say in a sentence");

    await report.getByLabel(/Note about/).fill("Ask the customer for the original photo.");
    await report.getByRole("button", { name: /Save reading of/ }).click();
    await expect(report.getByText("Needs follow-up").first()).toBeVisible();
    await expect(report.getByText(/Ask the customer for the original photo/)).toBeVisible();

    const reports = (await (await request.get(`${API}/api/v1/cases/${caseId}/authenticity`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) })).json()) as { disposition: string; dispositionNote: string }[];
    expect(reports[0].disposition).toBe("NeedsFollowUp");
  });

  test("another tenant cannot read a claim's picture checks", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const other = await request.get(`${API}/api/v1/cases/${caseId}/authenticity`, { headers: scopeHeaders(newTenant()) });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 17: background pipeline", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  test("an upload returns at once; the customer sees Being checked, then Received without doing anything", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const police = page.locator("li", { hasText: "Police abstract" }).first();
    // The first attempt fails and the next waits a second, so "Being checked" is on screen long enough to see.
    await police.locator('input[data-upload="choose"]').setInputFiles({ name: "police.pdf", mimeType: "application/pdf", buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ONCE") });
    await expect(police.getByText("Being checked")).toBeVisible();
    await expect(police.getByText("Received")).toBeVisible({ timeout: 30_000 });
  });

  test("an administrator sees what could not be done, with the reason, and sends it back", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const police = page.locator("li", { hasText: "Police abstract" }).first();
    await police.locator('input[data-upload="choose"]').setInputFiles({ name: "police.pdf", mimeType: "application/pdf", buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ALWAYS") });
    await expect(police.getByText("Could not be read")).toBeVisible({ timeout: 30_000 });
    const reference = `CLM-${caseId.replace(/-/g, "").slice(0, 8).toUpperCase()}`;

    await page.goto("/app/admin/background-work");
    const view = page.getByTestId("background-work");
    await expect(view).toBeVisible();
    await page.getByLabel("Show").selectOption("DeadLettered");
    const job = view.getByTestId("background-job").filter({ hasText: reference });
    await expect(job).toHaveCount(1);
    await expect(job.getByText("Reading an uploaded document")).toBeVisible();
    await expect(job.getByText("Gave up")).toBeVisible();
    await expect(job.getByText(/could not be read by the document service/)).toBeVisible();
    await expect(job.getByText(/attempt 3 of 3/)).toBeVisible();

    await job.getByRole("button", { name: /Send back/ }).click();
    await expect(view.getByRole("status")).toContainText("was sent back to the queue");
  });

  test("another tenant cannot see or send back this workspace's work", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const mine = (await (await request.get(`${API}/api/v1/admin/background-jobs?caseId=${caseId}`, { headers })).json()) as { jobs: { jobId: string }[] };
    const other = scopeHeaders(newTenant());
    const theirs = (await (await request.get(`${API}/api/v1/admin/background-jobs`, { headers: other })).json()) as { jobs: unknown[] };
    expect(theirs.jobs).toHaveLength(0);
    if (mine.jobs.length > 0) {
      const retry = await request.post(`${API}/api/v1/admin/background-jobs/${mine.jobs[0].jobId}/retry`, { headers: other });
      expect(retry.status()).toBe(404);
    }
  });
});

test.describe("Item 18: review score and recommendation", () => {
  test("a supervisor sees the score, its reasons and the advice with sources, and records a response that needs a reason to disagree", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await photos.locator('input[data-upload="choose"]').setInputFiles(fixture("damage_photo.jpg"));
    await expect(photos.getByText("1 of 10 sent")).toBeVisible();
    await waitForDocumentsRead(request, caseId);

    await page.goto(`/app/claims/${caseId}`);
    const card = page.getByTestId("claim-assessment");
    await expect(card.getByText("Review score and recommendation")).toBeVisible();
    await expect(card.getByText(/does not approve, reject or settle anything/)).toBeVisible();

    // What the platform prepared and what a person answers are two separate containers.
    const prepared = card.getByTestId("assessment-prepared");
    const response = card.getByTestId("assessment-response");
    await expect(prepared).toBeVisible();
    await expect(response).toBeVisible();
    await expect(prepared.getByTestId("assessment-score")).toHaveText(/^\d+$/);
    await expect(prepared.getByText("It is not a chance that anyone has done wrong.")).toBeVisible();

    // The advice names what is missing and where each reason comes from.
    const recommendation = prepared.getByTestId("assessment-recommendation");
    await expect(recommendation.getByText("Recommended next step")).toBeVisible();
    await expect(recommendation.getByTestId("assessment-reason").first()).toContainText("Source:");
    await expect(prepared.getByTestId("assessment-requests")).toContainText("Ask the customer for:");

    // The written explanation is made on request and says it is a test model.
    await expect(prepared.getByTestId("assessment-narrative")).toBeVisible();
    await prepared.getByRole("button", { name: /Prepare the explanation|Write it again/ }).click();
    await expect(prepared.getByTestId("assessment-narrative")).toContainText("MOCK AI OUTPUT", { timeout: 15_000 });
    await expect(prepared.getByText(/\(test model\)/)).toBeVisible();

    // Disagreeing needs a reason.
    await response.getByLabel("Your response to the recommendation").selectOption("Disagree");
    await response.getByRole("button", { name: "Record response" }).click();
    await expect(response.getByRole("alert")).toContainText("why you disagree");
    await response.getByLabel("Why, if you disagree").fill("The customer posted the missing form this morning.");
    await response.getByRole("button", { name: "Record response" }).click();
    await expect(response.getByTestId("assessment-response-recorded")).toContainText("Disagreed by local-admin: The customer posted the missing form this morning.");

    await page.reload();
    await expect(page.getByTestId("assessment-response-recorded")).toContainText("Disagreed by local-admin");
  });

  test("a claims officer does not see the review score, and another tenant cannot read it", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    expect((await request.get(`${API}/api/v1/cases/${caseId}/assessment`, { headers: officer })).status()).toBe(403);
    expect((await request.get(`${API}/api/v1/cases/${caseId}/assessment`, { headers: scopeHeaders(newTenant()) })).status()).toBe(404);
  });
});

test.describe("Item 19: customer status wording and timeline", () => {
  test("a customer on a phone reads where the claim stands, and the insurer's own wording replaces the default", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    await expect(page.getByTestId("claim-status-headline")).toHaveText("We have your claim");
    await expect(page.getByTestId("claim-status-detail")).toContainText("We have received your claim");
    const timeline = page.getByTestId("claim-timeline");
    // The steps come from the claim's product: six customer steps from the default claim process.
    await expect(timeline.locator("li")).toHaveCount(6);
    await expect(timeline.locator("li").nth(1)).toContainText("Documents and details");
    await expect(timeline.locator('li[data-state="Current"]')).toHaveCount(1);
    await expect(timeline.locator('li[data-state="Current"]')).toContainText("Claim received");
    // Nothing runs off the side of a 390 pixel screen.
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

    const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
    try {
      const saved = await request.put(`${API}/api/v1/admin/claim-status-wording`, {
        headers,
        data: { wording: { New: { headline: "Thank you, we have your claim", detail: "A claims officer will call you within one working day." } } },
      });
      expect(saved.ok()).toBeTruthy();
      await page.reload();
      await expect(page.getByTestId("claim-status-headline")).toHaveText("Thank you, we have your claim");
      await expect(page.getByTestId("claim-status-detail")).toHaveText("A claims officer will call you within one working day.");
    } finally {
      await request.put(`${API}/api/v1/admin/claim-status-wording`, { headers, data: { wording: {} } });
    }
  });

  test("an administrator edits the wording on its own page and can go back to the default", async ({ page }) => {
    await page.goto("/app/admin/status-wording");
    const view = page.getByTestId("status-wording");
    await expect(view.getByText("The default wording is a starting point to be confirmed")).toBeVisible();

    const headline = view.locator("#headline-Closed");
    await headline.fill("This claim is finished");
    await view.getByRole("button", { name: "Save wording" }).click();
    await expect(view.getByRole("status")).toContainText("Saved");
    await page.reload();
    await expect(page.locator("#headline-Closed")).toHaveValue("This claim is finished");

    await page.getByRole("button", { name: /Use the default wording for Closed/ }).click();
    await page.getByRole("button", { name: "Save wording" }).click();
    await expect(page.getByTestId("status-wording").getByRole("status")).toContainText("Saved");
    await page.reload();
    await expect(page.locator("#headline-Closed")).toHaveValue("Your claim is closed");
  });
});

test.describe("Item 20: change status", () => {
  test("an officer changes a claim's status with a reason, the customer sees it, and deciding statuses are explained rather than offered", async ({ page, request }) => {
    const { token, caseId } = await claimantLinkFor(request);

    await page.goto(`/app/claims/${caseId}`);
    const panel = page.getByTestId("claim-status-change");
    await expect(panel.getByTestId("current-status")).toHaveText("Received");

    // Only the moves an officer decides are offered; the rest say how they are reached.
    const choice = panel.getByLabel("Move the claim to");
    await expect(choice.locator("option")).toHaveText(["Choose a status", "Under review", "Waiting on documents"]);
    await panel.getByText("How the other statuses are reached").click();
    await expect(panel.getByTestId("other-statuses")).toContainText("Approvals queue");
    await expect(panel.getByTestId("other-statuses")).toContainText("Use Resolve");

    // A reason is needed.
    await choice.selectOption("InReview");
    await expect(panel.getByRole("button", { name: "Change status" })).toBeDisabled();
    await panel.getByLabel("Why you are changing the status").fill("Started going through the documents.");
    await panel.getByRole("button", { name: "Change status" }).click();
    await expect(panel.getByRole("status")).toContainText("The claim is now under review.");
    await expect(panel.getByTestId("current-status")).toHaveText("Under review");

    // The customer sees the matching words and the step they are on.
    await page.setViewportSize({ width: 390, height: 844 });
    await openClaimantPortal(page, token);
    await expect(page.getByTestId("claim-status-headline")).toHaveText("We are reviewing your claim");
    await expect(page.getByTestId("claim-timeline").locator('li[data-state="Current"]')).toContainText("Assessment");
  });

  test("a deciding status cannot be chosen by hand and another tenant cannot change this claim", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    const move = await request.post(`${API}/api/v1/cases/${caseId}/status`, { headers: officer, data: { toStatus: "Settled", reason: "Skipping ahead." } });
    expect(move.status()).toBe(409);
    const other = await request.post(`${API}/api/v1/cases/${caseId}/status`, { headers: scopeHeaders(newTenant()), data: { toStatus: "InReview", reason: "Not mine." } });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 21: customer messages on status change", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  test("an administrator turns status messages on, and a status change then reaches the customer in the insurer's words", async ({ page, request }) => {
    try {
      await page.goto("/app/admin/status-wording");
      const settings = page.getByTestId("status-messages-settings");
      await expect(settings.getByText("Telling customers when the status changes")).toBeVisible();
      await settings.getByLabel("Tell customers when their claim's status changes").check();
      await settings.getByLabel(/Send them without a person approving each one/).check();
      await settings.getByLabel("WhatsApp template name for Under review").fill("claim_in_review");
      await settings.getByRole("button", { name: "Save message settings" }).click();
      await expect(settings.getByRole("status")).toContainText("Saved");
      await page.reload();
      await expect(page.getByTestId("status-messages-settings").getByLabel("Tell customers when their claim's status changes")).toBeChecked();
      await expect(page.getByTestId("status-messages-settings").getByLabel("WhatsApp template name for Under review")).toHaveValue("claim_in_review");

      const { caseId } = await claimantLinkFor(request);
      const changed = await request.post(`${API}/api/v1/cases/${caseId}/status`, { headers, data: { toStatus: "InReview", reason: "Started the review." } });
      expect(changed.ok()).toBeTruthy();

      const outbox = (await (await request.get(`${API}/api/v1/dev/mock-outbox`, { headers })).json()) as { body: string; recipient: string }[];
      const sent = outbox.find((message) => message.body.includes(`CLM-${caseId.replace(/-/g, "").slice(0, 8).toUpperCase()}`) && message.body.includes("An update on your claim"));
      expect(sent?.body).toContain("We are reviewing your claim");
      expect(sent?.recipient).toBe("+256700000777");
    } finally {
      await request.put(`${API}/api/v1/admin/status-notifications`, { headers, data: { notifyCustomerOnStatusChange: false, autoSendStatusMessages: false, whatsAppTemplates: {} } });
    }
  });

  test("an officer cannot read or change the message settings and another tenant has its own", async ({ request }) => {
    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    expect((await request.get(`${API}/api/v1/admin/status-notifications`, { headers: officer })).status()).toBe(403);
    const other = await request.get(`${API}/api/v1/admin/status-notifications`, { headers: scopeHeaders(newTenant()) });
    expect(((await other.json()) as { notifyCustomerOnStatusChange: boolean }).notifyCustomerOnStatusChange).toBe(false);
  });
});

test.describe("Item 22: staff send a claim link", () => {
  test("an officer sends the customer a link from the claim page and the customer opens the claim with it", async ({ page, request }) => {
    const { caseId } = await claimantLinkFor(request);
    await page.goto(`/app/claims/${caseId}`);
    const panel = page.getByTestId("send-claim-link");
    await expect(panel.getByText("Send claim link", { exact: true }).first()).toBeVisible();

    await panel.getByRole("button", { name: "Send claim link" }).click();
    await expect(panel.getByRole("status")).toContainText("Link sent by text message to +256");
    // The screen shows where it went, masked, and never the link.
    await expect(panel).not.toContainText("/access/verify");

    const outbox = (await (await request.get(`${API}/api/v1/dev/mock-outbox`, { headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE) })).json()) as { body: string }[];
    const body = outbox.find((message) => message.body.includes("/access/verify?token=") && message.body.includes(`CLM-${caseId.replace(/-/g, "").slice(0, 8).toUpperCase()}`))!.body;
    const token = decodeURIComponent(/token=([^\s]+)/.exec(body)![1]);

    await page.setViewportSize({ width: 390, height: 844 });
    await openClaimantPortal(page, token);
    await expect(page.getByTestId("claim-status-headline")).toBeVisible();
  });

  test("a role without messaging rights cannot send a link and another tenant cannot send one for this claim", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const broker = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "Broker", "e2e-broker");
    expect((await request.post(`${API}/api/v1/cases/${caseId}/claim-link`, { headers: broker, data: {} })).status()).toBe(403);
    const other = await request.post(`${API}/api/v1/cases/${caseId}/claim-link`, { headers: scopeHeaders(newTenant()), data: {} });
    expect(other.status()).toBe(404);
  });
});

test.describe("Item 23: slow connections", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  /** A photo-sized file of random bytes with a JPEG start, large enough to need several pieces. */
  function bigPhoto(): Buffer {
    const bytes = Buffer.alloc(1_150_000);
    for (let index = 0; index < bytes.length; index += 1) bytes[index] = (index * 131 + 17) & 0xff;
    bytes[0] = 0xff;
    bytes[1] = 0xd8;
    return bytes;
  }

  /** About 2G: a third of a second to reach the server, and 120 kilobytes a second up. Returns a switch for the signal. */
  async function slowDown(page: Page) {
    const session = await page.context().newCDPSession(page);
    await session.send("Network.enable");
    const conditions = { latency: 300, downloadThroughput: 250 * 1024, uploadThroughput: 120 * 1024 };
    await session.send("Network.emulateNetworkConditions", { offline: false, ...conditions });
    return async (online: boolean) => {
      await session.send("Network.emulateNetworkConditions", { offline: !online, ...conditions });
    };
  }

  async function percentNow(page: Page) {
    return Number((await page.getByTestId("upload-progress").getAttribute("data-percent").catch(() => "0")) ?? 0);
  }

  async function documentsOf(request: APIRequestContext, caseId: string) {
    return (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers })).json()) as { sha256: string; originalFileName: string }[];
  }

  test("on a slow phone connection a large photo shows progress, survives the signal dropping, and arrives byte for byte", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const signal = await slowDown(page);
    const photo = bigPhoto();
    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();

    await photos.locator('input[data-upload="choose"]').setInputFiles({ name: "IMG_slow.jpg", mimeType: "image/jpeg", buffer: photo });
    const progress = photos.getByTestId("upload-progress");
    await expect(progress).toBeVisible();
    await expect(progress).toContainText("Sending IMG_slow.jpg");
    await expect.poll(() => percentNow(page), { timeout: 30_000 }).toBeGreaterThanOrEqual(20);

    // The signal drops in the middle: the screen says so instead of failing.
    await signal(false);
    await expect(progress).toContainText(/offline|connection dropped|Trying again/i, { timeout: 15_000 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

    // It comes back and the upload carries on by itself.
    await signal(true);
    await expect(photos.getByText("1 of 10 sent")).toBeVisible({ timeout: 90_000 });
    await expect(photos.getByTestId("upload-progress")).toHaveCount(0);

    const stored = await documentsOf(request, caseId);
    expect(stored).toHaveLength(1);
    expect(stored[0].originalFileName).toBe("IMG_slow.jpg");
    expect(stored[0].sha256).toBe(createHash("sha256").update(photo).digest("hex"));
  });

  test("after a reload in the middle of a send the same file carries on from where it stopped", async ({ page, request }, testInfo) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    await slowDown(page);
    const photo = bigPhoto();
    // A real file on disk keeps the same identity (name, size, last modified) when it is chosen again.
    const onDisk = join(testInfo.outputDir, "IMG_resume.jpg");
    mkdirSync(testInfo.outputDir, { recursive: true });
    writeFileSync(onDisk, photo);
    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();

    await photos.locator('input[data-upload="choose"]').setInputFiles(onDisk);
    await expect.poll(() => percentNow(page), { timeout: 30_000 }).toBeGreaterThanOrEqual(40);
    await page.reload();

    // The row remembers the unfinished send and nothing was stored yet.
    const reloaded = page.locator("li", { hasText: "Photos of the damage" }).first();
    await expect(reloaded.getByTestId("unfinished-upload")).toContainText("IMG_resume.jpg was interrupted");
    expect(await documentsOf(request, caseId)).toHaveLength(0);

    const pieces: string[] = [];
    page.on("request", (sent) => {
      if (sent.method() === "PUT" && sent.url().includes("/chunks/")) pieces.push(sent.url().split("/chunks/")[1]);
    });
    await slowDown(page);
    await reloaded.locator('input[data-upload="choose"]').setInputFiles(onDisk);
    await expect(reloaded.getByText("1 of 10 sent")).toBeVisible({ timeout: 90_000 });

    // Fewer pieces went the second time than the file has, and the stored file is exact.
    expect(pieces.length).toBeGreaterThan(0);
    expect(pieces.length).toBeLessThan(Math.ceil(photo.length / (256 * 1024)));
    const stored = await documentsOf(request, caseId);
    expect(stored).toHaveLength(1);
    expect(stored[0].sha256).toBe(createHash("sha256").update(photo).digest("hex"));
    await expect(reloaded.getByTestId("unfinished-upload")).toHaveCount(0);
  });
});

test.describe("Item 24: claims pack", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  test("a supervisor downloads a pack from the claim page with the summary, the renamed documents, the untouched evidence and a manifest that matches", async ({ page, request }, testInfo) => {
    const { token, caseId } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);
    const police = page.locator("li", { hasText: "Police abstract" }).first();
    await police.locator('input[data-upload="choose"]').setInputFiles({ name: "scan_001.pdf", mimeType: "application/pdf", buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract the police abstract") });
    await expect(police.getByText("Received")).toBeVisible({ timeout: 30_000 });
    const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
    await photos.locator('input[data-upload="choose"]').setInputFiles(join(FIXTURES, "damage_photo.jpg"));
    await expect(photos.getByText("1 of 10 sent")).toBeVisible();
    await waitForDocumentsRead(request, caseId);

    await page.goto(`/app/claims/${caseId}`);
    const box = page.getByTestId("claims-pack");
    await expect(box.getByText("Parts prepared by the platform are marked advisory.")).toBeVisible();
    const [download] = await Promise.all([page.waitForEvent("download"), box.getByRole("button", { name: "Download claims pack" }).click()]);
    const reference = `CLM-${caseId.replace(/-/g, "").slice(0, 8).toUpperCase()}`;
    expect(download.suggestedFilename()).toBe(`ClaimsPack_${reference}.zip`);
    await expect(box.getByRole("status")).toContainText(`Downloaded ClaimsPack_${reference}.zip`);

    // What was downloaded is the same pack the API serves, and its contents are right.
    const target = join(testInfo.outputDir, "pack.zip");
    mkdirSync(testInfo.outputDir, { recursive: true });
    await download.saveAs(target);
    const bytes = readFileSync(target);
    expect(bytes.subarray(0, 2).toString()).toBe("PK");
    const listing = (await (await request.get(`${API}/api/v1/compliance/cases/${caseId}/claims-pack`, { headers })).body()).toString("latin1");
    for (const name of [`00_ClaimSummary_${reference}.pdf`, `01_Documents/PoliceAbstract_${reference}.pdf`, "02_Evidence/", "manifest.json"]) {
      expect(listing).toContain(name);
    }
    // The zip's own index lists the same files by name (the bytes are checked against the manifest in the API tests).
    const downloaded = bytes.toString("latin1");
    for (const name of [`00_ClaimSummary_${reference}.pdf`, `01_Documents/PoliceAbstract_${reference}.pdf`, "02_Evidence/", "manifest.json"]) {
      expect(downloaded).toContain(name);
    }
    // Keep the downloaded pack where the run's other artefacts are, so it can be opened and checked by hand.
    await testInfo.attach("claims-pack.zip", { path: target, contentType: "application/zip" });
  });

  test("an officer cannot download a pack and another tenant cannot either", async ({ request }) => {
    const { caseId } = await claimantLinkFor(request);
    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    expect((await request.get(`${API}/api/v1/compliance/cases/${caseId}/claims-pack`, { headers: officer })).status()).toBe(403);
    expect((await request.get(`${API}/api/v1/compliance/cases/${caseId}/claims-pack`, { headers: scopeHeaders(newTenant()) })).status()).toBe(404);
  });
});

test.describe("Item 25: claims pack by email", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);
  const reset = (request: APIRequestContext) => request.put(`${API}/api/v1/admin/claims-pack-delivery`, { headers, data: { recipients: [], trigger: "Manual", triggerStatus: null } });

  test("an administrator names who receives the pack, staff send it now, and a large pack goes as a secure link that downloads from the public page", async ({ page, request }) => {
    try {
      await page.goto("/app/admin/claims-pack");
      const settings = page.getByTestId("claims-pack-email");
      await settings.getByLabel("Who receives it").fill("reinsurer@example.com\nassessor@example.com");
      await settings.getByLabel("When it is sent").selectOption("Complete");
      await settings.getByRole("button", { name: "Save" }).click();
      await expect(settings.getByRole("status")).toContainText("Saved");
      await page.reload();
      await expect(page.getByLabel("Who receives it")).toHaveValue("reinsurer@example.com\nassessor@example.com");
      await expect(page.getByLabel("When it is sent")).toHaveValue("Complete");

      const { token, caseId } = await claimantLinkFor(request);
      await openClaimantPortal(page, token);
      const photos = page.locator("li", { hasText: "Photos of the damage" }).first();
      await photos.locator('input[data-upload="choose"]').setInputFiles({ name: "IMG_large.jpg", mimeType: "image/jpeg", buffer: randomBytes(3_600_000) });
      await expect(photos.getByText("1 of 10 sent")).toBeVisible({ timeout: 60_000 });
      await waitForDocumentsRead(request, caseId);

      await page.goto(`/app/claims/${caseId}`);
      const box = page.getByTestId("claims-pack");
      await box.getByRole("button", { name: "Send pack now" }).click();
      await expect(box.getByRole("status")).toContainText("Sent to 2 addresses as a secure link.");
      await expect(box.getByTestId("claims-pack-history")).toContainText("sent as a secure link to r***@example.com, a***@example.com");

      const outbox = (await (await request.get(`${API}/api/v1/dev/mock-outbox`, { headers })).json()) as { subject: string; body: string; recipient: string }[];
      const reference = `CLM-${caseId.replace(/-/g, "").slice(0, 8).toUpperCase()}`;
      const mail = outbox.find((message) => message.subject === `Claims pack ${reference}` && message.recipient === "reinsurer@example.com")!;
      const link = /(http\S+\/shared\/claims-pack\?token=\S+)/.exec(mail.body)![1];

      // Someone with no account opens the link on a phone and downloads the pack.
      await page.context().clearCookies();
      await page.setViewportSize({ width: 390, height: 844 });
      // The link points at the configured portal address; this run serves the portal on its own port, so use the same path here.
      await page.goto(new URL(link).pathname + new URL(link).search);
      await expect(page.getByRole("heading", { name: "Claims pack" })).toBeVisible();
      const [download] = await Promise.all([page.waitForEvent("download"), page.getByRole("button", { name: "Download the pack" }).click()]);
      expect(download.suggestedFilename()).toBe(`ClaimsPack_${reference}.zip`);
      await expect(page.getByTestId("shared-pack-done")).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    } finally {
      await reset(request);
    }
  });

  test("a link that is not real says so plainly, and an officer cannot change who receives packs", async ({ page, request }) => {
    await page.goto("/shared/claims-pack?token=not-a-real-token");
    await page.getByRole("button", { name: "Download the pack" }).click();
    await expect(page.getByTestId("shared-pack-error")).toContainText("expired or is no longer valid");

    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    expect((await request.put(`${API}/api/v1/admin/claims-pack-delivery`, { headers: officer, data: { recipients: ["a@example.com"], trigger: "Manual" } })).status()).toBe(403);
    const other = await request.get(`${API}/api/v1/admin/claims-pack-delivery`, { headers: scopeHeaders(newTenant()) });
    expect(((await other.json()) as { recipients: string[] }).recipients).toHaveLength(0);
  });
});

test.describe("Item 26: customer memory", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  test("a returning customer is offered the ID they sent before and reuses it with one press, on a phone", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const first = await claimantLinkFor(request);
    await openClaimantPortal(page, first.token);
    const idRow = page.locator("li", { hasText: "Your ID" }).first();
    await idRow.locator('input[data-upload="choose"]').setInputFiles({ name: "national_id.jpg", mimeType: "image/jpeg", buffer: Buffer.from("IIP-MOCK-DOC:CustomerId national id card") });
    await expect(idRow.getByText("Received")).toBeVisible({ timeout: 30_000 });
    await waitForDocumentsRead(request, first.caseId);
    const firstReference = `CLM-${first.caseId.replace(/-/g, "").slice(0, 8).toUpperCase()}`;

    // The same customer files again.
    const second = await claimantLinkFor(request);
    await openClaimantPortal(page, second.token);
    const card = page.getByTestId("on-file-card");
    await expect(card.getByText("What we already have")).toBeVisible();
    const offer = card.getByTestId("on-file-documents").locator("li").filter({ hasText: "Your ID" }).first();
    await expect(offer).toContainText("national_id.jpg");
    await expect(offer.first()).toContainText(/from claim CLM-/);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    // Offered, not used: nothing is stored on the new claim yet.
    expect(((await (await request.get(`${API}/api/v1/cases/${second.caseId}/documents`, { headers })).json()) as unknown[]).length).toBe(0);

    await card.getByRole("button", { name: "Use the one on file for Your ID" }).click();
    await expect(card.getByRole("status")).toContainText("The document on file was added to your claim.");
    await expect(page.locator("li", { hasText: "Your ID" }).filter({ hasText: "Received" }).first()).toBeVisible();
    await expect(card.getByTestId("on-file-documents")).toHaveCount(0);

    const stored = (await (await request.get(`${API}/api/v1/cases/${second.caseId}/documents`, { headers })).json()) as { originalFileName: string; reusedFromClaim: string | null }[];
    expect(stored).toHaveLength(1);
    expect(stored[0].originalFileName).toBe("national_id.jpg");
    expect(stored[0].reusedFromClaim).toMatch(/^CLM-[0-9A-F]{8}$/);
    expect(firstReference).toMatch(/^CLM-/);
  });

  test("an administrator can switch memory off and a customer is then offered nothing", async ({ page, request }) => {
    try {
      await page.goto("/app/admin/settings");
      const box = page.getByLabel("Offer a customer the details and documents they gave on an earlier claim");
      await expect(box).toBeChecked();
      await box.uncheck();
      await page.getByRole("button", { name: "Save settings" }).click();
      await expect(page.getByRole("status").filter({ hasText: "Settings saved." })).toBeVisible();

      const next = await claimantLinkFor(request);
      await openClaimantPortal(page, next.token);
      await expect(page.getByTestId("claimant-documents")).toBeVisible();
      await expect(page.getByTestId("on-file-card")).toHaveCount(0);
    } finally {
      await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { customerMemoryEnabled: true } });
    }
  });

  test("an officer cannot change the memory settings and another tenant's customer is offered none of this workspace's papers", async ({ request }) => {
    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    expect((await request.put(`${API}/api/v1/admin/tenant-settings`, { headers: officer, data: { customerMemoryEnabled: false } })).status()).toBe(403);
    const settings = await request.get(`${API}/api/v1/admin/tenant-settings`, { headers: scopeHeaders(newTenant()) });
    expect(((await settings.json()) as { customerMemoryEnabled: boolean }).customerMemoryEnabled).toBe(true);
  });
});

test.describe("Item 27: several workspaces on one WhatsApp channel", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  test("an administrator names who Kato speaks for, the next workspace is not affected, and an officer cannot change it", async ({ page, request }) => {
    try {
      await page.goto("/app/admin/settings");
      await expect(page.getByLabel("Organisation")).toHaveValue("");
      await page.getByLabel("Organisation").fill("Mavuno Bank");
      await page.getByLabel("Short name").fill("Mavuno");
      await page.getByRole("button", { name: "Save settings" }).click();
      await expect(page.getByRole("status").filter({ hasText: "Settings saved." })).toBeVisible();

      await page.reload();
      await expect(page.getByLabel("Organisation")).toHaveValue("Mavuno Bank");
      await expect(page.getByLabel("Short name")).toHaveValue("Mavuno");

      const other = await request.get(`${API}/api/v1/admin/tenant-settings`, { headers: scopeHeaders(newTenant()) });
      expect(((await other.json()) as { assistantOrganisation: string | null }).assistantOrganisation).toBeNull();
      const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
      expect((await request.put(`${API}/api/v1/admin/tenant-settings`, { headers: officer, data: { assistantOrganisation: "Someone Else" } })).status()).toBe(403);
    } finally {
      await request.put(`${API}/api/v1/admin/tenant-settings`, { headers, data: { assistantOrganisation: " ", assistantShortName: " " } });
    }
  });

  test("a workspace with no WhatsApp number sees the channel as not configured, and a webhook for a number nobody connected is refused", async ({ request }) => {
    const health = await request.get(`${API}/api/v1/integrations/whatsapp/health`, { headers: scopeHeaders(newTenant()) });
    const body = (await health.json()) as { configured: boolean; status: string };
    expect(body.configured).toBe(false);
    expect(body.status).toBe("Not configured");

    const payload = JSON.stringify({ object: "whatsapp_business_account", entry: [{ id: "WABA", changes: [{ field: "messages", value: { messaging_product: "whatsapp", metadata: { phone_number_id: "nobody-owns-this" }, messages: [] } }] }] });
    const refused = await request.post(`${API}/api/v1/integrations/whatsapp/webhook`, { headers: { "Content-Type": "application/json", "X-Hub-Signature-256": "sha256=" + "0".repeat(64) }, data: payload });
    expect([401, 503]).toContain(refused.status());
    const verify = await request.get(`${API}/api/v1/integrations/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=guess&hub.challenge=1`);
    expect([401, 503]).toContain(verify.status());
  });
});

test.describe("Item 28: mail collected for every workspace, including over IMAP", () => {
  const imapBody = (host: string, username: string, password = "app-password") => ({
    displayName: "Claims over IMAP", mailboxAddress: username, ingestionEnabled: true, defaultChannel: "Email", source: "Imap",
    imapHost: host, imapPort: 993, imapUsername: username, imapPassword: password,
  });

  test("an administrator connects an IMAP mailbox from the screen, a wrong password is explained, and the password is never shown again", async ({ page }) => {
    const unique = Math.random().toString(36).slice(2, 8);
    await page.goto("/app/admin/email");
    await page.getByRole("button", { name: "Add mailbox" }).click();
    await page.getByLabel("Display name").fill("Claims over IMAP");
    await page.getByLabel("Mailbox address").fill(`claims-${unique}@insurer.test`);
    await page.getByLabel("Where the mail is").selectOption("Imap");
    await page.getByLabel("Mail host").fill(`imap.${unique}.test`);
    await page.getByLabel("Username").fill(`claims-${unique}@insurer.test`);
    await page.getByLabel("Password").fill("wrong-password");
    await page.getByLabel("Start collecting mail from this address straight away").uncheck();
    await page.getByLabel("Reason for audit trail").fill("Connecting the claims mailbox over IMAP.");
    await page.getByRole("button", { name: "Add mailbox" }).last().click();
    await expect(page.getByText("The mail host refused the username or password.")).toBeVisible();

    await page.getByLabel("Password").fill("app-password");
    await page.getByRole("button", { name: "Add mailbox" }).last().click();
    const card = page.locator("div", { hasText: `claims-${unique}@insurer.test` }).filter({ hasText: `IMAP, imap.${unique}.test` }).first();
    await expect(card).toBeVisible();
    await expect(page.getByText("app-password")).toHaveCount(0);
  });

  test("mail sent to an IMAP mailbox is collected, becomes a claim with its file, and another workspace sees none of it", async ({ request }) => {
    const tenant = newTenant();
    const headers = scopeHeaders(tenant);
    const host = `imap.${tenant}.test`;
    const reason = { ...headers, "X-Audit-Reason": "End to end test of IMAP collection." };
    const added = await request.post(`${API}/api/v1/integrations/email/mailboxes`, { headers: reason, data: imapBody(host, "claims@insurer.test") });
    expect(added.status()).toBe(201);
    expect(JSON.stringify(await added.json())).not.toContain("app-password");

    const delivered = await request.post(`${API}/api/v1/dev/mock-mailbox/deliver`, {
      headers,
      data: { host, username: "claims@insurer.test", from: "customer@example.test", subject: "Accident at Kira", body: "A taxi hit my car.", attachments: [{ fileName: "police_report.pdf", contentType: "application/pdf", text: "police letter" }] },
    });
    expect(delivered.ok()).toBe(true);

    const collected = (await (await request.post(`${API}/api/v1/integrations/email/ingest`, { headers: reason })).json()) as { messagesIngested: number };
    expect(collected.messagesIngested).toBe(1);
    const messages = (await (await request.get(`${API}/api/v1/integrations/email/messages`, { headers })).json()) as { messageId: string; subject: string }[];
    expect(messages.map((message) => message.subject)).toEqual(["Accident at Kira"]);

    const processed = await request.post(`${API}/api/v1/integrations/email/process-message`, { headers: reason, data: { messageId: messages[0].messageId } });
    expect(processed.ok()).toBe(true);
    const { caseId } = (await processed.json()) as { caseId: string };
    const documents = (await (await request.get(`${API}/api/v1/cases/${caseId}/documents`, { headers })).json()) as { originalFileName: string }[];
    expect(documents.map((document) => document.originalFileName)).toEqual(["police_report.pdf"]);

    const other = (await (await request.get(`${API}/api/v1/integrations/email/messages`, { headers: scopeHeaders(newTenant()) })).json()) as unknown[];
    expect(other).toEqual([]);
    const officer = scopeHeaders(tenant, "workspace-main", "ClaimsOfficer", "e2e-officer");
    expect((await request.post(`${API}/api/v1/integrations/email/mailboxes`, { headers: { ...officer, "X-Audit-Reason": "Not allowed." }, data: imapBody(host, "other@insurer.test") })).status()).toBe(403);
  });
});

test.describe("Item 29: each insurer's customers arrive on their own portal", () => {
  const headers = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE);

  test("an administrator sets the customer portal identity, links carry the link name, and the customer's pages show the insurer on a phone", async ({ page, request }) => {
    const slug = `acme-${Math.random().toString(36).slice(2, 8)}`;
    await page.goto("/app/admin/settings");
    await page.getByLabel("Link name").fill(slug);
    await page.getByLabel("Name customers know you by").fill("Acme Insurance");
    await page.getByLabel("Button colour").fill("#1F5EFF");
    await page.getByLabel("Support phone").fill("+256 700 000 111");
    await page.getByLabel("Support email").fill("help@acme.test");
    await page.getByRole("button", { name: "Save customer portal" }).click();
    await expect(page.getByText("Customer portal details saved.")).toBeVisible();
    await expect(page.getByTestId("portal-example-link")).toContainText(`&t=${slug}`);

    // A link made now carries the link name.
    const claim = await claimantLinkFor(request);
    let body = "";
    for (let attempt = 0; attempt < 10 && !body.includes("token="); attempt++) {
      const outbox = (await (await request.get(`${API}/api/v1/dev/mock-outbox`, { headers })).json()) as { recipient: string; body: string }[];
      body = outbox.find((message) => message.recipient === claim.email && /token=/.test(message.body))?.body ?? "";
      if (!body.includes("token=")) await new Promise((resolve) => setTimeout(resolve, 300));
    }
    expect(body).toMatch(new RegExp(`token=[A-Za-z0-9_-]+&t=${slug}`));

    // The customer's pages carry the insurer's name and who to call, and fit a phone.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/access/verify?token=${claim.token}&t=${slug}`);
    await expect(page.getByText("Your claim", { exact: true })).toBeVisible();
    await expect(page.getByText("Acme Insurance").first()).toBeVisible();
    await expect(page.getByTestId("portal-support-line")).toContainText("+256 700 000 111");
    await expect(page.getByTestId("portal-support-line")).toContainText("help@acme.test");
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  });

  test("a link that names no insurer the platform knows is refused plainly", async ({ page, request }) => {
    const claim = await claimantLinkFor(request);
    await page.goto(`/access/verify?token=${claim.token}&t=nobody-here`);
    await expect(page.getByText(/claim link is not valid/i)).toBeVisible();
  });

  test("an officer cannot change the customer portal and another workspace's link name is its own", async ({ request }) => {
    const officer = scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE, "ClaimsOfficer", "e2e-officer");
    const body = { slug: "officer-try", displayName: "No", accentColour: null, logoUrl: null, supportPhone: null, supportEmail: null, hostname: null };
    expect((await request.put(`${API}/api/v1/admin/portal-identity`, { headers: officer, data: body })).status()).toBe(403);
    const other = scopeHeaders(newTenant());
    const taken = `shared-${Math.random().toString(36).slice(2, 8)}`;
    expect((await request.put(`${API}/api/v1/admin/portal-identity`, { headers: other, data: { ...body, slug: taken, displayName: "First" } })).status()).toBe(200);
    expect((await request.put(`${API}/api/v1/admin/portal-identity`, { headers: scopeHeaders(newTenant()), data: { ...body, slug: taken, displayName: "Second" } })).status()).toBe(409);
  });
});

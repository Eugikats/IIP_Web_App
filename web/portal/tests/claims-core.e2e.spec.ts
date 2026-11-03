import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
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
export async function claimantLinkFor(request: APIRequestContext, claimType = "Accident") {
  const policyNumber = `POL-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;
  const email = `claimant.${Math.random().toString(36).slice(2, 8)}@example.com`;
  const created = await request.post(`${API}/api/v1/cases`, {
    headers: scopeHeaders(PORTAL_TENANT, PORTAL_WORKSPACE),
    data: {
      customerName: "Portal Customer",
      customerEmail: email,
      customerPhone: "+256700000777",
      policyNumber,
      productLine: "Motor",
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

export async function openClaimantPortal(page: Page, token: string) {
  await page.goto(`/access/verify?token=${token}`);
  await expect(page.getByText("Your claim", { exact: true })).toBeVisible();
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
  test("a file that fails to read is marked Could not be read, can be retried, then counts", async ({ page, request }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const { token } = await claimantLinkFor(request);
    await openClaimantPortal(page, token);

    const policeRow = page.locator("li", { hasText: "Police abstract" }).first();
    await policeRow.locator('input[type="file"]').setInputFiles({
      name: "police.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.from("IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ONCE"),
    });
    await expect(policeRow.getByText("Could not be read")).toBeVisible();
    const documentProgress = page.getByText("Documents we asked for").locator("..");
    await expect(documentProgress).toContainText("0 of 3 sent");

    await policeRow.getByRole("button", { name: "Try again" }).click();
    await expect(policeRow.getByText("Received")).toBeVisible();
    await expect(policeRow.getByRole("button", { name: "Try again" })).toHaveCount(0);
    await expect(documentProgress).toContainText("1 of 3 sent");
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
    await photos.locator('input[type="file"]').setInputFiles([damage, { ...damage, name: "damage_photo_2.jpg" }]);
    await expect(photos.getByText("2 of 10 sent")).toBeVisible();
    await expect(photos.getByRole("button", { name: /Remove/ })).toBeVisible();
    await photos.locator('input[type="file"]').setInputFiles([{ ...damage, name: "damage_photo_3.jpg" }]);
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

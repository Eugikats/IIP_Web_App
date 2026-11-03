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
export async function claimantLinkFor(request: APIRequestContext, claimType = "Own damage") {
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

    const policeRow = page.locator("li", { hasText: "Police report" }).first();
    await policeRow.locator('input[type="file"]').setInputFiles({
      name: "police.txt",
      mimeType: "text/plain",
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

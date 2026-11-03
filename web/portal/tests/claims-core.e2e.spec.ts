import { expect, test, type APIRequestContext } from "@playwright/test";
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

import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
const user = `spec-mode-${Date.now()}`;
const orgs: Record<string, { organisationId: string; workspaceId: string }> = {};

async function activate(request: import("@playwright/test").APIRequestContext, scenario: string) {
  const response = await request.post(`${API}/api/v1/demo/scenarios/${scenario}/activate`, {
    headers: { "x-demo-role": "Admin", "X-User-Id": user, "Content-Type": "application/json", "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" },
  });
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  orgs[scenario] = { organisationId: body.organisationId, workspaceId: body.workspaceId };
}

test.beforeAll(async ({ request }) => {
  await activate(request, "mavuno-bank");
  await activate(request, "nile-life");
});

async function enter(page: import("@playwright/test").Page, scenario: string) {
  await page.addInitScript(([org, workspace]) => {
    window.localStorage.setItem("iip.active-organisation-id", org);
    window.localStorage.setItem("iip.active-workspace-id", workspace);
  }, [orgs[scenario].organisationId, orgs[scenario].workspaceId]);
}

test("a bank sees bank customers and bank events, an insurer does not", async ({ page }) => {
  await enter(page, "mavuno-bank");
  await page.goto("/app/acquisition");
  await expect(page.getByRole("tab", { name: "Bank events" })).toBeVisible();
  await expect(page.getByText("Bank customers", { exact: true })).toBeVisible();
});

test("an insurer's acquisition dashboard drops the bank items", async ({ page }) => {
  await enter(page, "nile-life");
  await page.goto("/app/acquisition");
  await expect(page.getByRole("tab", { name: "Prospects" })).toBeVisible();
  await expect(page.getByRole("tab", { name: "Bank events" })).toHaveCount(0);
  await expect(page.getByText("Bank customers", { exact: true })).toHaveCount(0);
  await page.screenshot({ path: `test-results/workspace/acquisition-insurer-${test.info().project.name}.png` });
});

test("the claims list fits the screen without sideways scrolling", async ({ page }) => {
  // The seeded default workspace has claims, so this measures a real table, not a loading skeleton.
  await page.goto("/app/claims");
  await expect(page.getByRole("heading", { name: "Work Queue" })).toBeVisible();
  await expect(page.getByText(/Showing \d+ of \d+ claims/)).toBeVisible({ timeout: 60000 });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  // Nothing inside the claims table or its card may scroll sideways, however deeply it is nested.
  const hidden = await page.evaluate(() => {
    const root = document.querySelector('[data-testid="claims-desktop-table"]') ?? document.querySelector('[data-testid="claims-tablet-table"]');
    if (!root) return [] as string[];
    const scrollers = [root as HTMLElement, ...Array.from(root.querySelectorAll<HTMLElement>("*"))];
    let node: HTMLElement | null = root.parentElement;
    while (node) { scrollers.push(node); node = node.parentElement; }
    return scrollers
      .filter((element) => element.scrollWidth - element.clientWidth > 1 && getComputedStyle(element).display !== "inline" && !String(element.className).includes("truncate"))
      .map((element) => `${element.tagName.toLowerCase()}.${String(element.className).slice(0, 60)} ${element.scrollWidth}/${element.clientWidth}`);
  });
  expect(hidden).toEqual([]);
  await page.screenshot({ path: `test-results/workspace/claims-list-${test.info().project.name}.png`, fullPage: false });
});

test("a claim shows what was reported, the amount and the cover at a glance", async ({ page, request }) => {
  // The seeded default workspace has claims; a non-motor one proves the screen no longer assumes a motor claim.
  const headers = { "x-demo-role": "Admin", "X-User-Id": user, "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" };
  const claims = (await (await request.get(`${API}/api/v1/claims`, { headers })).json()) as { claimId: string; productLine: string }[];
  const claim = claims.find((item) => item.productLine !== "Motor") ?? claims[0];
  expect(claim).toBeTruthy();
  await page.goto(`/app/claims/${claim.claimId}`);
  const glance = page.getByRole("region", { name: "Claim at a glance" });
  await expect(glance).toBeVisible();
  await expect(glance.getByText("Claimed", { exact: true })).toBeVisible();
  await expect(glance.getByText("Cover", { exact: true })).toBeVisible();
  await expect(page.getByText("What was reported", { exact: true })).toBeVisible();
  await expect(page.getByText("Motor FNOL details")).toHaveCount(0);
  await page.screenshot({ path: `test-results/workspace/claim-rich-${test.info().project.name}.png`, fullPage: true });
});

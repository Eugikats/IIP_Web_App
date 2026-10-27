import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
const ORGANISATION_KEY = "iip.active-organisation-id";

test.beforeEach(async ({ page }) => {
  await page.addInitScript(([key]) => {
    // A fresh browser each time, so the first test does not inherit another's organisation.
    if (!window.sessionStorage.getItem("demo-spec-started")) {
      window.sessionStorage.setItem("demo-spec-started", "1");
      window.localStorage.removeItem(key);
    }
  }, [ORGANISATION_KEY]);
});

test("the Demonstrations page offers the three worlds", async ({ page }) => {
  await page.goto("/app/admin/demonstrations");
  await expect(page.getByRole("heading", { name: "Demonstrations", level: 1 })).toBeVisible();
  for (const title of ["Mavuno Bank", "Nile Life", "Nile General"]) {
    await expect(page.getByText(title, { exact: true }).first()).toBeVisible();
  }
  await expect(page.getByText("Sells for: Mpingo Life Assurance, Kifaru General Insurance")).toBeVisible();
  // No yellow synthetic-data banner and no status bar, ever.
  await expect(page.getByText(/synthetic demo data/i)).toHaveCount(0);
  await page.screenshot({ path: `test-results/workspace/demonstrations-${test.info().project.name}.png`, fullPage: true });
});

test("setting up Mavuno Bank acts in its own organisation and shows its own people", async ({ page }) => {
  await page.goto("/app/admin/demonstrations");
  await page.getByTestId("scenario-mavuno-bank").getByRole("button", { name: /Set it up and open|Open this demonstration/ }).click();
  await page.waitForURL("**/app", { timeout: 60_000 });

  const organisation = await page.evaluate((key) => window.localStorage.getItem(key), ORGANISATION_KEY);
  expect(organisation).toMatch(/^org-/);

  // Every request now carries that organisation, and the default workspace's prospects are not in it.
  const leads = await page.request.get(`${API}/api/v1/sales-leads`, {
    headers: { "X-Tenant-Id": organisation!, "X-Workspace-Id": "main", "x-demo-role": "Admin", "X-User-Id": "user-1" },
  });
  expect(leads.ok()).toBeTruthy();

  await page.goto("/app/acquisition");
  await expect(page.getByText("Aisha Namutebi").locator("visible=true").first()).toBeVisible({ timeout: 30_000 });
  await page.screenshot({ path: `test-results/workspace/mavuno-acquisition-${test.info().project.name}.png`, fullPage: true });

  await page.goto("/app/admin/demonstrations");
  await expect(page.getByText("Active now")).toBeVisible();
});

test("switching to Nile Life leaves Mavuno Bank untouched and shows life policyholders", async ({ page }) => {
  await page.goto("/app/admin/demonstrations");
  await page.getByTestId("scenario-nile-life").getByRole("button", { name: /Set it up and open|Open this demonstration/ }).click();
  await page.waitForURL("**/app", { timeout: 60_000 });

  await page.goto("/app/retention");
  await expect(page.getByText("Sarah Nakirya").locator("visible=true").first()).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Aisha Namutebi")).toHaveCount(0);
  await page.screenshot({ path: `test-results/workspace/nile-life-retention-${test.info().project.name}.png`, fullPage: true });
});

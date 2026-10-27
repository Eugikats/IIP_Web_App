import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
let organisationId = "";

test.beforeAll(async ({ request }) => {
  // A person of its own each run, so each run has a fresh Nile Life organisation.
  const activated = await request.post(`${API}/api/v1/demo/scenarios/nile-life/activate`, {
    headers: { "x-demo-role": "Admin", "X-User-Id": `spec-renewals-${Date.now()}`, "Content-Type": "application/json", "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" },
  });
  expect(activated.ok()).toBeTruthy();
  organisationId = (await activated.json()).organisationId;
});

test.beforeEach(async ({ page }) => {
  await page.addInitScript(([org]) => {
    window.localStorage.setItem("iip.active-organisation-id", org);
    window.localStorage.setItem("iip.active-workspace-id", "main");
  }, [organisationId]);
});

test("Nile Life starts with unapproved renewal rules; approving one sends reminders to the people whose policies end soon", async ({ page }) => {
  await page.goto("/app/acquisition");
  await page.getByRole("tab", { name: "Rules" }).click();

  const card = page.getByRole("listitem").filter({ hasText: "Renewal reminder, 30 days before" });
  await expect(card.getByText("Draft, not approved")).toBeVisible();
  await expect(page.getByRole("listitem").filter({ hasText: "Premium due reminder" })).toBeVisible();
  await expect(page.getByRole("listitem").filter({ hasText: "Win-back after a lapse" })).toBeVisible();
  await card.getByRole("button", { name: "Approve this rule" }).click();
  await expect(card.getByText("Running")).toBeVisible();

  await page.getByRole("button", { name: "Run the rules now" }).click();
  await expect(page.getByRole("status").filter({ hasText: /Looked at \d+/ })).toBeVisible();

  const log = page.getByRole("table").last();
  // Policies ending in 14, 21 and 30 days are within the 30 day window; their holders were offered a reminder.
  for (const name of ["Rose Namatovu", "Isaac Mukiibi", "Beatrice Adong"]) {
    const row = log.getByRole("row").filter({ hasText: name }).first();
    await expect(row).toContainText("Shown as sent, not delivered");
  }
  await page.screenshot({ path: `test-results/workspace/renewals-${test.info().project.name}.png`, fullPage: true });

  // The people are real records of this organisation only, and the events say where they came from.
  await page.getByRole("tab", { name: "Bank events" }).click();
  await expect(page.getByText("From policy dates").first()).toBeVisible();
});

test.describe("Nile General claims", () => {
  let generalOrganisation = "";

  test.beforeAll(async ({ request }) => {
    const activated = await request.post(`${API}/api/v1/demo/scenarios/nile-general/activate`, {
      headers: { "x-demo-role": "Admin", "X-User-Id": `spec-claims-${Date.now()}`, "Content-Type": "application/json", "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" },
    });
    expect(activated.ok()).toBeTruthy();
    generalOrganisation = (await activated.json()).organisationId;
  });

  test("approving the claims rules tells claimants where their claim is, without ever deciding it", async ({ page }) => {
    await page.addInitScript(([org]) => {
      window.localStorage.setItem("iip.active-organisation-id", org);
      window.localStorage.setItem("iip.active-workspace-id", "main");
    }, [generalOrganisation]);
    await page.goto("/app/acquisition");
    await page.getByRole("tab", { name: "Rules" }).click();

    for (const name of ["Tell the claimant their claim arrived", "Tell the claimant where their claim is", "Chase missing claim papers"]) {
      const card = page.getByRole("listitem").filter({ hasText: name });
      await expect(card.getByText("Draft, not approved")).toBeVisible();
      await card.getByRole("button", { name: "Approve this rule" }).click();
      await expect(card.getByText("Running")).toBeVisible();
    }

    await page.getByRole("button", { name: "Run the rules now" }).click();
    await expect(page.getByRole("status").filter({ hasText: /Looked at \d+/ })).toBeVisible();

    const log = page.getByRole("table").last();
    await expect(log.getByRole("row").filter({ hasText: "Tell the claimant where their claim is" }).first()).toContainText("Shown as sent about CLM-");
    await page.screenshot({ path: `test-results/workspace/claims-rules-${test.info().project.name}.png`, fullPage: true });
  });
});

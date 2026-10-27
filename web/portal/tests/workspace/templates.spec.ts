import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
const HEADERS = {
  "X-Tenant-Id": "tenant-demo",
  "X-Workspace-Id": "workspace-operations",
  "x-demo-role": "Admin",
  "X-User-Id": "user-1",
  "Content-Type": "application/json",
};

// A name of its own for this run, so a second run does not meet the first one's approved template.
const NAME = `spec_${Date.now().toString(36)}`;

test.beforeAll(async ({ request }) => {
  const response = await request.put(`${API}/api/v1/templates/${NAME}/en`, {
    headers: HEADERS,
    data: {
      category: "Utility",
      purpose: "Browser spec",
      body: "Hello {{1}}, thank you for choosing {{2}}.",
      parameters: ["customer_name", "organisation"],
      buttons: ["Yes"],
    },
  });
  expect(response.ok()).toBeTruthy();
});

test("the templates page lists the standard drafts", async ({ page }) => {
  await page.goto("/app/admin/templates");
  await expect(page.getByRole("heading", { name: "Message templates", level: 1 })).toBeVisible();
  await expect(page.getByText("bank_cover_intro")).toBeVisible();
  await expect(page.getByText("renewal_30_days")).toBeVisible();
  await page.screenshot({ path: `test-results/workspace/templates-${test.info().project.name}.png`, fullPage: true });
});

test("a template goes from draft to submitted to approved, and the page says so", async ({ page }) => {
  await page.goto("/app/admin/templates");
  await page.getByRole("button", { name: new RegExp(NAME) }).click();

  const drawer = page.getByRole("dialog");
  await expect(drawer.getByText("Hello Eugene, thank you for choosing Mavuno Bank.")).toBeVisible();
  await expect(drawer.getByText("Draft", { exact: true })).toBeVisible();
  await page.screenshot({ path: `test-results/workspace/template-draft-${test.info().project.name}.png` });

  await drawer.getByRole("button", { name: "Mark as submitted to WhatsApp" }).click();
  await expect(drawer.getByText("Waiting for WhatsApp", { exact: true })).toBeVisible();
  await expect(drawer.getByRole("button", { name: "Edit the draft" })).toHaveCount(0);

  await drawer.getByRole("button", { name: "WhatsApp approved it" }).click();
  await expect(drawer.getByText("Approved", { exact: true })).toBeVisible();
  await expect(drawer.getByRole("button", { name: "Pause this template" })).toBeVisible();
});

test("a rejection needs a reason and brings the draft back for editing", async ({ page, request }) => {
  const name = `${NAME}_b`;
  await request.put(`${API}/api/v1/templates/${name}/en`, {
    headers: HEADERS,
    data: { category: "Marketing", purpose: "Rejection spec", body: "Hello {{1}}, big offer from {{2}}.", parameters: ["customer_name", "organisation"], buttons: [] },
  });
  await request.post(`${API}/api/v1/templates/${name}/en/submit`, { headers: HEADERS });

  await page.goto("/app/admin/templates");
  await page.getByRole("button", { name: new RegExp(`${name}$|${name} `) }).first().click();
  const drawer = page.getByRole("dialog");
  await drawer.getByRole("button", { name: "WhatsApp rejected it" }).click();
  await expect(drawer.getByRole("button", { name: "Record the rejection" })).toBeDisabled();
  await drawer.getByLabel("Why did WhatsApp reject it?").fill("It reads as promotional.");
  await drawer.getByRole("button", { name: "Record the rejection" }).click();

  await expect(drawer.getByText("Rejected", { exact: true })).toBeVisible();
  await expect(drawer.getByText(/It reads as promotional/)).toBeVisible();
  await drawer.getByRole("button", { name: "Edit the draft" }).click();
  await drawer.getByLabel("Words").fill("Hello {{1}}, here is something from {{2}}.");
  await drawer.getByRole("button", { name: "Save draft" }).click();
  await expect(drawer.getByText("Hello Eugene, here is something from Mavuno Bank.")).toBeVisible();
});

test("filtering to a status with nothing in it shows the empty-after-filter state", async ({ page }) => {
  await page.goto("/app/admin/templates");
  await page.getByLabel("Filter by status").selectOption("Paused");
  await expect(page.getByText("No items match these filters.")).toBeVisible();
  await page.getByRole("button", { name: "Clear filters" }).click();
  await expect(page.getByText("bank_cover_intro")).toBeVisible();
});

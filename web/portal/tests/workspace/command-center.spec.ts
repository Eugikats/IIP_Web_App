import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
const HEADERS = {
  "X-Tenant-Id": "tenant-demo",
  "X-Workspace-Id": "workspace-operations",
  "x-demo-role": "Admin",
  "X-User-Id": "user-1",
  "Content-Type": "application/json",
};

test.beforeAll(async ({ request }) => {
  // A few prospects created today, so the counts and the chart have something real to show.
  for (let n = 1; n <= 3; n++) {
    await request.post(`${API}/api/v1/sales-leads`, {
      headers: HEADERS,
      data: { customerName: `Workspace Spec ${n}`, customerNeed: "Boda cover", contactInfo: `+25678100000${n}`, leadSource: "WhatsApp" },
    });
  }
});

test("Command Center answers a counting question with figures and a button to the right page", async ({ page }) => {
  await page.goto("/app");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await page.getByLabel("Ask the Command Center").fill("How many customers were acquired today?");
  await page.getByRole("button", { name: "Send message" }).click();

  await expect(page.getByRole("main").getByText("Prospects today")).toBeVisible({ timeout: 30_000 });
  const link = page.getByRole("link", { name: "Open the acquisition dashboard" });
  await expect(link).toHaveAttribute("href", "/app/acquisition");
});

test("Command Center draws a chart and exports it as a picture, a PDF and an Excel file", async ({ page }) => {
  await page.goto("/app");
  await page.getByLabel("Ask the Command Center").fill("Chart the % increase in new prospects over the past week");
  await page.getByRole("button", { name: "Send message" }).click();
  await expect(page.getByRole("main").getByText(/Prospects per day/)).toBeVisible({ timeout: 30_000 });

  for (const [item, extension] of [[/Picture/, "png"], ["PDF", "pdf"], ["Excel", "xlsx"]] as const) {
    await page.getByRole("button", { name: /^Export/ }).click();
    const [download] = await Promise.all([page.waitForEvent("download"), page.getByRole("menuitem", { name: item }).click()]);
    expect(download.suggestedFilename()).toMatch(new RegExp(`\\.${extension}$`));
  }
});

test("Settings opens as a window holding the settings screens, not as a page", async ({ page }) => {
  await page.goto("/app");
  await page.getByRole("button", { name: "Settings", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Settings" });
  await expect(dialog).toBeVisible();
  for (const section of ["Integrations", "Agent rules", "Team and access", "Products", "Usage and billing"]) {
    await expect(dialog.getByRole("button", { name: section }).first()).toBeVisible();
  }
  await dialog.getByRole("button", { name: "Integrations" }).first().click();
  await expect(dialog.getByRole("heading", { name: "Google Drive" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  expect(new URL(page.url()).pathname).toBe("/app");
});

test("The sidebar keeps to the essentials and every other screen is one step away", async ({ page }) => {
  await page.goto("/app");
  const nav = page.getByRole("complementary", { name: "Primary navigation" });
  for (const label of ["Command Center", "Claims", "Acquisition", "Inbox", "Data Hub"]) {
    await expect(nav.getByRole("link", { name: label })).toBeVisible();
  }
  await expect(nav.getByRole("link", { name: "Underwriting" })).toHaveCount(0);
  await nav.getByRole("button", { name: /Open profile menu/ }).click();
  await expect(page.getByRole("menuitem", { name: "Underwriting" })).toBeVisible();
});

test("A customer list in CSV or Excel is read, matched and imported, and the skipped rows are explained", async ({ page }) => {
  await page.goto("/app/acquisition/import");
  // Fresh numbers each run: a person already on record is (rightly) left alone.
  const stamp = String(Date.now()).slice(-6);
  const csv = [
    "Full name,Mobile,Work,Agreed to marketing",
    `Spec Import One,078${stamp}01,Salon owner,Yes`,
    `Spec Import Two,078${stamp}02,Boda rider,No`,
    ",0782000003,No name,",
  ].join("\n");
  await page.setInputFiles('input[type="file"]', { name: "spec-list.csv", mimeType: "text/csv", buffer: Buffer.from(csv) });
  await expect(page.getByText("Match the columns")).toBeVisible();
  await page.getByRole("button", { name: /^Import 3 people/ }).click();
  await expect(page.getByText("2 people added from spec-list")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/1 agreed to be contacted/)).toBeVisible();
  await expect(page.getByText(/1 row could not be read/)).toBeVisible();
});

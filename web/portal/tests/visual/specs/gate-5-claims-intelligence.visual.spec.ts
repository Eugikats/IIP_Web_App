import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 5 Claims Intelligence queue exposes approved controls and backend action state", async ({ page }, testInfo) => {
  await page.goto("/claims");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Claims Intelligence" })).toBeVisible();
  await expect(page.getByRole("tab", { name: "All Claims" })).toBeVisible();
  await expect(page.getByRole("tab", { name: "My Queue" })).toBeVisible();
  await expect(page.getByRole("tab", { name: "SLA Risk" })).toBeVisible();
  await expect(page.getByRole("tab", { name: "AI Recommended Review" })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Search", exact: true })).toBeVisible();
  await expect(page.getByLabel("Sort")).toHaveValue("SLA Risk");
  await expect(page.getByRole("table", { name: "Claims Intelligence work queue" })).toBeVisible();
  await expect(page.getByText("MISSING_REQUIRED_DOCUMENTS")).toHaveCount(0);
  await expect(page.getByText("Police report and damage photographs are required before review can continue.")).toBeVisible();

  await testInfo.attach(`gate-5-claims-intelligence-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 5 Claims Intelligence search, queue tabs and preview drawer are operable", async ({ page }) => {
  await page.goto("/claims");
  await page.waitForLoadState("networkidle");

  await page.getByRole("textbox", { name: "Search", exact: true }).fill("Daniel");
  await expect(page.getByText("Daniel K.")).toBeVisible();
  await expect(page.getByText("Amina T.")).toHaveCount(0);

  await page.getByRole("textbox", { name: "Search", exact: true }).fill("");
  await page.getByRole("tab", { name: "SLA Risk" }).click();
  await expect(page.getByText("Amina T.")).toBeVisible();

  await page.getByRole("button", { name: "Preview" }).first().click();
  await expect(page.getByRole("heading", { name: /Amina T.|Sarah N.|Daniel K./ })).toBeVisible();
  await expect(page.getByText("Missing evidence")).toBeVisible();
  await expect(page.getByText("Next recommended action")).toBeVisible();
  await expect(page.getByRole("link", { name: "Open full case" })).toBeVisible();
});

test("Gate 5 Claims Intelligence deployed profile shows no simulation controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/claims");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/test ingestion/i)).toHaveCount(0);
  await expect(page.getByText(/simulated send/i)).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
});

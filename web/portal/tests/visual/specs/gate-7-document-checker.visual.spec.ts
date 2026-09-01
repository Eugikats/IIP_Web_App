import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const documentPath = "/app/claims/CLM-2026-00491/extraction?documentId=DOC-2026-00215";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 7 Document Checker exposes adaptive verification workspace and blocked extraction state", async ({ page }, testInfo) => {
  await page.goto(documentPath);
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Extraction Review · Garage Estimate" })).toBeVisible();
  await expect(page.getByText("Release 1 · Document-to-Decision", { exact: true })).toBeVisible();
  await expect(page.getByText("repair_estimate.pdf").first()).toBeVisible();
  await expect(page.getByText("Extracted fields · 2")).toBeVisible();
  await expect(page.getByText("Amount", { exact: true })).toBeVisible();
  await expect(page.getByText("Confidence · Low · 63%").first()).toBeVisible();
  await expect(page.getByText("Garage Name")).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve extraction" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Save corrections" })).toBeDisabled();
  if ((page.viewportSize()?.width ?? 1440) < 1280) {
    await page.getByRole("tab", { name: "Source document" }).click();
  }
  await expect(page.getByAltText("Authorized preview of repair_estimate.pdf")).toBeVisible();

  await testInfo.attach(`gate-7-document-checker-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 7 Document Checker requires a human audit reason for correction or approval", async ({ page }) => {
  await page.goto(documentPath);
  await page.waitForLoadState("networkidle");

  await page.getByLabel("Review reason").fill("Verified the garage estimate against the authorized source preview.");
  await expect(page.getByRole("button", { name: "Approve extraction" })).toBeEnabled();
  await page.locator('input[value="UGX 18,500,000"]').fill("UGX 18,250,000");
  await expect(page.getByRole("button", { name: "Approve extraction" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Save corrections" })).toBeEnabled();
});

test("Gate 7 Document Checker deployed profile shows no fake processing success @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto(documentPath);
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
});

import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const documentPath = "/claims/CLM-2026-00491/documents";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 7 Document Checker exposes split verification workspace and blocked extraction state", async ({ page }, testInfo) => {
  await page.goto(documentPath);
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Claim document workspace" })).toBeVisible();
  await expect(page.getByText("Completeness checklist")).toBeVisible();
  await expect(page.getByText("Documents received")).toBeVisible();
  await expect(page.getByText("Extraction review")).toBeVisible();

  await page.getByRole("button", { name: /repair_estimate\.pdf/ }).click();
  await expect(page.getByText("Needs Human Verification")).toBeVisible();
  await expect(page.getByText("Extraction blocked actions")).toBeVisible();
  await expect(page.getByText("Human verification is required before extraction approval.")).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Human verification reason" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve extraction" })).toBeDisabled();

  await testInfo.attach(`gate-7-document-checker-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 7 Document Checker requires human verification reason before approval action is enabled", async ({ page }) => {
  await page.goto(documentPath);
  await page.waitForLoadState("networkidle");

  await page.getByRole("button", { name: /repair_estimate\.pdf/ }).click();
  await page.getByRole("textbox", { name: "Human verification reason" }).fill("Verified estimate amount and invoice details against the uploaded document.");
  await expect(page.getByRole("button", { name: "Approve extraction" })).toBeEnabled();
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

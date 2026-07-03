import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 11 Command Center summarizes implemented workflow state", async ({ page }, testInfo) => {
  await page.goto("/");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Command Center" })).toBeVisible();
  await expect(page.getByText(/Chat-first operational workspace for .+\./)).toBeVisible();
  await expect(page.getByRole("heading", { name: /Good morning,/ })).toBeVisible();
  await expect(page.getByText("Open claims")).toBeVisible();
  await expect(page.getByText("SLA at risk")).toBeVisible();
  await expect(page.getByText("Daily operational briefing")).toBeVisible();
  await expect(page.getByRole("link", { name: /SLA pressure/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Missing evidence/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Human review/ })).toBeVisible();
  await expect(page.getByText("Command Center prepares operational work.")).toBeVisible();
  await expect(page.getByText("Provider And Evidence Context")).toBeVisible();
  await expect(page.getByText("Owner Screens")).toBeVisible();
  await expect(page.getByText("Human Boundary")).toBeVisible();
  await expect(page.getByText("Not configured").first()).toBeVisible();

  await testInfo.attach(`gate-11-command-center-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 11 deployed profile exposes no decorative or autonomous command states @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/Auto Decisions/i)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByText(/mock/i)).toHaveCount(0);
  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByText(/placeholder/i)).toHaveCount(0);
  await expect(page.getByText(/local test/i)).toHaveCount(0);
});

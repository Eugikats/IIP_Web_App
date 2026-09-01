import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 11 Command Center summarizes implemented workflow state", async ({ page }, testInfo) => {
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: /Good morning,/ })).toBeVisible();
  await expect(page.getByRole("main").getByText("IIP Operations · Command Center", { exact: true })).toBeVisible();
  await expect(page.getByText("Ask about claims, documents, SLA, approvals, or missing information.")).toBeVisible();
  if ((page.viewportSize()?.width ?? 0) >= 640) {
    await expect(page.getByRole("main").getByText("Open claims")).toBeVisible();
    await expect(page.getByRole("main").getByText("SLA at risk")).toBeVisible();
    await expect(page.getByRole("main").getByText("Approvals pending")).toBeVisible();
  }
  await expect(page.getByRole("button", { name: "Show claims that need attention." })).toBeVisible();
  await expect(page.getByRole("button", { name: "Which claims are missing documents?" })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Ask IIP anything — claims, documents, customers, policies, workflows…" })).toBeVisible();
  await expect(page.getByText("IIP Operations assists — it never settles, denies, repudiates, pays, or accuses fraud.")).toBeVisible();

  const viewportWidth = page.viewportSize()?.width ?? 0;
  if (viewportWidth >= 1280) {
    await expect(page.getByText("Provider health")).toBeVisible();
  } else {
    await expect(page.getByText(/\d+ of \d+ providers need attention/)).toBeVisible();
  }

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

  await page.goto("/app");
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

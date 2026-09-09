import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 11 Command Center summarizes implemented workflow state", async ({ page }, testInfo) => {
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  // Navigation model §6.2 — the chat-first landing is exactly four elements (orb, greeting,
  // subtext, suggestion chips); there is no stat row in the hero itself.
  await expect(page.getByRole("heading", { name: /Good morning,/ })).toBeVisible();
  await expect(page.getByRole("main").getByText("How can I help with your work today?")).toBeVisible();
  await expect(page.getByRole("button", { name: "Which claims are waiting on documents?" })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Message IIP Operations" })).toBeVisible();
  // The chat composer disclaimer is shared by the Assistant landing and the right-rail Copilot
  // (design language v2 §8.17) — both render at desktop widths, so scope to main to disambiguate.
  await expect(page.getByRole("main").getByText("IIP can make mistakes and does not make claim decisions. Verify before acting.")).toBeVisible();

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

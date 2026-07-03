import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 1 shell uses the approved light-only MVP foundation", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "desktop-chromium", "Shell dimension assertions are desktop-only.");

  await page.goto("/");
  await page.waitForLoadState("networkidle");

  await expect(page.locator("html")).not.toHaveClass(/dark/);
  await expect(page.getByText("IIP Operations")).toBeVisible();
  await expect(page.getByText("Insurance AI Operating System")).toBeVisible();
  await expect(page.getByRole("link", { name: /AI Conversations/i })).toBeVisible();
  await expect(page.getByRole("link", { name: /New Claim Intake/i })).toBeVisible();

  const headerHeight = await page.locator("header").evaluate((element) => element.getBoundingClientRect().height);
  expect(Math.round(headerHeight)).toBe(64);

  const sidebar = page.locator("aside").first();
  const expandedWidth = await sidebar.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(expandedWidth)).toBe(280);

  const rightRail = page.locator("aside").nth(1);
  const railWidth = await rightRail.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(railWidth)).toBe(400);

  await page.getByRole("button", { name: "Collapse sidebar" }).click();
  await expect(sidebar).toHaveCSS("width", "72px");
});

test("Gate 1 shell exposes no delivered dark or system theme toggle", async ({ page }) => {
  await page.goto("/");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /theme/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /dark/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /system/i })).toHaveCount(0);
});

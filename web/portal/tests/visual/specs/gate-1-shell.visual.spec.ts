import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 1 shell uses the approved light-only MVP foundation", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "desktop-chromium", "Shell dimension assertions are desktop-only.");

  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.locator("html")).not.toHaveClass(/dark/);
  await expect(page.getByText("Insurance Intelligence Platform")).toBeVisible();
  await expect(page.getByText("Platform · IIP")).toBeVisible();
  await expect(page.getByRole("link", { name: /Command Center/i })).toBeVisible();
  await expect(page.getByRole("link", { name: "Claims", exact: true })).toBeVisible();

  const headerHeight = await page.locator("header").evaluate((element) => element.getBoundingClientRect().height);
  expect(Math.round(headerHeight)).toBe(64);

  const sidebar = page.locator("aside").first();
  const expandedWidth = await sidebar.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(expandedWidth)).toBe(264);

  const rightRail = page.locator("aside").nth(1);
  const railWidth = await rightRail.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(railWidth)).toBe(340);

  await page.getByRole("complementary").filter({ hasText: "Insurance Intelligence Platform" }).getByRole("button", { name: "Collapse sidebar navigation" }).click();
  await expect(page.getByRole("button", { name: "Open navigation" })).toBeVisible();
});

test("Gate 1 shell exposes no delivered dark or system theme toggle", async ({ page }) => {
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /theme/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /dark/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /system/i })).toHaveCount(0);
});

test("Gate 1 mobile shell keeps the workspace visible and uses an off-canvas sidebar", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mobile-chromium", "Mobile shell assertions run only on the mobile project.");

  const consoleErrors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") consoleErrors.push(message.text());
  });

  await page.goto("/app/integrations");
  await page.waitForLoadState("networkidle");

  const openSidebar = page.getByRole("button", { name: "Open navigation" });
  await expect(openSidebar).toBeVisible();

  const geometry = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    mainLeft: document.querySelector("main")?.getBoundingClientRect().left ?? -1,
  }));
  expect(geometry.scrollWidth).toBeLessThanOrEqual(geometry.clientWidth);
  expect(geometry.mainLeft).toBeGreaterThanOrEqual(0);
  expect(geometry.mainLeft).toBeLessThanOrEqual(1);

  await openSidebar.click();
  const drawer = page.getByRole("dialog", { name: "Primary navigation" });
  await expect(drawer).toHaveAttribute("aria-modal", "true");
  const sidebar = page.getByRole("complementary").filter({ hasText: "Insurance Intelligence Platform" });
  await expect(sidebar).toBeVisible();
  await expect(sidebar.getByRole("button", { name: "Close navigation" })).toBeFocused();
  expect(await page.locator("main").evaluate((main) => main.closest("[inert]") !== null)).toBe(true);
  await page.keyboard.press("Shift+Tab");
  expect(await page.evaluate(() => document.activeElement?.closest('[role="dialog"]')?.getAttribute("aria-label"))).toBe("Primary navigation");
  await page.keyboard.press("Escape");
  await expect(openSidebar).toBeVisible();
  await expect(openSidebar).toBeFocused();

  await openSidebar.click();
  await sidebar.getByRole("link", { name: "Claims", exact: true }).click();
  await expect(page).toHaveURL(/\/app\/claims$/);
  await expect(openSidebar).toBeVisible();
  expect(consoleErrors.filter((message) => message.includes("Hydration failed"))).toEqual([]);
});

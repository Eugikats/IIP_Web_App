import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

// A new person starts with the compact 64px icon rail; expansion is an explicit saved choice.
test("Gate 1 sidebar opens as the compact icon rail by default", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "desktop-chromium", "Shell dimension assertions are desktop-only.");

  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  const sidebar = page.locator("aside").first();
  expect(Math.round(await sidebar.evaluate((element) => element.getBoundingClientRect().width))).toBe(64);
});

test("Gate 1 shell uses the approved light-only MVP foundation", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "desktop-chromium", "Shell dimension assertions are desktop-only.");

  await page.addInitScript(() => window.localStorage.setItem("iip.sidebar.expanded", "false"));
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.locator("html")).not.toHaveClass(/dark/);
  // Navigation model §5.2, the rail is icon-only; "Insurance Intelligence Platform", "Brief"
  // and "Claims" are accessible names (aria-label/title), not visible text.
  await expect(page.getByRole("link", { name: "Insurance Intelligence Platform" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Brief" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Claims", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: /Good (morning|afternoon|evening),/ })).toBeVisible();

  // Design language v2 §3, no fixed top bar; a page's own breadcrumb/h1 lives in its content
  // column, so there is no <header> element to measure.
  const sidebar = page.locator("aside").first();
  const sidebarWidth = await sidebar.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(sidebarWidth)).toBe(64);

  const rightRail = page.locator("aside").nth(1);
  const railWidth = await rightRail.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(railWidth)).toBe(339);
});

test("Gate 1 shell exposes no delivered dark or system theme toggle", async ({ page }) => {
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /theme/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /dark/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /system/i })).toHaveCount(0);
});

test("Gate 1 mobile shell keeps the icon rail present and the workspace navigable", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mobile-chromium", "Mobile shell assertions run only on the mobile project.");

  const consoleErrors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") consoleErrors.push(message.text());
  });

  // A new person's sidebar opens expanded (deliberate, D-043); this check is about the collapsed rail.
  await page.addInitScript(() => window.localStorage.setItem("iip.sidebar.expanded", "false"));
  await page.goto("/app/integrations");
  await page.waitForLoadState("networkidle");

  // Navigation model §5.1, "The rail is always present" at every viewport width; it never
  // collapses into an off-canvas drawer, so there is no open/close affordance on mobile.
  const sidebar = page.getByRole("complementary", { name: "Primary navigation" });
  await expect(sidebar).toBeVisible();
  const sidebarWidth = await sidebar.evaluate((element) => element.getBoundingClientRect().width);
  expect(Math.round(sidebarWidth)).toBe(64);

  const geometry = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    mainLeft: document.querySelector("main")?.getBoundingClientRect().left ?? -1,
  }));
  expect(geometry.scrollWidth).toBeLessThanOrEqual(geometry.clientWidth);
  expect(geometry.mainLeft).toBe(sidebarWidth);

  await sidebar.getByRole("link", { name: "Claims", exact: true }).click();
  // The dev server compiles the Claims route on first visit, which can take longer than the default wait.
  await expect(page).toHaveURL(/\/app\/claims$/, { timeout: 90_000 });
  await expect(sidebar).toBeVisible();
  expect(consoleErrors.filter((message) => message.includes("Hydration failed"))).toEqual([]);
});

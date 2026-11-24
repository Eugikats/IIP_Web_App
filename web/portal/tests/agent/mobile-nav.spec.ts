import { expect, test, type Page } from "@playwright/test";

// The shell on a phone: below 768px the 80px rail gives way to a top bar (menu, screen name, new chat) and a drawer, as
// in Claude and ChatGPT, so the page keeps the whole width. From 768px up the rail is unchanged. Runs on the desktop,
// tablet and mobile projects of playwright.agent.config.ts; the checks follow the viewport width, not the project name.

const isPhone = (page: Page) => (page.viewportSize()?.width ?? 1280) < 768;
const sideways = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

async function open(page: Page, path = "/app/claims") {
  await page.goto(path);
  await expect(page.locator("main").first()).toBeVisible({ timeout: 30_000 });
}

// The bar is in the server's HTML before the page has hydrated, so a first tap can land on nothing. Tap until it opens.
async function openMenu(page: Page) {
  await expect(async () => {
    if ((await page.getByRole("dialog", { name: "Menu" }).count()) === 0) {
      await page.getByRole("button", { name: "Open menu" }).click();
    }
    await expect(page.getByRole("dialog", { name: "Menu" })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 30_000 });
}

test("a phone gets a top bar and no rail, a wider screen keeps the rail", async ({ page }) => {
  await open(page);
  const bar = page.getByTestId("mobile-bar");
  if (isPhone(page)) {
    await expect(bar).toBeVisible();
    await expect(bar.getByText("Claims")).toBeVisible();
    await expect(page.locator("aside[aria-label='Primary navigation']")).toBeHidden();
  } else {
    await expect(bar).toBeHidden();
    await expect(page.locator("aside[aria-label='Primary navigation']")).toBeVisible();
  }
});

test("on a phone the bar's buttons are easy to press and the content column uses the whole width", async ({ page }) => {
  test.skip(!isPhone(page), "phone only");
  await open(page);
  for (const button of await page.getByTestId("mobile-bar").locator("a, button").all()) {
    const box = await button.boundingBox();
    expect(box!.height).toBeGreaterThanOrEqual(44);
    expect(box!.width).toBeGreaterThanOrEqual(44);
  }
  const main = await page.locator("main").first().boundingBox();
  expect(main!.x).toBeLessThanOrEqual(1);
  expect(main!.width).toBeGreaterThanOrEqual(page.viewportSize()!.width - 1);
  expect(await sideways(page)).toBeLessThanOrEqual(0);
});

test("on a phone the drawer holds every destination, Settings and Sign out, and Escape closes it", async ({ page }) => {
  test.skip(!isPhone(page), "phone only");
  await open(page);
  await openMenu(page);
  const drawer = page.getByRole("dialog", { name: "Menu" });
  for (const name of ["Workspace", "Claims", "Acquisition", "Automations", "Workspaces", "Reports", "Conversations"]) {
    await expect(drawer.getByRole("link", { name, exact: true })).toBeVisible();
  }
  await expect(drawer.getByRole("link", { name: "New chat" }).first()).toBeVisible();
  await expect(drawer.getByRole("button", { name: "Settings" })).toBeVisible();
  await expect(drawer.getByRole("button", { name: /Sign out|Signing out/ })).toBeVisible();
  const box = await drawer.boundingBox();
  expect(box!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  expect(await sideways(page)).toBeLessThanOrEqual(0);

  await drawer.getByRole("button", { name: "More tools" }).click();
  await expect(drawer.getByRole("link", { name: "Customers" })).toBeVisible();

  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog", { name: "Menu" })).toHaveCount(0);
});

test("on a phone Settings opens from the drawer", async ({ page }) => {
  test.skip(!isPhone(page), "phone only");
  await open(page);
  await openMenu(page);
  await page.getByRole("dialog", { name: "Menu" }).getByRole("button", { name: "Settings" }).click();
  await expect(page.getByRole("dialog", { name: "Settings" })).toBeVisible({ timeout: 15_000 });
});

test("on a phone a destination in the drawer opens and the drawer closes", async ({ page }) => {
  test.skip(!isPhone(page), "phone only");
  await open(page);
  await openMenu(page);
  await page.getByRole("dialog", { name: "Menu" }).getByRole("button", { name: "More tools" }).click();
  await page.getByRole("dialog", { name: "Menu" }).getByRole("link", { name: "Customers" }).click();
  await expect(page).toHaveURL(/\/app\/customers/);
  await expect(page.getByRole("dialog", { name: "Menu" })).toHaveCount(0);
});

test("on a phone the home screen docks the message box to the bottom of the screen", async ({ page }) => {
  test.skip(!isPhone(page), "phone only");
  await open(page, "/app/workspace");
  const box = page.getByLabel("Message Kato");
  await expect(box).toBeVisible({ timeout: 30_000 });
  const field = await box.boundingBox();
  const viewport = page.viewportSize()!;
  // The text field is in the lower part of the screen, not floating in the middle.
  expect(field!.y).toBeGreaterThan(viewport.height * 0.6);
  expect(field!.y + field!.height).toBeLessThanOrEqual(viewport.height);
  expect(await sideways(page)).toBeLessThanOrEqual(0);
  // The buttons under the text are 44px targets that stay inside the screen.
  for (const name of ["Attach a customer workbook", "Send message"]) {
    const button = await page.locator(`button[aria-label="${name}"]`).boundingBox();
    expect(button!.height).toBeGreaterThanOrEqual(44);
    expect(button!.x + button!.width).toBeLessThanOrEqual(viewport.width);
  }
});

test("on a phone no main screen scrolls sideways", async ({ page }) => {
  test.skip(!isPhone(page), "phone only");
  test.setTimeout(240_000);
  for (const path of ["/app/workspace", "/app/claims", "/app/acquisition", "/app/customers", "/app/work-items", "/app/documents", "/app/knowledge", "/app/platform/automations", "/app/channels"]) {
    await open(page, path);
    await page.waitForTimeout(1500);
    expect(await sideways(page), `${path} scrolls sideways`).toBeLessThanOrEqual(0);
  }
});

test("the message box shows focus once, on the pill, not as a second blue rectangle around the field", async ({ page }) => {
  await open(page, "/app/workspace");
  const field = page.getByRole("textbox", { name: "Message Kato" });
  await field.click();
  await expect(field).toBeFocused();
  const shadow = await field.evaluate((element) => getComputedStyle(element).boxShadow);
  expect(shadow).toBe("none");
});

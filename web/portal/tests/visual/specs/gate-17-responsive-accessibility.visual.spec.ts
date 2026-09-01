import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const claimPath = "/app/claims/CLM-2026-00491";
const extractionPath = `${claimPath}/extraction?documentId=DOC-2026-00215`;

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 17 claims queue substitutes the correct density without page overflow", async ({ page }, testInfo) => {
  await page.goto("/app/claims");
  const width = page.viewportSize()?.width ?? 1440;
  if (width < 768) {
    await expect(page.getByTestId("claims-card-list")).toBeVisible();
    await expect(page.getByTestId("claims-card-list").getByRole("link", { name: /Review/ }).first()).toBeVisible();
  } else if (width < 1280) {
    await expect(page.getByTestId("claims-tablet-table")).toBeVisible();
    await expect(page.getByTestId("claims-tablet-table").getByText("More details").first()).toBeVisible();
  } else {
    await expect(page.getByTestId("claims-desktop-table")).toBeVisible();
  }
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
});

test("Gate 17 320px claims cards preserve identity, evidence, owner, freshness and action", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mobile-chromium", "Narrow reflow assertion runs once.");
  await page.setViewportSize({ width: 320, height: 720 });
  await page.goto("/app/claims");
  const first = page.getByTestId("claim-card").first();
  await expect(first).toContainText("CLM-2026-00491");
  await expect(first).toContainText(/Evidence|documents/i);
  await expect(first).toContainText(/Owner|Unassigned|Claims/i);
  await expect(first).toContainText(/Updated/i);
  await expect(first.getByRole("link", { name: /Review/ })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
});

test("Gate 17 768px uses the tablet priority view", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "tablet-chromium", "Boundary assertion runs once.");
  await page.setViewportSize({ width: 768, height: 900 });
  await page.goto("/app/claims");
  await expect(page.getByTestId("claims-tablet-table")).toBeVisible();
  await expect(page.getByTestId("claims-desktop-table")).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
});

test("Gate 17 evidence workspace preserves source and review task across contexts", async ({ page }) => {
  await page.goto(extractionPath);
  const width = page.viewportSize()?.width ?? 1440;
  if (width >= 1280) {
    await expect(page.getByTestId("evidence-source-panel")).toBeVisible();
    await expect(page.getByTestId("evidence-review-panel")).toBeVisible();
  } else {
    const tabs = page.getByRole("tablist", { name: "Evidence workspace view" });
    await expect(tabs).toBeVisible();
    await tabs.getByRole("tab", { name: "Review fields" }).click();
    await page.getByLabel("Review reason").fill("Check translated and corrected evidence state");
    await tabs.getByRole("tab", { name: "Source document" }).click();
    await tabs.getByRole("tab", { name: "Review fields" }).click();
    await expect(page.getByLabel("Review reason")).toHaveValue("Check translated and corrected evidence state");
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
});

test("Gate 17 critical surfaces pass axe and 200% text reflow", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "desktop-chromium", "Zoom and axe matrix runs once.");
  for (const route of ["/app/claims", claimPath, extractionPath, "/app"]) {
    await page.goto(route);
    const axe = await new AxeBuilder({ page }).analyze();
    expect(axe.violations, `${route}: ${axe.violations.map((item) => item.id).join(", ")}`).toEqual([]);
    await page.evaluate(() => { document.documentElement.style.fontSize = "200%"; });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  }
});

test("Gate 17 mobile targets, long content, reduced motion and composer remain usable", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mobile-chromium", "Mobile interaction assertion runs once.");
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 320, height: 720 });
  await page.goto("/app/claims");

  await page.locator("h1").evaluate((heading) => {
    heading.textContent = "Madai ya bima ya gari yenye maelezo marefu sana kwa ukaguzi wa ushahidi";
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);

  const primaryTargets = page.getByTestId("claim-card").first().getByRole("link", { name: /Review/ });
  for (const target of await primaryTargets.all()) {
    const box = await target.boundingBox();
    expect(box?.height ?? 0).toBeGreaterThanOrEqual(44);
  }
  const motionDuration = await page.getByTestId("claim-card").first().evaluate((element) => getComputedStyle(element).transitionDuration);
  expect(["0s", "0.00001s", "1e-05s"]).toContain(motionDuration);

  await page.goto("/app");
  const composer = page.getByLabel("Message IIP Operations");
  await expect(composer).toBeVisible();
  await composer.fill("Resume my interrupted evidence review");
  const geometry = await page.evaluate(() => {
    const input = document.querySelector<HTMLTextAreaElement>('[aria-label="Message IIP Operations"]');
    const main = document.querySelector("main");
    const inputRect = input?.getBoundingClientRect();
    const mainRect = main?.getBoundingClientRect();
    return { inputBottom: inputRect?.bottom ?? Infinity, mainBottom: mainRect?.bottom ?? 0 };
  });
  expect(geometry.inputBottom).toBeLessThanOrEqual(geometry.mainBottom);
  await expect(composer).toHaveValue("Resume my interrupted evidence review");
});

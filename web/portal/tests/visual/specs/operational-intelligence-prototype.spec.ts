import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

test("Operational Intelligence prototype keeps AI preparation separate from the human outcome", async ({ page }) => {
  await page.goto("/prototype/operational-intelligence");
  await expect(page.getByRole("heading", { name: "My work" })).toBeVisible();

  await page.getByRole("button", { name: "Open CLM-SYN-004287" }).click();
  await expect(page.getByRole("heading", { name: "Evidence review" })).toBeVisible();
  await expect(page.getByText("AI-prepared finding")).toBeVisible();

  await page.getByRole("button", { name: "Prepare decision pack" }).click();
  await expect(page.getByRole("heading", { name: "Accountable human outcome" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Record simulated human outcome" })).toBeVisible();
});

test("Operational Intelligence prototype has no automated accessibility violations", async ({ page }) => {
  await page.goto("/prototype/operational-intelligence");
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations).toEqual([]);
});

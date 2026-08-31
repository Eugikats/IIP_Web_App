import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../visual/fixtures/iip-fixtures";

test("production server survives My Work to claim review and repeated back navigation", async ({ page }) => {
  await installVisualApiFixtures(page);

  await page.goto("/app/work-items");
  await expect(page.getByRole("heading", { name: "Human review workspace" })).toBeVisible();
  await page.getByRole("link", { name: "Claims", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Work Queue" })).toBeVisible();

  for (let pass = 0; pass < 3; pass += 1) {
    const reviewLink = page.getByRole("main").getByRole("link", { name: "Review" }).first();
    const reviewHref = await reviewLink.getAttribute("href");
    await Promise.all([
      page.waitForURL(`**${reviewHref}`),
      reviewLink.click(),
    ]);
    await expect(page.getByRole("heading", { name: /CLM-2026/ })).toBeVisible({ timeout: 15_000 });
    await page.goBack();
    await page.waitForURL("**/app/claims");
    await expect(page.getByRole("heading", { name: "Work Queue" })).toBeVisible();
  }
});

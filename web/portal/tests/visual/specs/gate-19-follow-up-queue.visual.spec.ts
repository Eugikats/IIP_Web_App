import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 19 requested follow-ups stay legible and cancellable across viewports", async ({ page }) => {
  await page.goto("/app/communications");

  const queue = page.getByRole("region", { name: "Requested follow-ups" });
  await expect(queue.getByText("Automatic follow-up dispatch is not configured.")).toBeVisible();
  await expect(queue.getByText("Amina T.")).toBeVisible();
  await expect(queue.getByText("Pending review", { exact: true }).first()).toBeVisible();
  await expect(queue.getByText(/WhatsApp/)).toBeVisible();
  await expect(queue.getByText(/Africa\/Kampala|Nov/)).toBeVisible();

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  await queue.getByRole("button", { name: "Cancel reminder" }).click();
  const dialog = page.getByRole("dialog", { name: "Cancel customer follow-up" });
  await expect(dialog.getByText("Provider recipient and thread identifiers are not shown here.")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Cancel reminder" })).toBeDisabled();
  await dialog.getByLabel("Reason for audit trail").fill("Customer no longer needs a callback");
  await dialog.getByRole("button", { name: "Cancel reminder" }).click();

  await expect(queue.getByText("No requested follow-ups")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
});

import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { prepareVisualApi, selectDevelopmentRole } from "./fixtures";

const claimReference = "11111111-1111-4111-8111-111111111111";

test.describe("Client portal visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "Client");
  });

  test("opens a customer-safe claim view without staff provider surfaces", async ({ page }, testInfo) => {
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "Client");

    await expect(page.getByRole("heading", { name: "My Claims" })).toBeVisible();
    await expect(page.getByLabel("Provider status")).toHaveCount(0);

    await page.getByLabel("Claim reference").fill(claimReference);
    await page.getByRole("button", { name: "Open claim" }).click();

    await expect(page.getByRole("heading", { name: "Motor Claim" })).toBeVisible();
    await expect(page.getByText("Customer-safe claim help")).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Customer assistant question" })).toHaveAttribute("placeholder", "Customer assistant endpoint not configured yet");
    await expect(page.getByLabel("Upload claim documents")).toBeAttached();

    await expect(page.getByText(/fraud|reserve|internal risk|provider health|command center/i)).toHaveCount(0);
    await expect(page).toHaveScreenshot(`client-portal-${testInfo.project.name}.png`, {
      fullPage: true
    });
  });

  test("has no critical accessibility violations in the customer-safe claim view", async ({ page }) => {
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "Client");
    await expect(page.getByRole("heading", { name: "My Claims" })).toBeVisible();
    await page.getByLabel("Claim reference").fill(claimReference);
    await page.getByRole("button", { name: "Open claim" }).click();

    await expect(page.getByRole("heading", { name: "Motor Claim" })).toBeVisible();

    const results = await new AxeBuilder({ page })
      .disableRules(["color-contrast"])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});

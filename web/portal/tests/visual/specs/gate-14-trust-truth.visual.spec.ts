import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 14 operational metrics and provider health disclose source, freshness, and limits", async ({ page }) => {
  await page.goto("/app/executive");

  const operationsProvenance = page.getByRole("main").getByRole("note").filter({ hasText: "visual-operations-snapshot" });
  await expect(operationsProvenance).toContainText("Current workspace snapshot");
  await expect(operationsProvenance).toContainText("Values can change after this timestamp");
  await expect(page.getByRole("main").getByText("Open claims", { exact: true }).locator("..")).toContainText("124");

  await page.goto("/app/integrations");
  await expect(page.getByRole("heading", { name: "Integration control plane" })).toBeVisible();
  await expect(page.getByRole("main").getByRole("note")).toContainText("Current workspace snapshot");

  for (const name of ["graphMail", "azureOpenAI"]) {
    const missingProvider = page.getByRole("main").getByText(name, { exact: true }).locator("..");
    await expect(missingProvider).toContainText("Missing");
    await expect(missingProvider).not.toContainText("Operational");
  }
});

import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
  await page.goto("/app");
});

test("signed-in authority stays distinct from staff experience preview", async ({ page }) => {
  test.skip((page.viewportSize()?.width ?? 0) < 768, "Role preview is a desktop review control.");
  const preview = page.getByRole("combobox", { name: "Preview staff experience" });
  await expect(preview).toHaveValue("Admin");
  await expect(page.getByText("Signed in as Workspace Administrator").first()).toBeVisible();

  await preview.selectOption("ClaimsOfficer");
  await expect(page).toHaveURL(/\/app\/claims$/);
  await expect(page.getByText("Signed in as Workspace Administrator").first()).toBeVisible();
  await expect(page.getByText(/Previewing Claims Officer experience/)).toBeAttached();

  await preview.selectOption("Broker");
  await expect(page).toHaveURL(/\/app\/claims$/);

  await preview.selectOption("PublisherSupport");
  await expect(page).toHaveURL(/\/app$/);
  await expect(page.getByRole("link", { name: "Tenant Onboarding" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Integrations & Health" })).toHaveCount(0);
});

import { expect, test } from "@playwright/test";
import { previewLandingRoute, ROLES, STAFF_ROLES } from "../../../lib/roles";
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
  await expect(page.getByText(/Previewing Claims Officer experience/)).toBeAttached();
});

for (const role of STAFF_ROLES) {
  test(`Gate 15 ${ROLES[role].label} receives its valid preview landing without authority escalation`, async ({ page }) => {
    test.skip((page.viewportSize()?.width ?? 0) < 768, "Role preview is a desktop review control.");
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    page.on("console", (message) => {
      if (message.type() === "error") pageErrors.push(message.text());
    });
    const preview = page.getByRole("combobox", { name: "Preview staff experience" });
    if (role !== "Admin") await preview.selectOption(role);
    const route = previewLandingRoute(role).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    await expect(page).toHaveURL(new RegExp(`${route}$`));
    const signedAuthority = page.getByText("Signed in as Workspace Administrator").first();
    const clientError = page.getByRole("heading", { name: /Application error/ });
    await expect(signedAuthority.or(clientError)).toBeVisible();
    expect(pageErrors, `${ROLES[role].label} landing raised a client exception`).toEqual([]);
    await expect(signedAuthority).toBeVisible();
    await expect(preview).toHaveValue(role);
    if (role === "PublisherSupport") {
      await expect(page.getByRole("link", { name: "Tenant Onboarding" })).toBeVisible();
      await expect(page.getByRole("link", { name: "Integrations & Health" })).toHaveCount(0);
    }
  });
}

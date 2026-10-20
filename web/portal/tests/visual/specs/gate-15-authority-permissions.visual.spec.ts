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
  await expect(page.getByRole("button", { name: /Signed in as Workspace Administrator/ })).toBeVisible();
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
    const signedAuthority = page.getByRole("button", { name: /Signed in as Workspace Administrator/ });
    const clientError = page.getByRole("heading", { name: /Application error/ });
    await expect(signedAuthority.or(clientError)).toBeVisible();
    expect(pageErrors, `${ROLES[role].label} landing raised a client exception`).toEqual([]);
    await expect(signedAuthority).toBeVisible();
    await expect(preview).toHaveValue(role);
    if (role === "PublisherSupport") {
      // Navigation model §5.2, the rail holds exactly seven fixed destinations. Tenant
      // Onboarding was intentionally removed from in-app navigation for this role; it remains
      // reachable only by direct URL, not through the rail or the Admin console link.
      await expect(page.getByRole("link", { name: "Tenant Onboarding" })).toHaveCount(0);
      await expect(page.getByRole("link", { name: "Integrations & Health" })).toHaveCount(0);
    }
  });
}

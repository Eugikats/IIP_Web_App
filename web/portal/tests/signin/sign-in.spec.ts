import { expect, test } from "@playwright/test";
import { installStandIn } from "./stand-in-microsoft";

// The real sign-in code (MSAL) against a stand-in Microsoft. These exist because the sign-in page sent people back to
// itself after Microsoft returned them, and nothing in the other tests runs the real sign-in.

test("Continue with Microsoft from the sign-in page ends on the dashboard, not back on the sign-in page", async ({ page }) => {
  const seen = await installStandIn(page);

  await page.goto("/login");
  await page.getByRole("button", { name: /Continue with Microsoft/ }).click();

  await expect(page).toHaveURL(/\/app(\?|$|#)/, { timeout: 30_000 });
  await expect(page.getByText("Verifying your access")).toHaveCount(0, { timeout: 30_000 });
  await expect(page.getByRole("button", { name: "Settings", exact: true }).first()).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Sign in to IIP")).toHaveCount(0);
  expect(seen.authorizeVisits).toBe(1);
  expect(seen.apiCalls).toContain("GET /api/v1/me");
});

test("Going straight to the app while signed out signs in and arrives at the dashboard", async ({ page }) => {
  const seen = await installStandIn(page);

  await page.goto("/app");

  await expect(page.getByRole("button", { name: "Settings", exact: true }).first()).toBeVisible({ timeout: 30_000 });
  expect(seen.authorizeVisits).toBe(1);
});

test("A person who signs in but belongs to no organisation is sent to set one up, not back to the sign-in page", async ({ page }) => {
  await installStandIn(page, { memberOfNoOrganisation: true });

  await page.goto("/login");
  await page.getByRole("button", { name: /Continue with Microsoft/ }).click();

  await expect(page).toHaveURL(/\/welcome/, { timeout: 30_000 });
});

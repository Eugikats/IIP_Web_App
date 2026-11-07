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
  await expect(page.getByRole("button", { name: /Continue with Microsoft/ })).toHaveCount(0);
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

test("The sign-in page shows the Microsoft and Google marks and tells the truth about what is not set up", async ({ page }) => {
  await installStandIn(page);

  await page.goto("/login");

  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  const microsoft = page.getByRole("button", { name: /Continue with Microsoft/ });
  await expect(microsoft).toBeVisible();
  await expect(microsoft.locator("svg")).toHaveCount(1);
  const google = page.getByRole("button", { name: /Continue with Google/ });
  await expect(google).toBeVisible();
  await expect(google.locator("svg")).toHaveCount(1);
  // External ID is not configured in the test build, so Google and email are shown but cannot be pressed.
  await expect(google).toBeDisabled();
  await expect(page.getByText("Google and email sign-in are not set up for this environment.")).toBeVisible();
});

test("A person who belongs to several organisations is asked which to work in, and then gets in", async ({ page }) => {
  const seen = await installStandIn(page, { memberOfSeveralOrganisations: true });

  await page.goto("/login");
  await page.getByRole("button", { name: /Continue with Microsoft/ }).click();

  await expect(page.getByRole("heading", { name: "Choose an organisation" })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Workspace access unavailable")).toHaveCount(0);
  await page.getByRole("button", { name: /Mavuno Bank/ }).click();

  await expect(page.getByRole("button", { name: "Settings", exact: true }).first()).toBeVisible({ timeout: 30_000 });
  expect(seen.organisationsAskedFor).toContain("org-bank");
});

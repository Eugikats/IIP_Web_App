import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const diagnosticRoutes = [
  { name: "command-center", path: "/app" },
  { name: "claims-queue", path: "/app/claims" },
  { name: "case-review", path: "/app/claims/CLM-2026-00491" },
  { name: "document-checker", path: "/app/claims/CLM-2026-00491/extraction" },
  { name: "review-and-approvals", path: "/app/work-items" },
  { name: "integrations-admin", path: "/app/integrations" }
] as const;

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

for (const route of diagnosticRoutes) {
  test(`captures Gate 0.2 diagnostic screenshot for ${route.name}`, async ({ page }, testInfo) => {
    await page.goto(route.path);
    await page.waitForLoadState("networkidle");
    await expect(page.locator("body")).toBeVisible();

    await testInfo.attach(`gate-0-2-${route.name}-${testInfo.project.name}.png`, {
      body: await page.screenshot({ fullPage: true }),
      contentType: "image/png"
    });
  });
}

test("deployed-profile diagnostic shell hides local test controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText("Role")).toHaveCount(0);
  await expect(page.getByText("Switch role")).toHaveCount(0);
  await expect(page.getByText(/local test/i)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /test ingestion/i })).toHaveCount(0);
});

test("mobile diagnostic view does not expose prohibited consequential controls", async ({ page }) => {
  await page.goto("/app/work-items");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /authorise payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /final rejection/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /confirm fraud/i })).toHaveCount(0);
});

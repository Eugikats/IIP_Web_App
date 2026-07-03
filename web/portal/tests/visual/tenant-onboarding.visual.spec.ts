import { expect, test } from "@playwright/test";
import { prepareVisualApi, selectDevelopmentRole } from "./fixtures";

test.describe("Tenant onboarding visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "Admin");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "Admin");
  });

  test("renders tenant configuration from backend responses", async ({ page }, testInfo) => {
    await page.goto("/tenant-onboarding", { waitUntil: "domcontentloaded" });

    if (testInfo.project.name.includes("mobile")) {
      await expect(page.getByText("Tenant onboarding requires a desktop workspace")).toBeVisible();
    } else {
      await expect(page.getByRole("heading", { name: "Insurer setup workspace" })).toBeVisible();
      await expect(page.getByLabel("Insurer name")).toHaveValue("Visual Fixture Insurance");
      await expect(page.getByLabel("Country")).toHaveValue("Uganda");
    }

    await expect(page).toHaveScreenshot(`tenant-onboarding-${testInfo.project.name}.png`, { fullPage: true });
  });

  test("governed save excludes browser-owned authority and preserves backend configuration", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name.includes("mobile"), "Tenant-wide changes are blocked on mobile.");

    let requestBody: Record<string, unknown> | null = null;
    let auditReason: string | null = null;
    await page.route("**/api/v1/admin/tenant-configuration", async (route) => {
      if (route.request().method() === "GET") {
        await route.fallback();
        return;
      }

      requestBody = route.request().postDataJSON() as Record<string, unknown>;
      auditReason = route.request().headers()["x-audit-reason"] ?? null;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(requestBody)
      });
    });

    await page.goto("/tenant-onboarding", { waitUntil: "domcontentloaded" });
    await page.getByLabel("Insurer name").fill("Visual Fixture Insurance Updated");
    await page.getByRole("button", { name: /Review$/ }).click();
    await page.getByRole("button", { name: "Review and save" }).click();
    await page.getByLabel("Reason for audit trail").fill("Approved insurer profile update for onboarding verification.");
    await page.getByRole("button", { name: "Save tenant setup" }).click();

    await expect(page.getByText("Tenant setup saved and recorded in the audit trail.")).toBeVisible();
    expect(auditReason).toBe("Approved insurer profile update for onboarding verification.");
    expect(requestBody).toMatchObject({
      insurerName: "Visual Fixture Insurance Updated",
      approvalThresholds: [{ productLine: "Motor", amount: 5000, requiredRole: "ClaimsSupervisor" }],
      checklists: [{ productLine: "Motor", requiredDocuments: ["ClaimForm", "PoliceReport"] }]
    });
    expect(requestBody).not.toHaveProperty("tenantId");
    expect(requestBody).not.toHaveProperty("workspaceId");
    expect(requestBody).not.toHaveProperty("userId");
    expect(requestBody).not.toHaveProperty("providerModes");
  });

  test("fails closed when tenant configuration contract is malformed", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name.includes("mobile"), "Tenant onboarding is not rendered on mobile.");
    await page.route("**/api/v1/admin/tenant-configuration", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ insurerName: "Incomplete" }) });
    });

    await page.goto("/tenant-onboarding", { waitUntil: "domcontentloaded" });

    await expect(page.getByText("Tenant configuration response is incomplete.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Review and save" })).toHaveCount(0);
  });
});

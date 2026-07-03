import { expect, test } from "@playwright/test";
import { prepareVisualApi, selectDevelopmentRole, visualCaseId } from "./fixtures";

test.describe("Document verification visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "Admin");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "Admin");
  });

  test("renders typed verification, correction, and sensitivity state", async ({ page }, testInfo) => {
    await page.goto(`/claims/${visualCaseId}/documents`, { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: "Claim document workspace" })).toBeVisible();
    await page.getByRole("button", { name: /visual-claim-form\.pdf/i }).click();

    await expect(page.getByLabel("Verified value for policyNumber")).toHaveValue("VISUAL-POLICY-001");
    await expect(page.getByLabel("Verified value for incidentDate")).toHaveValue("20 June 2026");
    await expect(page.getByText("Confirm the incident date format against the submitted form.")).toBeVisible();
    await expect(page.getByText(/objectKey/i)).toHaveCount(0);
    await expect(page).toHaveScreenshot(`document-verification-${testInfo.project.name}.png`, { fullPage: true });
  });
});

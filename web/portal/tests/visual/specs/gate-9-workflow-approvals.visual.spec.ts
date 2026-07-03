import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 9 Workflow Approvals frames decisions as review steps with mandatory reason", async ({ page }, testInfo) => {
  await page.goto("/approvals");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Human review steps" })).toBeVisible();
  await expect(page.getByText("Review-step queue")).toBeVisible();

  await expect(page.getByText("Workflow review-step decision")).toBeVisible();
  await expect(page.getByText("They do not settle, deny, repudiate, accuse fraud, or authorize payment on a claim.")).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Review-step reason" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve review step" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Override AI recommendation" })).toBeDisabled();

  await page.getByRole("textbox", { name: "Review-step reason" }).fill("Reviewed the evidence checklist and confirmed this workflow step may continue.");
  await expect(page.getByRole("button", { name: "Approve review step" })).toBeEnabled();
  await expect(page.getByRole("button", { name: "Escalate review step" })).toBeEnabled();

  await testInfo.attach(`gate-9-workflow-approvals-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 9 deployed profile exposes no final claim decision controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/approvals");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /confirm fraud/i })).toHaveCount(0);
});

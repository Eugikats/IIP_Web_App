import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 9 Review and Approvals separates prepared work from consequential workflow review", async ({ page }, testInfo) => {
  await page.goto("/app/work-items");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Human review workspace" })).toBeVisible();
  await expect(page.getByRole("button", { name: "My Work" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Verify repair estimate" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Review repair-estimate\.pdf.*Document review/ })).toBeVisible();
  await expect(page.getByText("Current projection · 3")).toBeVisible();
  await expect(page.getByRole("button", { name: "Prepared work" })).toBeVisible();
  await page.getByRole("button", { name: "Consequential approvals" }).click();
  await expect(page.getByText(/Approval actions advance a governed workflow only/)).toBeVisible();
  await expect(page.getByRole("button", { name: "All approvals" })).toBeVisible();
  await expect(page.getByRole("button", { name: "High risk" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Completed" })).toBeVisible();
  await expect(page.getByText("Police report is missing; supervisor review is required.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve" }).first()).toBeDisabled();
  await expect(page.getByRole("button", { name: "Reject" }).first()).toBeDisabled();
  await expect(page.getByRole("button", { name: "Escalate" }).first()).toBeDisabled();
  await expect(page.getByRole("button", { name: "Override AI" }).first()).toBeDisabled();
  await page.getByLabel("Decision reason").fill("Reviewed the cited checklist and the missing evidence warning.");
  await expect(page.getByRole("button", { name: "Approve" }).first()).toBeEnabled();
  await expect(page.getByRole("button", { name: "Escalate" }).first()).toBeEnabled();

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

  await page.goto("/app/work-items");
  await page.waitForLoadState("networkidle");
  await page.getByRole("button", { name: "Consequential approvals" }).click();

  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /confirm fraud/i })).toHaveCount(0);
});

test("Gate 9 My Work reports an unavailable snapshot without false zero metrics", async ({ page }) => {
  await page.route("**/api/v1/my-work**", async (route) => {
    await route.fulfill({
      status: 503,
      contentType: "application/problem+json",
      headers: {
        "access-control-allow-origin": "*",
        "access-control-expose-headers": "x-correlation-id",
        "x-correlation-id": "corr-runtime-recovery",
      },
      body: JSON.stringify({ title: "Service unavailable" }),
    });
  });

  await page.goto("/app/work-items");
  await expect(page.getByText("My Work could not be loaded")).toBeVisible();
  await expect(page.getByText("Reference corr-runtime-recovery")).toBeVisible();

  for (const label of ["Visible work", "Overdue", "Unassigned", "Needs approval"]) {
    await expect(page.getByText(label).locator("..").getByText("-", { exact: true })).toBeVisible();
  }

  await expect(page.getByText("Failed to fetch", { exact: true })).toHaveCount(0);
});

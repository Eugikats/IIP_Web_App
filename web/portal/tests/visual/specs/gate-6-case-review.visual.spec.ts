import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const casePath = "/app/claims/CLM-2026-00491";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 6 Case Review exposes approved tabs, record fields and action state", async ({ page }, testInfo) => {
  await page.goto(casePath);
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: /CLM-2026 · Sarah N\./ })).toBeVisible();
  await expect(page.getByText("Motor claim lifecycle")).toBeVisible();
  await expect(page.getByText("Human review actions")).toBeVisible();
  for (const action of ["Draft customer update", "Request supervisor review", "Draft document follow-up", "Export claim pack"]) {
    await expect(page.getByRole("button", { name: action })).toBeVisible();
  }
  await expect(page.getByRole("main").getByText("Customer updates")).toBeVisible();
  await expect(page.getByRole("main").getByText("Documents received")).toBeVisible();
  await expect(page.getByRole("link", { name: "Open extraction review" })).toHaveAttribute("href", /\/app\/claims\/CLM-2026-00491\/extraction/);
  await expect(page.getByRole("main").getByText("Policy & customer")).toBeVisible();
  await expect(page.getByRole("main").getByText("Completeness", { exact: true })).toBeVisible();

  await testInfo.attach(`gate-6-case-review-overview-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 6 Case Review documents, approvals and governed action confirmation are operable", async ({ page }) => {
  await page.goto(casePath);
  await page.waitForLoadState("networkidle");

  await page.getByRole("button", { name: "Export claim pack" }).click();
  const dialog = page.getByRole("dialog", { name: "Export claim evidence pack" });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole("textbox", { name: "Reason for audit trail" })).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Export claim pack" })).toBeDisabled();
  await dialog.getByRole("textbox", { name: "Reason for audit trail" }).fill("Export evidence pack for supervisor review validation.");
  await expect(dialog.getByRole("button", { name: "Export claim pack" })).toBeEnabled();
  await dialog.getByRole("button", { name: "Cancel" }).click();

  await page.getByRole("button", { name: "Request supervisor review" }).click();
  await expect(page.getByText(/Supervisor review requested|Could not request supervisor review/)).toBeVisible();
});

test("Gate 6 Case Review deployed profile exposes no final decision controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto(casePath);
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /authorise payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /confirm fraud/i })).toHaveCount(0);
  await expect(page.getByText(/simulated send/i)).toHaveCount(0);
});

import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const casePath = "/cases/CLM-2026-00491";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 6 Case Review exposes approved tabs, record fields and action state", async ({ page }, testInfo) => {
  await page.goto(casePath);
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Sarah N." })).toBeVisible();
  for (const tab of ["Overview", "Documents", "Policy & Coverage", "Timeline", "Tasks", "Approvals", "Communications", "Audit"]) {
    await expect(page.getByRole("tab", { name: tab })).toBeVisible();
  }

  await expect(page.getByText("Claim ID")).toBeVisible();
  await expect(page.getByText("Masked contact")).toBeVisible();
  await expect(page.getByText("Claimed amount")).toBeVisible();
  await expect(page.getByText("Not captured")).toBeVisible();
  await expect(page.getByText("Case-level blocked actions")).toBeVisible();
  await expect(page.getByText("MISSING_REQUIRED_DOCUMENTS")).toBeVisible();

  await testInfo.attach(`gate-6-case-review-overview-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 6 Case Review documents, approvals and governed action confirmation are operable", async ({ page }) => {
  await page.goto(casePath);
  await page.waitForLoadState("networkidle");

  await page.getByRole("tab", { name: "Documents" }).click();
  await expect(page.getByText("repair_estimate.pdf")).toBeVisible();
  await page.getByText("repair_estimate.pdf").click();
  await expect(page.getByText("LOW_CONFIDENCE_AMOUNT")).toBeVisible();
  await expect(page.getByText("Document blocked actions")).toBeVisible();
  await page.getByRole("button", { name: "Close drawer" }).click();

  await page.getByRole("tab", { name: "Approvals" }).click();
  await expect(page.getByText("Police report is missing; supervisor review is required.")).toBeVisible();

  await page.getByRole("tab", { name: "Tasks" }).click();
  await page.getByRole("button", { name: "Create approval request" }).click();
  await expect(page.getByRole("textbox", { name: "Reason" })).toBeVisible();
  await expect(page.getByText("A mandatory reason and audit event are required before this governed action is recorded.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Create review step" })).toBeDisabled();
  await page.getByRole("textbox", { name: "Reason" }).fill("Supervisor review is required because mandatory evidence is still missing.");
  await expect(page.getByRole("button", { name: "Create review step" })).toBeEnabled();
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

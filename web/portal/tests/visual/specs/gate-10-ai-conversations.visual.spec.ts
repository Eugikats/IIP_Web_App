import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 10 AI Conversations exposes provenance and source-grounded answers", async ({ page }, testInfo) => {
  await page.goto("/ai-assistant");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "AI conversation workspace" })).toBeVisible();
  await expect(page.getByText("AI provenance log")).toBeVisible();
  await expect(page.getByText("corr-ai-visual-001")).toBeVisible();

  await page.getByPlaceholder("Ask IIP AI a policy question…").fill("Is windscreen damage covered?");
  await page.getByTitle("Send").click();

  await expect(page.getByText("Approved policy sources")).toBeVisible();
  await expect(page.getByText("Assistant blocked actions")).toBeVisible();
  await expect(page.getByText("AI policy answers cannot be used as final coverage decisions.")).toBeVisible();

  await testInfo.attach(`gate-10-ai-conversations-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 10 AI Conversations escalates unsupported questions instead of answering from memory", async ({ page }) => {
  await page.goto("/ai-assistant");
  await page.waitForLoadState("networkidle");

  await page.getByPlaceholder("Ask IIP AI a policy question…").fill("Does an unlisted benefit apply?");
  await page.getByTitle("Send").click();

  await expect(page.getByText("I cannot answer this from approved knowledge.")).toBeVisible();
  await expect(page.getByText("Escalation required", { exact: true })).toBeVisible();
  await expect(page.getByText("No approved source was returned. Treat this as an escalation, not a policy answer.")).toBeVisible();
});

test("Gate 10 deployed profile exposes no autonomous decision controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/ai-assistant");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /confirm fraud/i })).toHaveCount(0);
  await expect(page.getByText(/AI Approved/i)).toHaveCount(0);
  await expect(page.getByText(/AI Rejected/i)).toHaveCount(0);
});

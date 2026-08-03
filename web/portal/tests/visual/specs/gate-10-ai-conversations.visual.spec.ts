import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 10 Command Center exposes provenance and source-grounded answers", async ({ page }, testInfo) => {
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: /Good morning,/ })).toBeVisible();
  await expect(page.getByRole("main").getByText(/Answers are grounded in this workspace's real data/)).toBeVisible();
  if ((page.viewportSize()?.width ?? 0) >= 640) {
    await expect(page.getByRole("main").getByText("Open claims")).toBeVisible();
    await expect(page.getByRole("main").getByText("SLA at risk")).toBeVisible();
  }

  await page.getByPlaceholder("Ask IIP anything — claims, documents, customers, policies, workflows…").fill("Show claims that need attention.");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("Claims needing attention")).toBeVisible();
  await expect(page.getByRole("link", { name: /CLM-2026-00491 Sarah N\./ })).toHaveAttribute("href", /\/app\/claims\/CLM-2026-00491/);
  await expect(page.getByText("Police report")).toBeVisible();
  await expect(page.getByText("Azure OpenAI not configured.")).toBeVisible();

  await testInfo.attach(`gate-10-ai-conversations-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 10 Command Center escalates unsupported questions instead of answering from memory", async ({ page }) => {
  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await page.getByPlaceholder("Ask IIP anything — claims, documents, customers, policies, workflows…").fill("Does an unlisted benefit apply?");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("Does an unlisted benefit apply?")).toBeVisible();
  await expect(page.getByText("IIP Operations · human review required for regulated decisions")).toBeVisible();
  await expect(page.getByRole("button", { name: /approve payout|deny claim|confirm fraud/i })).toHaveCount(0);
});

test("Gate 10 deployed profile exposes no autonomous decision controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/app");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /confirm fraud/i })).toHaveCount(0);
  await expect(page.getByText(/AI Approved/i)).toHaveCount(0);
  await expect(page.getByText(/AI Rejected/i)).toHaveCount(0);
});

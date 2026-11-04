import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 10 Command Center exposes provenance and source-grounded answers", async ({ page }, testInfo) => {
  await page.goto("/app/assistant");
  await page.waitForLoadState("networkidle");

  // Navigation model §6.2, the chat-first landing is exactly four elements (orb, greeting,
  // subtext, suggestion chips); there is no stat row or provenance line in the hero itself.
  await expect(page.getByRole("heading", { name: /Good (morning|afternoon|evening),/ })).toBeVisible();
  await expect(page.getByRole("main").getByText("How can I help with your work today?")).toBeVisible();
  await expect(page.getByRole("main").getByRole("button", { name: "Which claims are waiting on documents?" })).toBeVisible();

  await page.getByPlaceholder("Ask about claims, documents or customers…").fill("Show claims that need attention.");
  await page.getByRole("button", { name: "Send to IIP Operations" }).click();

  await expect(page.getByText("Claims needing attention")).toBeVisible();
  await expect(page.getByRole("link", { name: /CLM-2026-00491 Sarah N\. Motor/ })).toHaveAttribute("href", /\/app\/claims\/CLM-2026-00491/);
  await expect(page.getByText("Police report")).toBeVisible();
  // DESIGN.md's "Absent capability renders Not configured" rule now goes through the shared
  // NotConfiguredStateSurface primitive ("Not configured" pill + "<provider>. <adminAction>"),
  // not the old ad-hoc "<provider> not configured." sentence.
  await expect(page.getByText("Azure OpenAI. Configure Azure OpenAI before production AI generation.")).toBeVisible();

  await testInfo.attach(`gate-10-ai-conversations-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 10 Command Center escalates unsupported questions instead of answering from memory", async ({ page }) => {
  await page.goto("/app/assistant");
  await page.waitForLoadState("networkidle");

  await page.getByPlaceholder("Ask about claims, documents or customers…").fill("Does an unlisted benefit apply?");
  await page.getByRole("button", { name: "Send to IIP Operations" }).click();

  await expect(page.getByText("Does an unlisted benefit apply?")).toBeVisible();
  // Design language v2 §8.17, AIMessageBubble no longer repeats a per-message authority line;
  // the always-visible StatusBar footer is the persistent statement of human decision authority.
  if ((page.viewportSize()?.width ?? 0) >= 640) {
    await expect(page.getByText("AI prepares work · humans approve regulated decisions")).toBeVisible();
  } else {
    await expect(page.getByText("Human authority")).toBeVisible();
  }
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

import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { prepareVisualApi, selectDevelopmentRole } from "./fixtures";

test.describe("AI Conversations visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "ClaimsSupervisor");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "ClaimsSupervisor");
    await page.goto("/ai-assistant", { waitUntil: "domcontentloaded" });
  });

  test("uses the grounded staff endpoint without browser-owned audience or authority", async ({ page }, testInfo) => {
    await expect(page.getByRole("heading", { name: "Grounded insurance assistant" })).toBeVisible();
    await expect(page.getByLabel("Audience")).toHaveCount(0);

    const requestPromise = page.waitForRequest("**/api/v1/assistant/policy-questions");
    const composer = page.getByPlaceholder("Ask IIP AI a policy question...");
    await composer.fill("What does the approved motor policy say about windscreen damage?");
    await composer.press("Enter");

    const request = await requestPromise;
    const payload = request.postDataJSON() as Record<string, unknown>;
    expect(payload).toEqual({
      caseId: null,
      question: "What does the approved motor policy say about windscreen damage?",
      productLine: null
    });
    expect(payload).not.toHaveProperty("audience");
    expect(payload).not.toHaveProperty("tenantId");
    expect(payload).not.toHaveProperty("workspaceId");
    expect(payload).not.toHaveProperty("userId");

    await expect(page.getByText("Prepared by AzureOpenAI", { exact: false })).toBeVisible();
    await expect(page.getByText("Preparation only", { exact: false })).toBeVisible();
    await expect(page.getByLabel("Approved sources")).toContainText("Motor Policy Wording, Windscreen section");
    await expect(page.getByLabel("Approved sources")).toContainText("Windscreen damage is included");

    await expect(page).toHaveScreenshot(`ai-conversations-${testInfo.project.name}.png`, {
      fullPage: true
    });
  });

  test("fails closed when the backend response can trigger a decision", async ({ page }) => {
    await page.route("**/api/v1/assistant/policy-questions", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          answer: "This response must not render.",
          confidenceScore: 0.99,
          escalationRequired: false,
          guardrailMessage: "Unsafe response fixture.",
          riskFlag: "grounded",
          coverageState: "GroundedAnswerAvailable",
          sources: [],
          agentActionLogId: "55555555-5555-4555-8555-555555555555",
          modelProvider: "AzureOpenAI",
          modelDeployment: "visual-chat",
          promptVersion: "unsafe-visual-fixture",
          visibility: "Internal",
          canTriggerDecision: true,
          generatedAt: "2026-06-20T09:18:00Z",
          correlationId: "unsafe-visual-correlation"
        })
      });
    });

    const composer = page.getByPlaceholder("Ask IIP AI a policy question...");
    await composer.fill("Can this response make a decision?");
    await composer.press("Enter");

    await expect(page.getByText("Assistant response did not include required governed provenance and non-decision state.")).toBeVisible();
    await expect(page.getByText("This response must not render.")).toHaveCount(0);
  });

  test("has no critical accessibility violations", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Grounded insurance assistant" })).toBeVisible();

    const results = await new AxeBuilder({ page })
      .disableRules(["color-contrast"])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});

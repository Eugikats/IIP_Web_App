import { expect, test } from "@playwright/test";
import { prepareVisualApi, selectDevelopmentRole } from "./fixtures";

test.describe("Policy Intelligence visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "Admin");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "Admin");
  });

  test("renders a grounded answer with approved citations and no browser authority", async ({ page }, testInfo) => {
    await page.goto("/policy-intelligence", { waitUntil: "domcontentloaded" });
    await expect(page.getByRole("heading", { name: "Policy coverage workbench" })).toBeVisible();

    await page.getByLabel("Policy question").fill("Is windscreen damage covered?");
    await page.getByLabel("Product line").fill("Motor");
    const requestPromise = page.waitForRequest("**/api/assistant/policy-questions");
    await page.getByRole("button", { name: "Check policy evidence" }).click();
    const request = await requestPromise;
    const body = request.postDataJSON() as Record<string, unknown>;

    expect(body).toMatchObject({
      question: "Is windscreen damage covered?",
      productLine: "Motor",
      audience: "InternalUser"
    });
    expect(body).not.toHaveProperty("tenantId");
    expect(body).not.toHaveProperty("workspaceId");
    expect(body).not.toHaveProperty("userId");
    await expect(page.getByText("Grounded answer available")).toBeVisible();
    await expect(page.getByText("Motor Policy Wording, Windscreen section")).toBeVisible();
    await expect(page.getByText("Cannot trigger decisions")).toBeVisible();
    await page.evaluate(() => document.querySelector("main")?.scrollTo(0, 0));
    await expect(page).toHaveScreenshot(`policy-intelligence-grounded-${testInfo.project.name}.png`, { fullPage: true });
    if (testInfo.project.name.includes("mobile")) {
      await page.getByRole("heading", { name: "Coverage result" }).scrollIntoViewIfNeeded();
      await expect(page).toHaveScreenshot(`policy-intelligence-result-${testInfo.project.name}.png`, { fullPage: true });
    }
  });

  test("refuses a policy conclusion when approved evidence is unavailable", async ({ page }) => {
    await page.route("**/api/assistant/policy-questions", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          answer: "I cannot answer this from approved knowledge. Escalate this question to a human reviewer.",
          confidenceScore: 0.1,
          escalationRequired: true,
          guardrailMessage: "No approved source was found. The assistant did not use model memory.",
          riskFlag: "no_source",
          coverageState: "InsufficientApprovedEvidence",
          sources: [],
          agentActionLogId: "55555555-5555-4555-8555-555555555555",
          modelProvider: "AzureOpenAI",
          modelDeployment: "visual-chat",
          promptVersion: "policy-answer-v2",
          visibility: "Internal",
          canTriggerDecision: false,
          generatedAt: "2026-06-20T09:20:00Z",
          correlationId: "visual-policy-no-source"
        })
      });
    });

    await page.goto("/policy-intelligence", { waitUntil: "domcontentloaded" });
    await page.getByLabel("Policy question").fill("Does this policy cover an unlisted event?");
    await page.getByRole("button", { name: "Check policy evidence" }).click();

    await expect(page.getByText("Insufficient approved evidence")).toBeVisible();
    await expect(page.getByText("Human review required")).toBeVisible();
    await expect(page.getByText("No approved source supports an answer.", { exact: false })).toBeVisible();
  });

  test("fails closed when backend grounding state is incomplete", async ({ page }) => {
    await page.route("**/api/assistant/policy-questions", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ answer: "Malformed response without authority state." })
      });
    });

    await page.goto("/policy-intelligence", { waitUntil: "domcontentloaded" });
    await page.getByLabel("Policy question").fill("Is this covered?");
    await page.getByRole("button", { name: "Check policy evidence" }).click();

    await expect(page.getByText("Policy response did not include the required grounding and coverage state.")).toBeVisible();
    await expect(page.getByText("Prepared answer")).toHaveCount(0);
  });
});

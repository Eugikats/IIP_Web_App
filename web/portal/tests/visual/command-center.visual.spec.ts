import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { prepareVisualApi, selectDevelopmentRole } from "./fixtures";

test.describe("Command Center visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "ClaimsSupervisor");
  });

  test("renders the chat-first staff shell with backend-owned provider truth", async ({ page }, testInfo) => {
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "ClaimsSupervisor");
    await page.goto("/", { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: "Command Center" })).toBeVisible();
    await expect(page.getByLabel("Provider status")).toBeVisible();
    await expect(page.getByLabel("Azure OpenAI: Connected")).toBeVisible();
    await expect(page.getByRole("main")).toContainText("Operational context");
    await expect(page.getByRole("main")).toContainText("Daily operational briefing");

    if (testInfo.project.name.includes("mobile")) {
      await expect(page.getByLabel("Primary mobile navigation")).toBeVisible();
      await expect(page.getByLabel("Claims Intelligence")).toBeVisible();
    }

    await expect(page).toHaveScreenshot(`command-center-${testInfo.project.name}.png`, {
      fullPage: true
    });
  });

  test("has no critical accessibility violations in the command center shell", async ({ page }) => {
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "ClaimsSupervisor");
    await page.goto("/", { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: "Command Center" })).toBeVisible();
    await expect(page.getByRole("main")).toContainText("Operational context");
    await expect(page.getByRole("main")).toContainText("Daily operational briefing");

    const results = await new AxeBuilder({ page })
      .disableRules(["color-contrast"])
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test("renders governed model and audit provenance after an operational answer", async ({ page }) => {
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "ClaimsSupervisor");
    await page.goto("/", { waitUntil: "domcontentloaded" });

    const question = "What should the claims team address first?";
    // Chat-first Command Center posts to the governed typed-block /query endpoint
    // (the prose /command-center/assistant endpoint was dropped in the v2 migration).
    const requestPromise = page.waitForRequest("**/api/v1/command-center/query");
    const composer = page.getByPlaceholder("Ask about claims, workflow pressure, approvals, or customer updates...");
    await composer.fill(question);
    await composer.press("Enter");

    const request = await requestPromise;
    expect((request.postDataJSON() as { question: string }).question).toBe(question);

    // The typed-block answer renders, and the surface states its non-decision boundary.
    await expect(page.getByText("SLA pressure is the immediate concern", { exact: false })).toBeVisible();
    await expect(page.getByText("Preparation only", { exact: false })).toBeVisible();
  });
});

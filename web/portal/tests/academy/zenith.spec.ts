import { expect, test } from "@playwright/test";

// Zenith in a real browser against the offline API, which has no language model: recommendations, plans and practice work,
// and an explanation says plainly that written answers are not set up, while still pointing at the lessons that cover it.

const API = "http://localhost:5167/api/v1/academy";
const HEADERS = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations", "x-demo-role": "Admin", "x-demo-user-id": "user-1", "X-User-Id": "user-1" };

test.describe("zenith", () => {
  test("floating panel and full page", async ({ page, request }) => {
    const compact = (page.viewportSize()?.width ?? 1280) <= 860;
    await request.post(`${API}/courses/ai-agentic-insurance/enrol`, { headers: HEADERS, data: {} });
    await request.post(`${API}/courses/ai-sales-distribution/enrol`, { headers: HEADERS, data: {} });

    await page.goto("/academy/courses");
    if (compact) {
      // Compact layouts expose Zenith through the bottom navigation instead of a floating button.
      await expect(page.getByTestId("zenith-fab")).toBeHidden();
      await page.locator(".bnav").getByRole("link", { name: "Zenith" }).click();
    } else {
      await expect(page.getByTestId("zenith-fab")).toBeVisible();
      await page.getByTestId("zenith-fab").click();
      await expect(page.getByTestId("zenith-panel")).toBeVisible();
    }
    await expect(page.getByRole("heading", { name: /I am Zenith/ })).toBeVisible();
    await expect(page.getByText("Written explanations are not set up")).toBeVisible();

    // A recommendation comes from real progress and links to the step.
    await page.getByRole("button", { name: "What should I study next?" }).click();
    await expect(page.getByText(/Your next .* is|You have finished everything/)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/Worked for .*steps/)).toBeVisible();

    // An explanation names the lessons that cover it, and says why it cannot write more.
    await page.getByLabel("Ask Zenith").fill("What is the difference between generative AI and agentic AI?");
    await page.getByRole("button", { name: "Send" }).click();
    await expect(page.getByText(/Written explanations are not set up in this environment/).last()).toBeVisible({ timeout: 15_000 });
    await expect(page.locator(".src").first()).toBeVisible();

    // A question the lessons do not cover is not guessed at.
    await page.getByLabel("Ask Zenith").fill("How do I cook matoke with groundnut sauce?");
    await page.getByRole("button", { name: "Send" }).click();
    await expect(page.getByText(/could not find that in your lessons/)).toBeVisible({ timeout: 15_000 });

    // The chat follows the person to the full page, with an activity log, and can be cleared.
    if (!compact) await page.getByTitle("Open full view").click();
    await expect(page).toHaveURL(/\/academy\/zenith$/);
    await expect(page.getByText(/could not find that in your lessons/)).toBeVisible();
    await expect(page.getByText("Answer delivered").first()).toBeVisible();
    await page.getByRole("button", { name: /Plan your week/ }).click();
    await expect(page.getByText(/5 day plan/)).toBeVisible({ timeout: 15_000 });
    await page.getByRole("button", { name: /New chat/ }).click();
    await expect(page.getByRole("heading", { name: /I am Zenith/ })).toBeVisible();

    // It is not offered while a person is being assessed.
    await page.goto("/academy/assessment/ai-agentic-insurance/unit-1-1-quiz");
    await expect(page.getByTestId("question").first()).toBeVisible();
    await expect(page.getByTestId("zenith-fab")).toHaveCount(0);
  });
});

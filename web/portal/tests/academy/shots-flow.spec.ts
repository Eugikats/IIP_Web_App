import { expect, test } from "@playwright/test";

// Screenshots of the assessment and final-task screens in their states, for looking at. Off unless ACADEMY_SHOTS_FLOW is set,
// because it enrols the one local learner. Output: test-results/academy-shots/flow-{name}-{project}.png

const API = "http://localhost:5167/api/v1/academy";
const HEADERS = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations", "x-demo-role": "Admin", "x-demo-user-id": "user-1", "X-User-Id": "user-1" };

test.skip(!process.env.ACADEMY_SHOTS_FLOW, "screenshots only");

test("assessment and final task screens", async ({ page, request }, info) => {
  const out = (name: string) => `test-results/academy-shots/flow-${name}-${info.project.name}.png`;
  await request.post(`${API}/courses/ai-agentic-insurance/enrol`, { headers: HEADERS, data: {} });

  await page.goto("/academy/assessment/ai-agentic-insurance/unit-1-1-quiz");
  await expect(page.getByTestId("question")).toHaveCount(10);
  for (let index = 0; index < 4; index++) await page.getByTestId("question").nth(index).getByRole("radio").first().click();
  await page.screenshot({ path: out("quiz"), fullPage: true });

  for (let index = 4; index < 10; index++) await page.getByTestId("question").nth(index).getByRole("radio").first().click();
  await page.getByTestId("submit-assessment").click();
  await expect(page.getByTestId("assessment-result")).toBeVisible();
  await page.screenshot({ path: out("result"), fullPage: true });

  await page.goto("/academy/final/ai-agentic-insurance");
  await expect(page.getByTestId("final-locked")).toBeVisible();
  await page.screenshot({ path: out("final-locked"), fullPage: true });

  await page.goto("/academy/learn/ai-agentic-insurance/unit-1-1");
  await page.waitForTimeout(1500);
  await page.screenshot({ path: out("lesson"), fullPage: true });
});

test("zenith panel and page", async ({ page, request }, info) => {
  const out = (name: string) => `test-results/academy-shots/flow-${name}-${info.project.name}.png`;
  await request.post(`${API}/courses/ai-sales-distribution/enrol`, { headers: HEADERS, data: {} });
  const narrow = (page.viewportSize()?.width ?? 1280) <= 860; // the design drops the floating button there; the bottom bar leads to the page
  if (!narrow) {
    await page.goto("/academy/courses");
    await page.getByTestId("zenith-fab").click();
    await page.getByRole("button", { name: "Plan my study week" }).click();
    await expect(page.getByText(/5 day plan/)).toBeVisible({ timeout: 15_000 });
    await page.waitForTimeout(800);
    await page.screenshot({ path: out("zenith-panel") });
  }
  await page.goto("/academy/zenith");
  if (narrow) await page.getByRole("button", { name: /Plan your week/ }).click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: out("zenith-page"), fullPage: true });
});

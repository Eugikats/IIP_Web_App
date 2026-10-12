import { expect, test } from "@playwright/test";

const NEW_COURSES = [
  { id: "ai-agentic-insurance", title: "AI & Agentic AI for Insurance", category: "ai-foundations" },
  { id: "ai-claims-fraud", title: "AI-Powered Claims Management & Fraud Intelligence", category: "claims-fraud" },
  { id: "ai-underwriting-risk", title: "AI for Underwriting & Risk Intelligence", category: "underwriting-risk" },
  { id: "ai-sales-distribution", title: "AI for Insurance Sales, Distribution & Customer Experience", category: "sales-experience" },
  { id: "agentic-systems", title: "Building Agentic AI Systems for Insurance", category: "build-govern" },
];

test("The Academy lists the five programme courses and the four hands-on ones, each at UGX 500,000", async ({ page }) => {
  await page.goto("/app/academy");

  for (const course of NEW_COURSES) {
    const card = page.getByTestId(`course-${course.id}`);
    await expect(card.getByRole("heading", { name: course.title })).toBeVisible();
    await expect(card.getByText("UGX 500,000")).toBeVisible();
  }
  await expect(page.locator("[data-testid^='course-']")).toHaveCount(9);
  // The old dollar price is gone everywhere on the page.
  await expect(page.getByText("$50")).toHaveCount(0);
});

test("Choosing a category narrows the courses, and the back link returns to all of them", async ({ page }) => {
  await page.goto("/app/academy");
  await expect(page.getByTestId("academy-categories").getByRole("link")).toHaveCount(5);

  await page.getByTestId("category-claims-fraud").click();
  await expect(page).toHaveURL(/category=claims-fraud/);
  await expect(page.getByTestId("category-header")).toBeVisible();
  await expect(page.getByTestId("academy-courses-heading")).toHaveText("Claims and fraud: courses");
  await expect(page.locator("[data-testid^='course-']")).toHaveCount(3);
  await expect(page.getByTestId("course-ai-claims-fraud")).toBeVisible();
  await expect(page.getByTestId("course-claims")).toBeVisible();
  await expect(page.getByTestId("course-fraud-identity")).toBeVisible();

  await page.getByTestId("category-header").getByRole("link", { name: "All courses" }).click();
  await expect(page.locator("[data-testid^='course-']")).toHaveCount(9);
});

test("A course whose lessons are being written shows its outline and says it cannot be started", async ({ page }) => {
  await page.goto("/app/academy/ai-agentic-insurance");

  await expect(page.getByRole("heading", { level: 1, name: "AI & Agentic AI for Insurance" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Course outline" })).toBeVisible();
  await expect(page.getByText("AI, generative AI and agentic AI in plain words", { exact: true })).toBeVisible();
  await expect(page.getByTestId("lessons-in-preparation")).toBeVisible();
  await expect(page.getByRole("button", { name: "Start course" })).toHaveCount(0);
  await expect(page.getByText("UGX 500,000")).toBeVisible();
});

test("A course with lessons still starts as before", async ({ page }) => {
  await page.goto("/app/academy/claims");

  await expect(page.getByRole("heading", { name: "Course content" })).toBeVisible();
  await page.getByRole("button", { name: "Start course" }).click();
  await expect(page.getByText("The life of a bancassurance claim").first()).toBeVisible();
});

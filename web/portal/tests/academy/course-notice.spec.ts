import { expect, test } from "@playwright/test";

for (const courseId of ["ai-agentic-insurance", "ai-sales-distribution"]) {
  test(`${courseId}: no pilot banner and paid enrolment remains unavailable`, async ({ page }) => {
    await page.goto(`/academy/courses/${courseId}`);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(page.getByTestId("reviewer-edition")).toBeVisible();
    await expect(page.getByTestId("content-status")).toHaveCount(0);
    await expect(page.getByText("Pilot reviewer edition.", { exact: false })).toHaveCount(0);
    await expect(page.getByTestId("enrol")).toHaveCount(0);
    await expect(page.getByRole("heading", { name: "Course content", exact: true })).toBeVisible();
  });
}

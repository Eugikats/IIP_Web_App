import { expect, test } from "@playwright/test";

// The Academy opens from IIP's sidebar as its own space, the old workspace routes lead to it, and Back to IIP returns.

test("sidebar to the Academy and back, and the old routes redirect", async ({ page }, info) => {
  test.skip(info.project.name === "phone", "the sidebar is a drawer on a phone");
  await page.goto("/app");
  await page.getByRole("complementary", { name: "Primary navigation" }).getByRole("link", { name: "IIP Academy" }).click();
  await expect(page).toHaveURL(/\/academy$/);
  await expect(page.getByRole("link", { name: "IIP Academy home" })).toBeVisible();

  await page.getByTestId("back-to-iip").click();
  await expect(page).toHaveURL(/\/app$/);

  await page.goto("/app/academy");
  await expect(page).toHaveURL(/\/academy$/);
  await page.goto("/app/academy/ai-agentic-insurance");
  await expect(page).toHaveURL(/\/academy\/courses\/ai-agentic-insurance$/);
});

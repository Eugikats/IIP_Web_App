import { test } from "@playwright/test";

// Screenshots of the checkout dialog, for looking at. Needs the API started with scripts/local/run-offline-payments.sh.
test.skip(!process.env.ACADEMY_SHOTS_FLOW, "screenshots only");

test("checkout dialog", async ({ page }, info) => {
  await page.goto("/academy/courses/governance");
  await page.getByTestId("enrol").click();
  await page.getByTestId("pay-mobile_money").waitFor();
  await page.waitForTimeout(600);
  await page.screenshot({ path: `test-results/academy-shots/checkout-${info.project.name}.png` });
});

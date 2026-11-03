import { expect, test } from "@playwright/test";

test("fixture claim navigation recovers and unavailable detail shows safe metadata", async ({ page }) => {
  let liveClaimDetailRequests = 0;
  page.on("request", (request) => {
    if (/\/api\/v1\/claims\/CLM-/.test(request.url())) liveClaimDetailRequests += 1;
  });

  await page.route("**/api/v1/my-work**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      headers: { "access-control-allow-origin": "*" },
      body: JSON.stringify({
        ownership: "Mine",
        returnedCount: 0,
        hasMore: false,
        items: [],
        summary: { total: 0, overdue: 0, unassigned: 0, approvalRequired: 0 },
      }),
    });
  });

  await page.goto("/app/work-items");
  await expect(page.getByRole("heading", { name: "Human review workspace" })).toBeVisible();
  await page.getByRole("link", { name: "Claims", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Work Queue" })).toBeVisible();

  for (let pass = 0; pass < 3; pass += 1) {
    await Promise.all([
      page.waitForURL("**/app/claims/CLM-1048"),
      page.getByRole("main").getByRole("link", { name: "Review" }).first().click(),
    ]);
    await expect(page.getByRole("heading", { name: /CLM-1048 · Aisha Nakato/ })).toBeVisible({ timeout: 15_000 });
    const incidentDescription = page.getByText("Rear-end collision, Kampala Rd");
    await expect(incidentDescription).toHaveCount(2);
    await expect(incidentDescription.nth(0)).toBeVisible();
    await expect(incidentDescription.nth(1)).toBeVisible();
    await expect(page.getByText("Claim unavailable")).toHaveCount(0);
    await page.goBack();
    await page.waitForURL("**/app/claims");
    await expect(page.getByRole("heading", { name: "Work Queue" })).toBeVisible();
  }

  expect(liveClaimDetailRequests).toBe(0);
  await page.goto("/app/claims/CLM-9999");

  await expect(page.getByText("Claim unavailable")).toBeVisible();
  await expect(page.getByText(/^Attempted /)).toBeVisible();
  await expect(page.getByText("This synthetic claim is not available in the current fixture workspace.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Return to claims" })).toBeVisible();
});

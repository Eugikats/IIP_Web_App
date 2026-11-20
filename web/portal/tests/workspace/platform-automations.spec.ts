import { expect, test } from "@playwright/test";

// The organisation platform: an automation goes from a template to a recorded run only through the governed steps,
// and what it finds lands in the Intelligence Inbox. Runs against the offline API (seeded demo claims).

test("an automation is tested, submitted, approved, activated and run, and its findings reach the inbox", async ({ page }) => {
  await page.goto("/app/platform/automations");
  await expect(page.getByRole("heading", { level: 1, name: "Automations" })).toBeVisible({ timeout: 60_000 });

  await page.getByRole("link", { name: "New automation" }).first().click();
  await page.getByRole("button", { name: /Claims past or near their service deadline/ }).click();
  await page.getByRole("button", { name: "Review" }).click();
  await page.getByRole("button", { name: "Save draft" }).click();

  // A draft can only be tested or retired: nothing else is offered before it has been tested.
  await expect(page.getByRole("button", { name: "Run test" })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole("button", { name: "Activate" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Approve" })).toHaveCount(0);

  await page.getByRole("button", { name: "Run test" }).click();
  await expect(page.getByText("Test result")).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "Submit for approval" }).click();
  await page.getByRole("button", { name: "Approve", exact: true }).click();
  await page.getByRole("button", { name: "Activate" }).click();
  await page.getByRole("button", { name: "Run now" }).click();
  await expect(page.getByText("Latest run")).toBeVisible({ timeout: 30_000 });

  await page.goto("/app/platform/automations?tab=inbox");
  await expect(page.getByRole("tab", { name: /Intelligence Inbox/ })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(/is past its service deadline|is due within/).first()).toBeVisible({ timeout: 30_000 });
});

test("the organisation overview and the workspace list open, with one primary workspace", async ({ page }) => {
  await page.goto("/app/platform");
  await expect(page.getByRole("search", { name: "Ask the Assistant" })).toBeVisible({ timeout: 60_000 });

  await page.goto("/app/platform/workspaces");
  await expect(page.getByRole("heading", { level: 1, name: "Workspaces" })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText("Insurance Intelligence").first()).toBeVisible();
  // The primary workspace is created once, however many requests open the page.
  await expect(page.getByRole("main").getByRole("link", { name: /Insurance Intelligence/ })).toHaveCount(1);
});

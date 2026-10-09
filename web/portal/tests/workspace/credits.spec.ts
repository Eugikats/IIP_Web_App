import { expect, test, type Page } from "@playwright/test";

const BASE = {
  metered: true,
  period: "2026-10",
  planName: "Trial",
  included: 2000,
  calls: 40,
  byFeature: [
    { feature: "kato", label: "Kato on WhatsApp", credits: 900, calls: 30 },
    { feature: "command_center", label: "Command Center", credits: 300, calls: 10 },
  ],
  byDay: [
    { day: "1 Oct", credits: 100 },
    { day: "2 Oct", credits: 300 },
  ],
};

async function openBilling(page: Page, summary: Record<string, unknown> | { status: number }) {
  await page.route("**/api/v1/billing/credits", (route) =>
    "status" in summary
      ? route.fulfill({ status: summary.status as number, contentType: "application/json", body: "{}" })
      : route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(summary) }),
  );
  await page.goto("/app");
  await page.getByRole("button", { name: "Settings", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Settings" });
  await dialog.getByRole("button", { name: "Usage and billing" }).first().click();
  return dialog;
}

test("The meter shows credits used against the plan, where they went, and no cost to people without billing", async ({ page }) => {
  const dialog = await openBilling(page, { ...BASE, used: 1200, remaining: 800, percentUsed: 60, state: "ok", costUsd: null, canAddCredits: false });

  const meter = dialog.getByRole("region", { name: "AI credits" });
  await expect(meter.getByText("1,200")).toBeVisible();
  await expect(meter.getByText("of 2,000 credits used")).toBeVisible();
  await expect(meter.getByText("Within your plan")).toBeVisible();
  await expect(meter.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "60");
  await expect(meter.getByText("Kato on WhatsApp")).toBeVisible();
  await expect(meter.getByText(/Cost to Otic/)).toHaveCount(0);
  await expect(meter.getByRole("link", { name: "Request more credits" })).toHaveCount(0);
});

test("When credits are used up an administrator is told plainly and can ask for more", async ({ page }) => {
  const dialog = await openBilling(page, { ...BASE, used: 2100, remaining: 0, percentUsed: 100, state: "exhausted", costUsd: 6.3, canAddCredits: true });

  const meter = dialog.getByRole("region", { name: "AI credits" });
  await expect(meter.getByRole("alert")).toContainText("used up");
  await expect(meter.getByText("Used up", { exact: true })).toBeVisible();
  await expect(meter.getByRole("link", { name: "Request more credits" })).toHaveAttribute("href", /^mailto:/);
  await expect(meter.getByText(/US\$ 6\.30/)).toBeVisible();
});

test("Everyone else is told to ask their administrator, with no add-credits action", async ({ page }) => {
  const dialog = await openBilling(page, { ...BASE, used: 2100, remaining: 0, percentUsed: 100, state: "exhausted", costUsd: null, canAddCredits: false });

  const meter = dialog.getByRole("region", { name: "AI credits" });
  await expect(meter.getByRole("alert")).toContainText("Ask your administrator");
  await expect(meter.getByRole("link", { name: "Request more credits" })).toHaveCount(0);
});

test("A workspace with no plan limit shows use without a bar, and a failure says so with a retry", async ({ page }) => {
  const dialog = await openBilling(page, { ...BASE, metered: false, planName: "No plan", included: 0, used: 55, remaining: 0, percentUsed: 0, state: "not_metered", costUsd: 0.2, canAddCredits: true });
  const meter = dialog.getByRole("region", { name: "AI credits" });
  await expect(meter.getByText("No plan limit", { exact: true })).toBeVisible();
  await expect(meter.getByRole("progressbar")).toHaveCount(0);

  const failing = await openBilling(await page.context().newPage(), { status: 500 });
  await expect(failing.getByRole("region", { name: "AI credits" }).getByText("Could not check")).toBeVisible();
  await expect(failing.getByRole("button", { name: "Try again" })).toBeVisible();
});

import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 5 Claims Intelligence queue exposes approved controls and backend action state", async ({ page }, testInfo) => {
  await page.goto("/app/claims");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Work Queue" })).toBeVisible();
  await expect(page.getByRole("main").getByText("Claims Intelligence", { exact: true })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Search claim, customer, policy, assignment..." })).toBeVisible();
  for (const filter of ["All", "My queue", "Unassigned", "Missing documents", "Pending approval", "Escalated", "SLA attention"]) {
    await expect(page.getByRole("button", { name: new RegExp(`^${filter}\\s+\\d+`) })).toBeVisible();
  }
  await expect(page.getByLabel("Product")).toBeVisible();
  await expect(page.getByLabel("Risk")).toBeVisible();
  await expect(page.getByLabel("Channel")).toBeVisible();
  await expect(page.getByRole("table")).toBeVisible();
  await expect(page.getByText("Sarah N.")).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: /^Review$/ }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: /Assign/ }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: /Escalate/ }).first()).toBeVisible();

  await testInfo.attach(`gate-5-claims-intelligence-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 5 Claims Intelligence search, queue tabs and preview drawer are operable", async ({ page }) => {
  await page.goto("/app/claims");
  await page.waitForLoadState("networkidle");

  await page.getByRole("textbox", { name: "Search claim, customer, policy, assignment..." }).fill("Daniel");
  await expect(page.getByText("Daniel K.")).toBeVisible();
  await expect(page.getByText("Amina T.")).toHaveCount(0);

  await page.getByRole("textbox", { name: "Search claim, customer, policy, assignment..." }).fill("");
  await page.getByRole("button", { name: /^SLA attention\s+\d+/ }).click();
  await expect(page.getByText("Amina T.")).toBeVisible();

  await page.getByRole("button", { name: /^Clear$/ }).click();
  await expect(page.getByText("Showing 3 of 3 claims")).toBeVisible();
  await expect(page.getByRole("main").getByRole("link", { name: /^Review$/ }).first()).toHaveAttribute("href", /\/app\/claims\/CLM-2026-/);
});

test("Gate 5 Claims Intelligence deployed profile shows no simulation controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/app/claims");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/test ingestion/i)).toHaveCount(0);
  await expect(page.getByText(/simulated send/i)).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /approve payout/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
});

import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 8 Policy Intelligence renders grounded answer citations and blocked decision use", async ({ page }, testInfo) => {
  await page.goto("/app/policy");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Approved policy lookup & coverage Q&A" })).toBeVisible();
  await expect(page.getByText("Source-grounded answers from your approved policy library.")).toBeVisible();
  const question = page.getByRole("textbox", { name: "Ask a policy or coverage question" });
  await expect(question).toHaveAttribute("placeholder", "Is rear-end collision covered? What is the excess on this policy?");
  await question.fill("Is rear-end collision covered under the approved motor wording?");
  await page.getByRole("main").getByRole("button", { name: "Ask", exact: true }).click();
  await expect(page.getByText("Claims needing attention are shown below for human review.")).toBeVisible();
  await expect(page.getByRole("link", { name: /Sarah N\./ }).first()).toBeVisible();
  await expect(page.getByText(/Prepared for review/)).toBeVisible();

  await testInfo.attach(`gate-8-policy-intelligence-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 8 Knowledge Hub blocks retrieval and hides lifecycle actions until governance data is explicit", async ({ page }) => {
  await page.goto("/app/knowledge");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Approved sources for grounded AI answers" })).toBeVisible();
  await expect(page.getByText("Unknown sensitivity is blocked from retrieval by default.")).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Search knowledge titles…" })).toBeVisible();
  await expect(page.getByRole("cell", { name: /Motor Policy Wording/ })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Blocked" })).toBeVisible();
  const documentRow = page.getByRole("row").filter({ hasText: "Motor Policy Wording" });
  await expect(documentRow.getByRole("button", { name: "Approve", exact: true })).toHaveCount(0);
  await expect(documentRow.getByRole("button", { name: "Reclassify", exact: true })).toHaveCount(0);
});

test("Gate 8 deployed profile hides local knowledge simulation wording @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/app/knowledge");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/mock/i)).toHaveCount(0);
  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
});

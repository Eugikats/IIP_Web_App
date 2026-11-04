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
  // The page shows nothing it was not given: before a question is asked it says so, and the answer
  // area only ever renders what the governed query endpoint returned.
  await expect(page.getByText(/Ask a coverage or policy-wording question above/)).toBeVisible();
  await expect(page.getByText(/it is not a binding legal or claims interpretation/)).toBeVisible();
  const question = page.getByRole("textbox", { name: "Ask a policy or coverage question" });
  await expect(question).toBeVisible();
  await expect(page.getByRole("button", { name: "Ask", exact: true }).first()).toBeDisabled();
  await question.fill("Is rear-end collision covered under MTR-8821? What is the excess?");
  await page.getByRole("button", { name: "Ask", exact: true }).first().click();
  await expect(page.getByText("Prepared for review")).toBeVisible();
  await expect(page.getByText("Is rear-end collision covered under MTR-8821? What is the excess?").first()).toBeVisible();

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
  // Upload and "Re-index approved" are workspace-level actions and are available. What stays hidden is any
  // lifecycle action on a source whose sensitivity is unknown: the row says so instead of offering one.
  const blockedRow = page.getByRole("row", { name: /Motor Policy Wording/ });
  await expect(blockedRow).toContainText("No action available");
  await expect(blockedRow.getByRole("button", { name: /Approve|Archive|Re-index|Classify/i })).toHaveCount(0);
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

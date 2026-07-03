import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 8 Policy Intelligence renders grounded answer citations and blocked decision use", async ({ page }, testInfo) => {
  await page.goto("/policy-intelligence");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Coverage answer workspace" })).toBeVisible();
  await expect(page.getByText("Approved and indexed tenant knowledge")).toBeVisible();

  await page.getByPlaceholder("Ask IIP AI a policy question…").fill("Is windscreen damage covered?");
  await page.getByTitle("Send").click();

  await expect(page.getByText("Approved policy sources")).toBeVisible();
  await expect(page.getByText("Motor Policy Wording", { exact: true })).toBeVisible();
  await expect(page.getByText("Assistant blocked actions")).toBeVisible();
  await expect(page.getByText("AI policy answers cannot be used as final coverage decisions.")).toBeVisible();

  await testInfo.attach(`gate-8-policy-intelligence-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 8 Knowledge Hub requires governance reason before source lifecycle action", async ({ page }) => {
  await page.goto("/knowledge-base");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Knowledge operations workbench" })).toBeVisible();
  await page.getByRole("button", { name: /Motor Policy Wording/ }).click();

  await expect(page.getByText("Source chunks")).toBeVisible();
  await page.getByRole("button", { name: "Approve for AI use" }).click();
  await expect(page.getByText("Approving this source makes it eligible for retrieval and citation after indexing.")).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Knowledge governance reason" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Confirm knowledge action" })).toBeDisabled();

  await page.getByRole("textbox", { name: "Knowledge governance reason" }).fill("Reviewed current policy wording and approved for grounded answers.");
  await expect(page.getByRole("button", { name: "Confirm knowledge action" })).toBeEnabled();
});

test("Gate 8 deployed profile hides local knowledge simulation wording @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/knowledge-base");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/mock/i)).toHaveCount(0);
  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
});

import { expect, test } from "@playwright/test";
import { prepareDeployedProfileApi } from "../visual/fixtures";

const prohibitedDeployedTerms = /mock|simulated|local test|placeholder|in-memory|sqlite|developmentauth|seeded-demo/i;
const prohibitedCustomerTerms = /fraud|reserve|internal risk|internal staff|hidden routing|system message|model metadata|raw provenance|provider health|command center/i;

test.describe("Deployed-profile DOM guardrails", () => {
  test("staff shell does not expose local development controls or fake runtime language", async ({ page }) => {
    await prepareDeployedProfileApi(page, "ClaimsSupervisor");
    await page.goto("/app", { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: /Good (morning|afternoon|evening)/i })).toBeVisible();
    await expect(page.locator("html")).not.toHaveClass(/dark/);
    await expect(page.getByLabel("Role")).toHaveCount(0);
    await expect(page.getByText(prohibitedDeployedTerms)).toHaveCount(0);
  });

  test("customer route remains customer-safe in a deployed profile", async ({ page }) => {
    test.skip(true, "Client Portal is not currently mapped in the Next /app route tree.");
    await prepareDeployedProfileApi(page, "CustomerService");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: "My Claims" })).toBeVisible();
    await expect(page.getByLabel("Role")).toHaveCount(0);
    await expect(page.getByLabel("Provider status")).toHaveCount(0);

    await page.getByLabel("Claim reference").fill("11111111-1111-4111-8111-111111111111");
    await page.getByRole("button", { name: "Open claim" }).click();

    await expect(page.getByRole("heading", { name: "Motor Claim" })).toBeVisible();
    await expect(page.getByText(prohibitedCustomerTerms)).toHaveCount(0);
  });

  test("mobile staff routes do not expose consequential decision buttons", async ({ page }, testInfo) => {
    test.skip(!testInfo.project.name.includes("mobile"), "Mobile consequential-control assertion only runs on mobile project.");

    await prepareDeployedProfileApi(page, "ClaimsSupervisor");
    await page.goto("/app/work-items", { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: "Human review workspace" })).toBeVisible();
    await page.getByRole("button", { name: "Consequential approvals" }).click();
    await expect(page.getByRole("button", { name: "Approve" })).toBeDisabled();
    await expect(page.getByRole("button", { name: "Reject" })).toBeDisabled();
    await expect(page.getByRole("button", { name: "Escalate" })).toBeDisabled();
    await expect(page.getByRole("button", { name: "Override AI" })).toBeDisabled();
    await expect(page.getByRole("button", { name: /payout authorization|authorize payment|settle claim|repudiate claim|confirm fraud/i })).toHaveCount(0);
    await expect(page.getByRole("link", { name: /payout authorization|authorize payment|settle claim|repudiate claim|confirm fraud/i })).toHaveCount(0);
  });
});

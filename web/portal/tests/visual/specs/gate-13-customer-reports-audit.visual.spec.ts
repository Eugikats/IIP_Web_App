import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

const customerSensitiveTerms = [
  /ai confidence/i,
  /risk score/i,
  /fraud/i,
  /internal note/i,
  /reserve/i,
  /authority limit/i,
  /raw audit/i,
  /prompt/i,
  /unapproved draft/i,
  /internal coverage/i
];

test("Gate 13 Customer Portal renders customer-safe home without staff shell", async ({ page }, testInfo) => {
  await page.goto("/customer-portal");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Your claim support workspace" })).toBeVisible();
  await expect(page.getByText("Customer-safe DTOs only")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Claim tracker" })).toBeVisible();
  await expect(page.getByText("No customer-safe claim data")).toBeVisible();
  await expect(page.getByText("Not configured")).toBeVisible();

  await expect(page.getByRole("navigation", { name: "Primary" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Command Center" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Admin Console" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Workflow & Approvals" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Knowledge Hub" })).toHaveCount(0);

  for (const term of customerSensitiveTerms) {
    await expect(page.getByText(term)).toHaveCount(0);
  }

  await testInfo.attach(`gate-13-customer-home-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 13 Customer Portal mobile journey uses safe unavailable states", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mobile-chromium", "Mobile customer journey is validated in the mobile project.");

  await page.setViewportSize({ width: 390, height: 844 });

  const routes = [
    ["/customer-portal/submit-claim", "Submit Claim"],
    ["/customer-portal/my-claims", "My Claims"],
    ["/customer-portal/upload-documents", "Upload Documents"],
    ["/customer-portal/policy-support", "Policy Support"],
    ["/customer-portal/messages", "Messages"],
    ["/customer-portal/profile", "Profile"]
  ] as const;

  for (const [route, heading] of routes) {
    await page.goto(route, { waitUntil: "domcontentloaded" });
    await expect(page.getByRole("heading", { name: heading })).toBeVisible();
    await expect(page.getByText("Not configured").first()).toBeVisible();
    for (const term of customerSensitiveTerms) {
      await expect(page.getByText(term)).toHaveCount(0);
    }
  }

  await testInfo.attach(`gate-13-customer-mobile-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 13 Reports exposes truthful export states", async ({ page }) => {
  await page.goto("/reports");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Operations summary" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Basic export states" })).toBeVisible();
  await expect(page.getByText("Operations CSV")).toBeVisible();
  await expect(page.getByText("Power BI", { exact: true })).toBeVisible();
  await expect(page.getByText("Audit packs")).toBeVisible();
  await expect(page.getByText("Not configured")).toHaveCount(2);
  await expect(page.getByText("Available in Compliance")).toBeVisible();
});

test("Gate 13 Compliance audit surface is read-only and shows safe audit packs", async ({ page }, testInfo) => {
  await page.goto("/compliance");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Compliance & Audit" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Read-only audit support" })).toBeVisible();
  await expect(page.getByText("References only")).toBeVisible();
  await expect(page.getByText("Document verification requested")).toBeVisible();
  await expect(page.getByRole("button", { name: "Preview pack" })).toBeVisible();

  await expect(page.getByRole("button", { name: /approve claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny claim/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /send customer update/i })).toHaveCount(0);
  const main = page.getByRole("main");
  await expect(main.getByText(/raw prompt/i)).toHaveCount(0);
  await expect(main.getByText(/secret/i)).toHaveCount(0);

  await page.getByRole("button", { name: "Preview pack" }).click();
  const dialog = page.getByRole("dialog");
  await expect(page.getByText("Audit pack file export")).toBeVisible();
  await expect(dialog.getByText("Preview only", { exact: true })).toBeVisible();
  await expect(dialog.getByText("Customer updates")).toBeVisible();

  await testInfo.attach(`gate-13-compliance-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 13 deployed profile keeps customer portal free of mock and staff controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/customer-portal");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByText(/mock/i)).toHaveCount(0);
  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Command Center" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /approve/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /deny/i })).toHaveCount(0);
});

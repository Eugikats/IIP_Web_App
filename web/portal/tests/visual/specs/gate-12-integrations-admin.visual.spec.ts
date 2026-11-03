import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 12 Integrations and Health shows truthful provider state and adapter boundaries", async ({ page }, testInfo) => {
  await page.goto("/app/integrations");
  await page.waitForLoadState("networkidle");

  const main = page.getByRole("main");
  await expect(main.getByRole("heading", { name: "Integration control plane" })).toBeVisible();
  await expect(main.getByText("One operational surface for provider health, adapter boundaries, identity onboarding, and safe diagnostics.")).toBeVisible();
  await expect(main.getByText(/does not show invented connection success, queue depth, webhook delivery, or support-ticket status/)).toBeVisible();
  await expect(main.getByText("Live provider health")).toBeVisible();
  await expect(main.getByText("Azure OpenAI is not configured for visual fixture.")).toBeVisible();
  await expect(main.getByText("Adapter boundaries", { exact: true })).toBeVisible();
  await expect(main.getByText("Inbound channels")).toBeVisible();
  await expect(main.getByText("Workflow transport")).toBeVisible();
  await expect(main.getByText("Safe diagnostics", { exact: true })).toBeVisible();
  await expect(main.getByText("Queue depth not configured")).toBeVisible();
  await expect(main.getByText(/^Enabled$/)).toHaveCount(0);
  await expect(main.getByRole("button", { name: /test connection|create support ticket/i })).toHaveCount(0);

  await testInfo.attach(`gate-12-integrations-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 12 Admin Console exposes role-filtered governance destinations and honest state", async ({ page }, testInfo) => {
  await page.goto("/app");
  const roleSelector = page.getByRole("combobox", { name: "Preview staff experience" });
  if (await roleSelector.isVisible()) {
    await roleSelector.selectOption("Admin");
  }

  await page.goto("/app/admin");
  await page.waitForLoadState("networkidle");

  const main = page.getByRole("main");
  await expect(main.getByRole("heading", { name: "Admin Console" })).toBeVisible();
  await expect(main.getByText("Tenant, users, workflows, documents, channels, AI providers, agent governance, and integrations.")).toBeVisible();
  await expect(main.getByText("Governance & operations")).toBeVisible();
  await expect(main.getByRole("link", { name: /^Agents / })).toBeVisible();
  await expect(main.getByRole("link", { name: /^Agent Policy / })).toBeVisible();
  await expect(main.getByRole("link", { name: /Integrations & Health/ })).toBeVisible();
  await expect(main.getByText("Resolved from the authenticated tenant and workspace context.")).toBeVisible();
  await expect(main.getByText("Provider state is reported by runtime health checks.")).toBeVisible();
  await expect(main.getByText("Global Insurance Co.")).toHaveCount(0);
  await expect(main.getByText("142 users")).toHaveCount(0);
  await expect(main.getByText("Azure OpenAI (primary)")).toHaveCount(0);
  await expect(main.getByText(/^Configured$/)).toHaveCount(0);

  await testInfo.attach(`gate-12-admin-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 12 tenant workspace cannot access platform-level insurer onboarding", async ({ page }) => {
  await page.goto("/app/admin/onboarding");
  await page.waitForLoadState("networkidle");

  const main = page.getByRole("main");
  await expect(page).toHaveURL(/\/app$/);
  await expect(main.getByRole("heading", { name: /Good (morning|afternoon|evening),/ })).toBeVisible();
  await expect(main.getByRole("heading", { name: "Insurer onboarding & admin consent" })).toHaveCount(0);
});

test("Gate 12 deployed profile shows no fake connected providers or local controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/app/integrations");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /run test ingestion/i })).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByText(/mock/i)).toHaveCount(0);
  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByText(/^Connected$/)).toHaveCount(0);
});

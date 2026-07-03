import { expect, test } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 12 Integrations shows truthful provider states and confirmation for configuration", async ({ page }, testInfo) => {
  await page.goto("/integrations");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Integrations" })).toBeVisible();
  await expect(page.getByText("Provider readiness, mailbox routing, health checks, and governed configuration.")).toBeVisible();
  await expect(page.getByText("Not configured").first()).toBeVisible();
  await expect(page.getByRole("heading", { name: "Microsoft Graph Shared Mailboxes" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Mailbox routing table" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Integration health" })).toBeVisible();
  await expect(page.getByText("Secrets, tokens, connection strings, and mailbox credentials are never displayed")).toHaveCount(0);

  await page.getByRole("button", { name: "Edit mapping" }).click();
  await page.getByLabel("Mailbox display name").fill("Claims mailbox");
  await page.getByLabel("Mailbox address").fill("claims@example.invalid");
  await page.getByRole("button", { name: "Save mailbox" }).click();

  await expect(page.getByRole("heading", { name: "Save mailbox configuration" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Confirm mailbox configuration" })).toBeDisabled();
  await page.getByLabel("Reason").fill("Configure tenant mailbox mapping for claims intake validation.");
  await expect(page.getByRole("button", { name: "Confirm mailbox configuration" })).toBeEnabled();

  await testInfo.attach(`gate-12-integrations-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 12 Admin Console requires confirmation before role changes", async ({ page }, testInfo) => {
  await page.goto("/");
  await page.getByRole("combobox", { name: "Role" }).selectOption("Admin");

  await page.goto("/admin-settings");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("heading", { name: "Tenant controls and provider modes" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Provider modes", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Users and roles" })).toBeVisible();

  await page.getByRole("main").getByRole("combobox").selectOption("ClaimsOfficer");
  await expect(page.getByRole("heading", { name: "Confirm tenant role change" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Submit role change" })).toBeDisabled();
  await page.getByLabel("Reason").fill("Limit this user to operational claim handling during validation.");
  await expect(page.getByRole("button", { name: "Submit role change" })).toBeEnabled();
  await expect(page.getByText(/^Role changes are tenant-scoped administrative actions/)).toBeVisible();

  await testInfo.attach(`gate-12-admin-${testInfo.project.name}.png`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: "image/png"
  });
});

test("Gate 12 deployed profile shows no fake connected providers or local controls @deployed", async ({ page }) => {
  const runtimeProfile = process.env.IIP_VISUAL_RUNTIME_PROFILE?.toLowerCase();
  test.skip(
    !["staging", "production", "managedapplicationreviewer"].includes(runtimeProfile ?? ""),
    "Run with IIP_VISUAL_RUNTIME_PROFILE=Staging and IIP_VISUAL_AUTH_MODE=Entra for deployed-profile assertions."
  );

  await page.goto("/integrations");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: /run test ingestion/i })).toHaveCount(0);
  await expect(page.getByText(/fake/i)).toHaveCount(0);
  await expect(page.getByText(/mock/i)).toHaveCount(0);
  await expect(page.getByText(/simulated/i)).toHaveCount(0);
  await expect(page.getByText(/^Connected$/)).toHaveCount(0);
});

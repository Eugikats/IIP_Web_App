import { expect, test } from "@playwright/test";
import { mkdirSync } from "node:fs";
import path from "node:path";
import { prepareVisualApi } from "../visual/fixtures";

const root = path.resolve(__dirname, "../../../..", "docs/marketing/screenshots");

const staffRoutes = [
  { path: "/", heading: "Command Center", file: "01-command-center.png", role: "ClaimsSupervisor" },
  { path: "/claims", heading: "Claims Inbox", file: "02-claims-inbox.png", role: "Admin" },
  { path: "/document-checker", heading: "Claim document workspace", file: "03-document-checker.png", role: "Admin" },
  { path: "/approvals", heading: "Approvals", file: "04-approvals.png", role: "Admin" },
  { path: "/customer-service", heading: "Customer-safe communication desk", file: "05-customer-service.png", role: "Admin" },
  { path: "/integrations", heading: "Integrations", file: "06-integrations.png", role: "Admin" },
  { path: "/knowledge-base", heading: "Knowledge operations workbench", file: "07-knowledge-base.png", role: "Admin" },
  { path: "/reports", heading: "Operations summary", file: "08-reports.png", role: "Admin" },
  { path: "/compliance", heading: "Compliance & Audit", file: "09-compliance-audit.png", role: "Admin" }
] as const;

test.describe("marketing screenshot pack", () => {
  for (const route of staffRoutes) {
    test(`${route.file} ${route.path}`, async ({ page }, testInfo) => {
      await prepareVisualApi(page, route.role);
      await page.goto(route.path, { waitUntil: "domcontentloaded" });

      await expect(page.getByRole("heading", { name: route.heading })).toBeVisible();
      await expect(page.getByLabel("Provider status")).toBeVisible();
      await expect(page.getByLabel("Role")).toHaveCount(0);
      if (testInfo.project.name !== "mobile") {
        await expect(page.getByText("Staging / Entra")).toBeVisible();
      }

      await capture(page, testInfo.project.name, route.file);
    });
  }

  test("10-client-portal.png /client-portal", async ({ page }, testInfo) => {
    await prepareVisualApi(page, "CustomerService");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: "My Claims" })).toBeVisible();
    await expect(page.getByLabel("Provider status")).toHaveCount(0);
    await expect(page.getByLabel("Role")).toHaveCount(0);
    if (testInfo.project.name !== "mobile") {
      await expect(page.getByText("Staging / Entra")).toBeVisible();
    }

    await page.getByLabel("Claim reference").fill("11111111-1111-4111-8111-111111111111");
    await page.getByRole("button", { name: "Open claim" }).click();
    await expect(page.getByRole("heading", { name: "Motor Claim" })).toBeVisible();

    await capture(page, testInfo.project.name, "10-client-portal.png");
  });
});

async function capture(page: import("@playwright/test").Page, projectName: string, fileName: string) {
  const folder = path.join(root, projectName);
  mkdirSync(folder, { recursive: true });

  await page.screenshot({
    path: path.join(folder, fileName),
    fullPage: true
  });
}

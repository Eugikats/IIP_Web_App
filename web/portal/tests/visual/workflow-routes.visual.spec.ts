import { expect, test } from "@playwright/test";
import { prepareVisualApi, selectDevelopmentRole, visualCaseId } from "./fixtures";

const routeChecks = [
  { path: "/claims", heading: "Claims Inbox", snapshot: "claims-inbox" },
  { path: "/approvals", heading: "Approvals", snapshot: "approvals" },
  { path: "/document-checker", heading: "Claim document workspace", snapshot: "document-checker" },
  { path: "/customer-service", heading: "Customer-safe communication desk", snapshot: "customer-service" },
  {
    path: "/integrations",
    heading: "Integrations",
    mobileState: "Integration setup requires a desktop workspace",
    snapshot: "integrations"
  },
  { path: "/compliance", heading: "Compliance & Audit", snapshot: "compliance-audit" },
  { path: "/knowledge-base", heading: "Knowledge operations workbench", snapshot: "knowledge-base" },
  { path: "/reports", heading: "Operations summary", snapshot: "reports" }
];

test.describe("Workflow route visual acceptance", () => {
  test.beforeEach(async ({ page }) => {
    await prepareVisualApi(page, "Admin");
    await page.goto("/client-portal", { waitUntil: "domcontentloaded" });
    await selectDevelopmentRole(page, "Admin");
  });

  test("renders governed workflow routes with backend-shaped visual fixtures", async ({ page }, testInfo) => {
    for (const routeCheck of routeChecks) {
      await page.goto(routeCheck.path, { waitUntil: "domcontentloaded" });

      if (testInfo.project.name.includes("mobile") && routeCheck.mobileState) {
        await expect(page.getByText(routeCheck.mobileState)).toBeVisible();
      } else {
        await expect(page.getByRole("heading", { name: routeCheck.heading })).toBeVisible();
      }
      await expect(page.getByLabel("Provider status")).toBeVisible();
      await expect(page).toHaveScreenshot(`${routeCheck.snapshot}-${testInfo.project.name}.png`, {
        fullPage: true
      });
    }
  });

  test("renders the governed integrations route with backend-shaped visual fixtures", async ({ page }, testInfo) => {
    await page.goto("/integrations", { waitUntil: "domcontentloaded" });

    if (testInfo.project.name.includes("mobile")) {
      await expect(page.getByText("Integration setup requires a desktop workspace")).toBeVisible();
    } else {
      await expect(page.getByRole("heading", { name: "Integrations" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Microsoft Graph Shared Mailboxes" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Mailbox routing table" })).toBeVisible();
      await expect(page.getByRole("heading", { name: "Integration health" })).toBeVisible();
      await expect(page.getByLabel("Workspace")).toHaveCount(0);
      await expect(page.getByRole("button", { name: "Edit mapping" })).toBeVisible();
    }
    await expect(page.getByLabel("Provider status")).toBeVisible();
  });

  test("claims inbox fails closed when backend action state is missing", async ({ page }) => {
    await page.route("**/api/v1/cases", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            caseId: visualCaseId,
            customerName: "Malformed authority fixture"
          }
        ])
      });
    });

    await page.goto("/claims", { waitUntil: "domcontentloaded" });

    await expect(page.getByText("Claims response did not include required backend action authorization state.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Review" })).toHaveCount(0);
  });

  test("approval decisions fail closed when backend action state is missing", async ({ page }) => {
    await page.route("**/api/v1/approvals", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            approvalId: "approval-malformed-1",
            caseId: visualCaseId,
            status: "Pending"
          }
        ])
      });
    });

    await page.goto("/approvals", { waitUntil: "domcontentloaded" });

    await expect(page.getByText("Approval response did not include required backend decision authorization state.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Approve step" })).toBeHidden();
  });

  test("customer update controls fail closed when backend action state is missing", async ({ page }) => {
    await page.route(`**/api/v1/cases/${visualCaseId}/customer-updates`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            updateId: "customer-update-malformed-1",
            status: "Draft"
          }
        ])
      });
    });

    await page.goto("/customer-service", { waitUntil: "domcontentloaded" });
    await page.getByLabel("Case ID").fill(visualCaseId);
    await page.getByRole("button", { name: "Open customer view" }).click();

    await expect(page.getByText("Customer update response did not include required backend action authorization state.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Draft update" })).toBeHidden();
  });

  test("case review fails closed when backend action state is missing", async ({ page }) => {
    await page.route(`**/api/v1/cases/${visualCaseId}`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          caseId: visualCaseId,
          customerName: "Malformed authority fixture"
        })
      });
    });

    await page.goto(`/cases/${visualCaseId}`, { waitUntil: "domcontentloaded" });

    await expect(page.getByText("Case response did not include required backend action authorization state.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Evaluate workflow" })).toHaveCount(0);
  });

  test("new claim sends case facts without browser-owned authority fields", async ({ page }) => {
    let requestBody: Record<string, unknown> | null = null;
    await page.route("**/api/v1/cases", async (route) => {
      requestBody = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          caseId: visualCaseId,
          status: "New",
          correlationId: "visual-intake-correlation",
          audit: { eventCount: 1, lastActionName: "case.created" },
          allowedActions: [
            { action: "openCaseReview", label: "Open case review", requiredPermission: "ViewAssignedClaims", reason: null }
          ],
          blockedActions: []
        })
      });
    });

    await page.goto("/cases/new", { waitUntil: "domcontentloaded" });
    await page.getByLabel("Customer name").fill("Visual Intake Customer");
    await page.getByLabel("Policy number").fill("POL-VISUAL-INTAKE");
    await page.getByLabel("Product line").fill("Motor");
    await page.getByLabel("Claim type").fill("Accident");
    await page.getByLabel("Channel").fill("WebPortal");
    await page.getByLabel("Incident description").fill("Visual fixture incident submitted for workflow review.");
    await page.getByRole("button", { name: "Create case" }).click();

    await expect(page.getByRole("link", { name: "Open case detail" })).toBeVisible();
    expect(requestBody).toMatchObject({
      customerName: "Visual Intake Customer",
      policyNumber: "POL-VISUAL-INTAKE",
      productLine: "Motor"
    });
    expect(requestBody).not.toHaveProperty("tenantId");
    expect(requestBody).not.toHaveProperty("workspaceId");
    expect(requestBody).not.toHaveProperty("createdByUserId");
  });

  test("knowledge hub fails closed when lifecycle action state is missing", async ({ page }) => {
    await page.route("**/api/v1/knowledge-documents", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            knowledgeDocumentId: "knowledge-malformed-1",
            documentName: "Malformed knowledge authority fixture"
          }
        ])
      });
    });

    await page.goto("/knowledge-base", { waitUntil: "domcontentloaded" });

    await expect(page.getByText("Knowledge response did not include required backend action authorization state.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Approve", exact: true })).toBeHidden();
  });

  test("admin role controls fail closed when role-change action state is missing", async ({ page }, testInfo) => {
    await page.route("**/api/v1/admin/provider-modes", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          storageProvider: "SqlServer",
          authMode: "Entra",
          aiProvider: "AzureOpenAI",
          documentProvider: "AzureDocumentIntelligence",
          retrievalProvider: "AzureAiSearch",
          emailProvider: "Graph",
          communicationProvider: "GraphEmail",
          notificationProvider: "Configured",
          seedDemoData: false
        })
      });
    });
    await page.route("**/api/v1/admin/tenant-configuration", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          insurerName: "Visual Fixture Insurance",
          country: "Uganda",
          region: "East Africa",
          auditRetentionDays: 365,
          businessLines: [],
          channels: []
        })
      });
    });
    await page.route("**/api/v1/admin/users", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            userId: "malformed-admin-user",
            name: "Malformed Admin User",
            email: "malformed@example.invalid",
            role: "Admin"
          }
        ])
      });
    });

    await page.goto("/admin-settings", { waitUntil: "domcontentloaded" });

    if (testInfo.project.name.includes("mobile")) {
      await expect(page.getByText("Admin console requires a desktop workspace")).toBeVisible();
      await expect(page.getByRole("main").getByRole("combobox")).toBeHidden();
      return;
    }

    await expect(page.getByText("Admin user response did not include required backend role-change authorization state.")).toBeVisible();
    await expect(page.getByRole("main").getByRole("combobox")).toBeHidden();
  });
});

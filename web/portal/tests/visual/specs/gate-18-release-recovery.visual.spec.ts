import { expect, test, type Page, type Route } from "@playwright/test";
import { installVisualApiFixtures } from "../fixtures/iip-fixtures";

const apiHeaders = {
  "access-control-allow-origin": "*",
  "access-control-expose-headers": "x-correlation-id",
};

async function json(route: Route, status: number, body: unknown, correlationId?: string) {
  await route.fulfill({
    status,
    contentType: status >= 400 ? "application/problem+json" : "application/json",
    headers: { ...apiHeaders, ...(correlationId ? { "x-correlation-id": correlationId } : {}) },
    body: JSON.stringify(body),
  });
}

async function openClaimsWithStatus(page: Page, status: number, detail: string) {
  await page.route("**/api/v1/claims", (route) => json(route, status, { title: detail, detail }, `corr-${status}`));
  await page.goto("/app/claims");
  await expect(page.getByText("Claims could not be loaded")).toBeVisible();
}

test.beforeEach(async ({ page }) => {
  await installVisualApiFixtures(page);
});

test("Gate 18 distinguishes loading, empty and filtered-empty conversation states", async ({ page }) => {
  await page.route("**/api/v1/command-center/conversations?*", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 700));
    await json(route, 200, { items: [], total: 0, hasMore: false });
  });
  await page.goto("/app/conversations");
  await expect(page.getByText("Loading conversations…")).toBeVisible();
  await expect(page.getByText("No conversations yet")).toBeVisible();

  await page.unroute("**/api/v1/command-center/conversations?*");
  await page.reload();
  await page.getByPlaceholder("Find by title, claim, work item, or owner").fill("not present");
  await expect(page.getByText("No matching conversations")).toBeVisible();
});

test("Gate 18 classifies unauthorized, forbidden, not-configured and offline claim failures", async ({ page }) => {
  await openClaimsWithStatus(page, 401, "Authentication required");
  await expect(page.getByText(/Sign in again/)).toBeVisible();
  await expect(page.getByText("Reference corr-401")).toBeVisible();

  await page.unroute("**/api/v1/claims");
  await openClaimsWithStatus(page, 403, "Access forbidden");
  await expect(page.getByText(/workspace administrator for access/)).toBeVisible();

  await page.unroute("**/api/v1/claims");
  await openClaimsWithStatus(page, 503, "Provider is not configured");
  await expect(page.getByText("This capability is not configured for the current workspace.")).toBeVisible();
  await expect(page.getByText(/configure this capability/)).toBeVisible();

  await page.unroute("**/api/v1/claims");
  await page.route("**/api/v1/claims", (route) => route.abort("internetdisconnected"));
  await page.goto("/app/claims");
  await expect(page.getByText("Claims could not be loaded")).toBeVisible();
  await expect(page.getByText(/Check the connection/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();

  await page.route("**/api/v1/cases/CLM-2026-00491/documents", (route) => route.abort("internetdisconnected"));
  await page.goto("/app/claims/CLM-2026-00491/extraction?documentId=DOC-2026-00215");
  await expect(page.getByText("Document service could not be reached. Check the connection.")).toBeVisible();
  await expect(page.getByText("Failed to fetch", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Return to claim" })).toBeVisible();
});

test("Gate 18 keeps a stale archive action single and recoverable", async ({ page }) => {
  let archiveRequests = 0;
  await page.route("**/archive*", async (route) => {
    archiveRequests += 1;
    await json(route, 409, { title: "Version conflict" });
  });
  await page.goto("/app/conversations");
  await page.getByRole("button", { name: "Archive Review motor claim evidence" }).click();
  await expect.poll(() => archiveRequests).toBe(1);
  await expect(page.getByRole("paragraph").filter({ hasText: "This conversation changed. Refresh before trying again." })).toBeVisible();
  await expect(page.getByText("Review motor claim evidence")).toBeVisible();
});

test("Gate 18 reconciles an unknown query result with the same idempotency key", async ({ page }) => {
  const requestIds: string[] = [];
  let attempts = 0;
  await page.route("**/api/v1/command-center/query", async (route) => {
    attempts += 1;
    const request = route.request().postDataJSON() as { clientRequestId: string };
    requestIds.push(request.clientRequestId);
    if (attempts === 1) {
      await new Promise((resolve) => setTimeout(resolve, 500));
      await json(route, 504, { title: "Outcome unknown" });
      return;
    }
    await json(route, 200, {
      blocks: [{ type: "text", markdown: "Request reconciled without a duplicate action." }],
      canTriggerDecision: false,
      generatedAt: "2026-05-30T10:42:00+03:00",
      dataAsOf: "2026-05-30T10:42:00+03:00",
      snapshotId: "release-recovery-snapshot",
    });
  });

  await page.goto("/app");
  const composer = page.getByLabel("Message IIP Operations");
  await composer.fill("Reconcile this interrupted request");
  await page.getByRole("button", { name: "Send to IIP Operations" }).dblclick();
  await expect(page.getByText(/could not confirm whether this request was saved/)).toBeVisible();
  expect(attempts).toBe(1);
  await expect(composer).toHaveValue("Reconcile this interrupted request");

  await page.getByRole("button", { name: "Retry safely" }).click();
  await expect(page.getByText("Request reconciled without a duplicate action.")).toBeVisible();
  expect(attempts).toBe(2);
  expect(requestIds[1]).toBe(requestIds[0]);
});

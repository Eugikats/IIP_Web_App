import { expect, test, type Page } from "@playwright/test";

// Connected folders on the Data Hub, against canned answers from the connectors API. Nothing here reaches Google or
// Microsoft: what is checked is what a person is told and what the page asks the server to do.

const FOLDER = { folderId: "folder-1", name: "Policy wordings", driveId: null, productLine: "Motor", documentType: "PolicyWording", isFile: false };
const SECOND = { folderId: "folder-2", name: "Claims procedures", driveId: null, productLine: "General", documentType: "ClaimsProcedure", isFile: false };

function connector(overrides: Record<string, unknown> = {}) {
  return {
    id: "11111111-1111-1111-1111-111111111111", provider: "microsoft", providerName: "OneDrive and SharePoint", owner: "user-admin", account: "admin@example.test",
    status: "Active", statusLabel: "Active", statusNote: null, folders: [FOLDER, SECOND], lastRunAt: new Date(Date.now() - 3600_000).toISOString(), lastOutcome: "Succeeded",
    counts: { pending: 4, skipped: 1, failed: 0, removed: 0 },
    recentRuns: [{ startedAt: new Date().toISOString(), finishedAt: new Date().toISOString(), outcome: "Succeeded", found: 5, added: 2, updated: 1, removed: 0, skipped: 1, failed: 0, message: null }],
    ...overrides,
  };
}

async function open(page: Page, connectors: unknown, status = 200) {
  await page.route("**/api/v1/connectors", (route) =>
    route.request().method() === "GET"
      ? route.fulfill({ status, json: status === 200 ? { connectors } : { title: "x" } })
      : route.fallback());
  await page.goto("/app/knowledge");
  return page.getByTestId("connected-folders");
}

test("With nothing connected it says so, and offers to connect", async ({ page }) => {
  const panel = await open(page, []);
  await expect(panel.getByText("No folders are connected")).toBeVisible();
  await expect(panel.getByRole("button", { name: "Connect folders" })).toBeVisible();
});

test("A connector shows whose sign-in it reads with, what is waiting and when it last ran", async ({ page }) => {
  const panel = await open(page, [connector()]);
  const card = panel.getByTestId("connector-11111111-1111-1111-1111-111111111111");
  await expect(card.getByText("Reads as admin@example.test")).toBeVisible();
  await expect(card.getByText("4 files waiting for a person to review")).toBeVisible();
  await expect(card.getByText("1 skipped (unsupported or too large)")).toBeVisible();
  await expect(card.getByTestId("last-sync")).toContainText("2 new, 1 changed, 0 removed");
  await expect(card.getByText("Active", { exact: true })).toBeVisible();
});

test("A connector whose sign-in expired says nothing was changed, and cannot be synced until reconnected", async ({ page }) => {
  const panel = await open(page, [connector({
    status: "NeedsReconnect", statusLabel: "Needs attention", lastOutcome: "SignInExpired",
    statusNote: "The sign-in this connector reads with has expired or was removed. Nothing was changed. The person who set it up needs to connect their account again.",
  })]);
  const card = panel.getByTestId("connector-11111111-1111-1111-1111-111111111111");
  await expect(card.getByRole("alert")).toContainText("Nothing was changed");
  await expect(card.getByRole("button", { name: "Sync now" })).toBeDisabled();
});

test("A role without permission is told so and is not offered the button", async ({ page }) => {
  const panel = await open(page, [], 403);
  await expect(panel.getByText("Your role cannot manage connected folders.")).toBeVisible();
  await expect(panel.getByRole("button", { name: "Connect folders" })).toHaveCount(0);
});

test("When the list cannot be read it says so instead of showing an empty list", async ({ page }) => {
  const panel = await open(page, [], 500);
  await expect(panel.getByRole("alert")).toContainText("could not be read");
  await expect(panel.getByText("No folders are connected")).toHaveCount(0);
});

test("Removing a folder asks first, says its files leave at once, then sends the remaining folders", async ({ page }) => {
  let put: { folders: { folderId: string }[] } | null = null;
  await page.route("**/api/v1/connectors/*/folders", async (route) => {
    put = route.request().postDataJSON();
    await route.fulfill({ json: { retired: 3, folders: [] } });
  });
  const panel = await open(page, [connector()]);
  const card = panel.getByTestId("connector-11111111-1111-1111-1111-111111111111");

  await card.getByRole("listitem").filter({ hasText: "Claims procedures" }).getByRole("button", { name: "Remove" }).click();
  await expect(card.getByText("Its files leave the Data Hub at once.")).toBeVisible();
  await card.getByRole("button", { name: "Remove folder" }).click();

  await expect(panel.getByRole("status")).toContainText("3 file(s) were taken out of use");
  expect(put).toEqual({ folders: [expect.objectContaining({ folderId: "folder-1", displayName: "Policy wordings", isFile: false })] });
});

test("Connecting a OneDrive folder files it under the product line and kind of document the person chose", async ({ page }) => {
  let posted: { provider: string; folders: Record<string, unknown>[] } | null = null;
  await page.route("**/api/v1/me/file-connections", (route) =>
    route.fulfill({ json: [
      { provider: "google", configured: true, connected: false, accountEmail: null, reason: null },
      { provider: "microsoft", configured: true, connected: true, accountEmail: "admin@example.test", reason: null },
    ] }));
  await page.route("**/api/v1/me/file-connections/microsoft/files*", (route) =>
    route.fulfill({ json: { folderId: null, items: [
      { id: "folder-9", name: "Product sheets", isFolder: true, size: null, mimeType: null, modifiedAt: null },
      { id: "file-1", name: "notes.docx", isFolder: false, size: 10, mimeType: null, modifiedAt: null },
    ] } }));
  await page.route("**/api/v1/connectors", async (route) => {
    if (route.request().method() === "POST") {
      posted = route.request().postDataJSON();
      await route.fulfill({ status: 201, json: {} });
    } else {
      await route.fulfill({ json: { connectors: [] } });
    }
  });
  await page.goto("/app/knowledge");
  const panel = page.getByTestId("connected-folders");

  await panel.getByRole("button", { name: "Connect folders" }).click();
  const dialog = page.getByRole("dialog", { name: "Connect folders" });
  await expect(dialog.getByRole("radio", { name: /Google Drive/ })).toBeDisabled();
  await expect(dialog.getByText("Connect your own account first")).toBeVisible();
  await dialog.getByRole("radio", { name: /OneDrive and SharePoint/ }).click();
  await dialog.getByRole("button", { name: "Choose a folder" }).click();

  const picker = page.getByRole("dialog", { name: "Choose a folder from OneDrive" });
  await picker.getByRole("button", { name: "Product sheets" }).click();
  await picker.getByRole("button", { name: "Choose this folder" }).click();

  await dialog.getByLabel("Product line for Product sheets").fill("Motor");
  await dialog.getByLabel("Kind of document for Product sheets").selectOption("ProductOverview");
  await dialog.getByRole("button", { name: "Connect", exact: true }).click();

  await expect(panel.getByRole("status")).toContainText("Connected.");
  expect(posted).toEqual({
    provider: "microsoft",
    folders: [expect.objectContaining({ folderId: "folder-9", displayName: "Product sheets", productLine: "Motor", documentType: "ProductOverview", isFile: false })],
  });
});

test("Connecting is refused with the server's reason when the person has not connected their own account", async ({ page }) => {
  await page.route("**/api/v1/me/file-connections", (route) =>
    route.fulfill({ json: [
      { provider: "google", configured: false, connected: false, accountEmail: null, reason: "Google sign-in has not been set up for IIP yet." },
      { provider: "microsoft", configured: true, connected: true, accountEmail: "admin@example.test", reason: null },
    ] }));
  await page.route("**/api/v1/me/file-connections/microsoft/files*", (route) =>
    route.fulfill({ json: { folderId: null, items: [{ id: "folder-9", name: "Product sheets", isFolder: true, size: null, mimeType: null, modifiedAt: null }] } }));
  await page.route("**/api/v1/connectors", async (route) => {
    if (route.request().method() === "POST") {
      await route.fulfill({ status: 409, json: { code: "not_connected", message: "Connect your own OneDrive and SharePoint account first (Settings, Integrations). A connector reads with your sign-in." } });
    } else {
      await route.fulfill({ json: { connectors: [] } });
    }
  });
  await page.goto("/app/knowledge");
  await page.getByTestId("connected-folders").getByRole("button", { name: "Connect folders" }).click();
  const dialog = page.getByRole("dialog", { name: "Connect folders" });
  await expect(dialog.getByText("Google sign-in has not been set up for IIP yet.")).toBeVisible();
  await dialog.getByRole("radio", { name: /OneDrive and SharePoint/ }).click();
  await dialog.getByRole("button", { name: "Choose a folder" }).click();
  const picker = page.getByRole("dialog", { name: "Choose a folder from OneDrive" });
  await picker.getByRole("button", { name: "Product sheets" }).click();
  await picker.getByRole("button", { name: "Choose this folder" }).click();
  await dialog.getByRole("button", { name: "Connect", exact: true }).click();

  await expect(dialog.getByRole("alert")).toContainText("Connect your own");
});

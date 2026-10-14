import { expect, test, type Page } from "@playwright/test";

// Exporting a claim's evidence pack can also keep a copy in the person's own storage. The export, download and save
// calls are answered here; what is checked is what the person is offered, what is asked of the server and what they are told.

const API = "http://localhost:5167";
const HEADERS = { "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations", "x-demo-role": "Admin", "X-User-Id": "user-1" };

async function openExportDialog(page: Page, connected: { microsoft: boolean; google: boolean }) {
  const claims = (await (await page.request.get(`${API}/api/v1/cases`, { headers: HEADERS })).json()) as { caseId: string }[];
  await page.route("**/api/v1/me/file-connections", (route) =>
    route.fulfill({ json: [
      { provider: "google", configured: true, connected: connected.google, accountEmail: connected.google ? "ops@example.test" : null, reason: null },
      { provider: "microsoft", configured: true, connected: connected.microsoft, accountEmail: connected.microsoft ? "admin@example.test" : null, reason: null },
    ] }));
  await page.route("**/api/v1/compliance/cases/*/audit-pack/export", (route) =>
    route.fulfill({ json: { caseId: claims[0].caseId, objectKey: "packs/case-1.zip", contentType: "application/zip", fileSizeBytes: 10, exportedAt: new Date().toISOString() } }));
  await page.route("**/api/v1/compliance/cases/*/audit-pack/export/download*", (route) =>
    route.fulfill({ body: "pack", contentType: "application/zip" }));

  await page.goto(`/app/claims/${claims[0].caseId}`);
  // The claim page keeps secondary actions behind "More actions".
  await page.getByRole("button", { name: "More actions" }).click();
  await page.getByRole("menuitem", { name: /Export claim pack/ }).click();
  return page.getByRole("dialog", { name: "Export claim evidence pack" });
}

test("The export dialog offers only the storage the person has connected, and defaults to download only", async ({ page }) => {
  const dialog = await openExportDialog(page, { microsoft: true, google: false });

  await expect(dialog.getByRole("radio", { name: "Download only" })).toBeChecked();
  await expect(dialog.getByRole("radio", { name: /Save to my OneDrive/ })).toBeEnabled();
  await expect(dialog.getByRole("radio", { name: /Save to my Google Drive/ })).toBeDisabled();
  await expect(dialog.getByText("connect your account in Settings, Integrations first")).toBeVisible();
});

test("Choosing OneDrive exports, downloads, then saves into the person's own storage and says where", async ({ page }) => {
  let saved: unknown = null;
  await page.route("**/api/v1/compliance/cases/*/audit-pack/export/save", async (route) => {
    saved = route.request().postDataJSON();
    await route.fulfill({ json: { saved: { name: "case-1-evidence.zip", webUrl: "https://onedrive.example.test/x", provider: "microsoft" } } });
  });
  const dialog = await openExportDialog(page, { microsoft: true, google: true });

  await dialog.getByRole("radio", { name: /Save to my OneDrive/ }).check();
  await dialog.getByRole("textbox", { name: "Reason for audit trail" }).fill("Supervisor asked for the pack.");
  await dialog.getByRole("button", { name: "Export claim pack" }).click();

  await expect(page.getByText("Saved to your OneDrive")).toBeVisible();
  await expect(page.getByText(/case-1-evidence\.zip, in the folder/)).toBeVisible();
  expect(saved).toEqual({ objectKey: "packs/case-1.zip", provider: "microsoft" });
});

test("Download only never calls the save endpoint", async ({ page }) => {
  let called = false;
  await page.route("**/api/v1/compliance/cases/*/audit-pack/export/save", async (route) => {
    called = true;
    await route.fulfill({ json: {} });
  });
  const dialog = await openExportDialog(page, { microsoft: true, google: true });

  await dialog.getByRole("textbox", { name: "Reason for audit trail" }).fill("Supervisor asked for the pack.");
  await dialog.getByRole("button", { name: "Export claim pack" }).click();

  await expect(page.getByText("Claim pack exported")).toBeVisible();
  expect(called).toBe(false);
});

test("When the storage refuses, the export still stands and the person is told it was not saved, and why", async ({ page }) => {
  await page.route("**/api/v1/compliance/cases/*/audit-pack/export/save", (route) =>
    route.fulfill({ status: 413, json: { code: "too_large", message: "That file is larger than 25 MB, so it cannot be saved to your storage from here. Download it instead." } }));
  const dialog = await openExportDialog(page, { microsoft: true, google: false });

  await dialog.getByRole("radio", { name: /Save to my OneDrive/ }).check();
  await dialog.getByRole("textbox", { name: "Reason for audit trail" }).fill("Supervisor asked for the pack.");
  await dialog.getByRole("button", { name: "Export claim pack" }).click();

  await expect(page.getByText("Claim pack exported")).toBeVisible();
  await expect(page.getByText("Not saved to your OneDrive")).toBeVisible();
  await expect(page.getByText(/larger than 25 MB/)).toBeVisible();
});

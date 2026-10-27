import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
const ADMIN = { "x-demo-role": "Admin", "X-User-Id": "user-1", "Content-Type": "application/json" };

// Aisha Namutebi is the first person on the Mavuno Bank demonstration list: a consenting bank customer.
const PHONE = "256790000001";

let organisationId = "";

test.beforeAll(async ({ request }) => {
  // A person of its own each run, so each run has its own Mavuno Bank organisation and no earlier offer to anyone in it.
  const activated = await request.post(`${API}/api/v1/demo/scenarios/mavuno-bank/activate`, {
    headers: { ...ADMIN, "X-User-Id": `spec-${Date.now()}`, "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" },
  });
  expect(activated.ok()).toBeTruthy();
  organisationId = (await activated.json()).organisationId;

  const when = new Date(Date.now() - 2 * 86400000 - Date.now() % 1000).toISOString();
  // A new reference every run, so the event is new even when the same organisation is reused.
  const recorded = await request.post(`${API}/api/v1/events`, {
    headers: { ...ADMIN, "X-Tenant-Id": organisationId, "X-Workspace-Id": "main" },
    data: { eventType: "loan_disbursed", phone: `+${PHONE}`, occurredAt: when, externalId: `spec-${Date.now()}`, amount: 8000000, termMonths: 36, branch: "Ntinda" },
  });
  expect(recorded.ok()).toBeTruthy();
});

test.beforeEach(async ({ page }) => {
  await page.addInitScript(([org]) => {
    window.localStorage.setItem("iip.active-organisation-id", org);
    window.localStorage.setItem("iip.active-workspace-id", "main");
  }, [organisationId]);
});

test("a rule is written, approved once, run, and every decision is in the log with its checks", async ({ page }) => {
  await page.goto("/app/acquisition");
  await page.getByRole("tab", { name: "Rules" }).click();
  await expect(page.getByRole("heading", { name: "Rules", exact: true })).toBeVisible();
  await expect(page.getByText(/Who may approve a rule here:/)).toContainText("Admin");

  await page.getByRole("button", { name: "New rule" }).click();
  const form = page.getByRole("form", { name: "Write a rule" });
  const name = `Loan Protector ${Date.now().toString(36)}`;
  await form.getByLabel("Name").fill(name);
  await form.getByLabel("When this happens").selectOption("loan_disbursed");
  await form.getByLabel("Wait (days)").fill("0");
  await form.getByLabel("Only if the amount is at least (UGX)").fill("2000000");
  await form.getByLabel("Only if the term is at least (months)").fill("12");
  await form.getByLabel("From hour").fill("0");
  await form.getByLabel("Until hour").fill("24");
  await form.getByLabel(/Mpingo Loan Protector/).check();
  await form.getByLabel(/Mpingo Family Funeral Cover/).check();
  await form.getByRole("button", { name: "Save as a draft" }).click();

  const card = page.getByRole("listitem").filter({ hasText: name });
  await expect(card.getByText("Draft, not approved")).toBeVisible();
  await expect(card.getByText(/When: loan disbursed \(UGX 2,000,000 or more, 12 months or more\)/)).toBeVisible();

  await card.getByRole("button", { name: "Approve this rule" }).click();
  await expect(card.getByText("Running")).toBeVisible();
  await expect(card.getByText(/Approved by user-1 \(Admin\)/)).toBeVisible();
  await page.screenshot({ path: `test-results/workspace/rules-${test.info().project.name}.png`, fullPage: true });

  await page.getByRole("button", { name: "Run the rules now" }).click();
  await expect(page.getByRole("status").filter({ hasText: /Looked at/ })).toBeVisible();

  const log = page.getByRole("table").last();
  const row = log.getByRole("row").filter({ hasText: name }).first();
  await expect(row).toContainText("Aisha Namutebi");
  await expect(row).toContainText("Shown as sent, not delivered");
  await row.getByRole("button", { name: "Show the checks" }).click();
  await expect(row).toContainText("Agreed to be contacted: passed");
  await expect(row).toContainText("Best-fit cover: passed");
  await page.screenshot({ path: `test-results/workspace/rules-log-${test.info().project.name}.png`, fullPage: true });
});

test("changing an approved rule takes its approval away", async ({ page }) => {
  await page.goto("/app/acquisition");
  await page.getByRole("tab", { name: "Rules" }).click();
  await page.getByRole("button", { name: "New rule" }).click();
  const form = page.getByRole("form", { name: "Write a rule" });
  const name = `Editable ${Date.now().toString(36)}`;
  await form.getByLabel("Name").fill(name);
  await form.getByLabel(/Mpingo Loan Protector/).check();
  await form.getByRole("button", { name: "Save as a draft" }).click();
  const card = page.getByRole("listitem").filter({ hasText: name });
  await card.getByRole("button", { name: "Approve this rule" }).click();
  await expect(card.getByText("Running")).toBeVisible();

  await card.getByRole("button", { name: "Edit" }).click();
  await page.getByRole("form", { name: "Write a rule" }).getByLabel("Wait (days)").fill("5");
  await page.getByRole("form", { name: "Write a rule" }).getByRole("button", { name: "Save as a draft" }).click();

  await expect(card.getByText("Draft, not approved")).toBeVisible();
  await expect(card.getByRole("button", { name: "Approve this rule" })).toBeVisible();
});

test("a rule that offers no cover is refused with a reason", async ({ page }) => {
  await page.goto("/app/acquisition");
  await page.getByRole("tab", { name: "Rules" }).click();
  await page.getByRole("button", { name: "New rule" }).click();
  const form = page.getByRole("form", { name: "Write a rule" });
  await form.getByLabel("Name").fill("Nothing to offer");
  await form.getByRole("button", { name: "Save as a draft" }).click();
  await expect(page.getByRole("alert").filter({ hasText: /Choose at least one cover/ })).toBeVisible();
});

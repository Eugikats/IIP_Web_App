import { expect, test } from "@playwright/test";

const API = "http://localhost:5167";
let organisationId = "";

test.beforeAll(async ({ request }) => {
  const activated = await request.post(`${API}/api/v1/demo/scenarios/mavuno-bank/activate`, {
    headers: { "x-demo-role": "Admin", "X-User-Id": `spec-setup-${Date.now()}`, "Content-Type": "application/json", "X-Tenant-Id": "tenant-demo", "X-Workspace-Id": "workspace-operations" },
  });
  expect(activated.ok()).toBeTruthy();
  organisationId = (await activated.json()).organisationId;
});

test.beforeEach(async ({ page }) => {
  await page.addInitScript(([org]) => {
    window.localStorage.setItem("iip.active-organisation-id", org);
    window.localStorage.setItem("iip.active-workspace-id", "main");
  }, [organisationId]);
});

test("the profile, terms and security reading are recorded only when complete", async ({ page }) => {
  await page.goto("/app/admin/setup");
  await expect(page.getByRole("heading", { name: "Set up your organisation", level: 1 })).toBeVisible();

  const steps = page.getByRole("list", { name: "Set-up steps" });
  const profileStep = steps.getByRole("listitem").filter({ hasText: "Tell us about your organisation" });
  const termsStep = steps.getByRole("listitem").filter({ hasText: "Accept the terms and the data processing agreement" });
  const securityStep = steps.getByRole("listitem").filter({ hasText: "Read how your data is protected" });
  const storageStep = steps.getByRole("listitem").filter({ hasText: "Choose where your documents come from" });
  await expect(profileStep).toContainText("Not done yet");
  await expect(termsStep).toContainText("Not done yet");
  await expect(securityStep).toContainText("Not done yet");
  await expect(storageStep).toContainText("Not done yet");
  await expect(page.getByRole("heading", { name: "How your data is protected" })).toBeVisible();
  await expect(page.getByText("Not configured on this platform yet").first()).toBeVisible();

  const profile = page.getByRole("form", { name: "About your organisation" });
  await profile.getByLabel("Registered name").fill("Mavuno Bank Limited");
  await profile.getByLabel("What kind of licence").selectOption("BancassuranceDistributor");
  await profile.getByLabel("Licence or registration number").fill("??");
  await profile.getByLabel("Contact person").fill("Aisha Namutebi");
  await profile.getByLabel("Contact email").fill("aisha@mavuno.example");
  await profile.getByLabel("About you").fill("Mavuno Bank serves salaried workers and small traders across Uganda and sells insurance from its partner insurers.");
  await profile.getByRole("button", { name: "Save the profile" }).click();
  await expect(profile.getByRole("alert")).toContainText("licence or registration number");

  await profile.getByLabel("Licence or registration number").fill("IRA/BA/0042");
  await profile.getByRole("button", { name: "Save the profile" }).click();
  await expect(page.getByRole("status").filter({ hasText: "profile was saved" })).toBeVisible();
  await expect(profileStep).toContainText("Done");
  await expect(termsStep).toContainText("Not done yet");

  const terms = page.getByRole("form", { name: "Accept the terms" });
  await expect(terms.getByRole("button", { name: "Accept for the organisation" })).toBeDisabled();
  await terms.getByLabel("Your full name").fill("Aisha Namutebi");
  await terms.getByLabel("Your position").fill("Managing Director");
  await terms.getByLabel(/I am authorised to accept/).check();
  await terms.getByRole("button", { name: "Accept for the organisation" }).click();

  await expect(page.getByText(/Accepted by Aisha Namutebi, Managing Director/)).toBeVisible();
  // What was saved is what comes back after a reload.
  await page.reload();
  await expect(profile.getByLabel("What kind of licence")).toHaveValue("BancassuranceDistributor");
  await expect(profile.getByLabel("Licence or registration number")).toHaveValue("IRA/BA/0042");
  await expect(termsStep).toContainText("Done");

  const security = page.getByRole("form", { name: "Confirm reading security statements" });
  await security.getByLabel("Your full name").fill("Aisha Namutebi");
  await security.getByRole("button", { name: "I have read this" }).click();
  await expect(page.getByText(/Read by Aisha Namutebi on/)).toBeVisible();
  await expect(securityStep).toContainText("Done");
  await expect(page.getByRole("link", { name: "Upload documents in the Data Hub" })).toHaveAttribute("href", "/app/knowledge");
  await page.screenshot({ path: `test-results/workspace/setup-${test.info().project.name}.png`, fullPage: true });
});

test("the legal documents the organisation accepts are public pages", async ({ page }) => {
  for (const [path, heading] of [["/terms", "Terms and Conditions"], ["/privacy", "Data Privacy & Protection Policy"], ["/data-processing", "Data Processing Agreement"]] as const) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: heading, level: 1 })).toBeVisible();
  }
});

test("a workspace can request and cancel managed WhatsApp setup without sending or storing a code", async ({ page }) => {
  await page.goto("/app/admin/setup");
  const form = page.getByRole("form", { name: "Request WhatsApp setup" });
  await expect(form).toBeVisible();
  await form.getByLabel("Business phone number").fill("+256700000001");
  await form.getByLabel("Display name for Meta review").fill("IIP browser fixture");
  await form.getByLabel("Setup contact email").fill("setup-fixture@example.test");
  await form.getByRole("checkbox").check();
  await form.getByRole("button", { name: "Request setup" }).click();
  await expect(page.getByText("Request status:")).toContainText("Requested");
  await expect(page.getByText("Codes and registration PINs are submitted to Meta for verification and never stored by IIP.")).toBeVisible();
  await page.screenshot({ path: `test-results/workspace/whatsapp-onboarding-${test.info().project.name}.png`, fullPage: true });
  await page.getByRole("button", { name: "Cancel request" }).click();
  await expect(form).toBeVisible();
});

test("Publisher Support sees only the managed-number queue on its support screen", async ({ page, request }) => {
  const tenant = `support-fixture-${Date.now()}`;
  const displayName = `Support browser fixture ${tenant}`;
  const workspace = "main";
  const headers = { "x-demo-role": "Admin", "X-User-Id": "support-queue-fixture", "X-Tenant-Id": tenant, "X-Workspace-Id": workspace, "Content-Type": "application/json" };
  const created = await request.post(`${API}/api/v1/integrations/whatsapp/onboarding`, {
    headers,
    data: { phoneNumber: "+256700000002", displayName, contactEmail: "support-fixture@example.test", consent: true },
  });
  expect(created.ok()).toBeTruthy();

  try {
    await page.addInitScript(() => window.localStorage.setItem("iip.role", JSON.stringify("PublisherSupport")));
    await page.goto("/app/admin/whatsapp-support");
    await expect(page.getByRole("heading", { name: "WhatsApp number requests" })).toBeVisible();
    await expect(page.getByRole("heading", { name: `${displayName} · Requested` })).toBeVisible();
    await expect(page.locator("header").getByText("Not configured")).toBeVisible();
    await page.screenshot({ path: `test-results/workspace/whatsapp-support-${test.info().project.name}.png`, fullPage: true });
  } finally {
    await request.delete(`${API}/api/v1/integrations/whatsapp/onboarding`, { headers });
  }
});

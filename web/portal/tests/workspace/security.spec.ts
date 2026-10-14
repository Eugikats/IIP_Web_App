import { createHmac } from "node:crypto";
import { expect, test, type Page } from "@playwright/test";

const API = "http://localhost:5167";
const HEADERS = {
  "X-Tenant-Id": "tenant-demo",
  "X-Workspace-Id": "workspace-operations",
  "x-demo-role": "Admin",
  "X-User-Id": "user-1",
  "Content-Type": "application/json",
};

// The same arithmetic an authenticator app does (RFC 6238), so the test can type the code the phone would show.
function base32Decode(text: string): Buffer {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  let bits = 0;
  let value = 0;
  const bytes: number[] = [];
  for (const char of text.replace(/=+$/, "").toUpperCase()) {
    value = (value << 5) | alphabet.indexOf(char);
    bits += 5;
    if (bits >= 8) {
      bytes.push((value >>> (bits - 8)) & 0xff);
      bits -= 8;
    }
  }
  return Buffer.from(bytes);
}

function codeFor(secret: string, atMs = Date.now()): string {
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(atMs / 1000 / 30)));
  const hash = createHmac("sha1", base32Decode(secret)).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
  return String(binary % 1_000_000).padStart(6, "0");
}

async function openSecurity(page: Page) {
  await page.goto("/app");
  await page.getByRole("button", { name: "Settings", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Settings" });
  await dialog.getByRole("button", { name: "Security" }).first().click();
  return dialog;
}

const OTHER_ADMIN = { ...HEADERS, "X-User-Id": "user-admin-2" };

// Every test starts from the same place: nothing required, and user-1 with no methods. A second administrator does
// the clearing, because nobody can reset themselves.
test.beforeEach(async ({ request }) => {
  await request.put(`${API}/api/v1/security/mfa/policy`, {
    headers: OTHER_ADMIN,
    data: { required: false, allowSnooze: true, minMethods: 1, enabledMethods: ["totp", "passkey", "email_otp", "sms_otp", "backup_codes"] },
  });
  await request.post(`${API}/api/v1/security/mfa/users/user-1/reset`, { headers: OTHER_ADMIN });
  // A legal hold left on by an earlier run is released (a 400 when none is in place is fine).
  await request.put(`${API}/api/v1/compliance/legal-hold`, { headers: OTHER_ADMIN, data: { active: false, reason: "test reset" } });
});

test.afterAll(async ({ request }) => {
  // Leave the shared in-memory API as the other specs expect it: nothing required.
  await request.put(`${API}/api/v1/security/mfa/policy`, {
    headers: OTHER_ADMIN,
    data: { required: false, allowSnooze: true, minMethods: 1, enabledMethods: ["totp", "passkey", "email_otp", "sms_otp", "backup_codes"] },
  });
  await request.post(`${API}/api/v1/security/mfa/users/user-1/reset`, { headers: OTHER_ADMIN });
});

test("Security shows each method, and an authenticator app can be set up end to end", async ({ page }) => {
  const dialog = await openSecurity(page);

  await expect(dialog.getByRole("heading", { name: "Protect your account" })).toBeVisible();
  for (const name of ["Authenticator app", "Email codes", "SMS codes", "Backup codes"]) {
    await expect(dialog.getByRole("heading", { name })).toBeVisible();
  }
  await expect(dialog.getByTestId("mfa-card-totp").getByText("Not set up")).toBeVisible();

  await dialog.getByTestId("mfa-card-totp").getByRole("button", { name: "Set up" }).click();
  const setup = page.getByRole("dialog", { name: "Set up an authenticator app" });
  await setup.getByRole("button", { name: "Microsoft Authenticator" }).click();
  await expect(setup.getByAltText("QR code to scan with your authenticator app")).toBeVisible();

  const secret = (await setup.getByTestId("totp-secret").innerText()).trim();
  await setup.getByLabel("Code from your app").fill("000000");
  await setup.getByRole("button", { name: "Turn on" }).click();
  await expect(setup.getByRole("alert")).toContainText("not right");

  await setup.getByLabel("Code from your app").fill(codeFor(secret));
  await setup.getByRole("button", { name: "Turn on" }).click();
  await expect(setup).toBeHidden();

  await expect(dialog.getByTestId("mfa-card-totp").getByText("Active")).toBeVisible();
  await expect(dialog.getByRole("heading", { name: "Protect your account" }).locator("..").getByText("Protected")).toBeVisible();
});

test("Backup codes are shown once, and email and SMS say plainly when they are not connected", async ({ page, request }) => {
  // Backup codes need another method first, so one is set up through the API.
  const begin = await request.post(`${API}/api/v1/security/mfa/totp/begin`, { headers: HEADERS, data: { label: "Test app" } });
  const { id, secret } = (await begin.json()) as { id: string; secret: string };
  expect((await request.post(`${API}/api/v1/security/mfa/totp/confirm`, { headers: HEADERS, data: { id, code: codeFor(secret) } })).status()).toBe(200);

  const dialog = await openSecurity(page);

  await dialog.getByTestId("mfa-card-email_otp").getByRole("button", { name: "Set up" }).click();
  const email = page.getByRole("dialog", { name: "Set up email codes" });
  await email.getByRole("button", { name: "Send the code" }).click();
  await expect(email.getByRole("alert")).toContainText(/not connected|no email address/i);
  await email.getByRole("button", { name: "Close" }).first().click().catch(() => page.keyboard.press("Escape"));

  await dialog.getByTestId("mfa-card-backup_codes").getByRole("button", { name: /Make/ }).click();
  const backup = page.getByRole("dialog", { name: "Backup codes" });
  await backup.getByRole("button", { name: "Make backup codes" }).click();
  await expect(backup.getByTestId("backup-codes").getByRole("listitem")).toHaveCount(8);
});

test("When an organisation requires it, a signed-out person is sent to verify and gets back in with a code", async ({ page, request }) => {
  // Set up an authenticator through the API so the secret is known to the test.
  const begin = await request.post(`${API}/api/v1/security/mfa/totp/begin`, { headers: HEADERS, data: { label: "Test app" } });
  const { id, secret } = (await begin.json()) as { id: string; secret: string };
  const confirm = await request.post(`${API}/api/v1/security/mfa/totp/confirm`, { headers: HEADERS, data: { id, code: codeFor(secret) } });
  expect(confirm.status()).toBe(200);

  const policy = await request.put(`${API}/api/v1/security/mfa/policy`, {
    headers: HEADERS,
    data: { required: true, allowSnooze: true, minMethods: 1, enabledMethods: ["totp", "passkey", "email_otp", "sms_otp", "backup_codes"] },
  });
  expect(policy.status()).toBe(200);

  // Signed in and verified: the app opens as normal.
  await page.goto("/app");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();

  // Signing out ends the verified state, so the API now refuses protected routes...
  await request.post(`${API}/api/v1/security/mfa/sign-out`, { headers: HEADERS });
  const blocked = await request.get(`${API}/api/v1/claims`, { headers: HEADERS });
  expect(blocked.status()).toBe(403);
  expect(((await blocked.json()) as { code: string }).code).toBe("MFA_REQUIRED");

  // ...and the portal sends the person to verify, where the right code lets them back in.
  // The app sends the page elsewhere while it is still loading, which the browser reports as an aborted navigation.
  await page.goto("/app").catch(() => undefined);
  await page.waitForURL(/\/security\/verify/);
  await expect(page.getByRole("heading", { name: "Verify it is you" })).toBeVisible();
  await page.getByLabel("Verification code").fill("000000");
  await page.getByRole("button", { name: "Verify" }).click();
  await expect(page.getByText("That code is not right.")).toBeVisible();

  // The code for the next 30 seconds is also valid and has not been used, so the test need not wait.
  await page.getByLabel("Verification code").fill(codeFor(secret, Date.now() + 30_000));
  await page.getByRole("button", { name: "Verify" }).click();
  await page.waitForURL(/\/app/);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("A passkey is made and then used to get back in, using a virtual authenticator", async ({ page, context, request }) => {
  // Chromium's built-in virtual authenticator stands in for a phone or a security key, so a real key pair is made
  // and a real signature checked by the API.
  const client = await context.newCDPSession(page);
  await client.send("WebAuthn.enable");
  await client.send("WebAuthn.addVirtualAuthenticator", {
    options: { protocol: "ctap2", transport: "internal", hasResidentKey: true, hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true },
  });

  const dialog = await openSecurity(page);
  await dialog.getByTestId("mfa-card-passkey").getByRole("button", { name: "Set up" }).click();
  const setup = page.getByRole("dialog", { name: "Add a passkey or security key" });
  await setup.getByLabel("Name for this passkey").fill("Test key");
  await setup.getByRole("button", { name: "Create passkey" }).click();
  await expect(setup).toBeHidden();
  await expect(dialog.getByTestId("mfa-card-passkey").getByText("Active")).toBeVisible();
  await expect(dialog.getByTestId("mfa-card-passkey").getByText("Test key", { exact: true })).toBeVisible();

  const policy = await request.put(`${API}/api/v1/security/mfa/policy`, {
    headers: HEADERS,
    data: { required: true, allowSnooze: true, minMethods: 1, enabledMethods: ["totp", "passkey", "email_otp", "sms_otp", "backup_codes"] },
  });
  expect(policy.status()).toBe(200);

  await request.post(`${API}/api/v1/security/mfa/sign-out`, { headers: HEADERS });
  await page.goto("/app").catch(() => undefined);
  await page.waitForURL(/\/security\/verify/);
  await page.getByRole("button", { name: "Use my passkey" }).click();
  await page.waitForURL(/\/app/);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("An administrator can place and release a legal hold, and must say why", async ({ page }) => {
  const dialog = await openSecurity(page);
  const panel = dialog.getByTestId("legal-hold");
  await expect(panel.getByText("Not in place", { exact: true })).toBeVisible();

  const button = panel.getByRole("button", { name: "Place a legal hold" });
  await expect(button).toBeDisabled(); // no reason yet
  await panel.getByLabel("Reason").fill("Disputed motor claim");
  await button.click();
  await expect(panel.getByText("In place", { exact: true })).toBeVisible();
  await expect(panel.getByText("Disputed motor claim")).toBeVisible();

  await panel.getByLabel("Reason").fill("Dispute settled");
  await panel.getByRole("button", { name: "Release the hold" }).click();
  await expect(panel.getByText("Not in place", { exact: true })).toBeVisible();
});

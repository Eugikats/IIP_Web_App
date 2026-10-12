import { expect, test } from "@playwright/test";

test("The privacy policy is public and states how Google and Microsoft data is used", async ({ page }) => {
  const response = await page.goto("/privacy");
  expect(response?.status()).toBe(200);

  await expect(page.getByRole("heading", { level: 1, name: "Data Privacy & Protection Policy" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "12.0 Google and Microsoft account data" })).toBeVisible();
  await expect(page.getByText("Limited Use requirements")).toBeVisible();
  await expect(page.getByText("We do not sell Google or Microsoft user data.")).toBeVisible();
  await expect(page.getByRole("link", { name: "legal@oticgroup.net" }).first()).toHaveAttribute("href", "mailto:legal@oticgroup.net");
  await expect(page.getByRole("link", { name: "Google API Services User Data Policy" })).toHaveCount(0); // the address itself is the link text
  await expect(page.getByRole("link", { name: /developers\.google\.com\/terms\/api-services-user-data-policy/ })).toBeVisible();
});

test("The terms are public and say AI prepares and people decide", async ({ page }) => {
  const response = await page.goto("/terms");
  expect(response?.status()).toBe(200);

  await expect(page.getByRole("heading", { level: 1, name: "Terms and Conditions" })).toBeVisible();
  await expect(page.getByText("IIP is designed so that AI prepares work and people decide.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Data Privacy & Protection Policy" })).toBeVisible();
});

test("The home page footer links to both legal pages, and so does sign-up", async ({ page }) => {
  await page.goto("/");
  const footer = page.getByRole("contentinfo");
  await expect(footer.getByRole("link", { name: "Privacy Policy" })).toHaveAttribute("href", "/privacy");
  await expect(footer.getByRole("link", { name: "Terms and Conditions" })).toHaveAttribute("href", "/terms");

  await page.goto("/signup");
  await expect(page.getByRole("link", { name: "Terms and Conditions" })).toHaveAttribute("href", "/terms");
  await expect(page.getByRole("link", { name: "Privacy Policy" })).toHaveAttribute("href", "/privacy");
});

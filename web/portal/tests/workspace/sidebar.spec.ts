import { expect, test } from "@playwright/test";

const SIDEBAR = "Primary navigation";

test.beforeEach(async ({ page }) => {
  // Start every test from the default, the sidebar open and the chat list showing.
  await page.addInitScript(() => {
    try {
      // Once per test, so a reload inside the test keeps whatever the page saved.
      if (window.sessionStorage.getItem("spec-seeded")) return;
      window.sessionStorage.setItem("spec-seeded", "1");
      window.localStorage.setItem("iip.sidebar.expanded", "true");
      window.localStorage.removeItem("iip.sidebar.recentChatsCollapsed");
    } catch {
      // Storage can be blocked; the defaults apply.
    }
  });
});

test("The sidebar groups its destinations and keeps them reachable in a short window", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 520 });
  await page.goto("/app");
  const sidebar = page.getByRole("complementary", { name: SIDEBAR });

  for (const label of ["Work", "Data", "Manage"]) {
    await expect(sidebar.getByText(label, { exact: true })).toBeVisible();
  }
  await expect(sidebar.getByRole("link", { name: "Command Center" })).toBeVisible();
  await expect(sidebar.getByRole("link", { name: "Claims" })).toBeVisible();
  await expect(sidebar.getByRole("button", { name: "Integrations" })).toBeVisible();
  await expect(sidebar.getByRole("button", { name: "Settings" })).toBeVisible();
});

test("Recent chats can be collapsed, and stays collapsed after a reload", async ({ page }) => {
  await page.goto("/app");
  const sidebar = page.getByRole("complementary", { name: SIDEBAR });
  const toggle = sidebar.getByRole("button", { name: /Recent chats/ });

  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await expect(sidebar.getByText("No conversations yet").or(sidebar.getByText(/question/).first())).toHaveCount(0);

  await page.reload();
  await expect(sidebar.getByRole("button", { name: /Recent chats/ })).toHaveAttribute("aria-expanded", "false");

  await sidebar.getByRole("button", { name: /Recent chats/ }).click();
  await expect(sidebar.getByRole("button", { name: /Recent chats/ })).toHaveAttribute("aria-expanded", "true");
});

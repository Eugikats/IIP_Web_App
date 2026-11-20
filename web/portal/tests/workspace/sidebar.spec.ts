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

test("The sidebar keeps a short primary set, groups the rest under More tools and stays reachable in a short window", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 520 });
  await page.goto("/app/workspace");
  const sidebar = page.getByRole("complementary", { name: SIDEBAR });

  for (const label of ["Work", "Manage"]) {
    await expect(sidebar.getByText(label, { exact: true })).toBeVisible();
  }
  for (const label of ["Workspace", "Claims", "Acquisition", "Automations"]) {
    await expect(sidebar.getByRole("link", { name: label, exact: true })).toBeVisible();
  }
  await expect(sidebar.getByRole("button", { name: "Integrations" })).toBeVisible();
  await expect(sidebar.getByRole("button", { name: "Settings" })).toBeVisible();

  // Everything else is one step away, grouped by purpose, not tucked into the profile menu.
  await expect(sidebar.getByRole("link", { name: "Inbox" })).toHaveCount(0);
  await sidebar.getByRole("button", { name: "More tools" }).click();
  const more = page.getByRole("dialog", { name: "More tools" });
  for (const title of ["Work queues", "Customers", "Policy and risk", "Knowledge and learning", "Insight and governance", "Organisation"]) {
    await expect(more.locator("p", { hasText: new RegExp(`^${title}$`) })).toBeVisible();
  }
  await expect(more.getByRole("menuitem", { name: "Inbox" })).toBeVisible();
});

test("On the organisation pages the sidebar changes to the organisation set, with a way back", async ({ page }) => {
  await page.goto("/app/platform/automations");
  const sidebar = page.getByRole("complementary", { name: SIDEBAR });

  for (const label of ["Command Center", "Workspaces", "Automations", "Reports", "Conversations", "Settings"]) {
    await expect(sidebar.getByRole("link", { name: label, exact: true })).toBeVisible();
  }
  // The workspace set is gone while the organisation set is showing.
  await expect(sidebar.getByRole("link", { name: "Claims", exact: true })).toHaveCount(0);
  await expect(sidebar.getByRole("button", { name: "More tools" })).toHaveCount(0);
  await expect(sidebar.getByRole("link", { name: "Automations", exact: true })).toHaveClass(/bg-dls-primary-soft/);

  await sidebar.getByRole("link", { name: "Back to workspace" }).click();
  await expect(page).toHaveURL(/\/app\/workspace$/);
  await expect(sidebar.getByRole("link", { name: "Claims", exact: true })).toBeVisible();
});

test("Recent chats can be collapsed, and stays collapsed after a reload", async ({ page }) => {
  await page.goto("/app/workspace");
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

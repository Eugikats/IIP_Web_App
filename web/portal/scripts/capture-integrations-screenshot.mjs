import { chromium } from "@playwright/test";

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 1536, height: 960 },
  deviceScaleFactor: 1
});

await context.addInitScript(() => {
  window.localStorage.setItem("iip-demo-role", "Admin");
  window.localStorage.setItem("iip-sidebar-collapsed", "0");
  window.localStorage.setItem("iip-rightpanel-collapsed", "0");
});

const page = await context.newPage();
await page.goto("http://127.0.0.1:3000/integrations", { waitUntil: "domcontentloaded" });
await page.getByRole("heading", { name: "Microsoft Graph Shared Mailboxes" }).waitFor({ timeout: 30000 });
await page.screenshot({ path: "../../output/screen-review/integrations-desktop.png", fullPage: true });

await browser.close();

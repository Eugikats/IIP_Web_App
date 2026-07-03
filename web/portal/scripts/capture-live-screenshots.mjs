import { chromium } from "@playwright/test";

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 1440, height: 1000 },
  deviceScaleFactor: 1
});

await context.addInitScript(() => {
  window.localStorage.setItem("iip-demo-role", "ClaimsSupervisor");
  window.localStorage.setItem("iip-sidebar-collapsed", "0");
  window.localStorage.setItem("iip-rightpanel-collapsed", "0");
});

async function capture(path, fileName, visibleText, role = "ClaimsSupervisor") {
  const page = await context.newPage();
  await page.addInitScript((nextRole) => {
    window.localStorage.setItem("iip-demo-role", nextRole);
  }, role);
  await page.goto(`http://127.0.0.1:3000${path}`, { waitUntil: "domcontentloaded" });
  await page.getByRole("heading", { name: visibleText }).first().waitFor({ timeout: 30000 });
  await page.screenshot({ path: `../../output/screen-review/${fileName}`, fullPage: true });
  await page.close();
}

await capture("/", "command-center-desktop.png", "Command Center");
await capture("/integrations", "integrations-shell-desktop.png", "Integrations", "Admin");

await browser.close();

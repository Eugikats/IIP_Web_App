import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.IIP_VISUAL_PORT ?? 3100);
const baseURL = process.env.IIP_VISUAL_BASE_URL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./specs",
  globalSetup: "./global-setup.ts",
  timeout: 90_000,
  workers: 1,
  expect: {
    timeout: 10_000
  },
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { outputFolder: "test-results/visual-report", open: "never" }]],
  outputDir: "test-results/visual-artifacts",
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure"
  },
  projects: [
    {
      name: "desktop-chromium",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 1000 } }
    },
    {
      name: "tablet-chromium",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1024, height: 900 } }
    },
    {
      name: "mobile-chromium",
      use: { ...devices["Pixel 5"], viewport: { width: 390, height: 844 } }
    }
  ]
});

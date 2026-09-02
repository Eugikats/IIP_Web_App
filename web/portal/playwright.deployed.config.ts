import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.IIP_DEPLOYED_PROFILE_PORT ?? 3102);
const baseURL = process.env.IIP_DEPLOYED_PROFILE_BASE_URL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./tests/deployed-profile",
  globalSetup: "./tests/deployed-profile/global-setup.ts",
  outputDir: "./test-results/deployed-profile",
  timeout: 90_000,
  expect: {
    timeout: 15_000
  },
  fullyParallel: false,
  workers: 1,
  reporter: [["list"], ["html", { outputFolder: "playwright-report/deployed-profile", open: "never" }]],
  use: {
    baseURL,
    trace: "on-first-retry"
  },
  projects: [
    {
      name: "deployed-desktop",
      use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1440, height: 1000 } }
    },
    {
      name: "deployed-mobile",
      use: { ...devices["Pixel 7"], browserName: "chromium", viewport: { width: 375, height: 900 } }
    }
  ]
});

import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env.IIP_VISUAL_PORT ?? 3100);
const baseURL = process.env.IIP_VISUAL_BASE_URL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./specs",
  timeout: 45_000,
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
  webServer: {
    command: `node node_modules/next/dist/bin/next dev --hostname 127.0.0.1 --port ${port}`,
    cwd: "../..",
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: process.env.IIP_VISUAL_AUTH_MODE ?? "Development",
      NEXT_PUBLIC_RUNTIME_PROFILE: process.env.IIP_VISUAL_RUNTIME_PROFILE ?? "Development",
      NEXT_PUBLIC_TENANT_ID: "tenant-visual-fixture",
      NEXT_PUBLIC_WORKSPACE_ID: "workspace-visual-fixture"
    }
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

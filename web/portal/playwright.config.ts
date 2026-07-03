import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/visual",
  outputDir: "./test-results/visual",
  timeout: 90_000,
  expect: {
    timeout: 15_000,
    toHaveScreenshot: {
      maxDiffPixelRatio: 0.02
    }
  },
  fullyParallel: false,
  workers: 1,
  reporter: [["list"], ["html", { outputFolder: "playwright-report", open: "never" }]],
  use: {
    baseURL: "http://127.0.0.1:3000",
    trace: "on-first-retry"
  },
  projects: [
    {
      name: "chromium-desktop",
      use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1440, height: 1000 } }
    },
    {
      name: "chromium-tablet",
      use: { ...devices["iPad Pro 11"], browserName: "chromium", viewport: { width: 1024, height: 900 } }
    },
    {
      name: "chromium-mobile",
      use: { ...devices["Pixel 7"], browserName: "chromium", viewport: { width: 375, height: 900 } }
    }
  ],
  webServer: {
    command: "powershell -NoProfile -Command \"$env:NEXT_PUBLIC_API_BASE_URL='http://127.0.0.1:5167'; $env:NEXT_PUBLIC_AUTH_MODE='Development'; $env:NEXT_PUBLIC_RUNTIME_PROFILE='Development'; $env:NEXT_PUBLIC_TENANT_ID='tenant-visual'; $env:NEXT_PUBLIC_WORKSPACE_ID='workspace-visual'; $env:NEXT_PUBLIC_LOCAL_USER_ID='visual-user'; pnpm.cmd dev --hostname 127.0.0.1 --port 3000\"",
    url: "http://127.0.0.1:3000",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: "Development",
      NEXT_PUBLIC_RUNTIME_PROFILE: "Development",
      NEXT_PUBLIC_TENANT_ID: "tenant-visual",
      NEXT_PUBLIC_WORKSPACE_ID: "workspace-visual",
      NEXT_PUBLIC_LOCAL_USER_ID: "visual-user"
    }
  }
});

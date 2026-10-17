import { defineConfig, devices } from "@playwright/test";

// The signed-in workspace against a local API in offline mode (scripts/local/run-offline.sh, port 5167).
// Start the API first; this config starts the portal itself.
// Run one spec/project pair per invocation and restart the API between pairs to reset the shared learner.
// payments.spec.ts requires scripts/local/run-offline-payments.sh; other specs use scripts/local/run-offline.sh.
export default defineConfig({
  testDir: "./tests/academy",
  outputDir: "./test-results/academy",
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:3000",
    timezoneId: "Africa/Kampala",
    acceptDownloads: true,
    screenshot: "only-on-failure",
    trace: "off",
  },
  projects: [
    { name: "phone", use: { ...devices["Pixel 7"], browserName: "chromium", viewport: { width: 390, height: 844 } } },
    { name: "desktop", use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1280, height: 800 } } },
    { name: "tablet", use: { ...devices["iPad Pro 11"], browserName: "chromium", viewport: { width: 768, height: 1024 } } },
  ],
  webServer: {
    command:
      "powershell -NoProfile -Command \"$env:NEXT_PUBLIC_API_BASE_URL='http://localhost:5167'; $env:NEXT_PUBLIC_TENANT_ID='tenant-demo'; $env:NEXT_PUBLIC_WORKSPACE_ID='workspace-operations'; $env:NEXT_PUBLIC_LOCAL_USER_ID='user-1'; pnpm.cmd dev\"",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 180_000,
  },
});

import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/workspace",
  outputDir: "./test-results/workspace-whatsapp-mobile",
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:3000",
    timezoneId: "Africa/Kampala",
    acceptDownloads: true,
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "mobile", use: { ...devices["iPhone 13"], browserName: "chromium", viewport: { width: 390, height: 844 } } },
  ],
  webServer: {
    command: "powershell -NoProfile -Command \"$env:NEXT_PUBLIC_API_BASE_URL='http://localhost:5167'; $env:NEXT_PUBLIC_TENANT_ID='tenant-demo'; $env:NEXT_PUBLIC_WORKSPACE_ID='workspace-operations'; $env:NEXT_PUBLIC_LOCAL_USER_ID='user-1'; pnpm.cmd dev\"",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 180_000,
  },
});

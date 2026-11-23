import { defineConfig, devices } from "@playwright/test";

// Interactive document review. The documents API is answered inside the spec, so no API is needed;
// this config starts the portal itself. Run with: pnpm test:documents
export default defineConfig({
  testDir: "./tests/documents",
  outputDir: "./test-results/documents",
  timeout: 120_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:3000",
    timezoneId: "Africa/Kampala",
    screenshot: "only-on-failure",
    trace: "off",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1280, height: 900 } } },
    { name: "tablet", use: { ...devices["iPad Pro 11"], browserName: "chromium", viewport: { width: 768, height: 1024 } } },
    { name: "mobile", use: { ...devices["Pixel 7"], browserName: "chromium", viewport: { width: 375, height: 812 } } },
  ],
  webServer: {
    command:
      "powershell -NoProfile -Command \"$env:NEXT_PUBLIC_API_BASE_URL='http://localhost:5167'; $env:NEXT_PUBLIC_TENANT_ID='tenant-demo'; $env:NEXT_PUBLIC_WORKSPACE_ID='workspace-operations'; $env:NEXT_PUBLIC_LOCAL_USER_ID='user-1'; pnpm.cmd dev\"",
    url: "http://localhost:3000",
    reuseExistingServer: true,
    timeout: 180_000,
  },
});

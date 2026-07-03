import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/marketing",
  outputDir: "./test-results/marketing",
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://127.0.0.1:3000",
    trace: "off"
  },
  projects: [
    {
      name: "desktop",
      use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1440, height: 1000 } }
    },
    {
      name: "tablet",
      use: { ...devices["iPad Pro 11"], browserName: "chromium", viewport: { width: 1024, height: 900 } }
    },
    {
      name: "mobile",
      use: { ...devices["Pixel 7"], browserName: "chromium", viewport: { width: 375, height: 900 } }
    }
  ],
  webServer: {
    command: "powershell -NoProfile -Command \"$env:NEXT_PUBLIC_API_BASE_URL='http://127.0.0.1:5167'; $env:NEXT_PUBLIC_AUTH_MODE='Entra'; $env:NEXT_PUBLIC_RUNTIME_PROFILE='Staging'; $env:NEXT_PUBLIC_TENANT_ID=''; $env:NEXT_PUBLIC_WORKSPACE_ID=''; $env:NEXT_PUBLIC_LOCAL_USER_ID=''; $env:NEXT_PUBLIC_ENTRA_TENANT_ID=''; $env:NEXT_PUBLIC_ENTRA_CLIENT_ID=''; $env:NEXT_PUBLIC_ENTRA_API_SCOPE=''; pnpm.cmd dev --hostname 127.0.0.1 --port 3000\"",
    url: "http://127.0.0.1:3000",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
      NEXT_PUBLIC_AUTH_MODE: "Entra",
      NEXT_PUBLIC_RUNTIME_PROFILE: "Staging",
      NEXT_PUBLIC_TENANT_ID: "",
      NEXT_PUBLIC_WORKSPACE_ID: "",
      NEXT_PUBLIC_LOCAL_USER_ID: "",
      NEXT_PUBLIC_ENTRA_TENANT_ID: "",
      NEXT_PUBLIC_ENTRA_CLIENT_ID: "",
      NEXT_PUBLIC_ENTRA_API_SCOPE: ""
    }
  }
});

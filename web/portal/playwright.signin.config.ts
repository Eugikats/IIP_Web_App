import { defineConfig, devices } from "@playwright/test";

// The real Microsoft sign-in code path (MSAL) against a stand-in sign-in server answered by the test itself.
// The portal runs in Entra mode on its own port; no API or Microsoft is contacted. Stop any other portal dev
// server first: two cannot share one build folder.
export default defineConfig({
  testDir: "./tests/signin",
  outputDir: "./test-results/signin",
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:3100",
    screenshot: "only-on-failure",
    trace: "off",
  },
  projects: [{ name: "desktop", use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1280, height: 800 } } }],
  webServer: {
    command:
      "powershell -NoProfile -Command \"$env:NEXT_PUBLIC_API_BASE_URL='http://localhost:5199'; $env:NEXT_PUBLIC_AUTH_MODE='Entra'; $env:NEXT_PUBLIC_RUNTIME_PROFILE='Demo'; $env:NEXT_PUBLIC_ENTRA_TENANT_ID='11111111-1111-1111-1111-111111111111'; $env:NEXT_PUBLIC_ENTRA_CLIENT_ID='22222222-2222-2222-2222-222222222222'; $env:NEXT_PUBLIC_ENTRA_API_SCOPE='api://33333333-3333-3333-3333-333333333333/access_as_user'; $env:NEXT_PUBLIC_ENTRA_REDIRECT_URI='http://localhost:3100/app'; pnpm.cmd dev -p 3100\"",
    url: "http://localhost:3100",
    reuseExistingServer: true,
    timeout: 180_000,
  },
});

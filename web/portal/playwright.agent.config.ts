import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

// A recording of a short burst of speech followed by silence, played once through the fake microphone.
const FAKE_SPEECH = path.join(__dirname, "tests", "agent", "fixtures", "speech-then-silence.wav");

// The agent that looks awake: live steps, presence and the brief on open. A small stand-in API is started inside the spec;
// this config starts the portal itself. Run with: pnpm test:agent
export default defineConfig({
  testDir: "./tests/agent",
  outputDir: "./test-results/agent",
  timeout: 120_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:3000",
    timezoneId: "Africa/Kampala",
    screenshot: "only-on-failure",
    trace: "off",
    // A fake microphone, so a recording can be made without a person speaking.
    permissions: ["microphone"],
    launchOptions: { args: ["--use-fake-device-for-media-stream", "--use-fake-ui-for-media-stream", `--use-file-for-fake-audio-capture=${FAKE_SPEECH}%noloop`] },
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"], browserName: "chromium", viewport: { width: 1280, height: 900 } } },
    { name: "tablet", use: { ...devices["iPad Pro 11"], browserName: "chromium", viewport: { width: 768, height: 1024 } } },
    // A phone, the way IIP on the Go shows it: touch, no hover, a narrow column.
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

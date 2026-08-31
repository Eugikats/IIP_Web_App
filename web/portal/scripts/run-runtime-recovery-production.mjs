import { spawnSync } from "node:child_process";

const windows = process.platform === "win32";
const runtimeEnv = {
  ...process.env,
  NEXT_PUBLIC_API_BASE_URL: "http://127.0.0.1:5167",
  NEXT_PUBLIC_AUTH_MODE: "Development",
  NEXT_PUBLIC_RUNTIME_PROFILE: "Development",
};

function run(args) {
  const command = windows ? process.env.ComSpec || "cmd.exe" : "pnpm";
  const commandArgs = windows ? ["/d", "/s", "/c", ["pnpm", ...args].join(" ")] : args;
  const result = spawnSync(command, commandArgs, {
    env: runtimeEnv,
    stdio: "inherit",
  });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}

run(["build"]);
run(["exec", "playwright", "test", "--config", "tests/runtime-recovery/playwright.production.config.ts"]);

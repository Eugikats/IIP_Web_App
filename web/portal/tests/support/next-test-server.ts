import { spawn, spawnSync, type ChildProcess } from "node:child_process";
import { join } from "node:path";

type NextTestServerOptions = {
  baseUrlEnv: string;
  defaultPort: number;
  portEnv: string;
  runtimeEnv: Record<string, string>;
};

function stopServer(server: ChildProcess) {
  if (!server.pid || server.exitCode !== null) return;

  if (process.platform === "win32") {
    spawnSync("taskkill", ["/PID", String(server.pid), "/T", "/F"], { stdio: "ignore" });
    return;
  }

  try {
    process.kill(-server.pid, "SIGTERM");
  } catch {
    server.kill("SIGTERM");
  }
}

async function waitForServer(server: ChildProcess, baseURL: string) {
  const deadline = Date.now() + 120_000;
  let exitError: Error | undefined;
  server.once("exit", (code, signal) => {
    exitError = new Error(`Test server exited before readiness (code ${code ?? "none"}, signal ${signal ?? "none"}).`);
  });

  while (Date.now() < deadline) {
    if (exitError) throw exitError;
    try {
      const response = await fetch(baseURL, { redirect: "manual" });
      if (response.status < 500) return;
    } catch {
      // The compiler may not be listening yet.
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  throw new Error(`Test server did not become ready at ${baseURL}.`);
}

export async function startNextTestServer(options: NextTestServerOptions) {
  if (process.env[options.baseUrlEnv]) return;

  const port = Number(process.env[options.portEnv] ?? options.defaultPort);
  const baseURL = `http://127.0.0.1:${port}`;
  const portalRoot = process.cwd();
  const devRunner = join(portalRoot, "scripts", "run-next-dev.mjs");
  const server = spawn(process.execPath, [devRunner, "--hostname", "127.0.0.1", "--port", String(port)], {
    cwd: portalRoot,
    env: { ...process.env, ...options.runtimeEnv },
    detached: process.platform !== "win32",
    stdio: "inherit",
    windowsHide: true,
  });

  try {
    await waitForServer(server, baseURL);
  } catch (error) {
    stopServer(server);
    throw error;
  }

  return () => stopServer(server);
}

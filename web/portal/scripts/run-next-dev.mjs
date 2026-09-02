import { closeSync, openSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";

const portalRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const lockPath = join(portalRoot, ".next-dev.lock");
const nextCli = join(portalRoot, "node_modules", "next", "dist", "bin", "next");
let lockHandle;
let ownsLock = false;

function processIsAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

function releaseLock() {
  if (!ownsLock) return;
  ownsLock = false;
  try { closeSync(lockHandle); } catch {}
  try { unlinkSync(lockPath); } catch {}
}

function acquireLock() {
  try {
    lockHandle = openSync(lockPath, "wx");
  } catch (error) {
    if (error?.code !== "EEXIST") throw error;
    let ownerPid = 0;
    try { ownerPid = Number.parseInt(readFileSync(lockPath, "utf8"), 10); } catch {}
    if (processIsAlive(ownerPid)) {
      throw new Error(`Another portal dev compiler is already running (process ${ownerPid}). Stop it before starting another dev or Playwright server.`);
    }
    try { unlinkSync(lockPath); } catch {}
    lockHandle = openSync(lockPath, "wx");
  }
  writeFileSync(lockHandle, String(process.pid));
  ownsLock = true;
}

acquireLock();
const child = spawn(process.execPath, [nextCli, "dev", ...process.argv.slice(2)], {
  cwd: portalRoot,
  env: process.env,
  stdio: "inherit",
});

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => {
    child.kill(signal);
  });
}

child.on("error", (error) => {
  releaseLock();
  throw error;
});

child.on("exit", (code, signal) => {
  releaseLock();
  if (signal) {
    process.removeAllListeners(signal);
    process.kill(process.pid, signal);
    return;
  }
  process.exit(code ?? 1);
});

process.on("exit", releaseLock);

import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const portalRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const nextCli = join(portalRoot, "node_modules", "next", "dist", "bin", "next");
const result = spawnSync(process.execPath, [nextCli, "build", ...process.argv.slice(2)], {
  cwd: portalRoot,
  env: { ...process.env, IIP_PORTAL_BUILD: "1" },
  stdio: "inherit",
});

if (result.error) throw result.error;
process.exit(result.status ?? 1);

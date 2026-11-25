// Toàn bộ Playwright (src/frontend/e2e).
import { existsSync } from "node:fs";
import { join } from "node:path";
import { frontend, runAll } from "./run.mjs";

if (!existsSync(join(frontend, "e2e"))) {
  console.log("Chưa có src/frontend/e2e — bỏ qua.");
  process.exit(0);
}
process.exit(runAll([{ cmd: "pnpm", args: ["exec", "playwright", "test"], cwd: frontend }]));

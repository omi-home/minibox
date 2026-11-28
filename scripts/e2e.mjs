// Toàn bộ Playwright (src/frontend/e2e).
import { existsSync } from "node:fs";
import { join } from "node:path";
import { frontend, runAll } from "./run.mjs";

if (!existsSync(join(frontend, "e2e"))) {
  console.log("Chưa có src/frontend/e2e — bỏ qua.");
  process.exit(0);
}
// CI máy mới: tải Chromium (kèm thư viện hệ thống); máy dev cài một lần bằng `pnpm exec playwright install chromium`.
const install = { cmd: "pnpm", args: ["exec", "playwright", "install", "--with-deps", "chromium"], cwd: frontend };
process.exit(runAll([...(process.env.CI ? [install] : []), { cmd: "pnpm", args: ["exec", "playwright", "test"], cwd: frontend }]));

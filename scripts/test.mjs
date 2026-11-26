// Lệnh chuẩn: build + toàn bộ test backend, frontend và script. Exit ≠ 0 khi lỗi (spec §9.3).
import { backend, frontend, isMain, root, runAll } from "./run.mjs";

export const testSteps = () => [
  { cmd: "node", args: ["--test", "scripts/*.test.mjs", "database/tests/*.test.mjs"], cwd: root },
  { cmd: "dotnet", args: ["test", "--solution", "Minibox.slnx"], cwd: backend },
  // Sau build backend: build vừa sinh lại contracts/openapi, test hợp đồng so với bản đã commit.
  { cmd: "node", args: ["--test", "contracts/tests/*.test.mjs"], cwd: root },
  { cmd: "pnpm", args: ["install", "--frozen-lockfile"], cwd: frontend },
  { cmd: "pnpm", args: ["lint"], cwd: frontend },
  { cmd: "pnpm", args: ["typecheck"], cwd: frontend },
  { cmd: "pnpm", args: ["test"], cwd: frontend },
  { cmd: "pnpm", args: ["build"], cwd: frontend },
];

if (isMain(import.meta.url)) process.exit(runAll(testSteps()));

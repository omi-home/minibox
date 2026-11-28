// Lệnh chuẩn: build + toàn bộ test backend, frontend và script. Exit ≠ 0 khi lỗi (spec §9.3).
// `node scripts/test.mjs <nhóm...>` chỉ chạy các nhóm đó (job CI song song, scripts/check.mjs dùng chung định nghĩa bước).
import { backend, frontend, isMain, root, runAll } from "./run.mjs";

export const step = {
  scripts: { cmd: "node", args: ["--test", "scripts/*.test.mjs", "database/tests/*.test.mjs"], cwd: root },
  dotnet: { cmd: "dotnet", args: ["test", "--solution", "Minibox.slnx"], cwd: backend },
  install: { cmd: "pnpm", args: ["install", "--frozen-lockfile"], cwd: frontend },
  // Sau build backend (vừa sinh lại contracts/openapi) và pnpm install (cần openapi-typescript): so hợp đồng + client TS với bản đã commit.
  contracts: { cmd: "node", args: ["--test", "contracts/tests/*.test.mjs"], cwd: root },
  lint: { cmd: "pnpm", args: ["lint"], cwd: frontend },
  typecheck: { cmd: "pnpm", args: ["typecheck"], cwd: frontend },
  test: { cmd: "pnpm", args: ["test"], cwd: frontend },
  build: { cmd: "pnpm", args: ["build"], cwd: frontend },
  e2e: { cmd: "node", args: ["scripts/e2e.mjs"], cwd: root },
};

export const groups = {
  scripts: [step.scripts],
  backend: [step.dotnet, step.install, step.contracts],
  frontend: [step.install, step.lint, step.typecheck, step.test, step.build],
  e2e: [step.install, step.e2e],
};

/** Bước của các nhóm (mặc định: scripts, backend, frontend — e2e chạy riêng), bỏ bước trùng. */
export function testSteps(names = ["scripts", "backend", "frontend"]) {
  for (const n of names) if (!groups[n]) throw new Error(`Nhóm không tồn tại: ${n} (có: ${Object.keys(groups).join(", ")})`);
  return [...new Set(names.flatMap((n) => groups[n]))];
}

if (isMain(import.meta.url)) {
  const names = process.argv.slice(2);
  process.exit(runAll(testSteps(names.length ? names : undefined)));
}

// Lệnh nghiệm thu omi: chỉ chạy test có tên chứa ac<n>_ và in tên test (spec §9.2, §9.3).
// Không có test khớp không tính là lỗi (omi phán quyết).
import { existsSync } from "node:fs";
import { join } from "node:path";
import { backend, frontend, isMain, root, runAll } from "./run.mjs";

const NO_TESTS = 8; // exit code của Microsoft.Testing.Platform khi không test nào chạy

export function acceptSteps(n) {
  if (!/^\d+$/.test(n ?? "")) throw new Error(`Cần số issue, nhận: ${n}`);
  const pat = `ac${n}_`;
  const steps = [
    { cmd: "node", args: ["--test", "--test-name-pattern", pat, "scripts/*.test.mjs"], cwd: root },
    {
      cmd: "dotnet",
      args: ["test", "--solution", "Minibox.slnx", "--filter-method", `*${pat}*`, "--output", "detailed"],
      cwd: backend,
      okCodes: [0, NO_TESTS],
    },
    // Sau dotnet test (đã build → sinh lại contracts/openapi); client TS cần openapi-typescript trong node_modules.
    { cmd: "pnpm", args: ["install", "--frozen-lockfile"], cwd: frontend },
    { cmd: "node", args: ["--test", "--test-name-pattern", pat, "contracts/tests/*.test.mjs"], cwd: root },
    { cmd: "pnpm", args: ["-r", "test", "-t", pat, "--reporter=verbose"], cwd: frontend },
  ];
  if (existsSync(join(frontend, "e2e")))
    steps.push({ cmd: "pnpm", args: ["exec", "playwright", "test", "-g", pat, "--reporter=list", "--pass-with-no-tests"], cwd: frontend });
  return steps;
}

if (isMain(import.meta.url)) process.exit(runAll(acceptSteps(process.argv[2])));

import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export const root = join(dirname(fileURLToPath(import.meta.url)), "..");
export const backend = join(root, "src", "backend");
export const frontend = join(root, "src", "frontend");

// pnpm trên Windows là pnpm.cmd → cần shell; dotnet/node chạy trực tiếp để không bị shell glob tham số.
const needsShell = (cmd) => process.platform === "win32" && cmd === "pnpm";

/** Chạy lần lượt; dừng ở bước đầu tiên lỗi và trả exit code của nó (0 nếu tất cả pass). */
export function runAll(steps) {
  for (const { cmd, args = [], cwd = root, okCodes = [0] } of steps) {
    console.log(`\n> ${[cmd, ...args].join(" ")}  (${cwd})`);
    const r = spawnSync(cmd, args, { cwd, stdio: "inherit", shell: needsShell(cmd) });
    const code = r.status ?? 1;
    if (!okCodes.includes(code)) return code || 1;
  }
  return 0;
}

export const isMain = (url) => process.argv[1] && fileURLToPath(url) === process.argv[1];

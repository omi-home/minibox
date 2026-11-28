// Dựng môi trường dev bằng Compose và chờ health check (spec §8.2). Tham số thêm chuyển thẳng cho `docker compose`.
import { existsSync } from "node:fs";
import { join } from "node:path";
import { isMain, root, runAll } from "./run.mjs";

/**
 * Tham số `docker compose` cho môi trường `env`: `deploy/compose.yaml` (chung) + `deploy/<env>/compose.yaml`,
 * kèm `deploy/<env>/.env` nếu có (không có thì dùng giá trị mặc định trong compose).
 * @param {string} env tên môi trường, trùng tên thư mục dưới `deploy/` (vd. `dev`)
 * @returns {string[]} tham số bắt đầu bằng `compose`
 */
export const composeArgs = (env) => {
  const envFile = join("deploy", env, ".env");
  return [
    "compose", "-f", join("deploy", "compose.yaml"), "-f", join("deploy", env, "compose.yaml"),
    ...(existsSync(join(root, envFile)) ? ["--env-file", envFile] : []),
  ];
};

if (isMain(import.meta.url)) {
  const extra = process.argv.slice(2);
  const args = [...composeArgs("dev"), ...(extra.length ? extra : ["up", "-d", "--wait"])];
  process.exit(runAll([{ cmd: "docker", args, cwd: root }]));
}

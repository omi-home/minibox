// Deploy một môi trường trên homeserver (spec §8.4): `deploy/deploy.sh <staging|production> <tag>`.
// pull → (production) backup incr pgBackRest → bootstrap DB → `--migrate` từng service (lỗi thì dừng) → up --wait → không đạt thì quay về tag cũ.
// Image app tham chiếu `${MINIBOX_TAG}`; tag đang chạy lưu ở /srv/minibox/<env>/deployed-tag. Migration không quay lui (expand/contract).
import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { isMain, root } from "./run.mjs";

const ENVS = ["staging", "production"];
const TAG = /^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$/; // cú pháp tag Docker

const realExec = (cmd, args, env) => {
  console.log(`\n> ${[cmd, ...args].join(" ")}  (MINIBOX_TAG=${env.MINIBOX_TAG})`);
  return spawnSync(cmd, args, { cwd: root, stdio: "inherit", env: { ...process.env, ...env } }).status ?? 1;
};

/**
 * @param {string} env staging | production
 * @param {string} tag tag image cần deploy
 * @param {{ exec?: Function, services?: (env: string) => string[], stateDir?: string }} [o] runner/thư mục trạng thái (test thay)
 * @returns {number} exit code (0 = deploy thành công)
 */
export function deploy(env, tag, o = {}) {
  if (!ENVS.includes(env)) throw new Error(`env phải là ${ENVS.join("|")}: ${env}`);
  if (!TAG.test(tag)) throw new Error(`tag không hợp lệ: ${tag}`);
  const exec = o.exec ?? realExec;
  const services = (o.services ?? composeServices)(env);
  const tagFile = join(o.stateDir ?? `/srv/minibox/${env}`, "deployed-tag");
  const prev = existsSync(tagFile) ? readFileSync(tagFile, "utf8").trim() : null;
  const dc = composeArgs(env);
  const at = (t) => ({ MINIBOX_TAG: t });
  const up = ["up", "-d", "--build", "--wait", "--remove-orphans"];

  const steps = [
    ["docker", [...dc, "pull", "--ignore-buildable"]],
    ["docker", [...dc, "up", "-d", "--build", "--wait", "database-postgres"]],
    ...(env === "production" ? [["docker", [...dc, "exec", "-T", "-u", "postgres", "database-postgres", "pgbackrest", "--stanza=minibox", "--type=incr", "backup"]]] : []),
    [process.execPath, ["database/bootstrap/run.mjs", env]],
    ...services.filter((s) => s.startsWith("backend-")).map((s) => ["docker", [...dc, "run", "--rm", "--no-deps", s, "--migrate"]]),
  ];
  for (const [cmd, args] of steps) {
    const code = exec(cmd, args, at(tag));
    if (code !== 0) {
      console.error(`deploy ${env} ${tag}: dừng ở "${args.slice(dc.length).join(" ") || args.join(" ")}" (exit ${code}); stack giữ nguyên`);
      return code;
    }
  }
  if (exec("docker", [...dc, ...up], at(tag)) !== 0) {
    if (!prev) return console.error(`deploy ${env} ${tag}: health check không đạt, chưa có tag cũ để quay về`), 1;
    console.error(`deploy ${env} ${tag}: health check không đạt, quay về ${prev}`);
    if (exec("docker", [...dc, ...up], at(prev)) !== 0) console.error(`quay về ${prev} cũng không đạt — cần xử lý tay`);
    return 1;
  }
  writeFileSync(tagFile, tag);
  console.log(`deploy ${env} ${tag}: xong`);
  return 0;
}

const composeServices = (env) =>
  execFileSync("docker", [...composeArgs(env), "config", "--services"], { cwd: root, encoding: "utf8", env: { ...process.env, MINIBOX_TAG: "x" } })
    .split("\n").filter(Boolean);

if (isMain(import.meta.url)) {
  const [env, tag] = process.argv.slice(2);
  try {
    process.exit(deploy(env, tag ?? ""));
  } catch (e) {
    console.error(`${e.message}\nDùng: deploy/deploy.sh <staging|production> <tag>`);
    process.exit(2);
  }
}

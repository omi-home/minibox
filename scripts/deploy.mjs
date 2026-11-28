// Deploy một môi trường trên homeserver (spec §8.4): `deploy/deploy.sh <staging|production> <tag>`.
// pull → (production) kiểm đúng digest staging + backup incr pgBackRest → bootstrap DB → `--migrate` từng service (lỗi thì dừng)
// → up --wait → không đạt thì quay về tag cũ. Image app tham chiếu `${MINIBOX_TAG}`; tag đang chạy lưu ở /srv/minibox/<env>/deployed-tag,
// digest image app ở deployed-digests.json. Migration không quay lui (expand/contract). Chạy bằng root (đổi chủ file secret).
import { execFileSync, spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { chownSync, closeSync, existsSync, mkdirSync, openSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { isMain, root } from "./run.mjs";

const ENVS = ["staging", "production"];
const TAG = /^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$/; // cú pháp tag Docker

// uid user chạy trong image (src/backend/Dockerfile: APP_UID của aspnet; src/frontend/Dockerfile: node). Compose không swarm mount
// file secret giữ nguyên chủ/quyền nên file phải thuộc uid này (quyền 400) thì process trong container mới đọc được.
const BACKEND_UID = 1654;
const FRONTEND_UID = 1000;

const readSecret = (dir, file) => {
  const value = readFileSync(join(dir, file), "utf8").trim();
  if (!value) throw new Error(`file secret rỗng: ${join(dir, file)}`);
  return value;
};

/** Chuỗi kết nối PostgreSQL của role `<svc>_<kind>` trong mạng Compose của môi trường. */
const pgConnection = (dir, svc, kind) => {
  const password = readSecret(dir, `${svc}_${kind}.password`);
  // Mật khẩu bootstrap là base64url; ký tự khác (`;`, `=`) có thể chèn thêm tham số vào chuỗi Npgsql.
  if (!/^[A-Za-z0-9_-]+$/.test(password)) throw new Error(`mật khẩu ${svc}_${kind} chỉ được gồm A-Z a-z 0-9 _ -`);
  return `Host=database-postgres;Database=minibox;Username=${svc}_${kind};Password=${password}`;
};

/**
 * Sinh file secret runtime cho service app (deploy/apps.yaml) từ mật khẩu bootstrap và rabbitmq.conf; khoá MFA/phiên admin sinh
 * ngẫu nhiên một lần rồi giữ nguyên (mất khoá MFA = không giải mã được secret TOTP đã lưu).
 * @param {string} dir thư mục secret của môi trường
 * @param {string[]} services tên service compose (backend-<svc>, frontend-<app>)
 * @param {(file: string, uid: number, gid: number) => void} [chown] đổi chủ file (test thay)
 */
export function writeAppSecrets(dir, services, chown = chownSync) {
  const conf = readSecret(dir, "rabbitmq.conf");
  const field = (k) => {
    const value = conf.match(new RegExp(`^\\s*${k}\\s*=\\s*(.+?)\\s*$`, "m"))?.[1];
    if (!value) throw new Error(`rabbitmq.conf thiếu ${k}`);
    return value;
  };
  const amqp = `amqp://${encodeURIComponent(field("default_user"))}:${encodeURIComponent(field("default_pass"))}@infra-rabbitmq:5672`;
  const put = (file, value, uid) => {
    const path = join(dir, file);
    rmSync(path, { force: true }); // file cũ quyền 400 không ghi đè được
    writeFileSync(path, value, { mode: 0o400 });
    chown(path, uid, uid);
  };
  const keep = (file, uid) => existsSync(join(dir, file)) || put(file, randomBytes(32).toString("base64"), uid);
  put("backend_rabbitmq.uri", amqp, BACKEND_UID);
  put("frontend_rabbitmq.url", amqp, FRONTEND_UID);
  for (const s of services.filter((x) => x.startsWith("backend-"))) put(`${s.slice(8)}_app.connection`, pgConnection(dir, s.slice(8), "app"), BACKEND_UID);
  keep("identity_mfa.key", BACKEND_UID);
  keep("admin_session.key", FRONTEND_UID);
}

/** Digest (`image@sha256:…`) của image app đã pull cho tag, theo tên service. */
const realDigests = (env, tag) => {
  const opts = { cwd: root, encoding: "utf8", env: { ...process.env, MINIBOX_TAG: tag } };
  const services = JSON.parse(execFileSync("docker", [...composeArgs(env), "config", "--format", "json"], opts)).services;
  return Object.fromEntries(
    Object.entries(services)
      .filter(([, s]) => s.image?.startsWith("ghcr.io/"))
      .map(([name, s]) => [name, execFileSync("docker", ["image", "inspect", "-f", "{{index .RepoDigests 0}}", s.image], opts).trim()]),
  );
};

const realExec = (cmd, args, env) => {
  console.log(`\n> ${[cmd, ...args].join(" ")}  (MINIBOX_TAG=${env.MINIBOX_TAG})`);
  return spawnSync(cmd, args, { cwd: root, stdio: "inherit", env: { ...process.env, ...env } }).status ?? 1;
};

/**
 * @param {string} env staging | production
 * @param {string} tag tag image cần deploy (commit develop); production phải trùng tag đang chạy ở staging
 * @param {{ exec?: Function, services?: (env: string) => string[], digests?: (env: string, tag: string) => Record<string, string>,
 *   stateDir?: string, stagingDir?: string, chown?: Function }} [o] runner/digest/thư mục trạng thái/đổi chủ file (test thay)
 * @returns {number} exit code (0 = deploy thành công)
 */
export function deploy(env, tag, o = {}) {
  if (!ENVS.includes(env)) throw new Error(`env phải là ${ENVS.join("|")}: ${env}`);
  if (!TAG.test(tag)) throw new Error(`tag không hợp lệ: ${tag}`);
  const stateDir = o.stateDir ?? `/srv/minibox/${env}`;
  mkdirSync(stateDir, { recursive: true });
  // Khoá chạy đơn: hai deploy cùng môi trường tranh deployed-tag/rollback. Deploy bị kill giữa chừng thì xoá deploy.lock bằng tay.
  const lock = join(stateDir, "deploy.lock");
  let fd;
  try {
    fd = openSync(lock, "wx");
  } catch (e) {
    if (e.code !== "EEXIST") throw e;
    console.error(`deploy ${env}: đang có deploy khác (${lock})`);
    return 4;
  }
  try {
    return run(env, tag, stateDir, o);
  } finally {
    closeSync(fd);
    rmSync(lock, { force: true });
  }
}

function run(env, tag, stateDir, o) {
  const exec = o.exec ?? realExec;
  const services = (o.services ?? composeServices)(env);
  const digests = o.digests ?? realDigests;
  const secretsDir = join(stateDir, "secrets");
  const tagFile = join(stateDir, "deployed-tag");
  const prev = existsSync(tagFile) ? readFileSync(tagFile, "utf8").trim() : null;
  const dc = composeArgs(env);
  const at = (t) => ({ MINIBOX_TAG: t });
  const up = ["up", "-d", "--build", "--wait", "--remove-orphans"];

  const steps = [
    ["docker", [...dc, "pull", "--ignore-buildable"]],
    // Production chỉ nhận đúng bản đang chạy ở staging: cùng tag và cùng digest từng image (tag GHCR ghi đè được, digest thì không).
    ...(env === "production" ? [() => verifyStaged(tag, digests(env, tag), o.stagingDir ?? "/srv/minibox/staging")] : []),
    ["docker", [...dc, "up", "-d", "--build", "--wait", "database-postgres"]],
    ...(env === "production" ? [["docker", [...dc, "exec", "-T", "-u", "postgres", "database-postgres", "pgbackrest", "--stanza=minibox", "--type=incr", "backup"]]] : []),
    [process.execPath, ["database/bootstrap/run.mjs", env]],
    // Sau bootstrap (đã có mật khẩu role): sinh secret runtime trước khi chạy container app.
    () => void writeAppSecrets(secretsDir, services, o.chown),
    // Chuỗi migrator chỉ cho job migrate, qua biến môi trường của tiến trình deploy (`-e TÊN` không lộ giá trị trên dòng lệnh).
    ...services.filter((s) => s.startsWith("backend-")).map((s) => [
      "docker", [...dc, "run", "--rm", "--no-deps", "-e", "MINIBOX_MIGRATOR_CONNECTION", s, "--migrate"],
      () => ({ MINIBOX_MIGRATOR_CONNECTION: pgConnection(secretsDir, s.slice(8), "migrator") }),
    ]),
  ];
  for (const step of steps) {
    if (typeof step === "function") {
      const code = step() ?? 0;
      if (code !== 0) return code;
      continue;
    }
    const [cmd, args, extraEnv] = step;
    const code = exec(cmd, args, { ...at(tag), ...extraEnv?.() });
    if (code !== 0) {
      console.error(`deploy ${env} ${tag}: dừng ở "${args.slice(dc.length).join(" ") || args.join(" ")}" (exit ${code}); stack giữ nguyên`);
      return code;
    }
  }
  if (exec("docker", [...dc, ...up], at(tag)) !== 0) {
    if (!prev) return console.error(`deploy ${env} ${tag}: health check không đạt, chưa có tag cũ để quay về`), 1;
    console.error(`deploy ${env} ${tag}: health check không đạt, quay về ${prev}`);
    if (exec("docker", [...dc, ...up], at(prev)) !== 0) console.error(`quay về ${prev} cũng không đạt — cần xử lý tay`);
    else console.error(`đã quay về ${prev}`);
    return 1;
  }
  writeFileSync(join(stateDir, "deployed-digests.json"), JSON.stringify(digests(env, tag), null, 2));
  writeFileSync(tagFile, tag);
  console.log(`deploy ${env} ${tag}: xong`);
  return 0;
}

/**
 * Kiểm bản production sắp deploy chính là bản đang chạy ở staging.
 * @param {string} tag tag production
 * @param {Record<string, string>} pulled digest vừa pull, theo service
 * @param {string} stagingDir thư mục trạng thái staging
 * @returns {number} 0 nếu khớp, 3 nếu không
 */
function verifyStaged(tag, pulled, stagingDir) {
  const read = (file) => (existsSync(join(stagingDir, file)) ? readFileSync(join(stagingDir, file), "utf8").trim() : null);
  const stagedTag = read("deployed-tag");
  const staged = JSON.parse(read("deployed-digests.json") ?? "{}");
  if (stagedTag !== tag) return console.error(`deploy production ${tag}: staging đang chạy ${stagedTag ?? "(chưa có)"}, chỉ deploy bản đã qua staging`), 3;
  const diff = Object.keys({ ...staged, ...pulled }).filter((s) => staged[s] !== pulled[s]);
  if (diff.length) return console.error(`deploy production ${tag}: digest khác staging ở ${diff.join(", ")}`), 3;
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

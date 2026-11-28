// Smoke e2e trên stack Compose (NFR #217, task #219): build image app như lúc deploy → bootstrap DB → `--migrate` từng backend
// → tạo nhân viên thử → up --wait → Playwright (src/frontend/e2e/smoke: health mọi service, đăng nhập admin + MFA bằng identity thật)
// → down -v. Cần Docker. Chạy: `node scripts/smoke.mjs` (CI job `images`, sau khi image đã build nên dùng lại cache).
import { spawnSync } from "node:child_process";
import { argon2Sync, randomBytes, randomUUID } from "node:crypto";
import { join } from "node:path";
import { bootstrap, composePsql, ensurePasswords } from "../database/bootstrap/run.mjs";
import { composeArgs } from "./dev.mjs";
import { frontend, isMain, root } from "./run.mjs";

const ENV = "ci";
const PORT = process.env.SMOKE_ADMIN_PORT ?? "3201";
const BACKENDS = ["identity", "localization", "catalog"];
// ponytail: bỏ infra-clamav — lần đầu tải CSDL chữ ký mất ~6 phút (vượt ngân sách CI 15 phút) và chưa app nào dùng; thêm khi có service quét file.
const SKIP = ["infra-clamav"];

/** Băm PHC Argon2id cùng tham số Argon2idPasswordHasher của identity (19 MiB, t=2, p=1, tag 32 byte). */
export const argon2id = (password, salt = randomBytes(16)) => {
  const hash = argon2Sync("argon2id", { message: password, nonce: salt, parallelism: 1, tagLength: 32, memory: 19456, passes: 2 });
  const b64 = (b) => b.toString("base64").replace(/=+$/, "");
  return `$argon2id$v=19$m=19456,t=2,p=1$${b64(salt)}$${b64(hash)}`;
};

/** Chuỗi kết nối role `<svc>_app` trong mạng Compose (mật khẩu bootstrap là base64url nên không chèn được tham số Npgsql). */
const appConnection = (svc, passwords) => `Host=database-postgres;Database=minibox;Username=${svc}_app;Password=${passwords[`${svc}_app`]}`;

const sh = (cmd, args, { env = {}, input, cwd = root } = {}) => {
  console.log(`\n> ${[cmd, ...args].join(" ")}`);
  const r = spawnSync(cmd, args, {
    cwd, input, env: { ...process.env, ...env }, shell: process.platform === "win32" && cmd === "pnpm",
    stdio: [input === undefined ? "inherit" : "pipe", "inherit", "inherit"],
  });
  return r.status ?? 1;
};

export function smoke() {
  const dc = composeArgs(ENV);
  const secrets = join(root, "deploy", ENV, "secrets");
  const email = "smoke@minibox.vn";
  const password = randomBytes(18).toString("base64url");
  let env = { MINIBOX_TAG: ENV, SMOKE_ADMIN_PORT: PORT };
  const run = (args, extra = {}) => sh("docker", [...dc, ...args], { env: { ...env, ...extra } });
  try {
    if (run(["build"]) || run(["up", "-d", "--wait", "database-postgres", "infra-rabbitmq"])) return 1;
    process.chdir(root);
    if (bootstrap(composePsql(ENV), secrets)) return 1;
    // bootstrap đã sinh/đọc mật khẩu role; ensurePasswords chỉ đọc lại.
    const { passwords } = ensurePasswords(secrets);
    env = { ...env, IDENTITY_APP_CONNECTION: appConnection("identity", passwords), LOCALIZATION_APP_CONNECTION: appConnection("localization", passwords),
      CATALOG_APP_CONNECTION: appConnection("catalog", passwords) };
    for (const svc of BACKENDS) {
      const migrator = `Host=database-postgres;Database=minibox;Username=${svc}_migrator;Password=${passwords[`${svc}_migrator`]}`;
      if (run(["run", "--rm", "--no-deps", "-e", "MINIBOX_MIGRATOR_CONNECTION", `backend-${svc}`, "--migrate"], { MINIBOX_MIGRATOR_CONNECTION: migrator })) return 1;
    }
    const insert = `INSERT INTO identity.account (id, kind, email, password_hash) VALUES ('${randomUUID()}', 'staff', '${email}', '${argon2id(password)}');`;
    const [psql, ...psqlArgs] = composePsql(ENV);
    if (sh(psql, [...psqlArgs, "-X", "-q", "-v", "ON_ERROR_STOP=1"], { input: insert, env })) return 1;
    const services = spawnSync("docker", [...dc, "config", "--services"], { cwd: root, encoding: "utf8", env: { ...process.env, ...env } })
      .stdout.split("\n").filter((s) => s && !SKIP.includes(s));
    if (run(["up", "-d", "--wait", ...services])) return run(["logs", "--no-color", "--tail", "200"]), 1;
    const code = sh("pnpm", ["exec", "playwright", "test", "-c", "playwright.smoke.config.ts"], {
      cwd: frontend,
      env: { SMOKE_PROJECT: `minibox-${ENV}`, SMOKE_SERVICES: services.join(","), SMOKE_ADMIN_URL: `http://localhost:${PORT}`, SMOKE_EMAIL: email, SMOKE_PASSWORD: password },
    });
    if (code) run(["logs", "--no-color", "--tail", "200"]);
    return code;
  } finally {
    run(["down", "-v", "--remove-orphans"]);
  }
}

if (isMain(import.meta.url)) process.exit(smoke());

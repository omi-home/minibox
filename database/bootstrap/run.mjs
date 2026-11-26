// Chạy bootstrap DB (spec §4.3): `node database/bootstrap/run.mjs <env> [--secrets <dir>]`.
// Sinh mật khẩu cho role chưa có file secret, rồi đẩy bootstrap.sql vào psql của container database-postgres.
import { spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { composeArgs } from "../../scripts/dev.mjs";
import { isMain, root } from "../../scripts/run.mjs";

export const SERVICES = [
  "identity", "catalog", "content", "order", "payment", "minixu",
  "inventory", "farm", "purchasing", "finance", "support",
];
export const ROLES = SERVICES.flatMap((s) => [`${s}_migrator`, `${s}_app`]);

const sqlFile = join(dirname(fileURLToPath(import.meta.url)), "bootstrap.sql");

/** Thư mục secret mặc định: dev trong repo (đã .gitignore), staging/production ngoài repo (spec §8.4). */
export const defaultSecretsDir = (env) =>
  env === "dev" ? join(root, "deploy", "dev", "secrets") : `/srv/minibox/${env}/secrets`;

/**
 * Đọc mật khẩu từ `<dir>/<role>.password`; role chưa có file thì sinh ngẫu nhiên và ghi file (quyền 600).
 * @returns {{ passwords: Record<string,string>, created: string[] }}
 */
export function ensurePasswords(dir) {
  mkdirSync(dir, { recursive: true, mode: 0o700 });
  const passwords = {}, created = [];
  for (const role of ROLES) {
    const file = join(dir, `${role}.password`);
    if (!existsSync(file)) {
      writeFileSync(file, randomBytes(24).toString("base64url"), { mode: 0o600 });
      created.push(role);
    }
    passwords[role] = readFileSync(file, "utf8").trim();
  }
  return { passwords, created };
}

const lit = (s) => `'${s.replaceAll("'", "''")}'`;

/**
 * Chạy bootstrap qua `psql` (mật khẩu đi qua stdin, không lộ trên dòng lệnh).
 * @param {string[]} psql lệnh psql đã trỏ tới database minibox bằng db_admin, vd. `["docker","exec","-i",c,"psql","-U","db_admin","-d","minibox"]`
 * @param {string} secretsDir thư mục secret của môi trường
 * @returns {number} exit code của psql
 */
export function bootstrap(psql, secretsDir) {
  const { passwords, created } = ensurePasswords(secretsDir);
  const input = [
    `SELECT set_config('minibox.services', ${lit(SERVICES.join(","))}, false),`,
    `       set_config('minibox.passwords', ${lit(JSON.stringify(passwords))}, false),`,
    `       set_config('minibox.new_passwords', ${lit(created.join(","))}, false) \\g /dev/null`,
    readFileSync(sqlFile, "utf8"),
  ].join("\n");
  const [cmd, ...args] = psql;
  const r = spawnSync(cmd, [...args, "-X", "-q", "-v", "ON_ERROR_STOP=1"], { input, stdio: ["pipe", "inherit", "inherit"] });
  return r.status ?? 1;
}

/** Lệnh psql vào database minibox bằng db_admin trong container `database-postgres` của môi trường (chạy với cwd = root repo). */
export const composePsql = (env) => ["docker", ...composeArgs(env), "exec", "-T", "database-postgres",
  "sh", "-c", 'exec psql -U "$POSTGRES_USER" -d minibox "$@"', "psql"];

if (isMain(import.meta.url)) {
  const [env = "dev", ...rest] = process.argv.slice(2);
  const i = rest.indexOf("--secrets");
  const dir = i >= 0 ? rest[i + 1] : defaultSecretsDir(env);
  process.chdir(root);
  process.exit(bootstrap(composePsql(env), dir));
}

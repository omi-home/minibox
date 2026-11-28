// Nạp dữ liệu demo (spec §4.6): `ENVIRONMENT=<env> node database/seed/run.mjs [env]`.
// Chỉ dev/staging; production (hoặc ENVIRONMENT thiếu/lạ) bị từ chối trước khi chạm DB (BRD §18).
import { spawnSync } from "node:child_process";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { composePsql } from "../bootstrap/run.mjs";
import { root } from "../../scripts/run.mjs";

const ALLOWED = ["dev", "staging"];
const demoDir = join(dirname(fileURLToPath(import.meta.url)), "demo");

const declared = process.env.ENVIRONMENT ?? "";
const env = process.argv[2] ?? declared;
if (!ALLOWED.includes(declared) || env !== declared) {
  console.error(`Từ chối seed demo: ENVIRONMENT='${declared}', env='${env}'. Chỉ cho phép ${ALLOWED.join("/")} và hai giá trị phải trùng (production luôn bị chặn).`);
  process.exit(1);
}

const files = readdirSync(demoDir).filter((f) => f.endsWith(".sql")).sort();
if (!files.length) {
  console.log("Không có file seed demo.");
  process.exit(0);
}
const input = files.map((f) => readFileSync(join(demoDir, f), "utf8")).join("\n");
const [cmd, ...args] = composePsql(env);
// Một transaction cho cả bộ seed: lỗi giữa chừng không để lại dữ liệu nửa vời.
const r = spawnSync(cmd, [...args, "-X", "-q", "-1", "-v", "ON_ERROR_STOP=1"], { cwd: root, input, stdio: ["pipe", "inherit", "inherit"] });
process.exit(r.status ?? 1);

// File trạng thái đích database/schemas/<svc>/{tables,views,functions,stored-procedures,security}/ phải khớp schema sau khi chạy
// scripts/ (pre-deployment → migrations → post-deployment) — spec §4.4. Cần Docker.
import { after, before, test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { bootstrap } from "../bootstrap/run.mjs";
import { startPostgres } from "./pg.mjs";

const database = join(dirname(fileURLToPath(import.meta.url)), "..", "schemas");
// Service = thư mục dưới database/schemas/ có scripts/migrations.
const services = readdirSync(database).filter((d) => existsSync(join(database, d, "scripts", "migrations")));
const sqlFiles = (...dir) => {
  const p = join(database, ...dir);
  return existsSync(p) ? readdirSync(p).filter((f) => f.endsWith(".sql")).sort().map((f) => join(p, f)) : [];
};

const secrets = mkdtempSync(join(tmpdir(), "minibox-secrets-"));
let pg;
const pw = (role) => readFileSync(join(secrets, `${role}.password`), "utf8");
const asMigrator = (svc, q) => pg.sql(q, `${svc}_migrator`, pw(`${svc}_migrator`));

before(() => {
  pg = startPostgres("state");
  assert.equal(bootstrap(pg.psql, secrets), 0);
});

after(() => {
  pg?.stop();
  rmSync(secrets, { recursive: true, force: true });
});

/** DDL + quyền của schema `svc` (pg_dump --schema-only), bỏ dòng chú thích/SET không phụ thuộc đối tượng. */
function schemaDump(svc) {
  const r = spawnSync("docker", ["exec", pg.name, "pg_dump", "-U", "db_admin", "-d", "minibox", "--schema-only",
    "-n", svc, "-T", `${svc}.schema_versions`], { encoding: "utf8" });
  assert.equal(r.status, 0, r.stderr);
  return r.stdout.split("\n").filter((l) => l.trim() && !/^(--|\\(un)?restrict |SET |SELECT pg_catalog\.set_config)/.test(l)).join("\n");
}

/** Xoá schema rồi bootstrap lại: schema trống với quyền và default privileges như lúc mới. */
function resetSchema(svc) {
  assert.ok(pg.sql(`DROP SCHEMA "${svc}" CASCADE`).ok);
  assert.equal(bootstrap(pg.psql, secrets), 0);
}

/** Áp file trạng thái đích; file phụ thuộc file chưa áp (khóa ngoại) thì lặp lại tới khi không còn tiến triển. */
function applyState(svc) {
  // Thân function không kiểm lúc tạo: function có thể nhắc tới bảng tạo sau (giống migration 0008 → bảng có sẵn).
  const groups = [["functions"], ["tables"], ["views"], ["stored-procedures"], ["security"]];
  for (const g of groups) {
    let pending = sqlFiles(svc, ...g);
    while (pending.length) {
      const failed = pending.filter((f) => !asMigrator(svc, `SET check_function_bodies = off;\nBEGIN;\n${readFileSync(f, "utf8")}\nCOMMIT;`).ok);
      if (failed.length === pending.length)
        assert.fail(`${svc}/${g}: không áp được ${failed.map((f) => f.slice(database.length + 1)).join(", ")}: ${asMigrator(svc, `BEGIN;\n${readFileSync(failed[0], "utf8")}\nROLLBACK;`).err}`);
      pending = failed;
    }
  }
}

for (const svc of services) {
  test(`${svc}: file trạng thái đích khớp schema sau khi chạy scripts/ (pre-deployment → migrations → post-deployment)`, () => {
    for (const f of [...sqlFiles(svc, "scripts", "pre-deployment"), ...sqlFiles(svc, "scripts", "migrations"), ...sqlFiles(svc, "scripts", "post-deployment")]) {
      const r = asMigrator(svc, readFileSync(f, "utf8"));
      assert.ok(r.ok, `${f}: ${r.err}`);
    }
    const migrated = schemaDump(svc);

    resetSchema(svc);
    applyState(svc);
    const state = schemaDump(svc);

    assert.ok(migrated.includes("CREATE TABLE"), "dump rỗng — kiểm lại tên schema");
    // So theo dòng để thông báo chỉ nêu phần lệch (thứ tự dòng của pg_dump cố định theo loại + tên đối tượng).
    const a = migrated.split("\n"), b = state.split("\n");
    const diff = [...a.filter((l) => !b.includes(l)).map((l) => `- migration: ${l}`), ...b.filter((l) => !a.includes(l)).map((l) => `+ trạng thái đích: ${l}`)];
    assert.ok(state === migrated, `lệch giữa file trạng thái đích và migration (sửa tables/... hoặc thêm migration):\n${diff.join("\n") || "(cùng dòng, khác thứ tự — kiểm thứ tự cột)"}`);
  });
}

test("mỗi service có đủ thư mục kiểu SSDT", () => {
  assert.ok(services.length > 0);
  for (const svc of services)
    for (const d of ["tables", "views", "functions", "stored-procedures", "security", "scripts/pre-deployment", "scripts/post-deployment"])
      assert.ok(existsSync(join(database, svc, d)), `thiếu database/schemas/${svc}/${d}`);
});

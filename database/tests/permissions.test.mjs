// 0 quyền chéo schema giữa 11 service (spec §4.2, NFR #184). Cần Docker.
import { after, before, test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { bootstrap, ROLES, SERVICES } from "../bootstrap/run.mjs";
import { startPostgres } from "./pg.mjs";

const secrets = mkdtempSync(join(tmpdir(), "minibox-secrets-"));
let pg;
const pw = (role) => readFileSync(join(secrets, `${role}.password`), "utf8");
/** Chạy cả lô bằng `role`, không dừng ở lỗi; trả số câu bị "permission denied". */
const denied = (role, statements) =>
  (pg.sql(statements.join("\n"), role, pw(role), false).err.match(/permission denied/g) ?? []).length;

before(() => {
  pg = startPostgres("permissions");
  assert.equal(bootstrap(pg.psql, secrets), 0);
  // Mỗi service có một bảng do migrator tạo (như sau migration) để thử truy cập.
  for (const s of SERVICES) assert.ok(pg.sql("CREATE TABLE probe (id int)", `${s}_migrator`, pw(`${s}_migrator`)).ok);
});

after(() => {
  pg?.stop();
  rmSync(secrets, { recursive: true, force: true });
});

const others = (svc) => SERVICES.filter((s) => s !== svc);

test("ac184_1 role <svc>_app bị từ chối SELECT/INSERT sang schema của service khác", () => {
  for (const svc of SERVICES) {
    const stmts = others(svc).flatMap((o) => [`SELECT * FROM "${o}".probe;`, `INSERT INTO "${o}".probe VALUES (1);`]);
    assert.equal(denied(`${svc}_app`, stmts), stmts.length, `${svc}_app chạm được schema khác`);
  }
});

test("migrator cũng không chạm được schema của service khác", () => {
  for (const svc of SERVICES) {
    const stmts = others(svc).flatMap((o) => [`SELECT * FROM "${o}".probe;`, `CREATE TABLE "${o}".x (id int);`]);
    assert.equal(denied(`${svc}_migrator`, stmts), stmts.length, `${svc}_migrator chạm được schema khác`);
  }
});

test("ac184_2 role <svc>_app bị từ chối DDL ngay trong schema của mình", () => {
  for (const svc of SERVICES) {
    const stmts = [
      `CREATE TABLE "${svc}".x (id int);`,
      `ALTER TABLE "${svc}".probe ADD COLUMN y int;`,
      `DROP TABLE "${svc}".probe;`,
      `TRUNCATE "${svc}".probe;`,
    ];
    const r = pg.sql(stmts.join("\n"), `${svc}_app`, pw(`${svc}_app`), false);
    // DROP/ALTER của bảng người khác báo "must be owner"; CREATE/TRUNCATE báo "permission denied".
    assert.equal((r.err.match(/permission denied|must be owner/g) ?? []).length, stmts.length, `${svc}_app có DDL: ${r.err}`);
    assert.ok(pg.sql(`INSERT INTO "${svc}".probe VALUES (1)`, `${svc}_app`, pw(`${svc}_app`)).ok, `${svc}_app mất DML`);
  }
});

test("ac184_4 không role ứng dụng nào là superuser hoặc có quyền CREATE trên database", () => {
  const bad = pg.sql(`
    SELECT string_agg(rolname, ',') FROM pg_roles
    WHERE rolname = ANY ('{${ROLES}}')
      AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolbypassrls OR rolreplication
           OR has_database_privilege(rolname, 'minibox', 'CREATE')
           OR has_database_privilege(rolname, 'minibox', 'TEMP'))`).out;
  assert.equal(bad, "");
  assert.equal(pg.sql(`SELECT count(*) FROM pg_roles WHERE rolname = ANY ('{${ROLES}}')`).out, "22");
});

test("ac184_3 bảng append-only theo mẫu từ chối UPDATE/DELETE bằng role app, kể cả sau khi bootstrap lại", () => {
  const tpl = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "..", "templates", "append-only-table.sql"), "utf8");
  for (const svc of ["order", "finance"]) {
    const m = `${svc}_migrator`, a = `${svc}_app`;
    assert.ok(pg.sql(tpl.replaceAll("{{svc}}", svc).replaceAll("{{table}}", "audit_log"), m, pw(m)).ok);
    const insert = `INSERT INTO audit_log (id, actor, payload) VALUES (gen_random_uuid(), 'test', '{}')`;
    assert.ok(pg.sql(insert, a, pw(a)).ok, `${a} phải ghi thêm được`);
    assert.ok(pg.sql("SELECT count(*) FROM audit_log", a, pw(a)).ok, `${a} phải đọc được`);
    const forbidden = ["UPDATE audit_log SET actor = 'x';", "DELETE FROM audit_log;", "TRUNCATE audit_log;"];
    assert.equal(denied(a, forbidden), forbidden.length);
    assert.equal(bootstrap(pg.psql, secrets), 0);
    assert.equal(denied(a, forbidden), forbidden.length, "bootstrap lại đã trả quyền UPDATE/DELETE");
  }
});
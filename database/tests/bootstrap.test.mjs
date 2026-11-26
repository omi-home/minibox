// Bootstrap trên PostgreSQL 18 tạm, spec §4.1–4.3. Cần Docker.
import { after, before, test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { bootstrap, ROLES, SERVICES } from "../bootstrap/run.mjs";
import { startPostgres } from "./pg.mjs";

const secrets = mkdtempSync(join(tmpdir(), "minibox-secrets-"));
// Ảnh chụp mọi thứ bootstrap quản lý, để so lần chạy 1 và 2.
const SNAPSHOT = `
  SELECT string_agg(x, E'\\n' ORDER BY x) FROM (
    SELECT 'role ' || rolname || ' ' || coalesce(rolpassword, '') || ' ' || coalesce(array_to_string(rolconfig, ','), '')
      FROM pg_authid WHERE rolname LIKE '%\\_migrator' OR rolname LIKE '%\\_app'
    UNION ALL SELECT 'schema ' || nspname || ' ' || nspowner::regrole || ' ' || coalesce(nspacl::text, '') FROM pg_namespace
    UNION ALL SELECT 'db ' || coalesce(datacl::text, '') FROM pg_database WHERE datname = 'minibox'
    UNION ALL SELECT 'defacl ' || defaclrole::regrole || ' ' || defaclnamespace::regnamespace || ' ' || defaclacl::text FROM pg_default_acl
    UNION ALL SELECT 'ext ' || extname || ' ' || extnamespace::regnamespace FROM pg_extension) t`;

let pg, sql, psql;
before(() => {
  pg = startPostgres("bootstrap");
  ({ sql, psql } = pg);
});

after(() => {
  pg?.stop();
  rmSync(secrets, { recursive: true, force: true });
});

const pw = (role) => readFileSync(join(secrets, `${role}.password`), "utf8");

test("ac180_1 bootstrap DB sạch: 11 schema, 22 role, extensions có unaccent và pg_trgm", () => {
  assert.equal(bootstrap(psql, secrets), 0);
  const schemas = sql(`SELECT count(*) FROM pg_namespace WHERE nspname = ANY ('{${SERVICES}}')`).out;
  assert.equal(schemas, "11");
  assert.equal(sql(`SELECT count(*) FROM pg_roles WHERE rolname = ANY ('{${ROLES}}')`).out, "22");
  assert.equal(sql("SELECT string_agg(extname, ',' ORDER BY extname) FROM pg_extension WHERE extnamespace = 'extensions'::regnamespace").out,
    "pg_trgm,unaccent");
  for (const r of ROLES) assert.ok(sql("SELECT 1", r, pw(r)).ok, `${r} không đăng nhập được bằng mật khẩu trong file secret`);
});

test("ac180_2 bootstrap lần hai không lỗi và không đổi gì", () => {
  const first = sql(SNAPSHOT).out;
  assert.equal(bootstrap(psql, secrets), 0);
  assert.equal(sql(SNAPSHOT).out, first);
});

test("migrator có DDL trong schema mình; app chỉ DML, không DDL, không chạm schema khác", () => {
  const m = (q) => sql(q, "order_migrator", pw("order_migrator"));
  const a = (q, svc = "order") => sql(q, `${svc}_app`, pw(`${svc}_app`));
  assert.ok(m("CREATE TABLE t (id int)").ok);
  assert.ok(a("INSERT INTO t VALUES (1); UPDATE t SET id = 2; DELETE FROM t").ok, "app phải có DML trên bảng mới của migrator");
  assert.ok(!a("CREATE TABLE x (id int)").ok, "app không được DDL");
  assert.ok(!a("SELECT * FROM \"order\".t", "payment").ok, "app service khác không được đọc schema order");
  assert.ok(!m("CREATE TABLE public.x (id int)").ok, "không ai được ghi schema public");
  assert.ok(!a("SELECT extensions.unaccent('Ả')").ok, "service không cần tìm kiếm không thấy extensions");
  assert.equal(sql("SELECT unaccent('Rau cải')", "catalog_app", pw("catalog_app")).out, "Rau cai");
});

test("role mất file secret được đặt lại mật khẩu theo file mới sinh", () => {
  rmSync(join(secrets, "farm_app.password"));
  assert.equal(bootstrap(psql, secrets), 0);
  assert.ok(sql("SELECT 1", "farm_app", pw("farm_app")).ok);
});

// Luồng deploy.sh (spec §8.4): chạy với runner giả, không cần Docker.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, statSync, writeFileSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import { deploy, writeAppSecrets } from "./deploy.mjs";

/** Runner giả: ghi lại lệnh (kèm MINIBOX_TAG) và trả exit code theo `fail(line)`. */
const fake = (fail = () => 0) => {
  const calls = [], envs = [], owners = [];
  const exec = (cmd, args, env = {}) => {
    const line = [cmd, ...args.filter((a) => !a.includes("compose.yaml") && a !== "-f")].join(" ");
    calls.push(`${env.MINIBOX_TAG ?? "-"} ${line}`);
    envs.push(env);
    return fail(line, env);
  };
  const services = () => ["database-postgres", "infra-rabbitmq", "backend-identity", "backend-catalog", "frontend-shop"];
  const chown = (file, uid) => owners.push([basename(file), uid]);
  return { calls, envs, owners, exec, services, chown };
};
/** Thư mục trạng thái giả kèm secrets/ như sau bootstrap: mật khẩu role và rabbitmq.conf. */
const stateDir = (prev) => {
  const d = mkdtempSync(join(tmpdir(), "deploy-"));
  if (prev) writeFileSync(join(d, "deployed-tag"), prev);
  const secrets = join(d, "secrets");
  mkdirSync(secrets);
  for (const svc of ["identity", "catalog"]) for (const kind of ["app", "migrator"]) writeFileSync(join(secrets, `${svc}_${kind}.password`), `${svc}-${kind}-pw\n`);
  writeFileSync(join(secrets, "rabbitmq.conf"), "default_user = minibox\ndefault_pass = p@ss/w:rd\n");
  return d;
};
const tagOf = (d) => (existsSync(join(d, "deployed-tag")) ? readFileSync(join(d, "deployed-tag"), "utf8") : null);

test("ac220_3 production: pull → backup incr → bootstrap → migrate từng service → up → ghi tag", () => {
  const f = fake(), dir = stateDir("old");
  assert.equal(deploy("production", "new", { ...f, stateDir: dir }), 0);
  assert.deepEqual(f.calls, [
    "new docker compose pull --ignore-buildable",
    "new docker compose up -d --build --wait database-postgres",
    "new docker compose exec -T -u postgres database-postgres pgbackrest --stanza=minibox --type=incr backup",
    `new ${process.execPath} database/bootstrap/run.mjs production`,
    "new docker compose run --rm --no-deps -e MINIBOX_MIGRATOR_CONNECTION backend-identity --migrate",
    "new docker compose run --rm --no-deps -e MINIBOX_MIGRATOR_CONNECTION backend-catalog --migrate",
    "new docker compose up -d --build --wait --remove-orphans",
  ]);
  assert.equal(tagOf(dir), "new");
});

test("ac220_3 staging không backup", () => {
  const f = fake();
  assert.equal(deploy("staging", "new", { ...f, stateDir: stateDir() }), 0);
  assert.ok(!f.calls.some((c) => c.includes("pgbackrest")));
});

test("ac220_3 migration lỗi → dừng, không up stack, không đổi tag", () => {
  const f = fake((l) => (l.includes("backend-identity --migrate") ? 1 : 0)), dir = stateDir("old");
  assert.notEqual(deploy("production", "new", { ...f, stateDir: dir }), 0);
  assert.match(f.calls.at(-1), /backend-identity --migrate/);
  assert.equal(tagOf(dir), "old");
});

test("ac220_3 backup lỗi → dừng trước migrate", () => {
  const f = fake((l) => (l.includes("pgbackrest") ? 1 : 0));
  assert.notEqual(deploy("production", "new", { ...f, stateDir: stateDir("old") }), 0);
  assert.match(f.calls.at(-1), /pgbackrest/);
});

test("ac220_3 health check không đạt → quay về tag cũ, không đổi tag", () => {
  const f = fake((l, env) => (l.includes("--remove-orphans") && env.MINIBOX_TAG === "new" ? 1 : 0)), dir = stateDir("old");
  assert.notEqual(deploy("staging", "new", { ...f, stateDir: dir }), 0);
  assert.deepEqual(f.calls.slice(-2), [
    "new docker compose up -d --build --wait --remove-orphans",
    "old docker compose up -d --build --wait --remove-orphans",
  ]);
  assert.equal(tagOf(dir), "old");
});

test("ac220_3 lần deploy đầu health lỗi: không có tag cũ để quay về, báo lỗi", () => {
  const f = fake((l) => (l.includes("--remove-orphans") ? 1 : 0)), dir = stateDir();
  assert.notEqual(deploy("staging", "new", { ...f, stateDir: dir }), 0);
  assert.equal(f.calls.filter((c) => c.includes("--remove-orphans")).length, 1);
  assert.equal(tagOf(dir), null);
});

test("ac220_3 từ chối env/tag không hợp lệ trước khi chạy lệnh nào", () => {
  for (const [env, tag] of [["dev", "abc"], ["prod", "abc"], ["staging", ""], ["staging", "a;rm -rf /"], ["staging", "-x"]]) {
    const f = fake();
    assert.throws(() => deploy(env, tag, { ...f, stateDir: stateDir() }), /env|tag/);
    assert.deepEqual(f.calls, []);
  }
});

test("ac220_4 chuỗi migrator chỉ cấp cho job --migrate của đúng service, không cho stack runtime (173.37)", () => {
  const f = fake();
  assert.equal(deploy("staging", "new", { ...f, stateDir: stateDir() }), 0);
  const migrate = f.calls.map((c, i) => [c, f.envs[i]]).filter(([c]) => c.includes("--migrate"));
  assert.deepEqual(migrate.map(([, e]) => e.MINIBOX_MIGRATOR_CONNECTION), [
    "Host=database-postgres;Database=minibox;Username=identity_migrator;Password=identity-migrator-pw",
    "Host=database-postgres;Database=minibox;Username=catalog_migrator;Password=catalog-migrator-pw",
  ]);
  for (const [c, e] of f.calls.map((c, i) => [c, f.envs[i]])) if (!c.includes("--migrate")) assert.equal(e.MINIBOX_MIGRATOR_CONNECTION, undefined, c);
  for (const c of f.calls) assert.doesNotMatch(c, /migrator-pw/, "mật khẩu không nằm trên dòng lệnh");
});

test("ac220_4 sinh secret runtime từ mật khẩu bootstrap: quyền 400, chủ là uid trong image; khoá MFA/phiên giữ nguyên giữa các lần deploy", () => {
  const f = fake(), dir = join(stateDir(), "secrets");
  const services = f.services();
  writeAppSecrets(dir, services, f.chown);
  const read = (file) => readFileSync(join(dir, file), "utf8");
  assert.equal(read("identity_app.connection"), "Host=database-postgres;Database=minibox;Username=identity_app;Password=identity-app-pw");
  assert.equal(read("catalog_app.connection"), "Host=database-postgres;Database=minibox;Username=catalog_app;Password=catalog-app-pw");
  assert.equal(read("backend_rabbitmq.uri"), "amqp://minibox:p%40ss%2Fw%3Ard@infra-rabbitmq:5672");
  assert.equal(read("frontend_rabbitmq.url"), read("backend_rabbitmq.uri"));
  assert.equal(Buffer.from(read("identity_mfa.key"), "base64").length, 32);
  assert.deepEqual(new Map(f.owners), new Map([
    ["backend_rabbitmq.uri", 1654], ["frontend_rabbitmq.url", 1000], ["identity_app.connection", 1654],
    ["catalog_app.connection", 1654], ["identity_mfa.key", 1654], ["admin_session.key", 1000],
  ]));
  const [mfa, session] = [read("identity_mfa.key"), read("admin_session.key")];
  writeAppSecrets(dir, services, f.chown); // deploy lần hai: ghi lại được file quyền 400, không đổi khoá
  assert.equal(read("identity_mfa.key"), mfa);
  assert.equal(read("admin_session.key"), session);
  if (process.platform !== "win32") assert.equal(statSync(join(dir, "identity_app.connection")).mode & 0o777, 0o400);
});

test("ac220_4 thiếu mật khẩu bootstrap hoặc rabbitmq.conf thiếu trường → báo lỗi rõ", () => {
  const dir = join(stateDir(), "secrets");
  writeFileSync(join(dir, "rabbitmq.conf"), "default_user = minibox\n");
  assert.throws(() => writeAppSecrets(dir, ["backend-identity"], () => {}), /rabbitmq.conf thiếu default_pass/);
  assert.throws(() => writeAppSecrets(join(dir, "khong-co"), ["backend-identity"], () => {}), /ENOENT/);
});

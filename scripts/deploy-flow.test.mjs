// Luồng deploy.sh (spec §8.4): chạy với runner giả, không cần Docker.
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, writeFileSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { deploy } from "./deploy.mjs";

/** Runner giả: ghi lại lệnh (kèm MINIBOX_TAG) và trả exit code theo `fail(line)`. */
const fake = (fail = () => 0) => {
  const calls = [];
  const exec = (cmd, args, env = {}) => {
    const line = [cmd, ...args.filter((a) => !a.includes("compose.yaml") && a !== "-f")].join(" ");
    calls.push(`${env.MINIBOX_TAG ?? "-"} ${line}`);
    return fail(line, env);
  };
  const services = () => ["database-postgres", "infra-rabbitmq", "backend-identity", "backend-catalog"];
  return { calls, exec, services };
};
const stateDir = (prev) => {
  const d = mkdtempSync(join(tmpdir(), "deploy-"));
  if (prev) writeFileSync(join(d, "deployed-tag"), prev);
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
    "new docker compose run --rm --no-deps backend-identity --migrate",
    "new docker compose run --rm --no-deps backend-catalog --migrate",
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

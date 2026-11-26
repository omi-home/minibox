// Backup PostgreSQL bằng pgBackRest (spec §8.6, D16): image staging/production có pgBackRest, WAL archive, lịch full/diff. Cần Docker.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { root } from "./run.mjs";

const run = (cmd, args) => execFileSync(cmd, args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
const config = (env) => JSON.parse(run("docker", [...composeArgs(env), "config", "--format", "json"]));
const PG = "database-postgres";

test("ac227_1 staging/production dùng image PostgreSQL có pgBackRest, repo1 trên volume riêng", () => {
  for (const env of ["staging", "production"]) {
    const s = config(env).services[PG];
    assert.equal(s.image, "minibox/postgres-pgbackrest:18", env);
    assert.equal(s.build.dockerfile, "postgres.Dockerfile", env);
    assert.ok(s.volumes.some((v) => v.target === "/var/lib/pgbackrest" && v.source === "pgbackrest-repo"), `${env}: thiếu volume repo1`);
  }
  assert.ok(!config("dev").services[PG].image.startsWith("minibox/"), "dev không cần backup");
});

test("ac227_1 lịch full Chủ nhật, diff các ngày còn lại; giữ 2 full và đủ 14 ngày PITR", () => {
  const cron = readFileSync(join(root, "deploy", "backup", "pgbackrest.cron"), "utf8").split("\n").filter((l) => /^\d/.test(l));
  for (const env of ["staging", "production"]) {
    const jobs = cron.filter((l) => l.includes(`minibox-${env}-${PG}`)).map((l) => [l.split(/\s+/)[4], l.match(/--type=(\w+)/)[1]]);
    assert.deepEqual(jobs, [["0", "full"], ["1-6", "diff"]], env);
  }
  const conf = readFileSync(join(root, "deploy", "backup", "pgbackrest.conf"), "utf8");
  assert.match(conf, /^repo1-retention-full-type=time$/m);
  assert.match(conf, /^repo1-retention-full=14$/m);
});

test("ac227_1 pgBackRest tạo full/diff và archive WAL với archive_timeout = 300s", { timeout: 600000 }, () => {
  run("docker", [...composeArgs("production"), "build", PG]);
  const name = `minibox-pgbackrest-test-${process.pid}`;
  // Mật khẩu chỉ dùng cho container tạm của test.
  run("docker", ["run", "-d", "--name", name, "-e", "POSTGRES_USER=db_admin", "-e", "POSTGRES_DB=minibox", "-e", "POSTGRES_PASSWORD=pw", "minibox/postgres-pgbackrest:18"]);
  const exec = (...a) => run("docker", ["exec", "-u", "postgres", name, ...a]);
  const sql = (q) => exec("psql", "-U", "db_admin", "-d", "minibox", "-tAc", q).trim();
  try {
    const until = Date.now() + 120000;
    while (!run("docker", ["logs", name]).includes("PostgreSQL init process complete") || spawnSync("docker", ["exec", name, "pg_isready", "-U", "db_admin"]).status !== 0) {
      assert.ok(Date.now() < until, "PostgreSQL không lên");
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);
    }
    assert.equal(sql("SHOW archive_timeout"), "5min");
    assert.equal(sql("SHOW archive_mode"), "on");
    exec("pgbackrest", "--stanza=minibox", "--type=full", "backup");
    sql("CREATE TABLE t AS SELECT generate_series(1, 1000) AS id");
    exec("pgbackrest", "--stanza=minibox", "--type=diff", "backup");
    exec("pgbackrest", "--stanza=minibox", "check"); // ép switch WAL và xác nhận đoạn WAL vừa đóng đã vào repo
    const [info] = JSON.parse(exec("pgbackrest", "--stanza=minibox", "--output=json", "info"));
    assert.equal(info.status.code, 0, info.status.message);
    assert.deepEqual(info.backup.map((b) => b.type), ["full", "diff"]);
    assert.ok(info.archive[0]?.max, "chưa có WAL nào trong archive");
  } finally {
    spawnSync("docker", ["rm", "-f", "-v", name]);
  }
});

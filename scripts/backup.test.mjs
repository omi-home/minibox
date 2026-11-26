// Backup PostgreSQL bằng pgBackRest (spec §8.6, D16): image staging/production có pgBackRest, WAL archive, lịch full/diff, restore thử + PITR. Cần Docker.
import { after, before, describe, test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { composeArgs } from "./dev.mjs";
import { restoreTest } from "./restore-test.mjs";
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
  const name = `minibox-pgbackrest-test-${process.pid}`;
  const { exec, sql } = startPg(name);
  try {
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

/** Build image rồi chạy PostgreSQL + pgBackRest tạm (stanza tạo lúc init), chờ server thật nhận kết nối TCP. */
function startPg(name, extra = []) {
  run("docker", [...composeArgs("production"), "build", PG]);
  // Mật khẩu chỉ dùng cho container tạm của test.
  run("docker", ["run", "-d", "--name", name, ...extra, "-e", "POSTGRES_USER=db_admin", "-e", "POSTGRES_DB=minibox", "-e", "POSTGRES_PASSWORD=pw", "minibox/postgres-pgbackrest:18"]);
  const until = Date.now() + 120000;
  // Server tạm lúc init chỉ nghe socket; nghe 127.0.0.1 nghĩa là init xong.
  while (spawnSync("docker", ["exec", name, "pg_isready", "-h", "127.0.0.1", "-U", "db_admin"]).status !== 0) {
    if (Date.now() > until) throw new Error("PostgreSQL không lên");
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);
  }
  const exec = (...a) => run("docker", ["exec", "-u", "postgres", name, ...a]);
  return { exec, sql: (q) => exec("psql", "-U", "db_admin", "-d", "minibox", "-tAc", q).trim() };
}

describe("restore thử (scripts/restore-test.mjs)", { timeout: 900000 }, () => {
  const name = `minibox-restore-src-${process.pid}`, volume = `minibox-restore-test-repo-${process.pid}`;
  let result, sent, live;
  before(() => {
    const { exec, sql } = startPg(name, ["-v", `${volume}:/var/lib/pgbackrest`]);
    exec("pgbackrest", "--stanza=minibox", "--type=full", "backup");
    result = restoreTest("staging", { container: name, volume, send: (p) => (sent = p) });
    live = sql("SELECT to_regclass('public._restore_probe') IS NULL");
  });
  after(() => {
    spawnSync("docker", ["rm", "-f", "-v", name]);
    spawnSync("docker", ["volume", "rm", "-f", volume]);
  });

  test("ac227_2 khôi phục vào container tạm, số bản ghi + checksum khớp bản gốc", () => {
    assert.equal(result.ok, true, result.error);
    assert.match(result.expected, /^1000 [0-9a-f]{32}$/);
    assert.equal(result.actual, result.expected);
    assert.equal(live, "t", "bảng probe phải được dọn khỏi DB gốc");
    assert.equal(spawnSync("docker", ["inspect", result.tmp]).status, 1, "container tạm phải bị xóa");
  });

  test("ac227_3 PITR về restore point trước thao tác xóa thử lấy lại dữ liệu đã xóa", () => {
    assert.equal(result.deletedLive, "0");
    assert.equal(result.actual.split(" ")[0], "1000");
  });

  test("ac227_2 kết quả xuất thành metric OTLP (success, thời gian, tuổi backup, trạng thái pgBackRest)", () => {
    const rm = sent.resourceMetrics[0];
    assert.deepEqual(rm.resource.attributes.find((a) => a.key === "deployment.environment").value, { stringValue: "staging" });
    const m = Object.fromEntries(rm.scopeMetrics[0].metrics.map((x) => [x.name, x.gauge.dataPoints[0].asDouble]));
    assert.equal(m["minibox.backup.restore_test.success"], 1);
    assert.ok(m["minibox.backup.restore_test.duration"] > 0);
    assert.ok(m["minibox.backup.last_backup.age"] >= 0);
    assert.equal(m["minibox.backup.pgbackrest.status"], 0);
  });
});

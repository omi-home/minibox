// Restore thử + PITR hằng tuần trên staging (spec §8.6): `node scripts/restore-test.mjs <staging|production>` (lịch: deploy/backup/pgbackrest.cron).
// DB gốc: tạo bảng probe → ghi count+checksum → restore point → xóa thử → `pgbackrest check` (đẩy WAL vào repo).
// Container tạm (repo mount chỉ đọc, archive_mode=off): restore về restore point, so count+checksum. Kết quả gửi OTLP metric tới Collector.
import { execFileSync, spawnSync } from "node:child_process";
import { isMain } from "./run.mjs";

const ENVS = ["staging", "production"];
const IMAGE = "minibox/postgres-pgbackrest:18";
const PROBE = "public._restore_probe";
const CHECKSUM = `SELECT count(*) || ' ' || coalesce(md5(string_agg(id || ':' || v, ',' ORDER BY id)), '') FROM ${PROBE}`;

const docker = (...a) => execFileSync("docker", a, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
const psql = (c, q) => docker("exec", "-u", "postgres", c, "psql", "-U", "db_admin", "-d", "minibox", "-v", "ON_ERROR_STOP=1", "-tAc", q);
const sleep = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);

/**
 * @param {string} env staging | production
 * @param {{ container?: string, volume?: string, send?: (payload: object) => void }} [o] container DB gốc, volume repo1, nơi gửi metric (test thay)
 * @returns {{ ok: boolean, expected?: string, actual?: string, deletedLive?: string, tmp: string, error?: string }}
 */
export function restoreTest(env, o = {}) {
  if (!ENVS.includes(env)) throw new Error(`env phải là ${ENVS.join("|")}: ${env}`);
  const src = o.container ?? `minibox-${env}-database-postgres`;
  const volume = o.volume ?? `minibox-${env}_pgbackrest-repo`; // volume pgbackrest-repo của Compose project minibox-<env>
  const send = o.send ?? sendOtlp(env);
  const started = Date.now();
  const target = `restore-test-${started}`;
  const r = { ok: false, tmp: `minibox-${env}-restore-test-${process.pid}` };
  let info;
  try {
    psql(src, `DROP TABLE IF EXISTS ${PROBE}; CREATE TABLE ${PROBE} AS SELECT g AS id, md5(random()::text) AS v FROM generate_series(1, 1000) g`);
    r.expected = psql(src, CHECKSUM);
    psql(src, `SELECT pg_create_restore_point('${target}')`);
    psql(src, `DELETE FROM ${PROBE}`); // thao tác xóa thử
    r.deletedLive = psql(src, `SELECT count(*) FROM ${PROBE}`);
    docker("exec", "-u", "postgres", src, "pgbackrest", "--stanza=minibox", "check"); // switch WAL: restore point + xóa thử vào repo
    [info] = JSON.parse(docker("exec", "-u", "postgres", src, "pgbackrest", "--stanza=minibox", "--output=json", "info"));

    docker("run", "-d", "--name", r.tmp, "--network", "none", "-u", "postgres", "-v", `${volume}:/var/lib/pgbackrest:ro`, "--entrypoint", "sleep", IMAGE, "infinity");
    docker("exec", r.tmp, "sh", "-ec", `mkdir -p -m 700 "$PGDATA"
      pgbackrest --stanza=minibox --type=name --target=${target} --target-action=promote restore
      pg_ctl -D "$PGDATA" -l /tmp/postgres.log -w -t 300 -o "-c archive_mode=off -c listen_addresses=''" start`);
    const until = Date.now() + 300000;
    while (psql(r.tmp, "SELECT pg_is_in_recovery()") !== "f") {
      if (Date.now() > until) throw new Error("container tạm chưa xong recovery sau 5 phút");
      sleep(1000);
    }
    r.actual = psql(r.tmp, CHECKSUM);
    r.ok = r.actual === r.expected && r.expected.startsWith("1000 ");
    if (!r.ok) r.error = `checksum lệch: gốc "${r.expected}", restore "${r.actual}"`;
  } catch (e) {
    r.error = `${e.message}\n${e.stderr ?? ""}`.trim();
  } finally {
    spawnSync("docker", ["rm", "-f", "-v", r.tmp]);
    spawnSync("docker", ["exec", "-u", "postgres", src, "psql", "-U", "db_admin", "-d", "minibox", "-c", `DROP TABLE IF EXISTS ${PROBE}`]);
  }
  const last = info?.backup?.at(-1)?.timestamp?.stop;
  const metrics = {
    "minibox.backup.restore_test.success": [r.ok ? 1 : 0, "1"],
    "minibox.backup.restore_test.duration": [(Date.now() - started) / 1000, "s"],
    // Thay cho log file của pgBackRest (log-level-file=off): status ≠ 0 hoặc tuổi backup > 1 ngày là cần xem.
    ...(info ? { "minibox.backup.pgbackrest.status": [info.status.code, "1"] } : {}),
    ...(last ? { "minibox.backup.last_backup.age": [Date.now() / 1000 - last, "s"] } : {}),
  };
  try {
    send(otlpPayload(env, metrics));
  } catch (e) {
    console.error(`gửi metric lỗi: ${e.message}`);
  }
  return r;
}

/** OTLP/JSON gauge; Prometheus nhận qua Collector (deploy/observability/). */
export const otlpPayload = (env, metrics, nowMs = Date.now()) => ({
  resourceMetrics: [{
    resource: { attributes: [["service.name", "backup"], ["service.namespace", "database"], ["deployment.environment", env]].map(([key, v]) => ({ key, value: { stringValue: v } })) },
    scopeMetrics: [{
      scope: { name: "minibox.restore-test" },
      metrics: Object.entries(metrics).map(([name, [v, unit]]) => ({ name, unit, gauge: { dataPoints: [{ asDouble: v, timeUnixNano: `${BigInt(nowMs) * 1000000n}` }] } })),
    }],
  }],
});

// Mạng minibox-<env>-otel là internal (không publish 4318) → gửi từ container ngắn hạn nối vào mạng đó (busybox wget của image sẵn có).
const sendOtlp = (env) => (payload) =>
  docker("run", "--rm", "--network", `minibox-${env}-otel`, IMAGE, "wget", "-q", "-O", "/dev/null", "--header", "Content-Type: application/json",
    "--post-data", JSON.stringify(payload), "http://observability-otel-collector:4318/v1/metrics");

if (isMain(import.meta.url)) {
  try {
    const r = restoreTest(process.argv[2]);
    console.log(r.ok ? `restore thử ${process.argv[2]}: khớp ${r.actual}` : `restore thử ${process.argv[2]}: LỖI\n${r.error}`);
    process.exit(r.ok ? 0 : 1);
  } catch (e) {
    console.error(`${e.message}\nDùng: node scripts/restore-test.mjs <staging|production>`);
    process.exit(2);
  }
}

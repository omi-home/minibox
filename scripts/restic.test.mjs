// restic cho SeaweedFS + secret (scripts/restic-backup.mjs). AC4 của #227 là [manual] (đích ngoài nhà chờ OPEN-03); test này khóa phần chạy được cục bộ. Cần Docker.
import { after, before, test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { resticBackup, RESTIC_IMAGE } from "./restic-backup.mjs";
import { root } from "./run.mjs";

const MARK = "minibox-restic-plaintext-marker";
const dir = mkdtempSync(join(tmpdir(), "minibox-restic-"));
const config = join(dir, "config"), secrets = join(dir, "secrets"), volume = `minibox-restic-test-${process.pid}`;
let restic;

before(() => {
  mkdirSync(config);
  mkdirSync(secrets);
  writeFileSync(join(config, "restic.env"), "RESTIC_REPOSITORY=/config/repo\n");
  writeFileSync(join(config, "restic.password"), "test-only-password\n"); // chỉ cho repo tạm của test
  writeFileSync(join(secrets, "postgres_admin.password"), `${MARK}-secret\n`);
  execFileSync("docker", ["run", "--rm", "-v", `${volume}:/data`, "--entrypoint", "sh", RESTIC_IMAGE, "-c", `echo ${MARK}-blob > /data/1.dat`]);
  restic = resticBackup("staging", { config, volume, secrets });
  resticBackup("staging", { config, volume, secrets }); // lần 2: repo đã có, không init lại
}, { timeout: 300000 });

after(() => {
  spawnSync("docker", ["volume", "rm", "-f", volume]);
  // file repo do root trong container tạo; xóa bằng container để không kẹt quyền trên Linux
  spawnSync("docker", ["run", "--rm", "-v", `${dir}:/d`, "--entrypoint", "sh", RESTIC_IMAGE, "-c", "rm -rf /d/*"]);
  rmSync(dir, { recursive: true, force: true });
});

test("restic_backup sao lưu dữ liệu SeaweedFS và thư mục secret, khôi phục được", () => {
  const snap = JSON.parse(restic("snapshots", "--json", "latest"))[0];
  assert.deepEqual(snap.paths.sort(), ["/data/seaweedfs", "/data/secrets"]);
  assert.equal(snap.hostname, "minibox-staging");
  assert.deepEqual(snap.tags, ["staging"]);
  assert.equal(restic("dump", "latest", "/data/seaweedfs/1.dat").trim(), `${MARK}-blob`);
  assert.equal(restic("dump", "latest", "/data/secrets/postgres_admin.password").trim(), `${MARK}-secret`);
});

test("restic_backup repo được mã hóa: không có bản rõ, sai khóa không mở được", () => {
  const files = (d) => readdirSync(d).flatMap((f) => (statSync(join(d, f)).isDirectory() ? files(join(d, f)) : [join(d, f)]));
  for (const f of files(join(config, "repo"))) assert.ok(!readFileSync(f).includes(MARK), `bản rõ trong ${f}`);
  writeFileSync(join(config, "restic.password"), "wrong\n");
  assert.throws(() => restic("snapshots"));
});

test("restic_backup cron mỗi giờ cho staging và production", () => {
  const cron = readFileSync(join(root, "deploy", "backup", "restic.cron"), "utf8").split("\n").filter((l) => /^\d/.test(l));
  for (const env of ["staging", "production"]) {
    const line = cron.find((l) => l.endsWith(`restic-backup.mjs" ${env}`));
    assert.ok(line, env);
    assert.deepEqual(line.split(/\s+/).slice(1, 5), ["*", "*", "*", "*"], `${env} phải chạy mỗi giờ`);
  }
});

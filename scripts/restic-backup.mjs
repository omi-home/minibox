// restic mỗi giờ cho dữ liệu SeaweedFS + thư mục secret (spec §8.6, D16): `node scripts/restic-backup.mjs <staging|production>` (lịch: deploy/backup/restic.cron).
// Cấu hình trên homeserver ở /srv/minibox/backup/ (root, chmod 700), mẫu: deploy/backup/restic.env.example.
//   restic.env       RESTIC_REPOSITORY (đích, OPEN-03) + credential S3 nếu có
//   restic.password  khóa mã hóa repo (restic luôn mã hóa AES-256) — mất file này là mất backup, cất thêm một bản ngoài máy
// Chạy bằng container restic (không cài restic lên host); repo cục bộ /config/repo dùng tạm khi OPEN-03 chưa chốt.
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { isMain } from "./run.mjs";

const ENVS = ["staging", "production"];
export const RESTIC_IMAGE = "restic/restic:0.18.1@sha256:39d9072fb5651c80d75c7a811612eb60b4c06b32ffe87c2e9f3c7222e1797e76";

/**
 * @param {string} env staging | production
 * @param {{ config?: string, volume?: string, secrets?: string }} [o] thư mục cấu hình, volume SeaweedFS, thư mục secret (test thay)
 * @returns {(...args: string[]) => string} chạy lệnh restic với cùng cấu hình (để test khôi phục)
 */
export function resticBackup(env, o = {}) {
  if (!ENVS.includes(env)) throw new Error(`env phải là ${ENVS.join("|")}: ${env}`);
  const config = o.config ?? "/srv/minibox/backup";
  for (const f of ["restic.env", "restic.password"]) if (!existsSync(join(config, f))) throw new Error(`thiếu ${join(config, f)} (mẫu: deploy/backup/restic.env.example)`);
  const volume = o.volume ?? `minibox-${env}_seaweedfs-data`; // volume seaweedfs-data của Compose project minibox-<env>
  const secrets = o.secrets ?? `/srv/minibox/${env}/secrets`;
  const restic = (...args) => execFileSync("docker", ["run", "--rm", "--hostname", `minibox-${env}`,
    "--env-file", join(config, "restic.env"), "-e", "RESTIC_PASSWORD_FILE=/config/restic.password", "-e", "RESTIC_CACHE_DIR=/config/cache",
    "-v", `${config}:/config`, "-v", `${volume}:/data/seaweedfs:ro`, "-v", `${secrets}:/data/secrets:ro`,
    RESTIC_IMAGE, ...args], { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

  try {
    restic("cat", "config");
  } catch {
    restic("init"); // lần đầu (hoặc đích mới sau khi chốt OPEN-03)
  }
  // ponytail: SeaweedFS đang chạy được đọc nóng (file volume chủ yếu append); cần snapshot nhất quán thì `weed backup` hoặc dừng ghi.
  restic("backup", "--tag", env, "/data/seaweedfs", "/data/secrets");
  // ponytail: prune mỗi giờ tốn băng thông khi repo ở S3 xa; tách prune ra lịch ngày nếu chậm.
  restic("forget", "--tag", env, "--host", `minibox-${env}`, "--keep-hourly", "48", "--keep-daily", "14", "--keep-weekly", "8", "--prune");
  return restic;
}

if (isMain(import.meta.url)) {
  try {
    resticBackup(process.argv[2]);
    console.log(`restic ${process.argv[2]}: xong`);
  } catch (e) {
    console.error(`${e.message}\n${e.stderr ?? ""}\nDùng: node scripts/restic-backup.mjs <staging|production>`.trim());
    process.exit(1);
  }
}

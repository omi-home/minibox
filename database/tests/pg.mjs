// PostgreSQL 18 tạm cho test database (docker run, xoá khi xong). Cần Docker.
import { execFileSync, spawnSync } from "node:child_process";

/**
 * Khởi động container và chờ nhận kết nối TCP.
 * @param {string} label phân biệt container giữa các file test chạy song song
 */
export function startPostgres(label) {
  const name = `minibox-test-${label}-${process.pid}`;

  /** Chạy SQL qua TCP (kiểm mật khẩu thật) bằng `user`; mặc định db_admin. */
  const sql = (q, user = "db_admin", password = "minibox", stopOnError = true) => {
    // Qua stdin (không dùng -c): mỗi câu một transaction riêng, câu lỗi không kéo theo các câu sau.
    const r = spawnSync("docker", ["exec", "-i", "-e", `PGPASSWORD=${password}`, name, "psql", "-h", "127.0.0.1",
      "-U", user, "-d", "minibox", "-XAtq", "-v", `ON_ERROR_STOP=${stopOnError ? 1 : 0}`], { encoding: "utf8", input: q });
    return { ok: r.status === 0, out: r.stdout.trim(), err: r.stderr };
  };

  execFileSync("docker", ["run", "-d", "--rm", "--name", name, "-e", "POSTGRES_USER=db_admin",
    "-e", "POSTGRES_PASSWORD=minibox", "-e", "POSTGRES_DB=minibox", "postgres:18-alpine"]);
  // Image chạy initdb trên socket rồi khởi động lại; TCP nhận kết nối nghĩa là đã sẵn sàng.
  for (let i = 0; i < 60 && !sql("SELECT 1").ok; i++) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);

  return {
    sql,
    /** Lệnh psql qua socket bằng db_admin, dùng cho `bootstrap()`. */
    psql: ["docker", "exec", "-i", name, "psql", "-U", "db_admin", "-d", "minibox"],
    stop: () => spawnSync("docker", ["rm", "-f", name]),
  };
}

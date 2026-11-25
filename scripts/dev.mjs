// Dựng môi trường dev bằng Compose và chờ health check (spec §8.2). Tham số thêm chuyển thẳng cho `docker compose`.
import { join } from "node:path";
import { root, runAll } from "./run.mjs";

const files = ["-f", join("deploy", "compose.yaml"), "-f", join("deploy", "compose.dev.yaml")];
const extra = process.argv.slice(2);
const args = ["compose", ...files, ...(extra.length ? extra : ["up", "-d", "--wait"])];
process.exit(runAll([{ cmd: "docker", args, cwd: root }]));

// Kiểm thay đổi phá vỡ OpenAPI so với develop bằng oasdiff (spec §6.3, D17). Phá vỡ chỉ được phép khi tăng major của info.version.
// Chạy: node contracts/breaking.mjs [ref]   (mặc định origin/develop, hoặc env OPENAPI_BASE_REF). Cần Docker.
import { execFileSync, spawnSync } from "node:child_process";
import { mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { isMain, root } from "../scripts/run.mjs";

export const OASDIFF_IMAGE = "tufin/oasdiff:v1.41.0";
const major = (spec) => Number.parseInt(String(spec?.info?.version ?? "0"), 10) || 0;

/** So hai spec (object). Trả danh sách lỗi: rỗng khi không phá vỡ hoặc đã tăng major. */
export function breakingErrors(base, head) {
  if (major(head) > major(base)) return [];
  const dir = mkdtempSync(join(tmpdir(), "oasdiff-"));
  try {
    writeFileSync(join(dir, "base.json"), JSON.stringify(base));
    writeFileSync(join(dir, "head.json"), JSON.stringify(head));
    const r = spawnSync(
      "docker",
      ["run", "--rm", "-v", `${dir}:/w:ro`, OASDIFF_IMAGE, "breaking", "/w/base.json", "/w/head.json", "--fail-on", "ERR"],
      { encoding: "utf8" },
    );
    if (r.status === 0) return [];
    if (r.status === 1 && /\berror\b/.test(r.stdout)) return [r.stdout.trim()];
    throw new Error(`oasdiff lỗi (exit ${r.status}): ${r.error?.message ?? ""}${r.stderr}`);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

const git = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

/** Đảm bảo ref có trong repo (CI checkout nông chưa có origin/develop). */
function ensureRef(ref) {
  try {
    git("rev-parse", "--verify", "--quiet", `${ref}^{commit}`);
  } catch {
    const m = /^origin\/(.+)$/.exec(ref);
    if (!m) throw new Error(`Không thấy ref ${ref}`);
    git("fetch", "--depth=1", "origin", `${m[1]}:refs/remotes/${ref}`);
  }
}

/** Kiểm mọi contracts/openapi/*.json so với ref. Service chưa có trên ref thì bỏ qua. Trả { svc: lỗi[] } chỉ gồm service phá vỡ. */
export function checkAgainst(ref = process.env.OPENAPI_BASE_REF || "origin/develop") {
  ensureRef(ref);
  const failures = {};
  for (const file of readdirSync(join(root, "contracts", "openapi")).filter((f) => f.endsWith(".json"))) {
    let base;
    try {
      base = JSON.parse(git("show", `${ref}:contracts/openapi/${file}`));
    } catch {
      continue; // service mới
    }
    const errors = breakingErrors(base, JSON.parse(readFileSync(join(root, "contracts", "openapi", file), "utf8")));
    if (errors.length) failures[file] = errors;
  }
  return failures;
}

if (isMain(import.meta.url)) {
  const failures = checkAgainst(process.argv[2]);
  for (const [file, errors] of Object.entries(failures)) console.error(`${file}: thay đổi phá vỡ mà chưa tăng major info.version\n${errors.join("\n")}`);
  process.exit(Object.keys(failures).length ? 1 : 0);
}

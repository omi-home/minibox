// Chạy SAU khi build backend (scripts/test.mjs, scripts/accept.mjs): build ghi lại contracts/openapi/<svc>.json từ code (spec §6.3).
// File khác bản đã commit/stage (hoặc chưa được git theo dõi) nghĩa là hợp đồng lệch code → fail.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { existsSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { root } from "../../scripts/run.mjs";

const git = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8" });
const services = readdirSync(join(root, "src", "backend", "services"), { withFileTypes: true })
  .filter((d) => d.isDirectory() && existsSync(join(root, "src", "backend", "services", d.name, "src")))
  .map((d) => d.name)
  .filter((svc) => existsSync(join(root, "src", "backend", "services", svc, "src", `Minibox.${svc[0].toUpperCase()}${svc.slice(1)}.Api`)));

test("ac212_1 mỗi service có Api đều có OpenAPI sinh lúc build trong contracts/openapi", () => {
  assert.ok(services.includes("identity"));
  for (const svc of services) assert.ok(existsSync(join(root, "contracts", "openapi", `${svc}.json`)), `thiếu contracts/openapi/${svc}.json`);
});

test("ac212_1 OpenAPI đã commit khớp code (build không làm đổi contracts/openapi)", () => {
  const untracked = git("ls-files", "--others", "--exclude-standard", "--", "contracts/openapi").trim();
  assert.equal(untracked, "", `OpenAPI chưa commit:\n${untracked}`);
  const diff = git("diff", "--stat", "--", "contracts/openapi").trim();
  assert.equal(diff, "", `OpenAPI lệch code — build đã sinh lại, hãy commit:\n${diff}`);
});

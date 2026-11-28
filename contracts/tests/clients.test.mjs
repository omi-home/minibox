// Chạy SAU build backend và pnpm install (scripts/test.mjs, scripts/accept.mjs): sinh lại client TS từ contracts/openapi
// rồi so với bản đã commit/stage. Khác nghĩa là client lệch hợp đồng → chạy `pnpm -C src/frontend --filter @minibox/api-clients gen` và commit.
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readdirSync } from "node:fs";
import { join } from "node:path";
import { root } from "../../scripts/run.mjs";

const pkg = join(root, "src", "frontend", "packages", "api-clients");
const generated = "src/frontend/packages/api-clients/src/generated";
const git = (...args) => execFileSync("git", args, { cwd: root, encoding: "utf8" }).trim();

test("ac212_3 client TS sinh lại khớp bản đã commit (mỗi OpenAPI một file client)", () => {
  execFileSync(process.execPath, [join(pkg, "scripts", "gen.mjs")], { cwd: pkg, stdio: "pipe" });
  const specs = readdirSync(join(root, "contracts", "openapi")).filter((f) => f.endsWith(".json")).map((f) => f.replace(/\.json$/, ".ts"));
  assert.deepEqual(readdirSync(join(root, generated)).sort(), specs.sort());
  assert.equal(git("ls-files", "--others", "--exclude-standard", "--", generated), "", "client sinh chưa commit");
  const diff = git("diff", "--stat", "--", generated);
  assert.equal(diff, "", `client TS lệch contracts/openapi — đã sinh lại, hãy commit:\n${diff}`);
});

import { test } from "node:test";
import assert from "node:assert/strict";
import { existsSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { runAll } from "./run.mjs";
import { acceptSteps } from "./accept.mjs";

const node = (code) => ({ cmd: process.execPath, args: ["-e", code] });

test("ac174_2 runAll trả exit ≠ 0 khi một bước fail và dừng ở bước đó", () => {
  const marker = join(mkdtempSync(join(tmpdir(), "minibox-")), "ran");
  const touch = `require("fs").writeFileSync(${JSON.stringify(marker)}, "")`;
  assert.equal(runAll([node(""), node("process.exit(3)"), node(touch)]), 3);
  assert.equal(existsSync(marker), false);
});

test("ac174_2 runAll trả 0 khi mọi bước pass", () => {
  assert.equal(runAll([node(""), node("")]), 0);
});

test("ac174_3 accept chỉ lọc test có tên chứa ac<n>_ và in tên test", () => {
  const steps = acceptSteps("42");
  const all = steps.map((s) => [s.cmd, ...s.args].join(" ")).join("\n");
  assert.match(all, /--filter-method \*ac42_\*/);
  assert.match(all, /--output detailed/);
  assert.match(all, /--test-name-pattern ac42_/);
  assert.match(all, /pnpm -r test -t ac42_/);
  assert.doesNotMatch(all, /ac4_|ac420_/);
});

test("ac174_3 accept từ chối n không phải số", () => {
  assert.throws(() => acceptSteps("1; rm -rf /"));
});
